using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Sandbox;
using Editor;

namespace SboxPro;

public static class AssetTools
{
	// ──────────────────────────────────────────────
	//  browse_assets
	// ──────────────────────────────────────────────

	[Tool( "browse_assets", "Browse project assets by type and optional name filter." )]
	[Param( "type", "Asset type filter (e.g. 'model', 'material', 'texture', 'sound', 'prefab', 'scene'). Leave empty for all.", Required = false )]
	[Param( "name", "Name substring filter (case-insensitive).", Required = false )]
	[Param( "path", "Subdirectory to search (relative to project root).", Required = false )]
	[Param( "limit", "Max results. Default: 50", Required = false, Type = "integer", Default = "50" )]
	public static object BrowseAssets( JsonElement args )
	{
		var typeFilter = ToolHandlerBase.GetString( args, "type" );
		var nameFilter = ToolHandlerBase.GetString( args, "name" );
		var pathFilter = ToolHandlerBase.GetString( args, "path" );
		var limit = ToolHandlerBase.GetInt( args, "limit", 50 );

		var extension = MapTypeToExtension( typeFilter );

		var rootPath = PathNormalizer.GetProjectRoot();
		var searchDir = string.IsNullOrEmpty( pathFilter )
			? rootPath
			: PathNormalizer.Normalize( Path.Combine( rootPath, pathFilter ) );

		if ( !Directory.Exists( searchDir ) )
			return ToolHandlerBase.ErrorResult( $"Directory not found: {pathFilter}" );

		var pattern = string.IsNullOrEmpty( extension ) ? "*.*" : $"*{extension}";

		var assets = Directory.GetFiles( searchDir, pattern, SearchOption.AllDirectories )
			.Where( f =>
			{
				var ext = Path.GetExtension( f ).ToLowerInvariant();
				if ( string.IsNullOrEmpty( extension ) )
				{
					return IsAssetExtension( ext );
				}
				return true;
			} )
			.Where( f => string.IsNullOrEmpty( nameFilter ) || Path.GetFileNameWithoutExtension( f ).Contains( nameFilter, StringComparison.OrdinalIgnoreCase ) )
			.Take( limit )
			.Select( f => new
			{
				path = PathNormalizer.ToRelative( f ),
				name = Path.GetFileNameWithoutExtension( f ),
				type = Path.GetExtension( f ).TrimStart( '.' ),
				sizeBytes = new FileInfo( f ).Length
			} )
			.OrderBy( a => a.path )
			.ToArray();

		return ToolHandlerBase.JsonResult( new
		{
			count = assets.Length,
			truncated = assets.Length >= limit,
			assets
		} );
	}

	// ──────────────────────────────────────────────
	//  search_assets
	// ──────────────────────────────────────────────

	[Tool( "search_assets", "Search assets by query string and optional type filter. Merged Lou+Ozmium implementation." )]
	[Param( "query", "Search query (matches against file name and path).", Required = true )]
	[Param( "type", "Asset type filter (e.g. 'model', 'material', 'prefab').", Required = false )]
	[Param( "limit", "Max results. Default: 50", Required = false, Type = "integer", Default = "50" )]
	public static object SearchAssets( JsonElement args )
	{
		var query = ToolHandlerBase.RequireString( args, "query" );
		var typeFilter = ToolHandlerBase.GetString( args, "type" );
		var limit = ToolHandlerBase.GetInt( args, "limit", 50 );

		var extension = MapTypeToExtension( typeFilter );
		var rootPath = PathNormalizer.GetProjectRoot();

		var pattern = string.IsNullOrEmpty( extension ) ? "*.*" : $"*{extension}";

		var assets = Directory.GetFiles( rootPath, pattern, SearchOption.AllDirectories )
			.Where( f =>
			{
				if ( string.IsNullOrEmpty( extension ) && !IsAssetExtension( Path.GetExtension( f ).ToLowerInvariant() ) )
					return false;

				var rel = PathNormalizer.ToRelative( f );
				return rel.Contains( query, StringComparison.OrdinalIgnoreCase );
			} )
			.Take( limit )
			.Select( f => new
			{
				path = PathNormalizer.ToRelative( f ),
				name = Path.GetFileNameWithoutExtension( f ),
				type = Path.GetExtension( f ).TrimStart( '.' ),
				sizeBytes = new FileInfo( f ).Length
			} )
			.ToArray();

		return ToolHandlerBase.JsonResult( new
		{
			query,
			count = assets.Length,
			truncated = assets.Length >= limit,
			assets
		} );
	}

	// ──────────────────────────────────────────────
	//  get_asset_info
	// ──────────────────────────────────────────────

	[Tool( "get_asset_info", "Get metadata for a single asset file." )]
	[Param( "path", "Asset file path (relative to project root).", Required = true )]
	public static object GetAssetInfo( JsonElement args )
	{
		var path = ToolHandlerBase.RequireString( args, "path" );
		var normalized = PathNormalizer.NormalizeAssetPath( path );
		var absPath = PathNormalizer.ToAbsolute( normalized );

		if ( !File.Exists( absPath ) )
			return ToolHandlerBase.ErrorResult( $"Asset not found: {normalized}" );

		var fi = new FileInfo( absPath );
		var ext = fi.Extension.ToLowerInvariant();

		var result = new Dictionary<string, object>
		{
			["path"] = normalized,
			["name"] = Path.GetFileNameWithoutExtension( absPath ),
			["extension"] = ext,
			["type"] = ext.TrimStart( '.' ),
			["sizeBytes"] = fi.Length,
			["lastModified"] = fi.LastWriteTimeUtc.ToString( "o" ),
			["created"] = fi.CreationTimeUtc.ToString( "o" )
		};

		// Try to get additional info via AssetSystem
		try
		{
			var asset = AssetSystem.FindByPath( normalized );
			if ( asset != null )
			{
				try { result["assetType"] = asset.AssetType?.FriendlyName; } catch { }
			}
		}
		catch { }

		return ToolHandlerBase.JsonResult( result );
	}

	// ──────────────────────────────────────────────
	//  get_asset_dependencies
	// ──────────────────────────────────────────────

	[Tool( "get_asset_dependencies", "List assets referenced by a given asset (reads JSON references)." )]
	[Param( "path", "Asset file path.", Required = true )]
	public static object GetAssetDependencies( JsonElement args )
	{
		var path = ToolHandlerBase.RequireString( args, "path" );
		var normalized = PathNormalizer.NormalizeAssetPath( path );
		var absPath = PathNormalizer.ToAbsolute( normalized );

		if ( !File.Exists( absPath ) )
			return ToolHandlerBase.ErrorResult( $"Asset not found: {normalized}" );

		try
		{
			var content = File.ReadAllText( absPath );
			var doc = JsonDocument.Parse( content );
			var refs = new List<string>();

			if ( doc.RootElement.TryGetProperty( "__references", out var refsArray ) && refsArray.ValueKind == JsonValueKind.Array )
			{
				foreach ( var item in refsArray.EnumerateArray() )
				{
					if ( item.ValueKind == JsonValueKind.String )
						refs.Add( item.GetString() );
				}
			}

			// Also scan for resource path strings in the JSON
			var resourcePaths = new HashSet<string>();
			ScanForResourcePaths( doc.RootElement, resourcePaths );

			return ToolHandlerBase.JsonResult( new
			{
				path = normalized,
				explicitReferences = refs.ToArray(),
				resourcePaths = resourcePaths.OrderBy( r => r ).ToArray()
			} );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Failed to read asset: {ex.Message}" );
		}
	}

	private static void ScanForResourcePaths( JsonElement element, HashSet<string> paths )
	{
		switch ( element.ValueKind )
		{
			case JsonValueKind.String:
				var val = element.GetString();
				if ( !string.IsNullOrEmpty( val ) && IsResourcePath( val ) )
					paths.Add( val );
				break;
			case JsonValueKind.Object:
				foreach ( var prop in element.EnumerateObject() )
					ScanForResourcePaths( prop.Value, paths );
				break;
			case JsonValueKind.Array:
				foreach ( var item in element.EnumerateArray() )
					ScanForResourcePaths( item, paths );
				break;
		}
	}

	private static bool IsResourcePath( string val )
	{
		if ( val.Length < 5 || val.Contains( ' ' ) ) return false;
		var ext = Path.GetExtension( val ).ToLowerInvariant();
		return ext is ".vmdl" or ".vmat" or ".vtex" or ".sound" or ".sndevt" or ".prefab" or ".scene" or ".vpcf";
	}

	// ──────────────────────────────────────────────
	//  reload_asset
	// ──────────────────────────────────────────────

	[Tool( "reload_asset", "Force reimport/reload of an asset.", RequiresMainThread = true )]
	[Param( "path", "Asset file path.", Required = true )]
	public static object ReloadAsset( JsonElement args )
	{
		var path = ToolHandlerBase.RequireString( args, "path" );
		var normalized = PathNormalizer.NormalizeAssetPath( path );

		try
		{
			var asset = AssetSystem.FindByPath( normalized );
			if ( asset == null )
				return ToolHandlerBase.ErrorResult( $"Asset not found in AssetSystem: {normalized}" );

			// Recompile the asset
			try { asset.Compile( true ); }
			catch
			{
				// Fallback: try Recompile or just touch the file to trigger rebuild
				var absPath = PathNormalizer.ToAbsolute( normalized );
				if ( File.Exists( absPath ) )
					File.SetLastWriteTimeUtc( absPath, DateTime.UtcNow );
			}

			return ToolHandlerBase.JsonResult( new
			{
				reloaded = true,
				path = normalized
			} );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Failed to reload asset: {ex.Message}" );
		}
	}

	// ──────────────────────────────────────────────
	//  assign_model
	// ──────────────────────────────────────────────

	[Tool( "assign_model", "Set a model on a ModelRenderer or SkinnedModelRenderer component.", RequiresMainThread = true )]
	[Param( "name", "Name of the target GameObject.", Required = false )]
	[Param( "guid", "GUID of the target GameObject.", Required = false )]
	[Param( "model_path", "Model asset path (e.g. 'models/citizen/citizen.vmdl').", Required = true )]
	public static object AssignModel( JsonElement args )
	{
		var modelPath = ToolHandlerBase.RequireString( args, "model_path" );

		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );

		var model = Model.Load( modelPath );
		if ( model == null )
			return ToolHandlerBase.ErrorResult( $"Model not found: {modelPath}" );

		// Try ModelRenderer first, then SkinnedModelRenderer
		var renderer = go.Components.Get<ModelRenderer>()
			?? (ModelRenderer)go.Components.Get<SkinnedModelRenderer>();

		if ( renderer == null )
			return ToolHandlerBase.ErrorResult( $"No ModelRenderer or SkinnedModelRenderer on '{go.Name}'" );

		renderer.Model = model;

		return ToolHandlerBase.JsonResult( new
		{
			assigned = true,
			gameObject = go.Name,
			gameObjectGuid = go.Id.ToString(),
			modelPath,
			rendererType = renderer.GetType().Name
		} );
	}

	// ──────────────────────────────────────────────
	//  create_material
	// ──────────────────────────────────────────────

	[Tool( "create_material", "Create a new .vmat material file.", RequiresMainThread = true )]
	[Param( "path", "Material file path (e.g. 'materials/floor.vmat'). Normalized under Assets/.", Required = true )]
	[Param( "shader", "Shader name. Default: 'shaders/complex.shader'", Required = false )]
	[Param( "color", "Base color as 'r,g,b' (0-1 range). Default: white.", Required = false )]
	public static object CreateMaterial( JsonElement args )
	{
		var path = ToolHandlerBase.RequireString( args, "path" );
		var shader = ToolHandlerBase.GetString( args, "shader", "shaders/complex.shader" );
		var colorStr = ToolHandlerBase.GetString( args, "color" );

		if ( !path.EndsWith( ".vmat", StringComparison.OrdinalIgnoreCase ) )
			path += ".vmat";

		var safePath = PathNormalizer.ResolveAssetPath( path );
		if ( safePath == null )
			return ToolHandlerBase.ErrorResult( $"Path outside project: {path}" );

		if ( File.Exists( safePath ) )
			return ToolHandlerBase.ErrorResult( $"Material already exists: {PathNormalizer.ToRelative( safePath )}" );

		try
		{
			var dir = Path.GetDirectoryName( safePath );
			if ( !string.IsNullOrEmpty( dir ) )
				Directory.CreateDirectory( dir );

			// S&Box Sandbox.Material doesn't expose a public Create(name, shader) factory.
			// Available API: Load, LoadAsync, CreateCopy(string). To create a new material
			// from scratch we write a minimal .vmat JSON file to disk and let the asset
			// system pick it up. Color override is embedded in the JSON.
			string colorJson = null;
			if ( !string.IsNullOrEmpty( colorStr ) )
			{
				var parts = colorStr.Split( ',' );
				if ( parts.Length >= 3 )
				{
					var r = float.Parse( parts[0], System.Globalization.CultureInfo.InvariantCulture );
					var g = float.Parse( parts[1], System.Globalization.CultureInfo.InvariantCulture );
					var b = float.Parse( parts[2], System.Globalization.CultureInfo.InvariantCulture );
					var a = parts.Length >= 4 ? float.Parse( parts[3], System.Globalization.CultureInfo.InvariantCulture ) : 1f;
					colorJson = $"\t\t\"g_vColorTint\": \"{r} {g} {b} {a}\",\n";
				}
			}

			var vmat = "{\n" +
				$"\t\"_class\": \"Material\",\n" +
				$"\t\"shader\": \"{shader}\",\n" +
				"\tattributes:\n\t{\n" +
				( colorJson ?? "" ) +
				"\t}\n" +
				"}\n";

			File.WriteAllText( safePath, vmat );

			return ToolHandlerBase.JsonResult( new
			{
				created = true,
				path = PathNormalizer.ToRelative( safePath ),
				shader,
				note = "Material created as raw .vmat file. Recompile via reload_asset to make it usable."
			} );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Failed to create material: {ex.Message}" );
		}
	}

	// ──────────────────────────────────────────────
	//  assign_material
	// ──────────────────────────────────────────────

	[Tool( "assign_material", "Set a material on a ModelRenderer component.", RequiresMainThread = true )]
	[Param( "name", "Name of the target GameObject.", Required = false )]
	[Param( "guid", "GUID of the target GameObject.", Required = false )]
	[Param( "material_path", "Material asset path (e.g. 'materials/floor.vmat').", Required = true )]
	public static object AssignMaterial( JsonElement args )
	{
		var materialPath = ToolHandlerBase.RequireString( args, "material_path" );

		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );

		var material = Material.Load( materialPath );
		if ( material == null )
			return ToolHandlerBase.ErrorResult( $"Material not found: {materialPath}" );

		var renderer = go.Components.Get<ModelRenderer>()
			?? (ModelRenderer)go.Components.Get<SkinnedModelRenderer>();

		if ( renderer == null )
			return ToolHandlerBase.ErrorResult( $"No ModelRenderer or SkinnedModelRenderer on '{go.Name}'" );

		renderer.MaterialOverride = material;

		return ToolHandlerBase.JsonResult( new
		{
			assigned = true,
			gameObject = go.Name,
			gameObjectGuid = go.Id.ToString(),
			materialPath,
			rendererType = renderer.GetType().Name
		} );
	}

	// ──────────────────────────────────────────────
	//  set_material_property
	// ──────────────────────────────────────────────

	[Tool( "set_material_property", "Set a shader property on a material loaded by path or on a renderer's material.", RequiresMainThread = true )]
	[Param( "material_path", "Material path to load and modify.", Required = false )]
	[Param( "name", "Name of GO with renderer (used if material_path not provided).", Required = false )]
	[Param( "guid", "GUID of GO with renderer.", Required = false )]
	[Param( "property", "Shader property name (e.g. 'Color', 'Normal', 'Metalness').", Required = true )]
	[Param( "value", "Value to set (string, number, or 'r,g,b,a' for colors).", Required = true )]
	public static object SetMaterialProperty( JsonElement args )
	{
		var materialPath = ToolHandlerBase.GetString( args, "material_path" );
		var property = ToolHandlerBase.RequireString( args, "property" );
		var valueStr = ToolHandlerBase.RequireString( args, "value" );

		Material material = null;

		if ( !string.IsNullOrEmpty( materialPath ) )
		{
			material = Material.Load( materialPath );
			if ( material == null )
				return ToolHandlerBase.ErrorResult( $"Material not found: {materialPath}" );
		}
		else
		{
			var go = ResolveGO( args );
			if ( go == null ) return GONotFound( args );

			var renderer = go.Components.Get<ModelRenderer>()
				?? (ModelRenderer)go.Components.Get<SkinnedModelRenderer>();

			if ( renderer == null )
				return ToolHandlerBase.ErrorResult( $"No renderer on '{go.Name}'" );

			material = renderer.MaterialOverride;
			if ( material == null )
				return ToolHandlerBase.ErrorResult( $"No material override set on '{go.Name}'. Assign a material first." );
		}

		try
		{
			// Try to detect type and set appropriately
			if ( valueStr.Contains( ',' ) )
			{
				var parts = valueStr.Split( ',' );
				if ( parts.Length >= 3 )
				{
					var color = new Color(
						float.Parse( parts[0] ), float.Parse( parts[1] ),
						float.Parse( parts[2] ), parts.Length > 3 ? float.Parse( parts[3] ) : 1f );
					material.Set( property, color );
				}
			}
			else if ( float.TryParse( valueStr, out var floatVal ) )
			{
				material.Set( property, floatVal );
			}
			else
			{
				material.Set( property, valueStr );
			}

			return ToolHandlerBase.JsonResult( new
			{
				set = true,
				property,
				value = valueStr,
				materialPath = materialPath ?? "(from renderer)"
			} );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Failed to set material property: {ex.Message}" );
		}
	}

	// ──────────────────────────────────────────────
	//  get_material_properties
	// ──────────────────────────────────────────────

	[Tool( "get_material_properties", "Read shader and surface properties from a material.", RequiresMainThread = true )]
	[Param( "path", "Material file path.", Required = false )]
	[Param( "name", "Name of GO with renderer.", Required = false )]
	[Param( "guid", "GUID of GO with renderer.", Required = false )]
	public static object GetMaterialProperties( JsonElement args )
	{
		var path = ToolHandlerBase.GetString( args, "path" );

		Material material = null;
		string source;

		if ( !string.IsNullOrEmpty( path ) )
		{
			material = Material.Load( path );
			source = path;
		}
		else
		{
			var go = ResolveGO( args );
			if ( go == null ) return GONotFound( args );

			var renderer = go.Components.Get<ModelRenderer>()
				?? (ModelRenderer)go.Components.Get<SkinnedModelRenderer>();

			if ( renderer == null )
				return ToolHandlerBase.ErrorResult( $"No renderer on '{go.Name}'" );

			material = renderer.MaterialOverride;
			source = $"renderer on {go.Name}";
		}

		if ( material == null )
			return ToolHandlerBase.ErrorResult( $"Material not found: {source}" );

		return ToolHandlerBase.JsonResult( new
		{
			source,
			name = material.Name,
			resourcePath = material.ResourcePath
		} );
	}

	// ──────────────────────────────────────────────
	//  get_model_info
	// ──────────────────────────────────────────────

	[Tool( "get_model_info", "Get information about a model asset (bones, attachments, physics bodies).", RequiresMainThread = true )]
	[Param( "path", "Model file path (e.g. 'models/citizen/citizen.vmdl').", Required = false )]
	[Param( "name", "Name of GO with ModelRenderer (alternative to path).", Required = false )]
	[Param( "guid", "GUID of GO with ModelRenderer.", Required = false )]
	public static object GetModelInfo( JsonElement args )
	{
		var path = ToolHandlerBase.GetString( args, "path" );

		Model model = null;

		if ( !string.IsNullOrEmpty( path ) )
		{
			model = Model.Load( path );
		}
		else
		{
			var go = ResolveGO( args );
			if ( go == null ) return GONotFound( args );

			var renderer = go.Components.Get<ModelRenderer>()
				?? (ModelRenderer)go.Components.Get<SkinnedModelRenderer>();

			if ( renderer != null )
				model = renderer.Model;
		}

		if ( model == null )
			return ToolHandlerBase.ErrorResult( "Model not found" );

		var result = new Dictionary<string, object>
		{
			["path"] = model.ResourcePath,
			["boneCount"] = model.BoneCount
		};

		// Bone names — Model.Bones is BoneCollection with AllBones IReadOnlyList<Bone>
		try
		{
			var bones = model.Bones?.AllBones?
				.Take( 200 )
				.Where( b => !string.IsNullOrEmpty( b.Name ) )
				.Select( b => b.Name )
				.ToArray() ?? Array.Empty<string>();
			result["bones"] = bones;
		}
		catch { result["bones"] = Array.Empty<string>(); }

		// Attachments — Model.Attachments is ModelAttachments with All IReadOnlyList<Attachment>
		try
		{
			var attachments = model.Attachments?.All?
				.Where( a => !string.IsNullOrEmpty( a.Name ) )
				.Select( a => a.Name )
				.ToArray() ?? Array.Empty<string>();
			result["attachments"] = attachments;
		}
		catch { result["attachments"] = Array.Empty<string>(); }

		// Physics body count — Model.Physics is PhysicsGroupDescription with Parts list
		try { result["physicsBodyCount"] = model.Physics?.Parts?.Count ?? 0; }
		catch { result["physicsBodyCount"] = 0; }

		// Material groups
		try { result["materialGroupCount"] = model.MaterialGroupCount; }
		catch { }

		return ToolHandlerBase.JsonResult( result );
	}

	// ──────────────────────────────────────────────
	//  install_asset
	// ──────────────────────────────────────────────

	[Tool( "install_asset", "Install a sbox.game library into the project's Libraries folder (the same path the Library Manager UI uses). Downloads source code, runs Editor.LibrarySystem.Install, and regenerates the solution so the new types compile. Idempotent — skips if Libraries/<ident>/ already exists.", RequiresMainThread = true )]
	[Param( "package_id", "Package identifier (e.g. 'conna.inventory', 'fish.shrimple_ragdolls').", Required = true )]
	[Param( "force", "Reinstall even if Libraries/<ident>/ already exists. Default: false.", Required = false, Type = "boolean", Default = "false" )]
	public static async Task<object> InstallAsset( JsonElement args )
	{
		var packageId = ToolHandlerBase.RequireString( args, "package_id" );
		var force = ToolHandlerBase.GetBool( args, "force", false );

		// Fast path: source already vendored under Libraries/<ident>/.
		// This is the format the engine's compile pipeline understands — same shape that
		// fish.shrimple_ragdolls has after a manual Library Manager install.
		var libsRoot = Path.Combine( PathNormalizer.GetProjectRoot(), "Libraries" );
		var libDir = Path.Combine( libsRoot, packageId );

		if ( !force && Directory.Exists( libDir ) )
		{
			return ToolHandlerBase.JsonResult( new
			{
				installed = true,
				alreadyInstalled = true,
				method = "skip-already-in-libraries",
				packageId,
				libraryPath = libDir
			} );
		}

		// Need the Revision.VersionId to call LibrarySystem.Install.
		Package pkg;
		try
		{
			pkg = await Package.FetchAsync( packageId, false );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Failed to fetch package '{packageId}': {ex.Message}" );
		}

		if ( pkg == null )
			return ToolHandlerBase.ErrorResult( $"Package not found on sbox.game: {packageId}" );

		var versionId = pkg.Revision?.VersionId ?? 0L;
		if ( versionId == 0L )
			return ToolHandlerBase.ErrorResult( $"Package '{packageId}' has no current revision; cannot install." );

		// LibrarySystem.Install is what the Library Manager UI calls under the hood —
		// downloads source files into Libraries/<ident>/, NOT the cloud cache. This is
		// what makes the package a real ProjectReference for the compiler.
		try
		{
			await LibrarySystem.Install( packageId, versionId, CancellationToken.None );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"LibrarySystem install failed for '{packageId}': {ex.Message}" );
		}

		// After source lands in Libraries/, regenerate the solution so the user's csproj
		// gets a ProjectReference to it. GenerateSolution returns a Task — must await,
		// otherwise the tool returns before the csproj is actually rewritten.
		bool solutionRegenerated = false;
		string regenError = null;
		try
		{
			await EditorUtility.Projects.GenerateSolution();
			solutionRegenerated = true;
		}
		catch ( Exception ex )
		{
			regenError = ex.Message;
		}

		return ToolHandlerBase.JsonResult( new
		{
			installed = true,
			alreadyInstalled = false,
			method = "library-system-install",
			packageId,
			versionId,
			libraryPath = libDir,
			libraryCreated = Directory.Exists( libDir ),
			solutionRegenerated,
			regenError
		} );
	}

	[Tool( "regenerate_solution", "Regenerate the project's .csproj/.slnx files. Wraps Editor.EditorUtility.Projects.GenerateSolution (async — awaits completion). Useful after manual edits to Libraries/ or .sbproj.", RequiresMainThread = true )]
	public static async Task<object> RegenerateSolution()
	{
		try
		{
			await EditorUtility.Projects.GenerateSolution();
			return ToolHandlerBase.JsonResult( new { regenerated = true } );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Solution regenerate failed: {ex.Message}" );
		}
	}

	// ──────────────────────────────────────────────
	//  list_asset_library
	// ──────────────────────────────────────────────

	[Tool( "list_asset_library", "Search the sbox.game package catalog. Returns up to N packages matching the query — pass any returned ident to install_asset." )]
	[Param( "query", "Search query string. Supports text and filters (e.g. 'inventory', 'type:library physics').", Required = false )]
	[Param( "limit", "Max results. Default: 20.", Required = false, Type = "integer", Default = "20" )]
	[Param( "skip", "Results to skip (pagination). Default: 0.", Required = false, Type = "integer", Default = "0" )]
	public static async Task<object> ListAssetLibrary( JsonElement args )
	{
		var query = ToolHandlerBase.GetString( args, "query" ) ?? "";
		var limit = Math.Clamp( ToolHandlerBase.GetInt( args, "limit", 20 ), 1, 100 );
		var skip = Math.Max( 0, ToolHandlerBase.GetInt( args, "skip", 0 ) );

		try
		{
			var result = await Package.FindAsync( query, limit, skip, CancellationToken.None );
			if ( result?.Packages == null )
				return ToolHandlerBase.JsonResult( new { count = 0, totalCount = 0, packages = Array.Empty<object>() } );

			var packages = result.Packages.Select( p => new
			{
				ident = p.FullIdent,
				title = p.Title,
				summary = p.Summary,
				type = p.TypeName,
				org = p.Org?.Ident,
				installed = AssetSystem.IsCloudInstalled( p.FullIdent )
			} ).ToArray();

			return ToolHandlerBase.JsonResult( new
			{
				count = packages.Length,
				totalCount = result.TotalCount,
				query,
				skip,
				packages
			} );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Library search failed: {ex.Message}" );
		}
	}

	// ──────────────────────────────────────────────
	//  Shared helpers
	// ──────────────────────────────────────────────

	private static GameObject ResolveGO( JsonElement args )
	{
		var name = ToolHandlerBase.GetString( args, "name" );
		var guid = ToolHandlerBase.GetString( args, "guid" );

		if ( string.IsNullOrEmpty( name ) && string.IsNullOrEmpty( guid ) )
			return null;

		var scene = SceneHelpers.ResolveActiveScene();
		if ( scene == null ) return null;

		return SceneHelpers.FindByGuidOrName( scene, guid, name );
	}

	private static object GONotFound( JsonElement args )
	{
		var name = ToolHandlerBase.GetString( args, "name" );
		var guid = ToolHandlerBase.GetString( args, "guid" );
		return ToolHandlerBase.ErrorResult( $"GameObject not found: {guid ?? name ?? "(no identifier)"}" );
	}

	private static string MapTypeToExtension( string type )
	{
		if ( string.IsNullOrEmpty( type ) ) return null;
		return type.ToLowerInvariant() switch
		{
			"model" => ".vmdl",
			"material" => ".vmat",
			"texture" => ".vtex",
			"sound" => ".sound",
			"prefab" => ".prefab",
			"scene" => ".scene",
			"shader" => ".shader",
			"particle" => ".vpcf",
			_ => $".{type}"
		};
	}

	private static bool IsAssetExtension( string ext )
	{
		return ext is ".vmdl" or ".vmat" or ".vtex" or ".sound" or ".sndevt"
			or ".prefab" or ".scene" or ".shader" or ".vpcf" or ".vmdl_c"
			or ".vmat_c" or ".vtex_c";
	}
}
