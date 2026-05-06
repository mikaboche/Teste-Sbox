using System;
using System.Text.Json;
using Sandbox;

namespace SboxPro;

public static class TerrainTools
{
	// ──────────────────────────────────────────────
	//  create_terrain
	// ──────────────────────────────────────────────

	[Tool( "create_terrain", "Create a GameObject with a Terrain component (heightmap-based).", RequiresMainThread = true )]
	[Param( "name", "Name for the new GameObject. Default: 'Terrain'.", Required = false )]
	[Param( "position", "World position 'x,y,z'. Default: '0,0,0'.", Required = false )]
	[Param( "terrain_size", "Uniform world size (width × length). Default: keep component default.", Required = false, Type = "number" )]
	[Param( "terrain_height", "Maximum world height. Default: keep component default.", Required = false, Type = "number" )]
	[Param( "resolution", "Heightmap resolution (powers of 2 typical, e.g. 512). Default: keep storage default.", Required = false, Type = "integer" )]
	[Param( "enable_collision", "Generate collision mesh. Default: true.", Required = false, Type = "boolean" )]
	public static object CreateTerrain( JsonElement args )
	{
		var name = ToolHandlerBase.GetString( args, "name", "Terrain" );
		var posStr = ToolHandlerBase.GetString( args, "position" );

		var go = SceneHelpers.CreateInScene( name );
		if ( !string.IsNullOrEmpty( posStr ) ) go.WorldPosition = ParseVec3( posStr, Vector3.Zero );

		var comp = go.Components.Create<Terrain>();
		ApplyTerrainProps( comp, args );

		try { comp.Create(); }
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Terrain.Create() failed: {ex.Message}" );
		}

		return ToolHandlerBase.JsonResult( new
		{
			created = true,
			gameObject = go.Name,
			gameObjectGuid = go.Id.ToString(),
			componentGuid = comp.Id.ToString(),
			terrainSize = comp.TerrainSize,
			terrainHeight = comp.TerrainHeight,
			resolution = comp.Storage?.Resolution
		} );
	}

	// ──────────────────────────────────────────────
	//  configure_terrain
	// ──────────────────────────────────────────────

	[Tool( "configure_terrain", "Modify properties on an existing Terrain component.", RequiresMainThread = true )]
	[Param( "name", "Name of GameObject with Terrain.", Required = false )]
	[Param( "guid", "GUID of GameObject with Terrain.", Required = false )]
	[Param( "terrain_size", "Uniform world size.", Required = false, Type = "number" )]
	[Param( "terrain_height", "Maximum world height.", Required = false, Type = "number" )]
	[Param( "resolution", "Heightmap resolution.", Required = false, Type = "integer" )]
	[Param( "enable_collision", "Generate collision.", Required = false, Type = "boolean" )]
	[Param( "rebuild", "Call Create() to rebuild after applying changes. Default: true.", Required = false, Type = "boolean", Default = "true" )]
	public static object ConfigureTerrain( JsonElement args )
	{
		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );

		var comp = go.Components.Get<Terrain>();
		if ( comp == null )
			return ToolHandlerBase.ErrorResult( $"'{go.Name}' has no Terrain component." );

		ApplyTerrainProps( comp, args );

		if ( ToolHandlerBase.GetBool( args, "rebuild", true ) )
			comp.Create();

		return ToolHandlerBase.JsonResult( new
		{
			configured = true,
			gameObject = go.Name,
			terrainSize = comp.TerrainSize,
			terrainHeight = comp.TerrainHeight,
			resolution = comp.Storage?.Resolution,
			enableCollision = comp.EnableCollision
		} );
	}

	// ──────────────────────────────────────────────
	//  terrain_raycast
	// ──────────────────────────────────────────────

	[Tool( "terrain_raycast", "Cast a ray against a Terrain component and return the local-space hit position." )]
	[Param( "name", "Name of GameObject with Terrain.", Required = false )]
	[Param( "guid", "GUID of GameObject with Terrain.", Required = false )]
	[Param( "ray_origin", "Ray origin 'x,y,z'.", Required = true )]
	[Param( "ray_direction", "Ray direction 'x,y,z' (will be normalized).", Required = true )]
	[Param( "max_distance", "Max ray distance. Default: 50000.", Required = false, Type = "number" )]
	public static object TerrainRaycast( JsonElement args )
	{
		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );

		var comp = go.Components.Get<Terrain>();
		if ( comp == null )
			return ToolHandlerBase.ErrorResult( $"'{go.Name}' has no Terrain component." );

		var origin = ParseVec3( ToolHandlerBase.RequireString( args, "ray_origin" ), Vector3.Zero );
		var dir = ParseVec3( ToolHandlerBase.RequireString( args, "ray_direction" ), Vector3.Forward );
		var maxDist = ToolHandlerBase.GetFloat( args, "max_distance", 50000f );

		var ray = new Ray( origin, dir.Normal );
		var hit = comp.RayIntersects( ray, maxDist, out var localPos );

		return ToolHandlerBase.JsonResult( new
		{
			hit,
			gameObject = go.Name,
			localPosition = hit ? $"{localPos.x},{localPos.y},{localPos.z}" : null
		} );
	}

	// ──────────────────────────────────────────────
	//  get_terrain_info
	// ──────────────────────────────────────────────

	[Tool( "get_terrain_info", "Get info about a Terrain component (size, resolution, materials, storage state)." )]
	[Param( "name", "Name of GameObject with Terrain.", Required = false )]
	[Param( "guid", "GUID of GameObject with Terrain.", Required = false )]
	public static object GetTerrainInfo( JsonElement args )
	{
		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );

		var comp = go.Components.Get<Terrain>();
		if ( comp == null )
			return ToolHandlerBase.ErrorResult( $"'{go.Name}' has no Terrain component." );

		var storage = comp.Storage;
		return ToolHandlerBase.JsonResult( new
		{
			gameObject = go.Name,
			terrainSize = comp.TerrainSize,
			terrainHeight = comp.TerrainHeight,
			enableCollision = comp.EnableCollision,
			subdivisionFactor = comp.SubdivisionFactor,
			subdivisionLodCount = comp.SubdivisionLodCount,
			clipMapLodLevels = comp.ClipMapLodLevels,
			hasStorage = storage != null,
			resolution = storage?.Resolution,
			heightMapSize = storage?.HeightMap?.Length ?? 0,
			controlMapSize = storage?.ControlMap?.Length ?? 0,
			materialCount = storage?.Materials?.Count ?? 0
		} );
	}

	// ──────────────────────────────────────────────
	//  terrain_sync_cpu
	// ──────────────────────────────────────────────

	[Tool( "terrain_sync_cpu", "Download dirty terrain regions from GPU to CPU (call after sculpting to make changes saveable).", RequiresMainThread = true )]
	[Param( "name", "Name of GameObject with Terrain.", Required = false )]
	[Param( "guid", "GUID of GameObject with Terrain.", Required = false )]
	[Param( "sync_height", "Sync HeightMap. Default: true.", Required = false, Type = "boolean", Default = "true" )]
	[Param( "sync_control", "Sync ControlMap. Default: true.", Required = false, Type = "boolean", Default = "true" )]
	public static object TerrainSyncCpu( JsonElement args )
	{
		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );

		var comp = go.Components.Get<Terrain>();
		if ( comp == null )
			return ToolHandlerBase.ErrorResult( $"'{go.Name}' has no Terrain component." );

		var syncHeight = ToolHandlerBase.GetBool( args, "sync_height", true );
		var syncControl = ToolHandlerBase.GetBool( args, "sync_control", true );

		var flags = Terrain.SyncFlags.Height & ~Terrain.SyncFlags.Height; // start as zero/none
		if ( syncHeight ) flags |= Terrain.SyncFlags.Height;
		if ( syncControl ) flags |= Terrain.SyncFlags.Control;

		var resolution = comp.Storage?.Resolution ?? 0;
		var region = new RectInt( 0, 0, resolution, resolution );
		comp.SyncCPUTexture( flags, region );

		return ToolHandlerBase.JsonResult( new
		{
			synced = true,
			gameObject = go.Name,
			syncHeight,
			syncControl,
			resolution
		} );
	}

	// ──────────────────────────────────────────────
	//  Helpers
	// ──────────────────────────────────────────────

	private static void ApplyTerrainProps( Terrain comp, JsonElement args )
	{
		if ( args.TryGetProperty( "terrain_size", out _ ) )
			comp.TerrainSize = ToolHandlerBase.GetFloat( args, "terrain_size", comp.TerrainSize );
		if ( args.TryGetProperty( "terrain_height", out _ ) )
			comp.TerrainHeight = ToolHandlerBase.GetFloat( args, "terrain_height", comp.TerrainHeight );
		if ( args.TryGetProperty( "enable_collision", out _ ) )
			comp.EnableCollision = ToolHandlerBase.GetBool( args, "enable_collision", comp.EnableCollision );

		if ( args.TryGetProperty( "resolution", out _ ) )
		{
			var res = ToolHandlerBase.GetInt( args, "resolution", comp.Storage?.Resolution ?? 512 );
			if ( comp.Storage != null )
				comp.Storage.SetResolution( res );
		}
	}

	private static GameObject ResolveGO( JsonElement args )
	{
		var name = ToolHandlerBase.GetString( args, "name" );
		var guid = ToolHandlerBase.GetString( args, "guid" );
		if ( string.IsNullOrEmpty( name ) && string.IsNullOrEmpty( guid ) ) return null;

		var scene = SceneHelpers.ResolveActiveScene();
		if ( scene == null ) return null;
		return SceneHelpers.FindByGuidOrName( scene, guid, name );
	}

	private static object GONotFound( JsonElement args )
	{
		var name = ToolHandlerBase.GetString( args, "name" );
		var guid = ToolHandlerBase.GetString( args, "guid" );
		return ToolHandlerBase.ErrorResult( $"GameObject not found (name='{name}', guid='{guid}')." );
	}

	private static Vector3 ParseVec3( string s, Vector3 fallback )
	{
		if ( string.IsNullOrWhiteSpace( s ) ) return fallback;
		var parts = s.Split( ',' );
		if ( parts.Length < 3 ) return fallback;
		var ci = System.Globalization.CultureInfo.InvariantCulture;
		if ( !float.TryParse( parts[0], System.Globalization.NumberStyles.Float, ci, out var x ) ) return fallback;
		if ( !float.TryParse( parts[1], System.Globalization.NumberStyles.Float, ci, out var y ) ) return fallback;
		if ( !float.TryParse( parts[2], System.Globalization.NumberStyles.Float, ci, out var z ) ) return fallback;
		return new Vector3( x, y, z );
	}
}
