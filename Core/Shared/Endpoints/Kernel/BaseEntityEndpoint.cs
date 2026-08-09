using Core.Shared.DataProcessings;
using Core.Shared.Endpoints.Kernel.Dictionaries;
using Core.Shared.Endpoints.Kernel.OutputCache;
using Core.Shared.Models.DB.Kernel.Interfaces;
using Core.Shared.Models.DTO.Kernel.Interfaces;
using Core.Shared.Paginations;
using Core.Shared.Services.Kernel.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.Routing;

namespace Core.Shared.Endpoints.Kernel;

/// <summary>
/// Allows for an easy implementation of a generic CRUD.
/// </summary>
/// <typeparam name="T"></typeparam>
/// <typeparam name="TDTO"></typeparam>
/// <typeparam name="TService"></typeparam>
public class BaseEntityEndpoint<T, TDTO, TService> : BaseEndpoint
	where T : class, IBaseEntity<T, TDTO>
	where TDTO : class, IDTO<T, TDTO>
	where TService : IBaseEntityService<T, TDTO>
{
	private bool _isLogged;
	private bool _useCache;
	private string _entityCacheTag = string.Empty;

	/// <summary>
	/// Calling this function creates the needed CRUD generic functions depending on which ones are asked for.
	/// </summary>
	/// <param name="group"></param>
	/// <param name="flags">
	/// <see cref="BaseEndpointFlags"/>specifying which endpoints are needed among Create, Read, Update and Delete.
	/// Allows to also disable logging
	/// </param>
	/// <param name="cache">
	/// Optional per-route output-cache durations. When provided, read endpoints are cached and
	/// Create/Update/Delete evict entries tagged with the entity type name.
	/// Also maps <c>POST cache/clear</c> on the route group.
	/// </param>
	/// <returns></returns>
	protected RouteGroupBuilder MapBaseEndpoints(
		RouteGroupBuilder group,
		BaseEndpointFlags flags,
		BaseEntityCacheOptions? cache = null)
	{
		string dtoName = typeof(TDTO).Name;
		string tName = typeof(T).Name;
		_isLogged = flags.HasFlag(BaseEndpointFlags.ToLogs);
		_useCache = cache is not null;
		_entityCacheTag = tName;

		if (_useCache)
			MapCacheClearEndpoint(group);

		group = group.MapGroup(tName);

		if (flags.HasFlag(BaseEndpointFlags.Create))
			group.MapPost(string.Empty, Add).WithSummary($"Add the {dtoName} in the body to the database").WithOpenApi();

		if (flags.HasFlag(BaseEndpointFlags.Read))
		{
			group.MapGet(string.Empty, GetAll)
				.WithName($"{nameof(GetAll)}{tName}s")
				.WithSummary($"Get all {tName}s")
				.WithOpenApi()
				.CacheEntityGet(cache?.GetAll, _entityCacheTag);

			group.MapPut(string.Empty, GetAllWithDataProcess)
				.WithName($"{nameof(GetAllWithDataProcess)}{tName}s")
				.WithSummary($"Get all {tName}s with filters, sorting and text search with includes required")
				.WithOpenApi()
				.CacheEntityPut(cache?.GetAllWithDataProcess, _entityCacheTag);

			group.MapPut("by", GetBy)
				.WithName($"{nameof(GetBy)}{tName}")
				.WithSummary($"Get first {tName} with filters, sorting and text search with includes required")
				.WithOpenApi()
				.CacheEntityPut(cache?.GetBy, _entityCacheTag);

			group.MapPut("pagination", CountWithPagination)
				.WithName($"{nameof(CountWithPagination)}{tName}s")
				.WithSummary($"Get the number of {tName}s available in the filter and search with includes required")
				.WithOpenApi()
				.CacheEntityPut(cache?.CountWithPagination, _entityCacheTag);

			group.MapPut("pagination/{nbItems}", GetWithPagination)
				.WithName($"{nameof(GetWithPagination)}{tName}s")
				.WithSummary($"Get {tName}s by paging, sorting, text search and filtering with includes")
				.WithOpenApi()
				.CacheEntityPut(cache?.GetWithPagination, _entityCacheTag);
		}

		if (flags.HasFlag(BaseEndpointFlags.Update))
		{
			group.MapPut("update", Update)
				.WithName($"{nameof(Update)}{tName}")
				.WithSummary($"Update the {tName} in the body if it already exist")
				.Accepts<TDTO>("application/json")
				.WithOpenApi();
		}

		if (!flags.HasFlag(BaseEndpointFlags.Delete))
			return group;

		group.MapDelete("{id}", Remove)
			.WithName($"{nameof(Remove)}{tName}")
			.WithSummary($"Remove the {tName} by its ID")
			.WithOpenApi();

		return group;
	}

	/// <summary>
	/// Maps <c>POST cache/clear</c> on the current route group to evict every entry tagged with
	/// <see cref="OutputCacheTags.All"/>.
	/// </summary>
	/// <param name="group"></param>
	protected void MapCacheClearEndpoint(RouteGroupBuilder group)
	{
		group.MapPost("cache/clear", ClearEntireCache)
			.WithName($"ClearEntireOutputCache{_entityCacheTag}")
			.WithSummary("Clear the entire output cache")
			.WithOpenApi();
	}

	private static async Task<NoContent> ClearEntireCache(
		IOutputCacheStore cacheStore,
		CancellationToken cancellationToken)
	{
		await cacheStore.EvictByTagAsync(OutputCacheTags.All, cancellationToken);
		return TypedResults.NoContent();
	}

	private async Task EvictEntityCacheAsync(IOutputCacheStore cacheStore, CancellationToken cancellationToken)
	{
		if (_useCache)
			await cacheStore.EvictByTagAsync(_entityCacheTag, cancellationToken);
	}

	#region Create

	private async Task<Results<Ok<TDTO>, ProblemHttpResult>> Add(
		TService service,
		IOutputCacheStore cacheStore,
		HttpContext httpContext,
		TDTO dto,
		CancellationToken cancellationToken)
	{
		Results<Ok<TDTO>, ProblemHttpResult> result =
			await GenericEndpoint(() => service.Add(dto.ToModel()), httpContext, _isLogged);
		if (result.Result is Ok<TDTO>)
			await EvictEntityCacheAsync(cacheStore, cancellationToken);

		return result;
	}

	#endregion Create

	#region Read

	private Task<Results<Ok<List<TDTO>>, ProblemHttpResult>> GetAll(
		TService service,
		HttpContext httpContext)
			=> GenericEndpoint(() => service.GetAll(withTracking: false), httpContext, _isLogged);

	private Task<Results<Ok<List<TDTO>>, ProblemHttpResult>> GetAllWithDataProcess(
		TService service,
		HttpContext httpContext,
		[FromBody] DataProcessing dataProcessing)
			=> GenericEndpoint(() => service.GetAllWithDataProcess(dataProcessing), httpContext, _isLogged);

	private Task<Results<Ok<TDTO>, ProblemHttpResult>> GetBy(
		TService service,
		HttpContext httpContext,
		[FromBody] DataProcessing dataProcessing)
			=> GenericEndpoint(() => service.GetByWithDataProcess(dataProcessing), httpContext, _isLogged);

	private Task<Results<Ok<List<TDTO>>, ProblemHttpResult>> GetWithPagination(
		TService service,
		HttpContext httpContext,
		[FromRoute] int nbItems,
		[FromBody] PaginationParam pagination)
			=> GenericEndpoint(() => service.GetWithPagination(pagination, nbItems), httpContext, _isLogged);

	private Task<Results<Ok<int>, ProblemHttpResult>> CountWithPagination(
		TService service,
		HttpContext httpContext,
		[FromBody] PaginationParam pagination)
			=> GenericEndpoint(() => service.CountWithPagination(pagination), httpContext, _isLogged);

	#endregion Read

	#region Update

	private async Task<Results<Ok<TDTO>, ProblemHttpResult>> Update(
		TService service,
		IOutputCacheStore cacheStore,
		HttpContext httpContext,
		TDTO dto,
		CancellationToken cancellationToken)
	{
		Results<Ok<TDTO>, ProblemHttpResult> result =
			await GenericEndpoint(() => service.Update(dto.ToModel()), httpContext, _isLogged);
		if (result.Result is Ok<TDTO>)
			await EvictEntityCacheAsync(cacheStore, cancellationToken);

		return result;
	}

	#endregion Update

	#region Delete

	private async Task<Results<Ok<bool>, ProblemHttpResult>> Remove(
		[FromServices] TService service,
		IOutputCacheStore cacheStore,
		HttpContext httpContext,
		[FromRoute] int id,
		CancellationToken cancellationToken)
	{
		Results<Ok<bool>, ProblemHttpResult> result =
			await GenericEndpointEmptyResponse(() => service.Remove(id), httpContext, _isLogged);
		if (result.Result is Ok<bool>)
			await EvictEntityCacheAsync(cacheStore, cancellationToken);

		return result;
	}

	#endregion Delete
}