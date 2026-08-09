using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;

using Microsoft.AspNetCore.OutputCaching;

namespace Core.Shared.Endpoints.Kernel.OutputCache;

/// <summary>
/// Output-cache policy for entity reads: expiration, entity tag, optional body vary, optional PUT.
/// </summary>
internal sealed class EntityReadOutputCachePolicy : IOutputCachePolicy
{
	private readonly TimeSpan _duration;
	private readonly string _entityTag;
	private readonly bool _allowPut;
	private readonly bool _varyByBody;

	public EntityReadOutputCachePolicy(
		TimeSpan duration,
		string entityTag,
		bool allowPut = false,
		bool varyByBody = false)
	{
		_duration = duration;
		_entityTag = entityTag;
		_allowPut = allowPut;
		_varyByBody = varyByBody;
	}

	async ValueTask IOutputCachePolicy.CacheRequestAsync(OutputCacheContext context, CancellationToken cancellation)
	{
		HttpRequest request = context.HttpContext.Request;
		bool isGetOrHead = HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method);
		bool isPut = HttpMethods.IsPut(request.Method);

		if (!isGetOrHead && !(_allowPut && isPut))
		{
			context.EnableOutputCaching = false;
			return;
		}

		context.EnableOutputCaching = true;
		context.AllowCacheLookup = true;
		context.AllowCacheStorage = true;
		context.AllowLocking = true;
		context.ResponseExpirationTimeSpan = _duration;
		context.Tags.Add(_entityTag);
		context.Tags.Add(OutputCacheTags.All);

		if (_allowPut)
			context.CacheVaryByRules.RouteValueNames = "nbItems";

		if (!_varyByBody)
			return;

		request.EnableBuffering();
		if (request.Body.CanSeek)
			request.Body.Position = 0;

		await using MemoryStream copy = new();
		await request.Body.CopyToAsync(copy, cancellation);

		if (request.Body.CanSeek)
			request.Body.Position = 0;

		byte[] hash = SHA256.HashData(copy.ToArray());
		context.CacheVaryByRules.VaryByValues["body"] = Convert.ToHexString(hash);
	}

	ValueTask IOutputCachePolicy.ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellation)
		=> ValueTask.CompletedTask;

	ValueTask IOutputCachePolicy.ServeResponseAsync(OutputCacheContext context, CancellationToken cancellation)
	{
		if (context.HttpContext.Response.StatusCode == StatusCodes.Status200OK)
			return ValueTask.CompletedTask;

		context.AllowCacheStorage = false;
		return ValueTask.CompletedTask;
	}
}