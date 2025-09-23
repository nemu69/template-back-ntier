namespace Core.Shared.DataProcessings.Sorting;

/// <summary>
/// A single sortParam is needed per pagination. If LastValue is empty it defaults to the first values,
/// if ColumnName or SortOptionName is empty, it defaults to the default descending ID orderBy.
/// </summary>
/// <summary>
/// This class describes a sort to be applied to the query.
/// </summary>
public class SortParam
{
	/// <summary>
	/// The name of the entity property (or SQL table column name) to sort upon.
	/// </summary>
	public string ColumnName { get; set; } = string.Empty;

	/// <summary>
	/// The "name" of which sort order to use to make it "user-friendly". It is then converted to an enum for internal
	/// processing. All possible filters are in <see cref="SortOptionMap"/>.
	/// </summary>
	public string SortOptionName { get; set; } = string.Empty;
}