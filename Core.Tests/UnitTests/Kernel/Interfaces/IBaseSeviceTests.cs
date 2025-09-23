using Core.Shared.Repositories.Kernel.Interfaces;
using Core.Shared.Models.DB.Kernel.Interfaces;
using Core.Shared.Models.DTO.Kernel.Interfaces;
using Core.Shared.Services.Kernel.Interfaces;

namespace Core.Tests.UnitTests.Kernel.Interfaces;

public interface IBaseServiceTests<TService, TRepository, T, TDTO>
	where TService : class, IBaseEntityService<T, TDTO>
	where TRepository : class, IBaseEntityRepository<T, TDTO>
	where T : class, IBaseEntity<T, TDTO>
	where TDTO : class, IDTO<T, TDTO>
{
	void AssertUnitOfWorkCommit();
	Task AssertUnitOfWorkCommitAndTransaction();
}