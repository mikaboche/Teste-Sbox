using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SboxPro.Docs;

/// <summary>
/// Resolves and parses the S&Box API schema dump. Schema URL pattern:
///   https://cdn.sbox.game/releases/YYYY-MM-DD-HH-MM-SS.zip.json
/// discovered via the HTML page at https://sbox.game/api/schema. The JSON
/// is a `{ Types: [...] }` object; we keep public types only and project
/// each into a CachedApiType (slim members list + full pretty payload).
/// </summary>
public static class ApiClient
{
	private const string SchemaPageUrl = "https://sbox.game/api/schema";
	private const int RequestTimeoutMs = 60000; // schema can be 50MB+

	private static readonly HttpClient _http = CreateClient();

	private static HttpClient CreateClient()
	{
		var c = new HttpClient { Timeout = TimeSpan.FromMilliseconds( RequestTimeoutMs ) };
		c.DefaultRequestHeaders.Add( "User-Agent", "sbox-pro-docs/1.0" );
		return c;
	}

	public sealed class CrawlStats
	{
		public int TypeCount;
		public string SchemaUrl;
		public bool FromCache;
		public string Error;
	}

	public static async Task<CrawlStats> CrawlAllAsync( CancellationToken ct = default )
	{
		var stats = new CrawlStats();
		DocsCache.EnsureLoaded();

		var url = await ResolveSchemaUrlAsync( ct );
		if ( url == null )
		{
			stats.Error = "Could not resolve API schema URL from sbox.game/api/schema. Set SBOX_PRO_DOCS_API_SCHEMA_URL env var manually if needed.";
			return stats;
		}
		stats.SchemaUrl = url;

		var json = await DownloadSchemaAsync( url, ct );
		if ( json == null )
		{
			stats.Error = $"Failed to download or parse schema JSON from {url}.";
			return stats;
		}

		var types = json["Types"] as JsonArray;
		if ( types == null )
		{
			stats.Error = "Schema JSON had no Types array.";
			return stats;
		}

		int kept = 0;
		foreach ( var node in types )
		{
			ct.ThrowIfCancellationRequested();
			if ( node is not JsonObject t ) continue;
			if ( !ShouldKeep( t ) ) continue;

			var entry = ProjectType( t );
			if ( entry == null ) continue;
			DocsCache.SetApiType( entry );
			kept++;
		}

		stats.TypeCount = kept;
		DocsCache.MarkApiCrawled( url );
		DocsCache.Save();
		return stats;
	}

	private static async Task<string> ResolveSchemaUrlAsync( CancellationToken ct )
	{
		// 1. Env override
		var envUrl = Environment.GetEnvironmentVariable( "SBOX_PRO_DOCS_API_SCHEMA_URL" );
		if ( !string.IsNullOrEmpty( envUrl ) ) return envUrl;

		// 2. Scrape live page
		try
		{
			using var resp = await _http.GetAsync( SchemaPageUrl, ct );
			if ( resp.IsSuccessStatusCode )
			{
				var html = await resp.Content.ReadAsStringAsync( ct );
				var match = Regex.Match( html, @"https://cdn\.sbox\.game/releases/[^""'\s<>]+\.json" );
				if ( match.Success ) return match.Value;
			}
		}
		catch ( OperationCanceledException ) { throw; }
		catch { }

		// 3. Re-use last known cached URL if it still resolves
		var cached = DocsCache.ApiSchemaUrl;
		if ( !string.IsNullOrEmpty( cached ) && await VerifyUrlAsync( cached, ct ) )
			return cached;

		return null;
	}

	private static async Task<bool> VerifyUrlAsync( string url, CancellationToken ct )
	{
		try
		{
			using var req = new HttpRequestMessage( HttpMethod.Head, url );
			using var resp = await _http.SendAsync( req, ct );
			return resp.IsSuccessStatusCode;
		}
		catch { return false; }
	}

	private static async Task<JsonObject> DownloadSchemaAsync( string url, CancellationToken ct )
	{
		try
		{
			using var resp = await _http.GetAsync( url, ct );
			if ( !resp.IsSuccessStatusCode ) return null;
			using var stream = await resp.Content.ReadAsStreamAsync( ct );
			var node = await JsonNode.ParseAsync( stream, cancellationToken: ct );
			return node as JsonObject;
		}
		catch ( OperationCanceledException ) { throw; }
		catch ( Exception ex )
		{
			SboxProLog.Warn( "ApiClient", $"Schema download failed: {ex.Message}" );
			return null;
		}
	}

	private static bool ShouldKeep( JsonObject t )
	{
		var pub = t["IsPublic"]?.GetValue<bool>() ?? false;
		if ( !pub ) return false;
		var name = t["Name"]?.GetValue<string>();
		var fullName = t["FullName"]?.GetValue<string>();
		if ( string.IsNullOrEmpty( name ) || string.IsNullOrEmpty( fullName ) ) return false;
		if ( name.StartsWith( "<" ) || name.StartsWith( "__" ) ) return false;
		return true;
	}

	private static CachedApiType ProjectType( JsonObject t )
	{
		var name = t["Name"]?.GetValue<string>();
		var fullName = t["FullName"]?.GetValue<string>();
		var ns = t["Namespace"]?.GetValue<string>();
		var baseType = t["BaseType"]?.GetValue<string>();

		string kind = "class";
		if ( t["IsInterface"]?.GetValue<bool>() ?? false ) kind = "interface";
		else if ( !(t["IsClass"]?.GetValue<bool>() ?? true) ) kind = "struct";
		if ( t["IsAbstract"]?.GetValue<bool>() ?? false ) kind = "abstract " + kind;
		if ( t["IsSealed"]?.GetValue<bool>() ?? false ) kind = "sealed " + kind;

		var summary = t["Documentation"]?["Summary"]?.GetValue<string>();

		// Member name list (cap to 12 for ranking; full body has all)
		var members = new List<string>();
		AppendNames( t["Properties"] as JsonArray, members );
		AppendNames( t["Methods"] as JsonArray, members );
		AppendNames( t["Fields"] as JsonArray, members );

		var pretty = BuildPrettyPayload( t, name, fullName, ns, baseType, kind, summary );

		return new CachedApiType
		{
			Name = name,
			FullName = fullName,
			Namespace = ns,
			Kind = kind,
			Description = summary,
			BaseType = baseType,
			Members = members.Distinct().Take( 12 ).ToArray(),
			FullPayload = pretty
		};
	}

	private static void AppendNames( JsonArray arr, List<string> sink )
	{
		if ( arr == null ) return;
		foreach ( var n in arr )
			if ( n is JsonObject o && o["Name"]?.GetValue<string>() is string s ) sink.Add( s );
	}

	private static string BuildPrettyPayload( JsonObject t, string name, string fullName, string ns, string baseType, string kind, string summary )
	{
		var sb = new StringBuilder();
		sb.AppendLine( $"# {fullName}" );
		sb.AppendLine( $"**Type:** {kind} | **Namespace:** {ns}" );
		if ( !string.IsNullOrEmpty( baseType ) ) sb.AppendLine( $"**Inherits:** {baseType}" );
		sb.AppendLine( $"**URL:** https://sbox.game/api/t/{fullName}" );
		sb.AppendLine();
		if ( !string.IsNullOrEmpty( summary ) )
		{
			sb.AppendLine( summary );
			sb.AppendLine();
		}

		AppendSection( sb, "Constructors", t["Constructors"] as JsonArray, isMethod: true );
		AppendSection( sb, "Properties", t["Properties"] as JsonArray, isMethod: false );
		AppendSection( sb, "Methods", t["Methods"] as JsonArray, isMethod: true );
		AppendSection( sb, "Fields", t["Fields"] as JsonArray, isMethod: false );
		return sb.ToString();
	}

	private static void AppendSection( StringBuilder sb, string title, JsonArray arr, bool isMethod )
	{
		if ( arr == null || arr.Count == 0 ) return;
		sb.AppendLine( $"## {title}" );
		foreach ( var n in arr )
		{
			if ( n is not JsonObject o ) continue;
			var name = o["Name"]?.GetValue<string>() ?? "";
			var sig = isMethod ? BuildMethodSig( o ) : BuildMemberSig( o );
			sb.AppendLine( $"- `{sig}`" );
			var doc = o["Documentation"]?["Summary"]?.GetValue<string>();
			if ( !string.IsNullOrEmpty( doc ) ) sb.AppendLine( $"  {doc}" );
		}
		sb.AppendLine();
	}

	private static string BuildMethodSig( JsonObject m )
	{
		var ret = m["ReturnType"]?.GetValue<string>() ?? "void";
		var name = m["Name"]?.GetValue<string>() ?? "";
		var ps = new List<string>();
		if ( m["Parameters"] is JsonArray parr )
		{
			foreach ( var p in parr )
			{
				if ( p is not JsonObject po ) continue;
				var pt = po["Type"]?.GetValue<string>() ?? "object";
				var pn = po["Name"]?.GetValue<string>() ?? "_";
				ps.Add( $"{pt} {pn}" );
			}
		}
		return $"{ret} {name}({string.Join( ", ", ps )})";
	}

	private static string BuildMemberSig( JsonObject m )
	{
		var t = m["PropertyType"]?.GetValue<string>() ?? m["FieldType"]?.GetValue<string>() ?? "object";
		var name = m["Name"]?.GetValue<string>() ?? "";
		return $"{t} {name}";
	}
}
