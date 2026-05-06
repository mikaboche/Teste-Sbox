using System;
using System.Linq;
using System.Text.Json;
using Sandbox;

namespace SboxPro;

public static class NetworkingTools
{
	// ──────────────────────────────────────────────
	//  get_network_status
	// ──────────────────────────────────────────────

	[Tool( "get_network_status", "Read the global networking session state (host/client/connected, server name, connections, etc)." )]
	public static object GetNetworkStatus( JsonElement args )
	{
		return ToolHandlerBase.JsonResult( new
		{
			isActive = Networking.IsActive,
			isHost = Networking.IsHost,
			isClient = Networking.IsClient,
			isConnecting = Networking.IsConnecting,
			serverName = Networking.ServerName,
			mapName = Networking.MapName,
			maxPlayers = Networking.MaxPlayers,
			connectionCount = Connection.All?.Count ?? 0,
			hostConnectionId = Connection.Host?.Id.ToString()
		} );
	}

	// ──────────────────────────────────────────────
	//  network_spawn
	// ──────────────────────────────────────────────

	[Tool( "network_spawn", "Network-spawn a GameObject so it's replicated to clients (host only).", RequiresMainThread = true )]
	[Param( "name", "Name of the GameObject to spawn.", Required = false )]
	[Param( "guid", "GUID of the GameObject to spawn.", Required = false )]
	public static object NetworkSpawn( JsonElement args )
	{
		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );

		if ( go.Network == null )
			return ToolHandlerBase.ErrorResult( $"'{go.Name}' has no NetworkAccessor (something is very wrong)." );

		if ( go.Network.Active )
			return ToolHandlerBase.ErrorResult( $"'{go.Name}' is already network-spawned." );

		go.NetworkSpawn();
		var success = go.Network.Active;

		return ToolHandlerBase.JsonResult( new
		{
			spawned = success,
			gameObject = go.Name,
			isActive = go.Network.Active,
			ownerId = go.Network.OwnerId.ToString(),
			creatorId = go.Network.CreatorId.ToString()
		} );
	}

	// ──────────────────────────────────────────────
	//  set_ownership
	// ──────────────────────────────────────────────

	[Tool( "set_ownership", "Take, drop, or reassign ownership of a networked GameObject.", RequiresMainThread = true )]
	[Param( "name", "Name of the GameObject.", Required = false )]
	[Param( "guid", "GUID of the GameObject.", Required = false )]
	[Param( "action", "Action: 'take' (become owner), 'drop' (release ownership), 'assign' (assign to specific connection).", Required = true, Enum = "take,drop,assign" )]
	[Param( "connection_id", "Connection GUID (required for action='assign').", Required = false )]
	public static object SetOwnership( JsonElement args )
	{
		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );
		if ( !go.Network.Active )
			return ToolHandlerBase.ErrorResult( $"'{go.Name}' is not networked. Spawn it first." );

		var action = ToolHandlerBase.RequireString( args, "action" )?.ToLowerInvariant();
		bool ok;
		string detail;

		switch ( action )
		{
			case "take":
				ok = go.Network.TakeOwnership();
				detail = $"TakeOwnership → {(ok ? "success" : "failed (OwnerTransfer policy?)")}";
				break;
			case "drop":
				ok = go.Network.DropOwnership();
				detail = $"DropOwnership → {(ok ? "success" : "failed")}";
				break;
			case "assign":
				var connStr = ToolHandlerBase.GetString( args, "connection_id" );
				if ( string.IsNullOrEmpty( connStr ) )
					return ToolHandlerBase.ErrorResult( "action='assign' requires connection_id." );
				if ( !Guid.TryParse( connStr, out var connGuid ) )
					return ToolHandlerBase.ErrorResult( $"Invalid connection_id (not a GUID): {connStr}" );
				var conn = Connection.Find( connGuid );
				if ( conn == null )
					return ToolHandlerBase.ErrorResult( $"Connection not found: {connStr}" );
				ok = go.Network.AssignOwnership( conn );
				detail = $"AssignOwnership → {(ok ? "success" : "failed")}";
				break;
			default:
				return ToolHandlerBase.ErrorResult( $"Unknown action '{action}'. Use take/drop/assign." );
		}

		return ToolHandlerBase.JsonResult( new
		{
			success = ok,
			gameObject = go.Name,
			action,
			detail,
			ownerId = go.Network.OwnerId.ToString(),
			isOwner = go.Network.IsOwner,
			isProxy = go.Network.IsProxy
		} );
	}

	// ──────────────────────────────────────────────
	//  network_refresh
	// ──────────────────────────────────────────────

	[Tool( "network_refresh", "Send a complete refresh snapshot of a networked GameObject to clients.", RequiresMainThread = true )]
	[Param( "name", "Name of the GameObject.", Required = false )]
	[Param( "guid", "GUID of the GameObject.", Required = false )]
	public static object NetworkRefresh( JsonElement args )
	{
		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );
		if ( !go.Network.Active )
			return ToolHandlerBase.ErrorResult( $"'{go.Name}' is not networked." );

		go.Network.Refresh();

		return ToolHandlerBase.JsonResult( new
		{
			refreshed = true,
			gameObject = go.Name
		} );
	}

	// ──────────────────────────────────────────────
	//  set_network_flags
	// ──────────────────────────────────────────────

	[Tool( "set_network_flags", "Set network flags on a networked GameObject (host only). Flags: NoInterpolation, NoPositionSync, NoRotationSync, NoScaleSync.", RequiresMainThread = true )]
	[Param( "name", "Name of the GameObject.", Required = false )]
	[Param( "guid", "GUID of the GameObject.", Required = false )]
	[Param( "flags", "Comma-separated flags: 'None', 'NoInterpolation', 'NoPositionSync', 'NoRotationSync', 'NoScaleSync'.", Required = true )]
	[Param( "always_transmit", "Whether updates are always transmitted to clients regardless of visibility.", Required = false, Type = "boolean" )]
	public static object SetNetworkFlags( JsonElement args )
	{
		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );
		if ( !go.Network.Active )
			return ToolHandlerBase.ErrorResult( $"'{go.Name}' is not networked." );

		var flagsStr = ToolHandlerBase.RequireString( args, "flags" );
		var flags = NetworkFlags.None;
		foreach ( var f in flagsStr.Split( ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) )
		{
			if ( !Enum.TryParse<NetworkFlags>( f, true, out var parsed ) )
				return ToolHandlerBase.ErrorResult( $"Unknown NetworkFlags value: {f}" );
			flags |= parsed;
		}

		go.Network.Flags = flags;

		if ( args.TryGetProperty( "always_transmit", out _ ) )
			go.Network.AlwaysTransmit = ToolHandlerBase.GetBool( args, "always_transmit", go.Network.AlwaysTransmit );

		return ToolHandlerBase.JsonResult( new
		{
			set = true,
			gameObject = go.Name,
			flags = go.Network.Flags.ToString(),
			alwaysTransmit = go.Network.AlwaysTransmit
		} );
	}

	// ──────────────────────────────────────────────
	//  add_network_helper
	// ──────────────────────────────────────────────

	[Tool( "add_network_helper", "Add a NetworkHelper component to a GameObject (creates lobby + assigns player prefabs).", RequiresMainThread = true )]
	[Param( "name", "Name of the target GameObject.", Required = false )]
	[Param( "guid", "GUID of the target GameObject.", Required = false )]
	[Param( "create_go", "Create a new GameObject if name/guid not provided. Default: false.", Required = false, Type = "boolean", Default = "false" )]
	[Param( "go_name", "Name for the new GameObject (if create_go=true). Default: 'NetworkHelper'.", Required = false )]
	[Param( "start_server", "Whether this helper should create a server. Default: true.", Required = false, Type = "boolean" )]
	[Param( "player_prefab", "Path to player prefab (.prefab).", Required = false )]
	public static object AddNetworkHelper( JsonElement args )
	{
		var createGO = ToolHandlerBase.GetBool( args, "create_go", false );
		var goName = ToolHandlerBase.GetString( args, "go_name", "NetworkHelper" );

		GameObject go;
		if ( createGO )
		{
			go = SceneHelpers.CreateInScene( goName );
		}
		else
		{
			go = ResolveGO( args );
			if ( go == null ) return ToolHandlerBase.ErrorResult( "GameObject not found. Provide name/guid or set create_go=true." );
		}

		var existing = go.Components.Get<NetworkHelper>();
		if ( existing != null )
			return ToolHandlerBase.ErrorResult( $"'{go.Name}' already has a NetworkHelper." );

		var comp = go.Components.Create<NetworkHelper>();

		if ( args.TryGetProperty( "start_server", out _ ) )
			comp.StartServer = ToolHandlerBase.GetBool( args, "start_server", comp.StartServer );

		var prefabPath = ToolHandlerBase.GetString( args, "player_prefab" );
		if ( !string.IsNullOrEmpty( prefabPath ) )
		{
			var prefab = GameObject.GetPrefab( prefabPath );
			if ( prefab == null )
				return ToolHandlerBase.ErrorResult( $"Prefab not found: {prefabPath}" );
			comp.PlayerPrefab = prefab;
		}

		return ToolHandlerBase.JsonResult( new
		{
			added = true,
			gameObject = go.Name,
			gameObjectGuid = go.Id.ToString(),
			componentGuid = comp.Id.ToString(),
			startServer = comp.StartServer,
			playerPrefab = comp.PlayerPrefab?.Name
		} );
	}

	// ──────────────────────────────────────────────
	//  configure_network_helper
	// ──────────────────────────────────────────────

	[Tool( "configure_network_helper", "Modify an existing NetworkHelper component.", RequiresMainThread = true )]
	[Param( "name", "Name of GameObject with NetworkHelper.", Required = false )]
	[Param( "guid", "GUID of GameObject with NetworkHelper.", Required = false )]
	[Param( "start_server", "Whether this helper creates a server.", Required = false, Type = "boolean" )]
	[Param( "player_prefab", "Path to player prefab (.prefab).", Required = false )]
	[Param( "spawn_point_guids", "Comma-separated GameObject GUIDs to use as spawn points.", Required = false )]
	public static object ConfigureNetworkHelper( JsonElement args )
	{
		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );

		var comp = go.Components.Get<NetworkHelper>();
		if ( comp == null )
			return ToolHandlerBase.ErrorResult( $"'{go.Name}' has no NetworkHelper." );

		if ( args.TryGetProperty( "start_server", out _ ) )
			comp.StartServer = ToolHandlerBase.GetBool( args, "start_server", comp.StartServer );

		var prefabPath = ToolHandlerBase.GetString( args, "player_prefab" );
		if ( !string.IsNullOrEmpty( prefabPath ) )
		{
			var prefab = GameObject.GetPrefab( prefabPath );
			if ( prefab == null )
				return ToolHandlerBase.ErrorResult( $"Prefab not found: {prefabPath}" );
			comp.PlayerPrefab = prefab;
		}

		var spawnsStr = ToolHandlerBase.GetString( args, "spawn_point_guids" );
		if ( !string.IsNullOrEmpty( spawnsStr ) )
		{
			var scene = SceneHelpers.ResolveActiveScene();
			if ( scene == null ) return ToolHandlerBase.ErrorResult( "No active scene." );
			var list = new System.Collections.Generic.List<GameObject>();
			foreach ( var g in spawnsStr.Split( ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) )
			{
				var sp = SceneHelpers.FindByGuidOrName( scene, g, null );
				if ( sp != null ) list.Add( sp );
			}
			comp.SpawnPoints = list;
		}

		return ToolHandlerBase.JsonResult( new
		{
			configured = true,
			gameObject = go.Name,
			startServer = comp.StartServer,
			playerPrefab = comp.PlayerPrefab?.Name,
			spawnPointCount = comp.SpawnPoints?.Count ?? 0
		} );
	}

	// ──────────────────────────────────────────────
	//  create_lobby
	// ──────────────────────────────────────────────

	[Tool( "create_lobby", "Create a default networking lobby (host).", RequiresMainThread = true )]
	public static object CreateLobby( JsonElement args )
	{
		try
		{
			Networking.CreateLobby( new Sandbox.Network.LobbyConfig() );
			return ToolHandlerBase.JsonResult( new
			{
				created = true,
				isHost = Networking.IsHost,
				isActive = Networking.IsActive
			} );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Lobby creation failed: {ex.Message}" );
		}
	}

	// ──────────────────────────────────────────────
	//  disconnect_network
	// ──────────────────────────────────────────────

	[Tool( "disconnect_network", "Disconnect from the current multiplayer session.", RequiresMainThread = true )]
	public static object DisconnectNetwork( JsonElement args )
	{
		Networking.Disconnect();
		return ToolHandlerBase.JsonResult( new
		{
			disconnected = true,
			isActive = Networking.IsActive
		} );
	}

	// ──────────────────────────────────────────────
	//  set_network_data
	// ──────────────────────────────────────────────

	[Tool( "set_network_data", "Set lobby/server metadata that other players can query.", RequiresMainThread = true )]
	[Param( "key", "Metadata key.", Required = true )]
	[Param( "value", "Metadata value.", Required = true )]
	public static object SetNetworkData( JsonElement args )
	{
		var key = ToolHandlerBase.RequireString( args, "key" );
		var value = ToolHandlerBase.RequireString( args, "value" );

		Networking.SetData( key, value );
		var readBack = Networking.GetData( key, "" );

		return ToolHandlerBase.JsonResult( new
		{
			set = true,
			key,
			requestedValue = value,
			currentValue = readBack
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
}
