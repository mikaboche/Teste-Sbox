using System;
using System.Text.Json;
using Sandbox;

namespace SboxPro;

public static class LightingTools
{
	// ──────────────────────────────────────────────
	//  create_light
	// ──────────────────────────────────────────────

	[Tool( "create_light", "Create a GameObject with a Light component. Type: 'point', 'spot', or 'directional'.", RequiresMainThread = true )]
	[Param( "name", "Name for the new GameObject. Default: derived from type.", Required = false )]
	[Param( "type", "Light type: 'point', 'spot', or 'directional'. Default: 'point'.", Required = false, Enum = "point,spot,directional", Default = "point" )]
	[Param( "position", "World position 'x,y,z'. Default: '0,0,0'.", Required = false )]
	[Param( "rotation", "Rotation as 'pitch,yaw,roll' degrees. Default: '0,0,0'.", Required = false )]
	[Param( "color", "Light color 'r,g,b' (0-1) or 'r,g,b,a'.", Required = false )]
	[Param( "shadows", "Cast shadows. Default: true.", Required = false, Type = "boolean", Default = "true" )]
	[Param( "radius", "Radius (for point/spot lights). Default: 200.", Required = false, Type = "number" )]
	[Param( "cone_outer", "Outer cone angle in degrees (spot light).", Required = false, Type = "number" )]
	[Param( "cone_inner", "Inner cone angle in degrees (spot light).", Required = false, Type = "number" )]
	public static object CreateLight( JsonElement args )
	{
		var lightType = ToolHandlerBase.GetString( args, "type", "point" )?.ToLowerInvariant();
		var name = ToolHandlerBase.GetString( args, "name" );
		var posStr = ToolHandlerBase.GetString( args, "position" );
		var rotStr = ToolHandlerBase.GetString( args, "rotation" );
		var colorStr = ToolHandlerBase.GetString( args, "color" );
		var shadows = ToolHandlerBase.GetBool( args, "shadows", true );
		var radius = ToolHandlerBase.GetFloat( args, "radius", 200f );
		var coneOuter = ToolHandlerBase.GetFloat( args, "cone_outer", 45f );
		var coneInner = ToolHandlerBase.GetFloat( args, "cone_inner", 30f );

		if ( string.IsNullOrEmpty( name ) )
			name = lightType switch { "spot" => "SpotLight", "directional" => "DirectionalLight", _ => "PointLight" };

		var go = SceneHelpers.CreateInScene( name );
		if ( !string.IsNullOrEmpty( posStr ) ) go.WorldPosition = ParseVec3( posStr, Vector3.Zero );
		if ( !string.IsNullOrEmpty( rotStr ) )
		{
			var ang = ParseVec3( rotStr, Vector3.Zero );
			go.WorldRotation = new Angles( ang.x, ang.y, ang.z ).ToRotation();
		}

		Light light;
		switch ( lightType )
		{
			case "spot":
				var spot = go.Components.Create<SpotLight>();
				spot.Radius = radius;
				spot.ConeOuter = coneOuter;
				spot.ConeInner = coneInner;
				light = spot;
				break;
			case "directional":
				light = go.Components.Create<DirectionalLight>();
				break;
			case "point":
			default:
				var point = go.Components.Create<PointLight>();
				point.Radius = radius;
				light = point;
				break;
		}

		if ( !string.IsNullOrEmpty( colorStr ) )
			light.LightColor = ParseColor( colorStr, Color.White );

		light.Shadows = shadows;

		return ToolHandlerBase.JsonResult( new
		{
			created = true,
			gameObject = go.Name,
			gameObjectGuid = go.Id.ToString(),
			lightType = light.GetType().Name,
			componentGuid = light.Id.ToString(),
			color = $"{light.LightColor.r},{light.LightColor.g},{light.LightColor.b},{light.LightColor.a}",
			shadows = light.Shadows
		} );
	}

	// ──────────────────────────────────────────────
	//  configure_light
	// ──────────────────────────────────────────────

	[Tool( "configure_light", "Modify properties of an existing Light component.", RequiresMainThread = true )]
	[Param( "name", "Name of the GameObject containing the light.", Required = false )]
	[Param( "guid", "GUID of the GameObject.", Required = false )]
	[Param( "color", "Light color 'r,g,b' or 'r,g,b,a'.", Required = false )]
	[Param( "shadows", "Cast shadows.", Required = false, Type = "boolean" )]
	[Param( "shadow_bias", "Shadow bias (typical 0.0001-0.001).", Required = false, Type = "number" )]
	[Param( "shadow_hardness", "Shadow hardness.", Required = false, Type = "number" )]
	[Param( "fog_strength", "Fog strength.", Required = false, Type = "number" )]
	[Param( "radius", "Radius (for PointLight/SpotLight).", Required = false, Type = "number" )]
	public static object ConfigureLight( JsonElement args )
	{
		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );

		var light = go.Components.Get<Light>();
		if ( light == null )
			return ToolHandlerBase.ErrorResult( $"No Light component on '{go.Name}'." );

		if ( args.TryGetProperty( "color", out _ ) )
		{
			var colorStr = ToolHandlerBase.GetString( args, "color" );
			if ( !string.IsNullOrEmpty( colorStr ) ) light.LightColor = ParseColor( colorStr, light.LightColor );
		}

		if ( args.TryGetProperty( "shadows", out _ ) ) light.Shadows = ToolHandlerBase.GetBool( args, "shadows", light.Shadows );
		if ( args.TryGetProperty( "shadow_bias", out _ ) ) light.ShadowBias = ToolHandlerBase.GetFloat( args, "shadow_bias", light.ShadowBias );
		if ( args.TryGetProperty( "shadow_hardness", out _ ) ) light.ShadowHardness = ToolHandlerBase.GetFloat( args, "shadow_hardness", light.ShadowHardness );
		if ( args.TryGetProperty( "fog_strength", out _ ) ) light.FogStrength = ToolHandlerBase.GetFloat( args, "fog_strength", light.FogStrength );

		if ( args.TryGetProperty( "radius", out _ ) )
		{
			var newRadius = ToolHandlerBase.GetFloat( args, "radius", 0f );
			if ( light is PointLight pl ) pl.Radius = newRadius;
			else if ( light is SpotLight sl ) sl.Radius = newRadius;
		}

		return ToolHandlerBase.JsonResult( new
		{
			configured = true,
			gameObject = go.Name,
			lightType = light.GetType().Name,
			color = $"{light.LightColor.r},{light.LightColor.g},{light.LightColor.b},{light.LightColor.a}",
			shadows = light.Shadows
		} );
	}

	// ──────────────────────────────────────────────
	//  create_sky_box
	// ──────────────────────────────────────────────

	[Tool( "create_sky_box", "Create a GameObject with a SkyBox2D component for sky rendering.", RequiresMainThread = true )]
	[Param( "name", "Name for the new GameObject. Default: 'Sky Box'.", Required = false )]
	[Param( "sky_material", "Path to sky material (.vmat). Default: 'materials/skybox/skybox_day_01.vmat'.", Required = false )]
	[Param( "tint", "Tint color 'r,g,b' or 'r,g,b,a'. Default: white.", Required = false )]
	[Param( "indirect_lighting", "Enable sky indirect lighting. Default: true.", Required = false, Type = "boolean", Default = "true" )]
	public static object CreateSkyBox( JsonElement args )
	{
		var name = ToolHandlerBase.GetString( args, "name", "Sky Box" );
		var matPath = ToolHandlerBase.GetString( args, "sky_material", "materials/skybox/skybox_day_01.vmat" );
		var tintStr = ToolHandlerBase.GetString( args, "tint" );
		var indirect = ToolHandlerBase.GetBool( args, "indirect_lighting", true );

		var go = SceneHelpers.CreateInScene( name );
		var sky = go.Components.Create<SkyBox2D>();

		if ( !string.IsNullOrEmpty( matPath ) ) sky.SkyMaterial = Material.Load( matPath );
		if ( !string.IsNullOrEmpty( tintStr ) ) sky.Tint = ParseColor( tintStr, Color.White );
		sky.SkyIndirectLighting = indirect;

		return ToolHandlerBase.JsonResult( new
		{
			created = true,
			gameObject = go.Name,
			gameObjectGuid = go.Id.ToString(),
			componentGuid = sky.Id.ToString(),
			skyMaterial = matPath
		} );
	}

	// ──────────────────────────────────────────────
	//  set_sky_box
	// ──────────────────────────────────────────────

	[Tool( "set_sky_box", "Modify an existing SkyBox2D component.", RequiresMainThread = true )]
	[Param( "name", "Name of the GameObject.", Required = false )]
	[Param( "guid", "GUID of the GameObject.", Required = false )]
	[Param( "sky_material", "Path to sky material (.vmat).", Required = false )]
	[Param( "tint", "Tint color 'r,g,b' or 'r,g,b,a'.", Required = false )]
	[Param( "indirect_lighting", "Enable sky indirect lighting.", Required = false, Type = "boolean" )]
	public static object SetSkyBox( JsonElement args )
	{
		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );

		var sky = go.Components.Get<SkyBox2D>();
		if ( sky == null )
			return ToolHandlerBase.ErrorResult( $"No SkyBox2D on '{go.Name}'." );

		if ( args.TryGetProperty( "sky_material", out _ ) )
		{
			var matPath = ToolHandlerBase.GetString( args, "sky_material" );
			if ( !string.IsNullOrEmpty( matPath ) ) sky.SkyMaterial = Material.Load( matPath );
		}

		if ( args.TryGetProperty( "tint", out _ ) )
		{
			var tintStr = ToolHandlerBase.GetString( args, "tint" );
			if ( !string.IsNullOrEmpty( tintStr ) ) sky.Tint = ParseColor( tintStr, sky.Tint );
		}

		if ( args.TryGetProperty( "indirect_lighting", out _ ) )
			sky.SkyIndirectLighting = ToolHandlerBase.GetBool( args, "indirect_lighting", sky.SkyIndirectLighting );

		return ToolHandlerBase.JsonResult( new
		{
			configured = true,
			gameObject = go.Name,
			tint = $"{sky.Tint.r},{sky.Tint.g},{sky.Tint.b},{sky.Tint.a}",
			indirectLighting = sky.SkyIndirectLighting
		} );
	}

	// ──────────────────────────────────────────────
	//  create_ambient_light
	// ──────────────────────────────────────────────

	[Tool( "create_ambient_light", "Create a GameObject with an AmbientLight component (global ambient illumination).", RequiresMainThread = true )]
	[Param( "name", "Name for the new GameObject. Default: 'Ambient Light'.", Required = false )]
	[Param( "color", "Ambient color 'r,g,b' or 'r,g,b,a'. Default: dim grey.", Required = false )]
	public static object CreateAmbientLight( JsonElement args )
	{
		var name = ToolHandlerBase.GetString( args, "name", "Ambient Light" );
		var colorStr = ToolHandlerBase.GetString( args, "color" );

		var go = SceneHelpers.CreateInScene( name );
		var amb = go.Components.Create<AmbientLight>();

		if ( !string.IsNullOrEmpty( colorStr ) )
			amb.Color = ParseColor( colorStr, new Color( 0.25f, 0.32f, 0.35f, 1f ) );

		return ToolHandlerBase.JsonResult( new
		{
			created = true,
			gameObject = go.Name,
			gameObjectGuid = go.Id.ToString(),
			componentGuid = amb.Id.ToString(),
			color = $"{amb.Color.r},{amb.Color.g},{amb.Color.b},{amb.Color.a}"
		} );
	}

	// ──────────────────────────────────────────────
	//  create_indirect_light_volume
	// ──────────────────────────────────────────────

	[Tool( "create_indirect_light_volume", "Create an IndirectLightVolume — DDGI probe grid for dynamic indirect lighting.", RequiresMainThread = true )]
	[Param( "name", "Name for the new GameObject. Default: 'Indirect Light Volume'.", Required = false )]
	[Param( "position", "World position 'x,y,z'.", Required = false )]
	public static object CreateIndirectLightVolume( JsonElement args )
	{
		var name = ToolHandlerBase.GetString( args, "name", "Indirect Light Volume" );
		var posStr = ToolHandlerBase.GetString( args, "position" );

		var go = SceneHelpers.CreateInScene( name );
		if ( !string.IsNullOrEmpty( posStr ) ) go.WorldPosition = ParseVec3( posStr, Vector3.Zero );

		var ilv = go.Components.Create<IndirectLightVolume>();

		return ToolHandlerBase.JsonResult( new
		{
			created = true,
			gameObject = go.Name,
			gameObjectGuid = go.Id.ToString(),
			componentGuid = ilv.Id.ToString(),
			note = "Use BakeProbes() to generate lighting data after scene setup is complete."
		} );
	}

	// ──────────────────────────────────────────────
	//  create_environment_light
	// ──────────────────────────────────────────────

	[Tool( "create_environment_light", "Combo helper: create Sun (DirectionalLight) + AmbientLight + SkyBox2D in one call.", RequiresMainThread = true )]
	[Param( "sun_direction", "Sun rotation as 'pitch,yaw,roll' degrees. Default: '-60,30,0'.", Required = false )]
	[Param( "sun_color", "Sun light color. Default: warm white.", Required = false )]
	[Param( "ambient_color", "Ambient color. Default: dim sky-tinted.", Required = false )]
	[Param( "sky_material", "Sky material path. Default: 'materials/skybox/skybox_day_01.vmat'.", Required = false )]
	public static object CreateEnvironmentLight( JsonElement args )
	{
		var sunDirStr = ToolHandlerBase.GetString( args, "sun_direction", "-60,30,0" );
		var sunColorStr = ToolHandlerBase.GetString( args, "sun_color" );
		var ambColorStr = ToolHandlerBase.GetString( args, "ambient_color" );
		var skyMatPath = ToolHandlerBase.GetString( args, "sky_material", "materials/skybox/skybox_day_01.vmat" );

		// Sun
		var sunGo = SceneHelpers.CreateInScene( "Sun" );
		var sunAng = ParseVec3( sunDirStr, new Vector3( -60, 30, 0 ) );
		sunGo.WorldRotation = new Angles( sunAng.x, sunAng.y, sunAng.z ).ToRotation();
		var sun = sunGo.Components.Create<DirectionalLight>();
		if ( !string.IsNullOrEmpty( sunColorStr ) ) sun.LightColor = ParseColor( sunColorStr, sun.LightColor );

		// Ambient
		var ambGo = SceneHelpers.CreateInScene( "Ambient Light" );
		var amb = ambGo.Components.Create<AmbientLight>();
		if ( !string.IsNullOrEmpty( ambColorStr ) ) amb.Color = ParseColor( ambColorStr, amb.Color );

		// Sky
		var skyGo = SceneHelpers.CreateInScene( "Sky Box" );
		var sky = skyGo.Components.Create<SkyBox2D>();
		if ( !string.IsNullOrEmpty( skyMatPath ) ) sky.SkyMaterial = Material.Load( skyMatPath );

		return ToolHandlerBase.JsonResult( new
		{
			created = true,
			sun = new { name = sunGo.Name, guid = sunGo.Id.ToString() },
			ambient = new { name = ambGo.Name, guid = ambGo.Id.ToString() },
			sky = new { name = skyGo.Name, guid = skyGo.Id.ToString(), material = skyMatPath }
		} );
	}

	// ──────────────────────────────────────────────
	//  configure_post_processing
	// ──────────────────────────────────────────────

	[Tool( "configure_post_processing", "Configure post-processing: add Bloom/Tonemapping/Sharpen to a GameObject (typically the Camera).", RequiresMainThread = true )]
	[Param( "name", "Name of the target GameObject (typically Camera).", Required = false )]
	[Param( "guid", "GUID of the target GameObject.", Required = false )]
	[Param( "bloom", "Add Bloom effect. Default: false.", Required = false, Type = "boolean" )]
	[Param( "bloom_strength", "Bloom strength.", Required = false, Type = "number" )]
	[Param( "bloom_threshold", "Bloom threshold.", Required = false, Type = "number" )]
	[Param( "tonemapping", "Add Tonemapping. Default: false.", Required = false, Type = "boolean" )]
	[Param( "sharpen", "Add Sharpen effect. Default: false.", Required = false, Type = "boolean" )]
	[Param( "sharpen_scale", "Sharpen scale.", Required = false, Type = "number" )]
	public static object ConfigurePostProcessing( JsonElement args )
	{
		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );

		var addedComponents = new System.Collections.Generic.List<string>();

		if ( ToolHandlerBase.GetBool( args, "bloom", false ) )
		{
			var bloom = go.Components.Get<Bloom>() ?? go.Components.Create<Bloom>();
			if ( args.TryGetProperty( "bloom_strength", out _ ) ) bloom.Strength = ToolHandlerBase.GetFloat( args, "bloom_strength", bloom.Strength );
			if ( args.TryGetProperty( "bloom_threshold", out _ ) ) bloom.Threshold = ToolHandlerBase.GetFloat( args, "bloom_threshold", bloom.Threshold );
			addedComponents.Add( "Bloom" );
		}

		if ( ToolHandlerBase.GetBool( args, "tonemapping", false ) )
		{
			go.Components.Get<Tonemapping>();
			if ( go.Components.Get<Tonemapping>() == null )
			{
				go.Components.Create<Tonemapping>();
				addedComponents.Add( "Tonemapping" );
			}
		}

		if ( ToolHandlerBase.GetBool( args, "sharpen", false ) )
		{
			var sharpen = go.Components.Get<Sharpen>() ?? go.Components.Create<Sharpen>();
			if ( args.TryGetProperty( "sharpen_scale", out _ ) ) sharpen.Scale = ToolHandlerBase.GetFloat( args, "sharpen_scale", sharpen.Scale );
			addedComponents.Add( "Sharpen" );
		}

		return ToolHandlerBase.JsonResult( new
		{
			configured = true,
			gameObject = go.Name,
			added = addedComponents.ToArray()
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
