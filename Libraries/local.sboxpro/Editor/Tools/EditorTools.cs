using System;
using System.Text.Json;
using Sandbox;
using Editor;

namespace SboxPro;

public static class EditorTools
{
	// ──────────────────────────────────────────────
	//  start_play
	// ──────────────────────────────────────────────

	[Tool( "start_play", "Start play mode for the active scene editor session.", RequiresMainThread = true )]
	public static object StartPlay( JsonElement args )
	{
		var session = SceneEditorSession.Active;
		if ( session == null )
			return ToolHandlerBase.ErrorResult( "No active scene editor session." );

		if ( session.IsPlaying )
			return ToolHandlerBase.ErrorResult( "Session is already playing." );

		EditorScene.Play( session );

		return ToolHandlerBase.JsonResult( new
		{
			started = true,
			isPlaying = session.IsPlaying,
			sceneName = session.Scene?.Name
		} );
	}

	// ──────────────────────────────────────────────
	//  stop_play
	// ──────────────────────────────────────────────

	[Tool( "stop_play", "Stop play mode and return to scene editing.", RequiresMainThread = true )]
	public static object StopPlay( JsonElement args )
	{
		var session = SceneEditorSession.Active;
		if ( session == null )
			return ToolHandlerBase.ErrorResult( "No active scene editor session." );

		if ( !session.IsPlaying )
			return ToolHandlerBase.ErrorResult( "Session is not playing." );

		EditorScene.Stop();

		return ToolHandlerBase.JsonResult( new
		{
			stopped = true,
			isPlaying = session.IsPlaying
		} );
	}

	// ──────────────────────────────────────────────
	//  toggle_play
	// ──────────────────────────────────────────────

	[Tool( "toggle_play", "Toggle play mode on/off for the active session.", RequiresMainThread = true )]
	public static object TogglePlay( JsonElement args )
	{
		var session = SceneEditorSession.Active;
		if ( session == null )
			return ToolHandlerBase.ErrorResult( "No active scene editor session." );

		var wasPlaying = session.IsPlaying;
		EditorScene.TogglePlay();

		return ToolHandlerBase.JsonResult( new
		{
			toggled = true,
			wasPlaying,
			isPlaying = session.IsPlaying
		} );
	}

	// ──────────────────────────────────────────────
	//  get_play_state
	// ──────────────────────────────────────────────

	[Tool( "get_play_state", "Get the current play/edit state of the active session." )]
	public static object GetPlayState( JsonElement args )
	{
		var session = SceneEditorSession.Active;
		if ( session == null )
			return ToolHandlerBase.JsonResult( new { hasSession = false, isPlaying = false } );

		return ToolHandlerBase.JsonResult( new
		{
			hasSession = true,
			isPlaying = session.IsPlaying,
			isPrefabSession = session.IsPrefabSession,
			hasUnsavedChanges = session.HasUnsavedChanges,
			sceneName = session.Scene?.Name,
			openSessionsCount = SceneEditorSession.All?.Count ?? 0
		} );
	}

	// ──────────────────────────────────────────────
	//  undo
	// ──────────────────────────────────────────────

	[Tool( "undo", "Undo the last edit operation in the active session.", RequiresMainThread = true )]
	public static object Undo( JsonElement args )
	{
		var session = SceneEditorSession.Active;
		if ( session == null )
			return ToolHandlerBase.ErrorResult( "No active scene editor session." );

		var sys = session.UndoSystem;
		if ( sys == null )
			return ToolHandlerBase.ErrorResult( "Active session has no UndoSystem." );

		var success = sys.Undo();
		return ToolHandlerBase.JsonResult( new
		{
			success,
			backStackSize = sys.Back?.Count ?? 0,
			forwardStackSize = sys.Forward?.Count ?? 0
		} );
	}

	// ──────────────────────────────────────────────
	//  redo
	// ──────────────────────────────────────────────

	[Tool( "redo", "Redo the last undone edit operation in the active session.", RequiresMainThread = true )]
	public static object Redo( JsonElement args )
	{
		var session = SceneEditorSession.Active;
		if ( session == null )
			return ToolHandlerBase.ErrorResult( "No active scene editor session." );

		var sys = session.UndoSystem;
		if ( sys == null )
			return ToolHandlerBase.ErrorResult( "Active session has no UndoSystem." );

		var success = sys.Redo();
		return ToolHandlerBase.JsonResult( new
		{
			success,
			backStackSize = sys.Back?.Count ?? 0,
			forwardStackSize = sys.Forward?.Count ?? 0
		} );
	}

	// ──────────────────────────────────────────────
	//  new_scene
	// ──────────────────────────────────────────────

	[Tool( "new_scene", "Open a new empty scene in the editor (replaces current session). Discards unsaved changes.", RequiresMainThread = true )]
	public static object NewScene( JsonElement args )
	{
		EditorScene.NewScene();

		var session = SceneEditorSession.Active;
		return ToolHandlerBase.JsonResult( new
		{
			created = true,
			sceneName = session?.Scene?.Name,
			isPlaying = session?.IsPlaying ?? false
		} );
	}

	// ──────────────────────────────────────────────
	//  discard_scene_changes
	// ──────────────────────────────────────────────

	[Tool( "discard_scene_changes", "Discard unsaved changes and reload the active scene from disk.", RequiresMainThread = true )]
	public static object DiscardSceneChanges( JsonElement args )
	{
		var session = SceneEditorSession.Active;
		if ( session == null )
			return ToolHandlerBase.ErrorResult( "No active scene editor session." );

		EditorScene.Discard();

		return ToolHandlerBase.JsonResult( new
		{
			discarded = true,
			hasUnsavedChanges = SceneEditorSession.Active?.HasUnsavedChanges ?? false
		} );
	}
}
