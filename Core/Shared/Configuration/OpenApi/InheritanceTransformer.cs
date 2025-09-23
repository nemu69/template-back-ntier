// using Microsoft.AspNetCore.OpenApi;

// namespace Core.Shared.Configuration.OpenApi;

// public static class InheritanceTransformer
// {
// 	public static OpenApiOptions AddInheritanceTransformer(this OpenApiOptions options)
// 	{
// 		options.AddSchemaTransformer((schema, context, _) => {
// 			const string schemaId = "x-schema-id";

// 			if (schema.Annotations?.TryGetValue(schemaId, out object? referenceIdObject) == true
// 				&& referenceIdObject is string newSchemaId)
// 			{
// 				Type clrType = context.JsonTypeInfo.Type;
// 				newSchemaId = GetSchemaName(clrType);
// 				schema.Annotations[schemaId] = newSchemaId;
// 			}

// 			return Task.CompletedTask;
// 		});

// 		return options;
// 	}

// 	private static string GetSchemaName(this Type type)
// 	{
// 		if (type.IsGenericType)
// 		{
// 			// Process the generic arguments
// 			string[] typeNames = type.GetGenericArguments().Select(GetSchemaNameCore).ToArray();
// 			string args = string.Join("And", typeNames);

// 			// Get the name of the generic type without the arity (backtick and number)
// 			string typeName = type.Name;
// 			int index = typeName.IndexOf('`');
// 			if (index >= 0)
// 				typeName = typeName[..index];

// 			return $"{typeName}Of{args}";
// 		}

// 		return GetSchemaNameCore(type);
// 	}

// 	private static string GetSchemaNameCore(Type type)
// 	{
// 		string typeName = type.Name;

// 		foreach (string? suffix in (string[])(["Dto"]))
// 		{
// 			if (typeName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
// 			{
// 				// Ensure the type name is longer than the suffix
// 				if (typeName.Length > suffix.Length)
// 					return typeName[..^suffix.Length];

// 				return typeName; // Return original name if it's shorter than the suffix
// 			}
// 		}

// 		return type.Name;
// 	}
// }