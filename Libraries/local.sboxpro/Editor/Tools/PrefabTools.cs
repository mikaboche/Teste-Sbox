using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Sandbox;
using Editor;

namespace SboxPro;

public static class PrefabTools
{
	// ──────────────────────────────────────────────
	//  create_prefab — Bug fix §6.1
	// ──────────────────────────────────────────────

	[Tool( "create_prefab", "Save a GameObject as a .prefab file with full serialization (all components, properties, children). Bug fix: replaces Lou's broken stub serializer.", RequiresMainThread = true )]
	[Param( "name", "Name of the source GameObject.", Required = false )]
	[Param( "guid", "GUID of the source GameObject.", Required = false )]
	[Param( "path", "Target prefab file path (e.g. 'prefabs/player.prefab'). Normalized under Assets/.", Required = true )]
	[Param( "overwrite", "Overwrite if file exists. Default: false", Required = false, Type = "boolean", Default = "false" )]
	public static object CreatePrefab( JsonElement args )
	{
		var path = ToolHandlerBase.RequireString( args, "path" );
		var overwrite = ToolHandlerBase.GetBool( args, "overwrite", false );

		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );

		if ( !path.EndsWith( ".prefab", StringComparison.OrdinalIgnoreCase ) )
			path += ".prefab";

		var safePath = PathNormalizer.ResolveAssetPath( path );
		if ( safePath == null )
			return ToolHandlerBase.ErrorResult( $"Path outside project: {path}" );

		if ( File.Exists( safePath ) && !overwrite )
			return ToolHandlerBase.ErrorResult( $"Prefab already exists: {PathNormalizer.ToRelative( safePath )}. Set overwrite=true to replace." );

		try
		{
			var dir = Path.GetDirectoryName( safePath );
			if ( !string.IsNullOrEmpty( dir ) )
				Directory.CreateDirectory( dir );

			var prefabJson = RuntimeReflection.SerializePrefab( go );
			SerializationHelpers.WriteFile( safePath, prefabJson );

			// Round-trip verification: read back and check root object exists
			var readBack = PrefabSerializer.Load( safePath );
			var rootObj = PrefabSerializer.GetRootObject( readBack );
			if ( rootObj == null )
				return ToolHandlerBase.ErrorResult( "Prefab saved but round-trip verification failed: RootObject missing" );

			var allGOs = PrefabSerializer.GetAllGameObjects( readBack );

			return ToolHandlerBase.JsonResult( new
			{
				created = true,
				path = PathNormalizer.ToRelative( safePath ),
				sourceGameObject = go.Name,
				sourceGuid = go.Id.ToString(),
				gameObjectCount = allGOs.Count,
				componentCount = allGOs.Sum( g => (g as JsonObject)?["Components"] is JsonArray ca ? ca.Count : 0 ),
				roundTripVerified = true
			} );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Failed to create prefab: {ex.Message}" );
		}
	}

	// ──────────────────────────────────────────────
	//  instantiate_prefab
	// ──────────────────────────────────────────────

	[Tool( "instantiate_prefab", "Instantiate a prefab into the active scene at a given position.", RequiresMainThread = true )]
	[Param( "path", "Prefab file path (e.g. 'Assets/prefabs/player.prefab').", Required = true )]
	[Param( "position", "World position as 'x,y,z'. Default: '0,0,0'", Required = false )]
	[Param( "rotation", "Rotation as 'pitch,yaw,roll' degrees. Default: '0,0,0'", Required = false )]
	[Param( "name", "Override name for the instantiated root GameObject.", Required = false )]
	[Param( "parent_name", "Name of parent GameObject.", Required = false )]
	[Param( "parent_guid", "GUID of parent GameObject.", Required = false )]
	public static object InstantiatePrefab( JsonElement args )
	{
		var path = ToolHandlerBase.RequireString( args, "path" );
		var posStr = ToolHandlerBase.GetString( args, "position" );
		var rotStr = ToolHandlerBase.GetString( args, "rotation" );
		var overrideName = ToolHandlerBase.GetString( args, "name" );
		var parentName = ToolHandlerBase.GetString( args, "parent_name" );
		var parentGuid = ToolHandlerBase.GetString( args, "parent_guid" );

		var scene = SceneHelpers.ResolveActiveScene();
		if ( scene == null )
			return ToolHandlerBase.ErrorResult( "No active scene" );

		// PrefabFile is NOT indexed by ResourceLibrary — that registry is for
		// GameResource subclasses (Item/Recipe/etc), not engine-level prefabs.
		// Use PrefabFile.Load(absolutePath) directly. (#32)
		var assetPath = PathNormalizer.NormalizeAssetPath( path ); // for diagnostics
		var absolutePath = PathNormalizer.ToAbsolute( assetPath );
		if ( !System.IO.File.Exists( absolutePath ) )
			return ToolHandlerBase.ErrorResult( $"Prefab not found: {assetPath}" );

		try
		{
			// GameObject.GetPrefab returns the engine's cached template — works
			// once the asset is registered. Try the resource-style path (no
			// "Assets/" prefix) since that matches the format other s&box code
			// uses to look up prefabs. Fall back to PrefabFile.Load + GetScene
			// + RootObject if the cached path lookup misses (newly created
			// prefabs may not be in the engine's prefab cache yet).
			var resourcePath = PathNormalizer.ForResourceLibrary( path );
			var template = GameObject.GetPrefab( resourcePath )
				?? GameObject.GetPrefab( assetPath );

			if ( template == null )
			{
				// Last-resort fallback: load the file directly and pull the root.
				var prefabFile = PrefabFile.Load( absolutePath );
				if ( prefabFile != null )
				{
					var prefabScene = prefabFile.GetScene();
					template = prefabScene?.Root;
				}
			}

			if ( template == null )
				return ToolHandlerBase.ErrorResult( $"Could not load prefab template from: {assetPath}" );

			// `template.Clone()` clones into the template's scene context (the
			// prefab scope), NOT the active scene. Without `scene.Push()` the
			// new GameObject ends up orphaned — it gets a GUID and the call
			// "succeeds", but no scene tree references it, so it's garbage
			// collected. Push the active scene so Clone attaches there. (#32)
			GameObject go;
			using ( scene.Push() )
			{
				go = template.Clone( new Transform( Vector3.Zero ) );
			}

			if ( !string.IsNullOrEmpty( overrideName ) )
				go.Name = overrideName;

			if ( !string.IsNullOrEmpty( posStr ) )
				go.WorldPosition = RuntimeReflection.ParseVector3( posStr );

			if ( !string.IsNullOrEmpty( rotStr ) )
			{
				var parts = rotStr.Split( ',' );
				go.WorldRotation = new Angles( float.Parse( parts[0] ), float.Parse( parts[1] ), float.Parse( parts[2] ) ).ToRotation();
			}

			GameObject parent = null;
			if ( !string.IsNullOrEmpty( parentGuid ) || !string.IsNullOrEmpty( parentName ) )
			{
				parent = SceneHelpers.FindByGuidOrName( scene, parentGuid, parentName );
				if ( parent != null )
					go.Parent = parent;
			}

			return ToolHandlerBase.JsonResult( new
			{
				instantiated = true,
				prefabPath = assetPath,
				name = go.Name,
				guid = go.Id.ToString(),
				worldPosition = GameObjectTools.FormatVector3( go.WorldPosition ),
				parent = parent?.Name
			} );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Failed to instantiate prefab: {ex.Message}" );
		}
	}

	// ──────────────────────────────────────────────
	//  list_prefabs
	// ──────────────────────────────────────────────

	[Tool( "list_prefabs", "List all .prefab files in the project." )]
	[Param( "path", "Subdirectory to search (relative to project root). Default: entire project.", Required = false )]
	[Param( "filter", "Name filter substring (case-insensitive).", Required = false )]
	public static object ListPrefabs( JsonElement args )
	{
		var subDir = ToolHandlerBase.GetString( args, "path", "" );
		var filter = ToolHandlerBase.GetString( args, "filter" );
		var rootPath = PathNormalizer.GetProjectRoot();

		var searchDir = string.IsNullOrEmpty( subDir )
			? rootPath
			: PathNormalizer.Normalize( Path.Combine( rootPath, subDir ) );

		if ( !Directory.Exists( searchDir ) )
			return ToolHandlerBase.ErrorResult( $"Directory not found: {subDir}" );

		var prefabs = Directory.GetFiles( searchDir, "*.prefab", SearchOption.AllDirectories )
			.Select( f => new
			{
				path = PathNormalizer.ToRelative( f ),
				name = Path.GetFileNameWithoutExtension( f ),
				sizeBytes = new FileInfo( f ).Length
			} )
			.Where( p => string.IsNullOrEmpty( filter ) || p.name.Contains( filter, StringComparison.OrdinalIgnoreCase ) )
			.OrderBy( p => p.path )
			.ToArray();

		return ToolHandlerBase.JsonResult( new
		{
			count = prefabs.Length,
			prefabs
		} );
	}

	// ──────────────────────────────────────────────
	//  get_prefab_info
	// ──────────────────────────────────────────────

	[Tool( "get_prefab_info", "Get metadata of a single .prefab file from disk." )]
	[Param( "path", "Prefab file path (relative to project root).", Required = true )]
	public static object GetPrefabInfo( JsonElement args )
	{
		var path = ToolHandlerBase.RequireString( args, "path" );
		var assetPath = PathNormalizer.NormalizeAssetPath( path );
		var absPath = PathNormalizer.ToAbsolute( assetPath );

		if ( !File.Exists( absPath ) )
			return ToolHandlerBase.ErrorResult( $"Prefab not found: {assetPath}" );

		try
		{
			var prefab = PrefabSerializer.Load( absPath );
			var rootObj = PrefabSerializer.GetRootObject( prefab );
			var allGOs = PrefabSerializer.GetAllGameObjects( prefab );

			var rootName = rootObj != null
				? SerializationHelpers.GetString( rootObj, "Name" ) ?? "(unnamed)"
				: "(no root)";

			var components = new List<string>();
			foreach ( var go in allGOs )
			{
				if ( go is not JsonObject goObj ) continue;
				if ( goObj["Components"] is not JsonArray comps ) continue;
				foreach ( var comp in comps )
				{
					if ( comp is not JsonObject compObj ) continue;
					var typeName = SerializationHelpers.GetString( compObj, "__type" );
					if ( !string.IsNullOrEmpty( typeName ) )
						components.Add( typeName );
				}
			}

			var fi = new FileInfo( absPath );

			return ToolHandlerBase.JsonResult( new
			{
				path = assetPath,
				rootName,
				rootGuid = PrefabSerializer.GetGuid( prefab ),
				gameObjectCount = allGOs.Count,
				componentCount = components.Count,
				componentTypes = components.Distinct().OrderBy( c => c ).ToArray(),
				sizeBytes = fi.Length,
				lastModified = fi.LastWriteTimeUtc.ToString( "o" )
			} );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Failed to read prefab: {ex.Message}" );
		}
	}

	// ──────────────────────────────────────────────
	//  get_prefab_structure
	// ──────────────────────────────────────────────

	[Tool( "get_prefab_structure", "Get the full hierarchy structure of a .prefab file from disk JSON." )]
	[Param( "path", "Prefab file path.", Required = true )]
	[Param( "include_properties", "Include component property values. Default: false", Required = false, Type = "boolean", Default = "false" )]
	public static object GetPrefabStructure( JsonElement args )
	{
		var path = ToolHandlerBase.RequireString( args, "path" );
		var includeProps = ToolHandlerBase.GetBool( args, "include_properties", false );
		var assetPath = PathNormalizer.NormalizeAssetPath( path );
		var absPath = PathNormalizer.ToAbsolute( assetPath );

		if ( !File.Exists( absPath ) )
			return ToolHandlerBase.ErrorResult( $"Prefab not found: {assetPath}" );

		try
		{
			var prefab = PrefabSerializer.Load( absPath );
			var rootObj = PrefabSerializer.GetRootObject( prefab );

			if ( rootObj == null )
				return ToolHandlerBase.ErrorResult( "Prefab has no RootObject" );

			return ToolHandlerBase.JsonResult( new
			{
				path = assetPath,
				structure = BuildPrefabNode( rootObj, includeProps )
			} );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Failed to read prefab: {ex.Message}" );
		}
	}

	private static object BuildPrefabNode( JsonObject go, bool includeProps )
	{
		var result = new Dictionary<string, object>
		{
			["name"] = SerializationHelpers.GetString( go, "Name" ) ?? "(unnamed)",
			["guid"] = SerializationHelpers.GetString( go, "__guid" ),
			["enabled"] = go["Enabled"]?.GetValue<bool>() ?? true
		};

		if ( go["Components"] is JsonArray comps && comps.Count > 0 )
		{
			result["components"] = comps
				.OfType<JsonObject>()
				.Select( c =>
				{
					var comp = new Dictionary<string, object>
					{
						["type"] = SerializationHelpers.GetString( c, "__type" ),
						["guid"] = SerializationHelpers.GetString( c, "__guid" ),
						["enabled"] = c["__enabled"]?.GetValue<bool>() ?? true
					};

					if ( includeProps )
					{
						var props = new Dictionary<string, object>();
						foreach ( var prop in c )
						{
							if ( prop.Key.StartsWith( "__" ) ) continue;
							props[prop.Key] = prop.Value?.ToString();
						}
						if ( props.Count > 0 )
							comp["properties"] = props;
					}

					return (object)comp;
				} )
				.ToArray();
		}

		if ( go["Children"] is JsonArray children && children.Count > 0 )
		{
			result["children"] = children
				.OfType<JsonObject>()
				.Select( child => BuildPrefabNode( child, includeProps ) )
				.ToArray();
		}

		return result;
	}

	// ──────────────────────────────────────────────
	//  get_prefab_instances
	// ──────────────────────────────────────────────

	[Tool( "get_prefab_instances", "Find all instances of a prefab in the active scene.", RequiresMainThread = true )]
	[Param( "path", "Prefab asset path to search for.", Required = true )]
	public static object GetPrefabInstances( JsonElement args )
	{
		var path = ToolHandlerBase.RequireString( args, "path" );
		var assetPath = PathNormalizer.NormalizeAssetPath( path );

		var scene = SceneHelpers.ResolveActiveScene();
		if ( scene == null )
			return ToolHandlerBase.ErrorResult( "No active scene" );

		var instances = SceneHelpers.WalkAll( scene )
			.Where( go => go.IsPrefabInstance && go.IsPrefabInstanceRoot )
			.Where( go =>
			{
				var source = go.PrefabInstanceSource;
				if ( string.IsNullOrEmpty( source ) ) return false;
				return source.Contains( Path.GetFileNameWithoutExtension( assetPath ), StringComparison.OrdinalIgnoreCase )
					|| source.Equals( assetPath, StringComparison.OrdinalIgnoreCase );
			} )
			.Select( go => new
			{
				name = go.Name,
				guid = go.Id.ToString(),
				worldPosition = GameObjectTools.FormatVector3( go.WorldPosition ),
				prefabSource = go.PrefabInstanceSource,
				parent = go.Parent?.Name
			} )
			.ToArray();

		return ToolHandlerBase.JsonResult( new
		{
			prefabPath = assetPath,
			instanceCount = instances.Length,
			instances
		} );
	}

	// ──────────────────────────────────────────────
	//  break_from_prefab
	// ──────────────────────────────────────────────

	[Tool( "break_from_prefab", "Detach a prefab instance from its source prefab.", RequiresMainThread = true )]
	[Param( "name", "Name of the prefab instance GameObject.", Required = false )]
	[Param( "guid", "GUID of the prefab instance GameObject.", Required = false )]
	public static object BreakFromPrefab( JsonElement args )
	{
		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );

		if ( !go.IsPrefabInstance )
			return ToolHandlerBase.ErrorResult( $"'{go.Name}' is not a prefab instance" );

		var previousSource = go.PrefabInstanceSource;
		go.BreakFromPrefab();

		return ToolHandlerBase.JsonResult( new
		{
			broken = true,
			name = go.Name,
			guid = go.Id.ToString(),
			previousPrefabSource = previousSource,
			isPrefabInstance = go.IsPrefabInstance
		} );
	}

	// ──────────────────────────────────────────────
	//  update_from_prefab
	// ──────────────────────────────────────────────

	[Tool( "update_from_prefab", "Re-apply prefab changes to an instance.", RequiresMainThread = true )]
	[Param( "name", "Name of the prefab instance GameObject.", Required = false )]
	[Param( "guid", "GUID of the prefab instance GameObject.", Required = false )]
	public static object UpdateFromPrefab( JsonElement args )
	{
		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );

		if ( !go.IsPrefabInstance )
			return ToolHandlerBase.ErrorResult( $"'{go.Name}' is not a prefab instance" );

		var source = go.PrefabInstanceSource;
		go.UpdateFromPrefab();

		return ToolHandlerBase.JsonResult( new
		{
			updated = true,
			name = go.Name,
			guid = go.Id.ToString(),
			prefabSource = source
		} );
	}

	// ──────────────────────────────────────────────
	//  extract_to_prefab
	// ──────────────────────────────────────────────

	[Tool( "extract_to_prefab", "Workflow: save an in-scene GameObject as a new prefab, optionally replacing the original with a prefab instance.", RequiresMainThread = true )]
	[Param( "name", "Name of the source GameObject.", Required = false )]
	[Param( "guid", "GUID of the source GameObject.", Required = false )]
	[Param( "path", "Target prefab file path (e.g. 'prefabs/enemy.prefab').", Required = true )]
	[Param( "replace_with_instance", "Replace original GO with a prefab instance. Default: false", Required = false, Type = "boolean", Default = "false" )]
	public static object ExtractToPrefab( JsonElement args )
	{
		var path = ToolHandlerBase.RequireString( args, "path" );
		var replaceWithInstance = ToolHandlerBase.GetBool( args, "replace_with_instance", false );

		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );

		if ( !path.EndsWith( ".prefab", StringComparison.OrdinalIgnoreCase ) )
			path += ".prefab";

		var safePath = PathNormalizer.ResolveAssetPath( path );
		if ( safePath == null )
			return ToolHandlerBase.ErrorResult( $"Path outside project: {path}" );

		if ( File.Exists( safePath ) )
			return ToolHandlerBase.ErrorResult( $"Prefab already exists: {PathNormalizer.ToRelative( safePath )}" );

		try
		{
			// Step 1: Serialize and save the prefab
			var dir = Path.GetDirectoryName( safePath );
			if ( !string.IsNullOrEmpty( dir ) )
				Directory.CreateDirectory( dir );

			var prefabJson = RuntimeReflection.SerializePrefab( go );
			SerializationHelpers.WriteFile( safePath, prefabJson );

			var relativePath = PathNormalizer.ToRelative( safePath );
			var assetPath = PathNormalizer.NormalizeAssetPath( relativePath );
			var originalName = go.Name;
			var originalGuid = go.Id.ToString();

			string instanceGuid = null;

			// Step 2: Optionally replace with prefab instance
			if ( replaceWithInstance )
			{
				var parent = go.Parent;
				var worldPos = go.WorldPosition;
				var worldRot = go.WorldRotation;

				// GameObject.GetPrefab loads the template; Clone instantiates it
				// (replaces deprecated SceneUtility.Instantiate).
				var template = GameObject.GetPrefab( assetPath );
				if ( template != null )
				{
					var instance = template.Clone( new Transform( Vector3.Zero ) );
					instance.Name = originalName;
					instance.WorldPosition = worldPos;
					instance.WorldRotation = worldRot;
					instance.Parent = parent;
					instanceGuid = instance.Id.ToString();

					go.Destroy();
				}
			}

			return ToolHandlerBase.JsonResult( new
			{
				extracted = true,
				prefabPath = assetPath,
				sourceGameObject = originalName,
				sourceGuid = originalGuid,
				replacedWithInstance = replaceWithInstance && instanceGuid != null,
				instanceGuid
			} );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Failed to extract prefab: {ex.Message}" );
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
}
