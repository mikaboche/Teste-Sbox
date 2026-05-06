using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Editor;
using Sandbox;

namespace SboxPro;

// ──────────────────────────────────────────────
//  Game composers — orchestrate multiple templates into a project starter.
//
//  Each composer:
//   1. Calls the underlying template generators (player_controller, weapon, etc)
//   2. Aggregates the generated file paths into a single result
//   3. Stops at the first failure and reports it
//
//  Idea: when the user says "I want to start an FPS", they call one composer
//  and get a working baseline scene + player + weapon + networking instead of
//  invoking 5 separate template tools and wiring them by hand.
// ──────────────────────────────────────────────

public static class GameComposersTools
{
	// ──────────────────────────────────────────────
	//  start_multiplayer_fps_project
	// ──────────────────────────────────────────────

	[Tool( "start_multiplayer_fps_project", "Compose a multiplayer FPS starter: PlayerController (FPS view, sprint, crouch) + NetworkedPlayer (sync health/name) + Weapons (Pistol + Knife) + Interact (optional) + Grab (optional) + multiplayer scene with NetworkHelper. One-shot starter for shooter games.", RequiresMainThread = true )]
	[Param( "namespace", "C# namespace for generated Components. Default: 'Game'.", Required = false )]
	[Param( "scene_name", "Scene file name under Assets/scenes/. Default: 'fps_main'.", Required = false )]
	[Param( "include_interact", "Generate template_interact_component for use/door/button gameplay. Default: true.", Required = false, Type = "boolean", Default = "true" )]
	[Param( "include_grab", "Generate template_grab_component for physics pickups. Default: false.", Required = false, Type = "boolean", Default = "false" )]
	[Param( "spawn_radius", "Distance from origin for the 4 spawn points. Default: 256.", Required = false, Type = "number" )]
	public static object StartMultiplayerFpsProject( JsonElement args )
	{
		var ns = ToolHandlerBase.GetString( args, "namespace", "Game" );
		var sceneName = ToolHandlerBase.GetString( args, "scene_name", "fps_main" );
		var includeInteract = ToolHandlerBase.GetBool( args, "include_interact", true );
		var includeGrab = ToolHandlerBase.GetBool( args, "include_grab", false );
		var spawnRadius = ToolHandlerBase.GetFloat( args, "spawn_radius", 256f );

		var generated = new List<object>();

		// 🔄 [FORK] No PlayerController.cs generated — use Sandbox.PlayerController (built-in,
		// sealed) which handles input + camera + look + animation in one component (issue #22).
		// The custom PlayerController.cs we used to emit duplicated this and never animated
		// because animgraph parameters weren't being sent. ComposerPrefabBuilder wires
		// Sandbox.PlayerController.Renderer to the Body's SkinnedModelRenderer so Citizen
		// idle/walk/run/jump/crouch + footsteps work automatically.

		var netResult = CallTool( () => TemplatesTools.NetworkedPlayerTemplate( BuildArgs(
			("path", "Code/Player/NetworkedPlayer.cs"),
			("class_name", "NetworkedPlayer"),
			("namespace", ns),
			("max_health", 100)
		) ) );
		if ( netResult.error != null ) return netResult.error;
		generated.Add( new { template = "networked_player", path = "Code/Player/NetworkedPlayer.cs" } );

		var weaponResult = CallTool( () => CommunityTemplatesTools.WeaponTemplate( BuildArgs(
			("path", "Code/Combat/Weapons.cs"),
			("namespace", ns + ".Combat")
		) ) );
		if ( weaponResult.error != null ) return weaponResult.error;
		generated.Add( new { template = "weapon", path = "Code/Combat/Weapons.cs" } );

		if ( includeInteract )
		{
			var r = CallTool( () => CommunityTemplatesTools.InteractComponentTemplate( BuildArgs(
				("path", "Code/Player/InteractComponent.cs"),
				("class_name", "PlayerInteract"),
				("namespace", ns)
			) ) );
			if ( r.error != null ) return r.error;
			generated.Add( new { template = "interact_component", path = "Code/Player/InteractComponent.cs" } );
		}

		if ( includeGrab )
		{
			var r = CallTool( () => CommunityTemplatesTools.GrabComponentTemplate( BuildArgs(
				("path", "Code/Player/GrabComponent.cs"),
				("class_name", "PlayerGrab"),
				("namespace", ns)
			) ) );
			if ( r.error != null ) return r.error;
			generated.Add( new { template = "grab_component", path = "Code/Player/GrabComponent.cs" } );
		}

		var sceneResult = CallTool( () => SceneTemplatesTools.MultiplayerBasicScene( BuildArgs(
			("path", $"scenes/{sceneName}.scene"),
			("spawn_radius", spawnRadius),
			("open", false)
		) ) );
		if ( sceneResult.error != null ) return sceneResult.error;
		generated.Add( new { template = "multiplayer_basic", path = $"scenes/{sceneName}.scene" } );

		var prefabResult = WritePlayerPrefabAndWireScene(
			ComposerPlayerKind.Fps, ns, $"scenes/{sceneName}.scene", "prefabs/sboxpro_player.prefab",
			new ComposerOptions { IncludeGrab = includeGrab, IncludeInteract = includeInteract },
			generated );
		if ( prefabResult != null ) return prefabResult;

		return ToolHandlerBase.JsonResult( new
		{
			composer = "start_multiplayer_fps_project",
			project_kind = "Multiplayer FPS",
			generated,
			next_steps = new[]
			{
				"Run trigger_hotload to compile the generated C# files.",
				"Open the generated scene — Camera, Sun, Sky, NetworkHelper, 4 spawn points, and a Player template are pre-placed and wired.",
				"Hit Play. The NetworkHelper will clone the Player template per connection and assign ownership.",
				"Drop a Pistol or Knife child GameObject under the Player template if you want a starting weapon.",
				"The .prefab file at prefabs/sboxpro_player.prefab can be dragged into other scenes for reuse."
			}
		} );
	}

	// ──────────────────────────────────────────────
	//  start_parkour_game
	// ──────────────────────────────────────────────

	[Tool( "start_parkour_game", "Compose a parkour/obby starter: PlayerController (sprint, crouch, jump) + NetworkedPlayer + Ragdoll Driver (Shrimple) + multiplayer scene. For movement-focused games where falling triggers a ragdoll. Auto-installs fish.shrimple_ragdolls if missing.", RequiresMainThread = true )]
	[Param( "namespace", "C# namespace. Default: 'Game'.", Required = false )]
	[Param( "scene_name", "Scene file name under Assets/scenes/. Default: 'parkour_main'.", Required = false )]
	[Param( "spawn_radius", "Distance from origin for spawn points. Default: 128.", Required = false, Type = "number" )]
	[Param( "auto_install_deps", "Auto-install required libraries before generating. Default: true.", Required = false, Type = "boolean", Default = "true" )]
	public static async Task<object> StartParkourGame( JsonElement args )
	{
		var ns = ToolHandlerBase.GetString( args, "namespace", "Game" );
		var sceneName = ToolHandlerBase.GetString( args, "scene_name", "parkour_main" );
		var spawnRadius = ToolHandlerBase.GetFloat( args, "spawn_radius", 128f );
		var autoInstall = ToolHandlerBase.GetBool( args, "auto_install_deps", true );

		// Pre-flight: ensure required libraries are present before generating code that
		// references their types. Without this, the composer succeeds at writing files
		// but the project fails to compile because the types don't exist.
		var preflight = await EnsureDependenciesAsync( new[] { "fish.shrimple_ragdolls" }, autoInstall );
		if ( preflight != null ) return preflight;

		var generated = new List<object>();

		// 🔄 [FORK] No PlayerController.cs — Sandbox.PlayerController built-in handles
		// movement + animation. ComposerOptions tunes the speed values (parkour feel).

		var netResult = CallTool( () => TemplatesTools.NetworkedPlayerTemplate( BuildArgs(
			("path", "Code/Player/NetworkedPlayer.cs"),
			("class_name", "NetworkedPlayer"),
			("namespace", ns)
		) ) );
		if ( netResult.error != null ) return netResult.error;
		generated.Add( new { template = "networked_player", path = "Code/Player/NetworkedPlayer.cs" } );

		var ragdollResult = CallTool( () => CommunityTemplatesTools.ShrimpleRagdollTemplate( BuildArgs(
			("path", "Code/Combat/RagdollDriver.cs"),
			("class_name", "RagdollDriver"),
			("namespace", ns)
		) ) );
		if ( ragdollResult.error != null ) return ragdollResult.error;
		generated.Add( new { template = "shrimple_ragdoll", path = "Code/Combat/RagdollDriver.cs" } );

		var sceneResult = CallTool( () => SceneTemplatesTools.MultiplayerBasicScene( BuildArgs(
			("path", $"scenes/{sceneName}.scene"),
			("spawn_radius", spawnRadius),
			("open", false)
		) ) );
		if ( sceneResult.error != null ) return sceneResult.error;
		generated.Add( new { template = "multiplayer_basic", path = $"scenes/{sceneName}.scene" } );

		var prefabResult = WritePlayerPrefabAndWireScene(
			ComposerPlayerKind.Parkour, ns, $"scenes/{sceneName}.scene", "prefabs/sboxpro_player.prefab",
			new ComposerOptions { WalkSpeed = 220f, RunSpeed = 380f, JumpStrength = 400f, StartInThirdPerson = true },
			generated );
		if ( prefabResult != null ) return prefabResult;

		return ToolHandlerBase.JsonResult( new
		{
			composer = "start_parkour_game",
			project_kind = "Multiplayer parkour with ragdoll",
			generated,
			requires_libraries = new[] { "fish.shrimple_ragdolls" },
			next_steps = new[]
			{
				"Install fish.shrimple_ragdolls via Asset Browser if not already installed.",
				"Run trigger_hotload to compile.",
				"Open the scene — the Player template is pre-placed with PlayerController + RagdollDriver + a placeholder SkinnedModelRenderer.",
				"Set the SkinnedModelRenderer.Model on the Body child to your rigged character (e.g. Citizen). RagdollDriver auto-discovers it via GetComponentInChildren.",
				"Add level geometry — boxes, ramps, gaps. Tag death zones with 'kill' and call RagdollDriver.SetMode(RagdollMode.Enabled) on contact."
			}
		} );
	}

	// ──────────────────────────────────────────────
	//  start_survival_game
	// ──────────────────────────────────────────────

	[Tool( "start_survival_game", "Compose a survival starter: NetworkedPlayer + Inventory (Tetris grid + hotbar + UI + drag-drop) + multiplayer scene. For games centered on resource gathering and inventory management. Inventory uses vendored SboxPro.Inventory (local.sboxpro_inventory library) — zero external deps. Pickup/interaction is intentionally NOT included; it ships as part of the inventory's own (Phase 3) world-item + interactable system once that lands, to avoid two parallel raycast pipelines competing on the player.", RequiresMainThread = true )]
	[Param( "namespace", "C# namespace. Default: 'Game'.", Required = false )]
	[Param( "scene_name", "Scene file name under Assets/scenes/. Default: 'survival_main'.", Required = false )]
	[Param( "inventory_width", "Inventory grid width. Default: 8.", Required = false, Type = "integer" )]
	[Param( "inventory_height", "Inventory grid height. Default: 6.", Required = false, Type = "integer" )]
	[Param( "hotbar_size", "Hotbar slots (1-9). Default: 6.", Required = false, Type = "integer" )]
	[Param( "auto_install_deps", "Auto-install required libraries before generating. Default: true.", Required = false, Type = "boolean", Default = "true" )]
	public static async Task<object> StartSurvivalGame( JsonElement args )
	{
		// 🔄 [FORK] conna.inventory is now vendored as `local.sboxpro_inventory` library.
		// No external install step needed — the library ships alongside `local.sboxpro`.
		// Generated code uses `using SboxPro.Inventory;` directly.
		await Task.CompletedTask;   // keep async signature for ToolRegistry compatibility

		var ns = ToolHandlerBase.GetString( args, "namespace", "Game" );
		var sceneName = ToolHandlerBase.GetString( args, "scene_name", "survival_main" );
		var invW = ToolHandlerBase.GetInt( args, "inventory_width", 8 );
		var invH = ToolHandlerBase.GetInt( args, "inventory_height", 6 );
		var hotbar = ToolHandlerBase.GetInt( args, "hotbar_size", 6 );

		var generated = new List<object>();

		// 🔄 [FORK] No PlayerController.cs — Sandbox.PlayerController built-in covers
		// movement + camera + animation. The composer prefab wires Renderer to the
		// Body's SkinnedModelRenderer so Citizen animates automatically.

		var netResult = CallTool( () => TemplatesTools.NetworkedPlayerTemplate( BuildArgs(
			("path", "Code/Player/NetworkedPlayer.cs"),
			("class_name", "NetworkedPlayer"),
			("namespace", ns)
		) ) );
		if ( netResult.error != null ) return netResult.error;
		generated.Add( new { template = "networked_player", path = "Code/Player/NetworkedPlayer.cs" } );

		var invResult = CallTool( () => CommunityTemplatesTools.InventorySkeletonTemplate( BuildArgs(
			("path", "Code/Inventory/PlayerInventory.cs"),
			("class_name", "PlayerInventory"),
			("namespace", ns + ".Inventory"),
			("width", invW),
			("height", invH),
			("hotbar_size", hotbar)
		) ) );
		if ( invResult.error != null ) return invResult.error;
		generated.Add( new { template = "inventory", path = "Code/Inventory/PlayerInventory.cs (+ UI/Hotbar razor + scss)" } );

		// 🔄 [FORK] No Grab + Interact components for Survival kind. The third-party CC0
		// Grab/Interact templates have their own raycast pipeline that competes with the
		// inventory's pickup/interaction layer (Phase 3 of the inventory roadmap). Survival
		// games will get a dedicated, inventory-aware InteractableComponent + WorldItem +
		// pickup/use flow as part of the inventory template itself. The standalone
		// `template_grab_component` / `template_interact_component` tools still exist for
		// non-inventory projects that want them.

		var sceneResult = CallTool( () => SceneTemplatesTools.MultiplayerBasicScene( BuildArgs(
			("path", $"scenes/{sceneName}.scene"),
			("spawn_radius", 256f),
			("open", false)
		) ) );
		if ( sceneResult.error != null ) return sceneResult.error;
		generated.Add( new { template = "multiplayer_basic", path = $"scenes/{sceneName}.scene" } );

		var prefabResult = WritePlayerPrefabAndWireScene(
			ComposerPlayerKind.Survival, ns, $"scenes/{sceneName}.scene", "prefabs/sboxpro_player.prefab",
			new ComposerOptions { IncludeGrab = false, IncludeInteract = false, StartInThirdPerson = true },
			generated );
		if ( prefabResult != null ) return prefabResult;

		return ToolHandlerBase.JsonResult( new
		{
			composer = "start_survival_game",
			project_kind = "Multiplayer survival with Tetris inventory",
			generated,
			requires_libraries = Array.Empty<string>(),   // SboxPro.Inventory is vendored — zero external deps
			next_steps = new[]
			{
				"Ensure both `local.sboxpro` AND `local.sboxpro_inventory` libraries are present in Libraries/.",
				"Run trigger_hotload to compile.",
				"Open the scene — Player template has Sandbox.PlayerController + Inventory holder + HUD (ScreenPanel with inventory + hotbar UI bound to the Holder) already attached.",
				"Author item assets via the Asset Browser → New → Weapon/Armor/Consumable/Material/Ammo/Tool/Blueprint; reference them in loot/spawner Components when those land.",
				"Pickup/interaction will be added by the inventory template itself (Phase 3) — not by composer-injected third-party Components."
			}
		} );
	}

	// ──────────────────────────────────────────────
	//  Helpers
	// ──────────────────────────────────────────────

	/// <summary>
	/// Ensures every package ident is locally installed before code generation runs.
	/// If autoInstall=true and a package is missing, calls Editor.AssetSystem.InstallAsync.
	/// Returns null on success; an error object if any package can't be satisfied.
	///
	/// This closes the gap between "generate code that uses Conna.Inventory.*" and
	/// "the Conna.Inventory assembly actually being on disk so that code compiles".
	/// </summary>
	private static async Task<object> EnsureDependenciesAsync( string[] idents, bool autoInstall )
	{
		var installed = new List<string>();
		var skipped = new List<string>();

		foreach ( var ident in idents )
		{
			if ( AssetSystem.IsCloudInstalled( ident ) )
			{
				skipped.Add( ident );
				continue;
			}

			if ( !autoInstall )
			{
				return ToolHandlerBase.ErrorResult(
					$"Required dependency '{ident}' is not installed and auto_install_deps is false. " +
					$"Set auto_install_deps=true, call install_asset manually, or add the package via the Package Manager UI before retrying."
				);
			}

			try
			{
				SboxProLog.Info( "Composer", $"Auto-installing dependency '{ident}'..." );
				await AssetSystem.InstallAsync( ident, skipIfInstalled: true, null, CancellationToken.None );
				installed.Add( ident );
			}
			catch ( Exception ex )
			{
				return ToolHandlerBase.ErrorResult(
					$"Failed to auto-install dependency '{ident}': {ex.Message}. " +
					$"Try installing it manually via the Package Manager UI, then re-run."
				);
			}
		}

		if ( installed.Count > 0 )
			SboxProLog.Info( "Composer", $"Dependencies ready: {installed.Count} installed, {skipped.Count} already present." );

		return null;
	}

	/// <summary>
	/// Builds the player prefab JSON, writes it to disk under Assets/{prefabPath},
	/// and injects an "in-scene template" copy into the previously-generated scene
	/// while wiring NetworkHelper.PlayerPrefab to that template's GUID.
	/// Returns null on success, or an error object suitable for the composer to
	/// return directly.
	/// </summary>
	private static object WritePlayerPrefabAndWireScene(
		ComposerPlayerKind kind,
		string ns,
		string scenePath,
		string prefabPath,
		ComposerOptions options,
		List<object> generated )
	{
		var build = ComposerPrefabBuilder.Build( kind, ns, options );

		// Resolve safe disk paths under Assets/
		var safePrefabPath = PathNormalizer.ResolveAssetPath( prefabPath );
		if ( safePrefabPath == null ) return ToolHandlerBase.ErrorResult( $"Invalid prefab path: {prefabPath}" );
		var safeScenePath = PathNormalizer.ResolveAssetPath( scenePath );
		if ( safeScenePath == null ) return ToolHandlerBase.ErrorResult( $"Invalid scene path: {scenePath}" );

		try
		{
			Directory.CreateDirectory( Path.GetDirectoryName( safePrefabPath ) );
			var prefabJson = build.PrefabFile.ToJsonString( new JsonSerializerOptions { WriteIndented = true } );
			File.WriteAllText( safePrefabPath, prefabJson );
			try { AssetSystem.RegisterFile( safePrefabPath ); } catch { } // #06
			generated.Add( new { template = "player_prefab", path = prefabPath } );
		}
		catch ( System.Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Failed to write player prefab: {ex.Message}" );
		}

		if ( !ComposerPrefabBuilder.InjectIntoScene( safeScenePath, build.InSceneGameObject, build.PlayerGuid, out var injectErr ) )
			return ToolHandlerBase.ErrorResult( injectErr );

		return null;
	}

	private record CallResult( object error );

	private static CallResult CallTool( System.Func<object> generator )
	{
		try
		{
			var result = generator();
			// Template tools return ErrorResult for failures (a wrapper with a specific shape);
			// surface it directly to abort the composer.
			if ( IsErrorResult( result ) )
				return new CallResult( result );
			return new CallResult( null );
		}
		catch ( System.Exception ex )
		{
			return new CallResult( ToolHandlerBase.ErrorResult( $"Composer step failed: {ex.Message}" ) );
		}
	}

	private static bool IsErrorResult( object result )
	{
		if ( result is null ) return false;
		var json = JsonSerializer.SerializeToElement( result );
		return json.ValueKind == JsonValueKind.Object
			&& json.TryGetProperty( "error", out _ );
	}

	private static JsonElement BuildArgs( params (string Key, object Value)[] kvs )
	{
		var obj = new JsonObject();
		foreach ( var (k, v) in kvs )
		{
			obj[k] = v switch
			{
				string s => JsonValue.Create( s ),
				int i => JsonValue.Create( i ),
				float f => JsonValue.Create( f ),
				double d => JsonValue.Create( d ),
				bool b => JsonValue.Create( b ),
				_ => JsonValue.Create( v?.ToString() )
			};
		}
		return JsonDocument.Parse( obj.ToJsonString() ).RootElement;
	}
}
