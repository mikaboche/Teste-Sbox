using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Sandbox;
using Editor;

namespace SboxPro;

public static class DiagnosticsTools
{
	[Tool( "get_compile_errors", "Returns current compile diagnostics (errors, warnings) from all projects in the editor.", RequiresMainThread = true )]
	[Param( "severity", "Filter by minimum severity: Error, Warning, Info, Hidden. Default: Warning", Required = false, Type = "string", Enum = "Error,Warning,Info,Hidden", Default = "Warning" )]
	[Param( "project_filter", "Optional substring filter for project name", Required = false )]
	[Param( "limit", "Max diagnostics to return. Default: 100", Required = false, Type = "integer", Default = "100" )]
	public static object GetCompileErrors( JsonElement args )
	{
		var severityStr = ToolHandlerBase.GetString( args, "severity", "Warning" );
		var projectFilter = ToolHandlerBase.GetString( args, "project_filter" );
		var limit = ToolHandlerBase.GetInt( args, "limit", 100 );

		if ( !Enum.TryParse<Microsoft.CodeAnalysis.DiagnosticSeverity>( severityStr, true, out var minSeverity ) )
			return ToolHandlerBase.ErrorResult( $"Invalid severity '{severityStr}'. Use Error, Warning, Info, or Hidden." );

		var projects = EditorUtility.Projects.GetAll();
		var results = new List<object>();
		var projectSummaries = new List<object>();

		foreach ( var project in projects )
		{
			var name = project.Config?.FullIdent ?? project.GetType().Name;

			if ( projectFilter != null && !name.Contains( projectFilter, StringComparison.OrdinalIgnoreCase ) )
				continue;

			if ( !project.HasCompiler )
			{
				projectSummaries.Add( new { project_name = name, has_compiler = false, status = "no_compiler" } );
				continue;
			}

			var asm = System.AppDomain.CurrentDomain.GetAssemblies()
				.FirstOrDefault( a => a.GetName().Name == project.Config?.FullIdent );
			if ( asm == null )
			{
				projectSummaries.Add( new { project_name = name, has_compiler = true, status = "assembly_not_loaded" } );
				continue;
			}

			var compiler = EditorUtility.Projects.ResolveCompiler( asm );
			if ( compiler == null )
			{
				projectSummaries.Add( new { project_name = name, has_compiler = true, status = "compiler_not_resolved" } );
				continue;
			}

			var output = compiler.Output;
			var buildSuccess = output?.Successful ?? compiler.BuildSuccess;

			projectSummaries.Add( new
			{
				project_name = name,
				has_compiler = true,
				status = buildSuccess ? "success" : "failed",
				compiler_name = compiler.Name
			} );

			if ( output?.Diagnostics == null )
				continue;

			foreach ( var diag in output.Diagnostics )
			{
				if ( diag.Severity < minSeverity )
					continue;

				if ( results.Count >= limit )
					break;

				var location = diag.Location;
				var lineSpan = location?.GetMappedLineSpan();

				results.Add( new
				{
					id = diag.Id,
					severity = diag.Severity.ToString(),
					message = diag.GetMessage(),
					project_name = name,
					file = lineSpan?.Path,
					line = lineSpan?.StartLinePosition.Line + 1,
					column = lineSpan?.StartLinePosition.Character + 1
				} );
			}
		}

		return ToolHandlerBase.JsonResult( new
		{
			total_diagnostics = results.Count,
			limit_applied = results.Count >= limit,
			projects = projectSummaries,
			diagnostics = results
		} );
	}

	[Tool( "get_console_output", "Returns captured console/log output from the S&Box editor. Starts engine log capture on first call.", RequiresMainThread = false )]
	[Param( "severity", "Minimum severity filter: Trace, Info, Warning, Error. Default: Trace", Required = false, Type = "string", Enum = "Trace,Info,Warning,Error", Default = "Trace" )]
	[Param( "logger", "Filter by logger name substring (case-insensitive)", Required = false )]
	[Param( "source", "Filter by source substring (case-insensitive)", Required = false )]
	[Param( "since", "Only entries after this ISO-8601 timestamp (e.g. 2026-04-29T18:00:00)", Required = false )]
	[Param( "limit", "Max entries to return (most recent first). Default: 100", Required = false, Type = "integer", Default = "100" )]
	[Param( "include_stack", "Include stack traces in output. Default: false", Required = false, Type = "boolean", Default = "false" )]
	public static object GetConsoleOutput( JsonElement args )
	{
		SboxProLog.HookEngineConsole();

		var severityStr = ToolHandlerBase.GetString( args, "severity", "Trace" );
		var loggerFilter = ToolHandlerBase.GetString( args, "logger" );
		var sourceFilter = ToolHandlerBase.GetString( args, "source" );
		var sinceStr = ToolHandlerBase.GetString( args, "since" );
		var limit = ToolHandlerBase.GetInt( args, "limit", 100 );
		var includeStack = ToolHandlerBase.GetBool( args, "include_stack", false );

		if ( !Enum.TryParse<LogSeverity>( severityStr, true, out var minSeverity ) )
			return ToolHandlerBase.ErrorResult( $"Invalid severity '{severityStr}'. Use Trace, Info, Warning, or Error." );

		DateTime? since = null;
		if ( sinceStr != null )
		{
			if ( DateTime.TryParse( sinceStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed ) )
				since = parsed;
			else
				return ToolHandlerBase.ErrorResult( $"Invalid timestamp '{sinceStr}'. Use ISO-8601 format." );
		}

		var entries = SboxProLog.Entries;
		var filtered = new List<object>();

		for ( int i = entries.Count - 1; i >= 0 && filtered.Count < limit; i-- )
		{
			var e = entries[i];

			if ( e.Severity < minSeverity )
				continue;

			if ( since.HasValue && e.Timestamp < since.Value )
				continue;

			if ( loggerFilter != null && ( e.LoggerName == null || !e.LoggerName.Contains( loggerFilter, StringComparison.OrdinalIgnoreCase ) ) )
				continue;

			if ( sourceFilter != null && ( e.Source == null || !e.Source.Contains( sourceFilter, StringComparison.OrdinalIgnoreCase ) ) )
				continue;

			var entry = new Dictionary<string, object>
			{
				["timestamp"] = e.Timestamp.ToString( "o" ),
				["severity"] = e.Severity.ToString(),
				["source"] = e.Source,
				["message"] = e.Message
			};

			if ( e.LoggerName != null )
				entry["logger"] = e.LoggerName;

			if ( includeStack && e.StackTrace != null )
				entry["stack"] = e.StackTrace;

			filtered.Add( entry );
		}

		return ToolHandlerBase.JsonResult( new
		{
			total_in_buffer = SboxProLog.EntryCount,
			buffer_capacity = SboxProLog.MaxEntries,
			returned = filtered.Count,
			engine_capture_active = true,
			entries = filtered
		} );
	}

	[Tool( "clear_console", "Clears the internal log buffer. Does not affect the S&Box editor console.", RequiresMainThread = false )]
	public static object ClearConsole()
	{
		var count = SboxProLog.EntryCount;
		SboxProLog.Clear();

		return ToolHandlerBase.JsonResult( new
		{
			cleared = count,
			message = $"Cleared {count} log entries from buffer."
		} );
	}
}
