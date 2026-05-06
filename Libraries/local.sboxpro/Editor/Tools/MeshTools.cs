using System;
using System.Linq;
using System.Text.Json;
using Sandbox;

namespace SboxPro;

public static class MeshTools
{
	// ──────────────────────────────────────────────
	//  get_mesh_info
	// ──────────────────────────────────────────────

	[Tool( "get_mesh_info", "Get info about a MeshComponent's polygon mesh: vertex/face/edge counts, bounds, smoothing.", RequiresMainThread = true )]
	[Param( "name", "Name of GameObject with MeshComponent.", Required = false )]
	[Param( "guid", "GUID of GameObject with MeshComponent.", Required = false )]
	public static object GetMeshInfo( JsonElement args )
	{
		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );

		var comp = go.Components.Get<MeshComponent>();
		if ( comp == null )
			return ToolHandlerBase.ErrorResult( $"'{go.Name}' has no MeshComponent." );

		var mesh = comp.Mesh;
		if ( mesh == null )
			return ToolHandlerBase.ErrorResult( $"MeshComponent on '{go.Name}' has no PolygonMesh." );

		var vertexCount = mesh.VertexHandles?.Count() ?? 0;
		var faceCount = mesh.FaceHandles?.Count() ?? 0;
		var edgeCount = mesh.HalfEdgeHandles?.Count() ?? 0;
		var bounds = mesh.CalculateBounds();

		return ToolHandlerBase.JsonResult( new
		{
			gameObject = go.Name,
			vertexCount,
			faceCount,
			halfEdgeCount = edgeCount,
			boundsMins = $"{bounds.Mins.x},{bounds.Mins.y},{bounds.Mins.z}",
			boundsMaxs = $"{bounds.Maxs.x},{bounds.Maxs.y},{bounds.Maxs.z}",
			collisionType = comp.Collision.ToString(),
			smoothingAngle = comp.SmoothingAngle,
			isDirty = mesh.IsDirty
		} );
	}

	// ──────────────────────────────────────────────
	//  set_vertex_position
	// ──────────────────────────────────────────────

	[Tool( "set_vertex_position", "Set the position of a vertex on a MeshComponent's PolygonMesh by vertex index.", RequiresMainThread = true )]
	[Param( "name", "Name of GameObject with MeshComponent.", Required = false )]
	[Param( "guid", "GUID of GameObject with MeshComponent.", Required = false )]
	[Param( "vertex_index", "Vertex index in the polygon mesh.", Required = true, Type = "integer" )]
	[Param( "position", "New world-space position 'x,y,z'.", Required = true )]
	[Param( "rebuild", "Call RebuildMesh() after applying. Default: true.", Required = false, Type = "boolean", Default = "true" )]
	public static object SetVertexPosition( JsonElement args )
	{
		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );
		var mesh = ResolveMesh( go, out var comp, out var err );
		if ( mesh == null ) return ToolHandlerBase.ErrorResult( err );

		var idx = ToolHandlerBase.GetInt( args, "vertex_index", -1 );
		if ( idx < 0 ) return ToolHandlerBase.ErrorResult( "vertex_index must be >= 0" );

		var pos = ParseVec3( ToolHandlerBase.RequireString( args, "position" ), Vector3.Zero );
		var handle = mesh.VertexHandleFromIndex( idx );
		mesh.SetVertexPosition( handle, pos );

		if ( ToolHandlerBase.GetBool( args, "rebuild", true ) )
			comp.RebuildMesh();

		return ToolHandlerBase.JsonResult( new
		{
			updated = true,
			gameObject = go.Name,
			vertexIndex = idx,
			position = $"{pos.x},{pos.y},{pos.z}"
		} );
	}

	// ──────────────────────────────────────────────
	//  set_vertex_color
	// ──────────────────────────────────────────────

	[Tool( "set_vertex_color", "Set the per-vertex color on a MeshComponent's PolygonMesh by half-edge index.", RequiresMainThread = true )]
	[Param( "name", "Name of GameObject with MeshComponent.", Required = false )]
	[Param( "guid", "GUID of GameObject with MeshComponent.", Required = false )]
	[Param( "half_edge_index", "Half-edge index (selects the face-vertex slot).", Required = true, Type = "integer" )]
	[Param( "color", "Color 'r,g,b' or 'r,g,b,a' (0-1).", Required = true )]
	[Param( "rebuild", "Call RebuildMesh() after applying. Default: true.", Required = false, Type = "boolean", Default = "true" )]
	public static object SetVertexColor( JsonElement args )
	{
		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );
		var mesh = ResolveMesh( go, out var comp, out var err );
		if ( mesh == null ) return ToolHandlerBase.ErrorResult( err );

		var idx = ToolHandlerBase.GetInt( args, "half_edge_index", -1 );
		if ( idx < 0 ) return ToolHandlerBase.ErrorResult( "half_edge_index must be >= 0" );

		var col = ParseColor( ToolHandlerBase.RequireString( args, "color" ), Color.White );
		var c32 = new Color32( (byte)(col.r * 255f), (byte)(col.g * 255f), (byte)(col.b * 255f), (byte)(col.a * 255f) );

		var handle = mesh.HalfEdgeHandleFromIndex( idx );
		mesh.SetVertexColor( handle, c32 );

		if ( ToolHandlerBase.GetBool( args, "rebuild", true ) )
			comp.RebuildMesh();

		return ToolHandlerBase.JsonResult( new
		{
			updated = true,
			gameObject = go.Name,
			halfEdgeIndex = idx,
			color = $"{col.r},{col.g},{col.b},{col.a}"
		} );
	}

	// ──────────────────────────────────────────────
	//  set_vertex_blend
	// ──────────────────────────────────────────────

	[Tool( "set_vertex_blend", "Set the per-vertex blend (RGBA layer weights) on a MeshComponent's PolygonMesh.", RequiresMainThread = true )]
	[Param( "name", "Name of GameObject with MeshComponent.", Required = false )]
	[Param( "guid", "GUID of GameObject with MeshComponent.", Required = false )]
	[Param( "half_edge_index", "Half-edge index (selects the face-vertex slot).", Required = true, Type = "integer" )]
	[Param( "blend", "Blend weights 'r,g,b' or 'r,g,b,a' (0-1).", Required = true )]
	[Param( "rebuild", "Call RebuildMesh() after applying. Default: true.", Required = false, Type = "boolean", Default = "true" )]
	public static object SetVertexBlend( JsonElement args )
	{
		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );
		var mesh = ResolveMesh( go, out var comp, out var err );
		if ( mesh == null ) return ToolHandlerBase.ErrorResult( err );

		var idx = ToolHandlerBase.GetInt( args, "half_edge_index", -1 );
		if ( idx < 0 ) return ToolHandlerBase.ErrorResult( "half_edge_index must be >= 0" );

		var col = ParseColor( ToolHandlerBase.RequireString( args, "blend" ), Color.White );
		var c32 = new Color32( (byte)(col.r * 255f), (byte)(col.g * 255f), (byte)(col.b * 255f), (byte)(col.a * 255f) );

		var handle = mesh.HalfEdgeHandleFromIndex( idx );
		mesh.SetVertexBlend( handle, c32 );

		if ( ToolHandlerBase.GetBool( args, "rebuild", true ) )
			comp.RebuildMesh();

		return ToolHandlerBase.JsonResult( new
		{
			updated = true,
			gameObject = go.Name,
			halfEdgeIndex = idx,
			blend = $"{col.r},{col.g},{col.b},{col.a}"
		} );
	}

	// ──────────────────────────────────────────────
	//  set_face_material
	// ──────────────────────────────────────────────

	[Tool( "set_face_material", "Assign a material to a face of a MeshComponent's PolygonMesh by face index.", RequiresMainThread = true )]
	[Param( "name", "Name of GameObject with MeshComponent.", Required = false )]
	[Param( "guid", "GUID of GameObject with MeshComponent.", Required = false )]
	[Param( "face_index", "Face index in the polygon mesh.", Required = true, Type = "integer" )]
	[Param( "material", "Path to a .vmat material file.", Required = true )]
	[Param( "rebuild", "Call RebuildMesh() after applying. Default: true.", Required = false, Type = "boolean", Default = "true" )]
	public static object SetFaceMaterial( JsonElement args )
	{
		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );
		var mesh = ResolveMesh( go, out var comp, out var err );
		if ( mesh == null ) return ToolHandlerBase.ErrorResult( err );

		var idx = ToolHandlerBase.GetInt( args, "face_index", -1 );
		if ( idx < 0 ) return ToolHandlerBase.ErrorResult( "face_index must be >= 0" );

		var matPath = ToolHandlerBase.RequireString( args, "material" );
		var handle = mesh.FaceHandleFromIndex( idx );
		mesh.SetFaceMaterial( handle, matPath );

		if ( ToolHandlerBase.GetBool( args, "rebuild", true ) )
			comp.RebuildMesh();

		return ToolHandlerBase.JsonResult( new
		{
			updated = true,
			gameObject = go.Name,
			faceIndex = idx,
			material = matPath
		} );
	}

	// ──────────────────────────────────────────────
	//  Helpers
	// ──────────────────────────────────────────────

	private static PolygonMesh ResolveMesh( GameObject go, out MeshComponent comp, out string error )
	{
		comp = go.Components.Get<MeshComponent>();
		if ( comp == null )
		{
			error = $"'{go.Name}' has no MeshComponent.";
			return null;
		}
		if ( comp.Mesh == null )
		{
			error = $"MeshComponent on '{go.Name}' has no PolygonMesh.";
			return null;
		}
		error = null;
		return comp.Mesh;
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

	private static Color ParseColor( string s, Color fallback )
	{
		if ( string.IsNullOrWhiteSpace( s ) ) return fallback;
		var parts = s.Split( ',' );
		var ci = System.Globalization.CultureInfo.InvariantCulture;
		if ( parts.Length < 3 ) return fallback;
		if ( !float.TryParse( parts[0], System.Globalization.NumberStyles.Float, ci, out var r ) ) return fallback;
		if ( !float.TryParse( parts[1], System.Globalization.NumberStyles.Float, ci, out var g ) ) return fallback;
		if ( !float.TryParse( parts[2], System.Globalization.NumberStyles.Float, ci, out var b ) ) return fallback;
		var a = 1f;
		if ( parts.Length >= 4 ) float.TryParse( parts[3], System.Globalization.NumberStyles.Float, ci, out a );
		return new Color( r, g, b, a );
	}
}
