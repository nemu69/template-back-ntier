using Core.Shared.DataProcessings;

namespace Core.Shared.Paginations;

/// <summary>
///	This class is used as an argument for paginated request along with filtering, text search and sorting.
/// </summary>
public class PaginationParam
{
	public DataProcessing DataProcessing { get; set; } = new();

	/// <summary>
	/// Used for pagination, it should be the last value of the current page from the ID.
	/// If empty, the first page is returned.
	/// </summary>
	public int? LastValueID { get; set; }
}