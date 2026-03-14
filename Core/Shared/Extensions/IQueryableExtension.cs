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

		return source.ExecuteUpdate(BuildSetPropertyExpression(entity));
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

		return source.ExecuteUpdateAsync(BuildSetPropertyExpression(entity), cancellationToken);
	}

	/// <summary>
	/// Builds the SetProperty chain expression for EF Core ExecuteUpdate.
	/// Collects all updatable properties (including owned entity nested properties) and chains SetProperty calls.
	/// </summary>
	/// <typeparam name="T">The type of the entity to build the SetProperty chain expression for.</typeparam>
	/// <param name="entity">The entity to build the SetProperty chain expression for.</param>
	/// <returns>A lambda expression representing the SetProperty chain expression.</returns>
	private static Expression<Func<SetPropertyCalls<T>, SetPropertyCalls<T>>> BuildSetPropertyExpression<T>(T entity)
	{
		ParameterExpression entityParam = Expression.Parameter(typeof(T), "e");
		ParameterExpression setPropertyParam = Expression.Parameter(typeof(SetPropertyCalls<T>), "s");

		// Collect all property setters: direct scalar props + owned entity scalar props (flattened)
		List<(MemberExpression PropertyAccess, object? Value, Type ValueType)> setters =
			CollectPropertySetters(entity, entityParam);

		Expression? constructorExpressions = null;
		MethodInfo? setPropertyMethod = typeof(SetPropertyCalls<>)
			.MakeGenericType(typeof(T))
			.GetMethods()
			.FirstOrDefault(IsSetPropertyMethod)
			?? throw new InvalidOperationException("Method SetProperty not found");

		// Build chained SetProperty calls: s.SetProperty(e => e.Prop, e => value).SetProperty(...)
		foreach ((MemberExpression propertyAccess, object? value, Type valueType) in setters)
		{
			LambdaExpression propertyExpression = Expression.Lambda(propertyAccess, entityParam);
			ConstantExpression constant = Expression.Constant(value, valueType);
			LambdaExpression valueExpressionFunc = Expression.Lambda(constant, entityParam);

			MethodInfo setProperty = setPropertyMethod.MakeGenericMethod(valueType);

			constructorExpressions = Expression.Call(
				constructorExpressions ?? setPropertyParam,
				setProperty,
				propertyExpression,
				valueExpressionFunc
			);
		}

		return Expression.Lambda<Func<SetPropertyCalls<T>, SetPropertyCalls<T>>>(
			constructorExpressions ?? setPropertyParam,
			setPropertyParam);
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
	private static List<(MemberExpression PropertyAccess, object? Value, Type ValueType)> CollectPropertySetters<T>(
		T entity,
		ParameterExpression parameter)
	{
		List<(MemberExpression PropertyAccess, object? Value, Type ValueType)> setters = [];
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

					// Use owned value if present, otherwise default (handles null owned entity)
					object? value = (ownedValue is not null)
						? subProperty.GetValue(ownedValue)
						: GetDefaultValue(subProperty.PropertyType);

					setters.Add((nestedAccess, value, subProperty.PropertyType));
				}
			}
			else if (IsValidForSetProperty(property))
			{
				// Direct scalar property: e => e.PropertyName
				MemberExpression propertyAccess = Expression.Property(parameter, property);
				object? value = property.GetValue(entity);
				setters.Add((propertyAccess, value, propertyType));
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
	/// Identifies the SetProperty method on SetPropertyCalls&lt;T&gt; (takes 2 Func parameters).
	/// </summary>
	/// <param name="m">The method info to check.</param>
	/// <returns>True if the method is the SetProperty method, false otherwise.</returns>
	private static bool IsSetPropertyMethod(MethodInfo m)
	{
		if (m.Name != "SetProperty")
			return false;

		int paramCount = m.GetParameters().Count(p =>
			p.ParameterType.IsGenericType
				&& p.ParameterType.GetGenericTypeDefinition() == typeof(Func<,>));
		return paramCount == 2;
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
			&& (property.PropertyType == typeof(string) || property.PropertyType.IsValueType)
			&& !property.Name.Equals("ID", StringComparison.OrdinalIgnoreCase);

	/// <summary>
	/// Returns true if the property should be excluded from bulk update (navigation, owned object itself, ID, etc.).
	/// </summary>
	/// <param name="property">The property to check.</param>
	/// <returns>True if the property should be excluded from bulk update (navigation, owned object itself, ID, etc.), false otherwise.</returns>
	public static bool IsInValidProperty(PropertyInfo property)
		=> !IsValidForSetProperty(property)
			|| (property.PropertyType.IsClass && property.PropertyType != typeof(string));
}