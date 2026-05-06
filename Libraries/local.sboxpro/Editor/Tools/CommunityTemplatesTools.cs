using System;
using System.IO;
using System.Text.Json;

namespace SboxPro;

// Internal-use community-derived templates.
public static class CommunityTemplatesTools
{
	// ──────────────────────────────────────────────
	// template_grab_component (sbox-simplecomponents, CC0)
	// ──────────────────────────────────────────────

	[Tool( "template_grab_component", "Generate a physics-grab Component (raycast pickup, hold + rotate).." )]
	[Param( "path", "Output path (e.g. 'Code/Player/PlayerGrab.cs').", Required = true )]
	[Param( "class_name", "Component class name. Default: derived from filename.", Required = false )]
	[Param( "namespace", "C# namespace. Default: 'Game'.", Required = false )]
	public static object GrabComponentTemplate( JsonElement args )
	{
		var (path, ns, className) = ResolvePath( args );
		if ( path == null ) return ToolHandlerBase.ErrorResult( "Invalid path." );
		if ( File.Exists( path ) ) return ToolHandlerBase.ErrorResult( $"File already exists: {PathNormalizer.ToRelative( path )}" );

		var content = $$"""
			using System;
			using Sandbox;

			namespace {{ns}};

			public sealed class {{className}} : Component
			{
				[Property] public string GrabActionName { get; set; } = "Attack1";
				[Property] public string RotationActionName { get; set; } = "reload";
				[Property] public string TagName { get; set; } = "grab";
				[Property] public float RayLength { get; set; } = 125.0f;
				[Property, MinMax( 0f, 1f )] public float RotationSpeedFactor { get; set; } = 0.5f;
				[Property] public float MinRotationMassFactor { get; set; } = 0.1f;

				public event Action<GameObject> OnObjectGrab;
				public event Action<GameObject> OnObjectRelease;
				public event Action<GameObject> OnObjectRotate;
				public bool IsHoldingObject;

				private GameObject _grabbedObject;
				private Rigidbody _grabbedBody;
				private CameraComponent _camera;
				// Use Sandbox.PlayerController explicitly — composer-generated Game.PlayerController
				// is a different type without UseLookControls. Sandbox's built-in is what
				// the simple-components grab pattern expects. UseCameraControls was removed
				// from the Sandbox API entirely (issue #11) — only UseLookControls survives.
				private Sandbox.PlayerController _playerController;
				private SceneTraceResult _trace;
				private Rotation _targetRotation;
				private bool _isRotating;
				private float _grabDistance;

				protected override void OnStart()
				{
					_playerController = GetComponentInChildren<Sandbox.PlayerController>();
					_camera = Scene.Camera ?? GetComponentInChildren<CameraComponent>();
					if ( _camera == null ) Log.Error( "[Grab] CameraComponent not found" );
				}

				protected override void OnUpdate()
				{
					// Owner-only: input drives grab logic; proxies just see the held body's
					// transform replicate via Rigidbody networking. Without this guard
					// every client would try to drive the same body and they'd fight.
					if ( IsProxy ) return;
					if ( _camera is null ) return;

					if ( Input.Pressed( GrabActionName ) && !IsHoldingObject )
					{
						_trace = CastRay();
						if ( _trace.Hit && _trace.GameObject.Tags.Has( TagName ) )
							GrabObject( _trace.GameObject );
					}

					if ( Input.Released( GrabActionName ) && IsHoldingObject )
						ReleaseObject();

					_isRotating = Input.Down( RotationActionName );

					if ( IsHoldingObject && _isRotating && _grabbedBody.IsValid() )
					{
						if ( _playerController is not null ) _playerController.UseLookControls = false;
						float mass = _grabbedBody.Mass.Clamp( 1f, 50f );
						float massFactor = MathF.Max( 1f / MathF.Sqrt( mass ), MinRotationMassFactor );
						float yaw = Input.MouseDelta.x * RotationSpeedFactor * massFactor;
						float pitch = Input.MouseDelta.y * RotationSpeedFactor * massFactor;
						var camRot = _camera.WorldRotation;
						_targetRotation = Rotation.FromAxis( camRot.Up, yaw ) *
										 Rotation.FromAxis( camRot.Right, pitch ) *
										 _targetRotation;
						OnObjectRotate?.Invoke( _grabbedObject );
					}

					if ( IsHoldingObject ) UpdateGrabbedObjectPosition();
					if ( !_isRotating && _playerController is not null && !_playerController.UseLookControls )
						_playerController.UseLookControls = true;
				}

				private void GrabObject( GameObject go )
				{
					var body = go.Components.Get<Rigidbody>();
					if ( body == null ) { Log.Warning( "[Grab] target has no Rigidbody" ); return; }
					_grabbedObject = go;
					_grabbedBody = body;
					_grabDistance = Vector3.DistanceBetween( _camera.WorldPosition, go.WorldPosition );
					_targetRotation = go.WorldRotation;
					IsHoldingObject = true;
					OnObjectGrab?.Invoke( go );
				}

				private void ReleaseObject()
				{
					OnObjectRelease?.Invoke( _grabbedObject );
					IsHoldingObject = false;
					_grabbedObject = null;
					_grabbedBody = null;
					_trace = new SceneTraceResult();
				}

				private void UpdateGrabbedObjectPosition()
				{
					if ( !_grabbedBody.IsValid() ) { ReleaseObject(); return; }
					var targetPos = _camera.WorldPosition + _camera.WorldRotation.Forward * _grabDistance;
					float massFactor = _grabbedBody.Mass.Clamp( 1f, 50f );
					float arriveTime = 0.05f + (massFactor * 0.01f);
					_grabbedBody.SmoothMove( new Transform( targetPos, _targetRotation ), arriveTime, Time.Delta );
				}

				private SceneTraceResult CastRay()
				{
					// Canonical aim ray — ScreenPixelToRay returns a ray from the camera through
					// the screen point in world space; far cleaner than reconstructing it from
					// ScreenToWorld + camera position.
					var aim = _camera.ScreenPixelToRay( Screen.Size * 0.5f );
					var start = aim.Position;
					var end = start + aim.Forward * RayLength;
					return Scene.Trace.Ray( start, end ).IgnoreGameObjectHierarchy( GameObject ).UseHitboxes().Run();
				}
			}
			""";

		return WriteTemplate( path, content, className, "grab_component" );
	}

	// ──────────────────────────────────────────────
	// template_interact_component (sbox-simplecomponents, CC0)
	// ──────────────────────────────────────────────

	[Tool( "template_interact_component", "Generate a raycast interact Component (cooldown + tag-filtered events).." )]
	[Param( "path", "Output path (e.g. 'Code/Player/PlayerInteract.cs').", Required = true )]
	[Param( "class_name", "Component class name. Default: derived from filename.", Required = false )]
	[Param( "namespace", "C# namespace. Default: 'Game'.", Required = false )]
	public static object InteractComponentTemplate( JsonElement args )
	{
		var (path, ns, className) = ResolvePath( args );
		if ( path == null ) return ToolHandlerBase.ErrorResult( "Invalid path." );
		if ( File.Exists( path ) ) return ToolHandlerBase.ErrorResult( $"File already exists: {PathNormalizer.ToRelative( path )}" );

		var content = $$"""
			using System;
			using Sandbox;

			namespace {{ns}};

			public sealed class {{className}} : Component
			{
				[Property] public string ActionName { get; set; } = "Use";
				[Property] public string TagName { get; set; } = "interact";
				[Property] public float RayLength { get; set; } = 125.0f;
				[Property] public float InteractCooldown { get; set; } = 2.0f;

				public event Action<SceneTraceResult> OnInteract;
				public event Action<SceneTraceResult> OnCanInteract;
				public event Action<SceneTraceResult> OnCanInteractEnd;

				private CameraComponent _camera;
				private TimeSince _timeSince;
				private bool _prevCanInteract;

				protected override void OnStart()
				{
					// Camera resolution: prefer Scene.Camera (active), fall back to nested
					// CameraComponent. The old code checked Sandbox.PlayerController.UseCameraControls
					// to decide which to use, but that property was removed from the engine API
					// (issue #11). Scene.Camera is the canonical "current view" reference.
					_camera = Scene.Camera ?? GetComponentInChildren<CameraComponent>();
					if ( _camera == null ) Log.Error( "[Interact] CameraComponent not found" );
					_timeSince = InteractCooldown;
				}

				protected override void OnUpdate()
				{
					// Input + UI hover state are owner-only — proxies don't have local screens.
					if ( IsProxy ) return;
					if ( _camera is null ) return;

					var trace = CastRay();
					bool canInteract = _timeSince > InteractCooldown && trace.Hit && trace.GameObject.Tags.Has( TagName );

					if ( canInteract != _prevCanInteract )
					{
						if ( canInteract ) OnCanInteract?.Invoke( trace );
						else OnCanInteractEnd?.Invoke( trace );
						_prevCanInteract = canInteract;
					}

					if ( canInteract && Input.Released( ActionName ) )
					{
						_timeSince = 0;
						OnInteract?.Invoke( trace );
					}
				}

				private SceneTraceResult CastRay()
				{
					var aim = _camera.ScreenPixelToRay( Screen.Size * 0.5f );
					var start = aim.Position;
					var end = start + aim.Forward * RayLength;
					return Scene.Trace.Ray( start, end ).IgnoreGameObjectHierarchy( GameObject ).Run();
				}
			}
			""";

		return WriteTemplate( path, content, className, "interact_component" );
	}

	// ──────────────────────────────────────────────
	// template_zoom_component (sbox-simplecomponents, CC0)
	// ──────────────────────────────────────────────

	[Tool( "template_zoom_component", "Generate a FOV-based zoom Component for first/third-person cycling.." )]
	[Param( "path", "Output path (e.g. 'Code/Player/ZoomComponent.cs').", Required = true )]
	[Param( "class_name", "Component class name. Default: derived from filename.", Required = false )]
	[Param( "namespace", "C# namespace. Default: 'Game'.", Required = false )]
	public static object ZoomComponentTemplate( JsonElement args )
	{
		var (path, ns, className) = ResolvePath( args );
		if ( path == null ) return ToolHandlerBase.ErrorResult( "Invalid path." );
		if ( File.Exists( path ) ) return ToolHandlerBase.ErrorResult( $"File already exists: {PathNormalizer.ToRelative( path )}" );

		var content = $$"""
			using System;
			using System.Linq;
			using Sandbox;

			namespace {{ns}};

			[Title( "Zoom Component" )]
			public sealed class {{className}} : Component
			{
				public enum FovState { FirstPerson, FirstToThird, ThirdPerson, ThirdToFirst }

				[Property, ReadOnly] public FovState State { get; set; } = FovState.FirstPerson;
				[Property] public string ZoomInActionName { get; set; } = "Zoom In";
				[Property] public string ZoomOutActionName { get; set; } = "Zoom Out";
				[Property] public string ChangeViewActionName { get; set; } = "view";
				[Property] public float ZoomStep { get; set; } = 3f;
				[Property] public float MinFov { get; set; } = 30f;
				[Property] public float MaxFov { get; set; } = 120f;

				private float _baseFov;
				private float _targetFovDiff;
				private bool _canZoom = true;
				private PlayerController _player;
				private CameraComponent _camera;

				protected override void OnStart()
				{
					_player = GetComponent<PlayerController>();
					_camera = Scene.GetAllComponents<CameraComponent>().FirstOrDefault( c => c.Enabled );
					if ( _camera != null ) _baseFov = _camera.FieldOfView;
				}

				protected override void OnUpdate()
				{
					// Owner-only: zoom mutates the local camera FOV. On a proxy this would
					// reach across and tamper with the local player's view.
					if ( IsProxy ) return;
					if ( _player == null || _camera == null ) return;

					if ( Input.Down( ChangeViewActionName ) )
						State = _player.ThirdPerson ? FovState.ThirdPerson : FovState.FirstPerson;

					if ( !_canZoom ) return;

					switch ( State )
					{
						case FovState.ThirdPerson:
							float input = 0;
							if ( Input.Down( ZoomInActionName ) ) input -= ZoomStep;
							if ( Input.Down( ZoomOutActionName ) ) input += ZoomStep;
							_targetFovDiff += input * ZoomStep;
							_targetFovDiff = Math.Clamp( _targetFovDiff, MinFov - _baseFov - 0.0001f, MaxFov - _baseFov );
							var target = _baseFov + _targetFovDiff;
							if ( target < MinFov ) Switch( FovState.ThirdToFirst );
							else _camera.FieldOfView = target;
							break;
						case FovState.FirstPerson:
							if ( Input.Down( ZoomOutActionName ) ) Switch( FovState.FirstToThird );
							break;
					}
				}

				private void Switch( FovState target )
				{
					_canZoom = false;
					switch ( target )
					{
						case FovState.FirstToThird:
							_targetFovDiff = MinFov - _baseFov;
							_camera.FieldOfView = MinFov;
							_player.ThirdPerson = true;
							State = FovState.ThirdPerson;
							break;
						case FovState.ThirdToFirst:
							_targetFovDiff = 0;
							_camera.FieldOfView = _baseFov;
							_player.ThirdPerson = false;
							State = FovState.FirstPerson;
							break;
					}
					_canZoom = true;
				}
			}
			""";

		return WriteTemplate( path, content, className, "zoom_component" );
	}

	// ──────────────────────────────────────────────
	// template_net_cooldown (netkit, MIT)
	// ──────────────────────────────────────────────

	[Tool( "template_net_cooldown", "Generate a NetCooldown GameObjectSystem for host-validated, client-replicated cooldowns by string key.." )]
	[Param( "path", "Output path (e.g. 'Code/Network/NetCooldown.cs').", Required = true )]
	[Param( "class_name", "System class name. Default: derived from filename.", Required = false )]
	[Param( "namespace", "C# namespace. Default: 'Game'.", Required = false )]
	public static object NetCooldownTemplate( JsonElement args )
	{
		var (path, ns, className) = ResolvePath( args );
		if ( path == null ) return ToolHandlerBase.ErrorResult( "Invalid path." );
		if ( File.Exists( path ) ) return ToolHandlerBase.ErrorResult( $"File already exists: {PathNormalizer.ToRelative( path )}" );

		var content = $$"""
			using System;
			using System.Collections.Generic;
			using Sandbox;

			namespace {{ns}};

			/// <summary>
			/// Host-validated, client-replicated cooldowns by string key.
			/// Host calls Start(connection, key, duration); the RPC fires on the target client; both sides
			/// share a deadline. Other code calls IsReady(key) / Remaining(key) on the client and
			/// IsActive(connection, key) on the host.
			/// </summary>
			[Title( "Net Cooldown" )]
			public sealed class {{className}} : GameObjectSystem<{{className}}>
			{
				private static readonly Dictionary<(ulong steamId, string key), (RealTimeSince started, float duration)> _host = new();
				private static readonly Dictionary<string, (RealTimeSince started, float duration)> _client = new();

				// Reused scratch lists for expired-key sweeps. The previous LINQ chains allocated
				// a fresh List every Tick — this runs every frame on every client; the GC pressure
				// shows up at scale.
				private static readonly List<string> _clientScratch = new();
				private static readonly List<(ulong, string)> _hostScratch = new();

				public static event Action<string, float> OnStarted;
				public static event Action<string> OnExpired;

				private RealTimeSince _lastCleanup;

				public {{className}}( Scene scene ) : base( scene )
				{
					// New scene = fresh cooldown universe. Without this, dictionaries persist across
					// scene loads and ghost keys silently shorten newly-started cooldowns.
					_host.Clear();
					_client.Clear();
					Listen( Stage.StartUpdate, 0, Tick, "{{className}}.Tick" );
				}

				public static void Start( Connection connection, string key, float duration )
				{
					if ( !Networking.IsHost || connection == null ) return;
					_host[(connection.SteamId, key)] = (0, duration);
					using ( Rpc.FilterInclude( connection ) )
						RpcReceive( key, duration );
				}

				public static bool IsActive( Connection connection, string key )
					=> connection != null && _host.TryGetValue( (connection.SteamId, key), out var cd ) && cd.started < cd.duration;

				public static float HostRemaining( Connection connection, string key )
					=> connection != null && _host.TryGetValue( (connection.SteamId, key), out var cd ) ? MathF.Max( 0f, cd.duration - cd.started ) : 0f;

				public static float Remaining( string key )
					=> _client.TryGetValue( key, out var cd ) ? MathF.Max( 0f, cd.duration - cd.started ) : 0f;

				public static bool IsReady( string key ) => Remaining( key ) <= 0f;

				private void Tick()
				{
					_clientScratch.Clear();
					foreach ( var kv in _client )
						if ( kv.Value.started >= kv.Value.duration ) _clientScratch.Add( kv.Key );
					for ( int i = 0; i < _clientScratch.Count; i++ )
					{
						var k = _clientScratch[i];
						_client.Remove( k );
						OnExpired?.Invoke( k );
					}

					if ( Networking.IsHost && _lastCleanup > 60f )
					{
						_lastCleanup = 0;
						_hostScratch.Clear();
						foreach ( var kv in _host )
							if ( kv.Value.started >= kv.Value.duration ) _hostScratch.Add( kv.Key );
						for ( int i = 0; i < _hostScratch.Count; i++ )
							_host.Remove( _hostScratch[i] );
					}
				}

				[Rpc.Owner( NetFlags.Reliable )]
				private static void RpcReceive( string key, float duration )
				{
					_client[key] = (0, duration);
					OnStarted?.Invoke( key, duration );
				}
			}
			""";

		return WriteTemplate( path, content, className, "net_cooldown" );
	}

	// ──────────────────────────────────────────────
	// template_net_visibility (netkit, MIT)
	// ──────────────────────────────────────────────

	[Tool( "template_net_visibility", "Generate a NetVisibility Component implementing INetworkVisible (Always/Never/OwnerOnly/Distance/Custom modes).." )]
	[Param( "path", "Output path (e.g. 'Code/Network/NetVisibility.cs').", Required = true )]
	[Param( "class_name", "Component class name. Default: derived from filename.", Required = false )]
	[Param( "namespace", "C# namespace. Default: 'Game'.", Required = false )]
	public static object NetVisibilityTemplate( JsonElement args )
	{
		var (path, ns, className) = ResolvePath( args );
		if ( path == null ) return ToolHandlerBase.ErrorResult( "Invalid path." );
		if ( File.Exists( path ) ) return ToolHandlerBase.ErrorResult( $"File already exists: {PathNormalizer.ToRelative( path )}" );

		var content = $$"""
			using System;
			using Sandbox;

			namespace {{ns}};

			public enum {{className}}Mode { Always, Never, OwnerOnly, Distance, Custom }

			/// <summary>
			/// Per-object network visibility filter. Implements <see cref="Component.INetworkVisible"/>;
			/// the engine asks once per (object, connection) pair whether to transmit. With the
			/// Distance mode, uses <see cref="Connection.DistanceSquared"/> directly — the previous
			/// implementation walked the entire scene per-call to find an owned GameObject, which
			/// is O(N) in objects, called for every networked object × every connection.
			/// </summary>
			[Title( "Net Visibility" )]
			[Icon( "visibility" )]
			public sealed class {{className}} : Component, Component.INetworkVisible
			{
				[Property] public {{className}}Mode Mode { get; set; } = {{className}}Mode.Always;
				[Property] public float MaxDistance { get; set; } = 1000f;
				public Func<Connection, bool> Filter { get; set; }

				bool Component.INetworkVisible.IsVisibleToConnection( Connection connection, in BBox bounds )
				{
					return Mode switch
					{
						{{className}}Mode.Always => true,
						{{className}}Mode.Never => false,
						{{className}}Mode.OwnerOnly => connection != null && connection.Id == Network.Owner?.Id,
						{{className}}Mode.Distance => IsWithinDistance( connection ),
						{{className}}Mode.Custom => Filter?.Invoke( connection ) ?? true,
						_ => true
					};
				}

				private bool IsWithinDistance( Connection connection )
				{
					if ( connection == null ) return true;
					// Connection.DistanceSquared returns squared distance to a world position the
					// engine tracks for the connection (camera/pawn). Compare against squared max
					// to skip the sqrt.
					var maxSq = MaxDistance * MaxDistance;
					return connection.DistanceSquared( WorldPosition ) <= maxSq;
				}
			}
			""";

		return WriteTemplate( path, content, className, "net_visibility" );
	}

	// ──────────────────────────────────────────────
	// template_dresser (dresser-plus, CC0)
	// ──────────────────────────────────────────────

	[Tool( "template_dresser", "Generate a Citizen/Human dresser Component with networked clothing/height/age/tint and workshop clothing support.." )]
	[Param( "path", "Output path (e.g. 'Code/Player/Dresser.cs').", Required = true )]
	[Param( "class_name", "Component class name. Default: derived from filename.", Required = false )]
	[Param( "namespace", "C# namespace. Default: 'Sandbox'.", Required = false )]
	public static object DresserTemplate( JsonElement args )
	{
		var path = ToolHandlerBase.RequireString( args, "path" );
		var ns = ToolHandlerBase.GetString( args, "namespace", "Sandbox" );
		var className = ToolHandlerBase.GetString( args, "class_name" );
		if ( !path.EndsWith( ".cs", StringComparison.OrdinalIgnoreCase ) ) path += ".cs";
		if ( string.IsNullOrEmpty( className ) ) className = Path.GetFileNameWithoutExtension( path );
		var safe = PathNormalizer.ResolveAssetPath( path );
		if ( safe == null ) return ToolHandlerBase.ErrorResult( $"Invalid path: {path}" );
		if ( File.Exists( safe ) ) return ToolHandlerBase.ErrorResult( $"File already exists: {PathNormalizer.ToRelative( safe )}" );

		var content = $$"""
			using System.Collections.Generic;
			using System.Linq;
			using System.Threading;
			using System.Threading.Tasks;
			using Sandbox;

			namespace {{ns}};

			[Title( "Dresser" )]
			[Category( "Game" )]
			[Icon( "checkroom" )]
			public sealed class {{className}} : Component, Component.ExecuteInEditor
			{
				public enum ClothingSource { Manual, LocalUser, OwnerUser, Hybrid }

				[Property] public SkinnedModelRenderer BodyTarget { get; set; }
				[Property] public ClothingSource Source { get; set; }
				[Property, Group( "Parameters" )] public bool RemoveUnownedItems { get; set; } = true;
				[Property, Group( "Parameters" )] public bool ApplyHeightScale { get; set; } = true;
				[Property, Group( "Parameters" ), Range( 0.8f, 1.2f )] [Sync] public float ManualHeight { get; set; } = 1f;
				[Property, Group( "Parameters" ), Range( 0, 1 )] [Sync] public float ManualAge { get; set; } = 0.5f;
				[Property, Group( "Parameters" ), Range( 0, 1 )] [Sync] public float ManualTint { get; set; } = 0.5f;
				[Property, Group( "Parameters" )] public List<ClothingContainer.ClothingEntry> Clothing { get; set; }
				[Property, Group( "Parameters" )] public List<string> WorkshopClothing { get; set; }
				[Property, Group( "Parameters" )] public List<global::Sandbox.Clothing.ClothingCategory> StrippedCategories { get; set; }

				public bool IsDressing { get; private set; }
				private bool NeedsNetworkOwner => Source is ClothingSource.OwnerUser or ClothingSource.Hybrid;
				private CancellationTokenSource _cts;

				protected override void OnAwake()
				{
					if ( IsProxy ) return;
					if ( !BodyTarget.IsValid() )
						BodyTarget = GetComponentInChildren<SkinnedModelRenderer>();
					_ = ApplyWhenReady();
				}

				protected override void OnEnabled() { if ( IsProxy ) ApplyAttributes(); }
				protected override void OnDestroy() { CancelDressing(); }

				private static async Task<global::Sandbox.Clothing> InstallWorkshopClothing( string ident, CancellationToken token )
				{
					if ( string.IsNullOrEmpty( ident ) ) return null;
					var package = await Package.FetchAsync( ident, false );
					if ( package?.TypeName != "clothing" ) return null;
					if ( token.IsCancellationRequested ) return null;
					var primaryAsset = package.PrimaryAsset;
					if ( string.IsNullOrWhiteSpace( primaryAsset ) ) return null;
					if ( await package.MountAsync() is null ) return null;
					return token.IsCancellationRequested ? null : ResourceLibrary.Get<global::Sandbox.Clothing>( primaryAsset );
				}

				private async ValueTask<ClothingContainer> GetClothing( CancellationToken token )
				{
					switch ( Source )
					{
						case ClothingSource.Manual:
						{
							var c = new ClothingContainer();
							await AddManualClothing( c, token );
							c.Normalize();
							return c;
						}
						case ClothingSource.LocalUser: return ClothingContainer.CreateFromLocalUser();
						case ClothingSource.OwnerUser:
							return Network.Owner != null
								? ClothingContainer.CreateFromConnection( Network.Owner, RemoveUnownedItems )
								: new ClothingContainer();
						case ClothingSource.Hybrid:
						{
							var c = Network.Owner != null
								? ClothingContainer.CreateFromConnection( Network.Owner, RemoveUnownedItems )
								: new ClothingContainer();
							if ( StrippedCategories is { Count: > 0 } )
								c.Clothing.RemoveAll( e => StrippedCategories.Contains( e.Clothing.Category ) );
							await AddManualClothing( c, token );
							c.Normalize();
							return c;
						}
						default: return null;
					}
				}

				private async ValueTask AddManualClothing( ClothingContainer clothing, CancellationToken token )
				{
					if ( Clothing != null ) clothing.AddRange( Clothing );
					clothing.Height = ManualHeight.Remap( 0.8f, 1.2f, 0, 1, true );
					clothing.Age = ManualAge;
					clothing.Tint = ManualTint;

					if ( WorkshopClothing is not { Count: > 0 } ) return;
					var tasks = WorkshopClothing.Select( s => InstallWorkshopClothing( s, token ) ).ToArray();
					await Task.WhenAll( tasks );
					foreach ( var t in tasks )
						if ( t.Result is not null ) clothing.Add( t.Result );
				}

				private async ValueTask ApplyWhenReady()
				{
					if ( NeedsNetworkOwner )
						while ( Network.Owner is null )
						{
							if ( !this.IsValid() ) return;
							await Task.Frame();
						}
					await Apply();
				}

				private void ApplyAttributes()
				{
					if ( BodyTarget is null ) return;
					BodyTarget.Set( "scale_height", ApplyHeightScale ? ManualHeight : 1f );
					foreach ( var r in BodyTarget.GetComponentsInChildren<SkinnedModelRenderer>() )
					{
						r.Attributes.Set( "skin_age", ManualAge );
						r.Attributes.Set( "skin_tint", ManualTint );
					}
				}

				[Button( "Apply Changes" )] private void ApplyButton() => _ = Apply();

				public async ValueTask Apply()
				{
					CancelDressing();
					if ( !BodyTarget.IsValid() ) return;
					_cts = new CancellationTokenSource();
					var token = _cts.Token;
					IsDressing = true;
					try
					{
						var clothing = await GetClothing( token );
						if ( clothing is null || token.IsCancellationRequested || !BodyTarget.IsValid() ) return;
						if ( !ApplyHeightScale ) clothing.Height = 1f;
						clothing.Normalize();
						await clothing.ApplyAsync( BodyTarget, token );
						ManualHeight = clothing.Height.Remap( 0, 1, 0.8f, 1.2f, true );
						ManualTint = clothing.Tint;
						ManualAge = clothing.Age;
						ApplyAttributes();
					}
					finally { IsDressing = false; }
				}

				public void CancelDressing()
				{
					_cts?.Cancel();
					_cts?.Dispose();
					_cts = null;
				}
			}
			""";

		try
		{
			var dir = Path.GetDirectoryName( safe );
			if ( !string.IsNullOrEmpty( dir ) ) Directory.CreateDirectory( dir );
			File.WriteAllText( safe, content );
			return ToolHandlerBase.JsonResult( new
			{
				generated = true,
				template = "dresser",
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

	// ──────────────────────────────────────────────
	// template_shrimple_ragdoll (wraps Small-Fish-Dev/Shrimple-Ragdolls, MIT)
	// ──────────────────────────────────────────────

	[Tool( "template_shrimple_ragdoll", "Generate a wrapper Component that drives a ShrimpleRagdoll (5 modes, hit reactions, partial ragdolling). Requires the fish.shrimple_ragdolls library to be installed." )]
	[Param( "path", "Output path (e.g. 'Code/Combat/RagdollDriver.cs').", Required = true )]
	[Param( "class_name", "Component class name. Default: derived from filename.", Required = false )]
	[Param( "namespace", "C# namespace. Default: 'Game'.", Required = false )]
	public static object ShrimpleRagdollTemplate( JsonElement args )
	{
		var (path, ns, className) = ResolvePath( args );
		if ( path == null ) return ToolHandlerBase.ErrorResult( "Invalid path." );
		if ( File.Exists( path ) ) return ToolHandlerBase.ErrorResult( $"File already exists: {PathNormalizer.ToRelative( path )}" );

		var content = $$"""
			// REQUIRES: fish.shrimple_ragdolls library installed in the project.
			using Sandbox;
			using ShrimpleRagdolls;

			namespace {{ns}};

			/// Wrapper that exposes ShrimpleRagdoll's main lifecycle hooks via simple [Property] flags
			/// + helper methods for hit reactions and partial ragdolling.
			[Title( "Ragdoll Driver" )]
			[Icon( "sports_martial_arts" )]
			public sealed class {{className}} : Component
			{
				[Property] public ShrimpleRagdoll Ragdoll { get; set; }
				[Property] public SkinnedModelRenderer Renderer { get; set; }
				[Property] public RagdollMode StartMode { get; set; } = RagdollMode.None;
				[Property] public bool FollowRootPosition { get; set; } = true;
				[Property] public bool FollowRootRotation { get; set; } = false;

				[Property, Group( "Hit Reaction" )] public float HitRadius { get; set; } = 30f;
				[Property, Group( "Hit Reaction" )] public float HitDuration { get; set; } = 0.5f;
				[Property, Group( "Hit Reaction" )] public float HitRotationStrength { get; set; } = 15f;

				protected override void OnStart()
				{
					if ( !Ragdoll.IsValid() )
					{
						Renderer ??= GetComponentInChildren<SkinnedModelRenderer>();
						if ( Renderer.IsValid() )
						{
							// GetOrAddComponent is the canonical "on this GameObject" helper;
							// Components.GetOrCreate requires a FindMode arg (skill: core-concepts.md §254).
							Ragdoll = GameObject.GetOrAddComponent<ShrimpleRagdoll>();
							Ragdoll.Renderer = Renderer;
						}
					}

					if ( Ragdoll.IsValid() )
					{
						Ragdoll.FollowRootPosition = FollowRootPosition;
						Ragdoll.FollowRootRotation = FollowRootRotation;
						Ragdoll.Mode = StartMode;
					}
				}

				/// Apply a directional hit at world position with optional knockback force.
				public void Hit( Vector3 worldPosition, Vector3 force )
				{
					if ( !Ragdoll.IsValid() ) return;
					Ragdoll.ApplyHitReaction( worldPosition, force, HitRadius, HitDuration, HitRotationStrength );
				}

				/// Switch ragdoll mode at runtime.
				public void SetMode( RagdollMode mode ) { if ( Ragdoll.IsValid() ) Ragdoll.Mode = mode; }

				/// Lerp the mesh back to animation pose, then return to None mode.
				public void GetUp( float duration = 0.4f )
				{
					if ( !Ragdoll.IsValid() ) return;
					Ragdoll.StartLerpMeshToAnimation( duration, RagdollMode.None );
				}

				/// Activate ragdoll on a single bone hierarchy (e.g. "spine" or "arm_R").
				public void RagdollLimb( string boneName, RagdollMode mode = RagdollMode.Enabled )
				{
					if ( Ragdoll.IsValid() ) Ragdoll.RagdollBone( boneName, mode );
				}

				public void UnragdollLimb( string boneName )
				{
					if ( Ragdoll.IsValid() ) Ragdoll.UnragdollBone( boneName );
				}
			}
			""";

		return WriteTemplate( path, content, className, "shrimple_ragdoll" );
	}

	// (template_shrimple_player removed — folded into the deepened template_player_controller
	// in TemplatesTools.cs, which exposes a Shrimple swap section at the bottom.)

	// ──────────────────────────────────────────────
	// template_inventory_skeleton (kurozael/sbox-inventory, MIT)
	// ──────────────────────────────────────────────

	[Tool( "template_inventory", "Generate a complete inventory system on top of SboxPro.Inventory (vendored conna fork): Tetris grid + hotbar + example items + ItemDefinition asset + Razor UI with drag-drop. Generates 5 files. Zero external deps — local.sboxpro_inventory library ships alongside local.sboxpro." )]
	[Param( "path", "Output path for the inventory .cs (sibling files are written next to it). E.g. 'Code/Inventory/PlayerInventory.cs'.", Required = true )]
	[Param( "class_name", "Inventory class name. Default: derived from filename.", Required = false )]
	[Param( "namespace", "C# namespace. Default: 'Game'.", Required = false )]
	[Param( "width", "Main inventory grid width in slots. Default: 10.", Required = false, Type = "integer" )]
	[Param( "height", "Main inventory grid height in slots. Default: 6.", Required = false, Type = "integer" )]
	[Param( "hotbar_size", "Number of hotbar slots (1-9). Default: 9.", Required = false, Type = "integer" )]
	public static object InventorySkeletonTemplate( JsonElement args )
	{
		var (path, ns, className) = ResolvePath( args );
		if ( path == null ) return ToolHandlerBase.ErrorResult( "Invalid path." );
		if ( File.Exists( path ) ) return ToolHandlerBase.ErrorResult( $"File already exists: {PathNormalizer.ToRelative( path )}" );
		var w = ToolHandlerBase.GetInt( args, "width", 10 );
		var h = ToolHandlerBase.GetInt( args, "height", 6 );
		var hotbar = Math.Clamp( ToolHandlerBase.GetInt( args, "hotbar_size", 9 ), 1, 9 );

		var content = $$"""
			// VENDORED: SboxPro.Inventory ships in Libraries/local.sboxpro_inventory/ (no external
			// install needed — composer via Editor.LibrarySystem.Install).
			//
			// All ItemDefinition / runtime Item types live in SboxPro.Inventory now —
			// don't redefine them here. Designers create assets via Asset Browser →
			// New → Weapon / Armor / Consumable / Material.
			//
			// Files generated (4 total in this directory):
			//   {{className}}.cs — backend: inventory + hotbar + holder
			//   {{className}}UI.razor — main inventory panel (drag-drop)
			//   {{className}}UI.razor.scss — styles
			//   {{className}}HotbarUI.razor — hotbar strip at bottom of screen
			//
			// Pipeline: pickups call Holder.TryPickup(item) → backend slots it → UI auto-rebuilds
			// from Inventory.GetItemAt(x,y). Drag-drop calls Inventory.TryMoveOrSwap.
			// Hotbar = a separate {{hotbar}}-slot Inventory (Single mode); number keys 1-{{hotbar}}
			// select slot. Hotbar filters by ItemDefinition.AllowInHotbar.
			using System;
			using Sandbox;
			using SboxPro.Inventory;

			namespace {{ns}};

			// ────────────────────────────────────────────────────────────
			// Main backpack — Tetris-mode {{w}}×{{h}} grid (items occupy WxH cells).
			// ────────────────────────────────────────────────────────────

			public sealed class {{className}} : BaseInventory
			{
				public {{className}}( Guid id ) : base( id, {{w}}, {{h}}, InventorySlotMode.Tetris ) { }
			}

			// ────────────────────────────────────────────────────────────
			// Quick-access hotbar — Single-mode {{hotbar}}×1 (any item shape fits one slot)
			// and only accepts items whose definition has AllowInHotbar=true.
			// ────────────────────────────────────────────────────────────

			public sealed class {{className}}Hotbar : BaseInventory
			{
				public {{className}}Hotbar( Guid id ) : base( id, {{hotbar}}, 1, InventorySlotMode.Single ) { }

				protected override bool CanInsertItem( InventoryItem item )
				{
					if ( item is GameResourceItem<ItemDefinition> r && r.Resource is not null )
						return r.Resource.AllowInHotbar;

					// Items without a Definition (raw InventoryItem subclasses) default to allowed —
					// keeps the hotbar usable for code-only quick items.
					return true;
				}
			}

			// ────────────────────────────────────────────────────────────
			// Holder Component (drop on the player GameObject)
			// ────────────────────────────────────────────────────────────

			[Title( "{{className}} Holder" )]
			[Icon( "inventory_2" )]
			public sealed class {{className}}Holder : Component
			{
				public {{className}} Inventory { get; private set; }
				public {{className}}Hotbar Hotbar { get; private set; }

				/// <summary>Currently selected hotbar slot index (0-based).</summary>
				[Sync] public int SelectedSlot { get; set; } = 0;

				/// <summary>Item in the active hotbar slot, or null.</summary>
				public InventoryItem ActiveItem => Hotbar?.GetItemAt( SelectedSlot, 0 );

				public event Action<{{className}}> OnReady;
				public event Action<InventoryItem, InventoryItem> OnActiveChanged;

				private InventoryItem _lastActive;

				protected override void OnStart()
				{
					if ( Network.Active && !Network.IsOwner ) return;
					Inventory = new {{className}}( GameObject.Id );
					Hotbar = new {{className}}Hotbar( GameObject.Id );
					OnReady?.Invoke( Inventory );
				}

				protected override void OnUpdate()
				{
					if ( IsProxy ) return;
					if ( Hotbar is null ) return;

					// Number keys 1-{{hotbar}} select hotbar slot. Project actions if defined; raw keycap fallback.
					for ( int i = 0; i < {{hotbar}}; i++ )
					{
						if ( Input.Pressed( "Slot" + (i + 1) ) || Input.Keyboard.Pressed( (i + 1).ToString() ) )
						{
							SelectedSlot = i;
							break;
						}
					}

					if ( Input.MouseWheel.y > 0 ) SelectedSlot = (SelectedSlot + 1) % {{hotbar}};
					else if ( Input.MouseWheel.y < 0 ) SelectedSlot = (SelectedSlot - 1 + {{hotbar}}) % {{hotbar}};

					var current = ActiveItem;
					if ( !ReferenceEquals( current, _lastActive ) )
					{
						OnActiveChanged?.Invoke( _lastActive, current );
						_lastActive = current;
					}
				}

				protected override void OnDestroy()
				{
					Inventory?.Dispose();
					Hotbar?.Dispose();
				}

				/// <summary>Try hotbar first (faster access), fall back to backpack.</summary>
				public bool TryPickup( InventoryItem item )
				{
					if ( Hotbar?.TryAdd( item ) == InventoryResult.Success ) return true;
					return Inventory?.TryAdd( item ) == InventoryResult.Success;
				}

				/// <summary>Move an item from main inventory to a specific hotbar slot.</summary>
				public bool MoveToHotbar( InventoryItem item, int slot )
				{
					if ( Inventory is null || Hotbar is null ) return false;
					return Inventory.TryTransferToAt( item, Hotbar, Math.Clamp( slot, 0, {{hotbar}} - 1 ), 0 ) == InventoryResult.Success;
				}

				/// <summary>Drop an item to the world. <paramref name="prefab"/> is the WorldItem prefab to spawn.</summary>
				public bool DropToWorld( InventoryItem item, Vector3 worldPosition, GameObject prefab = null )
				{
					var src = Hotbar?.Contains( item ) == true ? (BaseInventory)Hotbar : Inventory;
					if ( src is null ) return false;
					if ( src.TryRemove( item ) != InventoryResult.Success ) return false;
					if ( prefab.IsValid() ) prefab.Clone( worldPosition ).NetworkSpawn();
					return true;
				}

				public bool TransferTo( InventoryItem item, {{className}}Holder other )
				{
					if ( other?.Inventory is null ) return false;
					var src = Hotbar?.Contains( item ) == true ? (BaseInventory)Hotbar : Inventory;
					return src?.TryTransferTo( item, other.Inventory ) == InventoryResult.Success;
				}
			}
			""";

		// Sibling Razor + scss files (inventory UI + hotbar UI). Each Razor needs its
		// own paired .razor.scss with the same basename — Sandbox.UI auto-loads by name
		// and warns "stylesheet not found" otherwise (issue #25).
		var razorContent = BuildInventoryRazor( ns, className, w, h );
		var scssContent = BuildInventoryScss( className );
		var hotbarRazorContent = BuildHotbarRazor( ns, className, hotbar );
		var hotbarScssContent = BuildHotbarScss();

		var dir = Path.GetDirectoryName( path );
		var razorPath = Path.Combine( dir, $"{className}UI.razor" );
		var scssPath = Path.Combine( dir, $"{className}UI.razor.scss" );
		var hotbarRazorPath = Path.Combine( dir, $"{className}HotbarUI.razor" );
		var hotbarScssPath = Path.Combine( dir, $"{className}HotbarUI.razor.scss" );

		try
		{
			Directory.CreateDirectory( dir );
			File.WriteAllText( path, content );
			File.WriteAllText( razorPath, razorContent );
			File.WriteAllText( scssPath, scssContent );
			File.WriteAllText( hotbarRazorPath, hotbarRazorContent );
			File.WriteAllText( hotbarScssPath, hotbarScssContent );

			return ToolHandlerBase.JsonResult( new
			{
				generated = true,
				template = "inventory",
				className,
				files = new[]
				{
					PathNormalizer.ToRelative( path ),
					PathNormalizer.ToRelative( razorPath ),
					PathNormalizer.ToRelative( scssPath ),
					PathNormalizer.ToRelative( hotbarRazorPath ),
					PathNormalizer.ToRelative( hotbarScssPath )
				},
				note = "Run trigger_hotload to compile. Mount the InventoryUI panel inside a ScreenPanel; HotbarUI auto-shows when the holder has a hotbar."
			} );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Failed to write inventory template: {ex.Message}" );
		}
	}

	private static string BuildInventoryRazor( string ns, string className, int w, int h )
	{
		return $$"""
			@using System
			@using System.Collections.Generic
			@using System.Linq
			@using Sandbox
			@using Sandbox.UI
			@using SboxPro.Inventory
			@using {{ns}}
			@inherits PanelComponent
			@namespace {{ns}}

			<root class="inventory-root @(IsOpen ? "open" : "closed")" onmousedown=@(e => OnRootMouseDown( e ))>
				@* Sandbox.UI does not support CSS Grid (no `repeat()`, `grid-column-start`,    *@
				@* etc.). Use flex rows for slot layout and absolute positioning for items so   *@
				@* multi-cell items can span. CellSize/CellGap drive both layouts so they line  *@
				@* up. (Issue #23.)                                                              *@
				<div class="inventory-grid" style="width: @GridWidthPx; height: @GridHeightPx;">
					<div class="slot-rows">
						@for ( int y = 0; y < Height; y++ )
						{
							<div class="slot-row">
								@for ( int x = 0; x < Width; x++ )
								{
									var sx = x; var sy = y;
									<div class="slot" onmouseup=@(e => OnSlotMouseUp( sx, sy ))></div>
								}
							</div>
						}
					</div>

					@foreach ( var item in EnumerateRootItems() )
					{
						var slot = GetSlot( item );
						<div class="item @(IsDragging( item ) ? "dragging" : "")"
							 style="left: @(ItemLeftPx( slot.X )); top: @(ItemTopPx( slot.Y )); width: @(ItemSizePx( item.Width )); height: @(ItemSizePx( item.Height ));"
							 onmousedown=@(e => OnItemMouseDown( item ))>
							<label class="name">@item.DisplayName</label>
							@if ( item.MaxStackSize > 1 )
							{
								<span class="count">@item.StackCount</span>
							}
						</div>
					}
				</div>

				@if ( _drag is not null )
				{
					<div class="drag-ghost" style="left: @(MousePos.x)px; top: @(MousePos.y)px;">
						<label>@_drag.DisplayName</label>
					</div>
				}
			</root>

			@code
			{
				private const int CellSize = 64;
				private const int CellGap = 4;
				private const int Padding = 12;

				[Property] public {{className}}Holder Holder { get; set; }
				// [InputAction] makes this render as a dropdown of project-defined input
				// actions in the inspector instead of free text. The user picks an action;
				// if their project hasn't defined one yet, ToggleKeyFallback still works.
				[Property, InputAction] public string ToggleAction { get; set; } = "Inventory";
				[Property] public string ToggleKeyFallback { get; set; } = "tab";
				[Property] public bool IsOpen { get; set; }

				public int Width => Holder?.Inventory?.Width ?? {{w}};
				public int Height => Holder?.Inventory?.Height ?? {{h}};
				public Vector2 MousePos { get; set; }

				public string GridWidthPx => $"{Padding * 2 + Width * CellSize + (Width - 1) * CellGap}px";
				public string GridHeightPx => $"{Padding * 2 + Height * CellSize + (Height - 1) * CellGap}px";

				private string ItemLeftPx( int x ) => $"{Padding + x * (CellSize + CellGap)}px";
				private string ItemTopPx( int y ) => $"{Padding + y * (CellSize + CellGap)}px";
				private string ItemSizePx( int span ) => $"{span * CellSize + (span - 1) * CellGap}px";

				private InventoryItem _drag;

				protected override void OnStart()
				{
					// Each networked player prefab carries its own HUD child. ScreenPanels
					// render to the local screen regardless of ownership, so without this
					// guard every client would see every player's inventory + hotbar stacked
					// (issue #28). Disable the HUD GameObject on proxies so only the local
					// player's HUD survives.
					if ( IsProxy ) GameObject.Enabled = false;
				}

				protected override void OnUpdate()
				{
					// ToggleAction reads a project-defined input action; if the project hasn't
					// added an "Inventory" binding yet, fall back to a raw keyboard key so the
					// UI is still reachable out-of-the-box. (Issue #24.)
					bool toggled = false;
					if ( !string.IsNullOrEmpty( ToggleAction ) )
					{
						try { if ( Input.Pressed( ToggleAction ) ) toggled = true; }
						catch { /* action not registered — fallback below */ }
					}
					if ( !toggled && !string.IsNullOrEmpty( ToggleKeyFallback ) && Input.Keyboard.Pressed( ToggleKeyFallback ) )
						toggled = true;
					if ( toggled ) IsOpen = !IsOpen;

					// Only track mouse + repaint while panel is open and the user is dragging an
					// item. Previously this fired StateHasChanged every frame regardless, which
					// made the Razor panel rebuild its tree at full framerate even when idle.
					if ( IsOpen && _drag is not null )
					{
						MousePos = Mouse.Position;
						// BuildHash already reflects MousePos; the framework will rebuild only
						// when the hash actually differs from the previous frame.
					}
				}

				private IEnumerable<InventoryItem> EnumerateRootItems()
				{
					if ( Holder?.Inventory is null ) yield break;
					var seen = new HashSet<Guid>();
					for ( int y = 0; y < Height; y++ )
					for ( int x = 0; x < Width; x++ )
					{
						var item = Holder.Inventory.GetItemAt( x, y );
						if ( item is null || seen.Contains( item.Id ) ) continue;
						seen.Add( item.Id );
						yield return item;
					}
				}

				private InventorySlot GetSlot( InventoryItem item )
				{
					Holder.Inventory.TryGetSlot( item, out var slot );
					return slot;
				}

				private bool IsDragging( InventoryItem item ) => _drag is not null && _drag.Id == item.Id;

				private void OnItemMouseDown( InventoryItem item ) { _drag = item; }

				private void OnSlotMouseUp( int x, int y )
				{
					if ( _drag is null || Holder?.Inventory is null ) { _drag = null; return; }
					Holder.Inventory.TryMoveOrSwap( _drag, x, y, out _ );
					_drag = null;
				}

				private void OnRootMouseDown( PanelEvent e )
				{
					// Background click: drop drag. PanelEvent.This is a Panel; we extend
					// PanelComponent which renders to .Panel. Direct == comparison between
					// Panel and PanelComponent fails CS0019 (issue #11) — go through .Panel.
					if ( _drag is not null && e.This == this.Panel ) _drag = null;
				}

				protected override int BuildHash() => System.HashCode.Combine( IsOpen, Width, Height, MousePos, _drag?.Id, EnumerateRootItems().Count() );
			}
			""";
	}

	private static string BuildInventoryScss( string className )
	{
		return """
			.inventory-root {
				position: absolute;
				width: 100%;
				height: 100%;
				justify-content: center;
				align-items: center;
				pointer-events: none;

				&.closed { opacity: 0; }
				&.open { opacity: 1; pointer-events: all; }
			}

			.inventory-grid {
				position: relative;
				background-color: rgba( 0, 0, 0, 0.7 );
				border-radius: 8px;
				padding: 12px;
			}

			.slot-rows {
				flex-direction: column;
				gap: 4px;
			}

			.slot-row {
				flex-direction: row;
				gap: 4px;
			}

			.slot {
				width: 64px;
				height: 64px;
				background-color: rgba( 255, 255, 255, 0.05 );
				border: 1px solid rgba( 255, 255, 255, 0.1 );
				border-radius: 4px;

				&:hover { background-color: rgba( 255, 255, 255, 0.12 ); }
			}

			.item {
				position: absolute;
				background-color: rgba( 80, 120, 180, 0.6 );
				border: 1px solid rgba( 200, 220, 255, 0.5 );
				border-radius: 4px;
				padding: 4px;
				justify-content: space-between;
				align-items: stretch;
				cursor: grab;

				&.dragging { opacity: 0.4; cursor: grabbing; }
				&:hover { background-color: rgba( 100, 150, 220, 0.7 ); }

				.name {
					font-size: 11px;
					color: white;
					text-align: center;
				}

				.count {
					font-size: 12px;
					font-weight: bold;
					color: white;
					align-self: flex-end;
					text-shadow: 1px 1px 2px rgba( 0, 0, 0, 0.8 );
				}
			}

			.drag-ghost {
				position: absolute;
				width: 64px;
				height: 64px;
				background-color: rgba( 80, 120, 180, 0.8 );
				border: 1px solid white;
				border-radius: 4px;
				padding: 4px;
				pointer-events: none;
				transform: translate( -50%, -50% );

				label { font-size: 11px; color: white; text-align: center; }
			}
			""";
	}

	// Sandbox.UI auto-loads `<RazorFile>.razor.scss` by name. The hotbar Razor lives
	// in its own file, so it needs its own paired stylesheet — without it the engine
	// logs "Error opening stylesheet: ...HotbarUI.razor.scss (File not found)" on
	// every panel build (issue #25).
	private static string BuildHotbarScss()
	{
		return """
			.hotbar-root {
				position: absolute;
				bottom: 24px;
				left: 50%;
				transform: translateX( -50% );
				flex-direction: row;
				gap: 4px;
				background-color: rgba( 0, 0, 0, 0.6 );
				border-radius: 6px;
				padding: 6px;
			}

			.hotbar-slot {
				width: 56px;
				height: 56px;
				background-color: rgba( 255, 255, 255, 0.05 );
				border: 1px solid rgba( 255, 255, 255, 0.15 );
				border-radius: 4px;
				justify-content: center;
				align-items: center;

				&.active {
					border-color: rgba( 255, 220, 100, 0.9 );
					background-color: rgba( 255, 220, 100, 0.15 );
				}

				.name { font-size: 10px; color: white; text-align: center; }
				.count {
					position: absolute;
					right: 4px;
					bottom: 4px;
					font-size: 11px;
					font-weight: bold;
					color: white;
					text-shadow: 1px 1px 2px rgba( 0, 0, 0, 0.8 );
				}
				.key {
					position: absolute;
					left: 4px;
					top: 2px;
					font-size: 10px;
					color: rgba( 255, 255, 255, 0.6 );
				}
			}
			""";
	}

	private static string BuildHotbarRazor( string ns, string className, int hotbar )
	{
		return $$"""
			@using System
			@using Sandbox
			@using Sandbox.UI
			@using SboxPro.Inventory
			@using {{ns}}
			@inherits PanelComponent
			@namespace {{ns}}

			<root class="hotbar-root">
				@for ( int i = 0; i < Slots; i++ )
				{
					var idx = i;
					<div class="hotbar-slot @(IsActive( idx ) ? "active" : "")">
						<span class="key">@(idx + 1)</span>
						@if ( GetItem( idx ) is { } item )
						{
							<label class="name">@item.DisplayName</label>
							@if ( item.MaxStackSize > 1 )
							{
								<span class="count">@item.StackCount</span>
							}
						}
					</div>
				}
			</root>

			@code
			{
				[Property] public {{className}}Holder Holder { get; set; }
				public int Slots => {{hotbar}};

				protected override void OnStart()
				{
					// Hide HUD on remote players' prefabs — see PlayerInventoryUI.OnStart
					// (issue #28).
					if ( IsProxy ) GameObject.Enabled = false;
				}

				private InventoryItem GetItem( int slot ) => Holder?.Hotbar?.GetItemAt( slot, 0 );
				private bool IsActive( int slot ) => Holder?.SelectedSlot == slot;

				protected override int BuildHash()
				{
					int hash = Holder?.SelectedSlot ?? 0;
					for ( int i = 0; i < Slots; i++ )
					{
						var item = GetItem( i );
						hash = System.HashCode.Combine( hash, item?.Id, item?.StackCount );
					}
					return hash;
				}
			}
			""";
	}

	// ──────────────────────────────────────────────
	// template_shrimple_pawn (Small-Fish-Dev/shrimple-pawns, MIT)
	// ──────────────────────────────────────────────

	[Tool( "template_shrimple_pawn", "Generate a Pawn + Client + Game scaffolding on top of fish.sp library (classic Pawn architecture for S&Box). Requires shrimple-pawns installed." )]
	[Param( "path", "Output path (e.g. 'Code/Pawns/MyPawn.cs').", Required = true )]
	[Param( "class_name", "Base class name (used as prefix for MyClient + MyPawn). Default: derived from filename.", Required = false )]
	[Param( "namespace", "C# namespace. Default: 'Game'.", Required = false )]
	[Param( "pawn_prefab", "Path to the pawn .prefab file (will be set in [Pawn] attribute). Default: 'prefabs/pawn.prefab'.", Required = false )]
	public static object ShrimplePawnTemplate( JsonElement args )
	{
		var (path, ns, className) = ResolvePath( args );
		if ( path == null ) return ToolHandlerBase.ErrorResult( "Invalid path." );
		if ( File.Exists( path ) ) return ToolHandlerBase.ErrorResult( $"File already exists: {PathNormalizer.ToRelative( path )}" );
		var prefab = ToolHandlerBase.GetString( args, "pawn_prefab", "prefabs/pawn.prefab" );

		var content = $$"""
			// REQUIRES: fish.sp library installed.
			using Sandbox;
			using Sandbox.Network;
			using ShrimplePawns;

			namespace {{ns}};

			/// Per-player client component. Hosts AssignConnection and AssignPawn calls.
			public sealed class {{className}}Client : ShrimplePawns.Client { }

			/// Project-specific Pawn base. Tracks the owning client and exposes it to subclasses.
			public abstract class {{className}}Pawn : ShrimplePawns.Pawn
			{
				[Sync( SyncFlags.FromHost )] public {{className}}Client Owner { get; private set; }

				public override void OnAssign( ShrimplePawns.Client client )
				{
					Owner = client as {{className}}Client;
				}
			}

			/// Concrete spectator pawn — moves freely, has its own camera. Used as the default when a connection joins.
			[Pawn( "{{prefab}}" )]
			public sealed class {{className}}SpectatePawn : {{className}}Pawn
			{
				[RequireComponent] public CameraComponent CameraComponent { get; set; }

				[Property] public float MoveSpeed { get; set; } = 250f;

				private Angles _eyeAngles;

				protected override void OnStart()
				{
					CameraComponent.Enabled = !IsProxy;
				}

				protected override void OnUpdate()
				{
					if ( IsProxy ) return;
					_eyeAngles += Input.AnalogLook;
					_eyeAngles = _eyeAngles.WithPitch( _eyeAngles.pitch.Clamp( -89f, 89f ) );
					WorldRotation = _eyeAngles.ToRotation();
					// AnalogMove is unitless input (-1..1). Multiply by speed and Time.Delta so
					// movement is frame-rate independent — the previous form moved by raw input
					// per frame, meaning a 240Hz player flew 4× faster than a 60Hz player.
					WorldPosition += Input.AnalogMove * WorldRotation * (MoveSpeed * Time.Delta);
				}
			}

			/// Game manager — spawns the client + pawn for each connection. Place this on a scene-root GameObject.
			public sealed class {{className}}Game : Component, Component.INetworkListener
			{
				[Property] public PrefabFile ClientPrefab { get; set; }

				protected override void OnStart()
				{
					if ( !Networking.IsActive )
						Networking.CreateLobby( new LobbyConfig() );
				}

				public void OnActive( Connection channel )
				{
					var clientObj = SceneUtility.GetPrefabScene( ClientPrefab ).Clone();
					clientObj.NetworkSpawn( channel );

					var client = clientObj.Components.Get<{{className}}Client>();
					client.AssignConnection( channel );
					client.AssignPawn<{{className}}SpectatePawn>();
				}
			}
			""";

		return WriteTemplate( path, content, className, "shrimple_pawn" );
	}

	// (template_visual_novel removed — was a thin Play/Stop wrapper around the existing
	// VNBase.ScriptPlayer Component, providing no real composition value. Use
	// ScriptPlayer directly from the wizards.vnbase_library package.)

	// ──────────────────────────────────────────────
	// template_astar_npc (Small-Fish-Dev/Grid-and-Astar-NPC, MIT)
	// ──────────────────────────────────────────────

	[Tool( "template_astar_npc", "Generate an NPC Component that pathfinds via the Grid+A* library (build a Grid at scene start, then AStarPathBuilder.From(grid).Run() per target). Requires fish.grid_and_astar installed; library uses GridAStar namespace and may need adaptation if it still uses the legacy Entity API." )]
	[Param( "path", "Output path (e.g. 'Code/AI/AstarNpc.cs').", Required = true )]
	[Param( "class_name", "Component class name. Default: derived from filename.", Required = false )]
	[Param( "namespace", "C# namespace. Default: 'Game'.", Required = false )]
	public static object AstarNpcTemplate( JsonElement args )
	{
		var (path, ns, className) = ResolvePath( args );
		if ( path == null ) return ToolHandlerBase.ErrorResult( "Invalid path." );
		if ( File.Exists( path ) ) return ToolHandlerBase.ErrorResult( $"File already exists: {PathNormalizer.ToRelative( path )}" );

		var content = $$"""
			// REQUIRES: fish.grid_and_astar library installed. The library is feature-rich (~75KB grid + A* code)
			// but predates the current Component API in some places — adapt as needed if compile errors hit.
			using Sandbox;
			using GridAStar;

			namespace {{ns}};

			/// Minimal NPC: each frame advances along the cached A* path toward CurrentTarget.
			/// Recompute path by calling SetTarget(worldPos).
			[Title( "A* NPC" )]
			[Icon( "directions_walk" )]
			public sealed class {{className}} : Component
			{
				[Property] public Vector3 CurrentTarget { get; set; }
				[Property] public float MoveSpeed { get; set; } = 150f;
				[Property] public float ArrivalDistance { get; set; } = 16f;

				private AStarPath _path;
				private int _pathIndex;

				/// Set a new world-space target and recompute the path. Returns true if path found.
				public bool SetTarget( Vector3 target )
				{
					CurrentTarget = target;
					var grid = Grid.Main;
					if ( grid is null ) { Log.Warning( "[AstarNpc] No Grid.Main exists — generate a grid first." ); return false; }

					var startCell = grid.GetNearestCell( WorldPosition );
					var endCell = grid.GetNearestCell( target );
					if ( startCell is null || endCell is null ) return false;

					var builder = AStarPathBuilder.From( grid ).WithoutTags( "occupied" );
					_path = builder.Run( startCell, endCell );
					_pathIndex = 0;
					return _path is not null && _path.Count > 0;
				}

				protected override void OnUpdate()
				{
					// Path advancement is host-or-owner-only. Without this guard every client
					// would advance their local copy of _pathIndex and writes to WorldPosition
					// would race the network sync from the authoritative simulator.
					if ( IsProxy ) return;
					if ( _path is null || _pathIndex >= _path.Count ) return;

					var nextCell = _path.Cells[_pathIndex];
					var step = nextCell.Position;
					var toStep = step.WithZ( WorldPosition.z ) - WorldPosition;

					if ( toStep.Length <= ArrivalDistance )
					{
						_pathIndex++;
						return;
					}

					WorldPosition += toStep.Normal * MoveSpeed * Time.Delta;
				}

				/// Stop following the current path.
				public void Halt() { _path = null; _pathIndex = 0; }
			}
			""";

		return WriteTemplate( path, content, className, "astar_npc" );
	}

	// ──────────────────────────────────────────────
	// template_weapon (framework-agnostic Weapon base + Pistol + Knife examples)
	// ──────────────────────────────────────────────

	[Tool( "template_weapon", "Generate a complete weapon system: abstract Weapon base (handles cooldown, ammo, primary/secondary attack, hit events) + a working Pistol (ranged hitscan) + Knife (melee sweep). Subclass to add new weapons. Has a commented hook for SWB framework integration if you install timmybo5/simple-weapon-base." )]
	[Param( "path", "Output path (e.g. 'Code/Combat/Weapons.cs').", Required = true )]
	[Param( "namespace", "C# namespace. Default: 'Game.Combat'.", Required = false )]
	public static object WeaponTemplate( JsonElement args )
	{
		var path = ToolHandlerBase.RequireString( args, "path" );
		var ns = ToolHandlerBase.GetString( args, "namespace", "Game.Combat" );
		if ( !path.EndsWith( ".cs", StringComparison.OrdinalIgnoreCase ) ) path += ".cs";
		var safe = PathNormalizer.ResolveAssetPath( path );
		if ( safe == null ) return ToolHandlerBase.ErrorResult( $"Invalid path: {path}" );
		if ( File.Exists( safe ) ) return ToolHandlerBase.ErrorResult( $"File already exists: {PathNormalizer.ToRelative( safe )}" );

		var content = $$"""
			// Framework-agnostic weapon system. Drop into a project — no external dependencies.
			// One Weapon abstract base + two concrete examples (Pistol = ranged, Knife = melee).
			// Subclass Weapon to add new weapons. For full FPS-feature weapons (viewmodels,
			// attachments, ironsights, recoil), see SWB integration note at the bottom.
			using System;
			using Sandbox;

			namespace {{ns}};

			public enum WeaponKind { Ranged, Melee }
			public enum AttackResult { None, Cooldown, NoAmmo, Reloading, Fired }

			/// <summary>
			/// Abstract weapon base. Owns cooldown, ammo, fire/reload state. Subclass and
			/// override DoPrimary/DoSecondary to implement attack behaviour. The defaults
			/// already provide hitscan (Ranged) and a forward sweep (Melee).
			/// </summary>
			[Title( "Weapon" )]
			[Icon( "sports_kabaddi" )]
			public abstract class Weapon : Component
			{
				// --- Identity ---
				[Property] public string DisplayName { get; set; } = "Weapon";
				[Property] public WeaponKind Kind { get; set; } = WeaponKind.Ranged;

				// --- Stats ---
				[Property, Group( "Stats" )] public float PrimaryDamage { get; set; } = 10f;
				[Property, Group( "Stats" )] public float SecondaryDamage { get; set; } = 0f;
				/// <summary>Attacks per second. 4 = 240 RPM.</summary>
				[Property, Group( "Stats" ), Range( 0.5f, 30f )] public float PrimaryRate { get; set; } = 4f;
				/// <summary>Hitscan range (Ranged) or melee reach (Melee).</summary>
				[Property, Group( "Stats" ), Range( 16f, 8192f )] public float Range { get; set; } = 1024f;
				/// <summary>Cone half-angle in degrees for Melee swings. Ignored for Ranged.</summary>
				[Property, Group( "Stats" ), Range( 0f, 90f ), ShowIf( nameof( Kind ), WeaponKind.Melee )]
				public float MeleeArc { get; set; } = 45f;

				// --- Ammo (Ranged) ---
				[Property, Group( "Ammo" ), ShowIf( nameof( Kind ), WeaponKind.Ranged )]
				public int ClipSize { get; set; } = 12;
				[Property, Group( "Ammo" ), ShowIf( nameof( Kind ), WeaponKind.Ranged )]
				public float ReloadTime { get; set; } = 1.5f;
				[Sync] public int CurrentAmmo { get; set; } = 12;
				[Sync] public bool IsReloading { get; set; }

				// --- Input ---
				[Property, Group( "Input" )] public string PrimaryAction { get; set; } = "Attack1";
				[Property, Group( "Input" )] public string SecondaryAction { get; set; } = "Attack2";
				[Property, Group( "Input" )] public string ReloadActionName { get; set; } = "Reload";

				// --- Refs ---
				[Property, Group( "Refs" )] public GameObject Muzzle { get; set; }
				[Property, Group( "Refs" )] public CameraComponent Camera { get; set; }
				[Property, Group( "Refs" )] public SoundEvent FireSound { get; set; }
				[Property, Group( "Refs" )] public SoundEvent EmptyClickSound { get; set; }

				// --- Events ---
				public event Action<SceneTraceResult> OnHit;
				public event Action OnFired;
				public event Action OnReloadStarted;
				public event Action OnReloadFinished;

				// --- State ---
				public TimeSince TimeSinceFired { get; private set; } = 999f;
				public TimeSince TimeSinceReloadStarted { get; private set; } = 999f;
				public float Cooldown => 1f / Math.Max( 0.01f, PrimaryRate );
				public bool IsRanged => Kind == WeaponKind.Ranged;
				public bool HasAmmo => !IsRanged || CurrentAmmo > 0;
				public bool CanFire => !IsReloading && TimeSinceFired >= Cooldown && HasAmmo;

				protected override void OnStart()
				{
					Camera ??= Scene.Camera;
					if ( IsRanged ) CurrentAmmo = ClipSize;
				}

				protected override void OnUpdate()
				{
					if ( IsProxy ) return;
					TickReload();
					HandleInput();
				}

				private void HandleInput()
				{
					if ( !string.IsNullOrEmpty( PrimaryAction ) && Input.Down( PrimaryAction ) )
						TryAttack( primary: true );
					if ( !string.IsNullOrEmpty( SecondaryAction ) && Input.Pressed( SecondaryAction ) )
						TryAttack( primary: false );
					if ( IsRanged && !string.IsNullOrEmpty( ReloadActionName ) && Input.Pressed( ReloadActionName ) )
						TryReload();
				}

				private void TickReload()
				{
					if ( !IsReloading ) return;
					if ( TimeSinceReloadStarted < ReloadTime ) return;
					IsReloading = false;
					CurrentAmmo = ClipSize;
					OnReloadFinished?.Invoke();
				}

				public AttackResult TryAttack( bool primary )
				{
					if ( IsReloading ) return AttackResult.Reloading;
					if ( TimeSinceFired < Cooldown ) return AttackResult.Cooldown;
					if ( IsRanged && primary && CurrentAmmo <= 0 )
					{
						// Empty-click is purely cosmetic — broadcast so other players hear it too.
						BroadcastFx( fxKind: 1, MuzzlePosition );
						return AttackResult.NoAmmo;
					}
					TimeSinceFired = 0f;
					if ( primary ) DoPrimary();
					else DoSecondary();
					if ( IsRanged && primary ) CurrentAmmo = Math.Max( 0, CurrentAmmo - 1 );
					// Fire sound is networked so every connected client hears the gunshot, not
					// just the firing player. Hit-impact effects are broadcast by ApplyHit when
					// a trace lands.
					BroadcastFx( fxKind: 0, MuzzlePosition );
					OnFired?.Invoke();
					return AttackResult.Fired;
				}

				/// <summary>
				/// Broadcasts a cosmetic effect to all clients. fxKind: 0 = fire sound, 1 = empty click,
				/// 2 = hit impact. Plays the matching SoundEvent locally on each receiver.
				/// </summary>
				[Rpc.Broadcast( NetFlags.Unreliable )]
				private void BroadcastFx( int fxKind, Vector3 position )
				{
					switch ( fxKind )
					{
						case 0: if ( FireSound is not null ) Sound.Play( FireSound, position ); break;
						case 1: if ( EmptyClickSound is not null ) Sound.Play( EmptyClickSound, position ); break;
						case 2: /* override ApplyHit for impact FX */ break;
					}
				}

				public bool TryReload()
				{
					if ( !IsRanged ) return false;
					if ( IsReloading || CurrentAmmo >= ClipSize ) return false;
					IsReloading = true;
					TimeSinceReloadStarted = 0f;
					OnReloadStarted?.Invoke();
					return true;
				}

				/// <summary>Default primary: hitscan (Ranged) or forward melee (Melee). Override for custom behavior.</summary>
				protected virtual void DoPrimary()
				{
					if ( Kind == WeaponKind.Ranged ) DoHitscan( PrimaryDamage );
					else DoMeleeSweep( PrimaryDamage );
				}

				/// <summary>Default secondary: no-op. Override for ADS/scope/alt-fire/parry/etc.</summary>
				protected virtual void DoSecondary() { }

				protected void DoHitscan( float damage )
				{
					var aimRot = Camera?.WorldRotation ?? WorldRotation;
					var aimPos = Camera?.WorldPosition ?? WorldPosition;
					var trace = Scene.Trace.Ray( aimPos, aimPos + aimRot.Forward * Range )
						.IgnoreGameObjectHierarchy( GameObject )
						.UseHitboxes()
						.Run();
					if ( trace.Hit ) ApplyHit( trace, damage );
				}

				protected void DoMeleeSweep( float damage )
				{
					var aimRot = Camera?.WorldRotation ?? WorldRotation;
					var aimPos = WorldPosition;
					var trace = Scene.Trace.Ray( aimPos, aimPos + aimRot.Forward * Range )
						.IgnoreGameObjectHierarchy( GameObject )
						.Radius( Range * MathF.Sin( MathF.PI * MeleeArc / 180f ) * 0.5f )
						.UseHitboxes()
						.Run();
					if ( trace.Hit ) ApplyHit( trace, damage );
				}

				protected virtual void ApplyHit( SceneTraceResult trace, float damage )
				{
					OnHit?.Invoke( trace );
					// Damage application is host-authoritative. The owner predicts the trace
					// locally for responsive feedback, then asks the host to apply damage; host
					// validates against its own simulation and replicates Health via [Sync].
					//
					// Example wire-up (uncomment + adapt to your damage component):
					// var target = trace.GameObject;
					// if ( target.IsValid() ) RequestDamage( target, damage );
				}

				/// <summary>
				/// Send damage intent to the host. Host runs the body of this RPC and applies
				/// damage to the target's authoritative Health (sync replicates back to all).
				/// Only the object owner may invoke (NetFlags.OwnerOnly) — prevents proxies
				/// from calling this on someone else's weapon.
				/// </summary>
				[Rpc.Host( NetFlags.OwnerOnly )]
				protected void RequestDamage( GameObject target, float damage )
				{
					if ( !Networking.IsHost ) return;
					if ( !target.IsValid() ) return;
					// Project-specific damage hookup. Example with the bundled networked_player:
					// var np = target.Components.Get<NetworkedPlayer>();
					// np?.TakeDamage( (int)damage, GameObject.Id );
				}

				protected Vector3 MuzzlePosition => Muzzle?.WorldPosition ?? WorldPosition;
			}

			// ────────────────────────────────────────────────────────────
			// Concrete examples — keep, modify, or use as reference
			// ────────────────────────────────────────────────────────────

			[Title( "Pistol" )]
			[Icon( "auto_awesome_motion" )]
			public sealed class Pistol : Weapon
			{
				protected override void OnAwake()
				{
					DisplayName = "9mm Pistol";
					Kind = WeaponKind.Ranged;
					PrimaryDamage = 22f;
					PrimaryRate = 5f;
					Range = 4096f;
					ClipSize = 12;
					ReloadTime = 1.4f;
				}
			}

			[Title( "Knife" )]
			[Icon( "kitchen" )]
			public sealed class Knife : Weapon
			{
				protected override void OnAwake()
				{
					DisplayName = "Combat Knife";
					Kind = WeaponKind.Melee;
					PrimaryDamage = 35f;
					SecondaryDamage = 75f; // backstab/stab via DoSecondary
					PrimaryRate = 2.5f;
					Range = 80f;
					MeleeArc = 60f;
				}

				protected override void DoSecondary()
				{
					// Heavier stab — slower wind-up could be added with a separate cooldown.
					DoMeleeSweep( SecondaryDamage );
				}
			}

			// ── Optional SWB integration ────────────────────────────────────
			// If you've vendored the SWB framework code into Code/, the class
			// below extends its full feature set (viewmodels, recoil,
			// attachments, ironsights) instead of the lightweight base above.
			//
			// using SWB.Base;
			// using SWB.Shared;
			//
			// public sealed class FullSwbPistol : SWB.Base.Weapon
			// {
			// protected override void OnAwake()
			// {
			// ClassName = "swb_pistol";
			// DisplayName = "9mm Pistol";
			// HoldType = HoldTypes.Pistol;
			// Slot = 1;
			// ReloadTime = 1.5f;
			// DrawTime = 0.5f;
			// // ViewModel = Model.Load( "models/weapons/pistol/v_pistol.vmdl" );
			// // WorldModel = Model.Load( "models/weapons/pistol/w_pistol.vmdl" );
			// base.OnAwake();
			// }
			// }
			""";

		try
		{
			var dir = Path.GetDirectoryName( safe );
			if ( !string.IsNullOrEmpty( dir ) ) Directory.CreateDirectory( dir );
			File.WriteAllText( safe, content );
			return ToolHandlerBase.JsonResult( new
			{
				generated = true,
				template = "weapon",
				path = PathNormalizer.ToRelative( safe ),
				includes = new[] { "Weapon (abstract)", "Pistol (ranged)", "Knife (melee)" },
				note = "Run trigger_hotload. Compiles standalone — no external deps."
			} );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Failed to write template: {ex.Message}" );
		}
	}

	// ──────────────────────────────────────────────
	// Helpers
	// ──────────────────────────────────────────────

	private static (string safePath, string ns, string className) ResolvePath( JsonElement args )
	{
		var path = ToolHandlerBase.RequireString( args, "path" );
		var ns = ToolHandlerBase.GetString( args, "namespace", "Game" );
		var className = ToolHandlerBase.GetString( args, "class_name" );
		if ( !path.EndsWith( ".cs", StringComparison.OrdinalIgnoreCase ) ) path += ".cs";
		if ( string.IsNullOrEmpty( className ) ) className = Path.GetFileNameWithoutExtension( path );
		var safe = PathNormalizer.ResolveAssetPath( path );
		return (safe, ns, className);
	}

	private static object WriteTemplate( string safePath, string content, string className, string templateName )
	{
		try
		{
			var dir = Path.GetDirectoryName( safePath );
			if ( !string.IsNullOrEmpty( dir ) ) Directory.CreateDirectory( dir );
			File.WriteAllText( safePath, content );
			return ToolHandlerBase.JsonResult( new
			{
				generated = true,
				template = templateName,
				path = PathNormalizer.ToRelative( safePath ),
				className,
				note = "Run trigger_hotload to compile + register the new Component."
			} );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Failed to write template: {ex.Message}" );
		}
	}
}
