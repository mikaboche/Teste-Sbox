using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Sandbox;

namespace SboxPro;

public static class ValidationTools
{
	// ──────────────────────────────────────────────
	//  validate_scene
	// ──────────────────────────────────────────────

	[Tool( "validate_scene", "Walk the active scene and report all reference-typed component properties that are null or broken (Resource/GameObject/Component refs).", RequiresMainThread = true )]
	[Param( "include_null_refs", "Include null reference properties in report (info-level). Default: true.", Required = false, Type = "boolean", Default = "true" )]
	[Param( "include_disabled", "Walk disabled GameObjects too. Default: true.", Required = false, Type = "boolean", Default = "true" )]
	public static object ValidateScene( JsonElement args )
	{
		var includeNull = ToolHandlerBase.GetBool( args, "include_null_refs", true );
		var includeDisabled = ToolHandlerBase.GetBool( args, "include_disabled", true );

		var report = WalkAndAudit( includeNull, includeDisabled, missingOnly: false );
		if ( report == null )
			return ToolHandlerBase.ErrorResult( "No active scene." );

		return ToolHandlerBase.JsonResult( report );
	}

	// ──────────────────────────────────────────────
	//  find_missing_references
	// ──────────────────────────────────────────────

	[Tool( "find_missing_references", "Subset of validate_scene: only stale/broken references (resource path doesn't resolve, ref pointing to deleted target).", RequiresMainThread = true )]
	[Param( "include_disabled", "Walk disabled GameObjects too. Default: true.", Required = false, Type = "boolean", Default = "true" )]
	public static object FindMissingReferences( JsonElement args )
	{
		var includeDisabled = ToolHandlerBase.GetBool( args, "include_disabled", true );

		var report = WalkAndAudit( includeNullRefs: false, includeDisabled, missingOnly: true );
		if ( report == null )
			return ToolHandlerBase.ErrorResult( "No active scene." );

		return ToolHandlerBase.JsonResult( report );
	}

	// ──────────────────────────────────────────────
	//  Audit walker
	// ──────────────────────────────────────────────

	private static object WalkAndAudit( bool includeNullRefs, bool includeDisabled, bool missingOnly )
	{
		var scene = SceneHelpers.ResolveActiveScene();
		if ( scene == null ) return null;

		var brokenResources = new List<object>();
		var brokenRefs = new List<object>();
		var nullResources = new List<object>();
		var nullGameObjects = new List<object>();
		var nullComponents = new List<object>();
		var listBrokenEntries = new List<object>();

		int gameObjectsScanned = 0;
		int componentsScanned = 0;
		int propertiesScanned = 0;

		foreach ( var go in SceneHelpers.WalkAll( scene ) )
		{
			if ( !includeDisabled && !go.Active ) continue;
			gameObjectsScanned++;

			foreach ( var comp in go.Components.GetAll() )
			{
				componentsScanned++;

				List<RuntimeReflection.ComponentPropertyInfo> propsInfo;
				try { propsInfo = RuntimeReflection.GetComponentProperties( comp ); }
				catch { continue; }

				foreach ( var p in propsInfo )
				{
					propertiesScanned++;
					if ( !p.CanRead ) continue;
					if ( !p.IsResource && !p.IsGameObject && !p.IsComponent && !p.IsList ) continue;

					object value;
					try { (value, _) = RuntimeReflection.GetPropertyValue( comp, p.Name ); }
					catch { continue; }

					var ctx = new
					{
						gameObject = go.Name,
						gameObjectGuid = go.Id.ToString(),
						componentType = comp.GetType().Name,
						componentGuid = comp.Id.ToString(),
						property = p.Name,
						propertyType = p.TypeName
					};

					// Lists: check each entry
					if ( p.IsList && value is IEnumerable list && value is not string )
					{
						int idx = 0;
						foreach ( var item in list )
						{
							if ( item == null )
							{
								listBrokenEntries.Add( new
								{
									ctx.gameObject,
									ctx.gameObjectGuid,
									ctx.componentType,
									ctx.componentGuid,
									ctx.property,
									index = idx,
									reason = "null entry in list"
								} );
							}
							else if ( item is Resource res && IsBrokenResource( res ) )
							{
								listBrokenEntries.Add( new
								{
									ctx.gameObject,
									ctx.gameObjectGuid,
									ctx.componentType,
									ctx.componentGuid,
									ctx.property,
									index = idx,
									reason = "resource path does not resolve",
									path = res.ResourcePath
								} );
							}
							idx++;
						}
						continue;
					}

					// Single-ref properties
					if ( value == null )
					{
						if ( missingOnly ) continue;
						if ( !includeNullRefs ) continue;

						if ( p.IsResource )
							nullResources.Add( new { ctx.gameObject, ctx.gameObjectGuid, ctx.componentType, ctx.componentGuid, ctx.property, ctx.propertyType } );
						else if ( p.IsGameObject )
							nullGameObjects.Add( new { ctx.gameObject, ctx.gameObjectGuid, ctx.componentType, ctx.componentGuid, ctx.property } );
						else if ( p.IsComponent )
							nullComponents.Add( new { ctx.gameObject, ctx.gameObjectGuid, ctx.componentType, ctx.componentGuid, ctx.property, ctx.propertyType } );
						continue;
					}

					// Non-null but stale checks
					if ( p.IsResource && value is Resource resource && IsBrokenResource( resource ) )
					{
						brokenResources.Add( new
						{
							ctx.gameObject,
							ctx.gameObjectGuid,
							ctx.componentType,
							ctx.componentGuid,
							ctx.property,
							resourcePath = resource.ResourcePath,
							reason = "resource path set but file does not exist on disk"
						} );
					}
					else if ( p.IsGameObject && value is GameObject targetGO && IsStaleGameObject( scene, targetGO ) )
					{
						brokenRefs.Add( new
						{
							ctx.gameObject,
							ctx.gameObjectGuid,
							ctx.componentType,
							ctx.componentGuid,
							ctx.property,
							targetGuid = targetGO.Id.ToString(),
							reason = "GameObject ref points outside active scene tree"
						} );
					}
					else if ( p.IsComponent && value is Component targetComp && IsStaleComponent( scene, targetComp ) )
					{
						brokenRefs.Add( new
						{
							ctx.gameObject,
							ctx.gameObjectGuid,
							ctx.componentType,
							ctx.componentGuid,
							ctx.property,
							targetComponentGuid = targetComp.Id.ToString(),
							reason = "Component ref points outside active scene tree"
						} );
					}
				}
			}
		}

		var totalIssues = brokenResources.Count + brokenRefs.Count + listBrokenEntries.Count
			+ (missingOnly ? 0 : nullResources.Count + nullGameObjects.Count + nullComponents.Count);

		if ( missingOnly )
		{
			return new
			{
				scenePath = scene.Source?.ResourcePath,
				gameObjectsScanned,
				componentsScanned,
				propertiesScanned,
				totalIssues,
				brokenResources = brokenResources.ToArray(),
				brokenRefs = brokenRefs.ToArray(),
				listBrokenEntries = listBrokenEntries.ToArray()
			};
		}

		return new
		{
			scenePath = scene.Source?.ResourcePath,
			gameObjectsScanned,
			componentsScanned,
			propertiesScanned,
			totalIssues,
			brokenResources = brokenResources.ToArray(),
			brokenRefs = brokenRefs.ToArray(),
			listBrokenEntries = listBrokenEntries.ToArray(),
			nullResources = nullResources.ToArray(),
			nullGameObjects = nullGameObjects.ToArray(),
			nullComponents = nullComponents.ToArray()
		};
	}

	private static bool IsBrokenResource( Resource r )
	{
		if ( r == null ) return false;
		var path = r.ResourcePath;
		if ( string.IsNullOrEmpty( path ) ) return false;

		var safe = PathNormalizer.ResolveAssetPath( path );
		if ( string.IsNullOrEmpty( safe ) ) return true;
		return !File.Exists( safe );
	}

	private static bool IsStaleGameObject( Scene scene, GameObject target )
	{
		if ( target == null ) return false;
		// If the target's scene root differs from the active scene, treat as stale.
		var found = SceneHelpers.WalkAll( scene ).Any( g => g.Id == target.Id );
		return !found;
	}

	private static bool IsStaleComponent( Scene scene, Component target )
	{
		if ( target == null || target.GameObject == null ) return false;
		return IsStaleGameObject( scene, target.GameObject );
	}
}
