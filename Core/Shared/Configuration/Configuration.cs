using System.Configuration;
using Microsoft.Extensions.Configuration;

namespace Core.Shared.Configuration;

/// <summary>
/// Provides helper functions for easier configuration managing.
/// Those functions automatically throw if the configuration is not found thus returning a non-nullable value.
/// </summary>
public static class Configuration
{
	public static T GetValueWithThrow<T>(this IConfiguration configuration, string path)
		=> configuration.GetValue<T>(path) ?? throw new ConfigurationErrorsException($"Missing {path}");

	public static T GetSectionWithThrow<T>(this IConfiguration configuration, string path)
		=> configuration.GetSection(path).Get<T>() ?? throw new ConfigurationErrorsException($"Missing {path}");

	public static string GetConnectionStringWithThrow(this IConfiguration configuration, string path)
		=> configuration.GetConnectionString(path) ?? throw new ConfigurationErrorsException($"Missing {path}");
}