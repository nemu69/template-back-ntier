using Core.Shared.Models.DB.Kernel.Interfaces;
using Core.Shared.Models.DTO.Kernel.Interfaces;
using Core.Shared.DataProcessings.Sorting;
using Core.Shared.DataProcessings.Filtering;
using Core.Shared.Paginations;

namespace Core.Shared.DataProcessings.Paginations;

public static class Pagination
{
	/// <summary>
	/// Apply filters to an <see cref="IQueryable{T}"/> source from its pagination.
	/// The last value from the <see cref="SortParam"/> is first used to remove previously queried rows. If none is given, no rows are removed.
	/// Then, it chains every <see cref="FilterParam"/> in pagination with AND boolean operators.
	/// Filters are then applied to the query.
	/// </summary>
	/// <param name="source">Query to filter</param>
	/// <param name="pagination">Pagination parameters used to filter</param>
	/// <typeparam name="T">BaseEntity from which rows will be filtered</typeparam>
	/// <typeparam name="TDTO"></typeparam>
	/// <returns>A filtered query</returns>
	public static IQueryable<T> ApplyPagination<T, TDTO>(this IQueryable<T> source, PaginationParam pagination)
		where T : class, IBaseEntity<T, TDTO>
		where TDTO : class, IDTO<T, TDTO> => source
			.ApplyDataProcess<T, TDTO>(pagination.DataProcessing);
}