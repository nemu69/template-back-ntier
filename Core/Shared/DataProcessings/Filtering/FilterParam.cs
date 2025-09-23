namespace Core.Shared.DataProcessings.Filtering;

/// <summary>
/// This class describes a filter to be applied to the query.
/// </summary>
public class FilterParam
{
	/// <summary>
	/// The name of the entity property (or SQL table column name) to filter upon to.
	/// In case of the "IsType" operation, this column is useless.
	/// </summary>
	public string ColumnName { get; set; } = string.Empty;

	/// <summary>
	/// The value to which the entity value will be compared to.
	/// In case of the "IsType" operation, the type is compared to the entity itself and not one of its properties.
	/// </summary>
	public List<string> FilterValue { get; set; } = [];

	/// <summary>
	/// The "name" of which filter to use to make it "user-friendly". It is then converted to an enum for internal
	/// processing. All possible filters are in <see cref="FilterOptionMap"/>.
	/// </summary>
	public string FilterOptionName { get; set; } = string.Empty;

	public string? CastToType { get; set; }
}