using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace SboxPro.Docs;

/// <summary>
/// Crawls the S&Box wiki at docs.facepunch.com via the Outline API
/// (POST shares.info → POST documents.info per doc id). Caches markdown.
/// </summary>
public static class WikiClient
{
	private const string ShareId = "sbox-dev";
	private const string OutlineApi = "https://docs.facepunch.com/api";
	private const string DocsBase = "https://docs.facepunch.com/s/sbox-dev";
	private const int RequestDelayMs = 75;
	private const int RequestTimeoutMs = 15000;

	private static readonly HttpClient _http = CreateClient();

	private static HttpClient CreateClient()
	{
		var c = new HttpClient { Timeout = TimeSpan.FromMilliseconds( RequestTimeoutMs ) };
		c.DefaultRequestHeaders.Add( "User-Agent", "sbox-pro-docs/1.0" );
		return c;
	}

	public sealed class CrawlStats
	{
		public int Crawled;
		public int Failed;
		public int FromCache;
		public int Total;
		public string Error;
	}

	public static async Task<CrawlStats> CrawlAllAsync( CancellationToken ct = default )
	{
		var stats = new CrawlStats();
		DocsCache.EnsureLoaded();

		// 1. shares.info — get the doc tree
		var tree = await PostAsync( "shares.info", new JsonObject { ["id"] = ShareId }, ct );
		if ( tree == null )
		{
			stats.Error = "Could not load doc tree from docs.facepunch.com (shares.info failed).";
			return stats;
		}

		string shareUuid = null;
		if ( tree["shares"] is JsonArray shares && shares.Count > 0 )
			shareUuid = shares[0]?["id"]?.GetValue<string>();
		var shared = tree["sharedTree"] as JsonObject;
		if ( shareUuid == null || shared == null )
		{
			stats.Error = "Doc tree response was malformed (missing shares[0].id or sharedTree).";
			return stats;
		}

		var flat = new List<(string id, string title, string path, string url)>();
		Flatten( shared, "", flat );
		stats.Total = flat.Count;

		// 2. documents.info per leaf
		foreach ( var doc in flat )
		{
			ct.ThrowIfCancellationRequested();
			var fullUrl = DocsBase + doc.url;

			// Skip if cached and fresh
			if ( DocsCache.Pages.TryGetValue( fullUrl, out var cached )
				&& (DocsCache.NowUnix() - cached.FetchedAtUnix) < DocsCache.TtlSeconds() )
			{
				stats.FromCache++;
				continue;
			}

			var docInfo = await PostAsync( "documents.info", new JsonObject
			{
				["id"] = doc.id,
				["shareId"] = shareUuid
			}, ct );

			if ( docInfo == null )
			{
				stats.Failed++;
				await Task.Delay( RequestDelayMs, ct );
				continue;
			}

			var text = docInfo["text"]?.GetValue<string>();
			if ( string.IsNullOrEmpty( text ) || text.Length < 10 )
			{
				stats.Failed++;
				await Task.Delay( RequestDelayMs, ct );
				continue;
			}

			DocsCache.SetPage( new CachedDocPage
			{
				Url = fullUrl,
				Title = docInfo["title"]?.GetValue<string>() ?? doc.title,
				Category = ExtractCategory( doc.path ),
				Markdown = text,
				FetchedAtUnix = DocsCache.NowUnix()
			} );
			stats.Crawled++;
			await Task.Delay( RequestDelayMs, ct );
		}

		DocsCache.MarkWikiCrawled();
		DocsCache.Save();
		return stats;
	}

	private static void Flatten( JsonObject node, string parentPath, List<(string id, string title, string path, string url)> result )
	{
		var title = node["title"]?.GetValue<string>() ?? "";
		var currentPath = string.IsNullOrEmpty( parentPath ) ? title : $"{parentPath}/{title}";

		var id = node["id"]?.GetValue<string>();
		var url = node["url"]?.GetValue<string>();
		if ( !string.IsNullOrEmpty( id ) && !string.IsNullOrEmpty( url ) )
			result.Add( (id, title, currentPath, url) );

		if ( node["children"] is JsonArray kids )
		{
			foreach ( var k in kids )
				if ( k is JsonObject kobj ) Flatten( kobj, currentPath, result );
		}
	}

	private static string ExtractCategory( string path )
	{
		var parts = path.Split( '/', StringSplitOptions.RemoveEmptyEntries );
		return parts.Length >= 2 ? parts[1] : "root";
	}

	private static async Task<JsonObject> PostAsync( string endpoint, JsonObject body, CancellationToken ct )
	{
		try
		{
			var url = $"{OutlineApi}/{endpoint}";
			var content = new StringContent( body.ToJsonString(), Encoding.UTF8, "application/json" );
			using var resp = await _http.PostAsync( url, content, ct );
			if ( !resp.IsSuccessStatusCode ) return null;
			var raw = await resp.Content.ReadAsStringAsync( ct );
			var parsed = JsonNode.Parse( raw ) as JsonObject;
			return parsed?["data"] as JsonObject;
		}
		catch ( OperationCanceledException ) { throw; }
		catch { return null; }
	}
}
