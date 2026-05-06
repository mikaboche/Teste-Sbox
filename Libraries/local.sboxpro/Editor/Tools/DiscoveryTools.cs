using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Sandbox;
using Editor;

namespace SboxPro;

public static class DiscoveryTools
{
	[Tool( "describe_type", "Full reflection on a TypeLibrary type — properties, methods, attributes, base type, interfaces." )]
	[Param( "typeName", "Fully-qualified or short type name (e.g. 'PlayerController', 'Sandbox.ModelRenderer').", Required = true )]
	[Param( "include_methods", "Include method list. Default: true", Required = false, Type = "boolean", Default = "true" )]
	[Param( "include_properties", "Include property list. Default: true", Required = false, Type = "boolean", Default = "true" )]
	public static object DescribeType( JsonElement args )
	{
		var typeName = ToolHandlerBase.RequireString( args, "typeName" );
		var includeMethods = ToolHandlerBase.GetBool( args, "include_methods", true );
		var includeProperties = ToolHandlerBase.GetBool( args, "include_properties", true );

		var desc = TypeLibrary.GetType( typeName );
		if ( desc == null )
		{
			var byShort = TypeLibrary.GetTypes()
				.FirstOrDefault( t => t.Name.Equals( typeName, StringComparison.OrdinalIgnoreCase ) );

			if ( byShort == null )
				return ToolHandlerBase.ErrorResult( $"Type not found: {typeName}" );

			desc = byShort;
		}

		var result = new Dictionary<string, object>
		{
			["fullName"] = desc.FullName,
			["name"] = desc.Name,
			["title"] = desc.Title,
			["description"] = desc.Description,
			["isComponent"] = typeof( Component ).IsAssignableFrom( desc.TargetType ),
			["isResource"] = typeof( Resource ).IsAssignableFrom( desc.TargetType ),
			["isEnum"] = desc.TargetType.IsEnum,
			["isAbstract"] = desc.TargetType.IsAbstract,
			["isInterface"] = desc.TargetType.IsInterface,
			["baseType"] = desc.TargetType.BaseType?.FullName
		};

		var interfaces = desc.TargetType.GetInterfaces()
			.Select( i => i.FullName )
			.Where( n => n != null )
			.ToArray();
		if ( interfaces.Length > 0 )
			result["interfaces"] = interfaces;

		if ( desc.TargetType.IsEnum )
		{
			result["enumValues"] = Enum.GetNames( desc.TargetType );
		}

		if ( includeProperties )
		{
			var props = new List<object>();
			foreach ( var prop in desc.Properties )
			{
				props.Add( new
				{
					name = prop.Name,
					type = prop.PropertyType?.Name ?? "unknown",
					fullType = prop.PropertyType?.FullName ?? "unknown",
					canRead = prop.CanRead,
					canWrite = prop.CanWrite,
					hasPropertyAttribute = prop.HasAttribute<PropertyAttribute>()
				} );
			}
			result["properties"] = props;
		}

		if ( includeMethods )
		{
			var methods = new List<object>();
			foreach ( var method in desc.Methods )
			{
				var parms = method.Parameters.Select( p => new
				{
					name = p.Name,
					type = p.ParameterType?.Name ?? "unknown"
				} ).ToArray();

				methods.Add( new
				{
					name = method.Name,
					returnType = method.ReturnType?.Name ?? "void",
					isStatic = method.IsStatic,
					isPublic = method.IsPublic,
					parameters = parms
				} );
			}
			result["methods"] = methods;
		}

		return ToolHandlerBase.JsonResult( result );
	}

	[Tool( "search_types", "Search TypeLibrary for types matching a pattern. Optionally filter to components only." )]
	[Param( "pattern", "Name pattern to search for (case-insensitive substring match).", Required = true )]
	[Param( "components_only", "Only return types that inherit from Component. Default: false", Required = false, Type = "boolean", Default = "false" )]
	[Param( "limit", "Max results. Default: 50", Required = false, Type = "integer", Default = "50" )]
	public static object SearchTypes( JsonElement args )
	{
		var pattern = ToolHandlerBase.RequireString( args, "pattern" );
		var componentsOnly = ToolHandlerBase.GetBool( args, "components_only", false );
		var limit = ToolHandlerBase.GetInt( args, "limit", 50 );

		var matches = TypeLibrary.GetTypes()
			.Where( t => t.Name.Contains( pattern, StringComparison.OrdinalIgnoreCase )
				|| t.FullName.Contains( pattern, StringComparison.OrdinalIgnoreCase ) )
			.Where( t => !componentsOnly || typeof( Component ).IsAssignableFrom( t.TargetType ) )
			.OrderBy( t => t.Name )
			.Take( limit )
			.Select( t => new
			{
				name = t.Name,
				fullName = t.FullName,
				isComponent = typeof( Component ).IsAssignableFrom( t.TargetType ),
				isResource = typeof( Resource ).IsAssignableFrom( t.TargetType ),
				isAbstract = t.TargetType.IsAbstract
			} )
			.ToArray();

		return ToolHandlerBase.JsonResult( new
		{
			pattern,
			components_only = componentsOnly,
			count = matches.Length,
			truncated = matches.Length >= limit,
			types = matches
		} );
	}

	[Tool( "get_method_signature", "Get detailed signature for a specific method on a type." )]
	[Param( "typeName", "Fully-qualified or short type name.", Required = true )]
	[Param( "methodName", "Method name to look up.", Required = true )]
	public static object GetMethodSignature( JsonElement args )
	{
		var typeName = ToolHandlerBase.RequireString( args, "typeName" );
		var methodName = ToolHandlerBase.RequireString( args, "methodName" );

		var desc = TypeLibrary.GetType( typeName );
		if ( desc == null )
		{
			var byShort = TypeLibrary.GetTypes()
				.FirstOrDefault( t => t.Name.Equals( typeName, StringComparison.OrdinalIgnoreCase ) );

			if ( byShort == null )
				return ToolHandlerBase.ErrorResult( $"Type not found: {typeName}" );

			desc = byShort;
		}

		var overloads = desc.Methods
			.Where( m => m.Name.Equals( methodName, StringComparison.OrdinalIgnoreCase ) )
			.ToArray();

		if ( overloads.Length == 0 )
			return ToolHandlerBase.ErrorResult( $"Method '{methodName}' not found on {desc.FullName}" );

		var signatures = overloads.Select( m =>
		{
			var parms = m.Parameters.Select( p => new
			{
				name = p.Name,
				type = p.ParameterType?.Name ?? "unknown",
				fullType = p.ParameterType?.FullName ?? "unknown"
			} ).ToArray();

			var paramString = string.Join( ", ", parms.Select( p => $"{p.type} {p.name}" ) );
			var sig = $"{m.ReturnType?.Name ?? "void"} {m.Name}({paramString})";

			return new
			{
				signature = sig,
				returnType = m.ReturnType?.Name ?? "void",
				returnFullType = m.ReturnType?.FullName ?? "void",
				isStatic = m.IsStatic,
				isPublic = m.IsPublic,
				parameters = parms
			};
		} ).ToArray();

		return ToolHandlerBase.JsonResult( new
		{
			type = desc.FullName,
			method = methodName,
			overload_count = signatures.Length,
			signatures
		} );
	}

	[Tool( "list_available_components", "List all Component types available in the current project (engine + user code)." )]
	[Param( "pattern", "Optional name filter (case-insensitive substring).", Required = false )]
	[Param( "limit", "Max results. Default: 100", Required = false, Type = "integer", Default = "100" )]
	public static object ListAvailableComponents( JsonElement args )
	{
		var pattern = ToolHandlerBase.GetString( args, "pattern" );
		var limit = ToolHandlerBase.GetInt( args, "limit", 100 );

		var components = TypeLibrary.GetTypes<Component>()
			.Where( t => !t.TargetType.IsAbstract )
			.Where( t => string.IsNullOrEmpty( pattern )
				|| t.Name.Contains( pattern, StringComparison.OrdinalIgnoreCase )
				|| t.FullName.Contains( pattern, StringComparison.OrdinalIgnoreCase ) )
			.OrderBy( t => t.Name )
			.Take( limit )
			.Select( t =>
			{
				var propCount = t.Properties.Count( p => p.HasAttribute<PropertyAttribute>() );
				var ns = t.TargetType.Namespace ?? "";
				var isEngine = ns.StartsWith( "Sandbox" );

				return new
				{
					name = t.Name,
					fullName = t.FullName,
					title = t.Title,
					description = t.Description,
					ns = ns,
					source = isEngine ? "engine" : "project",
					propertyCount = propCount
				};
			} )
			.ToArray();

		return ToolHandlerBase.JsonResult( new
		{
			count = components.Length,
			truncated = components.Length >= limit,
			components
		} );
	}

	[Tool( "get_object_bounds", "Get world-space bounding box of a GameObject by name or GUID.", RequiresMainThread = true )]
	[Param( "name", "GameObject name to find. Searched in active scene.", Required = false )]
	[Param( "guid", "GameObject GUID. Takes precedence over name.", Required = false )]
	public static object GetObjectBounds( JsonElement args )
	{
		var name = ToolHandlerBase.GetString( args, "name" );
		var guid = ToolHandlerBase.GetString( args, "guid" );

		if ( string.IsNullOrEmpty( name ) && string.IsNullOrEmpty( guid ) )
			return ToolHandlerBase.ErrorResult( "Provide either 'name' or 'guid'" );

		var scene = SceneHelpers.ResolveActiveScene();
		if ( scene == null )
			return ToolHandlerBase.ErrorResult( "No active scene" );

		var target = SceneHelpers.FindByGuidOrName( scene, guid, name );
		if ( target == null )
		{
			var identifier = !string.IsNullOrEmpty( guid ) ? $"GUID: {guid}" : name;
			return ToolHandlerBase.ErrorResult( $"GameObject not found: {identifier}" );
		}

		var bounds = target.GetBounds();

		return ToolHandlerBase.JsonResult( new
		{
			name = target.Name,
			guid = target.Id.ToString(),
			bounds = new
			{
				center = new { x = bounds.Center.x, y = bounds.Center.y, z = bounds.Center.z },
				size = new { x = bounds.Size.x, y = bounds.Size.y, z = bounds.Size.z },
				mins = new { x = bounds.Mins.x, y = bounds.Mins.y, z = bounds.Mins.z },
				maxs = new { x = bounds.Maxs.x, y = bounds.Maxs.y, z = bounds.Maxs.z }
			},
			worldPosition = new { x = target.WorldPosition.x, y = target.WorldPosition.y, z = target.WorldPosition.z }
		} );
	}
}
