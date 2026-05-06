using System.Text.Json;

namespace SboxPro;

public static class ToolHandlerBase
{
	internal static object TextResult( string text ) => new
	{
		content = new object[] { new { type = "text", text } }
	};

	internal static object JsonResult( object data )
	{
		var json = JsonSerializer.Serialize( data, SboxProServer.JsonOptions );
		return TextResult( json );
	}

	internal static object ErrorResult( string message ) => new
	{
		content = new object[] { new { type = "text", text = $"Error: {message}" } },
		isError = true
	};

	internal static string GetString( JsonElement args, string name, string defaultValue = null )
	{
		if ( args.ValueKind != JsonValueKind.Undefined && args.TryGetProperty( name, out var prop ) && prop.ValueKind == JsonValueKind.String )
			return prop.GetString();
		return defaultValue;
	}

	internal static int GetInt( JsonElement args, string name, int defaultValue = 0 )
	{
		if ( args.ValueKind != JsonValueKind.Undefined && args.TryGetProperty( name, out var prop ) && prop.ValueKind == JsonValueKind.Number )
			return prop.GetInt32();
		return defaultValue;
	}

	internal static float GetFloat( JsonElement args, string name, float defaultValue = 0f )
	{
		if ( args.ValueKind != JsonValueKind.Undefined && args.TryGetProperty( name, out var prop ) && prop.ValueKind == JsonValueKind.Number )
			return prop.GetSingle();
		return defaultValue;
	}

	internal static bool GetBool( JsonElement args, string name, bool defaultValue = false )
	{
		if ( args.ValueKind != JsonValueKind.Undefined && args.TryGetProperty( name, out var prop ) )
		{
			if ( prop.ValueKind == JsonValueKind.True ) return true;
			if ( prop.ValueKind == JsonValueKind.False ) return false;
		}
		return defaultValue;
	}

	internal static string RequireString( JsonElement args, string name )
	{
		var val = GetString( args, name );
		if ( val == null )
			throw new System.ArgumentException( $"Missing required parameter: {name}" );
		return val;
	}
}
