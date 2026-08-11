using System.Diagnostics;
using Carter;
using Core.Shared.Configuration;
using Core.Shared.Configuration.Logging;
using Core.Shared.Data;
using Core.Shared.Dictionaries;
using Core.Shared.Endpoints.Kernel.OutputCache;
using Core.Shared.Models.ApiResponses;
using Core.Shared.UnitOfWork;
using Core.Shared.UnitOfWork.Interfaces;
using Mapster;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scalar.AspNetCore;
using Serilog;

namespace Core.Shared.DependencyInjection;

public static class DependencyInjection
{
	/// <summary>
	/// Custom method for IServiceCollection to add our required services
	/// </summary>
	/// <param name="services"></param>
	/// <param name="configuration"></param>
	/// <returns></returns>
	public static IServiceCollection AddRequiredServices(this IServiceCollection services, IConfiguration configuration)
	{
		services.AddControllers().AddJsonOptions(
			opt => {
				opt.JsonSerializerOptions.PropertyNamingPolicy = ApiResponse.JsonOptions.PropertyNamingPolicy;
				opt.JsonSerializerOptions.TypeInfoResolver = ApiResponse.JsonOptions.TypeInfoResolver;
				opt.JsonSerializerOptions.ReferenceHandler = ApiResponse.JsonOptions.ReferenceHandler;
				opt.JsonSerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.Strict;
			});

		services.ConfigureHttpJsonOptions(
			opt => {
				opt.SerializerOptions.PropertyNamingPolicy = ApiResponse.JsonOptions.PropertyNamingPolicy;
				opt.SerializerOptions.TypeInfoResolver = ApiResponse.JsonOptions.TypeInfoResolver;
				opt.SerializerOptions.ReferenceHandler = ApiResponse.JsonOptions.ReferenceHandler;
				opt.SerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.Strict;
			});

		// Built-in OpenAPI document generation (.NET 10+). WithOpenApi() is obsolete (ASPDEPR002).
		// https://learn.microsoft.com/aspnet/core/fundamentals/openapi/aspnetcore-openapi
		services.AddOpenApi(options =>
			options.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0);

		services.AddDbContext<AppDbContext>(
			options => options.UseSqlServer(configuration.GetConnectionStringWithThrow("DefaultConnection")));

		// To fix: Unable to resolve service for type 'Microsoft.AspNetCore.Http.IHttpContextAccessor'
		services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

		services.AddScoped<IAppUOW, AppUOW>();

		services.AddCarter();

		// Adds services for using Problem Details format
		services.AddProblemDetails(options => {
			options.CustomizeProblemDetails = context => {
				context.ProblemDetails.Instance =
					$"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path.ToString()}";

				context.ProblemDetails.Extensions.TryAdd("requestId", context.HttpContext.TraceIdentifier);

				Activity? activity = context.HttpContext.Features.Get<IHttpActivityFeature>()?.Activity;
				context.ProblemDetails.Extensions.TryAdd("traceId", activity?.Id);
			};
		});

		services.AddOutputCache(options => {
			// Named policy (opt-in via CacheOutput / EntityReadOutputCachePolicy).
			// Not a base policy — otherwise every GET would be cached for 60s.
			options.AddPolicy(
				OutputCacheTags.All,
				policy => policy.Tag(OutputCacheTags.All),
				excludeDefaultPolicy: true);
		});

		string[] clientHost = configuration.GetSectionWithThrow<string[]>(ConfigDictionary.ClientHost);
		services.AddCors(options => {
			options.AddDefaultPolicy(corsPolicyBuilder => corsPolicyBuilder.WithOrigins(clientHost)
				.WithMethods("GET", "POST", "HEAD", "PUT", "DELETE", "OPTIONS")
				.AllowAnyHeader()
				.AllowCredentials());
		});

		return services;
	}

	/// <summary>
	/// Custom method for WebApplicationBuilder to add our required builders
	/// </summary>
	/// <param name="builder"></param>
	/// <returns></returns>
	public static WebApplicationBuilder AddRequiredBuilders(this WebApplicationBuilder builder)
	{
		//common configuration
		builder.Configuration
			.AddJsonFile(
				Path.GetFullPath($"../core/appsettings.common.{builder.Environment.EnvironmentName}.json"),
				optional: true,
				reloadOnChange: true)
			.AddJsonFile($"appsettings.common.{builder.Environment.EnvironmentName}.json", optional: true)
			.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

		// Use Serilog as logger
		builder.Logging.ClearProviders();
		builder.Host.UseSerilog(
			static (ctx, serviceProvider, loggerConfig) => {
				loggerConfig
					.ReadFrom
					.Configuration(ctx.Configuration)
					.MinimumLevel
					.ControlledBy(LogSwitchLevel.LevelSwitch)
					.ReadFrom
					.Services(serviceProvider)
					.Enrich
					.WithCustomEnrichers(ctx.Configuration);
			});

		builder.Services.AddRequiredServices(builder.Configuration);

		return builder;
	}

	/// <summary>
	/// Custom method for WebApplication to add our required apps
	/// </summary>
	/// <param name="app"></param>
	/// <returns></returns>
	public static WebApplication UseRequiredApps(this WebApplication app)
	{
		app.UseCors();

		app.UseHttpsRedirection();

		// Converts unhandled exceptions into Problem Details responses
		app.UseExceptionHandler();

		// Returns the Problem Details response for (empty) non-successful responses
		app.UseStatusCodePages();

		// Must run before MapCarter so entity output-cache policies apply
		app.UseOutputCache();

		app.MapCarter();

		if (app.Environment.IsDevelopment())
		{
			app.MapOpenApi();
			app.MapScalarApiReference();
			app.UseSwaggerUI(options =>
				options.SwaggerEndpoint("/openapi/v1.json", "v1"));
			app.ApplyMigration<AppDbContext>();
		}

		TypeAdapterConfig.GlobalSettings.Default.PreserveReference(true);
		Log.Information("Starting API Service");

		return app;
	}

	private static void ApplyMigration<TDbContext>(this WebApplication app)
		where TDbContext : DbContext
	{
		using IServiceScope serviceScope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateScope();
		TDbContext context = serviceScope.ServiceProvider.GetRequiredService<TDbContext>();

		if (context.Database.GetPendingMigrations().Any())
			context.Database.Migrate();
	}
}