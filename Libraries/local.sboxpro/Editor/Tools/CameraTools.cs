using System;
using System.Text.Json;
using Sandbox;
using Editor;

namespace SboxPro;

public static class CameraTools
{
	// ──────────────────────────────────────────────
	//  create_camera
	// ──────────────────────────────────────────────

	[Tool( "create_camera", "Create a GameObject with a CameraComponent.", RequiresMainThread = true )]
	[Param( "name", "Name for the new GameObject. Default: 'Camera'.", Required = false )]
	[Param( "position", "World position 'x,y,z'. Default: '0,0,0'.", Required = false )]
	[Param( "rotation", "Rotation as 'pitch,yaw,roll' degrees. Default: '0,0,0'.", Required = false )]
	[Param( "fov", "Field of view in degrees. Default: 60.", Required = false, Type = "number" )]
	[Param( "z_near", "Near clip plane. Default: 5.", Required = false, Type = "number" )]
	[Param( "z_far", "Far clip plane. Default: 10000.", Required = false, Type = "number" )]
	[Param( "is_main", "Mark as main camera. Default: true.", Required = false, Type = "boolean", Default = "true" )]
	[Param( "orthographic", "Use orthographic projection. Default: false.", Required = false, Type = "boolean", Default = "false" )]
	[Param( "orthographic_height", "Orthographic height (when orthographic=true). Default: 1200.", Required = false, Type = "number" )]
	[Param( "background_color", "Background color 'r,g,b' or 'r,g,b,a' (0-1).", Required = false )]
	[Param( "clear_flags", "Clear flags: 'none', 'color', 'depth', 'stencil', 'all'. Default: 'all'.", Required = false, Enum = "none,color,depth,stencil,all" )]
	[Param( "priority", "Camera priority (higher renders on top). Default: 0.", Required = false, Type = "integer" )]
	public static object CreateCamera( JsonElement args )
	{
		var name = ToolHandlerBase.GetString( args, "name", "Camera" );
		var posStr = ToolHandlerBase.GetString( args, "position" );
		var rotStr = ToolHandlerBase.GetString( args, "rotation" );

		var go = SceneHelpers.CreateInScene( name );
		if ( !string.IsNullOrEmpty( posStr ) ) go.WorldPosition = ParseVec3( posStr, Vector3.Zero );
		if ( !string.IsNullOrEmpty( rotStr ) )
		{
			var ang = ParseVec3( rotStr, Vector3.Zero );
			go.WorldRotation = new Angles( ang.x, ang.y, ang.z ).ToRotation();
		}

		var cam = go.Components.Create<CameraComponent>();
		ApplyCameraProps( cam, args );

		return ToolHandlerBase.JsonResult( new
		{
			created = true,
			gameObject = go.Name,
			gameObjectGuid = go.Id.ToString(),
			componentGuid = cam.Id.ToString(),
			fieldOfView = cam.FieldOfView,
			zNear = cam.ZNear,
			zFar = cam.ZFar,
			isMainCamera = cam.IsMainCamera,
			orthographic = cam.Orthographic,
			priority = cam.Priority
		} );
	}

	// ──────────────────────────────────────────────
	//  configure_camera
	// ──────────────────────────────────────────────

	[Tool( "configure_camera", "Configure properties on an existing CameraComponent.", RequiresMainThread = true )]
	[Param( "name", "Name of GameObject with the camera.", Required = false )]
	[Param( "guid", "GUID of GameObject with the camera.", Required = false )]
	[Param( "fov", "Field of view in degrees.", Required = false, Type = "number" )]
	[Param( "z_near", "Near clip plane.", Required = false, Type = "number" )]
	[Param( "z_far", "Far clip plane.", Required = false, Type = "number" )]
	[Param( "is_main", "Mark as main camera.", Required = false, Type = "boolean" )]
	[Param( "orthographic", "Use orthographic projection.", Required = false, Type = "boolean" )]
	[Param( "orthographic_height", "Orthographic height.", Required = false, Type = "number" )]
	[Param( "background_color", "Background color 'r,g,b' or 'r,g,b,a' (0-1).", Required = false )]
	[Param( "clear_flags", "Clear flags: 'none', 'color', 'depth', 'stencil', 'all'.", Required = false, Enum = "none,color,depth,stencil,all" )]
	[Param( "priority", "Camera priority (higher renders on top).", Required = false, Type = "integer" )]
	[Param( "enable_post_processing", "Enable/disable post processing on this camera.", Required = false, Type = "boolean" )]
	public static object ConfigureCamera( JsonElement args )
	{
		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );

		var cam = go.Components.Get<CameraComponent>();
		if ( cam == null )
			return ToolHandlerBase.ErrorResult( $"'{go.Name}' has no CameraComponent." );

		ApplyCameraProps( cam, args );

		if ( args.TryGetProperty( "enable_post_processing", out _ ) )
			cam.EnablePostProcessing = ToolHandlerBase.GetBool( args, "enable_post_processing", cam.EnablePostProcessing );

		return ToolHandlerBase.JsonResult( new
		{
			configured = true,
			gameObject = go.Name,
			fieldOfView = cam.FieldOfView,
			zNear = cam.ZNear,
			zFar = cam.ZFar,
			isMainCamera = cam.IsMainCamera,
			orthographic = cam.Orthographic,
			priority = cam.Priority,
			enablePostProcessing = cam.EnablePostProcessing
		} );
	}

	// ──────────────────────────────────────────────
	//  take_screenshot
	// ──────────────────────────────────────────────

	[Tool( "take_screenshot", "Capture a high-resolution screenshot using the active scene camera. Saved to the editor's screenshot directory.", RequiresMainThread = true )]
	[Param( "width", "Screenshot width in pixels. Default: 1920.", Required = false, Type = "integer" )]
	[Param( "height", "Screenshot height in pixels. Default: 1080.", Required = false, Type = "integer" )]
	public static object TakeScreenshot( JsonElement args )
	{
		var width = ToolHandlerBase.GetInt( args, "width", 1920 );
		var height = ToolHandlerBase.GetInt( args, "height", 1080 );

		if ( width <= 0 || height <= 0 )
			return ToolHandlerBase.ErrorResult( $"Invalid dimensions: {width}x{height}" );

		try
		{
			EditorScene.TakeHighResScreenshot( width, height );
			return ToolHandlerBase.JsonResult( new
			{
				captured = true,
				width,
				height,
				note = "Screenshot saved to editor screenshot directory."
			} );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Screenshot failed: {ex.Message}" );
		}
	}

	// ──────────────────────────────────────────────
	//  Helpers
	// ──────────────────────────────────────────────

	private static void ApplyCameraProps( CameraComponent cam, JsonElement args )
	{
		if ( args.TryGetProperty( "fov", out _ ) )
			cam.FieldOfView = ToolHandlerBase.GetFloat( args, "fov", cam.FieldOfView );
		if ( args.TryGetProperty( "z_near", out _ ) )
			cam.ZNear = ToolHandlerBase.GetFloat( args, "z_near", cam.ZNear );
		if ( args.TryGetProperty( "z_far", out _ ) )
			cam.ZFar = ToolHandlerBase.GetFloat( args, "z_far", cam.ZFar );
		if ( args.TryGetProperty( "is_main", out _ ) )
			cam.IsMainCamera = ToolHandlerBase.GetBool( args, "is_main", cam.IsMainCamera );
		if ( args.TryGetProperty( "orthographic", out _ ) )
			cam.Orthographic = ToolHandlerBase.GetBool( args, "orthographic", cam.Orthographic );
		if ( args.TryGetProperty( "orthographic_height", out _ ) )
			cam.OrthographicHeight = ToolHandlerBase.GetFloat( args, "orthographic_height", cam.OrthographicHeight );
		if ( args.TryGetProperty( "priority", out _ ) )
			cam.Priority = ToolHandlerBase.GetInt( args, "priority", cam.Priority );

		var bgStr = ToolHandlerBase.GetString( args, "background_color" );
		if ( !string.IsNullOrEmpty( bgStr ) )
			cam.BackgroundColor = ParseColor( bgStr, cam.BackgroundColor );

		var clearStr = ToolHandlerBase.GetString( args, "clear_flags" );
		if ( !string.IsNullOrEmpty( clearStr ) )
		{
			cam.ClearFlags = clearStr.ToLowerInvariant() switch
			{
				"none" => ClearFlags.None,
				"color" => ClearFlags.Color,
				"depth" => ClearFlags.Depth,
				"stencil" => ClearFlags.Stencil,
				"all" => ClearFlags.All,
				_ => cam.ClearFlags
			};
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
