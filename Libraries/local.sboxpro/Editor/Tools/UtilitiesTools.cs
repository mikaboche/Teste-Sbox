using System;
using System.Linq;
using System.Text.Json;
using Sandbox;
using Editor;

namespace SboxPro;

public static class UtilitiesTools
{
	// ──────────────────────────────────────────────
	//  open_asset
	// ──────────────────────────────────────────────

	[Tool( "open_asset", "Open an asset in its native editor (e.g. .scene → scene editor, .vmdl → model editor).", RequiresMainThread = true )]
	[Param( "path", "Asset path relative to project (e.g. 'scenes/main.scene').", Required = true )]
	[Param( "editor", "Optional native editor identifier. Default: asset's default editor.", Required = false )]
	public static object OpenAsset( JsonElement args )
	{
		var path = ToolHandlerBase.RequireString( args, "path" );
		var editor = ToolHandlerBase.GetString( args, "editor" );

		var asset = AssetSystem.FindByPath( path );
		if ( asset == null )
			return ToolHandlerBase.ErrorResult( $"Asset not found: {path}" );

		try
		{
			asset.OpenInEditor( editor );
			return ToolHandlerBase.JsonResult( new
			{
				opened = true,
				path = asset.Path,
				name = asset.Name,
				assetType = asset.AssetType?.FriendlyName
			} );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Failed to open asset: {ex.Message}" );
		}
	}

	// ──────────────────────────────────────────────
	//  batch_transform
	// ──────────────────────────────────────────────

	[Tool( "batch_transform", "Apply transforms (translate/rotate/scale) to multiple GameObjects in one call.", RequiresMainThread = true )]
	[Param( "names", "Comma-separated GameObject names.", Required = false )]
	[Param( "guids", "Comma-separated GameObject GUIDs.", Required = false )]
	[Param( "translate", "World-space translation delta 'x,y,z' (added to current position).", Required = false )]
	[Param( "rotate", "Rotation delta 'pitch,yaw,roll' degrees (multiplied with current rotation).", Required = false )]
	[Param( "scale", "Local scale multiplier 'x,y,z' (multiplied with current scale).", Required = false )]
	[Param( "set_position", "Absolute world position 'x,y,z' (overrides translate).", Required = false )]
	[Param( "set_rotation", "Absolute rotation 'pitch,yaw,roll' degrees (overrides rotate).", Required = false )]
	[Param( "set_scale", "Absolute local scale 'x,y,z' (overrides scale).", Required = false )]
	public static object BatchTransform( JsonElement args )
	{
		var scene = SceneHelpers.ResolveActiveScene();
		if ( scene == null ) return ToolHandlerBase.ErrorResult( "No active scene." );

		var namesStr = ToolHandlerBase.GetString( args, "names" );
		var guidsStr = ToolHandlerBase.GetString( args, "guids" );

		if ( string.IsNullOrEmpty( namesStr ) && string.IsNullOrEmpty( guidsStr ) )
			return ToolHandlerBase.ErrorResult( "Provide names or guids." );

		var targets = new System.Collections.Generic.List<GameObject>();
		if ( !string.IsNullOrEmpty( guidsStr ) )
		{
			foreach ( var g in guidsStr.Split( ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) )
			{
				var go = SceneHelpers.FindByGuidOrName( scene, g, null );
				if ( go != null ) targets.Add( go );
			}
		}
		if ( !string.IsNullOrEmpty( namesStr ) )
		{
			foreach ( var n in namesStr.Split( ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) )
			{
				var go = SceneHelpers.FindByGuidOrName( scene, null, n );
				if ( go != null && !targets.Contains( go ) ) targets.Add( go );
			}
		}

		if ( targets.Count == 0 )
			return ToolHandlerBase.ErrorResult( "No matching GameObjects found." );

		var translate = ParseVec3( ToolHandlerBase.GetString( args, "translate" ), Vector3.Zero );
		var rotateAng = ParseVec3( ToolHandlerBase.GetString( args, "rotate" ), Vector3.Zero );
		var scaleMul = ParseVec3( ToolHandlerBase.GetString( args, "scale" ), Vector3.One );

		var setPosStr = ToolHandlerBase.GetString( args, "set_position" );
		var setRotStr = ToolHandlerBase.GetString( args, "set_rotation" );
		var setScaleStr = ToolHandlerBase.GetString( args, "set_scale" );

		var hasSetPos = !string.IsNullOrEmpty( setPosStr );
		var hasSetRot = !string.IsNullOrEmpty( setRotStr );
		var hasSetScale = !string.IsNullOrEmpty( setScaleStr );
		var hasTranslate = !string.IsNullOrWhiteSpace( ToolHandlerBase.GetString( args, "translate" ) );
		var hasRotate = !string.IsNullOrWhiteSpace( ToolHandlerBase.GetString( args, "rotate" ) );
		var hasScale = !string.IsNullOrWhiteSpace( ToolHandlerBase.GetString( args, "scale" ) );

		var setPos = hasSetPos ? ParseVec3( setPosStr, Vector3.Zero ) : Vector3.Zero;
		var setRotAng = hasSetRot ? ParseVec3( setRotStr, Vector3.Zero ) : Vector3.Zero;
		var setScale = hasSetScale ? ParseVec3( setScaleStr, Vector3.One ) : Vector3.One;

		foreach ( var go in targets )
		{
			if ( hasSetPos ) go.WorldPosition = setPos;
			else if ( hasTranslate ) go.WorldPosition = go.WorldPosition + translate;

			if ( hasSetRot ) go.WorldRotation = new Angles( setRotAng.x, setRotAng.y, setRotAng.z ).ToRotation();
			else if ( hasRotate )
			{
				var delta = new Angles( rotateAng.x, rotateAng.y, rotateAng.z ).ToRotation();
				go.WorldRotation = go.WorldRotation * delta;
			}

			if ( hasSetScale ) go.LocalScale = setScale;
			else if ( hasScale ) go.LocalScale = go.LocalScale * scaleMul;
		}

		return ToolHandlerBase.JsonResult( new
		{
			updated = targets.Count,
			gameObjects = targets.Select( g => g.Name ).ToArray()
		} );
	}

	// ──────────────────────────────────────────────
	//  Helpers
	// ──────────────────────────────────────────────

	private static Vector3 ParseVec3( string s, Vector3 fallback )
	{
		if ( string.IsNullOrWhiteSpace( s ) ) return fallback;
		var parts = s.Split( ',' );
		if ( parts.Length < 3 ) return fallback;
		var ci = System.Globalization.CultureInfo.InvariantCulture;
		if ( !float.TryParse( parts[0], System.Globalization.NumberStyles.Float, ci, out var x ) ) return fallback;
		if ( !float.TryParse( parts[1], System.Globalization.NumberStyles.Float, ci, out var y ) ) return fallback;
		if ( !float.TryParse( parts[2], System.Globalization.NumberStyles.Float, ci, out var z ) ) return fallback;
		return new Vector3( x, y, z );
	}
}
