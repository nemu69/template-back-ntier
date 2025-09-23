namespace Core.Shared.DataProcessings.Filtering;

/// <summary>
/// This class converts the "user-friendly" <see cref="FilterParam"/> into an enum using a map.
///	The available filters being:
/// <list type="bullet">
///		<item><description>Greater</description></item>
/// 	<item><description>GreaterEqual</description></item>
/// 	<item><description>Less</description></item>
/// 	<item><description>LessEqual</description></item>
/// 	<item><description>Equal</description></item>
/// 	<item><description>NotEqual</description></item>
/// 	<item><description>Contains</description></item>
/// 	<item><description>IsType</description></item>
/// </list>
///
/// <para>
/// IsType compares the type of the row (works with inheritance) by being an equivalent to the "is" operator in C#.
/// In this case, filter value corresponds to the (case-sensitive) name to the type it is being compared to
/// e.g. For <see cref="IOTDevice"/>, C# equivalent => entity is {filterValue}?
/// </para>
/// <para>
/// Other filters compares column value TO filter value.
/// e.g. Greater => is columnValue > filterValue?
/// </para>
/// <para>The nothing (which is mapped by an empty string) operation defaults to a constant true filter and does nothing.</para>
/// </summary>
public static class FilterOptionMap
{
	private static readonly Dictionary<string, FilterOption> Map = new() {
		{ string.Empty, FilterOption.Nothing },
		{ "Greater", FilterOption.IsGreaterThan },
		{ "GreaterEqual", FilterOption.IsGreaterThanOrEqualTo },
		{ "Less", FilterOption.IsLessThan },
		{ "LessEqual", FilterOption.IsLessThanOrEqualTo },
		{ "Equal", FilterOption.IsEqualTo },
		{ "NotEqual", FilterOption.IsNotEqualTo },
		{ "Contains", FilterOption.Contains },
		{ "IsType", FilterOption.IsType },
	};

	public static FilterOption Get(string key) => Map[key];
}

public enum FilterOption
{
	Nothing = 0,
	IsGreaterThan = 1,
	IsGreaterThanOrEqualTo = 2,
	IsLessThan = 3,
	IsLessThanOrEqualTo = 4,
	IsEqualTo = 5,
	IsNotEqualTo = 6,
	Contains = 7,
	IsType = 8,
}