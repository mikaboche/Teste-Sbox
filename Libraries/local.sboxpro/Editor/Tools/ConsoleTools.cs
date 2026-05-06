using System;
using System.Text.Json;
using Editor;

namespace SboxPro;

public static class ConsoleTools
{
	// ──────────────────────────────────────────────
	//  run_console_command
	// ──────────────────────────────────────────────

	[Tool( "run_console_command", "Run an editor console command (single command line).", RequiresMainThread = true )]
	[Param( "command", "Console command to execute (e.g. 'sv_cheats 1', 'r_show_fps 1').", Required = true )]
	public static object RunConsoleCommand( JsonElement args )
	{
		var cmd = ToolHandlerBase.RequireString( args, "command" );

		try
		{
			ConsoleSystem.Run( cmd );
			return ToolHandlerBase.JsonResult( new
			{
				executed = true,
				command = cmd
			} );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Console command failed: {ex.Message}" );
		}
	}

	// ──────────────────────────────────────────────
	//  get_console_var
	// ──────────────────────────────────────────────

	[Tool( "get_console_var", "Read the value of a console variable (ConVar)." )]
	[Param( "name", "ConVar name.", Required = true )]
	[Param( "type", "Result type: 'string' (default), 'int', 'float'.", Required = false, Enum = "string,int,float", Default = "string" )]
	public static object GetConsoleVar( JsonElement args )
	{
		var name = ToolHandlerBase.RequireString( args, "name" );
		var type = ToolHandlerBase.GetString( args, "type", "string" )?.ToLowerInvariant();

		try
		{
			object value = type switch
			{
				"int" => ConsoleSystem.GetValueInt( name, 0 ),
				"float" => ConsoleSystem.GetValueFloat( name, 0f ),
				_ => ConsoleSystem.GetValue( name, "" )
			};

			return ToolHandlerBase.JsonResult( new
			{
				name,
				type,
				value
			} );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Failed to read ConVar '{name}': {ex.Message}" );
		}
	}

	// ──────────────────────────────────────────────
	//  set_console_var
	// ──────────────────────────────────────────────

	[Tool( "set_console_var", "Set the value of a console variable (ConVar). Permission-checked by the engine.", RequiresMainThread = true )]
	[Param( "name", "ConVar name.", Required = true )]
	[Param( "value", "Value to set (string; engine parses numeric/bool as needed).", Required = true )]
	public static object SetConsoleVar( JsonElement args )
	{
		var name = ToolHandlerBase.RequireString( args, "name" );
		var value = ToolHandlerBase.RequireString( args, "value" );

		try
		{
			ConsoleSystem.SetValue( name, value );
			var readBack = ConsoleSystem.GetValue( name, "" );
			return ToolHandlerBase.JsonResult( new
			{
				set = true,
				name,
				requestedValue = value,
				currentValue = readBack
			} );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.ErrorResult( $"Failed to set ConVar '{name}': {ex.Message}" );
		}
	}
}
