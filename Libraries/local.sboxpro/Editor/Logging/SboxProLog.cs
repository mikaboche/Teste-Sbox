using System;
using System.Collections.Generic;
using Editor;
using Sandbox;

namespace SboxPro;

public static class SboxProLog
{
	private static readonly List<LogEntry> _entries = new();
	private static readonly object _lock = new();
	private static int _maxEntries = 1000;
	private static bool _engineHooked;

	public static event Action<LogEntry> OnEntry;

	public static int MaxEntries
	{
		get => _maxEntries;
		set => _maxEntries = Math.Max( 100, value );
	}

	public static int EntryCount
	{
		get { lock ( _lock ) return _entries.Count; }
	}

	public static IReadOnlyList<LogEntry> Entries
	{
		get
		{
			lock ( _lock )
				return _entries.ToArray();
		}
	}

	public static void HookEngineConsole()
	{
		if ( _engineHooked )
			return;

		EditorUtility.AddLogger( OnEngineLog );
		_engineHooked = true;
	}

	public static void UnhookEngineConsole()
	{
		if ( !_engineHooked )
			return;

		EditorUtility.RemoveLogger( OnEngineLog );
		_engineHooked = false;
	}

	private static void OnEngineLog( LogEvent e )
	{
		var severity = e.Level switch
		{
			LogLevel.Trace => LogSeverity.Trace,
			LogLevel.Warn => LogSeverity.Warning,
			LogLevel.Error => LogSeverity.Error,
			_ => LogSeverity.Info
		};

		string stack = null;
		if ( severity >= LogSeverity.Warning )
			stack = e.Stack;

		AddInternal( new LogEntry
		{
			Timestamp = e.Time,
			Severity = severity,
			Source = "Engine",
			Message = e.Message,
			LoggerName = e.Logger,
			StackTrace = stack
		} );
	}

	public static void Info( string source, string message )
	{
		Add( LogSeverity.Info, source, message );
		ForwardToEngine( () => Log.Info( $"[SboxPro] [{source}] {message}" ) );
	}

	public static void Warn( string source, string message )
	{
		Add( LogSeverity.Warning, source, message );
		ForwardToEngine( () => Log.Warning( $"[SboxPro] [{source}] {message}" ) );
	}

	/// <summary>Alias for <see cref="Warn"/> — engine convention is Log.Warning,
	/// some call sites expect that name. Keep both for compatibility.</summary>
	public static void Warning( string source, string message ) => Warn( source, message );

	public static void Error( string source, string message )
	{
		Add( LogSeverity.Error, source, message );
		ForwardToEngine( () => Log.Error( $"[SboxPro] [{source}] {message}" ) );
	}

	/// <summary>
	/// Forwards a log call to the engine. The engine's Log.Info/Warning/Error
	/// require main-thread allocation; calling them directly from a worker
	/// thread (e.g. inside the SSE listener loop) throws
	/// "Alloc must be called on the main thread!".
	/// GameTask.RunInThreadAsync(action) is misleadingly named — it dispatches
	/// the action onto the main thread queue. Same pattern Ozmium uses.
	/// </summary>
	private static async void ForwardToEngine( Action action )
	{
		try { await GameTask.RunInThreadAsync( action ); }
		catch { /* main thread torn down (shutdown) — drop silently */ }
	}

	public static void Clear()
	{
		lock ( _lock )
			_entries.Clear();
	}

	/// <summary>
	/// Drops every subscriber to <see cref="OnEntry"/>. Called from <c>SboxProInit</c>
	/// at the start of each (re)init pass. The reason is hotload-specific: when the
	/// editor reloads our assembly, dock widgets that subscribed via lambda close
	/// over <c>this</c> AND a compiler-generated display class that lives in the
	/// previous assembly. The static event keeps the delegate alive, but the IL
	/// behind the lambda is gone — next invocation throws "Unable to find matching
	/// substitution for a lambda method." Wiping subscribers on init guarantees
	/// only fresh post-hotload widgets fire.
	/// </summary>
	internal static void ClearSubscribers()
	{
		OnEntry = null;
	}

	private static void Add( LogSeverity severity, string source, string message )
	{
		string stack = null;
		if ( severity >= LogSeverity.Warning )
			stack = Environment.StackTrace;

		AddInternal( new LogEntry
		{
			Timestamp = DateTime.Now,
			Severity = severity,
			Source = source,
			Message = message,
			StackTrace = stack
		} );
	}

	private static void AddInternal( LogEntry entry )
	{
		lock ( _lock )
		{
			_entries.Add( entry );
			while ( _entries.Count > _maxEntries )
				_entries.RemoveAt( 0 );
		}

		// Fire OnEntry on the main thread so subscribers can safely touch the UI.
		// Most subscribers are dock widgets; firing on a worker thread would
		// throw "Alloc must be called on the main thread!" inside their handler,
		// which would itself become a log → cascade → editor crash.
		var snapshot = entry;
		FireOnEntryOnMainThread( snapshot );
	}

	private static async void FireOnEntryOnMainThread( LogEntry entry )
	{
		// Method name is correct — implementation was wrong. Was
		// `GameTask.RunInThreadAsync` (worker), which made Qt-touching dock
		// subscribers deadlock waiting for main-thread sync, exactly the
		// crash signature documented in #31. Use `GameTask.MainThread()` so
		// invocation lands on the UI thread where Qt mutation is legal.
		try
		{
			await GameTask.MainThread();
			OnEntry?.Invoke( entry );
		}
		catch { /* main thread torn down — drop silently */ }
	}
}
