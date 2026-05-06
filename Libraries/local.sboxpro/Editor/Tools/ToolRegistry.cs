using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Sandbox;

namespace SboxPro;

public sealed class ToolEntry
{
	public string Name { get; init; }
	public string Description { get; init; }
	public bool RequiresMainThread { get; init; }
	public MethodInfo Method { get; init; }
	public Dictionary<string, object> Schema { get; init; }
}

public static class ToolRegistry
{
	private static readonly Dictionary<string, ToolEntry> _tools = new( StringComparer.OrdinalIgnoreCase );
	private static object[] _schemaCache;

	public static int Count => _tools.Count;
	public static IReadOnlyDictionary<string, ToolEntry> Tools => _tools;

	public static void Initialize()
	{
		_tools.Clear();
		_schemaCache = null;

		var assemblies = AppDomain.CurrentDomain.GetAssemblies();

		foreach ( var assembly in assemblies )
		{
			try
			{
				ScanAssembly( assembly );
			}
			catch ( Exception ex )
			{
				SboxProLog.Warn( "ToolRegistry", $"Failed to scan assembly {assembly.GetName().Name}: {ex.Message}" );
			}
		}

		SboxProLog.Info( "ToolRegistry", $"Registered {_tools.Count} tools" );
	}

	public static ToolEntry Get( string name )
	{
		_tools.TryGetValue( name, out var entry );
		return entry;
	}

	public static object[] GetAllSchemas()
	{
		if ( _schemaCache != null )
			return _schemaCache;

		_schemaCache = _tools.Values
			.Select( t => (object)t.Schema )
			.ToArray();

		return _schemaCache;
	}

	private static void ScanAssembly( Assembly assembly )
	{
		foreach ( var type in assembly.GetTypes() )
		{
			foreach ( var method in type.GetMethods( BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic ) )
			{
				var toolAttr = method.GetCustomAttribute<ToolAttribute>();
				if ( toolAttr == null )
					continue;

				var paramAttrs = method.GetCustomAttributes<ParamAttribute>().ToList();
				var schema = BuildSchema( toolAttr, paramAttrs );

				var entry = new ToolEntry
				{
					Name = toolAttr.Name,
					Description = toolAttr.Description,
					RequiresMainThread = toolAttr.RequiresMainThread,
					Method = method,
					Schema = schema
				};

				if ( _tools.ContainsKey( toolAttr.Name ) )
				{
					SboxProLog.Warn( "ToolRegistry", $"Duplicate tool name '{toolAttr.Name}', skipping" );
					continue;
				}

				_tools[toolAttr.Name] = entry;
			}
		}
	}

	private static Dictionary<string, object> BuildSchema( ToolAttribute tool, List<ParamAttribute> paramAttrs )
	{
		var properties = new Dictionary<string, object>();
		var required = new List<string>();

		foreach ( var p in paramAttrs )
		{
			var prop = new Dictionary<string, object>
			{
				["type"] = p.Type,
				["description"] = p.Description
			};

			if ( p.Enum != null )
			{
				prop["enum"] = p.Enum.Split( ',' ).Select( s => s.Trim() ).ToArray();
			}

			if ( p.Default != null )
			{
				prop["default"] = p.Default;
			}

			properties[p.Name] = prop;

			if ( p.Required )
				required.Add( p.Name );
		}

		var inputSchema = new Dictionary<string, object>
		{
			["type"] = "object",
			["properties"] = properties
		};

		if ( required.Count > 0 )
			inputSchema["required"] = required.ToArray();

		return new Dictionary<string, object>
		{
			["name"] = tool.Name,
			["description"] = tool.Description,
			["inputSchema"] = inputSchema
		};
	}

	public static async Task<object> Invoke( ToolEntry tool, JsonElement args )
	{
		var parameters = tool.Method.GetParameters();
		object raw;

		if ( parameters.Length == 0 )
			raw = tool.Method.Invoke( null, null );
		else if ( parameters.Length == 1 && parameters[0].ParameterType == typeof( JsonElement ) )
			raw = tool.Method.Invoke( null, new object[] { args } );
		else
			throw new InvalidOperationException( $"Tool '{tool.Name}' has unsupported method signature" );

		// Tools may return Task<object> for async work (install_asset, get_package_details, etc).
		// Unwrap so the dispatcher serialises the actual result, not the Task object.
		if ( raw is Task task )
		{
			await task.ConfigureAwait( false );
			var resultProp = task.GetType().GetProperty( "Result" );
			return resultProp?.GetValue( task );
		}

		return raw;
	}
}
