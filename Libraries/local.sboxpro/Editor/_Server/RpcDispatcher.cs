using System;
using System.Text.Json;
using System.Threading.Tasks;
using Sandbox;

namespace SboxPro;

internal static class RpcDispatcher
{
	internal static async Task ProcessRpcRequest(
		McpSession session,
		object id,
		string method,
		string rawBody,
		JsonSerializerOptions jsonOptions )
	{
		object result = null;
		object error = null;

		using var doc = JsonDocument.Parse( rawBody );
		var root = doc.RootElement;

		try
		{
			if ( method == "initialize" )
			{
				result = new
				{
					protocolVersion = "2024-11-05",
					capabilities = new { tools = new { listChanged = true } },
					serverInfo = new { name = "sbox-pro", version = SboxProServer.Version }
				};
			}
			else if ( method == "tools/list" )
			{
				result = new { tools = ToolRegistry.GetAllSchemas() };
			}
			else if ( method == "tools/call" )
			{
				var paramsEl = root.GetProperty( "params" );
				var toolName = paramsEl.GetProperty( "name" ).GetString();
				var args = paramsEl.TryGetProperty( "arguments", out var a ) ? a : default;

				var tool = ToolRegistry.Get( toolName );
				if ( tool == null )
					throw new InvalidOperationException( $"Tool '{toolName}' not found" );

				if ( tool.RequiresMainThread )
				{
					SboxProLog.Info( "Dispatcher", $"Switching to main thread for {toolName}" );
					await GameTask.MainThread();
				}

				SboxProLog.Info( "Dispatcher", $"Executing tool: {toolName}" );
				result = await ToolRegistry.Invoke( tool, args );
			}
			else
			{
				error = new { code = -32601, message = $"Method '{method}' not found" };
			}
		}
		catch ( ArgumentException ex )
		{
			error = new { code = -32602, message = ex.Message };
		}
		catch ( Exception ex )
		{
			SboxProLog.Error( "Dispatcher", $"Tool error: {ex.Message}" );
			error = new { code = -32603, message = $"Internal error: {ex.Message}" };
		}

		var response = new { jsonrpc = "2.0", id, result, error };
		var json = JsonSerializer.Serialize( response, jsonOptions );
		await SboxProServer.SendSseEvent( session, "message", json );
	}
}
