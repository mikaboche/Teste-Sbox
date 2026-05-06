using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace SboxPro;

/// <summary>
/// Read/write .scene files with round-trip fidelity.
///
/// Scene format:
/// {
///   "__guid": "...",
///   "GameObjects": [ ... ],
///   "SceneProperties": { ... },
///   "ResourceVersion": 3,
///   "Title": null,
///   "Description": null,
///   "__references": [],
///   "__version": 3
/// }
/// </summary>
public static class SceneSerializer
{
	/// <summary>
	/// Load a .scene file into a mutable JsonNode tree.
	/// Returns the root JsonObject.
	/// </summary>
	public static JsonObject Load( string path )
	{
		var node = SerializationHelpers.ReadFile( path );
		if ( node is not JsonObject root )
			throw new System.InvalidOperationException( $"Scene file is not a JSON object: {path}" );
		return root;
	}

	/// <summary>
	/// Save a scene JsonObject back to disk.
	/// </summary>
	public static void Save( string path, JsonObject scene )
	{
		SerializationHelpers.WriteFile( path, scene );
	}

	/// <summary>
	/// Get the scene's root __guid.
	/// </summary>
	public static string GetGuid( JsonObject scene )
	{
		return SerializationHelpers.GetString( scene, "__guid" );
	}

	/// <summary>
	/// Get the GameObjects array from the scene.
	/// </summary>
	public static JsonArray GetGameObjects( JsonObject scene )
	{
		return SerializationHelpers.GetProperty( scene, "GameObjects" ) as JsonArray;
	}

	/// <summary>
	/// Get SceneProperties object.
	/// </summary>
	public static JsonObject GetSceneProperties( JsonObject scene )
	{
		return SerializationHelpers.GetProperty( scene, "SceneProperties" ) as JsonObject;
	}

	/// <summary>
	/// Find a GameObject by guid anywhere in the scene hierarchy.
	/// </summary>
	public static JsonNode FindGameObject( JsonObject scene, string guid )
	{
		var gameObjects = GetGameObjects( scene );
		return SerializationHelpers.FindByGuid( gameObjects, guid );
	}

	/// <summary>
	/// Get all GameObjects as a flat list (includes children recursively).
	/// </summary>
	public static List<JsonNode> GetAllGameObjects( JsonObject scene )
	{
		var result = new List<JsonNode>();
		var gameObjects = GetGameObjects( scene );
		if ( gameObjects != null )
			CollectGameObjects( gameObjects, result );
		return result;
	}

	private static void CollectGameObjects( JsonArray array, List<JsonNode> result )
	{
		foreach ( var go in array )
		{
			if ( go == null ) continue;
			result.Add( go );
			var children = SerializationHelpers.GetProperty( go, "Children" ) as JsonArray;
			if ( children != null )
				CollectGameObjects( children, result );
		}
	}

	/// <summary>
	/// Add a new root-level GameObject to the scene.
	/// </summary>
	public static void AddGameObject( JsonObject scene, JsonObject gameObject )
	{
		var gameObjects = GetGameObjects( scene );
		if ( gameObjects == null )
		{
			gameObjects = new JsonArray();
			scene["GameObjects"] = gameObjects;
		}
		gameObjects.Add( gameObject );
	}

	/// <summary>
	/// Remove a root-level GameObject by guid. Returns true if found and removed.
	/// </summary>
	public static bool RemoveGameObject( JsonObject scene, string guid )
	{
		var gameObjects = GetGameObjects( scene );
		if ( gameObjects == null ) return false;

		for ( int i = 0; i < gameObjects.Count; i++ )
		{
			var go = gameObjects[i];
			if ( go == null ) continue;
			var goGuid = SerializationHelpers.GetString( go, "__guid" );
			if ( goGuid == guid )
			{
				gameObjects.RemoveAt( i );
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Set a property on a GameObject or Component node.
	/// </summary>
	public static void SetProperty( JsonObject node, string name, JsonNode value )
	{
		node[name] = value;
	}
}
