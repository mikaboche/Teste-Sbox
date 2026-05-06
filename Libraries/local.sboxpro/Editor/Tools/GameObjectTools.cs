using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Sandbox;
using Editor;

namespace SboxPro;

public static class GameObjectTools
{
	// ──────────────────────────────────────────────
	//  create_game_object
	// ──────────────────────────────────────────────

	[Tool( "create_game_object", "Create an empty GameObject with optional name, position, and parent.", RequiresMainThread = true )]
	[Param( "name", "Name for the new GameObject. Default: 'GameObject'", Required = false )]
	[Param( "parent_name", "Name of parent GameObject.", Required = false )]
	[Param( "parent_guid", "GUID of parent GameObject. Takes precedence over parent_name.", Required = false )]
	[Param( "position", "World position as 'x,y,z'. Default: '0,0,0'", Required = false )]
	[Param( "rotation", "Rotation as 'pitch,yaw,roll' degrees. Default: '0,0,0'", Required = false )]
	[Param( "enabled", "Whether the GO starts enabled. Default: true", Required = false, Type = "boolean", Default = "true" )]
	public static object CreateGameObject( JsonElement args )
	{
		var name = ToolHandlerBase.GetString( args, "name", "GameObject" );
		var parentName = ToolHandlerBase.GetString( args, "parent_name" );
		var parentGuid = ToolHandlerBase.GetString( args, "parent_guid" );
		var posStr = ToolHandlerBase.GetString( args, "position" );
		var rotStr = ToolHandlerBase.GetString( args, "rotation" );
		var enabled = ToolHandlerBase.GetBool( args, "enabled", true );

		var scene = SceneHelpers.ResolveActiveScene();
		if ( scene == null )
			return ToolHandlerBase.ErrorResult( "No active scene" );

		GameObject parent = null;
		if ( !string.IsNullOrEmpty( parentGuid ) || !string.IsNullOrEmpty( parentName ) )
		{
			parent = SceneHelpers.FindByGuidOrName( scene, parentGuid, parentName );
			if ( parent == null )
				return ToolHandlerBase.ErrorResult( $"Parent not found: {parentGuid ?? parentName}" );
		}

		// Funnel through SceneHelpers.CreateInScene so the GO ends up attached to the
		// active scene's root — `new GameObject(...)` returns a freestanding object that
		// disappears on the next query (was issue #07).
		GameObject go;
		if ( parent != null )
		{
			go = new GameObject( enabled, name );
			go.Parent = parent; // parent is in-scene, so go inherits scene membership
		}
		else
		{
			go = SceneHelpers.CreateInScene( scene, name, enabled );
		}

		if ( !string.IsNullOrEmpty( posStr ) )
			go.WorldPosition = RuntimeReflection.ParseVector3( posStr );

		if ( !string.IsNullOrEmpty( rotStr ) )
		{
			var parts = rotStr.Split( ',' );
			go.WorldRotation = new Angles( float.Parse( parts[0] ), float.Parse( parts[1] ), float.Parse( parts[2] ) ).ToRotation();
		}

		return ToolHandlerBase.JsonResult( new
		{
			created = true,
			name = go.Name,
			guid = go.Id.ToString(),
			parent = parent?.Name,
			worldPosition = FormatVector3( go.WorldPosition )
		} );
	}

	// ──────────────────────────────────────────────
	//  destroy_game_object — Bug fix §6.6
	// ──────────────────────────────────────────────

	[Tool( "destroy_game_object", "Delete a GameObject by name or GUID. Bug fix: verifies removal after destroy.", RequiresMainThread = true )]
	[Param( "name", "Name of the GameObject to destroy.", Required = false )]
	[Param( "guid", "GUID of the GameObject to destroy. Takes precedence over name.", Required = false )]
	public static object DestroyGameObject( JsonElement args )
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
			return ToolHandlerBase.ErrorResult( $"GameObject not found: {guid ?? name}" );

		var targetId = target.Id;
		var targetName = target.Name;

		target.Destroy();

		return ToolHandlerBase.JsonResult( new
		{
			destroyed = true,
			name = targetName,
			guid = targetId.ToString()
		} );
	}

	// ──────────────────────────────────────────────
	//  duplicate_game_object
	// ──────────────────────────────────────────────

	[Tool( "duplicate_game_object", "Clone a GameObject with optional new name and position.", RequiresMainThread = true )]
	[Param( "name", "Name of the source GameObject.", Required = false )]
	[Param( "guid", "GUID of the source GameObject.", Required = false )]
	[Param( "new_name", "Name for the clone. Default: '<original>_copy'", Required = false )]
	[Param( "position", "World position for clone as 'x,y,z'.", Required = false )]
	public static object DuplicateGameObject( JsonElement args )
	{
		var name = ToolHandlerBase.GetString( args, "name" );
		var guid = ToolHandlerBase.GetString( args, "guid" );
		var newName = ToolHandlerBase.GetString( args, "new_name" );
		var posStr = ToolHandlerBase.GetString( args, "position" );

		if ( string.IsNullOrEmpty( name ) && string.IsNullOrEmpty( guid ) )
			return ToolHandlerBase.ErrorResult( "Provide either 'name' or 'guid'" );

		var scene = SceneHelpers.ResolveActiveScene();
		if ( scene == null )
			return ToolHandlerBase.ErrorResult( "No active scene" );

		var source = SceneHelpers.FindByGuidOrName( scene, guid, name );
		if ( source == null )
			return ToolHandlerBase.ErrorResult( $"GameObject not found: {guid ?? name}" );

		var clone = source.Clone();
		clone.Name = newName ?? $"{source.Name}_copy";
		clone.Parent = source.Parent;

		if ( !string.IsNullOrEmpty( posStr ) )
			clone.WorldPosition = RuntimeReflection.ParseVector3( posStr );

		return ToolHandlerBase.JsonResult( new
		{
			duplicated = true,
			sourceName = source.Name,
			sourceGuid = source.Id.ToString(),
			cloneName = clone.Name,
			cloneGuid = clone.Id.ToString(),
			worldPosition = FormatVector3( clone.WorldPosition )
		} );
	}

	// ──────────────────────────────────────────────
	//  set_game_object_name
	// ──────────────────────────────────────────────

	[Tool( "set_game_object_name", "Rename a GameObject.", RequiresMainThread = true )]
	[Param( "name", "Current name of the GameObject.", Required = false )]
	[Param( "guid", "GUID of the GameObject.", Required = false )]
	[Param( "new_name", "New name for the GameObject.", Required = true )]
	public static object SetGameObjectName( JsonElement args )
	{
		var name = ToolHandlerBase.GetString( args, "name" );
		var guid = ToolHandlerBase.GetString( args, "guid" );
		var newName = ToolHandlerBase.RequireString( args, "new_name" );

		var go = ResolveGameObject( args );
		if ( go == null )
			return ToolHandlerBase.ErrorResult( $"GameObject not found: {guid ?? name}" );

		var oldName = go.Name;
		go.Name = newName;

		return ToolHandlerBase.JsonResult( new
		{
			renamed = true,
			oldName,
			newName = go.Name,
			guid = go.Id.ToString()
		} );
	}

	// ──────────────────────────────────────────────
	//  set_game_object_enabled
	// ──────────────────────────────────────────────

	[Tool( "set_game_object_enabled", "Toggle a GameObject's enabled state.", RequiresMainThread = true )]
	[Param( "name", "Name of the GameObject.", Required = false )]
	[Param( "guid", "GUID of the GameObject.", Required = false )]
	[Param( "enabled", "Whether the GO should be enabled.", Required = true, Type = "boolean" )]
	public static object SetGameObjectEnabled( JsonElement args )
	{
		var enabled = ToolHandlerBase.GetBool( args, "enabled", true );

		var go = ResolveGameObject( args );
		if ( go == null )
			return GameObjectNotFound( args );

		go.Enabled = enabled;

		return ToolHandlerBase.JsonResult( new
		{
			name = go.Name,
			guid = go.Id.ToString(),
			enabled = go.Enabled
		} );
	}

	// ──────────────────────────────────────────────
	//  set_game_object_transform
	// ──────────────────────────────────────────────

	[Tool( "set_game_object_transform", "Set position, rotation, and/or scale on a GameObject.", RequiresMainThread = true )]
	[Param( "name", "Name of the GameObject.", Required = false )]
	[Param( "guid", "GUID of the GameObject.", Required = false )]
	[Param( "position", "World position as 'x,y,z'.", Required = false )]
	[Param( "rotation", "Rotation as 'pitch,yaw,roll' degrees.", Required = false )]
	[Param( "scale", "Uniform scale or 'x,y,z' scale.", Required = false )]
	[Param( "local", "Use local space instead of world space. Default: false", Required = false, Type = "boolean", Default = "false" )]
	public static object SetGameObjectTransform( JsonElement args )
	{
		var go = ResolveGameObject( args );
		if ( go == null )
			return GameObjectNotFound( args );

		var posStr = ToolHandlerBase.GetString( args, "position" );
		var rotStr = ToolHandlerBase.GetString( args, "rotation" );
		var scaleStr = ToolHandlerBase.GetString( args, "scale" );
		var local = ToolHandlerBase.GetBool( args, "local", false );

		if ( !string.IsNullOrEmpty( posStr ) )
		{
			var pos = RuntimeReflection.ParseVector3( posStr );
			if ( local ) go.LocalPosition = pos;
			else go.WorldPosition = pos;
		}

		if ( !string.IsNullOrEmpty( rotStr ) )
		{
			var parts = rotStr.Split( ',' );
			var angles = new Angles( float.Parse( parts[0] ), float.Parse( parts[1] ), float.Parse( parts[2] ) );
			if ( local ) go.LocalRotation = angles.ToRotation();
			else go.WorldRotation = angles.ToRotation();
		}

		if ( !string.IsNullOrEmpty( scaleStr ) )
		{
			if ( scaleStr.Contains( ',' ) )
				go.LocalScale = RuntimeReflection.ParseVector3( scaleStr );
			else
				go.LocalScale = new Vector3( float.Parse( scaleStr ) );
		}

		return ToolHandlerBase.JsonResult( new
		{
			name = go.Name,
			guid = go.Id.ToString(),
			worldPosition = FormatVector3( go.WorldPosition ),
			worldRotation = FormatAngles( go.WorldRotation.Angles() ),
			localScale = FormatVector3( go.LocalScale )
		} );
	}

	// ──────────────────────────────────────────────
	//  set_game_object_tags
	// ──────────────────────────────────────────────

	[Tool( "set_game_object_tags", "Set, add, or remove tags on a GameObject.", RequiresMainThread = true )]
	[Param( "name", "Name of the GameObject.", Required = false )]
	[Param( "guid", "GUID of the GameObject.", Required = false )]
	[Param( "tags", "Comma-separated tags to set (replaces all existing tags).", Required = false )]
	[Param( "add", "Comma-separated tags to add to existing tags.", Required = false )]
	[Param( "remove", "Comma-separated tags to remove from existing tags.", Required = false )]
	public static object SetGameObjectTags( JsonElement args )
	{
		var go = ResolveGameObject( args );
		if ( go == null )
			return GameObjectNotFound( args );

		var tagsStr = ToolHandlerBase.GetString( args, "tags" );
		var addStr = ToolHandlerBase.GetString( args, "add" );
		var removeStr = ToolHandlerBase.GetString( args, "remove" );

		if ( !string.IsNullOrEmpty( tagsStr ) )
		{
			foreach ( var existing in go.Tags.TryGetAll().ToArray() )
				go.Tags.Remove( existing );
			foreach ( var tag in tagsStr.Split( ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) )
				go.Tags.Add( tag );
		}

		if ( !string.IsNullOrEmpty( addStr ) )
		{
			foreach ( var tag in addStr.Split( ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) )
				go.Tags.Add( tag );
		}

		if ( !string.IsNullOrEmpty( removeStr ) )
		{
			foreach ( var tag in removeStr.Split( ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) )
				go.Tags.Remove( tag );
		}

		return ToolHandlerBase.JsonResult( new
		{
			name = go.Name,
			guid = go.Id.ToString(),
			tags = go.Tags.TryGetAll().ToArray()
		} );
	}

	// ──────────────────────────────────────────────
	//  reparent_game_object
	// ──────────────────────────────────────────────

	[Tool( "reparent_game_object", "Move a GameObject to a new parent (or to scene root if no parent specified).", RequiresMainThread = true )]
	[Param( "name", "Name of the GameObject to reparent.", Required = false )]
	[Param( "guid", "GUID of the GameObject to reparent.", Required = false )]
	[Param( "new_parent_name", "Name of the new parent GameObject. Omit to move to root.", Required = false )]
	[Param( "new_parent_guid", "GUID of the new parent.", Required = false )]
	[Param( "keep_world_position", "Maintain world position after reparent. Default: true", Required = false, Type = "boolean", Default = "true" )]
	public static object ReparentGameObject( JsonElement args )
	{
		var go = ResolveGameObject( args );
		if ( go == null )
			return GameObjectNotFound( args );

		var newParentName = ToolHandlerBase.GetString( args, "new_parent_name" );
		var newParentGuid = ToolHandlerBase.GetString( args, "new_parent_guid" );
		var keepWorldPos = ToolHandlerBase.GetBool( args, "keep_world_position", true );

		var scene = SceneHelpers.ResolveActiveScene();

		GameObject newParent = null;
		if ( !string.IsNullOrEmpty( newParentGuid ) || !string.IsNullOrEmpty( newParentName ) )
		{
			newParent = SceneHelpers.FindByGuidOrName( scene, newParentGuid, newParentName );
			if ( newParent == null )
				return ToolHandlerBase.ErrorResult( $"New parent not found: {newParentGuid ?? newParentName}" );
		}

		var worldPos = go.WorldPosition;
		var worldRot = go.WorldRotation;

		go.Parent = newParent;

		if ( keepWorldPos )
		{
			go.WorldPosition = worldPos;
			go.WorldRotation = worldRot;
		}

		return ToolHandlerBase.JsonResult( new
		{
			reparented = true,
			name = go.Name,
			guid = go.Id.ToString(),
			newParent = newParent?.Name ?? "(root)",
			worldPosition = FormatVector3( go.WorldPosition )
		} );
	}

	// ──────────────────────────────────────────────
	//  get_scene_hierarchy
	// ──────────────────────────────────────────────

	[Tool( "get_scene_hierarchy", "Get the full scene hierarchy as a tree structure.", RequiresMainThread = true )]
	[Param( "max_depth", "Max depth to traverse. Default: 10", Required = false, Type = "integer", Default = "10" )]
	[Param( "include_components", "Include component names on each GO. Default: false", Required = false, Type = "boolean", Default = "false" )]
	public static object GetSceneHierarchy( JsonElement args )
	{
		var maxDepth = ToolHandlerBase.GetInt( args, "max_depth", 10 );
		var includeComponents = ToolHandlerBase.GetBool( args, "include_components", false );

		var scene = SceneHelpers.ResolveActiveScene();
		if ( scene == null )
			return ToolHandlerBase.ErrorResult( "No active scene" );

		var hierarchy = scene.Children
			.Select( go => BuildHierarchyNode( go, includeComponents, 0, maxDepth ) )
			.ToArray();

		var totalCount = SceneHelpers.WalkAll( scene ).Count();

		return ToolHandlerBase.JsonResult( new
		{
			totalObjects = totalCount,
			rootCount = hierarchy.Length,
			hierarchy
		} );
	}

	private static object BuildHierarchyNode( GameObject go, bool includeComponents, int depth, int maxDepth )
	{
		var node = new Dictionary<string, object>
		{
			["name"] = go.Name,
			["guid"] = go.Id.ToString(),
			["enabled"] = go.Enabled
		};

		var tags = go.Tags.TryGetAll().ToArray();
		if ( tags.Length > 0 )
			node["tags"] = tags;

		if ( includeComponents )
		{
			node["components"] = go.Components.GetAll()
				.Select( c => c.GetType().Name )
				.ToArray();
		}

		if ( depth < maxDepth && go.Children.Any() )
		{
			node["children"] = go.Children
				.Select( child => BuildHierarchyNode( child, includeComponents, depth + 1, maxDepth ) )
				.ToArray();
		}
		else if ( go.Children.Any() )
		{
			node["childCount"] = go.Children.Count();
			node["truncated"] = true;
		}

		return node;
	}

	// ──────────────────────────────────────────────
	//  get_game_object_details
	// ──────────────────────────────────────────────

	[Tool( "get_game_object_details", "Get full details of a single GameObject: transform, tags, components, properties.", RequiresMainThread = true )]
	[Param( "name", "Name of the GameObject.", Required = false )]
	[Param( "guid", "GUID of the GameObject.", Required = false )]
	[Param( "include_children", "Include children recursively. Default: false", Required = false, Type = "boolean", Default = "false" )]
	public static object GetGameObjectDetails( JsonElement args )
	{
		var go = ResolveGameObject( args );
		if ( go == null )
			return GameObjectNotFound( args );

		var includeChildren = ToolHandlerBase.GetBool( args, "include_children", false );

		return ToolHandlerBase.JsonResult( BuildDetailNode( go, includeChildren ) );
	}

	private static object BuildDetailNode( GameObject go, bool recursive )
	{
		var components = go.Components.GetAll()
			.Select( c =>
			{
				var props = RuntimeReflection.GetComponentProperties( c );
				var propValues = new List<object>();

				foreach ( var p in props )
				{
					if ( !p.CanRead ) continue;
					try
					{
						var (val, _) = RuntimeReflection.GetPropertyValue( c, p.Name );
						propValues.Add( new
						{
							name = p.Name,
							type = p.TypeName,
							value = val?.ToString(),
							isResource = p.IsResource,
							isList = p.IsList
						} );
					}
					catch { }
				}

				return new
				{
					type = c.GetType().Name,
					fullType = c.GetType().FullName,
					guid = c.Id.ToString(),
					enabled = c.Enabled,
					properties = propValues
				};
			} )
			.ToArray();

		var result = new Dictionary<string, object>
		{
			["name"] = go.Name,
			["guid"] = go.Id.ToString(),
			["enabled"] = go.Enabled,
			["worldPosition"] = FormatVector3( go.WorldPosition ),
			["worldRotation"] = FormatAngles( go.WorldRotation.Angles() ),
			["localPosition"] = FormatVector3( go.LocalPosition ),
			["localRotation"] = FormatAngles( go.LocalRotation.Angles() ),
			["localScale"] = FormatVector3( go.LocalScale ),
			["tags"] = go.Tags.TryGetAll().ToArray(),
			["parent"] = go.Parent?.Name,
			["isPrefabInstance"] = go.IsPrefabInstance,
			["components"] = components
		};

		if ( recursive && go.Children.Any() )
		{
			result["children"] = go.Children
				.Select( child => BuildDetailNode( child, true ) )
				.ToArray();
		}
		else if ( go.Children.Any() )
		{
			result["childCount"] = go.Children.Count();
		}

		return result;
	}

	// ──────────────────────────────────────────────
	//  find_game_objects
	// ──────────────────────────────────────────────

	[Tool( "find_game_objects", "Find GameObjects by name, tag, component type, or enabled state.", RequiresMainThread = true )]
	[Param( "name", "Name substring filter (case-insensitive).", Required = false )]
	[Param( "tag", "Tag filter — objects must have this tag.", Required = false )]
	[Param( "component", "Component type name filter — objects must have this component.", Required = false )]
	[Param( "enabled_only", "Only return enabled objects. Default: false", Required = false, Type = "boolean", Default = "false" )]
	[Param( "limit", "Max results. Default: 100", Required = false, Type = "integer", Default = "100" )]
	public static object FindGameObjects( JsonElement args )
	{
		var nameFilter = ToolHandlerBase.GetString( args, "name" );
		var tagFilter = ToolHandlerBase.GetString( args, "tag" );
		var componentFilter = ToolHandlerBase.GetString( args, "component" );
		var enabledOnly = ToolHandlerBase.GetBool( args, "enabled_only", false );
		var limit = ToolHandlerBase.GetInt( args, "limit", 100 );

		var scene = SceneHelpers.ResolveActiveScene();
		if ( scene == null )
			return ToolHandlerBase.ErrorResult( "No active scene" );

		var results = SceneHelpers.WalkAll( scene )
			.Where( go =>
			{
				if ( enabledOnly && !go.Enabled ) return false;
				if ( !string.IsNullOrEmpty( nameFilter ) && !go.Name.Contains( nameFilter, StringComparison.OrdinalIgnoreCase ) ) return false;
				if ( !string.IsNullOrEmpty( tagFilter ) && !go.Tags.Has( tagFilter ) ) return false;
				if ( !string.IsNullOrEmpty( componentFilter ) )
				{
					var hasComp = go.Components.GetAll()
						.Any( c => c.GetType().Name.Contains( componentFilter, StringComparison.OrdinalIgnoreCase ) );
					if ( !hasComp ) return false;
				}
				return true;
			} )
			.Take( limit )
			.Select( go => new
			{
				name = go.Name,
				guid = go.Id.ToString(),
				enabled = go.Enabled,
				worldPosition = FormatVector3( go.WorldPosition ),
				tags = go.Tags.TryGetAll().ToArray(),
				components = go.Components.GetAll().Select( c => c.GetType().Name ).ToArray(),
				parent = go.Parent?.Name
			} )
			.ToArray();

		return ToolHandlerBase.JsonResult( new
		{
			count = results.Length,
			truncated = results.Length >= limit,
			objects = results
		} );
	}

	// ──────────────────────────────────────────────
	//  find_game_objects_in_radius
	// ──────────────────────────────────────────────

	[Tool( "find_game_objects_in_radius", "Find GameObjects within a radius of a point, sorted by distance.", RequiresMainThread = true )]
	[Param( "position", "Center point as 'x,y,z'.", Required = true )]
	[Param( "radius", "Search radius in units.", Required = true, Type = "number" )]
	[Param( "limit", "Max results. Default: 50", Required = false, Type = "integer", Default = "50" )]
	public static object FindGameObjectsInRadius( JsonElement args )
	{
		var posStr = ToolHandlerBase.RequireString( args, "position" );
		var radius = ToolHandlerBase.GetFloat( args, "radius", 100f );
		var limit = ToolHandlerBase.GetInt( args, "limit", 50 );

		var center = RuntimeReflection.ParseVector3( posStr );

		var scene = SceneHelpers.ResolveActiveScene();
		if ( scene == null )
			return ToolHandlerBase.ErrorResult( "No active scene" );

		var radiusSq = radius * radius;

		var results = SceneHelpers.WalkAll( scene )
			.Select( go => new { go, dist = go.WorldPosition.Distance( center ) } )
			.Where( x => x.dist <= radius )
			.OrderBy( x => x.dist )
			.Take( limit )
			.Select( x => new
			{
				name = x.go.Name,
				guid = x.go.Id.ToString(),
				distance = Math.Round( x.dist, 2 ),
				worldPosition = FormatVector3( x.go.WorldPosition )
			} )
			.ToArray();

		return ToolHandlerBase.JsonResult( new
		{
			center = posStr,
			radius,
			count = results.Length,
			objects = results
		} );
	}

	// ──────────────────────────────────────────────
	//  select_game_object
	// ──────────────────────────────────────────────

	[Tool( "select_game_object", "Select a GameObject in the editor.", RequiresMainThread = true )]
	[Param( "name", "Name of the GameObject.", Required = false )]
	[Param( "guid", "GUID of the GameObject.", Required = false )]
	public static object SelectGameObject( JsonElement args )
	{
		var go = ResolveGameObject( args );
		if ( go == null )
			return GameObjectNotFound( args );

		var session = SceneEditorSession.Active;
		if ( session == null )
			return ToolHandlerBase.ErrorResult( "No active editor session" );

		session.Selection.Set( go );

		return ToolHandlerBase.JsonResult( new
		{
			selected = true,
			name = go.Name,
			guid = go.Id.ToString()
		} );
	}

	// ──────────────────────────────────────────────
	//  set_selected_objects
	// ──────────────────────────────────────────────

	[Tool( "set_selected_objects", "Set the editor selection to multiple GameObjects.", RequiresMainThread = true )]
	[Param( "guids", "Comma-separated GUIDs of GameObjects to select.", Required = false )]
	[Param( "names", "Comma-separated names of GameObjects to select. Used if guids not provided.", Required = false )]
	public static object SetSelectedObjects( JsonElement args )
	{
		var guidsStr = ToolHandlerBase.GetString( args, "guids" );
		var namesStr = ToolHandlerBase.GetString( args, "names" );

		var scene = SceneHelpers.ResolveActiveScene();
		if ( scene == null )
			return ToolHandlerBase.ErrorResult( "No active scene" );

		var found = new List<GameObject>();
		var notFound = new List<string>();

		if ( !string.IsNullOrEmpty( guidsStr ) )
		{
			foreach ( var guidStr in guidsStr.Split( ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) )
			{
				var go = SceneHelpers.FindByGuidOrName( scene, guidStr, null );
				if ( go != null ) found.Add( go );
				else notFound.Add( guidStr );
			}
		}
		else if ( !string.IsNullOrEmpty( namesStr ) )
		{
			foreach ( var n in namesStr.Split( ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) )
			{
				var go = SceneHelpers.FindByName( scene, n );
				if ( go != null ) found.Add( go );
				else notFound.Add( n );
			}
		}
		else
		{
			return ToolHandlerBase.ErrorResult( "Provide either 'guids' or 'names'" );
		}

		var session = SceneEditorSession.Active;
		if ( session == null )
			return ToolHandlerBase.ErrorResult( "No active editor session" );

		session.Selection.Clear();
		foreach ( var go in found )
			session.Selection.Add( go );

		return ToolHandlerBase.JsonResult( new
		{
			selectedCount = found.Count,
			selected = found.Select( go => new { name = go.Name, guid = go.Id.ToString() } ).ToArray(),
			notFound = notFound.Count > 0 ? notFound.ToArray() : null
		} );
	}

	// ──────────────────────────────────────────────
	//  clear_selection
	// ──────────────────────────────────────────────

	[Tool( "clear_selection", "Deselect all objects in the editor.", RequiresMainThread = true )]
	public static object ClearSelection()
	{
		var session = SceneEditorSession.Active;
		if ( session == null )
			return ToolHandlerBase.ErrorResult( "No active editor session" );

		var previousCount = session.Selection.Count();
		session.Selection.Clear();

		return ToolHandlerBase.JsonResult( new
		{
			cleared = true,
			previousSelectionCount = previousCount
		} );
	}

	// ──────────────────────────────────────────────
	//  Shared helpers
	// ──────────────────────────────────────────────

	private static GameObject ResolveGameObject( JsonElement args )
	{
		var name = ToolHandlerBase.GetString( args, "name" );
		var guid = ToolHandlerBase.GetString( args, "guid" );

		if ( string.IsNullOrEmpty( name ) && string.IsNullOrEmpty( guid ) )
			return null;

		var scene = SceneHelpers.ResolveActiveScene();
		if ( scene == null ) return null;

		return SceneHelpers.FindByGuidOrName( scene, guid, name );
	}

	private static object GameObjectNotFound( JsonElement args )
	{
		var name = ToolHandlerBase.GetString( args, "name" );
		var guid = ToolHandlerBase.GetString( args, "guid" );
		return ToolHandlerBase.ErrorResult( $"GameObject not found: {guid ?? name ?? "(no identifier)"}" );
	}

	internal static object FormatVector3( Vector3 v ) => new { x = Math.Round( v.x, 3 ), y = Math.Round( v.y, 3 ), z = Math.Round( v.z, 3 ) };
	internal static object FormatAngles( Angles a ) => new { pitch = Math.Round( a.pitch, 3 ), yaw = Math.Round( a.yaw, 3 ), roll = Math.Round( a.roll, 3 ) };
}
