using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Sandbox;
using Editor;

namespace SboxPro;

public static class ComponentTools
{
	// ──────────────────────────────────────────────
	//  add_component
	// ──────────────────────────────────────────────

	[Tool( "add_component", "Add a component to a GameObject by type name.", RequiresMainThread = true )]
	[Param( "name", "Name of the target GameObject.", Required = false )]
	[Param( "guid", "GUID of the target GameObject.", Required = false )]
	[Param( "component_type", "Component type name (e.g. 'ModelRenderer', 'Sandbox.SkinnedModelRenderer').", Required = true )]
	public static object AddComponent( JsonElement args )
	{
		var componentType = ToolHandlerBase.RequireString( args, "component_type" );

		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );

		var typeDesc = FindComponentType( componentType );
		if ( typeDesc == null )
			return ToolHandlerBase.ErrorResult( $"Component type not found: {componentType}" );

		var comp = go.Components.Create( typeDesc );
		if ( comp == null )
			return ToolHandlerBase.ErrorResult( $"Failed to create component: {componentType}" );

		return ToolHandlerBase.JsonResult( new
		{
			added = true,
			gameObject = go.Name,
			gameObjectGuid = go.Id.ToString(),
			componentType = typeDesc.FullName,
			componentGuid = comp.Id.ToString()
		} );
	}

	// ──────────────────────────────────────────────
	//  add_component_with_properties
	// ──────────────────────────────────────────────

	[Tool( "add_component_with_properties", "Add a component and configure its properties in one call.", RequiresMainThread = true )]
	[Param( "name", "Name of the target GameObject.", Required = false )]
	[Param( "guid", "GUID of the target GameObject.", Required = false )]
	[Param( "component_type", "Component type name.", Required = true )]
	[Param( "properties", "JSON object of property name → value pairs to set after adding.", Required = true, Type = "object" )]
	public static object AddComponentWithProperties( JsonElement args )
	{
		var componentType = ToolHandlerBase.RequireString( args, "component_type" );

		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );

		var typeDesc = FindComponentType( componentType );
		if ( typeDesc == null )
			return ToolHandlerBase.ErrorResult( $"Component type not found: {componentType}" );

		var comp = go.Components.Create( typeDesc );
		if ( comp == null )
			return ToolHandlerBase.ErrorResult( $"Failed to create component: {componentType}" );

		var scene = SceneHelpers.ResolveActiveScene();
		var setResults = new List<object>();

		if ( args.TryGetProperty( "properties", out var properties ) && properties.ValueKind == JsonValueKind.Object )
		{
			foreach ( var prop in properties.EnumerateObject() )
			{
				try
				{
					RuntimeReflection.SetPropertyValue( comp, prop.Name, prop.Value, scene );
					setResults.Add( new { property = prop.Name, set = true } );
				}
				catch ( Exception ex )
				{
					setResults.Add( new { property = prop.Name, set = false, error = ex.Message } );
				}
			}
		}

		return ToolHandlerBase.JsonResult( new
		{
			added = true,
			gameObject = go.Name,
			componentType = typeDesc.FullName,
			componentGuid = comp.Id.ToString(),
			propertyResults = setResults
		} );
	}

	// ──────────────────────────────────────────────
	//  remove_component
	// ──────────────────────────────────────────────

	[Tool( "remove_component", "Remove a component from a GameObject by type name or GUID.", RequiresMainThread = true )]
	[Param( "name", "Name of the target GameObject.", Required = false )]
	[Param( "guid", "GUID of the target GameObject.", Required = false )]
	[Param( "component_type", "Component type name or substring to match.", Required = false )]
	[Param( "component_guid", "GUID of the specific component to remove.", Required = false )]
	public static object RemoveComponent( JsonElement args )
	{
		var compType = ToolHandlerBase.GetString( args, "component_type" );
		var compGuid = ToolHandlerBase.GetString( args, "component_guid" );

		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );

		Component target = null;

		if ( !string.IsNullOrEmpty( compGuid ) )
		{
			if ( Guid.TryParse( compGuid, out var parsed ) )
				target = go.Components.GetAll().FirstOrDefault( c => c.Id == parsed );
		}
		else if ( !string.IsNullOrEmpty( compType ) )
		{
			target = go.Components.GetAll()
				.FirstOrDefault( c => c.GetType().Name.Contains( compType, StringComparison.OrdinalIgnoreCase )
					|| c.GetType().FullName.Contains( compType, StringComparison.OrdinalIgnoreCase ) );
		}
		else
		{
			return ToolHandlerBase.ErrorResult( "Provide either 'component_type' or 'component_guid'" );
		}

		if ( target == null )
			return ToolHandlerBase.ErrorResult( $"Component not found: {compGuid ?? compType}" );

		var removedType = target.GetType().FullName;
		var removedGuid = target.Id.ToString();
		target.Destroy();

		return ToolHandlerBase.JsonResult( new
		{
			removed = true,
			gameObject = go.Name,
			componentType = removedType,
			componentGuid = removedGuid
		} );
	}

	// ──────────────────────────────────────────────
	//  set_component_enabled
	// ──────────────────────────────────────────────

	[Tool( "set_component_enabled", "Toggle a component's enabled state.", RequiresMainThread = true )]
	[Param( "name", "Name of the target GameObject.", Required = false )]
	[Param( "guid", "GUID of the target GameObject.", Required = false )]
	[Param( "component_type", "Component type name to find.", Required = false )]
	[Param( "component_guid", "GUID of the specific component.", Required = false )]
	[Param( "enabled", "Whether the component should be enabled.", Required = true, Type = "boolean" )]
	public static object SetComponentEnabled( JsonElement args )
	{
		var enabled = ToolHandlerBase.GetBool( args, "enabled", true );
		var comp = ResolveComponent( args );
		if ( comp == null )
			return ToolHandlerBase.ErrorResult( "Component not found" );

		comp.Enabled = enabled;

		return ToolHandlerBase.JsonResult( new
		{
			componentType = comp.GetType().FullName,
			componentGuid = comp.Id.ToString(),
			enabled = comp.Enabled
		} );
	}

	// ──────────────────────────────────────────────
	//  set_component_property — Bug fix §6.3, §6.4
	// ──────────────────────────────────────────────

	[Tool( "set_component_property", "Set a property on a component. Bug fix: handles resource refs (Model, Material), Lists, GameObject refs.", RequiresMainThread = true )]
	[Param( "name", "Name of the target GameObject.", Required = false )]
	[Param( "guid", "GUID of the target GameObject.", Required = false )]
	[Param( "component_type", "Component type name.", Required = false )]
	[Param( "component_guid", "GUID of the specific component.", Required = false )]
	[Param( "property", "Property name to set.", Required = true )]
	[Param( "value", "Value to set. Format depends on type: string, number, bool, 'x,y,z' for vectors, asset path for resources, GUID for GO/Component refs.", Required = true )]
	public static object SetComponentProperty( JsonElement args )
	{
		var propertyName = ToolHandlerBase.RequireString( args, "property" );

		var comp = ResolveComponent( args );
		if ( comp == null )
			return ToolHandlerBase.ErrorResult( "Component not found" );

		if ( !args.TryGetProperty( "value", out var value ) )
			return ToolHandlerBase.ErrorResult( "Missing 'value' parameter" );

		var scene = SceneHelpers.ResolveActiveScene();

		try
		{
			RuntimeReflection.SetPropertyValue( comp, propertyName, value, scene );

			var (readBack, propType) = RuntimeReflection.GetPropertyValue( comp, propertyName );

			return ToolHandlerBase.JsonResult( new
			{
				set = true,
				componentType = comp.GetType().Name,
				property = propertyName,
				valueType = propType?.Name ?? "unknown",
				currentValue = readBack?.ToString()
			} );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Failed to set {propertyName}: {ex.Message}" );
		}
	}

	// ──────────────────────────────────────────────
	//  set_property — alias for set_component_property
	// ──────────────────────────────────────────────

	[Tool( "set_property", "Alias for set_component_property. Set a property on a component.", RequiresMainThread = true )]
	[Param( "name", "Name of the target GameObject.", Required = false )]
	[Param( "guid", "GUID of the target GameObject.", Required = false )]
	[Param( "component_type", "Component type name.", Required = false )]
	[Param( "component_guid", "GUID of the specific component.", Required = false )]
	[Param( "property", "Property name to set.", Required = true )]
	[Param( "value", "Value to set.", Required = true )]
	public static object SetProperty( JsonElement args )
	{
		return SetComponentProperty( args );
	}

	// ──────────────────────────────────────────────
	//  set_list_property — explicit List<T> setter
	// ──────────────────────────────────────────────

	[Tool( "set_list_property", "Set a List<> property on a component. Accepts an array of values (GUIDs for GameObject/Component lists, paths for resource lists).", RequiresMainThread = true )]
	[Param( "name", "Name of the target GameObject.", Required = false )]
	[Param( "guid", "GUID of the target GameObject.", Required = false )]
	[Param( "component_type", "Component type name.", Required = false )]
	[Param( "component_guid", "GUID of the specific component.", Required = false )]
	[Param( "property", "Property name to set.", Required = true )]
	[Param( "values", "JSON array of values.", Required = true, Type = "array" )]
	public static object SetListProperty( JsonElement args )
	{
		var propertyName = ToolHandlerBase.RequireString( args, "property" );

		var comp = ResolveComponent( args );
		if ( comp == null )
			return ToolHandlerBase.ErrorResult( "Component not found" );

		if ( !args.TryGetProperty( "values", out var values ) || values.ValueKind != JsonValueKind.Array )
			return ToolHandlerBase.ErrorResult( "Missing or invalid 'values' parameter — must be a JSON array" );

		var scene = SceneHelpers.ResolveActiveScene();

		try
		{
			RuntimeReflection.SetPropertyValue( comp, propertyName, values, scene );

			return ToolHandlerBase.JsonResult( new
			{
				set = true,
				componentType = comp.GetType().Name,
				property = propertyName,
				itemCount = values.GetArrayLength()
			} );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Failed to set list {propertyName}: {ex.Message}" );
		}
	}

	// ──────────────────────────────────────────────
	//  set_resource_property — explicit resource setter
	// ──────────────────────────────────────────────

	[Tool( "set_resource_property", "Set a resource reference property (Model, Material, Sound) on a component by asset path.", RequiresMainThread = true )]
	[Param( "name", "Name of the target GameObject.", Required = false )]
	[Param( "guid", "GUID of the target GameObject.", Required = false )]
	[Param( "component_type", "Component type name.", Required = false )]
	[Param( "component_guid", "GUID of the specific component.", Required = false )]
	[Param( "property", "Property name to set.", Required = true )]
	[Param( "path", "Asset path to the resource (e.g. 'models/citizen/citizen.vmdl').", Required = true )]
	public static object SetResourceProperty( JsonElement args )
	{
		var propertyName = ToolHandlerBase.RequireString( args, "property" );
		var resourcePath = ToolHandlerBase.RequireString( args, "path" );

		var comp = ResolveComponent( args );
		if ( comp == null )
			return ToolHandlerBase.ErrorResult( "Component not found" );

		var scene = SceneHelpers.ResolveActiveScene();

		try
		{
			using var doc = JsonDocument.Parse( $"\"{resourcePath}\"" );
			RuntimeReflection.SetPropertyValue( comp, propertyName, doc.RootElement, scene );

			var (readBack, propType) = RuntimeReflection.GetPropertyValue( comp, propertyName );

			return ToolHandlerBase.JsonResult( new
			{
				set = true,
				componentType = comp.GetType().Name,
				property = propertyName,
				resourcePath,
				valueType = propType?.Name ?? "unknown",
				currentValue = readBack?.ToString(),
				isNull = readBack == null
			} );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Failed to set resource {propertyName}: {ex.Message}" );
		}
	}

	// ──────────────────────────────────────────────
	//  set_prefab_ref
	// ──────────────────────────────────────────────

	[Tool( "set_prefab_ref", "Assign a prefab file reference to a GameObject property on a component.", RequiresMainThread = true )]
	[Param( "name", "Name of the target GameObject.", Required = false )]
	[Param( "guid", "GUID of the target GameObject.", Required = false )]
	[Param( "component_type", "Component type name.", Required = false )]
	[Param( "component_guid", "GUID of the specific component.", Required = false )]
	[Param( "property", "Property name to set.", Required = true )]
	[Param( "prefab_path", "Path to the .prefab file.", Required = true )]
	public static object SetPrefabRef( JsonElement args )
	{
		var propertyName = ToolHandlerBase.RequireString( args, "property" );
		var prefabPath = ToolHandlerBase.RequireString( args, "prefab_path" );

		var comp = ResolveComponent( args );
		if ( comp == null )
			return ToolHandlerBase.ErrorResult( "Component not found" );

		var normalized = PathNormalizer.NormalizeAssetPath( prefabPath );
		var absolute = PathNormalizer.ToAbsolute( normalized );

		if ( !System.IO.File.Exists( absolute ) )
			return ToolHandlerBase.ErrorResult( $"Prefab file not found: {normalized}" );

		try
		{
			// Prefab references in components are stored as GameObject (the
			// prefab's root template), NOT PrefabFile. Use GameObject.GetPrefab
			// which loads the engine's cached template; fall back to direct
			// PrefabFile.Load for newly-created prefabs not yet in the cache. (#32)
			var resourcePath = PathNormalizer.ForResourceLibrary( prefabPath );
			var template = GameObject.GetPrefab( resourcePath )
				?? GameObject.GetPrefab( normalized );

			if ( template == null )
			{
				var prefabFile = PrefabFile.Load( absolute );
				template = prefabFile?.GetScene()?.Root;
			}

			if ( template == null )
				return ToolHandlerBase.ErrorResult( $"Failed to load prefab: {normalized}" );

			var typeDesc = TypeLibrary.GetType( comp.GetType() );
			var prop = typeDesc?.GetProperty( propertyName );
			if ( prop == null )
				return ToolHandlerBase.ErrorResult( $"Property '{propertyName}' not found on {comp.GetType().Name}" );

			prop.SetValue( comp, template );

			return ToolHandlerBase.JsonResult( new
			{
				set = true,
				componentType = comp.GetType().Name,
				property = propertyName,
				prefabPath = normalized
			} );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Failed to set prefab ref: {ex.Message}" );
		}
	}

	// ──────────────────────────────────────────────
	//  get_component_properties
	// ──────────────────────────────────────────────

	[Tool( "get_component_properties", "Read current values of all [Property]-marked properties on a component.", RequiresMainThread = true )]
	[Param( "name", "Name of the target GameObject.", Required = false )]
	[Param( "guid", "GUID of the target GameObject.", Required = false )]
	[Param( "component_type", "Component type name.", Required = false )]
	[Param( "component_guid", "GUID of the specific component.", Required = false )]
	public static object GetComponentProperties( JsonElement args )
	{
		var comp = ResolveComponent( args );
		if ( comp == null )
			return ToolHandlerBase.ErrorResult( "Component not found" );

		var props = RuntimeReflection.GetComponentProperties( comp );
		var values = new List<object>();

		foreach ( var p in props )
		{
			object val = null;
			string valStr = null;
			try
			{
				if ( p.CanRead )
				{
					var (v, _) = RuntimeReflection.GetPropertyValue( comp, p.Name );
					val = v;
					valStr = v?.ToString();
				}
			}
			catch { }

			values.Add( new
			{
				name = p.Name,
				type = p.TypeName,
				fullType = p.FullTypeName,
				value = valStr,
				canRead = p.CanRead,
				canWrite = p.CanWrite,
				isResource = p.IsResource,
				isList = p.IsList,
				isGameObject = p.IsGameObject,
				isComponent = p.IsComponent
			} );
		}

		return ToolHandlerBase.JsonResult( new
		{
			componentType = comp.GetType().FullName,
			componentGuid = comp.Id.ToString(),
			gameObject = comp.GameObject?.Name,
			propertyCount = values.Count,
			properties = values
		} );
	}

	// ──────────────────────────────────────────────
	//  get_all_properties — wider format from Lou
	// ──────────────────────────────────────────────

	[Tool( "get_all_properties", "Dump all component properties on a GameObject as JSON.", RequiresMainThread = true )]
	[Param( "name", "Name of the target GameObject.", Required = false )]
	[Param( "guid", "GUID of the target GameObject.", Required = false )]
	public static object GetAllProperties( JsonElement args )
	{
		var go = ResolveGO( args );
		if ( go == null ) return GONotFound( args );

		var components = new List<object>();

		foreach ( var comp in go.Components.GetAll() )
		{
			var props = RuntimeReflection.GetComponentProperties( comp );
			var propValues = new List<object>();

			foreach ( var p in props )
			{
				string valStr = null;
				try
				{
					if ( p.CanRead )
					{
						var (v, _) = RuntimeReflection.GetPropertyValue( comp, p.Name );
						valStr = v?.ToString();
					}
				}
				catch { }

				propValues.Add( new
				{
					name = p.Name,
					type = p.TypeName,
					value = valStr,
					isResource = p.IsResource,
					isList = p.IsList
				} );
			}

			components.Add( new
			{
				type = comp.GetType().Name,
				fullType = comp.GetType().FullName,
				guid = comp.Id.ToString(),
				enabled = comp.Enabled,
				properties = propValues
			} );
		}

		return ToolHandlerBase.JsonResult( new
		{
			gameObject = go.Name,
			gameObjectGuid = go.Id.ToString(),
			componentCount = components.Count,
			components
		} );
	}

	// ──────────────────────────────────────────────
	//  get_property
	// ──────────────────────────────────────────────

	[Tool( "get_property", "Read a single property value from a component.", RequiresMainThread = true )]
	[Param( "name", "Name of the target GameObject.", Required = false )]
	[Param( "guid", "GUID of the target GameObject.", Required = false )]
	[Param( "component_type", "Component type name.", Required = false )]
	[Param( "component_guid", "GUID of the specific component.", Required = false )]
	[Param( "property", "Property name to read.", Required = true )]
	public static object GetProperty( JsonElement args )
	{
		var propertyName = ToolHandlerBase.RequireString( args, "property" );

		var comp = ResolveComponent( args );
		if ( comp == null )
			return ToolHandlerBase.ErrorResult( "Component not found" );

		try
		{
			var (value, propType) = RuntimeReflection.GetPropertyValue( comp, propertyName );

			return ToolHandlerBase.JsonResult( new
			{
				componentType = comp.GetType().Name,
				property = propertyName,
				type = propType?.Name ?? "unknown",
				value = value?.ToString(),
				isNull = value == null
			} );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Failed to read {propertyName}: {ex.Message}" );
		}
	}

	// ──────────────────────────────────────────────
	//  copy_component
	// ──────────────────────────────────────────────

	[Tool( "copy_component", "Copy a component configuration from one GameObject to another.", RequiresMainThread = true )]
	[Param( "source_name", "Name of the source GameObject.", Required = false )]
	[Param( "source_guid", "GUID of the source GameObject.", Required = false )]
	[Param( "target_name", "Name of the target GameObject.", Required = false )]
	[Param( "target_guid", "GUID of the target GameObject.", Required = false )]
	[Param( "component_type", "Component type name to copy.", Required = true )]
	public static object CopyComponent( JsonElement args )
	{
		var componentType = ToolHandlerBase.RequireString( args, "component_type" );
		var sourceName = ToolHandlerBase.GetString( args, "source_name" );
		var sourceGuid = ToolHandlerBase.GetString( args, "source_guid" );
		var targetName = ToolHandlerBase.GetString( args, "target_name" );
		var targetGuid = ToolHandlerBase.GetString( args, "target_guid" );

		var scene = SceneHelpers.ResolveActiveScene();
		if ( scene == null )
			return ToolHandlerBase.ErrorResult( "No active scene" );

		var sourceGO = SceneHelpers.FindByGuidOrName( scene, sourceGuid, sourceName );
		if ( sourceGO == null )
			return ToolHandlerBase.ErrorResult( $"Source GameObject not found: {sourceGuid ?? sourceName}" );

		var targetGO = SceneHelpers.FindByGuidOrName( scene, targetGuid, targetName );
		if ( targetGO == null )
			return ToolHandlerBase.ErrorResult( $"Target GameObject not found: {targetGuid ?? targetName}" );

		var sourceComp = sourceGO.Components.GetAll()
			.FirstOrDefault( c => c.GetType().Name.Contains( componentType, StringComparison.OrdinalIgnoreCase ) );

		if ( sourceComp == null )
			return ToolHandlerBase.ErrorResult( $"Component '{componentType}' not found on source '{sourceGO.Name}'" );

		var typeDesc = TypeLibrary.GetType( sourceComp.GetType() );
		if ( typeDesc == null )
			return ToolHandlerBase.ErrorResult( $"Type not in TypeLibrary: {sourceComp.GetType().FullName}" );

		var newComp = targetGO.Components.Create( typeDesc );
		if ( newComp == null )
			return ToolHandlerBase.ErrorResult( $"Failed to create component on target" );

		var copied = 0;
		foreach ( var prop in typeDesc.Properties )
		{
			if ( !prop.HasAttribute<PropertyAttribute>() ) continue;
			if ( !prop.CanRead || !prop.CanWrite ) continue;

			try
			{
				var val = prop.GetValue( sourceComp );
				prop.SetValue( newComp, val );
				copied++;
			}
			catch { }
		}

		return ToolHandlerBase.JsonResult( new
		{
			copied = true,
			source = sourceGO.Name,
			target = targetGO.Name,
			componentType = typeDesc.FullName,
			newComponentGuid = newComp.Id.ToString(),
			propertiesCopied = copied
		} );
	}

	// ──────────────────────────────────────────────
	//  bulk_set_property — new tool
	// ──────────────────────────────────────────────

	[Tool( "bulk_set_property", "Set the same property on a component type across multiple GameObjects.", RequiresMainThread = true )]
	[Param( "guids", "Comma-separated GUIDs of target GameObjects.", Required = false )]
	[Param( "names", "Comma-separated names of target GameObjects. Used if guids not provided.", Required = false )]
	[Param( "tag", "Apply to all GameObjects with this tag.", Required = false )]
	[Param( "component_type", "Component type to target on each GO.", Required = true )]
	[Param( "property", "Property name to set.", Required = true )]
	[Param( "value", "Value to set.", Required = true )]
	public static object BulkSetProperty( JsonElement args )
	{
		var componentType = ToolHandlerBase.RequireString( args, "component_type" );
		var propertyName = ToolHandlerBase.RequireString( args, "property" );

		if ( !args.TryGetProperty( "value", out var value ) )
			return ToolHandlerBase.ErrorResult( "Missing 'value' parameter" );

		var scene = SceneHelpers.ResolveActiveScene();
		if ( scene == null )
			return ToolHandlerBase.ErrorResult( "No active scene" );

		var targets = ResolveMultipleGOs( args, scene );
		if ( targets.Count == 0 )
			return ToolHandlerBase.ErrorResult( "No target GameObjects found" );

		var successes = 0;
		var failures = new List<object>();

		foreach ( var go in targets )
		{
			var comp = go.Components.GetAll()
				.FirstOrDefault( c => c.GetType().Name.Contains( componentType, StringComparison.OrdinalIgnoreCase ) );

			if ( comp == null )
			{
				failures.Add( new { gameObject = go.Name, error = $"No {componentType} component" } );
				continue;
			}

			try
			{
				RuntimeReflection.SetPropertyValue( comp, propertyName, value, scene );
				successes++;
			}
			catch ( Exception ex )
			{
				failures.Add( new { gameObject = go.Name, error = ex.Message } );
			}
		}

		return ToolHandlerBase.JsonResult( new
		{
			targetCount = targets.Count,
			successes,
			failureCount = failures.Count,
			failures = failures.Count > 0 ? failures : null
		} );
	}

	// ──────────────────────────────────────────────
	//  Shared helpers
	// ──────────────────────────────────────────────

	private static GameObject ResolveGO( JsonElement args )
	{
		var name = ToolHandlerBase.GetString( args, "name" );
		var guid = ToolHandlerBase.GetString( args, "guid" );

		if ( string.IsNullOrEmpty( name ) && string.IsNullOrEmpty( guid ) )
			return null;

		var scene = SceneHelpers.ResolveActiveScene();
		return scene != null ? SceneHelpers.FindByGuidOrName( scene, guid, name ) : null;
	}

	private static object GONotFound( JsonElement args )
	{
		var name = ToolHandlerBase.GetString( args, "name" );
		var guid = ToolHandlerBase.GetString( args, "guid" );
		return ToolHandlerBase.ErrorResult( $"GameObject not found: {guid ?? name ?? "(no identifier)"}" );
	}

	private static Component ResolveComponent( JsonElement args )
	{
		var go = ResolveGO( args );
		if ( go == null ) return null;

		var compGuid = ToolHandlerBase.GetString( args, "component_guid" );
		var compType = ToolHandlerBase.GetString( args, "component_type" );

		if ( !string.IsNullOrEmpty( compGuid ) )
		{
			if ( Guid.TryParse( compGuid, out var parsed ) )
				return go.Components.GetAll().FirstOrDefault( c => c.Id == parsed );
		}

		if ( !string.IsNullOrEmpty( compType ) )
		{
			return go.Components.GetAll()
				.FirstOrDefault( c => c.GetType().Name.Contains( compType, StringComparison.OrdinalIgnoreCase )
					|| c.GetType().FullName.Contains( compType, StringComparison.OrdinalIgnoreCase ) );
		}

		return go.Components.GetAll().FirstOrDefault();
	}

	private static TypeDescription FindComponentType( string typeName )
	{
		var desc = TypeLibrary.GetType( typeName );
		if ( desc != null && typeof( Component ).IsAssignableFrom( desc.TargetType ) )
			return desc;

		return TypeLibrary.GetTypes<Component>()
			.FirstOrDefault( t => t.Name.Equals( typeName, StringComparison.OrdinalIgnoreCase )
				|| t.FullName.Equals( typeName, StringComparison.OrdinalIgnoreCase ) );
	}

	private static List<GameObject> ResolveMultipleGOs( JsonElement args, Scene scene )
	{
		var result = new List<GameObject>();

		var guidsStr = ToolHandlerBase.GetString( args, "guids" );
		var namesStr = ToolHandlerBase.GetString( args, "names" );
		var tag = ToolHandlerBase.GetString( args, "tag" );

		if ( !string.IsNullOrEmpty( guidsStr ) )
		{
			foreach ( var g in guidsStr.Split( ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) )
			{
				var go = SceneHelpers.FindByGuidOrName( scene, g, null );
				if ( go != null ) result.Add( go );
			}
		}
		else if ( !string.IsNullOrEmpty( namesStr ) )
		{
			foreach ( var n in namesStr.Split( ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) )
			{
				var go = SceneHelpers.FindByName( scene, n );
				if ( go != null ) result.Add( go );
			}
		}
		else if ( !string.IsNullOrEmpty( tag ) )
		{
			result.AddRange( SceneHelpers.WalkAll( scene ).Where( go => go.Tags.Has( tag ) ) );
		}

		return result;
	}
}
