using System;
using Editor;
using Sandbox;

namespace SboxPro;

public static class SboxProInit
{
	private static readonly object _initLock = new();
	private static DateTime _lastInitUtc = DateTime.MinValue;

	[EditorEvent.Hotload]
	private static void OnHotload()
	{
		Initialize();
	}

	static SboxProInit()
	{
		Initialize();
	}

	private static void Initialize()
	{
		// On hotload the static ctor AND the [EditorEvent.Hotload] both fire,
		// causing two near-simultaneous Stop()/Start() pairs. The second restart
		// races with the first server's still-running listener tasks and leaves
		// the HttpListener in a half-disposed state where every new SSE session
		// throws "Cannot access a disposed object" on the first write — that
		// was the entire reason the MCP client couldn't hold a session.
		// Dedupe inits within 1 second.
		lock ( _initLock )
		{
			var now = DateTime.UtcNow;
			if ( (now - _lastInitUtc).TotalSeconds < 1.0 ) return;
			_lastInitUtc = now;

			// NOTE: zombie-lambda cleanup (SboxProLog/Server.ClearSubscribers) is owned by
			// SboxProDock.BuildUI, NOT by Init. Reason: if init clears AFTER the dock has
			// already re-subscribed (order between [EditorEvent.Hotload] and Widget
			// reconstruction is undefined), we'd wipe the fresh handlers and the status
			// pill / log tab would silently stop updating. The dock clearing inside its
			// own constructor is order-safe — it always wipes-then-subscribes atomically.

			ToolRegistry.Initialize();
			// HookEngineConsole disabled: feedback loop (engine logs → OnEntry →
			// UI from worker → throws → engine logs → recurse → editor crash).
			// SboxProLog.HookEngineConsole();

			// Auto-install the bundled S&Box Pro skill into ~/.claude/skills/
			// so any Claude Code conversation activates it on s&box-related prompts.
			SkillInstaller.TryAutoInstall();

			if ( SboxProServer.IsRunning )
			{
				SboxProServer.Stop();
			}
			SboxProServer.Start();

			SboxProLog.Info( "Init", $"S&Box Pro v{SboxProServer.Version} — {ToolRegistry.Count} tools registered" );
		}
	}
}
