using System.ComponentModel.DataAnnotations;
using Core.Shared.Models.DB.Kernel.Interfaces;
using Core.Shared.Models.DTO.Kernel;

namespace Core.Shared.Models.DB.Kernel;

/// <summary>
///     Base class for all entities
/// </summary>
public class BaseEntity : IBaseEntity<BaseEntity, DTOBaseEntity>
{
	public BaseEntity()
	{
	}

	protected BaseEntity(DTOBaseEntity dto)
	{
		ID = dto.ID;
		TS = (DateTimeOffset)dto.TS!;
		VersionConcurrency = Guid.NewGuid();
	}

	public int ID { get; set; }

	public DateTimeOffset TS { get; set; } = DateTimeOffset.Now;

	[ConcurrencyCheck]
	public Guid VersionConcurrency { get; set; } = Guid.NewGuid();

	/// <summary>
	///     Converts the entity to its DTO.
	/// </summary>
	/// <returns> <see cref="DTO" /> of the entity</returns>
	public virtual DTOBaseEntity ToDTO()
	{
		return new() {
			ID = ID,
			TS = TS,
		};
	}
}