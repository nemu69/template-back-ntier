using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Configuration;

namespace Core.Shared.Configuration.Logging;

public static class LoggingExtensions
{
	public static LoggerConfiguration WithCustomEnrichers(
		this LoggerEnrichmentConfiguration enrich, IConfiguration configuration)
			=> (enrich is null)
				? throw new ArgumentNullException(nameof(enrich))
				: enrich.With(new CustomEnrichers(configuration));
}