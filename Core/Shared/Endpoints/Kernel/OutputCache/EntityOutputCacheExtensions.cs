using Microsoft.AspNetCore.Builder;

namespace Core.Shared.Endpoints.Kernel.OutputCache;

internal static class EntityOutputCacheExtensions
{
	public static RouteHandlerBuilder CacheEntityGet(
		this RouteHandlerBuilder builder,
		TimeSpan? duration,
		string entityTag) => ((duration is not { } d) || (d <= TimeSpan.Zero))
		? builder
		: EntityReadOutputCachePolicy.Apply(builder, d, entityTag);

	public static RouteHandlerBuilder CacheEntityPut(
		this RouteHandlerBuilder builder,
		TimeSpan? duration,
		string entityTag)

	{
		return ((duration is not { } d) || (d <= TimeSpan.Zero))
			? builder
			: EntityReadOutputCachePolicy.Apply(
				builder,
				d,
				entityTag,
				allowPut: true,
				varyByBody: true);
	}
}