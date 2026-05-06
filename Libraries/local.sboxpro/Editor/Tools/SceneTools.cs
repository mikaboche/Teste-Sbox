using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Sandbox;
using Editor;

namespace SboxPro;

public static class SceneTools
{
	// ──────────────────────────────────────────────
	//  list_scenes
	// ──────────────────────────────────────────────

	[Tool( "list_scenes", "List all .scene files in the project." )]
	[Param( "path", "Subdirectory to search (relative to project root). Default: entire project.", Required = false )]
	public static object ListScenes( JsonElement args )
	{
		var subDir = ToolHandlerBase.GetString( args, "path", "" );
		var rootPath = PathNormalizer.GetProjectRoot();

		var searchDir = string.IsNullOrEmpty( subDir )
			? rootPath
			: PathNormalizer.Normalize( Path.Combine( rootPath, subDir ) );

		if ( !Directory.Exists( searchDir ) )
			return ToolHandlerBase.ErrorResult( $"Directory not found: {subDir}" );

		var scenes = Directory.GetFiles( searchDir, "*.scene", SearchOption.AllDirectories )
			.Select( f =>
			{
				var rel = PathNormalizer.ToRelative( f );
				return new
				{
					path = rel,
					name = Path.GetFileNameWithoutExtension( f ),
					sizeBytes = new FileInfo( f ).Length
				};
			} )
			.OrderBy( s => s.path )
			.ToArray();

		return ToolHandlerBase.JsonResult( new
		{
			count = scenes.Length,
			scenes
		} );
	}

	// ──────────────────────────────────────────────
	//  load_scene
	// ──────────────────────────────────────────────

	[Tool( "load_scene", "Open a scene file in the editor session.", RequiresMainThread = true )]
	[Param( "path", "Scene file path (relative to project root, e.g. 'Assets/scenes/main.scene').", Required = true )]
	public static object LoadScene( JsonElement args )
	{
		var path = ToolHandlerBase.RequireString( args, "path" );
		var normalized = PathNormalizer.NormalizeAssetPath( path );
		var absolute = PathNormalizer.ToAbsolute( normalized );

		if ( !File.Exists( absolute ) )
			return ToolHandlerBase.ErrorResult( $"Scene file not found: {normalized}" );

		try
		{
			var asset = AssetSystem.FindByPath( normalized );
			if ( asset == null )
				return ToolHandlerBase.ErrorResult( $"Asset not found in AssetSystem: {normalized}" );

			asset.OpenInEditor();

			return ToolHandlerBase.JsonResult( new
			{
				loaded = true,
				path = normalized
			} );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Failed to load scene: {ex.Message}" );
		}
	}

	// ──────────────────────────────────────────────
	//  create_scene
	// ──────────────────────────────────────────────

	[Tool( "create_scene", "Create a new empty .scene file. Bug fix: always writes under Assets/.", RequiresMainThread = true )]
	[Param( "path", "Scene file path (e.g. 'scenes/test.scene'). Normalized under Assets/.", Required = true )]
	[Param( "open", "Open the scene in editor after creating. Default: true", Required = false, Type = "boolean", Default = "true" )]
	public static object CreateScene( JsonElement args )
	{
		var path = ToolHandlerBase.RequireString( args, "path" );
		var openAfter = ToolHandlerBase.GetBool( args, "open", true );

		if ( !path.EndsWith( ".scene", StringComparison.OrdinalIgnoreCase ) )
			path += ".scene";

		var safePath = PathNormalizer.ResolveAssetPath( path );
		if ( safePath == null )
			return ToolHandlerBase.ErrorResult( $"Path outside project: {path}" );

		if ( File.Exists( safePath ) )
			return ToolHandlerBase.ErrorResult( $"Scene already exists: {PathNormalizer.ToRelative( safePath )}" );

		try
		{
			var dir = Path.GetDirectoryName( safePath );
			if ( !string.IsNullOrEmpty( dir ) )
				Directory.CreateDirectory( dir );

			var sceneGuid = Guid.NewGuid().ToString();
			var sceneJson = new System.Text.Json.Nodes.JsonObject
			{
				["__guid"] = sceneGuid,
				["GameObjects"] = new System.Text.Json.Nodes.JsonArray(),
				["SceneProperties"] = new System.Text.Json.Nodes.JsonObject
				{
					["FixedUpdateFrequency"] = 50,
					["MaxFixedUpdates"] = 5,
					["TimeScale"] = 1,
					["UseFixedUpdate"] = true,
					["PhysicsSubSteps"] = 1,
					["ThreadedAnimation"] = true,
					["NetworkFrequency"] = 30
				},
				["ResourceVersion"] = 3,
				["Title"] = (string)null,
				["Description"] = (string)null,
				["__references"] = new System.Text.Json.Nodes.JsonArray(),
				["__version"] = 3
			};

			SerializationHelpers.WriteFile( safePath, sceneJson );

			var relativePath = PathNormalizer.ToRelative( safePath );

			if ( openAfter )
			{
				var asset = AssetSystem.FindByPath( PathNormalizer.NormalizeAssetPath( path ) );
				asset?.OpenInEditor();
			}

			return ToolHandlerBase.JsonResult( new
			{
				created = true,
				path = relativePath,
				guid = sceneGuid
			} );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Failed to create scene: {ex.Message}" );
		}
	}

	// ──────────────────────────────────────────────
	//  save_scene — Bug fix §6.2
	// ──────────────────────────────────────────────

	[Tool( "save_scene", "Save the active scene. Bug fix: handles unbound sessions via direct serialization.", RequiresMainThread = true )]
	[Param( "path", "Optional explicit path to save to (relative to project root). If omitted, uses session's bound path.", Required = false )]
	public static object SaveScene( JsonElement args )
	{
		var explicitPath = ToolHandlerBase.GetString( args, "path" );
		var session = SceneEditorSession.Active;

		if ( session == null )
			return ToolHandlerBase.ErrorResult( "No active editor session" );

		var scene = session.Scene;
		if ( scene == null )
			return ToolHandlerBase.ErrorResult( "Active session has no scene" );

		string targetPath;
		if ( !string.IsNullOrEmpty( explicitPath ) )
		{
			var safe = PathNormalizer.ResolveAssetPath( explicitPath );
			if ( safe == null )
				return ToolHandlerBase.ErrorResult( $"Path outside project: {explicitPath}" );
			targetPath = safe;
		}
		else
		{
			var boundPath = session.Scene?.Source?.ResourcePath;
			if ( string.IsNullOrEmpty( boundPath ) )
				return ToolHandlerBase.ErrorResult( "Scene has no bound path. Use save_scene_as with an explicit path, or provide the 'path' parameter." );
			// Scene.Source.ResourcePath returns the Assets/-relative path WITHOUT the
			// "Assets/" prefix (engine internal convention). PathNormalizer.ToAbsolute
			// blindly joined to project root, which dumped the file at <root>/scenes/
			// instead of <root>/Assets/scenes/. save_scene reported success but next
			// reload pulled stale content from the correct Assets/scenes/ location.
			// Issue #14. Use ResolveAssetPath which adds the missing Assets/ prefix.
			var safe = PathNormalizer.ResolveAssetPath( boundPath );
			if ( safe == null )
				return ToolHandlerBase.ErrorResult( $"Cannot resolve scene's bound path: {boundPath}" );
			targetPath = safe;
		}

		try
		{
			var dir = Path.GetDirectoryName( targetPath );
			if ( !string.IsNullOrEmpty( dir ) )
				Directory.CreateDirectory( dir );

			var json = RuntimeReflection.SerializeScene( scene );
			SerializationHelpers.WriteFile( targetPath, json );

			var relativePath = PathNormalizer.ToRelative( targetPath );

			return ToolHandlerBase.JsonResult( new
			{
				saved = true,
				path = relativePath,
				timestamp = File.GetLastWriteTimeUtc( targetPath ).ToString( "o" )
			} );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Failed to save scene: {ex.Message}" );
		}
	}

	// ──────────────────────────────────────────────
	//  save_scene_as
	// ──────────────────────────────────────────────

	[Tool( "save_scene_as", "Save the active scene to a specified path. Replaces broken Ozmium implementation.", RequiresMainThread = true )]
	[Param( "path", "Target file path (e.g. 'scenes/copy.scene'). Normalized under Assets/.", Required = true )]
	public static object SaveSceneAs( JsonElement args )
	{
		var path = ToolHandlerBase.RequireString( args, "path" );

		if ( !path.EndsWith( ".scene", StringComparison.OrdinalIgnoreCase ) )
			path += ".scene";

		var safePath = PathNormalizer.ResolveAssetPath( path );
		if ( safePath == null )
			return ToolHandlerBase.ErrorResult( $"Path outside project: {path}" );

		var session = SceneEditorSession.Active;
		if ( session == null )
			return ToolHandlerBase.ErrorResult( "No active editor session" );

		var scene = session.Scene;
		if ( scene == null )
			return ToolHandlerBase.ErrorResult( "Active session has no scene" );

		try
		{
			var dir = Path.GetDirectoryName( safePath );
			if ( !string.IsNullOrEmpty( dir ) )
				Directory.CreateDirectory( dir );

			var json = RuntimeReflection.SerializeScene( scene );
			SerializationHelpers.WriteFile( safePath, json );

			return ToolHandlerBase.JsonResult( new
			{
				saved = true,
				path = PathNormalizer.ToRelative( safePath ),
				timestamp = File.GetLastWriteTimeUtc( safePath ).ToString( "o" )
			} );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Failed to save scene: {ex.Message}" );
		}
	}

	// ──────────────────────────────────────────────
	//  get_scene_unsaved
	// ──────────────────────────────────────────────

	[Tool( "get_scene_unsaved", "Check if the active scene has unsaved changes.", RequiresMainThread = true )]
	public static object GetSceneUnsaved()
	{
		var session = SceneEditorSession.Active;
		if ( session == null )
			return ToolHandlerBase.ErrorResult( "No active editor session" );

		return ToolHandlerBase.JsonResult( new
		{
			hasUnsavedChanges = session.HasUnsavedChanges,
			scenePath = session.Scene?.Source?.ResourcePath ?? "(unbound)"
		} );
	}

	// ──────────────────────────────────────────────
	//  get_scene_summary
	// ──────────────────────────────────────────────

	[Tool( "get_scene_summary", "Aggregate summary of the active scene: object counts, component types, tags, prefabs.", RequiresMainThread = true )]
	public static object GetSceneSummary()
	{
		var scene = SceneHelpers.ResolveActiveScene();
		if ( scene == null )
			return ToolHandlerBase.ErrorResult( "No active scene" );

		var allObjects = SceneHelpers.WalkAll( scene ).ToList();
		var enabledCount = allObjects.Count( go => go.Enabled );

		var tagCounts = new Dictionary<string, int>();
		var componentCounts = new Dictionary<string, int>();
		var prefabCount = 0;

		foreach ( var go in allObjects )
		{
			foreach ( var tag in go.Tags.TryGetAll() )
			{
				tagCounts.TryGetValue( tag, out var c );
				tagCounts[tag] = c + 1;
			}

			foreach ( var comp in go.Components.GetAll() )
			{
				var typeName = comp.GetType().Name;
				componentCounts.TryGetValue( typeName, out var c );
				componentCounts[typeName] = c + 1;
			}

			if ( go.IsPrefabInstance )
				prefabCount++;
		}

		return ToolHandlerBase.JsonResult( new
		{
			totalObjects = allObjects.Count,
			enabledObjects = enabledCount,
			disabledObjects = allObjects.Count - enabledCount,
			prefabInstances = prefabCount,
			uniqueTags = tagCounts.OrderByDescending( kv => kv.Value ).Select( kv => new { tag = kv.Key, count = kv.Value } ).ToArray(),
			componentTypes = componentCounts.OrderByDescending( kv => kv.Value ).Select( kv => new { type = kv.Key, count = kv.Value } ).ToArray(),
			scenePath = SceneEditorSession.Active?.Scene?.Source?.ResourcePath ?? "(unbound)"
		} );
	}

	// ──────────────────────────────────────────────
	//  get_scene_statistics
	// ──────────────────────────────────────────────

	[Tool( "get_scene_statistics", "Enhanced scene statistics with hierarchy depth, component breakdown, and resource references.", RequiresMainThread = true )]
	public static object GetSceneStatistics()
	{
		var scene = SceneHelpers.ResolveActiveScene();
		if ( scene == null )
			return ToolHandlerBase.ErrorResult( "No active scene" );

		var allObjects = SceneHelpers.WalkAll( scene ).ToList();

		var maxDepth = 0;
		var totalComponents = 0;
		var componentCounts = new Dictionary<string, int>();
		var resourceRefs = new HashSet<string>();

		foreach ( var go in allObjects )
		{
			var depth = GetDepth( go );
			if ( depth > maxDepth ) maxDepth = depth;

			foreach ( var comp in go.Components.GetAll() )
			{
				totalComponents++;
				var typeName = comp.GetType().Name;
				componentCounts.TryGetValue( typeName, out var c );
				componentCounts[typeName] = c + 1;
			}
		}

		var rootCount = scene.Children.Count();

		return ToolHandlerBase.JsonResult( new
		{
			totalGameObjects = allObjects.Count,
			rootObjectCount = rootCount,
			maxHierarchyDepth = maxDepth,
			totalComponents,
			uniqueComponentTypes = componentCounts.Count,
			topComponents = componentCounts.OrderByDescending( kv => kv.Value ).Take( 10 ).Select( kv => new { type = kv.Key, count = kv.Value } ).ToArray(),
			scenePath = SceneEditorSession.Active?.Scene?.Source?.ResourcePath ?? "(unbound)"
		} );
	}

	private static int GetDepth( GameObject go )
	{
		int depth = 0;
		var current = go.Parent;
		while ( current != null )
		{
			depth++;
			current = current.Parent;
		}
		return depth;
	}
}
