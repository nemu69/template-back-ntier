using Core.Shared.Models.DB.Kernel;

namespace Core.Shared.DataProcessings.Sorting;

/// <summary>
/// This class converts the "user-friendly" <see cref="SortParam"/> into an enum using a map.
///	The available sorting methods being:
/// <list type="bullet">
///		<item><description>Ascending</description></item>
/// 	<item><description>Descending</description></item>
/// </list>
///
/// The nothing (which is mapped by an empty string) operation defaults to a descending filter on primary key (<see cref="BaseEntity.ID"/>)
/// </summary>
public static class SortOptionMap
{
	private static readonly Dictionary<string, SortOption> Map = new() {
		{ string.Empty, SortOption.None },
		{ "Ascending", SortOption.Ascending },
		{ "Descending", SortOption.Descending },
	};

	public static SortOption Get(string key) => Map[key];
}

public enum SortOption
{
	None = 0,
	Ascending = 1,
	Descending = 2,
}