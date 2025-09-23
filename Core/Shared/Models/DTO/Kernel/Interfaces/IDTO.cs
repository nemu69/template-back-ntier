using Core.Shared.Models.DB.Kernel.Interfaces;

namespace Core.Shared.Models.DTO.Kernel.Interfaces;

/// <summary>
///     Interface defining the properties of a DTO.
/// </summary>
/// <typeparam name="T">Type which the DTO is linked</typeparam>
/// <typeparam name="TDTO"></typeparam>
public interface IDTO<T, TDTO>
	where T : class, IBaseEntity<T, TDTO>
	where TDTO : class, IDTO<T, TDTO>
{
	int ID { get; set; }
	DateTimeOffset? TS { get; set; }

	/// <summary>
	///     Converts the DTO to the model as its own type.
	/// </summary>
	T ToModel();
}