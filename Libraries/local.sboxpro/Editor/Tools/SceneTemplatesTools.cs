using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Editor;

namespace SboxPro;

public static class SceneTemplatesTools
{
	// ──────────────────────────────────────────────
	//  template_empty_scene
	// ──────────────────────────────────────────────

	[Tool( "template_empty_scene", "Generate a minimal viable .scene file with Camera, Sun (DirectionalLight), AmbientLight, and SkyBox2D pre-placed.", RequiresMainThread = true )]
	[Param( "path", "Scene path under Assets/ (e.g. 'scenes/empty_template.scene').", Required = true )]
	[Param( "open", "Open the scene in editor after creating. Default: true.", Required = false, Type = "boolean", Default = "true" )]
	public static object EmptyScene( JsonElement args )
	{
		var path = NormalizeScenePath( args, out var safePath );
		if ( safePath == null ) return ToolHandlerBase.ErrorResult( $"Invalid path: {path}" );
		if ( File.Exists( safePath ) ) return ToolHandlerBase.ErrorResult( $"Scene already exists: {PathNormalizer.ToRelative( safePath )}" );

		var gos = new JsonArray
		{
			BuildCameraGameObject( "Main Camera", "0,0,128", "0,0,0,1" ),
			BuildSunGameObject( "Sun" ),
			BuildAmbientLightGameObject( "Ambient Light" ),
			BuildSkyBox2DGameObject( "Sky" )
		};

		return WriteSceneFile( safePath, gos, args, "template_empty_scene" );
	}

	// ──────────────────────────────────────────────
	//  template_multiplayer_basic
	// ──────────────────────────────────────────────

	[Tool( "template_multiplayer_basic", "Generate a multiplayer-ready .scene file: empty scene + NetworkHelper + 4 spawn points around origin.", RequiresMainThread = true )]
	[Param( "path", "Scene path under Assets/ (e.g. 'scenes/mp_basic.scene').", Required = true )]
	[Param( "spawn_radius", "Radius from origin to place spawn points. Default: 256.", Required = false, Type = "number" )]
	[Param( "open", "Open the scene in editor after creating. Default: true.", Required = false, Type = "boolean", Default = "true" )]
	public static object MultiplayerBasicScene( JsonElement args )
	{
		var path = NormalizeScenePath( args, out var safePath );
		if ( safePath == null ) return ToolHandlerBase.ErrorResult( $"Invalid path: {path}" );
		if ( File.Exists( safePath ) ) return ToolHandlerBase.ErrorResult( $"Scene already exists: {PathNormalizer.ToRelative( safePath )}" );

		var radius = ToolHandlerBase.GetFloat( args, "spawn_radius", 256f );
		var spawns = new JsonArray();
		var spawnGuids = new string[4];
		for ( int i = 0; i < 4; i++ )
		{
			var angle = (i * Math.PI / 2);
			var x = Math.Cos( angle ) * radius;
			var y = Math.Sin( angle ) * radius;
			var pos = $"{x.ToString( System.Globalization.CultureInfo.InvariantCulture )},{y.ToString( System.Globalization.CultureInfo.InvariantCulture )},0";
			var (go, goGuid) = BuildSpawnPointGameObject( $"SpawnPoint_{i + 1}", pos );
			spawns.Add( go );
			spawnGuids[i] = goGuid;
		}

		var gos = new JsonArray
		{
			BuildCameraGameObject( "Main Camera", "0,0,128", "0,0,0,1" ),
			BuildSunGameObject( "Sun" ),
			BuildAmbientLightGameObject( "Ambient Light" ),
			BuildSkyBox2DGameObject( "Sky" ),
			BuildNetworkHelperGameObject( "NetworkHelper", spawnGuids )
		};
		foreach ( var s in spawns ) gos.Add( s.DeepClone() );

		return WriteSceneFile( safePath, gos, args, "template_multiplayer_basic" );
	}

	// ──────────────────────────────────────────────
	//  GameObject builders
	// ──────────────────────────────────────────────

	private static JsonObject BuildCameraGameObject( string name, string position, string rotation )
	{
		var goGuid = Guid.NewGuid().ToString();
		var compGuid = Guid.NewGuid().ToString();
		return new JsonObject
		{
			["__guid"] = goGuid,
			["__version"] = 2,
			["Flags"] = 0,
			["Name"] = name,
			["Position"] = position,
			["Rotation"] = rotation,
			["Scale"] = "1,1,1",
			["Tags"] = "maincamera",
			["Enabled"] = true,
			["NetworkMode"] = 2,
			["Components"] = new JsonArray
			{
				new JsonObject
				{
					["__type"] = "Sandbox.CameraComponent",
					["__guid"] = compGuid,
					["__enabled"] = true,
					["BackgroundColor"] = "0.5,0.5,0.5,1",
					["ClearFlags"] = "All",
					["FieldOfView"] = 60,
					["IsMainCamera"] = true,
					["Orthographic"] = false,
					["OrthographicHeight"] = 1204,
					["Priority"] = 1,
					["EnablePostProcessing"] = true,
					["ZFar"] = 10000,
					["ZNear"] = 5
				}
			}
		};
	}

	private static JsonObject BuildSunGameObject( string name )
	{
		// Pitch -45, yaw 45 → Quaternion (approx). Easier: just set rotation as Angles via Quaternion math.
		// Quaternion for (pitch=-45, yaw=45, roll=0) — letting engine-space ZYX rotation:
		// We'll write a known good value matching the editor's "Add Sun" default.
		var goGuid = Guid.NewGuid().ToString();
		var compGuid = Guid.NewGuid().ToString();
		return new JsonObject
		{
			["__guid"] = goGuid,
			["__version"] = 2,
			["Flags"] = 0,
			["Name"] = name,
			["Position"] = "0,0,512",
			// Engine default sun rotation (sun pointing down-and-forward from south-west)
			["Rotation"] = "-0.1830127,0.1830127,0.6830127,0.6830127",
			["Scale"] = "1,1,1",
			["Tags"] = "light_directional,light",
			["Enabled"] = true,
			["NetworkMode"] = 2,
			["Components"] = new JsonArray
			{
				new JsonObject
				{
					["__type"] = "Sandbox.DirectionalLight",
					["__guid"] = compGuid,
					["__enabled"] = true,
					["FogMode"] = "Enabled",
					["FogStrength"] = 1,
					["LightColor"] = "1,0.95,0.85,1",
					["Shadows"] = true,
					["SkyColor"] = "0.4,0.45,0.55,1",
					["ShadowCascadeCount"] = 4
				}
			}
		};
	}

	private static JsonObject BuildAmbientLightGameObject( string name )
	{
		var goGuid = Guid.NewGuid().ToString();
		var compGuid = Guid.NewGuid().ToString();
		return new JsonObject
		{
			["__guid"] = goGuid,
			["__version"] = 2,
			["Flags"] = 0,
			["Name"] = name,
			["Position"] = "0,0,0",
			["Rotation"] = "0,0,0,1",
			["Scale"] = "1,1,1",
			["Tags"] = "light_ambient,light",
			["Enabled"] = true,
			["NetworkMode"] = 2,
			["Components"] = new JsonArray
			{
				new JsonObject
				{
					["__type"] = "Sandbox.AmbientLight",
					["__guid"] = compGuid,
					["__enabled"] = true,
					["Color"] = "0.2,0.22,0.26,1"
				}
			}
		};
	}

	private static JsonObject BuildSkyBox2DGameObject( string name )
	{
		var goGuid = Guid.NewGuid().ToString();
		var compGuid = Guid.NewGuid().ToString();
		return new JsonObject
		{
			["__guid"] = goGuid,
			["__version"] = 2,
			["Flags"] = 0,
			["Name"] = name,
			["Position"] = "0,0,0",
			["Rotation"] = "0,0,0,1",
			["Scale"] = "1,1,1",
			["Tags"] = "skybox",
			["Enabled"] = true,
			["NetworkMode"] = 2,
			["Components"] = new JsonArray
			{
				new JsonObject
				{
					["__type"] = "Sandbox.SkyBox2D",
					["__guid"] = compGuid,
					["__enabled"] = true,
					["SkyIndirectLighting"] = true,
					["Tint"] = "1,1,1,1"
				}
			}
		};
	}

	private static (JsonObject go, string guid) BuildSpawnPointGameObject( string name, string position )
	{
		var goGuid = Guid.NewGuid().ToString();
		var compGuid = Guid.NewGuid().ToString();
		return (new JsonObject
		{
			["__guid"] = goGuid,
			["__version"] = 2,
			["Flags"] = 0,
			["Name"] = name,
			["Position"] = position,
			["Rotation"] = "0,0,0,1",
			["Scale"] = "1,1,1",
			["Tags"] = "spawnpoint",
			["Enabled"] = true,
			["NetworkMode"] = 2,
			["Components"] = new JsonArray
			{
				new JsonObject
				{
					["__type"] = "Sandbox.SpawnPoint",
					["__guid"] = compGuid,
					["__enabled"] = true,
					["Color"] = "0.4,1,0.4,1"
				}
			}
		}, goGuid);
	}

	private static JsonObject BuildNetworkHelperGameObject( string name, string[] spawnGuids )
	{
		var goGuid = Guid.NewGuid().ToString();
		var compGuid = Guid.NewGuid().ToString();
		var spawnRefs = new JsonArray();
		foreach ( var g in spawnGuids ) spawnRefs.Add( g );

		return new JsonObject
		{
			["__guid"] = goGuid,
			["__version"] = 2,
			["Flags"] = 0,
			["Name"] = name,
			["Position"] = "0,0,0",
			["Rotation"] = "0,0,0,1",
			["Scale"] = "1,1,1",
			["Tags"] = "",
			["Enabled"] = true,
			["NetworkMode"] = 2,
			["Components"] = new JsonArray
			{
				new JsonObject
				{
					["__type"] = "Sandbox.NetworkHelper",
					["__guid"] = compGuid,
					["__enabled"] = true,
					["StartServer"] = true,
					["SpawnPoints"] = spawnRefs
				}
			}
		};
	}

	// ──────────────────────────────────────────────
	//  Helpers
	// ──────────────────────────────────────────────

	private static string NormalizeScenePath( JsonElement args, out string safePath )
	{
		var path = ToolHandlerBase.RequireString( args, "path" );
		if ( !path.EndsWith( ".scene", StringComparison.OrdinalIgnoreCase ) ) path += ".scene";
		safePath = PathNormalizer.ResolveAssetPath( path );
		return path;
	}

	private static object WriteSceneFile( string safePath, JsonArray gos, JsonElement args, string templateName )
	{
		var openAfter = ToolHandlerBase.GetBool( args, "open", true );
		var sceneGuid = Guid.NewGuid().ToString();

		var sceneJson = new JsonObject
		{
			["__guid"] = sceneGuid,
			["GameObjects"] = gos,
			["SceneProperties"] = new JsonObject
			{
				["FixedUpdateFrequency"] = 50,
				["MaxFixedUpdates"] = 5,
				["NetworkInterpolation"] = true,
				["TimeScale"] = 1,
				["UseFixedUpdate"] = true,
				["PhysicsSubSteps"] = 1,
				["ThreadedAnimation"] = true,
				["NetworkFrequency"] = 30,
				["WantsSystemScene"] = true,
				["Metadata"] = new JsonObject(),
				["NavMesh"] = new JsonObject
				{
					["Enabled"] = false,
					["AgentHeight"] = 64,
					["AgentRadius"] = 16,
					["AgentStepSize"] = 18,
					["AgentMaxSlope"] = 40
				}
			},
			["ResourceVersion"] = 3,
			["Title"] = (string)null,
			["Description"] = (string)null,
			["__references"] = new JsonArray(),
			["__version"] = 3
		};

		try
		{
			var dir = Path.GetDirectoryName( safePath );
			if ( !string.IsNullOrEmpty( dir ) ) Directory.CreateDirectory( dir );
			SerializationHelpers.WriteFile( safePath, sceneJson ); // also registers via AssetSystem (#06)

			var rel = PathNormalizer.ToRelative( safePath );

			if ( openAfter )
			{
				var asset = AssetSystem.FindByPath( PathNormalizer.NormalizeAssetPath( rel ) );
				asset?.OpenInEditor();
			}

			return ToolHandlerBase.JsonResult( new
			{
				generated = true,
				template = templateName,
				path = rel,
				sceneGuid,
				gameObjectCount = gos.Count
			} );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Failed to write scene: {ex.Message}" );
		}
	}
}
