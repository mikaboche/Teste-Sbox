using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace SboxPro;

/// <summary>
/// Read/write .prefab files with round-trip fidelity.
///
/// Prefab format:
/// {
///   "RootObject": {
///     "__guid": "...",
///     "__version": 2,
///     "Name": "...",
///     "Components": [ ... ],
///     "Children": [ ... ]
///   },
///   "__references": [],
///   "__version": 1
/// }
/// </summary>
public static class PrefabSerializer
{
	/// <summary>
	/// Load a .prefab file into a mutable JsonNode tree.
	/// Returns the root JsonObject (the file-level object containing "RootObject").
	/// </summary>
	public static JsonObject Load( string path )
	{
		var node = SerializationHelpers.ReadFile( path );
		if ( node is not JsonObject root )
			throw new System.InvalidOperationException( $"Prefab file is not a JSON object: {path}" );
		return root;
	}

	/// <summary>
	/// Save a prefab JsonObject back to disk.
	/// </summary>
	public static void Save( string path, JsonObject prefab )
	{
		SerializationHelpers.WriteFile( path, prefab );
	}

	/// <summary>
	/// Get the RootObject (the top-level GameObject of the prefab).
	/// </summary>
	public static JsonObject GetRootObject( JsonObject prefab )
	{
		return SerializationHelpers.GetProperty( prefab, "RootObject" ) as JsonObject;
	}

	/// <summary>
	/// Get the root object's __guid.
	/// </summary>
	public static string GetGuid( JsonObject prefab )
	{
		var root = GetRootObject( prefab );
		if ( root == null ) return null;
		return SerializationHelpers.GetString( root, "__guid" );
	}

	/// <summary>
	/// Get the Children array from the RootObject.
	/// </summary>
	public static JsonArray GetChildren( JsonObject prefab )
	{
		var root = GetRootObject( prefab );
		if ( root == null ) return null;
		return SerializationHelpers.GetProperty( root, "Children" ) as JsonArray;
	}

	/// <summary>
	/// Get Components array from the RootObject.
	/// </summary>
	public static JsonArray GetComponents( JsonObject prefab )
	{
		var root = GetRootObject( prefab );
		if ( root == null ) return null;
		return SerializationHelpers.GetProperty( root, "Components" ) as JsonArray;
	}

	/// <summary>
	/// Find a GameObject by guid anywhere in the prefab hierarchy.
	/// Searches RootObject and all Children recursively.
	/// </summary>
	public static JsonNode FindGameObject( JsonObject prefab, string guid )
	{
		var root = GetRootObject( prefab );
		if ( root == null ) return null;

		var rootGuid = SerializationHelpers.GetString( root, "__guid" );
		if ( rootGuid == guid ) return root;

		var children = SerializationHelpers.GetProperty( root, "Children" ) as JsonArray;
		if ( children == null ) return null;
		return SerializationHelpers.FindByGuid( children, guid );
	}

	/// <summary>
	/// Get all GameObjects as a flat list (RootObject + children recursively).
	/// </summary>
	public static List<JsonNode> GetAllGameObjects( JsonObject prefab )
	{
		var result = new List<JsonNode>();
		var root = GetRootObject( prefab );
		if ( root == null ) return result;

		result.Add( root );
		var children = SerializationHelpers.GetProperty( root, "Children" ) as JsonArray;
		if ( children != null )
			CollectGameObjects( children, result );
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
	/// Add a child GameObject to the RootObject.
	/// </summary>
	public static void AddChild( JsonObject prefab, JsonObject child )
	{
		var root = GetRootObject( prefab );
		if ( root == null ) return;

		var children = SerializationHelpers.GetProperty( root, "Children" ) as JsonArray;
		if ( children == null )
		{
			children = new JsonArray();
			root["Children"] = children;
		}
		children.Add( child );
	}

	/// <summary>
	/// Add a Component to the RootObject.
	/// </summary>
	public static void AddComponent( JsonObject prefab, JsonObject component )
	{
		var root = GetRootObject( prefab );
		if ( root == null ) return;

		var components = SerializationHelpers.GetProperty( root, "Components" ) as JsonArray;
		if ( components == null )
		{
			components = new JsonArray();
			root["Components"] = components;
		}
		components.Add( component );
	}
}
