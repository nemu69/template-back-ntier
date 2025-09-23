namespace Core.Shared.Models.ApiResponses;

internal class ApiResponseW<TResponse>
{
	public TResponse? Result { get; set; }
}