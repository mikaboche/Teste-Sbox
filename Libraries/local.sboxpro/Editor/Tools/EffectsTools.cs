using System;
using System.Text.Json;
using Sandbox;

namespace SboxPro;

public static class EffectsTools
{
	// ──────────────────────────────────────────────
	//  create_particle_effect
	// ──────────────────────────────────────────────

	[Tool( "create_particle_effect", "Create a GameObject with a ParticleEffect component.", RequiresMainThread = true )]
	[Param( "name", "Name for the new GameObject. Default: 'ParticleEffect'.", Required = false )]
	[Param( "position", "World position 'x,y,z'. Default: '0,0,0'.", Required = false )]
	[Param( "max_particles", "Maximum simultaneous particles. Default: 1000.", Required = false, Type = "integer" )]
	[Param( "tint", "Tint color 'r,g,b' or 'r,g,b,a' (0-1).", Required = false )]
	[Param( "force_direction", "Force vector 'x,y,z'.", Required = false )]
	[Param( "constant_movement", "Constant movement vector 'x,y,z'.", Required = false )]
	[Param( "apply_color", "Enable per-particle color. Default: true.", Required = false, Type = "boolean" )]
	[Param( "apply_rotation", "Enable per-particle rotation.", Required = false, Type = "boolean" )]
	[Param( "collision", "Enable particle collisions.", Required = false, Type = "boolean" )]
	[Param( "collision_radius", "Collision radius.", Required = false, Type = "number" )]
	public static object CreateParticleEffect( JsonElement args )
	{
		var name = ToolHandlerBase.GetString( args, "name", "ParticleEffect" );
		var posStr = ToolHandlerBase.GetString( args, "position" );

		var go = SceneHelpers.CreateInScene( name );
		if ( !string.IsNullOrEmpty( posStr ) ) go.WorldPosition = ParseVec3( posStr, Vector3.Zero );

		var comp = go.Components.Create<ParticleEffect>();
		ApplyParticleProps( comp, args );

		return ToolHandlerBase.JsonResult( new
		{
			created = true,
			gameObject = go.Name,
			gameObjectGuid = go.Id.ToString(),
			componentGuid = comp.Id.ToString(),
			maxParticles = comp.MaxParticles
		} );
	}

	// ──────────────────────────────────────────────
	//  configure_particle_effect
	// ──────────────────────────────────────────────

	[Tool( "configure_particle_effect", "Modify an existing ParticleEffect component.", RequiresMainThread = true )]
	[Param( "name", "Name of GameObject with ParticleEffect.", Required = false )]
	[Param( "guid", "GUID of GameObject with ParticleEffect.", Required = false )]
	[Param( "max_particles", "Maximum particles.", Required = false, Type = "integer" )]
	[Param( "tint", "Tint color 'r,g,b' or 'r,g,b,a'.", Required = false )]
	[Param( "force_direction", "Force vector 'x,y,z'.", Required = false )]
	[Param( "constant_movement", "Constant movement vector 'x,y,z'.", Required = false )]
	[Param( "apply_color", "Enable per-particle color.", Required = false, Type = "boolean" )]
	[Param( "apply_rotation", "Enable per-particle rotation.", Required = false, Type = "boolean" )]
	[Param( "collision", "Enable collisions.", Required = false, Type = "boolean" )]
	[Param( "collision_radius", "Collision radius.", Required = false, Type = "number" )]
	public static object ConfigureParticleEffect( JsonElement args )
	{
		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );

		var comp = go.Components.Get<ParticleEffect>();
		if ( comp == null )
			return ToolHandlerBase.ErrorResult( $"'{go.Name}' has no ParticleEffect component." );

		ApplyParticleProps( comp, args );

		return ToolHandlerBase.JsonResult( new
		{
			configured = true,
			gameObject = go.Name,
			maxParticles = comp.MaxParticles,
			tint = $"{comp.Tint.r},{comp.Tint.g},{comp.Tint.b},{comp.Tint.a}"
		} );
	}

	// ──────────────────────────────────────────────
	//  create_fog_volume
	// ──────────────────────────────────────────────

	[Tool( "create_fog_volume", "Create a GameObject with a VolumetricFogVolume component.", RequiresMainThread = true )]
	[Param( "name", "Name for the new GameObject. Default: 'FogVolume'.", Required = false )]
	[Param( "position", "World position 'x,y,z'. Default: '0,0,0'.", Required = false )]
	[Param( "bounds_mins", "Local bounds min corner 'x,y,z'. Default: '-256,-256,-256'.", Required = false )]
	[Param( "bounds_maxs", "Local bounds max corner 'x,y,z'. Default: '256,256,256'.", Required = false )]
	[Param( "strength", "Fog strength multiplier. Default: 1.", Required = false, Type = "number" )]
	[Param( "falloff_exponent", "Falloff exponent. Default: 1.", Required = false, Type = "number" )]
	public static object CreateFogVolume( JsonElement args )
	{
		var name = ToolHandlerBase.GetString( args, "name", "FogVolume" );
		var posStr = ToolHandlerBase.GetString( args, "position" );
		var minsStr = ToolHandlerBase.GetString( args, "bounds_mins" );
		var maxsStr = ToolHandlerBase.GetString( args, "bounds_maxs" );

		var go = SceneHelpers.CreateInScene( name );
		if ( !string.IsNullOrEmpty( posStr ) ) go.WorldPosition = ParseVec3( posStr, Vector3.Zero );

		var comp = go.Components.Create<VolumetricFogVolume>();
		var mins = ParseVec3( minsStr, new Vector3( -256, -256, -256 ) );
		var maxs = ParseVec3( maxsStr, new Vector3( 256, 256, 256 ) );
		comp.Bounds = new BBox( mins, maxs );

		if ( args.TryGetProperty( "strength", out _ ) )
			comp.Strength = ToolHandlerBase.GetFloat( args, "strength", comp.Strength );
		if ( args.TryGetProperty( "falloff_exponent", out _ ) )
			comp.FalloffExponent = ToolHandlerBase.GetFloat( args, "falloff_exponent", comp.FalloffExponent );

		return ToolHandlerBase.JsonResult( new
		{
			created = true,
			gameObject = go.Name,
			gameObjectGuid = go.Id.ToString(),
			componentGuid = comp.Id.ToString(),
			strength = comp.Strength,
			falloffExponent = comp.FalloffExponent
		} );
	}

	// ──────────────────────────────────────────────
	//  create_beam_effect
	// ──────────────────────────────────────────────

	[Tool( "create_beam_effect", "Create a GameObject with a LineRenderer component for beam/laser/trail rendering.", RequiresMainThread = true )]
	[Param( "name", "Name for the new GameObject. Default: 'Beam'.", Required = false )]
	[Param( "position", "World position 'x,y,z'. Default: '0,0,0'.", Required = false )]
	[Param( "additive", "Use additive blending. Default: false.", Required = false, Type = "boolean" )]
	[Param( "opaque", "Render as opaque. Default: false.", Required = false, Type = "boolean" )]
	[Param( "wireframe", "Render as wireframe. Default: false.", Required = false, Type = "boolean" )]
	[Param( "cast_shadows", "Cast shadows. Default: false.", Required = false, Type = "boolean" )]
	[Param( "lighting", "Receive lighting.", Required = false, Type = "boolean" )]
	public static object CreateBeamEffect( JsonElement args )
	{
		var name = ToolHandlerBase.GetString( args, "name", "Beam" );
		var posStr = ToolHandlerBase.GetString( args, "position" );

		var go = SceneHelpers.CreateInScene( name );
		if ( !string.IsNullOrEmpty( posStr ) ) go.WorldPosition = ParseVec3( posStr, Vector3.Zero );

		var comp = go.Components.Create<LineRenderer>();
		ApplyLineRendererProps( comp, args );

		return ToolHandlerBase.JsonResult( new
		{
			created = true,
			gameObject = go.Name,
			gameObjectGuid = go.Id.ToString(),
			componentGuid = comp.Id.ToString()
		} );
	}

	// ──────────────────────────────────────────────
	//  configure_beam_effect
	// ──────────────────────────────────────────────

	[Tool( "configure_beam_effect", "Modify an existing LineRenderer component.", RequiresMainThread = true )]
	[Param( "name", "Name of GameObject with LineRenderer.", Required = false )]
	[Param( "guid", "GUID of GameObject with LineRenderer.", Required = false )]
	[Param( "additive", "Use additive blending.", Required = false, Type = "boolean" )]
	[Param( "opaque", "Render as opaque.", Required = false, Type = "boolean" )]
	[Param( "wireframe", "Render as wireframe.", Required = false, Type = "boolean" )]
	[Param( "cast_shadows", "Cast shadows.", Required = false, Type = "boolean" )]
	[Param( "lighting", "Receive lighting.", Required = false, Type = "boolean" )]
	public static object ConfigureBeamEffect( JsonElement args )
	{
		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );

		var comp = go.Components.Get<LineRenderer>();
		if ( comp == null )
			return ToolHandlerBase.ErrorResult( $"'{go.Name}' has no LineRenderer component." );

		ApplyLineRendererProps( comp, args );

		return ToolHandlerBase.JsonResult( new
		{
			configured = true,
			gameObject = go.Name,
			additive = comp.Additive,
			opaque = comp.Opaque,
			wireframe = comp.Wireframe
		} );
	}

	// ──────────────────────────────────────────────
	//  create_verlet_rope
	// ──────────────────────────────────────────────

	[Tool( "create_verlet_rope", "Create a GameObject with a VerletRope component (with companion LineRenderer).", RequiresMainThread = true )]
	[Param( "name", "Name for the new GameObject. Default: 'VerletRope'.", Required = false )]
	[Param( "position", "World position 'x,y,z'. Default: '0,0,0'.", Required = false )]
	[Param( "attachment_name", "Name of GameObject the rope's end attaches to.", Required = false )]
	[Param( "attachment_guid", "GUID of GameObject the rope's end attaches to.", Required = false )]
	[Param( "segment_count", "Number of segments. Default: 16.", Required = false, Type = "integer" )]
	[Param( "slack", "Additional slack length.", Required = false, Type = "number" )]
	[Param( "radius", "Collision radius.", Required = false, Type = "number" )]
	[Param( "stiffness", "Stiffness factor.", Required = false, Type = "number" )]
	[Param( "damping_factor", "Damping factor.", Required = false, Type = "number" )]
	public static object CreateVerletRope( JsonElement args )
	{
		var name = ToolHandlerBase.GetString( args, "name", "VerletRope" );
		var posStr = ToolHandlerBase.GetString( args, "position" );

		var go = SceneHelpers.CreateInScene( name );
		if ( !string.IsNullOrEmpty( posStr ) ) go.WorldPosition = ParseVec3( posStr, Vector3.Zero );

		var line = go.Components.Create<LineRenderer>();
		var rope = go.Components.Create<VerletRope>();
		rope.LinkedRenderer = line;

		var attachName = ToolHandlerBase.GetString( args, "attachment_name" );
		var attachGuid = ToolHandlerBase.GetString( args, "attachment_guid" );
		if ( !string.IsNullOrEmpty( attachName ) || !string.IsNullOrEmpty( attachGuid ) )
		{
			var scene = SceneHelpers.ResolveActiveScene();
			var target = scene != null ? SceneHelpers.FindByGuidOrName( scene, attachGuid, attachName ) : null;
			if ( target == null )
				return ToolHandlerBase.ErrorResult( $"Attachment GameObject not found (name='{attachName}', guid='{attachGuid}')." );
			rope.Attachment = target;
		}

		if ( args.TryGetProperty( "segment_count", out _ ) )
			rope.SegmentCount = ToolHandlerBase.GetInt( args, "segment_count", rope.SegmentCount );
		if ( args.TryGetProperty( "slack", out _ ) )
			rope.Slack = ToolHandlerBase.GetFloat( args, "slack", rope.Slack );
		if ( args.TryGetProperty( "radius", out _ ) )
			rope.Radius = ToolHandlerBase.GetFloat( args, "radius", rope.Radius );
		if ( args.TryGetProperty( "stiffness", out _ ) )
			rope.Stiffness = ToolHandlerBase.GetFloat( args, "stiffness", rope.Stiffness );
		if ( args.TryGetProperty( "damping_factor", out _ ) )
			rope.DampingFactor = ToolHandlerBase.GetFloat( args, "damping_factor", rope.DampingFactor );

		return ToolHandlerBase.JsonResult( new
		{
			created = true,
			gameObject = go.Name,
			gameObjectGuid = go.Id.ToString(),
			ropeGuid = rope.Id.ToString(),
			lineRendererGuid = line.Id.ToString(),
			attachment = rope.Attachment?.Name,
			segmentCount = rope.SegmentCount
		} );
	}

	// ──────────────────────────────────────────────
	//  create_render_entity
	// ──────────────────────────────────────────────

	[Tool( "create_render_entity", "Create a GameObject with a ModelRenderer component (optionally assigned a model).", RequiresMainThread = true )]
	[Param( "name", "Name for the new GameObject. Default: 'RenderEntity'.", Required = false )]
	[Param( "position", "World position 'x,y,z'. Default: '0,0,0'.", Required = false )]
	[Param( "rotation", "Rotation 'pitch,yaw,roll'.", Required = false )]
	[Param( "scale", "Local scale 'x,y,z'.", Required = false )]
	[Param( "model", "Path to model asset (e.g. 'models/dev/box.vmdl').", Required = false )]
	[Param( "tint", "Tint color 'r,g,b' or 'r,g,b,a' (0-1).", Required = false )]
	[Param( "render_type", "Shadow render type: 'on', 'off', 'shadowsonly'.", Required = false )]
	public static object CreateRenderEntity( JsonElement args )
	{
		var name = ToolHandlerBase.GetString( args, "name", "RenderEntity" );
		var posStr = ToolHandlerBase.GetString( args, "position" );
		var rotStr = ToolHandlerBase.GetString( args, "rotation" );
		var scaleStr = ToolHandlerBase.GetString( args, "scale" );

		var go = SceneHelpers.CreateInScene( name );
		if ( !string.IsNullOrEmpty( posStr ) ) go.WorldPosition = ParseVec3( posStr, Vector3.Zero );
		if ( !string.IsNullOrEmpty( rotStr ) )
		{
			var ang = ParseVec3( rotStr, Vector3.Zero );
			go.WorldRotation = new Angles( ang.x, ang.y, ang.z ).ToRotation();
		}
		if ( !string.IsNullOrEmpty( scaleStr ) )
			go.LocalScale = ParseVec3( scaleStr, Vector3.One );

		var comp = go.Components.Create<ModelRenderer>();
		ApplyRendererProps( comp, args );

		return ToolHandlerBase.JsonResult( new
		{
			created = true,
			gameObject = go.Name,
			gameObjectGuid = go.Id.ToString(),
			componentGuid = comp.Id.ToString(),
			model = comp.Model?.ResourcePath,
			renderType = comp.RenderType.ToString()
		} );
	}

	// ──────────────────────────────────────────────
	//  configure_render_entity
	// ──────────────────────────────────────────────

	[Tool( "configure_render_entity", "Modify an existing ModelRenderer component.", RequiresMainThread = true )]
	[Param( "name", "Name of GameObject with ModelRenderer.", Required = false )]
	[Param( "guid", "GUID of GameObject with ModelRenderer.", Required = false )]
	[Param( "model", "Path to model asset.", Required = false )]
	[Param( "tint", "Tint color 'r,g,b' or 'r,g,b,a'.", Required = false )]
	[Param( "render_type", "Shadow render type: 'on', 'off', 'shadowsonly'.", Required = false )]
	public static object ConfigureRenderEntity( JsonElement args )
	{
		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );

		var comp = go.Components.Get<ModelRenderer>();
		if ( comp == null )
			return ToolHandlerBase.ErrorResult( $"'{go.Name}' has no ModelRenderer component." );

		ApplyRendererProps( comp, args );

		return ToolHandlerBase.JsonResult( new
		{
			configured = true,
			gameObject = go.Name,
			model = comp.Model?.ResourcePath,
			renderType = comp.RenderType.ToString()
		} );
	}

	// ──────────────────────────────────────────────
	//  Helpers
	// ──────────────────────────────────────────────

	private static void ApplyParticleProps( ParticleEffect comp, JsonElement args )
	{
		if ( args.TryGetProperty( "max_particles", out _ ) )
			comp.MaxParticles = ToolHandlerBase.GetInt( args, "max_particles", comp.MaxParticles );

		var tintStr = ToolHandlerBase.GetString( args, "tint" );
		if ( !string.IsNullOrEmpty( tintStr ) )
			comp.Tint = ParseColor( tintStr, comp.Tint );

		var forceStr = ToolHandlerBase.GetString( args, "force_direction" );
		if ( !string.IsNullOrEmpty( forceStr ) )
		{
			comp.Force = true;
			comp.ForceDirection = ParseVec3( forceStr, comp.ForceDirection );
		}

		var moveStr = ToolHandlerBase.GetString( args, "constant_movement" );
		if ( !string.IsNullOrEmpty( moveStr ) )
			comp.ConstantMovement = ParseVec3( moveStr, Vector3.Zero );

		if ( args.TryGetProperty( "apply_color", out _ ) )
			comp.ApplyColor = ToolHandlerBase.GetBool( args, "apply_color", comp.ApplyColor );
		if ( args.TryGetProperty( "apply_rotation", out _ ) )
			comp.ApplyRotation = ToolHandlerBase.GetBool( args, "apply_rotation", comp.ApplyRotation );
		if ( args.TryGetProperty( "collision", out _ ) )
			comp.Collision = ToolHandlerBase.GetBool( args, "collision", comp.Collision );
		if ( args.TryGetProperty( "collision_radius", out _ ) )
			comp.CollisionRadius = ToolHandlerBase.GetFloat( args, "collision_radius", comp.CollisionRadius );
	}

	private static void ApplyLineRendererProps( LineRenderer comp, JsonElement args )
	{
		if ( args.TryGetProperty( "additive", out _ ) )
			comp.Additive = ToolHandlerBase.GetBool( args, "additive", comp.Additive );
		if ( args.TryGetProperty( "opaque", out _ ) )
			comp.Opaque = ToolHandlerBase.GetBool( args, "opaque", comp.Opaque );
		if ( args.TryGetProperty( "wireframe", out _ ) )
			comp.Wireframe = ToolHandlerBase.GetBool( args, "wireframe", comp.Wireframe );
		if ( args.TryGetProperty( "cast_shadows", out _ ) )
			comp.CastShadows = ToolHandlerBase.GetBool( args, "cast_shadows", comp.CastShadows );
		if ( args.TryGetProperty( "lighting", out _ ) )
			comp.Lighting = ToolHandlerBase.GetBool( args, "lighting", comp.Lighting );
	}

	private static void ApplyRendererProps( ModelRenderer comp, JsonElement args )
	{
		var modelPath = ToolHandlerBase.GetString( args, "model" );
		if ( !string.IsNullOrEmpty( modelPath ) )
		{
			var model = Model.Load( modelPath );
			if ( model != null ) comp.Model = model;
		}

		var tintStr = ToolHandlerBase.GetString( args, "tint" );
		if ( !string.IsNullOrEmpty( tintStr ) )
			comp.Tint = ParseColor( tintStr, comp.Tint );

		var renderType = ToolHandlerBase.GetString( args, "render_type" );
		if ( !string.IsNullOrEmpty( renderType ) )
		{
			comp.RenderType = renderType.ToLowerInvariant() switch
			{
				"on" => ModelRenderer.ShadowRenderType.On,
				"off" => ModelRenderer.ShadowRenderType.Off,
				"shadowsonly" => ModelRenderer.ShadowRenderType.ShadowsOnly,
				_ => comp.RenderType
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
