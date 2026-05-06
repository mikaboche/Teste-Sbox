using System;
using System.IO;
using System.Text.Json;

namespace SboxPro;

public static class ScriptTools
{
	// ──────────────────────────────────────────────
	//  create_script
	// ──────────────────────────────────────────────

	[Tool( "create_script", "Create a new C# script file with a class stub extending Component." )]
	[Param( "path", "Script file path (e.g. 'Code/Player/PlayerController.cs'). Normalized under Assets/.", Required = true )]
	[Param( "class_name", "Class name. Default: derived from filename.", Required = false )]
	[Param( "base_class", "Base class to extend. Default: 'Component'", Required = false )]
	[Param( "namespace", "Namespace. Default: 'Sandbox'", Required = false )]
	public static object CreateScript( JsonElement args )
	{
		var path = ToolHandlerBase.RequireString( args, "path" );
		var baseClass = ToolHandlerBase.GetString( args, "base_class", "Component" );
		var ns = ToolHandlerBase.GetString( args, "namespace", "Sandbox" );

		if ( !path.EndsWith( ".cs", StringComparison.OrdinalIgnoreCase ) )
			path += ".cs";

		var className = ToolHandlerBase.GetString( args, "class_name" );
		if ( string.IsNullOrEmpty( className ) )
			className = Path.GetFileNameWithoutExtension( path );

		var safePath = PathNormalizer.ResolveAssetPath( path );
		if ( safePath == null )
			return ToolHandlerBase.ErrorResult( $"Path outside project: {path}" );

		if ( File.Exists( safePath ) )
			return ToolHandlerBase.ErrorResult( $"Script already exists: {PathNormalizer.ToRelative( safePath )}" );

		try
		{
			var dir = Path.GetDirectoryName( safePath );
			if ( !string.IsNullOrEmpty( dir ) )
				Directory.CreateDirectory( dir );

			var content = GenerateClassStub( className, baseClass, ns );
			File.WriteAllText( safePath, content );

			return ToolHandlerBase.JsonResult( new
			{
				created = true,
				path = PathNormalizer.ToRelative( safePath ),
				className,
				baseClass,
				@namespace = ns
			} );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Failed to create script: {ex.Message}" );
		}
	}

	// ──────────────────────────────────────────────
	//  edit_script
	// ──────────────────────────────────────────────

	[Tool( "edit_script", "Read or modify an existing C# script file." )]
	[Param( "path", "Script file path (relative to project root).", Required = true )]
	[Param( "content", "New file content. If omitted, returns current content.", Required = false )]
	[Param( "find", "Text to find for replacement.", Required = false )]
	[Param( "replace", "Replacement text (requires 'find' to be set).", Required = false )]
	public static object EditScript( JsonElement args )
	{
		var path = ToolHandlerBase.RequireString( args, "path" );
		var content = ToolHandlerBase.GetString( args, "content" );
		var find = ToolHandlerBase.GetString( args, "find" );
		var replace = ToolHandlerBase.GetString( args, "replace" );

		var normalized = PathNormalizer.NormalizeAssetPath( path );
		var absPath = PathNormalizer.ToAbsolute( normalized );

		if ( !File.Exists( absPath ) )
			return ToolHandlerBase.ErrorResult( $"Script not found: {normalized}" );

		// Read mode
		if ( string.IsNullOrEmpty( content ) && string.IsNullOrEmpty( find ) )
		{
			var existing = File.ReadAllText( absPath );
			return ToolHandlerBase.JsonResult( new
			{
				path = normalized,
				content = existing,
				lineCount = existing.Split( '\n' ).Length,
				sizeBytes = new FileInfo( absPath ).Length
			} );
		}

		try
		{
			// Find/replace mode
			if ( !string.IsNullOrEmpty( find ) )
			{
				var existing = File.ReadAllText( absPath );
				if ( !existing.Contains( find ) )
					return ToolHandlerBase.ErrorResult( $"Text to find not present in {normalized}" );

				content = existing.Replace( find, replace ?? "" );
			}

			// Write mode
			File.WriteAllText( absPath, content );

			return ToolHandlerBase.JsonResult( new
			{
				edited = true,
				path = normalized,
				lineCount = content.Split( '\n' ).Length,
				sizeBytes = new FileInfo( absPath ).Length
			} );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Failed to edit script: {ex.Message}" );
		}
	}

	// ──────────────────────────────────────────────
	//  delete_script
	// ──────────────────────────────────────────────

	[Tool( "delete_script", "Delete a C# script file." )]
	[Param( "path", "Script file path (relative to project root).", Required = true )]
	[Param( "confirm", "Must be true to actually delete. Safety check.", Required = true, Type = "boolean" )]
	public static object DeleteScript( JsonElement args )
	{
		var path = ToolHandlerBase.RequireString( args, "path" );
		var confirm = ToolHandlerBase.GetBool( args, "confirm", false );

		if ( !confirm )
			return ToolHandlerBase.ErrorResult( "Set confirm=true to delete the script" );

		var normalized = PathNormalizer.NormalizeAssetPath( path );
		var absPath = PathNormalizer.ToAbsolute( normalized );

		if ( !File.Exists( absPath ) )
			return ToolHandlerBase.ErrorResult( $"Script not found: {normalized}" );

		try
		{
			File.Delete( absPath );

			return ToolHandlerBase.JsonResult( new
			{
				deleted = true,
				path = normalized
			} );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Failed to delete script: {ex.Message}" );
		}
	}

	// ──────────────────────────────────────────────
	//  Helper
	// ──────────────────────────────────────────────

	private static string GenerateClassStub( string className, string baseClass, string ns )
	{
		return $@"using Sandbox;

namespace {ns};

public sealed class {className} : {baseClass}
{{
	protected override void OnUpdate()
	{{
	}}
}}
";
	}
}
