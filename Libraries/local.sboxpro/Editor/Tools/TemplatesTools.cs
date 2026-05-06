using System;
using System.IO;
using System.Text.Json;

namespace SboxPro;

public static class TemplatesTools
{
	// ──────────────────────────────────────────────
	//  template_player_controller
	// ──────────────────────────────────────────────

	[Tool( "template_player_controller", "Generate a complete first/third-person Player Controller: CharacterController-based movement + camera (FPS/TPS toggle), sprint, crouch (height ramp), jump, gravity. Body yaw follows camera. Includes a commented Shrimple swap section at the bottom for users who install ShrimpleCharacterController." )]
	[Param( "path", "Output path under Assets/ (e.g. 'Code/Player/PlayerController.cs').", Required = true )]
	[Param( "class_name", "Component class name. Default: derived from filename.", Required = false )]
	[Param( "namespace", "C# namespace. Default: 'Game'.", Required = false )]
	[Param( "walk_speed", "Walk speed. Default: 180.", Required = false, Type = "number" )]
	[Param( "run_speed", "Run/sprint speed. Default: 320.", Required = false, Type = "number" )]
	[Param( "crouch_speed", "Crouch speed. Default: 100.", Required = false, Type = "number" )]
	[Param( "jump_strength", "Jump impulse. Default: 320.", Required = false, Type = "number" )]
	[Param( "stand_height", "Standing CharacterController height. Default: 72.", Required = false, Type = "number" )]
	[Param( "crouch_height", "Crouched CharacterController height. Default: 40.", Required = false, Type = "number" )]
	public static object PlayerControllerTemplate( JsonElement args )
	{
		var path = ToolHandlerBase.RequireString( args, "path" );
		var ns = ToolHandlerBase.GetString( args, "namespace", "Game" );
		var className = ToolHandlerBase.GetString( args, "class_name" );
		var walk = ToolHandlerBase.GetFloat( args, "walk_speed", 180f );
		var run = ToolHandlerBase.GetFloat( args, "run_speed", 320f );
		var crouch = ToolHandlerBase.GetFloat( args, "crouch_speed", 100f );
		var jump = ToolHandlerBase.GetFloat( args, "jump_strength", 320f );
		var standH = ToolHandlerBase.GetFloat( args, "stand_height", 72f );
		var crouchH = ToolHandlerBase.GetFloat( args, "crouch_height", 40f );

		if ( !path.EndsWith( ".cs", StringComparison.OrdinalIgnoreCase ) )
			path += ".cs";
		if ( string.IsNullOrEmpty( className ) )
			className = Path.GetFileNameWithoutExtension( path );

		var content = $@"using System.Linq;
using Sandbox;

namespace {ns};

/// <summary>
/// First/third-person player. Drives a CharacterController, owns its camera transform,
/// and exposes movement state (sprinting/crouched/grounded) for other systems to consume.
/// </summary>
[Title( ""Player Controller"" )]
[Icon( ""nordic_walking"" )]
public sealed class {className} : Component
{{
	// --- Refs ---
	[Property] public CharacterController Controller {{ get; set; }}
	[Property] public CameraComponent Camera {{ get; set; }}
	[Property] public GameObject Body {{ get; set; }}        // optional: model to rotate with the camera yaw

	// --- Tuning ---
	[Property, Group( ""Speed"" ), Range( 0, 1000 )] public float WalkSpeed {{ get; set; }} = {Inv( walk )}f;
	[Property, Group( ""Speed"" ), Range( 0, 1500 )] public float RunSpeed {{ get; set; }} = {Inv( run )}f;
	[Property, Group( ""Speed"" ), Range( 0, 600 )] public float CrouchSpeed {{ get; set; }} = {Inv( crouch )}f;
	[Property, Group( ""Speed"" ), Range( 0, 1000 )] public float JumpStrength {{ get; set; }} = {Inv( jump )}f;
	[Property, Group( ""Speed"" )] public float Gravity {{ get; set; }} = 850f;

	[Property, Group( ""Stance"" ), Range( 32, 128 )] public float StandHeight {{ get; set; }} = {Inv( standH )}f;
	[Property, Group( ""Stance"" ), Range( 16, 96 )] public float CrouchHeight {{ get; set; }} = {Inv( crouchH )}f;
	[Property, Group( ""Stance"" )] public float StanceLerpSpeed {{ get; set; }} = 10f;

	[Property, Group( ""Camera"" )] public bool ThirdPerson {{ get; set; }} = false;
	[Property, Group( ""Camera"" )] public Vector3 EyeOffset {{ get; set; }} = new Vector3( 0, 0, 64 );
	[Property, Group( ""Camera"" )] public float ThirdPersonDistance {{ get; set; }} = 140f;
	[Property, Group( ""Camera"" )] public float MaxPitch {{ get; set; }} = 85f;
	[Property, Group( ""Input"" )] public string SprintAction {{ get; set; }} = ""Run"";
	[Property, Group( ""Input"" )] public string JumpAction {{ get; set; }} = ""Jump"";
	[Property, Group( ""Input"" )] public string CrouchAction {{ get; set; }} = ""Duck"";
	[Property, Group( ""Input"" )] public string ToggleViewAction {{ get; set; }} = ""View"";

	// --- State (read by other components) ---
	// EyeAngles is [Sync] so proxies see where the owner is aiming — needed for animation,
	// muzzle direction on remote weapons, and any third-person aim IK. Owner-authoritative.
	[Sync] public Angles EyeAngles {{ get; set; }}
	public bool IsSprinting {{ get; private set; }}
	public bool IsCrouching {{ get; private set; }}
	public bool IsOnGround => Controller?.IsOnGround ?? false;
	public Vector3 Velocity => Controller?.Velocity ?? Vector3.Zero;
	public Vector3 EyePosition => WorldPosition + Vector3.Up * (Controller?.Height ?? StandHeight) + EyeOffset.WithZ( 0 );
	public Rotation EyeRotation => EyeAngles.ToRotation();

	private float _currentHeight;

	protected override void OnStart()
	{{
		// GetOrAddComponent is the canonical ""on this GameObject"" helper; Components.GetOrCreate
		// requires a FindMode arg and is meant for ancestor/descendant searches.
		Controller ??= GameObject.GetOrAddComponent<CharacterController>();
		Camera ??= Scene.GetAllComponents<CameraComponent>().FirstOrDefault( c => c.IsMainCamera );

		Controller.Height = StandHeight;
		_currentHeight = StandHeight;

		// Snap eyes to current world rotation so we don't yank on first frame
		EyeAngles = WorldRotation.Angles().WithRoll( 0 );
	}}

	protected override void OnUpdate()
	{{
		// Frame-rate-dependent: input sampling and camera tracking.
		// Smoother visuals than pinning to the fixed update tick.
		if ( IsProxy ) return;
		HandleInput();
		HandleCamera();
		HandleBody();
	}}

	protected override void OnFixedUpdate()
	{{
		// Deterministic, fixed-timestep: physics-adjacent state changes
		// belong here so behaviour is identical regardless of FPS.
		if ( Controller is null ) return;
		if ( IsProxy ) return;
		HandleStance();
		HandleMovement();
	}}

	private void HandleInput()
	{{
		// Look — mouse / right stick
		var look = Input.AnalogLook;
		var ang = EyeAngles + look;
		ang.pitch = ang.pitch.Clamp( -MaxPitch, MaxPitch );
		ang.roll = 0f;
		EyeAngles = ang;

		// Toggle view
		if ( !string.IsNullOrEmpty( ToggleViewAction ) && Input.Pressed( ToggleViewAction ) )
			ThirdPerson = !ThirdPerson;
	}}

	private void HandleStance()
	{{
		IsCrouching = !string.IsNullOrEmpty( CrouchAction ) && Input.Down( CrouchAction );
		var targetHeight = IsCrouching ? CrouchHeight : StandHeight;
		_currentHeight = MathX.Lerp( _currentHeight, targetHeight, Time.Delta * StanceLerpSpeed );
		Controller.Height = _currentHeight;
	}}

	private void HandleMovement()
	{{
		var wish = Input.AnalogMove;
		var yaw = Rotation.FromYaw( EyeAngles.yaw );
		var wishDir = (yaw.Forward * wish.x + yaw.Right * -wish.y).WithZ( 0 ).Normal;

		IsSprinting = !IsCrouching && wish.Length > 0.1f && !string.IsNullOrEmpty( SprintAction ) && Input.Down( SprintAction );
		var speed = IsCrouching ? CrouchSpeed : (IsSprinting ? RunSpeed : WalkSpeed);
		var wishVel = wishDir * speed;

		if ( Controller.IsOnGround )
		{{
			Controller.Velocity = Controller.Velocity.WithZ( 0 );
			Controller.Accelerate( wishVel );
			Controller.ApplyFriction( 4f, 100f );

			if ( !string.IsNullOrEmpty( JumpAction ) && Input.Pressed( JumpAction ) && !IsCrouching )
				Controller.Punch( Vector3.Up * JumpStrength );
		}}
		else
		{{
			Controller.Velocity += Vector3.Down * Gravity * Time.Delta;
			Controller.Accelerate( wishVel.WithZ( 0 ) * 0.2f );
		}}

		Controller.Move();
	}}

	private void HandleCamera()
	{{
		if ( !Camera.IsValid() ) return;

		Camera.WorldRotation = EyeRotation;
		var basePos = WorldPosition + Vector3.Up * _currentHeight + EyeOffset.WithZ( 0 );

		if ( ThirdPerson )
		{{
			var trace = Scene.Trace.Ray( basePos, basePos - EyeRotation.Forward * ThirdPersonDistance )
				.IgnoreGameObjectHierarchy( GameObject )
				.Radius( 8f )
				.Run();
			Camera.WorldPosition = trace.Hit ? trace.HitPosition : trace.EndPosition;
		}}
		else
		{{
			Camera.WorldPosition = basePos;
		}}
	}}

	private void HandleBody()
	{{
		if ( !Body.IsValid() ) return;
		// Body keeps its own pitch/roll; only yaw follows the camera
		Body.WorldRotation = Rotation.FromYaw( EyeAngles.yaw );
	}}
}}

// ╔══════════════════════════════════════════════════════════════════════════════════════╗
// ║  SHRIMPLE INTEGRATION — uncomment when fish.scc (shrimple_character_controller)        ║
// ║  is installed via Asset Browser. Replaces CharacterController with the Shrimple        ║
// ║  collide-and-slide controller for better stairs, slopes, and tight spaces.             ║
// ╚══════════════════════════════════════════════════════════════════════════════════════╝
//
// using ShrimpleCharacterController;
//
// // 1) Replace the [Property] CharacterController Controller field above with:
// //    [Property] public ShrimpleCharacterController ShrimpleController {{ get; set; }}
// //
// // 2) Replace OnStart() body with:
// //    ShrimpleController ??= GameObject.GetOrAddComponent<ShrimpleCharacterController>();
// //    ShrimpleController.TraceHeight = StandHeight;
// //
// // 3) Replace HandleStance Controller.Height assignment with:
// //    ShrimpleController.TraceHeight = _currentHeight;
// //
// // 4) Replace HandleMovement with:
// //    ShrimpleController.WishVelocity = wishVel;
// //    if ( !string.IsNullOrEmpty( JumpAction ) && Input.Pressed( JumpAction ) && ShrimpleController.IsOnGround && !IsCrouching )
// //        ShrimpleController.Punch( Vector3.Up * JumpStrength );
// //    // ShrimpleController.Move() runs automatically each fixed update unless ManuallyUpdate=true
// //
// // 5) Update IsOnGround / Velocity getters to read from ShrimpleController.
";

		return WriteTemplate( path, content, className, "player_controller" );
	}

	// ──────────────────────────────────────────────
	//  template_networked_player
	// ──────────────────────────────────────────────

	[Tool( "template_networked_player", "Generate a networked player Component with [Sync] state (name/health) and an [Rpc.Broadcast] damage method." )]
	[Param( "path", "Output path (e.g. 'Code/Player/NetworkedPlayer.cs').", Required = true )]
	[Param( "class_name", "Component class name. Default: derived from filename.", Required = false )]
	[Param( "namespace", "C# namespace. Default: 'Game'.", Required = false )]
	[Param( "max_health", "Initial/max health value. Default: 100.", Required = false, Type = "integer" )]
	public static object NetworkedPlayerTemplate( JsonElement args )
	{
		var path = ToolHandlerBase.RequireString( args, "path" );
		var ns = ToolHandlerBase.GetString( args, "namespace", "Game" );
		var className = ToolHandlerBase.GetString( args, "class_name" );
		var maxHp = ToolHandlerBase.GetInt( args, "max_health", 100 );

		if ( !path.EndsWith( ".cs", StringComparison.OrdinalIgnoreCase ) )
			path += ".cs";
		if ( string.IsNullOrEmpty( className ) )
			className = Path.GetFileNameWithoutExtension( path );

		var content = $@"using System;
using Sandbox;

namespace {ns};

/// <summary>
/// Networked player state. Health is host-authoritative — anyone (including the owner)
/// requests damage/heal via RPC, the host validates and applies, and the new value
/// replicates to every client through [Sync(SyncFlags.FromHost)].
/// </summary>
public sealed class {className} : Component
{{
	[Sync] public string PlayerName {{ get; set; }} = ""Player"";
	// FromHost: only the host machine writes Health; everyone else (including owner)
	// observes the replicated value. Prevents a malicious client from refusing damage.
	[Sync( SyncFlags.FromHost )] public int Health {{ get; set; }} = {maxHp};
	[Property] public int MaxHealth {{ get; set; }} = {maxHp};

	public bool IsAlive => Health > 0;

	protected override void OnStart()
	{{
		if ( !Network.Active ) return;
		if ( Network.IsOwner )
		{{
			PlayerName = Network.Owner?.DisplayName ?? ""Anonymous"";
		}}
		if ( Networking.IsHost )
		{{
			// Host owns Health writes — initialise here so all clients see the same start value.
			Health = MaxHealth;
		}}
	}}

	/// <summary>Request damage. Routes to host; host applies and the value replicates to all clients.</summary>
	[Rpc.Host]
	public void TakeDamage( int amount, Guid attackerId )
	{{
		if ( !Networking.IsHost ) return;
		if ( amount <= 0 ) return;
		Health = Math.Max( 0, Health - amount );
		if ( Health <= 0 ) OnDeath( attackerId );
	}}

	/// <summary>Request heal. Host-authoritative; clamped at MaxHealth on the host.</summary>
	[Rpc.Host]
	public void Heal( int amount )
	{{
		if ( !Networking.IsHost ) return;
		if ( amount <= 0 ) return;
		Health = Math.Min( MaxHealth, Health + amount );
	}}

	private void OnDeath( Guid attackerId )
	{{
		Log.Info( $""{{PlayerName}} was killed (attacker={{attackerId}})"" );
	}}
}}
";

		return WriteTemplate( path, content, className, "networked_player" );
	}

	// ──────────────────────────────────────────────
	//  template_trigger_zone
	// ──────────────────────────────────────────────

	[Tool( "template_trigger_zone", "Generate a TriggerZone Component that wires Collider OnTriggerEnter/Exit with optional tag filtering." )]
	[Param( "path", "Output path (e.g. 'Code/World/TriggerZone.cs').", Required = true )]
	[Param( "class_name", "Component class name. Default: derived from filename.", Required = false )]
	[Param( "namespace", "C# namespace. Default: 'Game'.", Required = false )]
	[Param( "default_tag_filter", "Default required tag for trigger to fire (empty = any).", Required = false )]
	public static object TriggerZoneTemplate( JsonElement args )
	{
		var path = ToolHandlerBase.RequireString( args, "path" );
		var ns = ToolHandlerBase.GetString( args, "namespace", "Game" );
		var className = ToolHandlerBase.GetString( args, "class_name" );
		var defaultTag = ToolHandlerBase.GetString( args, "default_tag_filter", "" );

		if ( !path.EndsWith( ".cs", StringComparison.OrdinalIgnoreCase ) )
			path += ".cs";
		if ( string.IsNullOrEmpty( className ) )
			className = Path.GetFileNameWithoutExtension( path );

		var content = $@"using Sandbox;
using System;

namespace {ns};

/// <summary>
/// Tag-filtered trigger zone. Implements <see cref=""Component.ITriggerListener""/> directly
/// (the skill-canonical pattern) — engine routes overlap events to this method when a
/// Collider on the same or child GameObject has IsTrigger = true.
///
/// Subscribe to Entered / Exited events from other components. FireOnce gates Entered to a
/// single invocation per enable cycle. RequiredTag (empty = any) filters by GameObject tag.
/// </summary>
public sealed class {className} : Component, Component.ITriggerListener
{{
	[Property] public string RequiredTag {{ get; set; }} = ""{defaultTag}"";
	[Property] public bool FireOnce {{ get; set; }} = false;
	[Property] public bool AutoConfigureCollider {{ get; set; }} = true;

	// Plain C# events (not [Property]) — delegate properties don't serialise meaningfully
	// in the inspector; consumers wire up subscriptions in code via OnEntered += ....
	public event Action<GameObject> Entered;
	public event Action<GameObject> Exited;

	private bool _fired;

	protected override void OnEnabled()
	{{
		_fired = false;
		if ( !AutoConfigureCollider ) return;
		// Convenience: if the user dropped this on a GameObject with a Collider but forgot
		// to mark it as trigger, do it for them. Nothing to do if there is no Collider.
		var collider = GetComponent<Collider>();
		if ( collider is not null ) collider.IsTrigger = true;
	}}

	void Component.ITriggerListener.OnTriggerEnter( Collider other )
	{{
		var go = other?.GameObject;
		if ( go is null ) return;
		if ( _fired && FireOnce ) return;
		if ( !string.IsNullOrEmpty( RequiredTag ) && !go.Tags.Has( RequiredTag ) ) return;
		_fired = true;
		Entered?.Invoke( go );
	}}

	void Component.ITriggerListener.OnTriggerExit( Collider other )
	{{
		var go = other?.GameObject;
		if ( go is null ) return;
		if ( !string.IsNullOrEmpty( RequiredTag ) && !go.Tags.Has( RequiredTag ) ) return;
		Exited?.Invoke( go );
	}}
}}
";

		return WriteTemplate( path, content, className, "trigger_zone" );
	}

	// ──────────────────────────────────────────────
	//  Helpers
	// ──────────────────────────────────────────────

	private static object WriteTemplate( string path, string content, string className, string templateName )
	{
		var safe = PathNormalizer.ResolveAssetPath( path );
		if ( safe == null )
			return ToolHandlerBase.ErrorResult( $"Path outside project: {path}" );

		if ( File.Exists( safe ) )
			return ToolHandlerBase.ErrorResult( $"File already exists: {PathNormalizer.ToRelative( safe )}" );

		try
		{
			var dir = Path.GetDirectoryName( safe );
			if ( !string.IsNullOrEmpty( dir ) ) Directory.CreateDirectory( dir );
			File.WriteAllText( safe, content );

			return ToolHandlerBase.JsonResult( new
			{
				generated = true,
				template = templateName,
				path = PathNormalizer.ToRelative( safe ),
				className,
				note = "Run trigger_hotload to compile + register the new Component."
			} );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Failed to write template: {ex.Message}" );
		}
	}

	private static string Inv( float v ) => v.ToString( System.Globalization.CultureInfo.InvariantCulture );
}
