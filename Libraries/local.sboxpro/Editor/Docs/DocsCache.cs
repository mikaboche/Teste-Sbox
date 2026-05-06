using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SboxPro.Docs;

public sealed class CachedDocPage
{
	public string Url { get; set; }
	public string Title { get; set; }
	public string Category { get; set; }
	public string Markdown { get; set; }
	public long FetchedAtUnix { get; set; }
}

public sealed class CachedApiType
{
	public string Name { get; set; }
	public string FullName { get; set; }
	public string Namespace { get; set; }
	public string Kind { get; set; }       // class/struct/interface/enum
	public string Description { get; set; }
	public string BaseType { get; set; }
	public string[] Members { get; set; }  // top-N member names for search ranking
	public string FullPayload { get; set; } // pretty-printed full body for get_api_type
}

internal sealed class CacheManifest
{
	public int Version { get; set; } = 2;
	public Dictionary<string, CachedDocPage> Pages { get; set; } = new();
	public Dictionary<string, CachedApiType> Types { get; set; } = new();
	public long LastWikiCrawl { get; set; }
	public long LastApiCrawl { get; set; }
	public string ApiSchemaUrl { get; set; }
}

/// <summary>
/// Disk-backed cache for the docs index. Manifest is JSON in
/// %LOCALAPPDATA%/sbox-pro/docs-cache/manifest.json.
/// TTL defaults to 4 hours; configurable via SBOX_PRO_DOCS_TTL env var.
/// </summary>
public static class DocsCache
{
	private const int DefaultTtlSeconds = 4 * 60 * 60;
	private static readonly object _lock = new();
	private static CacheManifest _manifest;
	private static string _cacheDir;
	private static string _manifestPath;

	public static void EnsureLoaded()
	{
		if ( _manifest != null ) return;
		lock ( _lock )
		{
			if ( _manifest != null ) return;

			_cacheDir = Environment.GetEnvironmentVariable( "SBOX_PRO_DOCS_CACHE_DIR" )
				?? Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData ), "sbox-pro", "docs-cache" );
			_manifestPath = Path.Combine( _cacheDir, "manifest.json" );

			Directory.CreateDirectory( _cacheDir );

			if ( File.Exists( _manifestPath ) )
			{
				try
				{
					var json = File.ReadAllText( _manifestPath );
					var loaded = JsonSerializer.Deserialize<CacheManifest>( json );
					if ( loaded != null && loaded.Version == new CacheManifest().Version )
						_manifest = loaded;
				}
				catch { /* corrupt — start fresh */ }
			}

			_manifest ??= new CacheManifest();
		}
	}

	public static long NowUnix() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

	public static long TtlSeconds()
	{
		var raw = Environment.GetEnvironmentVariable( "SBOX_PRO_DOCS_TTL" );
		return long.TryParse( raw, out var v ) ? v : DefaultTtlSeconds;
	}

	public static bool IsWikiFresh()
	{
		EnsureLoaded();
		return _manifest.LastWikiCrawl != 0 && (NowUnix() - _manifest.LastWikiCrawl) < TtlSeconds();
	}

	public static bool IsApiFresh()
	{
		EnsureLoaded();
		return _manifest.LastApiCrawl != 0 && (NowUnix() - _manifest.LastApiCrawl) < TtlSeconds();
	}

	public static IReadOnlyDictionary<string, CachedDocPage> Pages
	{
		get { EnsureLoaded(); return _manifest.Pages; }
	}

	public static IReadOnlyDictionary<string, CachedApiType> Types
	{
		get { EnsureLoaded(); return _manifest.Types; }
	}

	public static string ApiSchemaUrl
	{
		get { EnsureLoaded(); return _manifest.ApiSchemaUrl; }
	}

	public static long LastWikiCrawlUnix { get { EnsureLoaded(); return _manifest.LastWikiCrawl; } }
	public static long LastApiCrawlUnix { get { EnsureLoaded(); return _manifest.LastApiCrawl; } }

	public static void SetPage( CachedDocPage page )
	{
		EnsureLoaded();
		lock ( _lock ) _manifest.Pages[page.Url] = page;
	}

	public static void SetApiType( CachedApiType type )
	{
		EnsureLoaded();
		lock ( _lock ) _manifest.Types[type.FullName ?? type.Name] = type;
	}

	public static void MarkWikiCrawled()
	{
		EnsureLoaded();
		_manifest.LastWikiCrawl = NowUnix();
	}

	public static void MarkApiCrawled( string schemaUrl )
	{
		EnsureLoaded();
		_manifest.LastApiCrawl = NowUnix();
		_manifest.ApiSchemaUrl = schemaUrl;
	}

	public static void Clear()
	{
		lock ( _lock )
		{
			_manifest = new CacheManifest();
			Save();
		}
	}

	public static void Save()
	{
		EnsureLoaded();
		lock ( _lock )
		{
			try
			{
				var json = JsonSerializer.Serialize( _manifest, new JsonSerializerOptions { WriteIndented = false } );
				File.WriteAllText( _manifestPath, json );
			}
			catch ( Exception ex )
			{
				SboxProLog.Warn( "DocsCache", $"Failed to save manifest: {ex.Message}" );
			}
		}
	}
}
