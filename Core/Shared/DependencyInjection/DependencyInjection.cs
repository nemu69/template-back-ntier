using Carter;
using Core.Shared.Configuration.Logging;
using Core.Shared.Configuration;
using Core.Shared.Dictionaries;
using Core.Shared.UnitOfWork;
using Core.Shared.UnitOfWork.Interfaces;
using Mapster;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Core.Shared.Models.ApiResponses;
using System.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Core.Shared.Data;

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
			});

		services.ConfigureHttpJsonOptions(
			opt => {
				opt.SerializerOptions.PropertyNamingPolicy = ApiResponse.JsonOptions.PropertyNamingPolicy;
				opt.SerializerOptions.TypeInfoResolver = ApiResponse.JsonOptions.TypeInfoResolver;
				opt.SerializerOptions.ReferenceHandler = ApiResponse.JsonOptions.ReferenceHandler;
			});

		// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
		services.AddSwaggerGen();

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

		services.AddOutputCache();

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

		app.MapCarter();

		if (app.Environment.IsDevelopment())
		{
			app.UseSwagger();
			app.UseSwaggerUI();
			app.ApplyMigration<AppDbContext>();
		}

		// Converts unhandled exceptions into Problem Details responses
		app.UseExceptionHandler();

		// Returns the Problem Details response for (empty) non-successful responses
		app.UseStatusCodePages();

		app.UseOutputCache();

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