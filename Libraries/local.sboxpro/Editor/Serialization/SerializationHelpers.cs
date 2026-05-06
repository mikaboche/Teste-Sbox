using System.Text.Json;
using System.Text.Json.Nodes;
using Editor;

namespace SboxPro;

/// <summary>
/// JSON options and utilities for scene/prefab serialization.
/// S&Box scene files use 2-space indent, no camelCase, preserve property order.
/// </summary>
public static class SerializationHelpers
{
	internal static readonly JsonSerializerOptions SceneJsonOptions = new()
	{
		WriteIndented = true,
		PropertyNameCaseInsensitive = false,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true
	};

	internal static readonly JsonDocumentOptions DocOptions = new()
	{
		CommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true
	};

	internal static readonly JsonNodeOptions NodeOptions = new()
	{
		PropertyNameCaseInsensitive = false
	};

	internal static readonly JsonWriterOptions WriterOptions = new()
	{
		Indented = true,
		// S&Box uses no trailing commas and standard JSON escaping
	};

	/// <summary>
	/// Parse JSON text into a mutable JsonNode tree (preserves property order).
	/// </summary>
	public static JsonNode Parse( string json )
	{
		return JsonNode.Parse( json, NodeOptions, DocOptions );
	}

	/// <summary>
	/// Serialize a JsonNode back to indented JSON string matching S&Box formatting.
	/// </summary>
	public static string Serialize( JsonNode node )
	{
		return node.ToJsonString( SceneJsonOptions );
	}

	/// <summary>
	/// Read a JSON file into a mutable JsonNode tree.
	/// </summary>
	public static JsonNode ReadFile( string path )
	{
		var text = System.IO.File.ReadAllText( path );
		return Parse( text );
	}

	/// <summary>
	/// Write a JsonNode tree to file with S&Box-compatible formatting.
	/// </summary>
	public static void WriteFile( string path, JsonNode node )
	{
		var text = Serialize( node );
		System.IO.File.WriteAllText( path, text );

		// Editor.AssetSystem indexes assets at editor boot; runtime-created files are
		// invisible until RegisterFile pulls them in (issue #06). Best-effort: a
		// non-asset extension or path outside the project will throw, which is fine.
		try { AssetSystem.RegisterFile( path ); } catch { }
	}

	/// <summary>
	/// Safe property access that returns null if missing.
	/// </summary>
	public static JsonNode GetProperty( JsonNode node, string name )
	{
		if ( node is JsonObject obj && obj.ContainsKey( name ) )
			return obj[name];
		return null;
	}

	/// <summary>
	/// Get a string property value or default.
	/// </summary>
	public static string GetString( JsonNode node, string name, string defaultValue = null )
	{
		var prop = GetProperty( node, name );
		if ( prop == null ) return defaultValue;
		return prop.GetValue<string>();
	}

	/// <summary>
	/// Find a GameObject node by __guid in a GameObjects array.
	/// </summary>
	public static JsonNode FindByGuid( JsonArray gameObjects, string guid )
	{
		if ( gameObjects == null ) return null;

		foreach ( var go in gameObjects )
		{
			if ( go == null ) continue;
			var goGuid = GetString( go, "__guid" );
			if ( goGuid == guid ) return go;

			var children = GetProperty( go, "Children" ) as JsonArray;
			if ( children != null )
			{
				var found = FindByGuid( children, guid );
				if ( found != null ) return found;
			}
		}

		return null;
	}

	/// <summary>
	/// Find a Component node by __guid within a GameObject.
	/// </summary>
	public static JsonNode FindComponentByGuid( JsonNode gameObject, string guid )
	{
		var components = GetProperty( gameObject, "Components" ) as JsonArray;
		if ( components == null ) return null;

		foreach ( var comp in components )
		{
			if ( comp == null ) continue;
			var compGuid = GetString( comp, "__guid" );
			if ( compGuid == guid ) return comp;
		}

		return null;
	}

	/// <summary>
	/// Find a Component by __type within a GameObject.
	/// </summary>
	public static JsonNode FindComponentByType( JsonNode gameObject, string typeName )
	{
		var components = GetProperty( gameObject, "Components" ) as JsonArray;
		if ( components == null ) return null;

		foreach ( var comp in components )
		{
			if ( comp == null ) continue;
			var type = GetString( comp, "__type" );
			if ( type == typeName ) return comp;
		}

		return null;
	}
}
