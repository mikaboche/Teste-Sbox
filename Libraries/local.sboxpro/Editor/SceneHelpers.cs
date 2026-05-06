using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;
using Editor;

namespace SboxPro;

public static class SceneHelpers
{
	public static Scene ResolveActiveScene()
	{
		return SceneEditorSession.Active?.Scene ?? Game.ActiveScene;
	}

	public static IEnumerable<GameObject> WalkAll( Scene scene )
	{
		foreach ( var root in scene.Children )
			foreach ( var go in WalkSubtree( root ) )
				yield return go;
	}

	public static IEnumerable<GameObject> WalkSubtree( GameObject go )
	{
		yield return go;
		foreach ( var child in go.Children )
			foreach ( var sub in WalkSubtree( child ) )
				yield return sub;
	}

	public static GameObject FindByName( Scene scene, string name )
	{
		return WalkAll( scene )
			.FirstOrDefault( go => go.Name.Equals( name, StringComparison.OrdinalIgnoreCase ) );
	}

	public static GameObject FindByGuidOrName( Scene scene, string guid, string name )
	{
		if ( !string.IsNullOrEmpty( guid ) )
		{
			if ( Guid.TryParse( guid, out var parsed ) )
			{
				var found = scene.Directory.FindByGuid( parsed );
				if ( found != null )
					return found;
			}

			// GUID provided but not found — try walk in case directory missed disabled objects
			return WalkAll( scene )
				.FirstOrDefault( go => go.Id.ToString().Equals( guid, StringComparison.OrdinalIgnoreCase ) );
		}

		if ( !string.IsNullOrEmpty( name ) )
			return FindByName( scene, name );

		return null;
	}

	/// <summary>
	/// Create a GameObject attached to <paramref name="scene"/> at the root level.
	///
	/// Why this helper exists: <c>new GameObject( enabled, name )</c> in s&amp;box returns a
	/// freestanding object that is NOT in any scene's hierarchy. It will not appear in
	/// <c>scene.Directory</c>, will not be found by <c>FindByGuidOrName</c>, will not be
	/// saved when the scene is saved, and will be garbage-collected on the next pass.
	/// <c>Scene.CreateObject(bool)</c> is the canonical API — it creates the GameObject
	/// already attached to the scene root, surviving queries and saves.
	///
	/// All <c>create_*</c> tools should funnel through here unless they're explicitly
	/// parenting to an existing scene object (in which case they can use
	/// <c>new GameObject(enabled, name) {{ Parent = parent }}</c>, since the parent
	/// itself is in the scene).
	/// </summary>
	public static GameObject CreateInScene( Scene scene, string name, bool enabled = true )
	{
		var go = scene.CreateObject( enabled );
		if ( !string.IsNullOrEmpty( name ) )
			go.Name = name;
		return go;
	}

	/// <summary>Convenience overload — resolves the active scene first.</summary>
	public static GameObject CreateInScene( string name, bool enabled = true )
	{
		var scene = ResolveActiveScene();
		return scene == null ? null : CreateInScene( scene, name, enabled );
	}
}
