using Core.Shared.Models.DB.Kernel.Interfaces;
using Core.Shared.Models.DTO.Kernel.Interfaces;
using Core.Shared.Repositories.Kernel.Interfaces;
using Core.Shared.Services.Kernel.Interfaces;
using Core.Shared.UnitOfWork.Interfaces;
using Core.Tests.UnitTests.Kernel.Interfaces;
using NSubstitute;

namespace Core.Tests.UnitTests.Kernel;

public abstract class BaseServiceTests<TService, TRepository, T, TDTO> :
	IBaseServiceTests<TService, TRepository, T, TDTO>
	where TService : class, IBaseEntityService<T, TDTO>
	where TRepository : class, IBaseEntityRepository<T, TDTO>
	where T : class, IBaseEntity<T, TDTO>
	where TDTO : class, IDTO<T, TDTO>
{
	protected readonly TService _service;
	protected readonly TRepository _repository;
	protected readonly IAppUOW _appUOW;
	protected T _mockEntity = null!;

	protected BaseServiceTests(Func<IAppUOW, TService> serviceFactory)
	{
		_repository = Substitute.For<TRepository>();
		_appUOW = Substitute.For<IAppUOW>();

		_appUOW.GetRepoByType(Arg.Is(typeof(TRepository))).Returns(_repository);
		_service = serviceFactory(_appUOW);
	}

	public void AssertUnitOfWorkCommit() => _appUOW.Received(1).Commit();

	public async Task AssertUnitOfWorkCommitAndTransaction()
	{
		AssertUnitOfWorkCommit();
		await _appUOW.Received(1).StartTransaction();
		await _appUOW.Received(1).CommitTransaction();
	}
}