// using Microsoft.OpenApi.Models;
// using Microsoft.AspNetCore.OpenApi;

// namespace Core.Shared.Configuration.OpenApi;

// public static class ProblemResponseTransformer
// {
// 	// An extension method to add the document and operation transformers that together will add
// 	// a 4XX response to every operation in the OpenAPI document.
// 	public static OpenApiOptions AddProblemResponseTransformer(this OpenApiOptions options)
// 	{
// 		options.AddDocumentTransformer((document, _, __) => {
// 			document.Components ??= new();
// 			document.Components.Responses ??= new Dictionary<string, OpenApiResponse>();
// 			document.Components.Responses["Problem"] = new() {
// 				Description = "A problem occurred",
// 				Content = new Dictionary<string, OpenApiMediaType>() {
// 					["application/problem+json"] = new() {
// 						Schema = new() {
// 							Reference = new() {
// 								Type = ReferenceType.Schema,
// 								Id = "Problem",
// 							},
// 						},
// 					},
// 				},
// 			};
// 			document.Components.Schemas ??= new Dictionary<string, OpenApiSchema>();
// 			document.Components.Schemas["Problem"] = new() {
// 				Description = "A problem occurred",
// 				Properties = new Dictionary<string, OpenApiSchema> {
// 					["type"] = new() {
// 						Type = "string",
// 						Nullable = false,
// 					},
// 					["title"] = new() {
// 						Type = "string",
// 						Nullable = false,
// 					},
// 					["status"] = new() {
// 						Type = "integer",
// 						Nullable = false,
// 					},
// 					["detail"] = new() {
// 						Type = "string",
// 						Nullable = false,
// 					},
// 					["instance"] = new() {
// 						Type = "string",
// 						Nullable = false,
// 					},
// 					["traceId"] = new() {
// 						Type = "string",
// 						Nullable = false,
// 					},
// 					["requestId"] = new() {
// 						Type = "string",
// 						Nullable = false,
// 					},
// 				},
// 			};
// 			return Task.CompletedTask;
// 		})
// 			.AddOperationTransformer((operation, _, __) => {
// 				operation.Responses ??= [];
// 				OpenApiResponse response = new() {
// 					Reference = new() {
// 						Type = ReferenceType.Response,
// 						Id = "Problem",
// 					},
// 				};
// 				operation.Responses["4XX"] = response;
// 				// operation.Responses["400"] = response;
// 				// operation.Responses["401"] = response;
// 				// operation.Responses["404"] = response;
// 				// operation.Responses["408"] = response;
// 				// operation.Responses["409"] = response;
// 				operation.Responses["500"] = response;

// 				return Task.CompletedTask;
// 			});
// 		return options;
// 	}
// }