using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Sandbox;

namespace SboxPro;

/// <summary>
/// Bridge between live runtime objects (GameObject, Component) and JsonNode trees.
/// Two directions:
///   Serialize: GameObject/Component → JsonObject  (for save_scene, create_prefab)
///   Convert:   JsonElement → typed C# value       (for set_property with resources, lists, refs)
/// </summary>
public static class RuntimeReflection
{
	// ──────────────────────────────────────────────
	//  Serialize direction: Runtime → JSON
	// ──────────────────────────────────────────────

	/// <summary>
	/// Serialize a full scene to a JsonObject matching the .scene file format.
	/// Must be called from the main thread.
	/// </summary>
	public static JsonObject SerializeScene( Scene scene )
	{
		var gameObjects = new JsonArray();
		foreach ( var go in scene.Children )
		{
			gameObjects.Add( SerializeGameObject( go ) );
		}

		var root = new JsonObject
		{
			["__guid"] = scene.Id.ToString(),
			["GameObjects"] = gameObjects,
			["SceneProperties"] = SerializeSceneProperties( scene ),
			["ResourceVersion"] = 3,
			["Title"] = null,
			["Description"] = null,
			["__references"] = new JsonArray(),
			["__version"] = 3
		};

		return root;
	}

	/// <summary>
	/// Serialize a GameObject as a prefab root (wraps in RootObject envelope).
	/// </summary>
	public static JsonObject SerializePrefab( GameObject root )
	{
		return new JsonObject
		{
			["__version"] = 0,
			["__referencedFiles"] = new JsonArray(),
			["RootObject"] = SerializeGameObject( root )
		};
	}

	/// <summary>
	/// Serialize a single GameObject to a JsonObject.
	/// Recursively includes Components and Children.
	/// </summary>
	public static JsonObject SerializeGameObject( GameObject go )
	{
		var obj = new JsonObject
		{
			["__guid"] = go.Id.ToString(),
			["__version"] = 2,
			["Name"] = go.Name,
			["Position"] = SerializeVector3( go.LocalPosition ),
			["Rotation"] = SerializeRotation( go.LocalRotation ),
			["Scale"] = SerializeVector3( go.LocalScale ),
			["Tags"] = SerializeTags( go.Tags ),
			["Enabled"] = go.Enabled,
			["Components"] = SerializeComponents( go ),
			["Children"] = SerializeChildren( go )
		};

		return obj;
	}

	private static JsonArray SerializeComponents( GameObject go )
	{
		var arr = new JsonArray();
		foreach ( var comp in go.Components.GetAll() )
		{
			arr.Add( SerializeComponent( comp ) );
		}
		return arr;
	}

	/// <summary>
	/// Serialize a Component to a JsonObject.
	/// Includes __type, __guid, __enabled, and all [Property]-marked members.
	/// </summary>
	public static JsonObject SerializeComponent( Component comp )
	{
		var obj = new JsonObject
		{
			["__type"] = TypeLibrary.GetType( comp.GetType() )?.FullName ?? comp.GetType().FullName,
			["__guid"] = comp.Id.ToString(),
			["__enabled"] = comp.Enabled
		};

		var typeDesc = TypeLibrary.GetType( comp.GetType() );
		if ( typeDesc == null ) return obj;

		foreach ( var prop in typeDesc.Properties )
		{
			if ( !prop.HasAttribute<PropertyAttribute>() ) continue;
			if ( !prop.CanRead ) continue;

			try
			{
				var value = prop.GetValue( comp );
				var jsonValue = SerializeValue( value, prop.PropertyType );
				if ( jsonValue != null )
					obj[prop.Name] = jsonValue;
			}
			catch
			{
				// Skip properties that throw on read
			}
		}

		return obj;
	}

	private static JsonArray SerializeChildren( GameObject go )
	{
		var arr = new JsonArray();
		foreach ( var child in go.Children )
		{
			arr.Add( SerializeGameObject( child ) );
		}
		return arr;
	}

	private static JsonObject SerializeSceneProperties( Scene scene )
	{
		// Most scene-physics/networking knobs moved out of Scene into
		// Sandbox.ProjectSettings.PhysicsSettings / Sandbox.ProjectSettings.Networking
		// in recent engine builds. We preserve only TimeScale (still scene-local) plus
		// the legacy keys with safe defaults, so saved scenes round-trip without
		// triggering CS0618 in our own compile.
		return new JsonObject
		{
			["TimeScale"] = scene.TimeScale,
			["FixedUpdateFrequency"] = 50,
			["MaxFixedUpdates"] = 5,
			["UseFixedUpdate"] = true,
			["PhysicsSubSteps"] = 1,
			["ThreadedAnimation"] = true,
			["NetworkFrequency"] = 30
		};
	}

	// ──────────────────────────────────────────────
	//  Value serialization helpers
	// ──────────────────────────────────────────────

	/// <summary>
	/// Serialize a C# value to a JsonNode, respecting S&Box scene format conventions.
	/// </summary>
	public static JsonNode SerializeValue( object value, Type type )
	{
		if ( value == null )
			return null;

		// Primitives
		if ( value is string s ) return JsonValue.Create( s );
		if ( value is bool b ) return JsonValue.Create( b );
		if ( value is int i ) return JsonValue.Create( i );
		if ( value is float f ) return JsonValue.Create( f );
		if ( value is double d ) return JsonValue.Create( d );
		if ( value is long l ) return JsonValue.Create( l );

		// Enums serialize as int in S&Box scene files
		if ( type.IsEnum )
			return JsonValue.Create( Convert.ToInt32( value ) );

		// S&Box value types — comma-separated string format
		if ( value is Vector3 v3 ) return JsonValue.Create( SerializeVector3( v3 ) );
		if ( value is Vector2 v2 ) return JsonValue.Create( $"{v2.x},{v2.y}" );
		if ( value is Rotation rot ) return JsonValue.Create( SerializeRotation( rot ) );
		if ( value is Angles ang ) return JsonValue.Create( $"{ang.pitch},{ang.yaw},{ang.roll}" );
		if ( value is Color col ) return JsonValue.Create( $"{col.r:F5},{col.g:F5},{col.b:F5},{col.a:F5}" );

		// Guid
		if ( value is Guid guid ) return JsonValue.Create( guid.ToString() );

		// Resource references → serialize as path string
		if ( value is Resource resource )
		{
			return JsonValue.Create( resource.ResourcePath ?? "" );
		}

		// GameObject reference → serialize as GUID
		if ( value is GameObject go )
		{
			return JsonValue.Create( go.Id.ToString() );
		}

		// Component reference → serialize as GUID
		if ( value is Component comp )
		{
			return JsonValue.Create( comp.Id.ToString() );
		}

		// Lists/collections
		if ( value is IList list )
		{
			var arr = new JsonArray();
			var elemType = type.IsGenericType ? type.GetGenericArguments()[0] : typeof( object );
			foreach ( var item in list )
			{
				var serialized = SerializeValue( item, elemType );
				arr.Add( serialized );
			}
			return arr;
		}

		// Fallback: try System.Text.Json serialization
		try
		{
			var json = JsonSerializer.Serialize( value );
			return JsonNode.Parse( json );
		}
		catch
		{
			return JsonValue.Create( value.ToString() );
		}
	}

	private static string SerializeVector3( Vector3 v )
	{
		return $"{v.x},{v.y},{v.z}";
	}

	private static string SerializeRotation( Rotation r )
	{
		return $"{r.x},{r.y},{r.z},{r.w}";
	}

	private static string SerializeTags( GameTags tags )
	{
		return string.Join( ",", tags.TryGetAll() );
	}

	// ──────────────────────────────────────────────
	//  Convert direction: JSON → Runtime
	// ──────────────────────────────────────────────

	/// <summary>
	/// Convert a JSON value to a typed C# value for property assignment.
	/// Handles: primitives, enums, vectors, rotations, colors,
	/// resource refs (via ResourceLibrary), GameObject/Component refs (via Scene.Directory).
	/// </summary>
	public static object ConvertValue( Type targetType, JsonElement value, Scene scene = null )
	{
		if ( value.ValueKind == JsonValueKind.Null )
			return null;

		// String
		if ( targetType == typeof( string ) )
			return value.GetString();

		// Bool — accept native JSON true/false AND string "true"/"false" forms.
		// MCP tools pass values via schema-typed strings, so {"value": "true"} arrives
		// as ValueKind.String. The old check returned false for ANY string, silently
		// failing every set_component_property bool call (issue #18 — discovered when
		// PlayerController.ThirdPerson refused to flip).
		if ( targetType == typeof( bool ) )
		{
			if ( value.ValueKind == JsonValueKind.True ) return true;
			if ( value.ValueKind == JsonValueKind.False ) return false;
			if ( value.ValueKind == JsonValueKind.String && bool.TryParse( value.GetString(), out var b ) )
				return b;
			if ( value.ValueKind == JsonValueKind.Number )
				return value.GetInt32() != 0;
			return false;
		}

		// Numeric types
		if ( targetType == typeof( int ) ) return value.GetInt32();
		if ( targetType == typeof( float ) ) return value.GetSingle();
		if ( targetType == typeof( double ) ) return value.GetDouble();
		if ( targetType == typeof( long ) ) return value.GetInt64();

		// Enum
		if ( targetType.IsEnum )
		{
			if ( value.ValueKind == JsonValueKind.Number )
				return Enum.ToObject( targetType, value.GetInt32() );
			if ( value.ValueKind == JsonValueKind.String )
				return Enum.Parse( targetType, value.GetString(), ignoreCase: true );
		}

		// Vector3 from "x,y,z" string
		if ( targetType == typeof( Vector3 ) )
		{
			if ( value.ValueKind == JsonValueKind.String )
				return ParseVector3( value.GetString() );
		}

		// Vector2 from "x,y" string
		if ( targetType == typeof( Vector2 ) )
		{
			if ( value.ValueKind == JsonValueKind.String )
			{
				var parts = value.GetString().Split( ',' );
				return new Vector2( float.Parse( parts[0] ), float.Parse( parts[1] ) );
			}
		}

		// Rotation from "x,y,z,w" string
		if ( targetType == typeof( Rotation ) )
		{
			if ( value.ValueKind == JsonValueKind.String )
				return ParseRotation( value.GetString() );
		}

		// Angles from "pitch,yaw,roll" string
		if ( targetType == typeof( Angles ) )
		{
			if ( value.ValueKind == JsonValueKind.String )
			{
				var parts = value.GetString().Split( ',' );
				return new Angles( float.Parse( parts[0] ), float.Parse( parts[1] ), float.Parse( parts[2] ) );
			}
		}

		// Color from "r,g,b,a" string
		if ( targetType == typeof( Color ) )
		{
			if ( value.ValueKind == JsonValueKind.String )
			{
				var parts = value.GetString().Split( ',' );
				return new Color( float.Parse( parts[0] ), float.Parse( parts[1] ),
					float.Parse( parts[2] ), parts.Length > 3 ? float.Parse( parts[3] ) : 1f );
			}
		}

		// Guid
		if ( targetType == typeof( Guid ) )
		{
			return Guid.Parse( value.GetString() );
		}

		// Resource references (Model, Material, Sound, etc.)
		if ( typeof( Resource ).IsAssignableFrom( targetType ) )
		{
			var path = value.GetString();
			return LoadResource( targetType, path );
		}

		// GameObject reference by GUID
		if ( targetType == typeof( GameObject ) && scene != null )
		{
			var guid = Guid.Parse( value.GetString() );
			return scene.Directory.FindByGuid( guid );
		}

		// Component reference by GUID
		if ( typeof( Component ).IsAssignableFrom( targetType ) && scene != null )
		{
			var guid = Guid.Parse( value.GetString() );
			return scene.Directory.FindComponentByGuid( guid );
		}

		// List<T>
		if ( targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof( List<> ) )
		{
			var elementType = targetType.GetGenericArguments()[0];
			var list = (IList)Activator.CreateInstance( targetType );
			if ( value.ValueKind == JsonValueKind.Array )
			{
				foreach ( var item in value.EnumerateArray() )
					list.Add( ConvertValue( elementType, item, scene ) );
			}
			return list;
		}

		// Fallback: System.Text.Json deserialization
		return JsonSerializer.Deserialize( value.GetRawText(), targetType );
	}

	// ──────────────────────────────────────────────
	//  Property get/set on live components
	// ──────────────────────────────────────────────

	/// <summary>
	/// Get a [Property]-marked property value from a component by name.
	/// Returns (value, propertyType) or throws if not found.
	/// </summary>
	public static (object Value, Type PropertyType) GetPropertyValue( Component comp, string propertyName )
	{
		var typeDesc = TypeLibrary.GetType( comp.GetType() );
		if ( typeDesc == null )
			throw new InvalidOperationException( $"Type not found in TypeLibrary: {comp.GetType().FullName}" );

		var prop = typeDesc.GetProperty( propertyName );
		if ( prop == null )
			throw new ArgumentException( $"Property '{propertyName}' not found on {typeDesc.FullName}" );

		if ( !prop.CanRead )
			throw new InvalidOperationException( $"Property '{propertyName}' is write-only on {typeDesc.FullName}" );

		return (prop.GetValue( comp ), prop.PropertyType);
	}

	/// <summary>
	/// Set a [Property]-marked property value on a component.
	/// Automatically converts the JSON value to the correct type.
	/// </summary>
	public static void SetPropertyValue( Component comp, string propertyName, JsonElement value, Scene scene = null )
	{
		var typeDesc = TypeLibrary.GetType( comp.GetType() );
		if ( typeDesc == null )
			throw new InvalidOperationException( $"Type not found in TypeLibrary: {comp.GetType().FullName}" );

		var prop = typeDesc.GetProperty( propertyName );
		if ( prop == null )
			throw new ArgumentException( $"Property '{propertyName}' not found on {typeDesc.FullName}" );

		if ( !prop.CanWrite )
			throw new InvalidOperationException( $"Property '{propertyName}' is read-only on {typeDesc.FullName}" );

		var converted = ConvertValue( prop.PropertyType, value, scene );
		prop.SetValue( comp, converted );
	}

	/// <summary>
	/// List all [Property]-marked properties on a component type.
	/// Returns property name, type name, and whether it's readable/writable.
	/// </summary>
	public static List<ComponentPropertyInfo> GetComponentProperties( Component comp )
	{
		var result = new List<ComponentPropertyInfo>();
		var typeDesc = TypeLibrary.GetType( comp.GetType() );
		if ( typeDesc == null ) return result;

		foreach ( var prop in typeDesc.Properties )
		{
			if ( !prop.HasAttribute<PropertyAttribute>() ) continue;

			result.Add( new ComponentPropertyInfo
			{
				Name = prop.Name,
				TypeName = prop.PropertyType?.Name ?? "unknown",
				FullTypeName = prop.PropertyType?.FullName ?? "unknown",
				CanRead = prop.CanRead,
				CanWrite = prop.CanWrite,
				IsResource = prop.PropertyType != null && typeof( Resource ).IsAssignableFrom( prop.PropertyType ),
				IsList = prop.PropertyType?.IsGenericType == true && prop.PropertyType.GetGenericTypeDefinition() == typeof( List<> ),
				IsGameObject = prop.PropertyType == typeof( GameObject ),
				IsComponent = prop.PropertyType != null && typeof( Component ).IsAssignableFrom( prop.PropertyType )
			} );
		}

		return result;
	}

	/// <summary>
	/// Describe a type from TypeLibrary — methods, properties, attributes.
	/// Used by describe_type and search_types tools.
	/// </summary>
	public static TypeInfo DescribeType( string typeName )
	{
		var desc = TypeLibrary.GetType( typeName );
		if ( desc == null ) return null;

		var info = new TypeInfo
		{
			FullName = desc.FullName,
			Name = desc.Name,
			IsComponent = typeof( Component ).IsAssignableFrom( desc.TargetType ),
			IsResource = typeof( Resource ).IsAssignableFrom( desc.TargetType ),
			Properties = new List<ComponentPropertyInfo>(),
			Methods = new List<MethodSummary>()
		};

		foreach ( var prop in desc.Properties )
		{
			info.Properties.Add( new ComponentPropertyInfo
			{
				Name = prop.Name,
				TypeName = prop.PropertyType?.Name ?? "unknown",
				FullTypeName = prop.PropertyType?.FullName ?? "unknown",
				CanRead = prop.CanRead,
				CanWrite = prop.CanWrite,
				IsResource = prop.PropertyType != null && typeof( Resource ).IsAssignableFrom( prop.PropertyType ),
				IsList = prop.PropertyType?.IsGenericType == true && prop.PropertyType.GetGenericTypeDefinition() == typeof( List<> ),
				IsGameObject = prop.PropertyType == typeof( GameObject ),
				IsComponent = prop.PropertyType != null && typeof( Component ).IsAssignableFrom( prop.PropertyType ),
				HasPropertyAttribute = prop.HasAttribute<PropertyAttribute>()
			} );
		}

		foreach ( var method in desc.Methods )
		{
			info.Methods.Add( new MethodSummary
			{
				Name = method.Name,
				ReturnType = method.ReturnType?.Name ?? "void",
				IsStatic = method.IsStatic,
				IsPublic = method.IsPublic,
				Parameters = GetMethodParameters( method )
			} );
		}

		return info;
	}

	private static List<ParameterSummary> GetMethodParameters( MethodDescription method )
	{
		var result = new List<ParameterSummary>();
		foreach ( var param in method.Parameters )
		{
			result.Add( new ParameterSummary
			{
				Name = param.Name,
				TypeName = param.ParameterType?.Name ?? "unknown"
			} );
		}
		return result;
	}

	// ──────────────────────────────────────────────
	//  Parsing helpers
	// ──────────────────────────────────────────────

	public static Vector3 ParseVector3( string s )
	{
		var parts = s.Split( ',' );
		return new Vector3( float.Parse( parts[0] ), float.Parse( parts[1] ), float.Parse( parts[2] ) );
	}

	public static Rotation ParseRotation( string s )
	{
		var parts = s.Split( ',' );
		return new Rotation( float.Parse( parts[0] ), float.Parse( parts[1] ),
			float.Parse( parts[2] ), float.Parse( parts[3] ) );
	}

	/// <summary>
	/// Load a resource by path using ResourceLibrary.Get&lt;T&gt;(path).
	/// Uses reflection to call the generic method with the correct type.
	/// </summary>
	private static object LoadResource( Type resourceType, string path )
	{
		// ResourceLibrary.Get<T>(string) is a generic static method.
		// Find the 1-param generic overload that takes a string path.
		var methods = typeof( ResourceLibrary ).GetMethods( System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public );
		System.Reflection.MethodInfo getMethod = null;
		foreach ( var m in methods )
		{
			if ( m.Name != "Get" || !m.IsGenericMethodDefinition ) continue;
			var parms = m.GetParameters();
			if ( parms.Length == 1 && parms[0].ParameterType == typeof( string ) )
			{
				getMethod = m;
				break;
			}
		}

		if ( getMethod == null )
			throw new InvalidOperationException( "ResourceLibrary.Get<T>(string) method not found" );

		var generic = getMethod.MakeGenericMethod( resourceType );
		return generic.Invoke( null, new object[] { path } );
	}

	// ──────────────────────────────────────────────
	//  Info DTOs
	// ──────────────────────────────────────────────

	public class ComponentPropertyInfo
	{
		public string Name { get; set; }
		public string TypeName { get; set; }
		public string FullTypeName { get; set; }
		public bool CanRead { get; set; }
		public bool CanWrite { get; set; }
		public bool IsResource { get; set; }
		public bool IsList { get; set; }
		public bool IsGameObject { get; set; }
		public bool IsComponent { get; set; }
		public bool HasPropertyAttribute { get; set; }
	}

	public class TypeInfo
	{
		public string FullName { get; set; }
		public string Name { get; set; }
		public bool IsComponent { get; set; }
		public bool IsResource { get; set; }
		public List<ComponentPropertyInfo> Properties { get; set; }
		public List<MethodSummary> Methods { get; set; }
	}

	public class MethodSummary
	{
		public string Name { get; set; }
		public string ReturnType { get; set; }
		public bool IsStatic { get; set; }
		public bool IsPublic { get; set; }
		public List<ParameterSummary> Parameters { get; set; }
	}

	public class ParameterSummary
	{
		public string Name { get; set; }
		public string TypeName { get; set; }
	}
}
