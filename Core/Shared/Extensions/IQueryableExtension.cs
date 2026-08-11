using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace Core.Shared.Extensions;

/// <summary>
///     Extensions for bulk entity updates using EF Core ExecuteUpdate.
///     Supports owned entities by flattening their scalar properties into SetProperty calls.
/// </summary>
public static class IQueryableExtensions
{
	/// <summary>
	///     Executes a bulk UPDATE on entities matching the source query, setting properties from the given entity.
	///     Works with owned entity types by updating each nested scalar property (e.g. e.ShippingAddress.Street).
	/// </summary>
	/// <typeparam name="T">The type of the entity to execute the bulk UPDATE on.</typeparam>
	/// <param name="source">The source queryable to execute the bulk UPDATE on.</param>
	/// <param name="entity">The entity to set properties from.</param>
	/// <returns>The number of entities updated.</returns>
	public static int ExecuteUpdateEntity<T>(
		this IQueryable<T> source,
		T entity)
	{
		ArgumentNullException.ThrowIfNull(entity);

		return source.ExecuteUpdate(setters => ApplyEntitySetters(setters, entity));
	}

	/// <summary>
	///     Executes a bulk UPDATE asynchronously on entities matching the source query, setting properties from the given entity.
	///     Works with owned entity types by updating each nested scalar property (e.g. e.ShippingAddress.Street).
	/// </summary>
	/// <typeparam name="T">The type of the entity to execute the bulk UPDATE on.</typeparam>
	/// <param name="source">The source queryable to execute the bulk UPDATE on.</param>
	/// <param name="entity">The entity to set properties from.</param>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>A task representing the asynchronous operation, containing the number of entities updated.</returns>
	public static Task<int> ExecuteUpdateEntityAsync<T>(
		this IQueryable<T> source,
		T entity,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(entity);

		return source.ExecuteUpdateAsync(setters => ApplyEntitySetters(setters, entity), cancellationToken);
	}

	/// <summary>
	/// Applies SetProperty calls for EF Core ExecuteUpdate via UpdateSettersBuilder.
	/// Collects all updatable properties (including owned entity nested properties) and calls SetProperty for each.
	/// </summary>
	/// <typeparam name="T">The type of the entity to apply SetProperty calls for.</typeparam>
	/// <param name="setters">The UpdateSettersBuilder used by ExecuteUpdate.</param>
	/// <param name="entity">The entity to set properties from.</param>
	private static void ApplyEntitySetters<T>(UpdateSettersBuilder<T> setters, T entity)
	{
		ParameterExpression entityParam = Expression.Parameter(typeof(T), "e");

		// Collect all property setters: direct scalar props + owned entity scalar props (flattened)
		List<(LambdaExpression PropertyExpression, object? Value, Type ValueType)> propertySetters =
			CollectPropertySetters(entity, entityParam);

		MethodInfo setPropertyMethod = typeof(UpdateSettersBuilder<T>)
			.GetMethods(BindingFlags.Public | BindingFlags.Instance)
			.FirstOrDefault(IsValueSetPropertyMethod)
			?? throw new InvalidOperationException("Method SetProperty not found");

		// Apply SetProperty calls: s.SetProperty(e => e.Prop, value)
		foreach ((LambdaExpression propertyExpression, object? value, Type valueType) in propertySetters)
		{
			MethodInfo setProperty = setPropertyMethod.MakeGenericMethod(valueType);
			setProperty.Invoke(setters, [propertyExpression, value]);
		}
	}

	/// <summary>
	/// Collects all property setters to apply during ExecuteUpdate.
	/// For owned entities: flattens them by adding each scalar sub-property (e.g. e.ShippingAddress.Street).
	/// For direct properties: adds scalar properties as-is.
	/// </summary>
	/// <typeparam name="T">The type of the entity to collect properties from.</typeparam>
	/// <param name="entity">The entity to collect properties from.</param>
	/// <param name="parameter">The parameter expression for the entity.</param>
	/// <returns>A list of property setters to apply during ExecuteUpdate.</returns>
	private static List<(LambdaExpression PropertyExpression, object? Value, Type ValueType)> CollectPropertySetters<T>(
		T entity,
		ParameterExpression parameter)
	{
		List<(LambdaExpression PropertyExpression, object? Value, Type ValueType)> setters = [];
		Type entityType = typeof(T);

		foreach (PropertyInfo property in entityType.GetProperties())
		{
			if (!property.CanRead || !property.CanWrite || property.Name.Equals("ID", StringComparison.OrdinalIgnoreCase))
				continue;

			Type propertyType = property.PropertyType;

			// Owned entity: flatten and add each scalar sub-property (EF Core supports e.Owned.Street in SetProperty)
			if (IsOwnedEntityType(propertyType))
			{
				object? ownedValue = property.GetValue(entity);
				foreach (PropertyInfo subProperty in propertyType.GetProperties())
				{
					if (!IsValidForSetProperty(subProperty))
						continue;

					// Build e => e.Owned.SubProperty
					MemberExpression ownedAccess = Expression.Property(parameter, property);
					MemberExpression nestedAccess = Expression.Property(ownedAccess, subProperty);
					LambdaExpression propertyExpression = Expression.Lambda(
						typeof(Func<,>).MakeGenericType(typeof(T), subProperty.PropertyType),
						nestedAccess,
						parameter);

					// Use owned value if present, otherwise default (handles null owned entity)
					object? value = (ownedValue is not null)
						? subProperty.GetValue(ownedValue)
						: GetDefaultValue(subProperty.PropertyType);

					setters.Add((propertyExpression, value, subProperty.PropertyType));
				}
			}
			else if (IsValidForSetProperty(property))
			{
				// Direct scalar property: e => e.PropertyName
				MemberExpression propertyAccess = Expression.Property(parameter, property);
				LambdaExpression propertyExpression = Expression.Lambda(
					typeof(Func<,>).MakeGenericType(typeof(T), propertyType),
					propertyAccess,
					parameter);
				object? value = property.GetValue(entity);
				setters.Add((propertyExpression, value, propertyType));
			}
		}

		return setters;
	}

	/// <summary>
	/// Returns the default value for a type (e.g. empty string for string, 0 for int).
	/// Used when owned entity is null to set sub-properties to defaults.
	/// </summary>
	/// <param name="type">The type to get the default value for.</param>
	/// <returns>The default value for the type.</returns>
	private static object? GetDefaultValue(Type type)
		=> (type.IsValueType && (Nullable.GetUnderlyingType(type) is null))
			? Activator.CreateInstance(type) : null;

	/// <summary>
	/// Identifies the SetProperty method on UpdateSettersBuilder&lt;T&gt; (property expression + value).
	/// </summary>
	/// <param name="method">The method info to check.</param>
	/// <returns>True if the method is the SetProperty method, false otherwise.</returns>
	private static bool IsValueSetPropertyMethod(MethodInfo method)
	{
		if ((method.Name != "SetProperty") || !method.IsGenericMethodDefinition)
			return false;

		ParameterInfo[] parameters = method.GetParameters();
		if (parameters.Length != 2)
			return false;

		// Second parameter is TProperty (not Expression<Func<...>>)
		return !parameters[1].ParameterType.IsGenericType;
	}

	/// <summary>
	/// Returns true if the type is marked with [Owned] (EF Core owned entity type).
	/// </summary>
	/// <param name="type">The type to check.</param>
	/// <returns>True if the type is marked with [Owned] (EF Core owned entity type), false otherwise.</returns>
	private static bool IsOwnedEntityType(Type type)
		=> type.GetCustomAttributes(false).Any(a => a.GetType().Name == "OwnedAttribute");

	/// <summary>
	/// Returns true if the property can be used with ExecuteUpdate SetProperty (scalar: string or value type).
	/// </summary>
	/// <param name="property">The property to check.</param>
	/// <returns>True if the property can be used with ExecuteUpdate SetProperty (scalar: string or value type), false otherwise.</returns>
	private static bool IsValidForSetProperty(PropertyInfo property)
		=> property.CanRead
			&& property.CanWrite
			&& ((property.PropertyType == typeof(string)) || property.PropertyType.IsValueType)
			&& !property.Name.Equals("ID", StringComparison.OrdinalIgnoreCase);

	/// <summary>
	/// Returns true if the property should be excluded from bulk update (navigation, owned object itself, ID, etc.).
	/// </summary>
	/// <param name="property">The property to check.</param>
	/// <returns>True if the property should be excluded from bulk update (navigation, owned object itself, ID, etc.), false otherwise.</returns>
	public static bool IsInValidProperty(PropertyInfo property)
		=> !IsValidForSetProperty(property)
			|| (property.PropertyType.IsClass && (property.PropertyType != typeof(string)));
}