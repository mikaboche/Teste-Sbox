using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Sandbox;

namespace SboxPro;

public static class SboxProServer
{
	public const int Port = 8099;
	public const string Version = "1.0.0";

	public static bool IsRunning => _listener != null && _listener.IsListening;
	public static int SessionCount => _sessions.Count;
	public static event Action OnStateChanged;

	// [SkipHotload] tells HotloadManager not to walk these fields when substituting
	// IL across assembly reloads. Without it, hotload tries to traverse into
	// HttpListener's internal async state (e.g. _httpContext.Request._memoryBlob.
	// _result._asyncCallback inside each McpSession's SseResponse) and chokes
	// trying to resolve framework-internal lambdas — that crashed the engine on
	// every dev cycle (issue #05). Skipping these fields is safe because we
	// recreate them from scratch in Stop()/Start() at every init pass anyway.
	[SkipHotload] private static HttpListener _listener;
	[SkipHotload] private static CancellationTokenSource _cts;
	private static DateTime _startedAt;
	[SkipHotload] private static readonly ConcurrentDictionary<string, McpSession> _sessions = new();
	[SkipHotload] private static readonly ConcurrentDictionary<Guid, Task> _inflightTasks = new();

	internal static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		WriteIndented = false
	};

	public static void Start()
	{
		if ( IsRunning )
		{
			SboxProLog.Info( "Server", "Already running" );
			return;
		}

		try
		{
			_listener = new HttpListener();
			_listener.Prefixes.Add( $"http://localhost:{Port}/" );
			_listener.Prefixes.Add( $"http://127.0.0.1:{Port}/" );
			_listener.Start();

			_cts = new CancellationTokenSource();
			_startedAt = DateTime.UtcNow;

			Task.Run( () => ListenLoop( _cts.Token ) );

			SboxProLog.Info( "Server", $"MCP server started on port {Port}" );
			NotifyStateChanged();
		}
		catch ( Exception ex )
		{
			SboxProLog.Error( "Server", $"Failed to start: {ex.Message}" );
		}
	}

	public static void Stop()
	{
		try
		{
			_cts?.Cancel();
			try { _listener?.Stop(); } catch { }
			try { _listener?.Close(); } catch { }
			_listener = null;

			_inflightTasks.Clear();

			foreach ( var session in _sessions.Values )
			{
				try { session.Tcs.TrySetResult( true ); } catch { }
				try { session.SseResponse?.Close(); } catch { }
			}
			_sessions.Clear();

			SboxProLog.Info( "Server", "Stopped" );
			NotifyStateChanged();
		}
		catch ( Exception ex )
		{
			SboxProLog.Error( "Server", $"Error stopping: {ex.Message}" );
		}
	}

	public static double UptimeSeconds => IsRunning ? (DateTime.UtcNow - _startedAt).TotalSeconds : 0;

	/// <summary>
	/// Drops all <see cref="OnStateChanged"/> subscribers. Called from SboxProInit on
	/// every (re)init — same hotload-survival reason as SboxProLog.ClearSubscribers:
	/// dock widgets attach via lambdas that close over the previous assembly's
	/// generated display class; after hotload that IL is gone and invoking the stale
	/// delegate throws "Unable to find matching substitution for a lambda method."
	/// </summary>
	internal static void ClearSubscribers()
	{
		OnStateChanged = null;
	}

	private static async void NotifyStateChanged()
	{
		// Subscribers are dock widgets — they touch Qt UI which only the main
		// thread is allowed to mutate. The previous implementation used
		// GameTask.RunInThreadAsync which schedules onto a worker; from there
		// any Qt write deadlocks waiting for the main thread sync. During
		// hotload the main thread is busy swapping assemblies → the worker
		// task never yields → engine's "task hasn't yielded for 1000ms" guard
		// fires → engine kills the worker → MCP server dies → editor
		// destabilises. Marshal to the main thread before invoking. (#31)
		try
		{
			await GameTask.MainThread();
			OnStateChanged?.Invoke();
		}
		catch { /* main thread torn down — drop silently */ }
	}

	private static async Task ListenLoop( CancellationToken token )
	{
		while ( !token.IsCancellationRequested && _listener != null && _listener.IsListening )
		{
			try
			{
				var context = await _listener.GetContextAsync();
				_ = Task.Run( () => HandleContext( context ), token );
			}
			catch ( Exception ex ) when ( ex is not ObjectDisposedException )
			{
				if ( !token.IsCancellationRequested )
					SboxProLog.Error( "Server", $"Listen loop error: {ex.Message}" );
			}
		}
	}

	private static async Task HandleContext( HttpListenerContext context )
	{
		var req = context.Request;
		var res = context.Response;

		res.Headers.Add( "Access-Control-Allow-Origin", "*" );
		res.Headers.Add( "Access-Control-Allow-Methods", "GET, POST, OPTIONS" );
		res.Headers.Add( "Access-Control-Allow-Headers", "*" );

		if ( req.HttpMethod == "OPTIONS" )
		{
			res.StatusCode = 200;
			res.Close();
			return;
		}

		try
		{
			var path = req.Url?.AbsolutePath ?? "";

			switch ( path )
			{
				case "/sse" when req.HttpMethod == "GET":
					await HandleSse( req, res );
					break;

				case "/message" when req.HttpMethod == "POST":
					await HandleMessage( req, res );
					break;

				case "/health" when req.HttpMethod == "GET":
					await HandleHealth( res );
					break;

				case "/tools" when req.HttpMethod == "GET":
					await HandleToolsList( res );
					break;

				case "/logs" when req.HttpMethod == "GET":
					await HandleLogs( req, res );
					break;

				default:
					res.StatusCode = 404;
					await WriteJson( res, new { error = "Not found" } );
					break;
			}
		}
		catch ( Exception ex )
		{
			SboxProLog.Error( "Server", $"Request error: {ex.Message}" );
			res.StatusCode = 500;
			try { res.Close(); } catch { }
		}
	}

	// ── SSE connection ────────────────────────────────────────────────

	private static async Task HandleSse( HttpListenerRequest req, HttpListenerResponse res )
	{
		var sessionId = Guid.NewGuid().ToString();
		var session = new McpSession { SessionId = sessionId, SseResponse = res };
		_sessions[sessionId] = session;

		res.ContentType = "text/event-stream";
		res.Headers.Add( "Cache-Control", "no-cache" );
		res.Headers.Add( "Connection", "keep-alive" );

		try
		{
			var msg = $"event: endpoint\ndata: /message?sessionId={sessionId}\n\n";
			var buffer = Encoding.UTF8.GetBytes( msg );
			await res.OutputStream.WriteAsync( buffer, 0, buffer.Length );
			await res.OutputStream.FlushAsync();

			SboxProLog.Info( "Server", $"SSE session created: {sessionId[..8]}..." );
			NotifyStateChanged();

			// Periodic SSE comment heartbeats let us detect client disconnects.
			while ( !session.Tcs.Task.IsCompleted )
			{
				await Task.Delay( 15_000 );
				if ( session.Tcs.Task.IsCompleted ) break;
				try
				{
					var ping = Encoding.UTF8.GetBytes( ": keepalive\n\n" );
					await res.OutputStream.WriteAsync( ping, 0, ping.Length );
					await res.OutputStream.FlushAsync();
				}
				catch
				{
					// Client disconnected — exit gracefully so finally cleans up.
					break;
				}
			}
		}
		catch ( Exception ex )
		{
			SboxProLog.Error( "Server", $"SSE error in {sessionId[..8]}: {ex.GetType().FullName}: {ex.Message}\nSTACK: {ex.StackTrace}" );
		}
		finally
		{
			_sessions.TryRemove( sessionId, out _ );
			try { res.Close(); } catch { }
			SboxProLog.Info( "Server", $"SSE session closed: {sessionId[..8]}... (sessions remaining: {_sessions.Count})" );
			NotifyStateChanged();
		}
	}

	// ── JSON-RPC message handler ──────────────────────────────────────

	private static async Task HandleMessage( HttpListenerRequest req, HttpListenerResponse res )
	{
		var sessionId = req.QueryString["sessionId"];
		if ( string.IsNullOrEmpty( sessionId ) || !_sessions.TryGetValue( sessionId, out var session ) )
		{
			res.StatusCode = 400;
			await WriteJson( res, new { error = "Invalid or missing sessionId" } );
			return;
		}

		using var reader = new StreamReader( req.InputStream, Encoding.UTF8 );
		var body = await reader.ReadToEndAsync();

		try
		{
			using var doc = JsonDocument.Parse( body );
			var root = doc.RootElement;
			string method = root.TryGetProperty( "method", out var m ) ? m.GetString() : null;
			object id = null;

			if ( root.TryGetProperty( "id", out var idProp ) )
			{
				if ( idProp.ValueKind == JsonValueKind.Number ) id = idProp.GetInt32();
				else if ( idProp.ValueKind == JsonValueKind.String ) id = idProp.GetString();
			}

			res.StatusCode = 202;
			res.Close();

			if ( id != null )
			{
				var bodyCopy = body;
				var idCopy = id;
				var methodCopy = method;
				var taskId = Guid.NewGuid();

				var task = GameTask.RunInThreadAsync( async () =>
				{
					try
					{
						await RpcDispatcher.ProcessRpcRequest( session, idCopy, methodCopy, bodyCopy, JsonOptions );
					}
					catch ( Exception ex )
					{
						SboxProLog.Error( "Server", $"RPC fault: {ex.Message}" );
						var errResponse = new
						{
							jsonrpc = "2.0",
							id = idCopy,
							result = (object)null,
							error = new { code = -32603, message = $"Internal error: {ex.Message}" }
						};
						var errJson = JsonSerializer.Serialize( errResponse, JsonOptions );
						await SendSseEvent( session, "message", errJson );
					}
					finally
					{
						_inflightTasks.TryRemove( taskId, out _ );
					}
				} );

				_inflightTasks[taskId] = task;
			}
			else if ( method == "notifications/initialized" )
			{
				session.Initialized = true;
				SboxProLog.Info( "Server", $"Session initialized: {sessionId[..8]}..." );
				NotifyStateChanged();
			}
		}
		catch ( Exception ex )
		{
			SboxProLog.Error( "Server", $"JSON-RPC parse error: {ex.Message}" );
		}
	}

	// ── SSE write ─────────────────────────────────────────────────────

	internal static async Task SendSseEvent( McpSession session, string eventName, string data )
	{
		if ( session.SseResponse == null || !session.SseResponse.OutputStream.CanWrite )
			return;

		try
		{
			var msg = $"event: {eventName}\ndata: {data}\n\n";
			var buffer = Encoding.UTF8.GetBytes( msg );
			await session.SseResponse.OutputStream.WriteAsync( buffer, 0, buffer.Length );
			await session.SseResponse.OutputStream.FlushAsync();
		}
		catch ( Exception ex )
		{
			SboxProLog.Warn( "Server", $"SSE write failed for {session.SessionId[..8]}: {ex.Message}" );
		}
	}

	// ── REST endpoints ────────────────────────────────────────────────

	private static async Task HandleHealth( HttpListenerResponse res )
	{
		var payload = new
		{
			status = "healthy",
			version = Version,
			uptime_seconds = Math.Round( UptimeSeconds, 1 ),
			tools = ToolRegistry.Count,
			sessions = SessionCount
		};

		res.StatusCode = 200;
		await WriteJson( res, payload );
	}

	private static async Task HandleToolsList( HttpListenerResponse res )
	{
		var payload = new { tools = ToolRegistry.GetAllSchemas() };
		res.StatusCode = 200;
		await WriteJson( res, payload );
	}

	private static async Task HandleLogs( HttpListenerRequest req, HttpListenerResponse res )
	{
		var countStr = req.QueryString["count"];
		var count = int.TryParse( countStr, out var n ) ? Math.Clamp( n, 1, 1000 ) : 100;
		var sevStr = req.QueryString["severity"]; // info | warning | error | all
		var sourceFilter = req.QueryString["source"];

		var entries = SboxProLog.Entries.AsEnumerable();
		if ( !string.IsNullOrEmpty( sevStr ) && sevStr != "all" )
		{
			if ( Enum.TryParse<LogSeverity>( sevStr, true, out var sev ) )
				entries = entries.Where( e => e.Severity == sev );
		}
		if ( !string.IsNullOrEmpty( sourceFilter ) )
			entries = entries.Where( e => e.Source != null && e.Source.Contains( sourceFilter, StringComparison.OrdinalIgnoreCase ) );

		var arr = entries.TakeLast( count )
			.Select( e => new {
				ts = e.Timestamp.ToString( "HH:mm:ss.fff" ),
				severity = e.Severity.ToString(),
				source = e.Source,
				message = e.Message
			} )
			.ToArray();

		res.StatusCode = 200;
		await WriteJson( res, new { total = SboxProLog.EntryCount, returned = arr.Length, entries = arr } );
	}

	private static async Task WriteJson( HttpListenerResponse res, object payload )
	{
		res.ContentType = "application/json";
		var json = JsonSerializer.Serialize( payload, JsonOptions );
		var buffer = Encoding.UTF8.GetBytes( json );
		res.ContentLength64 = buffer.Length;
		await res.OutputStream.WriteAsync( buffer, 0, buffer.Length );
		res.Close();
	}
}
