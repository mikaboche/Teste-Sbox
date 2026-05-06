using System;
using System.Linq;
using System.Text.Json;
using Sandbox;

namespace SboxPro;

public static class PhysicsTools
{
	// ──────────────────────────────────────────────
	//  add_collider
	// ──────────────────────────────────────────────

	[Tool( "add_collider", "Add a collider component to a GameObject. Type: 'box', 'sphere', 'capsule', or 'model'.", RequiresMainThread = true )]
	[Param( "name", "Name of the target GameObject.", Required = false )]
	[Param( "guid", "GUID of the target GameObject.", Required = false )]
	[Param( "collider_type", "Type: 'box', 'sphere', 'capsule', or 'model'. Default: 'box'.", Required = false, Enum = "box,sphere,capsule,model", Default = "box" )]
	[Param( "size", "For box: 'x,y,z' size. For sphere/capsule: radius (single number).", Required = false )]
	[Param( "center", "Center offset 'x,y,z'. Default: '0,0,0'.", Required = false )]
	[Param( "is_trigger", "Mark as trigger volume. Default: false", Required = false, Type = "boolean", Default = "false" )]
	public static object AddCollider( JsonElement args )
	{
		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );

		var colliderType = ToolHandlerBase.GetString( args, "collider_type", "box" )?.ToLowerInvariant();
		var sizeStr = ToolHandlerBase.GetString( args, "size" );
		var centerStr = ToolHandlerBase.GetString( args, "center" );
		var isTrigger = ToolHandlerBase.GetBool( args, "is_trigger", false );

		Collider comp;
		switch ( colliderType )
		{
			case "box":
				var box = go.Components.Create<BoxCollider>();
				if ( !string.IsNullOrEmpty( sizeStr ) ) box.Scale = ParseVec3( sizeStr, new Vector3( 50, 50, 50 ) );
				if ( !string.IsNullOrEmpty( centerStr ) ) box.Center = ParseVec3( centerStr, Vector3.Zero );
				comp = box;
				break;
			case "sphere":
				var sphere = go.Components.Create<SphereCollider>();
				if ( !string.IsNullOrEmpty( sizeStr ) && float.TryParse( sizeStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var sphereR ) )
					sphere.Radius = sphereR;
				if ( !string.IsNullOrEmpty( centerStr ) ) sphere.Center = ParseVec3( centerStr, Vector3.Zero );
				comp = sphere;
				break;
			case "capsule":
				var caps = go.Components.Create<CapsuleCollider>();
				if ( !string.IsNullOrEmpty( sizeStr ) && float.TryParse( sizeStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var capR ) )
					caps.Radius = capR;
				comp = caps;
				break;
			case "model":
				var modelCol = go.Components.Create<ModelCollider>();
				comp = modelCol;
				break;
			default:
				return ToolHandlerBase.ErrorResult( $"Unknown collider type: {colliderType}. Use 'box', 'sphere', 'capsule', or 'model'." );
		}

		comp.IsTrigger = isTrigger;

		return ToolHandlerBase.JsonResult( new
		{
			added = true,
			gameObject = go.Name,
			gameObjectGuid = go.Id.ToString(),
			componentType = comp.GetType().Name,
			componentGuid = comp.Id.ToString(),
			isTrigger
		} );
	}

	// ──────────────────────────────────────────────
	//  configure_collider
	// ──────────────────────────────────────────────

	[Tool( "configure_collider", "Modify properties of an existing Collider component on a GameObject.", RequiresMainThread = true )]
	[Param( "name", "Name of the target GameObject.", Required = false )]
	[Param( "guid", "GUID of the target GameObject.", Required = false )]
	[Param( "is_trigger", "Mark as trigger volume.", Required = false, Type = "boolean" )]
	[Param( "friction", "Friction override (0-1+).", Required = false, Type = "number" )]
	[Param( "elasticity", "Elasticity override (0-1).", Required = false, Type = "number" )]
	[Param( "static_collider", "Mark as static.", Required = false, Type = "boolean" )]
	public static object ConfigureCollider( JsonElement args )
	{
		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );

		var col = go.Components.Get<Collider>();
		if ( col == null )
			return ToolHandlerBase.ErrorResult( $"No Collider component on '{go.Name}'." );

		if ( args.TryGetProperty( "is_trigger", out _ ) )
			col.IsTrigger = ToolHandlerBase.GetBool( args, "is_trigger", col.IsTrigger );

		if ( args.TryGetProperty( "friction", out _ ) )
			col.Friction = ToolHandlerBase.GetFloat( args, "friction", 0f );

		if ( args.TryGetProperty( "elasticity", out _ ) )
			col.Elasticity = ToolHandlerBase.GetFloat( args, "elasticity", 0f );

		if ( args.TryGetProperty( "static_collider", out _ ) )
			col.Static = ToolHandlerBase.GetBool( args, "static_collider", col.Static );

		return ToolHandlerBase.JsonResult( new
		{
			configured = true,
			gameObject = go.Name,
			componentType = col.GetType().Name,
			isTrigger = col.IsTrigger,
			isStatic = col.Static,
			friction = col.Friction,
			elasticity = col.Elasticity
		} );
	}

	// ──────────────────────────────────────────────
	//  add_plane_collider
	// ──────────────────────────────────────────────

	[Tool( "add_plane_collider", "Add a PlaneCollider — infinite plane for ground/walls. Performance-friendly for static surfaces.", RequiresMainThread = true )]
	[Param( "name", "Name of the target GameObject.", Required = false )]
	[Param( "guid", "GUID of the target GameObject.", Required = false )]
	[Param( "normal", "Plane normal as 'x,y,z'. Default '0,0,1' (up).", Required = false )]
	[Param( "scale", "Scale 'x,y,z' (visual gizmo size).", Required = false )]
	public static object AddPlaneCollider( JsonElement args )
	{
		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );

		var normalStr = ToolHandlerBase.GetString( args, "normal" );
		var scaleStr = ToolHandlerBase.GetString( args, "scale" );

		var plane = go.Components.Create<PlaneCollider>();
		if ( !string.IsNullOrEmpty( normalStr ) ) plane.Normal = ParseVec3( normalStr, Vector3.Up );
		if ( !string.IsNullOrEmpty( scaleStr ) ) plane.Scale = ParseVec3( scaleStr, new Vector3( 100, 100, 1 ) );

		return ToolHandlerBase.JsonResult( new
		{
			added = true,
			gameObject = go.Name,
			componentGuid = plane.Id.ToString(),
			normal = $"{plane.Normal.x},{plane.Normal.y},{plane.Normal.z}"
		} );
	}

	// ──────────────────────────────────────────────
	//  add_hull_collider
	// ──────────────────────────────────────────────

	[Tool( "add_hull_collider", "Add a HullCollider — primitive hull (box, cone, cylinder).", RequiresMainThread = true )]
	[Param( "name", "Name of the target GameObject.", Required = false )]
	[Param( "guid", "GUID of the target GameObject.", Required = false )]
	[Param( "primitive", "Primitive type: 'box', 'cone', 'cylinder'. Default: 'box'.", Required = false, Enum = "box,cone,cylinder", Default = "box" )]
	[Param( "size", "For box: 'x,y,z'. For cone/cylinder: radius (single number).", Required = false )]
	[Param( "height", "Height (for cone/cylinder).", Required = false, Type = "number" )]
	public static object AddHullCollider( JsonElement args )
	{
		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );

		var primitive = ToolHandlerBase.GetString( args, "primitive", "box" )?.ToLowerInvariant();
		var sizeStr = ToolHandlerBase.GetString( args, "size" );
		var height = ToolHandlerBase.GetFloat( args, "height", 50f );

		var hull = go.Components.Create<HullCollider>();
		hull.Type = primitive switch
		{
			"cone" => HullCollider.PrimitiveType.Cone,
			"cylinder" => HullCollider.PrimitiveType.Cylinder,
			_ => HullCollider.PrimitiveType.Box
		};

		if ( !string.IsNullOrEmpty( sizeStr ) )
		{
			if ( hull.Type == HullCollider.PrimitiveType.Box )
				hull.BoxSize = ParseVec3( sizeStr, new Vector3( 50, 50, 50 ) );
			else if ( float.TryParse( sizeStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var radius ) )
				hull.Radius = radius;
		}

		if ( hull.Type != HullCollider.PrimitiveType.Box )
			hull.Height = height;

		return ToolHandlerBase.JsonResult( new
		{
			added = true,
			gameObject = go.Name,
			componentGuid = hull.Id.ToString(),
			primitive = hull.Type.ToString()
		} );
	}

	// ──────────────────────────────────────────────
	//  add_rigidbody / add_physics (alias)
	// ──────────────────────────────────────────────

	[Tool( "add_rigidbody", "Add a Rigidbody component for physics simulation.", RequiresMainThread = true )]
	[Param( "name", "Name of the target GameObject.", Required = false )]
	[Param( "guid", "GUID of the target GameObject.", Required = false )]
	[Param( "mass", "Mass override (0 = auto-calculate from colliders).", Required = false, Type = "number" )]
	[Param( "gravity", "Enable gravity. Default: true.", Required = false, Type = "boolean", Default = "true" )]
	[Param( "gravity_scale", "Gravity multiplier. Default: 1.0.", Required = false, Type = "number" )]
	[Param( "linear_damping", "Linear damping (0+). Default: 0.1.", Required = false, Type = "number" )]
	[Param( "angular_damping", "Angular damping (0+). Default: 1.0.", Required = false, Type = "number" )]
	[Param( "start_asleep", "Start in sleeping state.", Required = false, Type = "boolean", Default = "false" )]
	public static object AddRigidbody( JsonElement args )
	{
		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );

		var existing = go.Components.Get<Rigidbody>();
		if ( existing != null )
			return ToolHandlerBase.ErrorResult( $"'{go.Name}' already has a Rigidbody component." );

		var rb = go.Components.Create<Rigidbody>();

		if ( args.TryGetProperty( "mass", out _ ) )
			rb.MassOverride = ToolHandlerBase.GetFloat( args, "mass", 0f );

		rb.Gravity = ToolHandlerBase.GetBool( args, "gravity", true );

		if ( args.TryGetProperty( "gravity_scale", out _ ) )
			rb.GravityScale = ToolHandlerBase.GetFloat( args, "gravity_scale", 1f );

		if ( args.TryGetProperty( "linear_damping", out _ ) )
			rb.LinearDamping = ToolHandlerBase.GetFloat( args, "linear_damping", 0.1f );

		if ( args.TryGetProperty( "angular_damping", out _ ) )
			rb.AngularDamping = ToolHandlerBase.GetFloat( args, "angular_damping", 1f );

		rb.StartAsleep = ToolHandlerBase.GetBool( args, "start_asleep", false );

		return ToolHandlerBase.JsonResult( new
		{
			added = true,
			gameObject = go.Name,
			componentGuid = rb.Id.ToString(),
			gravity = rb.Gravity,
			mass = rb.MassOverride,
			gravityScale = rb.GravityScale
		} );
	}

	[Tool( "add_physics", "Alias for add_rigidbody (Lou-style naming).", RequiresMainThread = true )]
	[Param( "name", "Name of the target GameObject.", Required = false )]
	[Param( "guid", "GUID of the target GameObject.", Required = false )]
	[Param( "mass", "Mass override (0 = auto).", Required = false, Type = "number" )]
	[Param( "gravity", "Enable gravity. Default: true.", Required = false, Type = "boolean", Default = "true" )]
	public static object AddPhysics( JsonElement args ) => AddRigidbody( args );

	// ──────────────────────────────────────────────
	//  add_joint / create_joint (alias)
	// ──────────────────────────────────────────────

	[Tool( "add_joint", "Add a physics joint to a GameObject. Types: 'fixed' or 'hinge'.", RequiresMainThread = true )]
	[Param( "name", "Name of the target GameObject.", Required = false )]
	[Param( "guid", "GUID of the target GameObject.", Required = false )]
	[Param( "joint_type", "Joint type: 'fixed' or 'hinge'. Default: 'fixed'.", Required = false, Enum = "fixed,hinge", Default = "fixed" )]
	public static object AddJoint( JsonElement args )
	{
		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );

		var jointType = ToolHandlerBase.GetString( args, "joint_type", "fixed" )?.ToLowerInvariant();

		Joint joint;
		switch ( jointType )
		{
			case "hinge":
				joint = go.Components.Create<HingeJoint>();
				break;
			case "fixed":
			default:
				joint = go.Components.Create<FixedJoint>();
				break;
		}

		return ToolHandlerBase.JsonResult( new
		{
			added = true,
			gameObject = go.Name,
			componentGuid = joint.Id.ToString(),
			componentType = joint.GetType().Name
		} );
	}

	[Tool( "create_joint", "Alias for add_joint.", RequiresMainThread = true )]
	[Param( "name", "Name of the target GameObject.", Required = false )]
	[Param( "guid", "GUID of the target GameObject.", Required = false )]
	[Param( "joint_type", "Joint type: 'fixed' or 'hinge'.", Required = false, Enum = "fixed,hinge", Default = "fixed" )]
	public static object CreateJoint( JsonElement args ) => AddJoint( args );

	// ──────────────────────────────────────────────
	//  create_model_physics
	// ──────────────────────────────────────────────

	[Tool( "create_model_physics", "Add a ModelCollider configured to use physics from a model.", RequiresMainThread = true )]
	[Param( "name", "Name of the target GameObject.", Required = false )]
	[Param( "guid", "GUID of the target GameObject.", Required = false )]
	[Param( "model_path", "Model path (.vmdl). Required.", Required = true )]
	public static object CreateModelPhysics( JsonElement args )
	{
		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );

		var modelPath = ToolHandlerBase.RequireString( args, "model_path" );
		var model = Model.Load( modelPath );
		if ( model == null )
			return ToolHandlerBase.ErrorResult( $"Failed to load model: {modelPath}" );

		var col = go.Components.Get<ModelCollider>() ?? go.Components.Create<ModelCollider>();
		col.Model = model;

		return ToolHandlerBase.JsonResult( new
		{
			created = true,
			gameObject = go.Name,
			componentGuid = col.Id.ToString(),
			modelPath
		} );
	}

	// ──────────────────────────────────────────────
	//  create_character_controller
	// ──────────────────────────────────────────────

	[Tool( "create_character_controller", "Add a CharacterController for collision-constrained movement (no rigidbody required).", RequiresMainThread = true )]
	[Param( "name", "Name of the target GameObject.", Required = false )]
	[Param( "guid", "GUID of the target GameObject.", Required = false )]
	public static object CreateCharacterController( JsonElement args )
	{
		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );

		var existing = go.Components.Get<CharacterController>();
		if ( existing != null )
			return ToolHandlerBase.ErrorResult( $"'{go.Name}' already has a CharacterController." );

		var cc = go.Components.Create<CharacterController>();

		return ToolHandlerBase.JsonResult( new
		{
			added = true,
			gameObject = go.Name,
			componentGuid = cc.Id.ToString()
		} );
	}

	// ──────────────────────────────────────────────
	//  raycast
	// ──────────────────────────────────────────────

	[Tool( "raycast", "Cast a ray in the scene and return the first hit.", RequiresMainThread = true )]
	[Param( "from", "Start position 'x,y,z'. Required.", Required = true )]
	[Param( "to", "End position 'x,y,z'. Provide either 'to' or 'direction'+'distance'.", Required = false )]
	[Param( "direction", "Direction 'x,y,z' (used with distance).", Required = false )]
	[Param( "distance", "Max distance (used with direction). Default: 10000.", Required = false, Type = "number" )]
	public static object Raycast( JsonElement args )
	{
		var scene = SceneHelpers.ResolveActiveScene();
		if ( scene == null )
			return ToolHandlerBase.ErrorResult( "No active scene." );

		var fromStr = ToolHandlerBase.RequireString( args, "from" );
		var toStr = ToolHandlerBase.GetString( args, "to" );
		var dirStr = ToolHandlerBase.GetString( args, "direction" );
		var distance = ToolHandlerBase.GetFloat( args, "distance", 10000f );

		var from = ParseVec3( fromStr, Vector3.Zero );
		Vector3 to;

		if ( !string.IsNullOrEmpty( toStr ) )
		{
			to = ParseVec3( toStr, from );
		}
		else if ( !string.IsNullOrEmpty( dirStr ) )
		{
			var dir = ParseVec3( dirStr, Vector3.Forward ).Normal;
			to = from + dir * distance;
		}
		else
		{
			return ToolHandlerBase.ErrorResult( "Must provide either 'to' or 'direction' + 'distance'." );
		}

		var trace = scene.Trace.Ray( from, to ).Run();

		return ToolHandlerBase.JsonResult( new
		{
			hit = trace.Hit,
			distance = trace.Distance,
			startPosition = $"{from.x},{from.y},{from.z}",
			endPosition = $"{to.x},{to.y},{to.z}",
			hitPosition = trace.Hit ? $"{trace.HitPosition.x},{trace.HitPosition.y},{trace.HitPosition.z}" : null,
			normal = trace.Hit ? $"{trace.Normal.x},{trace.Normal.y},{trace.Normal.z}" : null,
			gameObject = trace.GameObject?.Name,
			gameObjectGuid = trace.GameObject?.Id.ToString()
		} );
	}

	// ──────────────────────────────────────────────
	//  Helpers
	// ──────────────────────────────────────────────

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
