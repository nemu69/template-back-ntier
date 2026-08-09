namespace Core.Shared.Endpoints.Kernel;

/// <summary>
/// Programmatic per-route output-cache durations for <see cref="BaseEntityEndpoint{T,TDTO,TService}"/>.
/// Null on a property disables caching for that route.
/// </summary>
public sealed class BaseEntityCacheOptions
{
	public static BaseEntityCacheOptions Default { get; } = new();

	public TimeSpan? GetAll { get; init; } = TimeSpan.FromMinutes(1);
	public TimeSpan? GetBy { get; init; } = TimeSpan.FromSeconds(30);
	public TimeSpan? GetAllWithDataProcess { get; init; } = TimeSpan.FromMinutes(1);
	public TimeSpan? GetWithPagination { get; init; } = TimeSpan.FromSeconds(30);
	public TimeSpan? CountWithPagination { get; init; } = TimeSpan.FromSeconds(30);
}