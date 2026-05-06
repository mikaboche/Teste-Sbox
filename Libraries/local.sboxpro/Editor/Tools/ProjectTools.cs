using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Sandbox;

namespace SboxPro;

public static class ProjectTools
{
	[Tool( "get_project_info", "Returns project metadata: name, org, ident, type, root path, assets path." )]
	public static object GetProjectInfo()
	{
		var project = Project.Current;
		if ( project == null )
			return ToolHandlerBase.ErrorResult( "No active project" );

		return ToolHandlerBase.JsonResult( new
		{
			name = project.Config.Title,
			org = project.Config.Org,
			ident = project.Config.Ident,
			type = project.Config.Type,
			path = PathNormalizer.Normalize( project.GetRootPath() ),
			assetsPath = PathNormalizer.Normalize( project.GetAssetsPath() )
		} );
	}

	[Tool( "get_project_config", "Returns the full .sbproj file content plus parsed project metadata." )]
	public static object GetProjectConfig()
	{
		var rootPath = PathNormalizer.GetProjectRoot();
		var sbproj = FindSbproj( rootPath );

		if ( sbproj == null )
			return ToolHandlerBase.ErrorResult( ".sbproj file not found in project root" );

		var content = File.ReadAllText( sbproj );

		return ToolHandlerBase.JsonResult( new
		{
			path = PathNormalizer.ToRelative( sbproj ),
			content,
			project = new
			{
				title = Project.Current.Config.Title,
				org = Project.Current.Config.Org,
				ident = Project.Current.Config.Ident,
				type = Project.Current.Config.Type
			}
		} );
	}

	[Tool( "set_project_config", "Updates fields in the .sbproj file. Uses proper JSON parsing — safe for values containing quotes or special characters." )]
	[Param( "changes", "JSON object of key-value pairs to set. Keys are top-level .sbproj fields (e.g. Title, StartupScene, MapList). Values can be strings, numbers, booleans, arrays, or objects.", Required = true, Type = "object" )]
	public static object SetProjectConfig( JsonElement args )
	{
		var rootPath = PathNormalizer.GetProjectRoot();
		var sbproj = FindSbproj( rootPath );

		if ( sbproj == null )
			return ToolHandlerBase.ErrorResult( ".sbproj file not found in project root" );

		if ( !args.TryGetProperty( "changes", out var changes ) || changes.ValueKind != JsonValueKind.Object )
			return ToolHandlerBase.ErrorResult( "Missing or invalid 'changes' parameter — must be a JSON object" );

		try
		{
			var text = File.ReadAllText( sbproj );
			var node = JsonNode.Parse( text, SerializationHelpers.NodeOptions, SerializationHelpers.DocOptions );

			if ( node is not JsonObject root )
				return ToolHandlerBase.ErrorResult( ".sbproj content is not a JSON object" );

			var updated = new List<string>();

			foreach ( var change in changes.EnumerateObject() )
			{
				var valueNode = JsonNode.Parse( change.Value.GetRawText() );
				root[change.Name] = valueNode;
				updated.Add( change.Name );
			}

			var output = node.ToJsonString( new JsonSerializerOptions { WriteIndented = true } );
			File.WriteAllText( sbproj, output );

			return ToolHandlerBase.JsonResult( new
			{
				updated = true,
				path = PathNormalizer.ToRelative( sbproj ),
				fields_changed = updated
			} );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Failed to update .sbproj: {ex.Message}" );
		}
	}

	[Tool( "set_project_thumbnail", "Sets the project thumbnail image. Copies the source image to thumb.png in the project root." )]
	[Param( "sourcePath", "Path to the source image file (relative to project root). Must be .png or .jpg.", Required = true )]
	public static object SetProjectThumbnail( JsonElement args )
	{
		var sourcePath = ToolHandlerBase.RequireString( args, "sourcePath" );
		var rootPath = PathNormalizer.GetProjectRoot();
		var fullSource = PathNormalizer.SafeResolve( sourcePath );

		if ( fullSource == null )
			return ToolHandlerBase.ErrorResult( $"Path traversal denied: {sourcePath}" );

		if ( !File.Exists( fullSource ) )
			return ToolHandlerBase.ErrorResult( $"Source image not found: {sourcePath}" );

		var ext = Path.GetExtension( fullSource ).ToLowerInvariant();
		if ( ext != ".png" && ext != ".jpg" && ext != ".jpeg" )
			return ToolHandlerBase.ErrorResult( "Thumbnail must be a .png or .jpg file" );

		var thumbDest = Path.Combine( rootPath, "thumb.png" );
		File.Copy( fullSource, thumbDest, overwrite: true );

		return ToolHandlerBase.JsonResult( new { set = true, thumbnail = "thumb.png" } );
	}

	[Tool( "validate_project", "Checks .sbproj integrity: existence, ident, title, scenes, code, startup scene." )]
	public static object ValidateProject()
	{
		var rootPath = PathNormalizer.GetProjectRoot();
		var issues = new List<string>();
		var checks = new List<object>();

		var sbproj = FindSbproj( rootPath );
		var hasSbproj = sbproj != null;
		checks.Add( new { check = "sbproj_exists", pass = hasSbproj, detail = hasSbproj ? PathNormalizer.ToRelative( sbproj ) : "No .sbproj found" } );
		if ( !hasSbproj ) issues.Add( "Missing .sbproj file" );

		var assetsPath = PathNormalizer.Normalize( Project.Current.GetAssetsPath() );
		var sceneDir = Path.Combine( assetsPath, "scenes" );
		var sceneCount = Directory.Exists( sceneDir )
			? Directory.GetFiles( sceneDir, "*.scene", SearchOption.AllDirectories ).Length
			: 0;
		if ( sceneCount == 0 && Directory.Exists( assetsPath ) )
			sceneCount = Directory.GetFiles( assetsPath, "*.scene", SearchOption.AllDirectories ).Length;
		checks.Add( new { check = "has_scenes", pass = sceneCount > 0, detail = $"{sceneCount} scene(s) found" } );
		if ( sceneCount == 0 ) issues.Add( "No .scene files found" );

		var hasIdent = !string.IsNullOrEmpty( Project.Current.Config.Ident );
		checks.Add( new { check = "has_ident", pass = hasIdent, detail = hasIdent ? Project.Current.Config.Ident : "No ident set" } );
		if ( !hasIdent ) issues.Add( "Project Ident not set" );

		var hasTitle = !string.IsNullOrEmpty( Project.Current.Config.Title );
		checks.Add( new { check = "has_title", pass = hasTitle, detail = hasTitle ? Project.Current.Config.Title : "No title set" } );
		if ( !hasTitle ) issues.Add( "Project Title not set" );

		var codeDir = Path.Combine( rootPath, "Code" );
		var hasCode = Directory.Exists( codeDir ) && Directory.GetFiles( codeDir, "*.cs", SearchOption.AllDirectories ).Length > 0;
		checks.Add( new { check = "has_code", pass = hasCode, detail = hasCode ? "Code/ directory with .cs files" : "No .cs files in Code/" } );

		if ( hasSbproj )
		{
			try
			{
				var text = File.ReadAllText( sbproj );
				var doc = JsonNode.Parse( text );
				var startup = doc?["StartupScene"]?.GetValue<string>();
				var hasStartup = !string.IsNullOrEmpty( startup );
				if ( hasStartup )
				{
					var startupAbsolute = Path.Combine( assetsPath, startup );
					var startupExists = File.Exists( startupAbsolute );
					checks.Add( new { check = "startup_scene_valid", pass = startupExists, detail = startupExists ? startup : $"StartupScene '{startup}' not found at {startupAbsolute}" } );
					if ( !startupExists ) issues.Add( $"StartupScene '{startup}' does not exist" );
				}
				else
				{
					checks.Add( new { check = "startup_scene_valid", pass = false, detail = "StartupScene not set in .sbproj" } );
					issues.Add( "StartupScene not configured" );
				}
			}
			catch { }
		}

		return ToolHandlerBase.JsonResult( new
		{
			valid = issues.Count == 0,
			issue_count = issues.Count,
			issues,
			checks
		} );
	}

	[Tool( "list_project_files", "Browse the project file tree, optionally filtering by extension and path." )]
	[Param( "path", "Subdirectory to list (relative to project root). Empty string = project root.", Required = false )]
	[Param( "extension", "File extension filter, e.g. '.cs', '.scene'. Include the dot.", Required = false )]
	[Param( "recursive", "Search subdirectories. Default: true", Required = false, Type = "boolean", Default = "true" )]
	[Param( "limit", "Max files to return. Default: 500", Required = false, Type = "integer", Default = "500" )]
	public static object ListProjectFiles( JsonElement args )
	{
		var rootPath = PathNormalizer.GetProjectRoot();
		var subDir = ToolHandlerBase.GetString( args, "path", "" );
		var extension = ToolHandlerBase.GetString( args, "extension" );
		var recursive = ToolHandlerBase.GetBool( args, "recursive", true );
		var limit = ToolHandlerBase.GetInt( args, "limit", 500 );

		var searchDir = string.IsNullOrEmpty( subDir )
			? rootPath
			: PathNormalizer.Normalize( Path.Combine( rootPath, subDir ) );

		if ( !Directory.Exists( searchDir ) )
			return ToolHandlerBase.ErrorResult( $"Directory not found: {subDir}" );

		var safe = PathNormalizer.SafeResolve( searchDir );
		if ( safe == null )
			return ToolHandlerBase.ErrorResult( $"Path traversal denied: {subDir}" );

		var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
		var files = Directory.GetFiles( searchDir, "*.*", searchOption )
			.Select( f => PathNormalizer.Normalize( Path.GetRelativePath( rootPath, f ) ) )
			.Where( f => extension == null || f.EndsWith( extension, StringComparison.OrdinalIgnoreCase ) )
			.Where( f => !f.Contains( "/.git/" ) && !f.Contains( "/bin/" ) && !f.Contains( "/obj/" ) )
			.Take( limit )
			.ToArray();

		return ToolHandlerBase.JsonResult( new
		{
			path = subDir,
			count = files.Length,
			truncated = files.Length >= limit,
			files
		} );
	}

	[Tool( "read_file", "Read the text content of a file in the project." )]
	[Param( "path", "File path relative to project root.", Required = true )]
	[Param( "offset", "Line offset to start reading from (0-based). Default: 0", Required = false, Type = "integer", Default = "0" )]
	[Param( "limit", "Max lines to return. Default: 1000", Required = false, Type = "integer", Default = "1000" )]
	public static object ReadFile( JsonElement args )
	{
		var filePath = ToolHandlerBase.RequireString( args, "path" );
		var offset = ToolHandlerBase.GetInt( args, "offset", 0 );
		var limit = ToolHandlerBase.GetInt( args, "limit", 1000 );

		var fullPath = PathNormalizer.SafeResolve( filePath );

		if ( fullPath == null )
			return ToolHandlerBase.ErrorResult( $"Path traversal denied: {filePath}" );

		if ( !File.Exists( fullPath ) )
			return ToolHandlerBase.ErrorResult( $"File not found: {filePath}" );

		var lines = File.ReadAllLines( fullPath );
		var totalLines = lines.Length;
		var sliced = lines.Skip( offset ).Take( limit ).ToArray();
		var content = string.Join( "\n", sliced );

		return ToolHandlerBase.JsonResult( new
		{
			path = filePath,
			total_lines = totalLines,
			offset,
			returned_lines = sliced.Length,
			truncated = offset + sliced.Length < totalLines,
			content
		} );
	}

	[Tool( "write_file", "Write text content to a file in the project. Creates parent directories if needed." )]
	[Param( "path", "File path relative to project root.", Required = true )]
	[Param( "content", "The text content to write.", Required = true )]
	public static object WriteFile( JsonElement args )
	{
		var filePath = ToolHandlerBase.RequireString( args, "path" );
		var content = ToolHandlerBase.RequireString( args, "content" );

		var fullPath = PathNormalizer.SafeResolve( filePath );

		if ( fullPath == null )
			return ToolHandlerBase.ErrorResult( $"Path traversal denied: {filePath}" );

		var dir = Path.GetDirectoryName( fullPath );
		if ( !string.IsNullOrEmpty( dir ) )
			Directory.CreateDirectory( dir );

		File.WriteAllText( fullPath, content );

		return ToolHandlerBase.JsonResult( new
		{
			path = filePath,
			written = true,
			length = content.Length
		} );
	}

	[Tool( "find_in_project", "Grep-like search across project files. Searches for a text pattern in files matching the given extension." )]
	[Param( "pattern", "Text pattern to search for (case-sensitive).", Required = true )]
	[Param( "extension", "File extension to search. Default: .cs", Required = false, Default = ".cs" )]
	[Param( "case_sensitive", "Whether the search is case-sensitive. Default: true", Required = false, Type = "boolean", Default = "true" )]
	[Param( "max_results", "Max matching lines to return. Default: 50", Required = false, Type = "integer", Default = "50" )]
	public static object FindInProject( JsonElement args )
	{
		var pattern = ToolHandlerBase.RequireString( args, "pattern" );
		var ext = ToolHandlerBase.GetString( args, "extension", ".cs" );
		var caseSensitive = ToolHandlerBase.GetBool( args, "case_sensitive", true );
		var maxResults = ToolHandlerBase.GetInt( args, "max_results", 50 );

		var rootPath = PathNormalizer.GetProjectRoot();
		if ( string.IsNullOrEmpty( rootPath ) || !Directory.Exists( rootPath ) )
			return ToolHandlerBase.ErrorResult( "Project root not found" );

		var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
		var hits = new List<object>();

		try
		{
			foreach ( var file in Directory.EnumerateFiles( rootPath, "*" + ext, SearchOption.AllDirectories ) )
			{
				if ( hits.Count >= maxResults ) break;

				var rel = PathNormalizer.Normalize( Path.GetRelativePath( rootPath, file ) );
				if ( rel.Contains( "/.git/" ) || rel.Contains( "/bin/" ) || rel.Contains( "/obj/" ) || rel.Contains( "/Libraries/" ) )
					continue;

				try
				{
					var lines = File.ReadAllLines( file );
					for ( int i = 0; i < lines.Length; i++ )
					{
						if ( lines[i].Contains( pattern, comparison ) )
						{
							hits.Add( new
							{
								file = rel,
								line = i + 1,
								text = lines[i].TrimStart()
							} );
							if ( hits.Count >= maxResults ) break;
						}
					}
				}
				catch { }
			}
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Search failed: {ex.Message}" );
		}

		return ToolHandlerBase.JsonResult( new
		{
			pattern,
			count = hits.Count,
			truncated = hits.Count >= maxResults,
			results = hits
		} );
	}

	[Tool( "get_package_details", "Returns metadata for an S&Box package by its ident (e.g. 'facepunch.flatgrass', 'conna.inventory')." )]
	[Param( "ident", "Package identifier.", Required = true )]
	public static async Task<object> GetPackageDetails( JsonElement args )
	{
		var ident = ToolHandlerBase.RequireString( args, "ident" );

		try
		{
			// Async path (was Task.Wait + 15s timeout that surfaced as "timed out" even when
			// sbox.game was just slow). Now the dispatcher awaits us — no artificial deadline.
			var pkg = await Package.FetchAsync( ident, false );
			if ( pkg == null )
				return ToolHandlerBase.ErrorResult( $"Package not found: {ident}" );

			return ToolHandlerBase.JsonResult( new
			{
				full_ident = pkg.FullIdent,
				title = pkg.Title,
				summary = pkg.Summary,
				description = pkg.Description,
				org = new
				{
					ident = pkg.Org?.Ident,
					title = pkg.Org?.Title
				},
				tags = pkg.Tags,
				type_name = pkg.TypeName,
				thumb = pkg.Thumb,
				updated = pkg.Updated.ToString( "o" ),
				created = pkg.Created.ToString( "o" ),
				installed = Editor.AssetSystem.IsCloudInstalled( pkg.FullIdent )
			} );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Failed to fetch package '{ident}': {ex.Message}" );
		}
	}

	// ──────────────────────────────────────────────
	//  Private helpers
	// ──────────────────────────────────────────────

	private static string FindSbproj( string rootPath )
	{
		return Directory.GetFiles( rootPath, "*.sbproj", SearchOption.TopDirectoryOnly ).FirstOrDefault();
	}
}
