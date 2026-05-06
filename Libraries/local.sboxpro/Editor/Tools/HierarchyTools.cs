using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Sandbox;
using Editor;

namespace SboxPro;

public static class HierarchyTools
{
	// ──────────────────────────────────────────────
	//  get_selected_objects
	// ──────────────────────────────────────────────

	[Tool( "get_selected_objects", "Returns the currently selected GameObjects in the editor.", RequiresMainThread = true )]
	public static object GetSelectedObjects()
	{
		var session = SceneEditorSession.Active;
		if ( session == null )
			return ToolHandlerBase.ErrorResult( "No active editor session" );

		var selected = session.Selection
			.OfType<GameObject>()
			.Select( go => new
			{
				name = go.Name,
				guid = go.Id.ToString(),
				enabled = go.Enabled,
				worldPosition = GameObjectTools.FormatVector3( go.WorldPosition ),
				components = go.Components.GetAll().Select( c => c.GetType().Name ).ToArray()
			} )
			.ToArray();

		return ToolHandlerBase.JsonResult( new
		{
			count = selected.Length,
			objects = selected
		} );
	}

	// ──────────────────────────────────────────────
	//  focus_object
	// ──────────────────────────────────────────────

	[Tool( "focus_object", "Move the editor camera to focus on a specific GameObject.", RequiresMainThread = true )]
	[Param( "name", "Name of the GameObject to focus.", Required = false )]
	[Param( "guid", "GUID of the GameObject to focus.", Required = false )]
	public static object FocusObject( JsonElement args )
	{
		var name = ToolHandlerBase.GetString( args, "name" );
		var guid = ToolHandlerBase.GetString( args, "guid" );

		if ( string.IsNullOrEmpty( name ) && string.IsNullOrEmpty( guid ) )
			return ToolHandlerBase.ErrorResult( "Provide either 'name' or 'guid'" );

		var scene = SceneHelpers.ResolveActiveScene();
		if ( scene == null )
			return ToolHandlerBase.ErrorResult( "No active scene" );

		var go = SceneHelpers.FindByGuidOrName( scene, guid, name );
		if ( go == null )
			return ToolHandlerBase.ErrorResult( $"GameObject not found: {guid ?? name}" );

		var session = SceneEditorSession.Active;
		if ( session == null )
			return ToolHandlerBase.ErrorResult( "No active editor session" );

		session.Selection.Set( go );
		session.FrameTo( go.GetBounds() );

		return ToolHandlerBase.JsonResult( new
		{
			focused = true,
			name = go.Name,
			guid = go.Id.ToString(),
			worldPosition = GameObjectTools.FormatVector3( go.WorldPosition )
		} );
	}

	// ──────────────────────────────────────────────
	//  frame_selection
	// ──────────────────────────────────────────────

	[Tool( "frame_selection", "Frame the current editor selection in the viewport.", RequiresMainThread = true )]
	public static object FrameSelection()
	{
		var session = SceneEditorSession.Active;
		if ( session == null )
			return ToolHandlerBase.ErrorResult( "No active editor session" );

		var selected = session.Selection.OfType<GameObject>().ToList();

		if ( selected.Count == 0 )
			return ToolHandlerBase.ErrorResult( "No GameObjects selected" );

		var combined = selected[0].GetBounds();
		for ( int i = 1; i < selected.Count; i++ )
			combined = combined.AddBBox( selected[i].GetBounds() );

		session.FrameTo( combined );

		return ToolHandlerBase.JsonResult( new
		{
			framed = true,
			objectCount = selected.Count,
			objects = selected.Select( go => new { name = go.Name, guid = go.Id.ToString() } ).ToArray()
		} );
	}
}
