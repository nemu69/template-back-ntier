using Serilog.Core;

namespace Core.Shared.Configuration.Logging;

public static class LogSwitchLevel
{
	public static LoggingLevelSwitch LevelSwitch { get; set; } = new(Serilog.Events.LogEventLevel.Information);
}