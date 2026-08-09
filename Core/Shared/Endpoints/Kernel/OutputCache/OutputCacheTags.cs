namespace Core.Shared.Endpoints.Kernel.OutputCache;

/// <summary>
/// Shared output-cache tags. The <see cref="All"/> tag allows clearing the entire cache
/// via <c>IOutputCacheStore.EvictByTagAsync</c>.
/// See https://antondevtips.com/blog/aspnetcore-output-cache-how-to-speed-up-your-api-with-in-memory-cache-and-redis#clearing-the-entire-cache
/// </summary>
public static class OutputCacheTags
{
	public const string All = "all";
}