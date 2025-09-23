namespace Core.Shared.Models.DB.System.Logs;

public partial class LogEntry
{
	public int ID { get; set; }
	public DateTimeOffset TS { get; set; } = DateTimeOffset.Now;
	public string Message { get; set; } = string.Empty;
	public string MessageTemplate { get; set; } = string.Empty;
	public string Level { get; set; } = "Debug";
	public string? Exception { get; set; }
	public string? Properties { get; set; }
	public string? Instance { get; set; }
}