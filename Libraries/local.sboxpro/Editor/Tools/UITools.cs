using System;
using System.IO;
using System.Text.Json;
using Sandbox;
using Sandbox.UI;
using Editor;

namespace SboxPro;

public static class UITools
{
	// ──────────────────────────────────────────────
	//  create_razor_ui
	// ──────────────────────────────────────────────

	[Tool( "create_razor_ui", "Generate a .razor UI component file with a basic template." )]
	[Param( "path", "Razor file path (e.g. 'Code/UI/Scoreboard.razor'). Normalized under Assets/.", Required = true )]
	[Param( "class_name", "Component class name. Default: derived from filename.", Required = false )]
	[Param( "panel_type", "Panel base type: 'Panel' (default) or 'PanelComponent'.", Required = false, Enum = "Panel,PanelComponent" )]
	[Param( "include_styles", "Include a companion .razor.scss file. Default: true", Required = false, Type = "boolean", Default = "true" )]
	public static object CreateRazorUI( JsonElement args )
	{
		var path = ToolHandlerBase.RequireString( args, "path" );
		var panelType = ToolHandlerBase.GetString( args, "panel_type", "PanelComponent" );
		var includeStyles = ToolHandlerBase.GetBool( args, "include_styles", true );

		if ( !path.EndsWith( ".razor", StringComparison.OrdinalIgnoreCase ) )
			path += ".razor";

		var className = ToolHandlerBase.GetString( args, "class_name" );
		if ( string.IsNullOrEmpty( className ) )
			className = Path.GetFileNameWithoutExtension( path );

		var safePath = PathNormalizer.ResolveAssetPath( path );
		if ( safePath == null )
			return ToolHandlerBase.ErrorResult( $"Path outside project: {path}" );

		if ( File.Exists( safePath ) )
			return ToolHandlerBase.ErrorResult( $"Razor file already exists: {PathNormalizer.ToRelative( safePath )}" );

		try
		{
			var dir = Path.GetDirectoryName( safePath );
			if ( !string.IsNullOrEmpty( dir ) )
				Directory.CreateDirectory( dir );

			var razorContent = GenerateRazorTemplate( className, panelType );
			File.WriteAllText( safePath, razorContent );

			string scssPath = null;
			if ( includeStyles )
			{
				scssPath = safePath + ".scss";
				var scssContent = GenerateScssTemplate( className );
				File.WriteAllText( scssPath, scssContent );
			}

			return ToolHandlerBase.JsonResult( new
			{
				created = true,
				razorPath = PathNormalizer.ToRelative( safePath ),
				scssPath = scssPath != null ? PathNormalizer.ToRelative( scssPath ) : null,
				className,
				panelType
			} );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Failed to create razor UI: {ex.Message}" );
		}
	}

	// ──────────────────────────────────────────────
	//  add_screen_panel
	// ──────────────────────────────────────────────

	[Tool( "add_screen_panel", "Add a ScreenPanel component to a GameObject for HUD/menu rendering.", RequiresMainThread = true )]
	[Param( "name", "Name of the target GameObject.", Required = false )]
	[Param( "guid", "GUID of the target GameObject.", Required = false )]
	[Param( "create_go", "Create a new GameObject if name/guid not provided. Default: false", Required = false, Type = "boolean", Default = "false" )]
	[Param( "go_name", "Name for the new GameObject (if create_go=true). Default: 'Screen'", Required = false )]
	public static object AddScreenPanel( JsonElement args )
	{
		var createGO = ToolHandlerBase.GetBool( args, "create_go", false );
		var goName = ToolHandlerBase.GetString( args, "go_name", "Screen" );

		var scene = SceneHelpers.ResolveActiveScene();
		if ( scene == null )
			return ToolHandlerBase.ErrorResult( "No active scene" );

		GameObject go;
		if ( createGO )
		{
			go = SceneHelpers.CreateInScene( goName );
		}
		else
		{
			go = ResolveGO( args );
			if ( go == null )
				return ToolHandlerBase.ErrorResult( "GameObject not found. Provide name/guid or set create_go=true." );
		}

		var typeDesc = TypeLibrary.GetType( typeof( ScreenPanel ) );
		if ( typeDesc == null )
			return ToolHandlerBase.ErrorResult( "ScreenPanel type not found in TypeLibrary" );

		var existing = go.Components.Get<ScreenPanel>();
		if ( existing != null )
			return ToolHandlerBase.ErrorResult( $"'{go.Name}' already has a ScreenPanel component" );

		var comp = go.Components.Create( typeDesc );

		return ToolHandlerBase.JsonResult( new
		{
			added = true,
			gameObject = go.Name,
			gameObjectGuid = go.Id.ToString(),
			componentType = "ScreenPanel",
			componentGuid = comp.Id.ToString()
		} );
	}

	// ──────────────────────────────────────────────
	//  add_world_panel
	// ──────────────────────────────────────────────

	[Tool( "add_world_panel", "Add a WorldPanel component to a GameObject for 3D-space UI rendering.", RequiresMainThread = true )]
	[Param( "name", "Name of the target GameObject.", Required = false )]
	[Param( "guid", "GUID of the target GameObject.", Required = false )]
	[Param( "create_go", "Create a new GameObject if name/guid not provided. Default: false", Required = false, Type = "boolean", Default = "false" )]
	[Param( "go_name", "Name for the new GameObject (if create_go=true). Default: 'WorldUI'", Required = false )]
	[Param( "position", "World position as 'x,y,z' (for new GO). Default: '0,0,0'", Required = false )]
	public static object AddWorldPanel( JsonElement args )
	{
		var createGO = ToolHandlerBase.GetBool( args, "create_go", false );
		var goName = ToolHandlerBase.GetString( args, "go_name", "WorldUI" );
		var posStr = ToolHandlerBase.GetString( args, "position" );

		var scene = SceneHelpers.ResolveActiveScene();
		if ( scene == null )
			return ToolHandlerBase.ErrorResult( "No active scene" );

		GameObject go;
		if ( createGO )
		{
			go = SceneHelpers.CreateInScene( goName );
			if ( !string.IsNullOrEmpty( posStr ) )
				go.WorldPosition = RuntimeReflection.ParseVector3( posStr );
		}
		else
		{
			go = ResolveGO( args );
			if ( go == null )
				return ToolHandlerBase.ErrorResult( "GameObject not found. Provide name/guid or set create_go=true." );
		}

		// Sandbox.WorldPanel and Sandbox.UI.WorldPanel both exist. We want the modern Component
		// (Sandbox.WorldPanel — "Renders attached PanelComponents to the world in 3D space"),
		// not the legacy Sandbox.UI.WorldPanel ("interactive 2D panel rendered in 3D world").
		var typeDesc = TypeLibrary.GetType( typeof( Sandbox.WorldPanel ) );
		if ( typeDesc == null )
			return ToolHandlerBase.ErrorResult( "WorldPanel type not found in TypeLibrary" );

		var existing = go.Components.Get<Sandbox.WorldPanel>();
		if ( existing != null )
			return ToolHandlerBase.ErrorResult( $"'{go.Name}' already has a WorldPanel component" );

		var comp = go.Components.Create( typeDesc );

		return ToolHandlerBase.JsonResult( new
		{
			added = true,
			gameObject = go.Name,
			gameObjectGuid = go.Id.ToString(),
			componentType = "WorldPanel",
			componentGuid = comp.Id.ToString(),
			worldPosition = GameObjectTools.FormatVector3( go.WorldPosition )
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
		if ( scene == null ) return null;

		return SceneHelpers.FindByGuidOrName( scene, guid, name );
	}

	private static string GenerateRazorTemplate( string className, string panelType )
	{
		return $@"@using Sandbox;
@using Sandbox.UI;
@inherits {panelType}

<root>
	<div class=""{className.ToLowerInvariant()}-container"">
		<label>Hello from {className}</label>
	</div>
</root>

@code
{{
	protected override void OnUpdate()
	{{
	}}
}}
";
	}

	private static string GenerateScssTemplate( string className )
	{
		var lowerName = className.ToLowerInvariant();
		return $@".{lowerName}-container {{
	position: absolute;
	left: 0;
	top: 0;
	width: 100%;
	height: 100%;
	pointer-events: none;

	label {{
		font-size: 24px;
		color: white;
		text-shadow: 2px 2px 4px rgba(0, 0, 0, 0.5);
	}}
}}
";
	}
}
