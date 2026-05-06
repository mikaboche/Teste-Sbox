using System;

namespace SboxPro;

public enum LogSeverity
{
	Trace,
	Info,
	Warning,
	Error
}

public sealed class LogEntry
{
	public DateTime Timestamp { get; init; }
	public LogSeverity Severity { get; init; }
	public string Source { get; init; }
	public string Message { get; init; }
	public string LoggerName { get; init; }
	public string StackTrace { get; init; }

	public override string ToString()
	{
		var tag = Severity switch
		{
			LogSeverity.Trace => "[TRACE]",
			LogSeverity.Warning => "[WARN]",
			LogSeverity.Error => "[ERROR]",
			_ => "[INFO]"
		};
		return $"{Timestamp:HH:mm:ss} {tag} [{Source}] {Message}";
	}
}
