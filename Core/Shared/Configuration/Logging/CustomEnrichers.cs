using Core.Shared.Dictionaries;
using Microsoft.Extensions.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace Core.Shared.Configuration.Logging;

public class CustomEnrichers : ILogEventEnricher
{
	private readonly string? _instance;

	public CustomEnrichers(IConfiguration configuration)
	{
		_instance = configuration.GetValue<string>(ConfigDictionary.InstanceRID)
			?? System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name;
	}

	public void Enrich(
		LogEvent logEvent,
		ILogEventPropertyFactory propertyFactory)
		=> logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("Instance", _instance));
}