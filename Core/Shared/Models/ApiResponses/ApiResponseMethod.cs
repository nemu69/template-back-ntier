using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Core.Shared.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Core.Shared.Models.ApiResponses;

public static class ApiResponse
{
	/// <summary>
	/// Default JsonOptions which should be used when sending data to the front or for back to back communications.
	/// </summary>
	public static readonly JsonSerializerOptions JsonOptions = new() {
		PropertyNamingPolicy = null,
		TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { AddNestedDerivedTypes } },
		ReferenceHandler = ReferenceHandler.IgnoreCycles,
	};

	public static Ok<bool> Success() => new ApiResponseWrapper<bool>(true).SuccessResult();

	public static Ok<bool> Success(HttpContext httpContext)
	{
		CreateLog(httpContext);
		return Success();
	}

	public static Ok<TResponse> SuccessResult<TResponse>(TResponse result)
		=> new ApiResponseWrapper<TResponse>(result).SuccessResult();

	public static Ok<TResponse> SuccessResult<TResponse>(TResponse result, HttpContext httpContext)
	{
		CreateLog(httpContext);
		return SuccessResult(result);
	}

	public static ProblemHttpResult ErrorResult(HttpContext httpContext, Exception e)
		=> new ApiResponseWrapper<bool>(false).ErrorResult(httpContext, e);

	private static void CreateLog(HttpContext httpContext) => new ApiResponseWrapper<bool>().CreateLog(httpContext);

	/// <summary>
	/// This function allows for the class decorator <see cref="JsonDerivedType"/> to only specify direct children.
	/// The function then recursively adds the derived type of its children to the possible derived types.
	/// It allows for less cumbersome class decorators in DTO by needing to only specify direct children.
	/// </summary>
	/// <param name="jsonTypeInfo"></param>
	private static void AddNestedDerivedTypes(JsonTypeInfo jsonTypeInfo)
	{
		if (jsonTypeInfo.PolymorphismOptions is null)
			return;

		List<Type> derivedTypes = jsonTypeInfo.PolymorphismOptions.DerivedTypes
			.Where(static t => Attribute.IsDefined(t.DerivedType, typeof(JsonDerivedTypeAttribute)))
			.Select(static t => t.DerivedType)
			.ToList();
		HashSet<Type> hashset = new(derivedTypes);
		Queue<Type> queue = new(derivedTypes);
		while (queue.TryDequeue(out Type? derived))
		{
			if (!hashset.Contains(derived))
			{
				jsonTypeInfo.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(derived));
				hashset.Add(derived);
			}

			foreach (JsonDerivedTypeAttribute jsonDerivedTypeAttribute in derived
				.GetCustomAttributes<JsonDerivedTypeAttribute>())
			{
				queue.Enqueue(jsonDerivedTypeAttribute.DerivedType);
			}
		}
	}

	private class ApiResponseWrapper<TResponse>
	{
		public TResponse? Result { get; set; }

		internal ApiResponseWrapper()
		{
		}

		internal ApiResponseWrapper(TResponse? result)
		{
			Result = result;
		}

		internal Ok<TResponse> SuccessResult() => TypedResults.Ok(Result);

		internal Ok<TResponse> SuccessResult(HttpContext httpContext)
		{
			CreateLog(httpContext);
			return TypedResults.Ok(Result);
		}

		internal ProblemHttpResult ErrorResult(HttpContext httpContext, Exception e)
		{
			int errorStatusCode = e switch {
				ArgumentException => 400,
				UnauthorizedAccessException => 401,
				EntityNotFoundException => 404,
				TimeoutException => 408,
				InvalidOperationException => 409,
				NotImplementedException => 501,
				_ => 500
			};

			string title = e switch {
				ArgumentException => "Malformed request",
				UnauthorizedAccessException => "Unauthorized action",
				EntityNotFoundException => "Entity not found",
				TimeoutException => "Request timeout",
				InvalidOperationException => "Request caused a conflict",
				NotImplementedException => "Not implemented request",
				_ => "Internal Server Error"
			};

			CreateLog(httpContext, errorStatusCode);

			ProblemDetails problemDetails = new() {
				Status = errorStatusCode,
				Title = title,
				Detail = e.Message,
				Type = e.GetType().Name,
			};

			return TypedResults.Problem(problemDetails);
		}

		internal void CreateLog(HttpContext httpContext, int code = 200)
		{
			try
			{
				string result = JsonSerializer.Serialize(Result);
				result = result[..((result.Length > 50) ? 50 : result.Length)];

				if (code == 200)
				{
					Serilog.Log.ForContext("User", httpContext.User.Identity?.Name)
						.Information("[{code}]: {response}", code, result);
				}
				else if (code == 404)
				{
					Serilog.Log.ForContext("User", httpContext.User.Identity?.Name)
						.Warning("[{code}]: {response}", code, result);
				}
				else
				{
					Serilog.Log.ForContext("User", httpContext.User.Identity?.Name)
						.Error("[{code}]: {response}", code, result);
				}
			}
			catch
			{
				Serilog.Log.ForContext("User", httpContext.User.Identity?.Name)
					.Error("[{code}]: Error during response log.", code);
			}
		}
	}
}