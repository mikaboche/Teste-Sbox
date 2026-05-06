using System;
using System.Text.Json;
using Sandbox;

// TODO: NavMeshArea.LinkedCollider was deprecated in favour of the SceneVolume property
// inherited from VolumeComponent. Migrate when revisiting nav tools — for now the field
// still works, we just suppress the noise.
#pragma warning disable CS0618

namespace SboxPro;

public static class NavigationTools
{
	// ──────────────────────────────────────────────
	//  create_nav_mesh_agent
	// ──────────────────────────────────────────────

	[Tool( "create_nav_mesh_agent", "Create a GameObject with a NavMeshAgent component.", RequiresMainThread = true )]
	[Param( "name", "Name for the new GameObject. Default: 'NavMeshAgent'.", Required = false )]
	[Param( "position", "World position 'x,y,z'. Default: '0,0,0'.", Required = false )]
	[Param( "height", "Agent height. Default: 72.", Required = false, Type = "number" )]
	[Param( "radius", "Agent radius. Default: 16.", Required = false, Type = "number" )]
	[Param( "max_speed", "Maximum movement speed. Default: 200.", Required = false, Type = "number" )]
	[Param( "acceleration", "Maximum acceleration.", Required = false, Type = "number" )]
	[Param( "update_position", "Auto-write agent position to GameObject every frame. Default: true.", Required = false, Type = "boolean" )]
	[Param( "update_rotation", "Auto-rotate to face movement direction. Default: true.", Required = false, Type = "boolean" )]
	[Param( "auto_traverse_links", "Automatically traverse navmesh links. Default: true.", Required = false, Type = "boolean" )]
	public static object CreateNavMeshAgent( JsonElement args )
	{
		var name = ToolHandlerBase.GetString( args, "name", "NavMeshAgent" );
		var posStr = ToolHandlerBase.GetString( args, "position" );

		var go = SceneHelpers.CreateInScene( name );
		if ( !string.IsNullOrEmpty( posStr ) ) go.WorldPosition = ParseVec3( posStr, Vector3.Zero );

		var comp = go.Components.Create<NavMeshAgent>();
		ApplyAgentProps( comp, args );

		return ToolHandlerBase.JsonResult( new
		{
			created = true,
			gameObject = go.Name,
			gameObjectGuid = go.Id.ToString(),
			componentGuid = comp.Id.ToString(),
			height = comp.Height,
			radius = comp.Radius,
			maxSpeed = comp.MaxSpeed
		} );
	}

	// ──────────────────────────────────────────────
	//  configure_nav_mesh_agent
	// ──────────────────────────────────────────────

	[Tool( "configure_nav_mesh_agent", "Modify an existing NavMeshAgent component.", RequiresMainThread = true )]
	[Param( "name", "Name of GameObject with NavMeshAgent.", Required = false )]
	[Param( "guid", "GUID of GameObject with NavMeshAgent.", Required = false )]
	[Param( "height", "Agent height.", Required = false, Type = "number" )]
	[Param( "radius", "Agent radius.", Required = false, Type = "number" )]
	[Param( "max_speed", "Maximum movement speed.", Required = false, Type = "number" )]
	[Param( "acceleration", "Maximum acceleration.", Required = false, Type = "number" )]
	[Param( "update_position", "Auto-write agent position to GameObject.", Required = false, Type = "boolean" )]
	[Param( "update_rotation", "Auto-rotate to face movement direction.", Required = false, Type = "boolean" )]
	[Param( "auto_traverse_links", "Automatically traverse navmesh links.", Required = false, Type = "boolean" )]
	public static object ConfigureNavMeshAgent( JsonElement args )
	{
		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );

		var comp = go.Components.Get<NavMeshAgent>();
		if ( comp == null )
			return ToolHandlerBase.ErrorResult( $"'{go.Name}' has no NavMeshAgent component." );

		ApplyAgentProps( comp, args );

		return ToolHandlerBase.JsonResult( new
		{
			configured = true,
			gameObject = go.Name,
			height = comp.Height,
			radius = comp.Radius,
			maxSpeed = comp.MaxSpeed
		} );
	}

	// ──────────────────────────────────────────────
	//  nav_mesh_move_to
	// ──────────────────────────────────────────────

	[Tool( "nav_mesh_move_to", "Command a NavMeshAgent to move to a target position.", RequiresMainThread = true )]
	[Param( "name", "Name of GameObject with NavMeshAgent.", Required = false )]
	[Param( "guid", "GUID of GameObject with NavMeshAgent.", Required = false )]
	[Param( "target", "Target world position 'x,y,z'.", Required = true )]
	public static object NavMeshMoveTo( JsonElement args )
	{
		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );

		var comp = go.Components.Get<NavMeshAgent>();
		if ( comp == null )
			return ToolHandlerBase.ErrorResult( $"'{go.Name}' has no NavMeshAgent component." );

		var target = ParseVec3( ToolHandlerBase.RequireString( args, "target" ), Vector3.Zero );
		comp.MoveTo( target );

		return ToolHandlerBase.JsonResult( new
		{
			moving = true,
			gameObject = go.Name,
			target = $"{target.x},{target.y},{target.z}",
			isNavigating = comp.IsNavigating
		} );
	}

	// ──────────────────────────────────────────────
	//  create_nav_mesh_area
	// ──────────────────────────────────────────────

	[Tool( "create_nav_mesh_area", "Create a GameObject with a NavMeshArea component (influences navmesh generation; requires a Collider on same/linked GO).", RequiresMainThread = true )]
	[Param( "name", "Name for the new GameObject. Default: 'NavMeshArea'.", Required = false )]
	[Param( "position", "World position 'x,y,z'. Default: '0,0,0'.", Required = false )]
	[Param( "is_blocker", "Whether navmesh generation is fully disabled in this area. Default: false.", Required = false, Type = "boolean" )]
	[Param( "linked_collider_name", "Name of GameObject whose Collider this area uses. Default: same GameObject's collider.", Required = false )]
	[Param( "linked_collider_guid", "GUID of GameObject whose Collider this area uses.", Required = false )]
	public static object CreateNavMeshArea( JsonElement args )
	{
		var name = ToolHandlerBase.GetString( args, "name", "NavMeshArea" );
		var posStr = ToolHandlerBase.GetString( args, "position" );

		var go = SceneHelpers.CreateInScene( name );
		if ( !string.IsNullOrEmpty( posStr ) ) go.WorldPosition = ParseVec3( posStr, Vector3.Zero );

		var comp = go.Components.Create<NavMeshArea>();
		if ( args.TryGetProperty( "is_blocker", out _ ) )
			comp.IsBlocker = ToolHandlerBase.GetBool( args, "is_blocker", comp.IsBlocker );

		var linkName = ToolHandlerBase.GetString( args, "linked_collider_name" );
		var linkGuid = ToolHandlerBase.GetString( args, "linked_collider_guid" );
		if ( !string.IsNullOrEmpty( linkName ) || !string.IsNullOrEmpty( linkGuid ) )
		{
			var scene = SceneHelpers.ResolveActiveScene();
			var target = scene != null ? SceneHelpers.FindByGuidOrName( scene, linkGuid, linkName ) : null;
			if ( target == null )
				return ToolHandlerBase.ErrorResult( $"Linked-collider GameObject not found (name='{linkName}', guid='{linkGuid}')." );
			var col = target.Components.Get<Collider>();
			if ( col == null )
				return ToolHandlerBase.ErrorResult( $"'{target.Name}' has no Collider component." );
			comp.LinkedCollider = col;
		}
		else
		{
			var col = go.Components.Get<Collider>();
			if ( col != null ) comp.LinkedCollider = col;
		}

		return ToolHandlerBase.JsonResult( new
		{
			created = true,
			gameObject = go.Name,
			gameObjectGuid = go.Id.ToString(),
			componentGuid = comp.Id.ToString(),
			isBlocker = comp.IsBlocker,
			linkedCollider = comp.LinkedCollider?.GameObject.Name,
			note = "NavMeshArea needs a (typically trigger) Collider — set linked_collider_* or add one to the same GameObject."
		} );
	}

	// ──────────────────────────────────────────────
	//  create_nav_mesh_link
	// ──────────────────────────────────────────────

	[Tool( "create_nav_mesh_link", "Create a GameObject with a NavMeshLink component connecting two navmesh points.", RequiresMainThread = true )]
	[Param( "name", "Name for the new GameObject. Default: 'NavMeshLink'.", Required = false )]
	[Param( "position", "World position 'x,y,z'. Default: '0,0,0'.", Required = false )]
	[Param( "local_start", "Start position relative to GameObject 'x,y,z'. Default: '0,0,0'.", Required = false )]
	[Param( "local_end", "End position relative to GameObject 'x,y,z'. Default: '0,0,0'.", Required = false )]
	[Param( "bi_directional", "Allow traversal in both directions. Default: true.", Required = false, Type = "boolean" )]
	[Param( "connection_radius", "Search radius at endpoints to connect to navmesh.", Required = false, Type = "number" )]
	public static object CreateNavMeshLink( JsonElement args )
	{
		var name = ToolHandlerBase.GetString( args, "name", "NavMeshLink" );
		var posStr = ToolHandlerBase.GetString( args, "position" );

		var go = SceneHelpers.CreateInScene( name );
		if ( !string.IsNullOrEmpty( posStr ) ) go.WorldPosition = ParseVec3( posStr, Vector3.Zero );

		var comp = go.Components.Create<NavMeshLink>();

		var startStr = ToolHandlerBase.GetString( args, "local_start" );
		if ( !string.IsNullOrEmpty( startStr ) )
			comp.LocalStartPosition = ParseVec3( startStr, comp.LocalStartPosition );

		var endStr = ToolHandlerBase.GetString( args, "local_end" );
		if ( !string.IsNullOrEmpty( endStr ) )
			comp.LocalEndPosition = ParseVec3( endStr, comp.LocalEndPosition );

		if ( args.TryGetProperty( "bi_directional", out _ ) )
			comp.IsBiDirectional = ToolHandlerBase.GetBool( args, "bi_directional", comp.IsBiDirectional );

		if ( args.TryGetProperty( "connection_radius", out _ ) )
			comp.ConnectionRadius = ToolHandlerBase.GetFloat( args, "connection_radius", comp.ConnectionRadius );

		return ToolHandlerBase.JsonResult( new
		{
			created = true,
			gameObject = go.Name,
			gameObjectGuid = go.Id.ToString(),
			componentGuid = comp.Id.ToString(),
			localStart = $"{comp.LocalStartPosition.x},{comp.LocalStartPosition.y},{comp.LocalStartPosition.z}",
			localEnd = $"{comp.LocalEndPosition.x},{comp.LocalEndPosition.y},{comp.LocalEndPosition.z}",
			biDirectional = comp.IsBiDirectional,
			connectionRadius = comp.ConnectionRadius
		} );
	}

	// ──────────────────────────────────────────────
	//  Helpers
	// ──────────────────────────────────────────────

	private static void ApplyAgentProps( NavMeshAgent comp, JsonElement args )
	{
		if ( args.TryGetProperty( "height", out _ ) )
			comp.Height = ToolHandlerBase.GetFloat( args, "height", comp.Height );
		if ( args.TryGetProperty( "radius", out _ ) )
			comp.Radius = ToolHandlerBase.GetFloat( args, "radius", comp.Radius );
		if ( args.TryGetProperty( "max_speed", out _ ) )
			comp.MaxSpeed = ToolHandlerBase.GetFloat( args, "max_speed", comp.MaxSpeed );
		if ( args.TryGetProperty( "acceleration", out _ ) )
			comp.Acceleration = ToolHandlerBase.GetFloat( args, "acceleration", comp.Acceleration );
		if ( args.TryGetProperty( "update_position", out _ ) )
			comp.UpdatePosition = ToolHandlerBase.GetBool( args, "update_position", comp.UpdatePosition );
		if ( args.TryGetProperty( "update_rotation", out _ ) )
			comp.UpdateRotation = ToolHandlerBase.GetBool( args, "update_rotation", comp.UpdateRotation );
		if ( args.TryGetProperty( "auto_traverse_links", out _ ) )
			comp.AutoTraverseLinks = ToolHandlerBase.GetBool( args, "auto_traverse_links", comp.AutoTraverseLinks );
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
