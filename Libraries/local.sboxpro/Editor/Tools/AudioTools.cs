using System;
using System.Text.Json;
using Sandbox;

namespace SboxPro;

public static class AudioTools
{
	// ──────────────────────────────────────────────
	//  create_sound_point
	// ──────────────────────────────────────────────

	[Tool( "create_sound_point", "Create a GameObject with a SoundPointComponent — plays a sound at a point.", RequiresMainThread = true )]
	[Param( "name", "Name for the new GameObject. Default: 'SoundPoint'.", Required = false )]
	[Param( "position", "World position 'x,y,z'. Default: '0,0,0'.", Required = false )]
	[Param( "sound_event", "Path to a .sound file (SoundEvent resource).", Required = false )]
	[Param( "play_on_start", "Auto-play when scene starts. Default: false.", Required = false, Type = "boolean", Default = "false" )]
	[Param( "volume", "Playback volume multiplier. Default: 1.", Required = false, Type = "number" )]
	[Param( "pitch", "Playback pitch multiplier. Default: 1.", Required = false, Type = "number" )]
	[Param( "force_2d", "Force 2D playback (no 3D positioning). Default: false.", Required = false, Type = "boolean" )]
	[Param( "repeat", "Repeat the sound. Default: false.", Required = false, Type = "boolean" )]
	[Param( "distance_attenuation", "Fade out over distance. Default: true.", Required = false, Type = "boolean" )]
	[Param( "distance", "Maximum audible distance.", Required = false, Type = "number" )]
	public static object CreateSoundPoint( JsonElement args )
	{
		var name = ToolHandlerBase.GetString( args, "name", "SoundPoint" );
		var posStr = ToolHandlerBase.GetString( args, "position" );

		var go = SceneHelpers.CreateInScene( name );
		if ( !string.IsNullOrEmpty( posStr ) ) go.WorldPosition = ParseVec3( posStr, Vector3.Zero );

		var comp = go.Components.Create<SoundPointComponent>();
		ApplyBaseSoundProps( comp, args );

		return ToolHandlerBase.JsonResult( new
		{
			created = true,
			gameObject = go.Name,
			gameObjectGuid = go.Id.ToString(),
			componentGuid = comp.Id.ToString(),
			soundEvent = comp.SoundEvent?.ResourcePath,
			playOnStart = comp.PlayOnStart
		} );
	}

	// ──────────────────────────────────────────────
	//  create_sound_box
	// ──────────────────────────────────────────────

	[Tool( "create_sound_box", "Create a GameObject with a SoundBoxComponent — plays sound within a box volume.", RequiresMainThread = true )]
	[Param( "name", "Name for the new GameObject. Default: 'SoundBox'.", Required = false )]
	[Param( "position", "World position 'x,y,z'. Default: '0,0,0'.", Required = false )]
	[Param( "scale", "Box scale 'x,y,z'. Default: '64,64,64'.", Required = false )]
	[Param( "sound_event", "Path to a .sound file (SoundEvent resource).", Required = false )]
	[Param( "play_on_start", "Auto-play when scene starts. Default: false.", Required = false, Type = "boolean" )]
	[Param( "volume", "Playback volume multiplier.", Required = false, Type = "number" )]
	[Param( "pitch", "Playback pitch multiplier.", Required = false, Type = "number" )]
	[Param( "repeat", "Repeat the sound. Default: false.", Required = false, Type = "boolean" )]
	public static object CreateSoundBox( JsonElement args )
	{
		var name = ToolHandlerBase.GetString( args, "name", "SoundBox" );
		var posStr = ToolHandlerBase.GetString( args, "position" );
		var scaleStr = ToolHandlerBase.GetString( args, "scale" );

		var go = SceneHelpers.CreateInScene( name );
		if ( !string.IsNullOrEmpty( posStr ) ) go.WorldPosition = ParseVec3( posStr, Vector3.Zero );

		var comp = go.Components.Create<SoundBoxComponent>();
		ApplyBaseSoundProps( comp, args );
		if ( !string.IsNullOrEmpty( scaleStr ) ) comp.Scale = ParseVec3( scaleStr, new Vector3( 64, 64, 64 ) );

		return ToolHandlerBase.JsonResult( new
		{
			created = true,
			gameObject = go.Name,
			gameObjectGuid = go.Id.ToString(),
			componentGuid = comp.Id.ToString(),
			scale = $"{comp.Scale.x},{comp.Scale.y},{comp.Scale.z}",
			soundEvent = comp.SoundEvent?.ResourcePath
		} );
	}

	// ──────────────────────────────────────────────
	//  create_audio_listener
	// ──────────────────────────────────────────────

	[Tool( "create_audio_listener", "Create a GameObject with an AudioListener component (overrides camera-based listening).", RequiresMainThread = true )]
	[Param( "name", "Name for the new GameObject. Default: 'AudioListener'.", Required = false )]
	[Param( "position", "World position 'x,y,z'. Default: '0,0,0'.", Required = false )]
	[Param( "use_camera_direction", "Use camera rotation for listener orientation. Default: false.", Required = false, Type = "boolean" )]
	public static object CreateAudioListener( JsonElement args )
	{
		var name = ToolHandlerBase.GetString( args, "name", "AudioListener" );
		var posStr = ToolHandlerBase.GetString( args, "position" );

		var go = SceneHelpers.CreateInScene( name );
		if ( !string.IsNullOrEmpty( posStr ) ) go.WorldPosition = ParseVec3( posStr, Vector3.Zero );

		var comp = go.Components.Create<AudioListener>();
		if ( args.TryGetProperty( "use_camera_direction", out _ ) )
			comp.UseCameraDirection = ToolHandlerBase.GetBool( args, "use_camera_direction", comp.UseCameraDirection );

		return ToolHandlerBase.JsonResult( new
		{
			created = true,
			gameObject = go.Name,
			gameObjectGuid = go.Id.ToString(),
			useCameraDirection = comp.UseCameraDirection
		} );
	}

	// ──────────────────────────────────────────────
	//  create_dsp_volume
	// ──────────────────────────────────────────────

	[Tool( "create_dsp_volume", "Create a GameObject with a DspVolume — applies a DSP preset to audio inside the volume.", RequiresMainThread = true )]
	[Param( "name", "Name for the new GameObject. Default: 'DspVolume'.", Required = false )]
	[Param( "position", "World position 'x,y,z'. Default: '0,0,0'.", Required = false )]
	[Param( "priority", "Volume priority (higher wins overlap). Default: 0.", Required = false, Type = "integer" )]
	public static object CreateDspVolume( JsonElement args )
	{
		var name = ToolHandlerBase.GetString( args, "name", "DspVolume" );
		var posStr = ToolHandlerBase.GetString( args, "position" );

		var go = SceneHelpers.CreateInScene( name );
		if ( !string.IsNullOrEmpty( posStr ) ) go.WorldPosition = ParseVec3( posStr, Vector3.Zero );

		var comp = go.Components.Create<DspVolume>();
		if ( args.TryGetProperty( "priority", out _ ) )
			comp.Priority = ToolHandlerBase.GetInt( args, "priority", comp.Priority );

		return ToolHandlerBase.JsonResult( new
		{
			created = true,
			gameObject = go.Name,
			gameObjectGuid = go.Id.ToString(),
			componentGuid = comp.Id.ToString(),
			priority = comp.Priority,
			note = "Set the Dsp preset via set_property/set_resource_property if needed."
		} );
	}

	// ──────────────────────────────────────────────
	//  create_soundscape_trigger
	// ──────────────────────────────────────────────

	[Tool( "create_soundscape_trigger", "Create a GameObject with a SoundscapeTrigger component for environmental ambience.", RequiresMainThread = true )]
	[Param( "name", "Name for the new GameObject. Default: 'SoundscapeTrigger'.", Required = false )]
	[Param( "position", "World position 'x,y,z'. Default: '0,0,0'.", Required = false )]
	[Param( "soundscape", "Path to a .sndscape file (Soundscape resource).", Required = false )]
	[Param( "trigger_type", "Trigger shape: 'global', 'sphere', 'box'.", Required = false )]
	[Param( "radius", "Sphere radius (when trigger_type='sphere').", Required = false, Type = "number" )]
	[Param( "box_size", "Box size 'x,y,z' (when trigger_type='box').", Required = false )]
	[Param( "volume", "Playback volume multiplier.", Required = false, Type = "number" )]
	[Param( "stay_active_on_exit", "Keep playing after exit until another takes over.", Required = false, Type = "boolean" )]
	public static object CreateSoundscapeTrigger( JsonElement args )
	{
		var name = ToolHandlerBase.GetString( args, "name", "SoundscapeTrigger" );
		var posStr = ToolHandlerBase.GetString( args, "position" );

		var go = SceneHelpers.CreateInScene( name );
		if ( !string.IsNullOrEmpty( posStr ) ) go.WorldPosition = ParseVec3( posStr, Vector3.Zero );

		var comp = go.Components.Create<SoundscapeTrigger>();

		var soundscapePath = ToolHandlerBase.GetString( args, "soundscape" );
		if ( !string.IsNullOrEmpty( soundscapePath ) )
		{
			var ss = ResourceLibrary.Get<Soundscape>( soundscapePath );
			if ( ss == null ) return ToolHandlerBase.ErrorResult( $"Soundscape not found: {soundscapePath}" );
			comp.Soundscape = ss;
		}

		var ttype = ToolHandlerBase.GetString( args, "trigger_type" )?.ToLowerInvariant();
		if ( !string.IsNullOrEmpty( ttype ) && Enum.TryParse<SoundscapeTrigger.TriggerType>( ttype, true, out var tt ) )
			comp.Type = tt;

		if ( args.TryGetProperty( "radius", out _ ) )
			comp.Radius = ToolHandlerBase.GetFloat( args, "radius", comp.Radius );

		var boxStr = ToolHandlerBase.GetString( args, "box_size" );
		if ( !string.IsNullOrEmpty( boxStr ) )
			comp.BoxSize = ParseVec3( boxStr, comp.BoxSize );

		if ( args.TryGetProperty( "volume", out _ ) )
			comp.Volume = ToolHandlerBase.GetFloat( args, "volume", comp.Volume );

		if ( args.TryGetProperty( "stay_active_on_exit", out _ ) )
			comp.StayActiveOnExit = ToolHandlerBase.GetBool( args, "stay_active_on_exit", comp.StayActiveOnExit );

		return ToolHandlerBase.JsonResult( new
		{
			created = true,
			gameObject = go.Name,
			gameObjectGuid = go.Id.ToString(),
			componentGuid = comp.Id.ToString(),
			triggerType = comp.Type.ToString(),
			soundscape = comp.Soundscape?.ResourcePath
		} );
	}

	// ──────────────────────────────────────────────
	//  configure_sound
	// ──────────────────────────────────────────────

	[Tool( "configure_sound", "Modify an existing SoundPoint/SoundBox sound component on a GameObject.", RequiresMainThread = true )]
	[Param( "name", "Name of GameObject with the sound component.", Required = false )]
	[Param( "guid", "GUID of GameObject with the sound component.", Required = false )]
	[Param( "sound_event", "Path to a .sound file.", Required = false )]
	[Param( "volume", "Playback volume multiplier.", Required = false, Type = "number" )]
	[Param( "pitch", "Playback pitch multiplier.", Required = false, Type = "number" )]
	[Param( "play_on_start", "Auto-play on scene start.", Required = false, Type = "boolean" )]
	[Param( "force_2d", "Force 2D playback.", Required = false, Type = "boolean" )]
	[Param( "repeat", "Repeat the sound.", Required = false, Type = "boolean" )]
	[Param( "distance_attenuation", "Fade out over distance.", Required = false, Type = "boolean" )]
	[Param( "distance", "Max audible distance.", Required = false, Type = "number" )]
	public static object ConfigureSound( JsonElement args )
	{
		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );

		var comp = go.Components.Get<BaseSoundComponent>();
		if ( comp == null )
			return ToolHandlerBase.ErrorResult( $"'{go.Name}' has no SoundPoint/SoundBox component." );

		ApplyBaseSoundProps( comp, args );

		return ToolHandlerBase.JsonResult( new
		{
			configured = true,
			gameObject = go.Name,
			componentType = comp.GetType().Name,
			soundEvent = comp.SoundEvent?.ResourcePath,
			volume = comp.Volume,
			pitch = comp.Pitch
		} );
	}

	// ──────────────────────────────────────────────
	//  play_sound_preview
	// ──────────────────────────────────────────────

	[Tool( "play_sound_preview", "Trigger StartSound() on a sound component for preview/testing.", RequiresMainThread = true )]
	[Param( "name", "Name of GameObject with the sound component.", Required = false )]
	[Param( "guid", "GUID of GameObject with the sound component.", Required = false )]
	[Param( "stop", "If true, call StopSound() instead. Default: false.", Required = false, Type = "boolean" )]
	public static object PlaySoundPreview( JsonElement args )
	{
		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );

		var comp = go.Components.Get<BaseSoundComponent>();
		if ( comp == null )
			return ToolHandlerBase.ErrorResult( $"'{go.Name}' has no sound component." );

		var stop = ToolHandlerBase.GetBool( args, "stop", false );
		if ( stop ) comp.StopSound();
		else comp.StartSound();

		return ToolHandlerBase.JsonResult( new
		{
			triggered = true,
			gameObject = go.Name,
			action = stop ? "StopSound" : "StartSound"
		} );
	}

	// ──────────────────────────────────────────────
	//  assign_sound
	// ──────────────────────────────────────────────

	[Tool( "assign_sound", "Assign a SoundEvent (.sound resource) to a sound component on a GameObject.", RequiresMainThread = true )]
	[Param( "name", "Name of GameObject with the sound component.", Required = false )]
	[Param( "guid", "GUID of GameObject with the sound component.", Required = false )]
	[Param( "sound_event", "Path to .sound file.", Required = true )]
	public static object AssignSound( JsonElement args )
	{
		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );

		var path = ToolHandlerBase.RequireString( args, "sound_event" );
		var sound = ResourceLibrary.Get<SoundEvent>( path );
		if ( sound == null ) return ToolHandlerBase.ErrorResult( $"SoundEvent not found: {path}" );

		var comp = go.Components.Get<BaseSoundComponent>();
		if ( comp == null )
			return ToolHandlerBase.ErrorResult( $"'{go.Name}' has no sound component." );

		comp.SoundEvent = sound;

		return ToolHandlerBase.JsonResult( new
		{
			assigned = true,
			gameObject = go.Name,
			componentType = comp.GetType().Name,
			soundEvent = sound.ResourcePath
		} );
	}

	// ──────────────────────────────────────────────
	//  Helpers
	// ──────────────────────────────────────────────

	private static void ApplyBaseSoundProps( BaseSoundComponent comp, JsonElement args )
	{
		var soundPath = ToolHandlerBase.GetString( args, "sound_event" );
		if ( !string.IsNullOrEmpty( soundPath ) )
		{
			var sound = ResourceLibrary.Get<SoundEvent>( soundPath );
			if ( sound != null ) comp.SoundEvent = sound;
		}

		if ( args.TryGetProperty( "play_on_start", out _ ) )
			comp.PlayOnStart = ToolHandlerBase.GetBool( args, "play_on_start", comp.PlayOnStart );
		if ( args.TryGetProperty( "volume", out _ ) )
			comp.Volume = ToolHandlerBase.GetFloat( args, "volume", comp.Volume );
		if ( args.TryGetProperty( "pitch", out _ ) )
			comp.Pitch = ToolHandlerBase.GetFloat( args, "pitch", comp.Pitch );
		if ( args.TryGetProperty( "force_2d", out _ ) )
			comp.Force2d = ToolHandlerBase.GetBool( args, "force_2d", comp.Force2d );
		if ( args.TryGetProperty( "repeat", out _ ) )
			comp.Repeat = ToolHandlerBase.GetBool( args, "repeat", comp.Repeat );
		if ( args.TryGetProperty( "distance_attenuation", out _ ) )
		{
			comp.DistanceAttenuationOverride = true;
			comp.DistanceAttenuation = ToolHandlerBase.GetBool( args, "distance_attenuation", comp.DistanceAttenuation );
		}
		if ( args.TryGetProperty( "distance", out _ ) )
			comp.Distance = ToolHandlerBase.GetFloat( args, "distance", comp.Distance );
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
