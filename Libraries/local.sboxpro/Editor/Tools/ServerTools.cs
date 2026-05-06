using System;
using System.Text.Json;

namespace SboxPro;

public static class ServerTools
{
	[Tool( "get_server_status", "Returns S&Box Pro server status, version, uptime, tool count, and active sessions.", RequiresMainThread = false )]
	public static object GetServerStatus()
	{
		return ToolHandlerBase.JsonResult( new
		{
			status = SboxProServer.IsRunning ? "running" : "stopped",
			version = SboxProServer.Version,
			uptime_seconds = Math.Round( SboxProServer.UptimeSeconds, 1 ),
			tools_registered = ToolRegistry.Count,
			active_sessions = SboxProServer.SessionCount
		} );
	}

	[Tool( "list_tools", "Returns the names and descriptions of all registered tools.", RequiresMainThread = false )]
	[Param( "filter", "Optional substring filter for tool names", Required = false )]
	public static object ListTools( JsonElement args )
	{
		var filter = ToolHandlerBase.GetString( args, "filter" );
		var tools = ToolRegistry.Tools;
		var list = new System.Collections.Generic.List<object>();

		foreach ( var kvp in tools )
		{
			if ( filter != null && !kvp.Key.Contains( filter, StringComparison.OrdinalIgnoreCase ) )
				continue;

			list.Add( new { name = kvp.Value.Name, description = kvp.Value.Description } );
		}

		return ToolHandlerBase.JsonResult( new { count = list.Count, tools = list } );
	}
}
