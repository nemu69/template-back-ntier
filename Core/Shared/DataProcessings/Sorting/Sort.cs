using System.Linq.Expressions;
using Core.Shared.DataProcessings.Filtering;
using Core.Shared.Models.DB.Kernel.Interfaces;
using Core.Shared.Models.DTO.Kernel.Interfaces;

namespace Core.Shared.DataProcessings.Sorting;

public static class Sort
{
	/// <summary>
	/// Sorts an <see cref="IQueryable{T}"/> source from its pagination.
	/// The last value from the <see cref="SortParam"/> is treated in the <see cref="Filter"/> part.
	/// The sort can either be ascending or descending.
	/// </summary>
	/// <param name="source">Query to sort</param>
	/// <param name="dataProcessing">Pagination parameters used to sort</param>
	/// <typeparam name="T">BaseEntity from which rows will be sorted</typeparam>
	/// <typeparam name="TDTO"></typeparam>
	/// <returns>An <see cref="IOrderedQueryable{T}"/></returns>
	public static IOrderedQueryable<T> ApplySorting<T, TDTO>(this IQueryable<T> source, DataProcessing dataProcessing)
		where T : class, IBaseEntity<T, TDTO>
		where TDTO : class, IDTO<T, TDTO>
	{
		SortParam sortParam = dataProcessing.SortParam;
		if (sortParam.ColumnName.Length == 0 || sortParam.SortOptionName.Length == 0)
			return source.OrderByDescending(t => t.ID);

		SortOption sortOption = SortOptionMap.Get(sortParam.SortOptionName);
		ParameterExpression param = Expression.Parameter(typeof(T));
		return sortOption switch {
			SortOption.None => source.OrderByDescending(t => t.ID),
			SortOption.Ascending => source.OrderBy(GetOrderByExpression<T>(sortParam, param)),
			SortOption.Descending => source.OrderByDescending(GetOrderByExpression<T>(sortParam, param)),
			_ => throw new ArgumentOutOfRangeException(nameof(dataProcessing.SortParam.SortOptionName)),
		};
	}

	private static Expression<Func<T, object>> GetOrderByExpression<T>(SortParam sortParam, ParameterExpression param)
	{
		return Expression.Lambda<Func<T, object>>(
			Expression.Convert(Expression.Property(param, sortParam.ColumnName), typeof(object)), param);
	}
}