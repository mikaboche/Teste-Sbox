using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;

namespace SboxPro;

// ──────────────────────────────────────────────
//  ComposerPrefabBuilder
//
//  Builds in-memory prefab JSON for player templates. The same JSON is written
//  twice: once as a standalone .prefab file (for drag-and-drop into other scenes),
//  and once inlined into the composer-generated scene as an "in-scene template"
//  GameObject (Enabled=false) that NetworkHelper.PlayerPrefab references by GUID.
//
//  We use the in-scene reference pattern (not a prefab-file ref in the scene) because
//  it has been validated to work end-to-end with NetworkHelper, while the "prefab
//  file ref" serialization had historical issues with incomplete stub generation.
//  Users who want to instance the player elsewhere can drag the .prefab file in
//  manually.
// ──────────────────────────────────────────────

public enum ComposerPlayerKind
{
	Fps,        // Stock CharacterController + camera + PlayerController + NetworkedPlayer + Pistol child
	Parkour,    // + RagdollDriver + SkinnedModelRenderer placeholder
	Survival    // + Inventory holder + Grab + Interact
}

public sealed class PrefabBuildResult
{
	public string PlayerGuid;     // Root GameObject guid (for NetworkHelper.PlayerPrefab in-scene ref)
	public JsonObject PrefabFile; // Full .prefab file payload
	public JsonObject InSceneGameObject; // Same root GO but with Enabled=false, ready to inject into a scene
}

public static class ComposerPrefabBuilder
{
	public static PrefabBuildResult Build( ComposerPlayerKind kind, string ns, ComposerOptions options = null )
	{
		options ??= new ComposerOptions();

		var rootGuid = Guid.NewGuid().ToString();
		var playerCtrlGuid = Guid.NewGuid().ToString();
		var netPlayerGuid = Guid.NewGuid().ToString();
		var bodyGoGuid = Guid.NewGuid().ToString();
		var bodyRendererGuid = Guid.NewGuid().ToString();

		// 🔄 [FORK] Use Sandbox.PlayerController (built-in, sealed) instead of generating
		// a custom PlayerController.cs. The built-in has Body+Input+Camera+Look+Animator
		// features in one component — no manual animgraph wiring needed because
		// UseAnimatorControls=true + Renderer ref drives Citizen idle/walk/run/jump/crouch
		// + footsteps automatically (issue #22). Generating a custom one duplicated effort
		// and never animated because parameters were never sent.
		var components = new JsonArray
		{
			BuildComponent( "Sandbox.PlayerController", playerCtrlGuid, new JsonObject
			{
				["BodyRadius"] = 16f,
				["BodyHeight"] = 72f,
				["BodyMass"] = 100f,
				["UseInputControls"] = true,
				["WalkSpeed"] = options.WalkSpeed,
				["RunSpeed"] = options.RunSpeed,
				["DuckedSpeed"] = options.CrouchSpeed,
				["JumpSpeed"] = options.JumpStrength,
				["UseCameraControls"] = true,
				["ThirdPerson"] = options.StartInThirdPerson,
				["HideBodyInFirstPerson"] = true,
				["CameraOffset"] = "256,0,12",
				["EyeDistanceFromTop"] = 8f,
				["UseLookControls"] = true,
				["PitchClamp"] = "-89,89",
				["LookSensitivity"] = 1f,
				["RotateWithGround"] = true,
				["UseAnimatorControls"] = true,
				["Renderer"] = ComponentRef( bodyRendererGuid, bodyGoGuid, "SkinnedModelRenderer" )
			} ),
			BuildComponent( $"{ns}.NetworkedPlayer", netPlayerGuid, new JsonObject
			{
				// MaxHealth is the only [Property] field; Health and PlayerName are [Sync]
				// without [Property] so the prefab system doesn't serialise them — host
				// initialises Health = MaxHealth in OnStart, owner sets PlayerName from
				// Network.Owner.DisplayName.
				["MaxHealth"] = 100
			} )
		};

		// No Camera child here — the scene-level "Main Camera" already exists (added by
		// SceneTemplatesTools.MultiplayerBasicScene) and that's what Sandbox.PlayerController
		// will drive. Adding a second camera tagged "maincamera" as a player child caused
		// the engine to lock view to first-person at the player's head regardless of the
		// ThirdPerson flag (issue #20).
		var children = new JsonArray
		{
			BuildBodyChild( bodyGoGuid, bodyRendererGuid, kind )
		};

		// Kind-specific extras on the root
		switch ( kind )
		{
			case ComposerPlayerKind.Parkour:
				var ragdollGuid = Guid.NewGuid().ToString();
				components.Add( BuildComponent( $"{ns}.RagdollDriver", ragdollGuid, new JsonObject
				{
					["StartMode"] = "None",
					["FollowRootPosition"] = true,
					["FollowRootRotation"] = false,
					["HitRadius"] = 30f,
					["HitDuration"] = 0.5f,
					["HitRotationStrength"] = 15f
				} ) );
				break;

			case ComposerPlayerKind.Survival:
				var holderGuid = Guid.NewGuid().ToString();
				// SelectedSlot is [Sync] without [Property] — initial value comes from the
				// field declaration (= 0), prefab can't override it. Empty props is correct.
				components.Add( BuildComponent( $"{ns}.Inventory.PlayerInventoryHolder", holderGuid, new JsonObject() ) );
				if ( options.IncludeGrab )
					components.Add( BuildComponent( $"{ns}.PlayerGrab", Guid.NewGuid().ToString(), new JsonObject() ) );
				if ( options.IncludeInteract )
					components.Add( BuildComponent( $"{ns}.PlayerInteract", Guid.NewGuid().ToString(), new JsonObject() ) );

				// HUD child: ScreenPanel + PlayerInventoryUI + PlayerInventoryHotbarUI, both
				// PanelComponents bound to the holder above. Without this the Razor UIs were
				// generated to disk but never instantiated — nothing rendered at runtime.
				children.Add( BuildSurvivalHudChild( ns, rootGuid, holderGuid ) );
				break;
		}

		// FPS: optional grab/interact on root
		if ( kind == ComposerPlayerKind.Fps )
		{
			if ( options.IncludeGrab )
				components.Add( BuildComponent( $"{ns}.PlayerGrab", Guid.NewGuid().ToString(), new JsonObject() ) );
			if ( options.IncludeInteract )
				components.Add( BuildComponent( $"{ns}.PlayerInteract", Guid.NewGuid().ToString(), new JsonObject() ) );
		}

		var rootGO = new JsonObject
		{
			["__guid"] = rootGuid,
			["__version"] = 2,
			["Flags"] = 0,
			["Name"] = "Player",
			["Position"] = "0,0,80",
			["Rotation"] = "0,0,0,1",
			["Scale"] = "1,1,1",
			["Tags"] = "player",
			["Enabled"] = true,
			["NetworkMode"] = 2,
			["NetworkFlags"] = 0,
			["NetworkOrphaned"] = 0,
			["NetworkTransmit"] = true,
			["OwnerTransfer"] = 1,
			["Components"] = components,
			["Children"] = children
		};

		// .prefab payload
		var prefabFile = new JsonObject
		{
			["RootObject"] = WithProperties( DeepCopy( rootGO ) ),
			["ResourceVersion"] = 2,
			["ShowInMenu"] = false,
			["MenuPath"] = (string)null,
			["MenuIcon"] = (string)null,
			["DontBreakAsTemplate"] = false,
			["__references"] = new JsonArray(),
			["__version"] = 2
		};

		// In-scene injected GO (template) — disabled so it doesn't run
		var inScene = DeepCopy( rootGO );
		inScene["Name"] = "Player Template";
		inScene["Enabled"] = false;

		return new PrefabBuildResult
		{
			PlayerGuid = rootGuid,
			PrefabFile = prefabFile,
			InSceneGameObject = inScene
		};
	}

	private static JsonObject BuildCameraChild( string goGuid, string compGuid )
	{
		return new JsonObject
		{
			["__guid"] = goGuid,
			["__version"] = 2,
			["Flags"] = 0,
			["Name"] = "Camera",
			["Position"] = "0,0,64",
			["Rotation"] = "0,0,0,1",
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
					["FieldOfView"] = 70,
					["IsMainCamera"] = true,
					["Orthographic"] = false,
					["Priority"] = 1,
					["EnablePostProcessing"] = true,
					["ZFar"] = 10000,
					["ZNear"] = 5
				}
			},
			["Children"] = new JsonArray()
		};
	}

	// Survival HUD: ScreenPanel + the two generated Razor PanelComponents. The Holder
	// property on each PanelComponent is wired by ComponentRef to the PlayerInventoryHolder
	// on the player root — same prefab, so component_id + go = (holderGuid, rootGuid).
	// Razor namespaces follow `{ns}.Inventory` because InventorySkeletonTemplate writes
	// the @namespace directive that way.
	private static JsonObject BuildSurvivalHudChild( string ns, string rootGuid, string holderGuid )
	{
		var screenPanelGuid = Guid.NewGuid().ToString();
		var inventoryUiGuid = Guid.NewGuid().ToString();
		var hotbarUiGuid = Guid.NewGuid().ToString();

		var components = new JsonArray
		{
			new JsonObject
			{
				["__type"] = "Sandbox.UI.ScreenPanel",
				["__guid"] = screenPanelGuid,
				["__enabled"] = true,
				["AutoScreenScale"] = true,
				["Opacity"] = 1f,
				["Scale"] = 1f,
				["ZIndex"] = 100
			},
			new JsonObject
			{
				["__type"] = $"{ns}.Inventory.PlayerInventoryUI",
				["__guid"] = inventoryUiGuid,
				["__enabled"] = true,
				["Holder"] = ComponentRef( holderGuid, rootGuid, "PlayerInventoryHolder" ),
				["ToggleAction"] = "Inventory",
				["IsOpen"] = false
			},
			new JsonObject
			{
				["__type"] = $"{ns}.Inventory.PlayerInventoryHotbarUI",
				["__guid"] = hotbarUiGuid,
				["__enabled"] = true,
				["Holder"] = ComponentRef( holderGuid, rootGuid, "PlayerInventoryHolder" )
			}
		};

		return new JsonObject
		{
			["__guid"] = Guid.NewGuid().ToString(),
			["__version"] = 2,
			["Flags"] = 0,
			["Name"] = "HUD",
			["Position"] = "0,0,0",
			["Rotation"] = "0,0,0,1",
			["Scale"] = "1,1,1",
			["Tags"] = "",
			["Enabled"] = true,
			["NetworkMode"] = 2,
			["Components"] = components,
			["Children"] = new JsonArray()
		};
	}

	private static JsonObject BuildBodyChild( string goGuid, string rendererGuid, ComposerPlayerKind kind )
	{
		var components = new JsonArray();
		// Body GO always gets a SkinnedModelRenderer with Citizen — third-person modes
		// (Survival, Parkour) need it visible; first-person mode (Fps default) hides it
		// automatically via Sandbox.PlayerController.HideBodyInFirstPerson when Renderer
		// is wired. Without this, third-person players were invisible (issue #19).
		// Parkour additionally relies on it so RagdollDriver can find a SkinnedModelRenderer
		// via GetComponentInChildren<>.
		// rendererGuid is provided by the caller so PlayerController.Renderer can ComponentRef it.
		components.Add( new JsonObject
		{
			["__type"] = "Sandbox.SkinnedModelRenderer",
			["__guid"] = rendererGuid,
			["__enabled"] = true,
			["Model"] = "models/citizen/citizen.vmdl",
			["Tint"] = "1,1,1,1",
			["RenderType"] = "On",
			["UseAnimGraph"] = true,
			["CreateBoneObjects"] = false
		} );

		return new JsonObject
		{
			["__guid"] = goGuid,
			["__version"] = 2,
			["Flags"] = 0,
			["Name"] = "Body",
			["Position"] = "0,0,0",
			["Rotation"] = "0,0,0,1",
			["Scale"] = "1,1,1",
			["Tags"] = "",
			["Enabled"] = true,
			["NetworkMode"] = 2,
			["Components"] = components,
			["Children"] = new JsonArray()
		};
	}

	private static JsonObject BuildComponent( string typeName, string guid, JsonObject props )
	{
		var c = new JsonObject
		{
			["__type"] = typeName,
			["__guid"] = guid,
			["__enabled"] = true
		};
		foreach ( var kv in props )
			c[kv.Key] = DeepCopy( kv.Value );
		return c;
	}

	/// <summary>Reference to a Component on a specific GameObject (component_id + go + component_type).</summary>
	private static JsonObject ComponentRef( string componentGuid, string ownerGoGuid, string componentTypeShortName )
	{
		return new JsonObject
		{
			["_type"] = "component",
			["component_id"] = componentGuid,
			["go"] = ownerGoGuid,
			["component_type"] = componentTypeShortName
		};
	}

	/// <summary>Reference to a GameObject by guid (in-scene or in-prefab).</summary>
	private static JsonObject GameObjectRef( string goGuid )
	{
		return new JsonObject
		{
			["_type"] = "gameobject",
			["go"] = goGuid
		};
	}

	private static JsonObject WithProperties( JsonObject root )
	{
		root["__properties"] = new JsonObject
		{
			["NetworkInterpolation"] = true,
			["TimeScale"] = 1,
			["WantsSystemScene"] = true,
			["Metadata"] = new JsonObject(),
			["NavMesh"] = new JsonObject
			{
				["Enabled"] = false,
				["IncludeStaticBodies"] = true,
				["IncludeKeyframedBodies"] = true,
				["EditorAutoUpdate"] = true,
				["AgentHeight"] = 64,
				["AgentRadius"] = 16,
				["AgentStepSize"] = 18,
				["AgentMaxSlope"] = 40,
				["ExcludedBodies"] = "",
				["IncludedBodies"] = ""
			}
		};
		root["__variables"] = new JsonArray();
		return root;
	}

	private static JsonNode DeepCopy( JsonNode node )
	{
		if ( node is null ) return null;
		return JsonNode.Parse( node.ToJsonString() );
	}

	private static JsonObject DeepCopy( JsonObject node ) => DeepCopy( (JsonNode)node ) as JsonObject;

	// ──────────────────────────────────────────────
	//  Scene injection — modifies a generated scene file to add the player
	//  template as an in-scene GameObject and wire NetworkHelper.PlayerPrefab.
	// ──────────────────────────────────────────────

	public static bool InjectIntoScene( string scenePath, JsonObject playerInScene, string playerGuid, out string error )
	{
		error = null;
		try
		{
			if ( !File.Exists( scenePath ) ) { error = $"Scene file not found: {scenePath}"; return false; }

			var raw = File.ReadAllText( scenePath );
			if ( JsonNode.Parse( raw ) is not JsonObject root )
			{
				error = "Scene file is not a JSON object.";
				return false;
			}
			if ( root["GameObjects"] is not JsonArray gos )
			{
				error = "Scene file has no GameObjects array.";
				return false;
			}

			// Append player template
			gos.Add( DeepCopy( playerInScene ) );

			// Find NetworkHelper component anywhere in the scene and set PlayerPrefab
			bool wired = false;
			foreach ( var go in gos )
			{
				if ( go is not JsonObject goObj ) continue;
				if ( goObj["Components"] is not JsonArray comps ) continue;
				foreach ( var c in comps )
				{
					if ( c is not JsonObject comp ) continue;
					if ( comp["__type"]?.GetValue<string>() == "Sandbox.NetworkHelper" )
					{
						comp["PlayerPrefab"] = GameObjectRef( playerGuid );
						wired = true;
						break;
					}
				}
				if ( wired ) break;
			}

			if ( !wired )
			{
				error = "Scene had no NetworkHelper component to wire PlayerPrefab into.";
				return false;
			}

			File.WriteAllText( scenePath, root.ToJsonString( new System.Text.Json.JsonSerializerOptions { WriteIndented = true } ) );
			try { Editor.AssetSystem.RegisterFile( scenePath ); } catch { } // #06
			return true;
		}
		catch ( Exception ex )
		{
			error = $"Scene injection failed: {ex.Message}";
			return false;
		}
	}
}

// Small DTO so composers can pass tuning into the prefab build without long signatures.
public sealed class ComposerOptions
{
	public float WalkSpeed = 180f;
	public float RunSpeed = 320f;
	public float CrouchSpeed = 100f;
	public float JumpStrength = 320f;
	public bool StartInThirdPerson = false;
	public bool IncludeGrab = false;
	public bool IncludeInteract = false;
}
