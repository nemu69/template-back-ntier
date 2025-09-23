using Core.Shared.DataProcessings.Filtering;
using Core.Shared.DataProcessings.Sorting;
using Core.Shared.Models.DB.Kernel.Interfaces;
using Core.Shared.Models.DTO.Kernel.Interfaces;

namespace Core.Shared.DataProcessings;

/// <summary>
/// This class describes the data processing to be applied to the query.
/// </summary>
public class DataProcessing
{
	/// <summary>
	/// All foreign classes which should be included in the query. Presence of mandatory relations is NOT assured by the program.
	/// e.g. <see cref="IOTDevice"/> foreign relation with <see cref="IOTTag"/> is mandatory and should be given as <c>[ "IOTTags" ]</c>
	/// as it is the name of the property.
	/// </summary>
	public List<string> Includes { get; set; } = [];

	/// <summary>
	/// A list of <see cref="FilterParam"/> to apply to the query. Filter params are the first ones to be applied.
	/// Filtering also handles the <see cref="Sorting.SortParam.LastValue"/>-based DataProcessing.
	/// <seealso cref="Filter"/>
	/// </summary>
	public List<FilterParam> FilterParams { get; set; } = [];

	/// <summary>
	/// A <see cref="SortParam"/> to apply to the query. Sorting is the third and last one to be applied.
	/// <seealso cref="Sort"/>
	/// </summary>
	public SortParam SortParam { get; set; } = new();
}

public static class DataProcessingExtensions
{
	/// <summary>
	/// Apply filters to an <see cref="IQueryable{T}"/> source from its pagination.
	/// The last value from the <see cref="SortParam"/> is first used to remove previously queried rows. If none is given, no rows are removed.
	/// Then, it chains every <see cref="FilterParam"/> in pagination with AND boolean operators.
	/// Filters are then applied to the query.
	/// </summary>
	/// <param name="source">Query to filter</param>
	/// <param name="dataProcessing">DataProcessing parameters used to filter</param>
	/// <typeparam name="T">BaseEntity from which rows will be filtered</typeparam>
	/// <typeparam name="TDTO"></typeparam>
	/// <returns>A filtered query</returns>
	public static IQueryable<T> ApplyDataProcess<T, TDTO>(this IQueryable<T> source, DataProcessing dataProcessing)
		where T : class, IBaseEntity<T, TDTO>
		where TDTO : class, IDTO<T, TDTO> => source.ApplyFiltering<T, TDTO>(dataProcessing)
			.ApplySorting<T, TDTO>(dataProcessing);
}