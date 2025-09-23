using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace Core.Shared.Extensions;

public static class IQueryableExtensions
{
	public static int ExecuteUpdateEntity<T>(
		this IQueryable<T> source,
		T entity)
	{
		ArgumentNullException.ThrowIfNull(entity);

		return source.ExecuteUpdate(GenerateUpdateExpression(entity).ConvertUpdateExpression());
	}

	public static Task<int> ExecuteUpdateEntityAsync<T>(
		this IQueryable<T> source,
		T entity,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(entity);

		return source.ExecuteUpdateAsync(GenerateUpdateExpression(entity).ConvertUpdateExpression(), cancellationToken);
	}

	private static Expression<Func<T, T>> GenerateUpdateExpression<T>(T entity)
	{
		// Get the parameter for the expression (e.g., `e => ...`)
		ParameterExpression parameter = Expression.Parameter(typeof(T), "e");

		// List of member bindings for each property
		List<MemberBinding> memberBindings = [];

		// Iterate over each property in the entity
		foreach (PropertyInfo property in typeof(T).GetProperties())
		{
			if (IsInValidProperty(property))
				continue;

			// Access the property (e.g., `e.PropertyName`)
			MemberExpression propertyAccess = Expression.Property(parameter, property);

			// Get the value of the property from the entity
			object? propertyValue = property.GetValue(entity);

			// Create the expression for the value (e.g., `entity.PropertyName`)
			ConstantExpression constant = Expression.Constant(propertyValue, property.PropertyType);

			// Bind the property to the new value
			MemberAssignment bind = Expression.Bind(property, constant);
			memberBindings.Add(bind);
		}

		// Create the member initialization (e.g., `new T { Property1 = value1, ... }`)
		MemberInitExpression body = Expression.MemberInit(Expression.New(typeof(T)), memberBindings);

		// Create the final expression (e.g., `e => new T { Property1 = value1, ... }`)
		return Expression.Lambda<Func<T, T>>(body, parameter);
	}

	public static bool IsInValidProperty(PropertyInfo property)
	{
		return !property.CanRead
			|| !property.CanWrite
			|| !property.PropertyType.IsValueType
			|| property.Name.Equals("ID", StringComparison.OrdinalIgnoreCase);
	}

	private static Expression<Func<
		SetPropertyCalls<T>,
		SetPropertyCalls<T>
		>> ConvertUpdateExpression<T>(
			this Expression<Func<T, T>> expression)
	{
		const string parameterName = "a";
		ParameterExpression param = Expression.Parameter(typeof(SetPropertyCalls<T>), parameterName);
		Expression? constructorExpressions = null;
		if (expression.Body is MemberInitExpression memberInitExpression)
		{
			foreach (MemberBinding item in memberInitExpression.Bindings)
			{
				if (item is MemberAssignment assignment)
				{
					PropertyInfo propertyInfo = (PropertyInfo)assignment.Member;

					string propertyName = assignment.Member.Name;

					Type properrtyType = propertyInfo.PropertyType;

					ParameterExpression parameter = Expression.Parameter(typeof(T), parameterName);
					MemberExpression propertyAccess = Expression.Property(parameter, propertyName);
					LambdaExpression propertyExpression = Expression.Lambda(propertyAccess, parameter);

					Expression valueExpression = assignment.Expression;

					ParameterExpression valueParameter = Expression.Parameter(typeof(T), parameterName);
					LambdaExpression valueExpressionFunc = Expression.Lambda(valueExpression, valueParameter);

					MethodInfo? setPropertyMethod = typeof(SetPropertyCalls<>)
						.MakeGenericType(typeof(T))
						.GetMethods()
						.FirstOrDefault(m => m.Name == "SetProperty"
							&& m.GetParameters()
								.Count(p =>
									p.ParameterType.IsGenericType
										&& p.ParameterType.GetGenericTypeDefinition() == typeof(Func<,>)) == 2)
						?? throw new InvalidOperationException("Method SetProperty not found");

					setPropertyMethod = setPropertyMethod.MakeGenericMethod(properrtyType);

					constructorExpressions = Expression.Call(
						constructorExpressions ?? param,
						setPropertyMethod,
						propertyExpression,
						valueExpressionFunc
					);
				}
			}
		}

		return Expression.Lambda<Func<SetPropertyCalls<T>, SetPropertyCalls<T>>>(
			constructorExpressions ?? param,
			param);
	}
}