using Core.Shared.Models.ApiResponses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Core.Shared.Endpoints.Kernel;

/// <summary>
/// Provides a set of generic endpoints. These one encapsulate a function in a try catch block which return a correct
/// <see cref="ApiResponse"/> in case of failure or success.
/// </summary>
public class BaseEndpoint
{
	protected static async Task<Results<Ok<TReturn>, ProblemHttpResult>> GenericEndpoint<TReturn>(
		Func<Task<TReturn>> func,
		HttpContext httpContext,
		bool isLogged = false)
	{
		TReturn ans;
		try
		{
			ans = await func.Invoke();
		}
		catch (Exception e)
		{
			return ApiResponse.ErrorResult(httpContext, e);
		}

		return isLogged
			? ApiResponse.SuccessResult(ans, httpContext)
			: ApiResponse.SuccessResult(ans);
	}

	protected static async Task<Results<Ok<bool>, ProblemHttpResult>> GenericEndpointEmptyResponse(
		Func<Task> func,
		HttpContext httpContext,
		bool isLogged = false)
	{
		try
		{
			await func.Invoke();
		}
		catch (Exception e)
		{
			return ApiResponse.ErrorResult(httpContext, e);
		}

		return isLogged ? ApiResponse.Success(httpContext) : ApiResponse.Success();
	}
}