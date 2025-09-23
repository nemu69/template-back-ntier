using System.Linq.Expressions;
using System.Reflection;
using Core.Shared.Models.DB.Kernel.Interfaces;
using Core.Shared.Models.DTO.Kernel.Interfaces;

namespace Core.Shared.DataProcessings.Filtering;

public static class Filter
{
	/// <summary>
	/// Applies filtering to the given source queryable based on the provided data processing parameters.
	/// </summary>
	/// <typeparam name="T">The type of the entities in the source queryable.</typeparam>
	/// <typeparam name="TDTO">The type of the DTOs associated with the entities.</typeparam>
	/// <param name="source">The source queryable to apply filtering to.</param>
	/// <param name="dataProcessing">The data processing parameters containing the filter parameters.</param>
	/// <returns>The filtered queryable.</returns>
	/// <remarks>
	/// This method applies filtering to the source queryable based on the filter parameters specified in the data processing parameters.
	/// It uses the FilterToExpression method to convert each filter parameter into an expression that represents the filtering condition.
	/// The resulting expressions are combined using the AndAlso operator and then used to create a lambda expression representing the
	/// where clause. The source queryable is then filtered using the where clause and the filtered queryable is returned.
	/// </remarks>
	public static IQueryable<T> ApplyFiltering<T, TDTO>(this IQueryable<T> source, DataProcessing dataProcessing)
	where T : class, IBaseEntity<T, TDTO>
	where TDTO : class, IDTO<T, TDTO>
	{
		ParameterExpression param = Expression.Parameter(typeof(T));
		IEnumerable<FilterParam> filterParams = dataProcessing.FilterParams;

		if (!filterParams.Any())
			return source;

		List<Expression> filters = filterParams
			.Select(filterParam => FilterToExpression<T>(filterParam, param))
			.ToList();

		if (filters.Count == 0)
			return source;

		Expression combinedFilter = filters.Aggregate(Expression.AndAlso);
		Expression<Func<T, bool>> whereClause = Expression.Lambda<Func<T, bool>>(combinedFilter, param);

		return source.Where(whereClause);
	}

	/// <summary>
	/// Converts a filterParam into a compilable LINQ to Entities compatible expression so it can be translated to SQL.
	/// </summary>
	/// <param name="filterParam"></param>
	/// <param name="param"></param>
	/// <typeparam name="T"></typeparam>
	/// <returns></returns>
	/// <exception cref="ArgumentException"></exception>
	private static Expression FilterToExpression<T>(FilterParam filterParam, ParameterExpression param)
	{
		// Gets the property of the class from its column name.
		FilterOption filterOption = FilterOptionMap.Get(filterParam.FilterOptionName);
		if (filterOption == FilterOption.Nothing)
			return Expression.Constant(true);

		if (filterOption == FilterOption.Contains)
			return ContainsExpression<T>(filterParam, param);

		if (filterOption == FilterOption.IsType)
		{
			// Gets all possible types in the Assembly (running instance) and find the one with the same name.
			Type? type = Assembly.GetAssembly(typeof(T))?.GetTypes().ToList()
				.Find(t => t.Name == filterParam.FilterValue[0]);
			return (type is null) ? Expression.Constant(false) : Expression.TypeIs(param, type);
		}

		string[] names = filterParam.ColumnName.Split('.');

		// Apply cast type if specified
		Expression propertyAccess;
		if (!string.IsNullOrEmpty(filterParam.CastToType))
		{
			Type castType = GetTypeByName(filterParam.CastToType);
			propertyAccess = GetExpressionProperty(Expression.Convert(param, castType), names);
		}
		else
		{
			propertyAccess = GetExpressionProperty(param, names);
		}

		PropertyInfo filterColumn = GetColumnProperty<T>(names, filterParam.CastToType);

		List<Expression> expressions = filterParam.FilterValue
			.ConvertAll(value => {
				// Get non nullable type if property is nullable
				Type propertyType = Nullable.GetUnderlyingType(filterColumn.PropertyType) ?? filterColumn.PropertyType;
				IComparable refValue = ParseAsComparable(propertyType, value)
					?? throw new ArgumentException("Error happened during parsing of filterValue");
				return (Expression)GetExpressionBody(
					filterOption,
					propertyAccess,
					Expression.Constant(refValue, propertyAccess.Type));
			});

		return (expressions.Count == 0) ? Expression.Constant(true) : expressions.Aggregate(Expression.OrElse);
	}

	private static BinaryExpression GetExpressionBody(FilterOption filterOption, Expression left, Expression right)
	{
		return filterOption switch {
			FilterOption.IsGreaterThan => Expression.GreaterThan(left, right),
			FilterOption.IsGreaterThanOrEqualTo => Expression.GreaterThanOrEqual(left, right),
			FilterOption.IsLessThan => Expression.LessThan(left, right),
			FilterOption.IsLessThanOrEqualTo => Expression.LessThanOrEqual(left, right),
			FilterOption.IsEqualTo => Expression.Equal(left, right),
			FilterOption.IsNotEqualTo => Expression.NotEqual(left, right),
			_ => throw new ArgumentOutOfRangeException(nameof(filterOption), filterOption, null),
		};
	}

	/// <summary>
	/// Returns the PropertyInfo queried by its name & path to it. If given [ "Bar", "ID" ],
	/// it will return the PropertyInfo of ID if there's an object with this column in "Bar".
	/// eg:
	/// <code>
	/// public class Foo
	/// {
	///     public BarClass Bar { get; set; }
	///     public int Value { get; set; }
	/// }
	/// public class BarClass
	/// {
	///     public int ID { get; set; }
	/// }
	/// </code>
	/// ID column is accessed with [ "Bar", "ID" ] from T = Foo.
	/// Value column is accessed with [ "Value" ] from T = Foo.
	/// </summary>
	/// <param name="names">An array of strings for nested parameters</param>
	/// <param name="castToType">Type to cast to</param>
	/// <typeparam name="T">Type from which column is queried</typeparam>
	/// <returns>The property info of the (possibly nested) queried column</returns>
	/// <exception cref="InvalidDataException">Thrown if no PropertyInfo is found due to invalid name</exception>
	private static PropertyInfo GetColumnProperty<T>(string[] names, string? castToType = null)
	{
		PropertyInfo? propertyInfo = null;
		Type type = (castToType is null) ? typeof(T) : GetTypeByName(castToType);

		foreach (string name in names)
		{
			propertyInfo = type.GetProperty(
				name,
				BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public);
			if (propertyInfo is null)
				throw new InvalidDataException("FilterParam Column name is invalid.");

			type = propertyInfo.PropertyType;
		}

		return propertyInfo ?? throw new InvalidDataException("FilterParam Column name is invalid.");
	}

	private static Type GetTypeByName(string typeName)
	{
		return AppDomain.CurrentDomain.GetAssemblies()
			.SelectMany(c => c.GetTypes())
			.FirstOrDefault(c => c.Name == typeName && !c.AssemblyQualifiedName!.Contains("Migration"))
			?? throw new ArgumentException("Filter: Trying to cast to a type which does not exist.");
	}

	/// <summary>
	/// Similar to <see cref="GetColumnProperty{T}"/>,
	/// except it returns an Expression accessing this (potentially nested) property instead of its PropertyInfo
	/// </summary>
	/// <param name="param">Parameter expression on which to access property</param>
	/// <param name="names">An array of strings for nested parameters</param>
	/// <returns>An expression accessing this property from param</returns>
	private static Expression GetExpressionProperty(Expression param, string[] names)
	{
		Expression property = Expression.Property(param, names[0]);
		for (int i = 1; i < names.Length; ++i)
			property = Expression.Property(property, names[i]);

		return property;
	}

	/// <summary>
	/// Will parse a string into an IComparable object if its <paramref name="type"/> implements IParsable and IComparable.
	/// This function uses System.Reflection.
	/// </summary>
	/// <param name="type">Type of the unparsed value</param>
	/// <param name="value">Value to be parsed as comparable</param>
	/// <returns>The parsed value as an IComparable</returns>
	/// <exception cref="ArgumentException">Thrown if type is either not Comparable or not Parsable</exception>
	private static IComparable? ParseAsComparable(Type type, string value)
	{
		if (type == typeof(string))
			return value;

		if (type.IsEnum)
			return Enum.Parse(type, value) as IComparable;

		if (type.GetInterfaces().All(c => c != typeof(IComparable)))
			throw new ArgumentException($"Filter: {type} is not parsable as IComparable.");

		// Verifies if the type is parsable or not by using reflection.
		if (!type.GetInterfaces().Any(c => c.IsGenericType && c.GetGenericTypeDefinition() == typeof(IParsable<>)))
			throw new ArgumentException("Filter: Trying to parse a value which is not parsable.");

		// Then it gets the Parse method through reflection.
		// https://stackoverflow.com/questions/74501978/how-do-i-test-if-a-type-t-implements-iparsablet
		MethodInfo? parse = Array.Find(
			type.GetMethods(BindingFlags.Static | BindingFlags.Public),
			c =>
				c.Name == "Parse"
					&& c.GetParameters().Length == 1
					&& c.GetParameters()[0].ParameterType == typeof(string))
			?? throw new ArgumentException(
				"Filter: Trying to parse a value which is not parsable.");

		// And it finally invokes it.
		return parse.Invoke(null, [value]) as IComparable;
	}

	private static Expression ContainsExpression<T>(FilterParam filterParam, ParameterExpression param)
	{
		// Gets the property of the class from its column name.
		string[] names = filterParam.ColumnName.Split('.');
		PropertyInfo filterColumn = GetColumnProperty<T>(names);

		List<Expression> expressions = filterParam.FilterValue
			.ConvertAll(value => {
				Expression filterValueExpression = Expression.Constant(value, typeof(string));
				MethodInfo toStringMethod = Array.Find(
					filterColumn.PropertyType.GetMethods(),
					method => method.Name == "ToString")!;
				Expression propertyToStringExpression = Expression.Call(
					Expression.Property(param, filterColumn.Name),
					toStringMethod);
				MethodInfo containsMethod = Array.Find(typeof(string).GetMethods(), method => method.Name == "Contains")!;
				return (Expression)Expression.Call(propertyToStringExpression, containsMethod, filterValueExpression);
			});

		return (expressions.Count == 0) ? Expression.Constant(true) : expressions.Aggregate(Expression.OrElse);
	}
}