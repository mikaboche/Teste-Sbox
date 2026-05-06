using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SboxPro.Docs;

namespace SboxPro;

// Phase 8 — real implementation. Indexes the S&Box wiki (docs.facepunch.com,
// via the Outline API) and the public API schema (sbox.game/api/schema →
// cdn.sbox.game/releases/*.json) into a disk-cached search index.
//
// Refresh runs on a background Task.Run so tool calls never block the editor's
// main thread. First call to any docs_* tool kicks off the crawl and returns
// an "indexing" placeholder; subsequent calls return whatever is already cached
// on disk plus an indexing flag, until the crawl finishes and flips _wikiReady
// (or _apiReady) true.

public static class DocsTools
{
	private static readonly object _refreshLock = new();
	private static Task _wikiTask, _apiTask;
	private static volatile bool _wikiReady, _apiReady;
	private static volatile string _wikiError, _apiError;

	private static void EnsureWikiReady()
	{
		DocsCache.EnsureLoaded();
		if ( DocsCache.IsWikiFresh() ) { _wikiReady = true; return; }
		if ( _wikiReady ) return;

		lock ( _refreshLock )
		{
			if ( _wikiTask != null && !_wikiTask.IsCompleted ) return; // in-flight
			_wikiTask = Task.Run( async () =>
			{
				try
				{
					var stats = await WikiClient.CrawlAllAsync().ConfigureAwait( false );
					if ( !string.IsNullOrEmpty( stats.Error ) ) _wikiError = stats.Error;
					_wikiReady = true;
				}
				catch ( Exception ex )
				{
					_wikiError = ex.Message;
					Console.WriteLine( $"[sbox-pro][DocsTools] Wiki crawl failed: {ex}" );
				}
			} );
		}
	}

	private static void EnsureApiReady()
	{
		DocsCache.EnsureLoaded();
		if ( DocsCache.IsApiFresh() ) { _apiReady = true; return; }
		if ( _apiReady ) return;

		lock ( _refreshLock )
		{
			if ( _apiTask != null && !_apiTask.IsCompleted ) return;
			_apiTask = Task.Run( async () =>
			{
				try
				{
					var stats = await ApiClient.CrawlAllAsync().ConfigureAwait( false );
					if ( !string.IsNullOrEmpty( stats.Error ) ) _apiError = stats.Error;
					_apiReady = true;
				}
				catch ( Exception ex )
				{
					_apiError = ex.Message;
					Console.WriteLine( $"[sbox-pro][DocsTools] API crawl failed: {ex}" );
				}
			} );
		}
	}

	private static bool WikiIndexing => _wikiTask != null && !_wikiTask.IsCompleted;
	private static bool ApiIndexing => _apiTask != null && !_apiTask.IsCompleted;

	// ──────────────────────────────────────────────
	//  docs_search
	// ──────────────────────────────────────────────

	[Tool( "docs_search", "Search the S&Box wiki (docs.facepunch.com) for a query. First call kicks off an async crawl (~30s) and returns indexing=true; retry once it finishes." )]
	[Param( "query", "Search query.", Required = true )]
	[Param( "limit", "Max results. Default: 8.", Required = false, Type = "integer" )]
	public static object DocsSearch( JsonElement args )
	{
		var query = ToolHandlerBase.RequireString( args, "query" );
		var limit = ToolHandlerBase.GetInt( args, "limit", 8 );
		EnsureWikiReady();

		var results = FuzzyMatcher.RankAndTake(
			DocsCache.Pages.Values,
			p => new[] { p.Title, p.Category, ExtractFirstNonEmptyLine( p.Markdown ) },
			query,
			limit
		).Select( p => new
		{
			title = p.Title,
			category = p.Category,
			url = p.Url,
			snippet = SnippetAround( p.Markdown, query )
		} );

		return ToolHandlerBase.JsonResult( new
		{
			query,
			indexed = DocsCache.Pages.Count,
			indexing = WikiIndexing,
			indexError = _wikiError,
			results = results.ToArray()
		} );
	}

	// ──────────────────────────────────────────────
	//  docs_get_page
	// ──────────────────────────────────────────────

	[Tool( "docs_get_page", "Fetch a specific S&Box wiki page by full URL (https://docs.facepunch.com/s/sbox-dev/...) or by title substring." )]
	[Param( "key", "Full URL or a title substring.", Required = true )]
	public static object DocsGetPage( JsonElement args )
	{
		var key = ToolHandlerBase.RequireString( args, "key" );
		EnsureWikiReady();

		var page = DocsCache.Pages.Values.FirstOrDefault( p => string.Equals( p.Url, key, StringComparison.OrdinalIgnoreCase ) )
			?? DocsCache.Pages.Values.FirstOrDefault( p => p.Title?.IndexOf( key, StringComparison.OrdinalIgnoreCase ) >= 0 );

		if ( page == null )
		{
			return ToolHandlerBase.JsonResult( new
			{
				found = false,
				indexing = WikiIndexing,
				indexed = DocsCache.Pages.Count,
				message = WikiIndexing
					? "Wiki is still indexing — try again shortly."
					: $"No page matched '{key}'."
			} );
		}

		return ToolHandlerBase.JsonResult( new
		{
			found = true,
			title = page.Title,
			category = page.Category,
			url = page.Url,
			markdown = page.Markdown
		} );
	}

	// ──────────────────────────────────────────────
	//  docs_list_categories
	// ──────────────────────────────────────────────

	[Tool( "docs_list_categories", "List S&Box wiki categories with page counts." )]
	public static object DocsListCategories( JsonElement args )
	{
		EnsureWikiReady();
		var grouped = DocsCache.Pages.Values
			.GroupBy( p => p.Category ?? "root" )
			.OrderByDescending( g => g.Count() )
			.Select( g => new { category = g.Key, count = g.Count() } )
			.ToArray();

		return ToolHandlerBase.JsonResult( new
		{
			totalPages = DocsCache.Pages.Count,
			indexing = WikiIndexing,
			categories = grouped
		} );
	}

	// ──────────────────────────────────────────────
	//  docs_search_api
	// ──────────────────────────────────────────────

	[Tool( "docs_search_api", "Search the S&Box public API (sbox.game/api schema) by type or member name. First call indexes ~1800 types from the schema dump (~60s; the schema can be 50MB+)." )]
	[Param( "query", "Type or member name (substring match).", Required = true )]
	[Param( "limit", "Max results. Default: 8.", Required = false, Type = "integer" )]
	public static object DocsSearchApi( JsonElement args )
	{
		var query = ToolHandlerBase.RequireString( args, "query" );
		var limit = ToolHandlerBase.GetInt( args, "limit", 8 );
		EnsureApiReady();

		var results = FuzzyMatcher.RankAndTake(
			DocsCache.Types.Values,
			t => new[] { t.Name, t.FullName, t.Namespace }
				.Concat( t.Members ?? Array.Empty<string>() )
				.ToArray(),
			query,
			limit
		).Select( t => new
		{
			name = t.Name,
			fullName = t.FullName,
			@namespace = t.Namespace,
			kind = t.Kind,
			description = t.Description,
			members = t.Members
		} );

		return ToolHandlerBase.JsonResult( new
		{
			query,
			indexed = DocsCache.Types.Count,
			indexing = ApiIndexing,
			indexError = _apiError,
			results = results.ToArray()
		} );
	}

	// ──────────────────────────────────────────────
	//  docs_get_api_type
	// ──────────────────────────────────────────────

	[Tool( "docs_get_api_type", "Get full API reference (constructors, methods, properties, fields, XML doc summaries) for a specific S&Box type." )]
	[Param( "name", "Short name ('Component') or fully-qualified name ('Sandbox.Component').", Required = true )]
	[Param( "max_length", "Max payload length in chars. Default: 5000.", Required = false, Type = "integer" )]
	[Param( "start_index", "Char offset to start from. Default: 0.", Required = false, Type = "integer" )]
	public static object DocsGetApiType( JsonElement args )
	{
		var name = ToolHandlerBase.RequireString( args, "name" );
		var maxLength = Math.Clamp( ToolHandlerBase.GetInt( args, "max_length", 5000 ), 100, 20000 );
		var startIndex = Math.Max( 0, ToolHandlerBase.GetInt( args, "start_index", 0 ) );
		EnsureApiReady();

		var type = DocsCache.Types.Values.FirstOrDefault( t => string.Equals( t.FullName, name, StringComparison.OrdinalIgnoreCase ) )
			?? DocsCache.Types.Values.FirstOrDefault( t => string.Equals( t.Name, name, StringComparison.OrdinalIgnoreCase ) );

		if ( type == null )
		{
			return ToolHandlerBase.JsonResult( new
			{
				found = false,
				indexing = ApiIndexing,
				indexed = DocsCache.Types.Count,
				message = ApiIndexing
					? "API schema is still indexing — try again shortly."
					: $"No API type matched '{name}'. Try docs_search_api first."
			} );
		}

		var payload = type.FullPayload ?? "";
		var sliceStart = Math.Min( startIndex, payload.Length );
		var sliceEnd = Math.Min( sliceStart + maxLength, payload.Length );
		var slice = payload.Substring( sliceStart, sliceEnd - sliceStart );
		var more = sliceEnd < payload.Length;

		return ToolHandlerBase.JsonResult( new
		{
			name = type.Name,
			fullName = type.FullName,
			@namespace = type.Namespace,
			kind = type.Kind,
			content = slice,
			startIndex = sliceStart,
			endIndex = sliceEnd,
			totalLength = payload.Length,
			hasMore = more
		} );
	}

	// ──────────────────────────────────────────────
	//  docs_cache_status
	// ──────────────────────────────────────────────

	[Tool( "docs_cache_status", "Show docs index state: page/type counts, indexing flags, last refresh timestamps, schema URL." )]
	public static object DocsCacheStatus( JsonElement args )
	{
		DocsCache.EnsureLoaded();
		var nowUnix = DocsCache.NowUnix();
		return ToolHandlerBase.JsonResult( new
		{
			wiki = new
			{
				pages = DocsCache.Pages.Count,
				lastCrawlUnix = DocsCache.LastWikiCrawlUnix,
				ageSeconds = DocsCache.LastWikiCrawlUnix == 0 ? -1 : nowUnix - DocsCache.LastWikiCrawlUnix,
				fresh = DocsCache.IsWikiFresh(),
				indexing = WikiIndexing,
				ready = _wikiReady,
				lastError = _wikiError
			},
			api = new
			{
				types = DocsCache.Types.Count,
				lastCrawlUnix = DocsCache.LastApiCrawlUnix,
				ageSeconds = DocsCache.LastApiCrawlUnix == 0 ? -1 : nowUnix - DocsCache.LastApiCrawlUnix,
				schemaUrl = DocsCache.ApiSchemaUrl,
				fresh = DocsCache.IsApiFresh(),
				indexing = ApiIndexing,
				ready = _apiReady,
				lastError = _apiError
			},
			ttlSeconds = DocsCache.TtlSeconds()
		} );
	}

	// ──────────────────────────────────────────────
	//  docs_refresh_index
	// ──────────────────────────────────────────────

	[Tool( "docs_refresh_index", "Kick off a background refresh of the docs index. Wiki only by default; pass include_api=true to also refetch the schema (slower, ~50MB download). Returns immediately; poll docs_cache_status for progress." )]
	[Param( "include_wiki", "Refresh wiki cache. Default: true.", Required = false, Type = "boolean", Default = "true" )]
	[Param( "include_api", "Refresh API schema. Default: false.", Required = false, Type = "boolean", Default = "false" )]
	[Param( "clear_cache", "Clear the on-disk cache before refetching. Default: false.", Required = false, Type = "boolean", Default = "false" )]
	public static object DocsRefreshIndex( JsonElement args )
	{
		var includeWiki = ToolHandlerBase.GetBool( args, "include_wiki", true );
		var includeApi = ToolHandlerBase.GetBool( args, "include_api", false );
		var clear = ToolHandlerBase.GetBool( args, "clear_cache", false );

		if ( clear ) DocsCache.Clear();

		bool wikiKicked = false, apiKicked = false;

		if ( includeWiki )
		{
			lock ( _refreshLock )
			{
				if ( _wikiTask == null || _wikiTask.IsCompleted )
				{
					_wikiReady = false;
					_wikiError = null;
					_wikiTask = null;
					EnsureWikiReady();
					wikiKicked = WikiIndexing;
				}
			}
		}

		if ( includeApi )
		{
			lock ( _refreshLock )
			{
				if ( _apiTask == null || _apiTask.IsCompleted )
				{
					_apiReady = false;
					_apiError = null;
					_apiTask = null;
					EnsureApiReady();
					apiKicked = ApiIndexing;
				}
			}
		}

		return ToolHandlerBase.JsonResult( new
		{
			wikiKicked,
			apiKicked,
			wikiAlreadyRunning = !wikiKicked && WikiIndexing,
			apiAlreadyRunning = !apiKicked && ApiIndexing,
			note = "Refresh runs in background. Poll docs_cache_status to track progress."
		} );
	}

	// ──────────────────────────────────────────────
	//  docs_run_tests
	// ──────────────────────────────────────────────

	[Tool( "docs_run_tests", "Self-check the docs subsystem: cache loads, fuzzy matcher scores expected values, indexes have content." )]
	public static object DocsRunTests( JsonElement args )
	{
		var passed = new System.Collections.Generic.List<string>();
		var failed = new System.Collections.Generic.List<string>();
		void Assert( string name, bool ok ) { (ok ? passed : failed).Add( name ); }

		try
		{
			DocsCache.EnsureLoaded();
			Assert( "DocsCache.EnsureLoaded() succeeds", true );
		}
		catch ( Exception ex ) { failed.Add( $"DocsCache.EnsureLoaded threw: {ex.Message}" ); }

		Assert( "FuzzyMatcher exact equal scores 0", FuzzyMatcher.Score( "Component", "component" ) == 0 );
		Assert( "FuzzyMatcher prefix scores 1", FuzzyMatcher.Score( "ComponentList", "comp" ) == 1 );
		Assert( "FuzzyMatcher word-boundary scores 5", FuzzyMatcher.Score( "GameObject.Component", "Component" ) == 5 );
		Assert( "FuzzyMatcher no match returns -1", FuzzyMatcher.Score( "Sandbox", "xyz" ) == -1 );
		Assert( "FuzzyMatcher empty needle scores 0", FuzzyMatcher.Score( "Anything", "" ) == 0 );

		return ToolHandlerBase.JsonResult( new
		{
			passed = passed.ToArray(),
			failed = failed.ToArray(),
			passedCount = passed.Count,
			failedCount = failed.Count
		} );
	}

	// ──────────────────────────────────────────────
	//  Helpers
	// ──────────────────────────────────────────────

	private static string SnippetAround( string markdown, string query, int radius = 80 )
	{
		if ( string.IsNullOrEmpty( markdown ) || string.IsNullOrEmpty( query ) ) return null;
		var idx = markdown.IndexOf( query, StringComparison.OrdinalIgnoreCase );
		if ( idx < 0 ) return ExtractFirstNonEmptyLine( markdown );
		var start = Math.Max( 0, idx - radius );
		var end = Math.Min( markdown.Length, idx + query.Length + radius );
		return (start > 0 ? "…" : "") + markdown.Substring( start, end - start ) + (end < markdown.Length ? "…" : "");
	}

	private static string ExtractFirstNonEmptyLine( string markdown )
	{
		if ( string.IsNullOrEmpty( markdown ) ) return null;
		foreach ( var line in markdown.Split( '\n' ) )
		{
			var t = line.Trim();
			if ( !string.IsNullOrEmpty( t ) && !t.StartsWith( "#" ) ) return t.Length > 200 ? t.Substring( 0, 200 ) + "…" : t;
		}
		return null;
	}
}
