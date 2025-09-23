using System.Linq.Expressions;
using Core.Shared.DataProcessings;
using Core.Shared.Exceptions;
using Core.Shared.Models.DB.Kernel.Interfaces;
using Core.Shared.Models.DTO.Kernel.Interfaces;
using Core.Shared.Paginations;
using Core.Shared.Repositories.Kernel.Interfaces;
using Core.Shared.Services.Kernel.Interfaces;
using Core.Shared.UnitOfWork.Interfaces;
using Mapster;

namespace Core.Shared.Services.Kernel;

public class BaseEntityService<TRepository, T, TDTO> : IBaseEntityService<T, TDTO>
	where TRepository : IBaseEntityRepository<T, TDTO>
	where T : class, IBaseEntity<T, TDTO>
	where TDTO : class, IDTO<T, TDTO>
{
	private readonly TRepository _repository;
	protected readonly IAppUOW _appUOW;

	protected BaseEntityService(IAppUOW appUOW)
	{
		_appUOW = appUOW;
		_repository = (TRepository?)appUOW.GetRepoByType(typeof(TRepository)) ??
			throw new InvalidOperationException("Repo is null");
	}

	public async Task<TDTO> GetByID(
		int id,
		Expression<Func<T, bool>>[]? filters = null,
		bool withTracking = true,
		params string[] includes) => (await _repository.GetByIdWithThrow(id, filters, withTracking, includes)).ToDTO();

	public async Task<TDTO> GetByWithDataProcess(DataProcessing dataProcessing)
		=> (await _repository.GetByWithDataProcess(dataProcessing)).ToDTO();

	public async Task<List<TDTO>> GetAll(
		Expression<Func<T, bool>>[]? filters = null,
		Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
		bool withTracking = true,
		int? maxCount = null,
		params string[] includes)
	{
		return (await _repository.GetAll(filters, orderBy, withTracking, maxCount, includes)).ConvertAll(entity =>
			entity.ToDTO());
	}

	public async Task<List<TDTO>> GetAllWithDataProcess(DataProcessing dataProcessing)
	{
		return (await _repository.GetAllWithDataProcess(dataProcessing)).ConvertAll(entity =>
			entity.ToDTO());
	}

	public async Task<List<TDTO>> GetWithPagination(PaginationParam pagination, int nbItems)
	{
		IEnumerable<T> query = (await _repository.GetWithPagination(pagination, nbItems))
			.SkipWhile(entity => pagination.LastValueID is not null && entity.ID != pagination.LastValueID)
			.Skip((pagination.LastValueID is null) ? 0 : 1);

		return (nbItems < 0)
			? query.ToList().ConvertAll(entity => entity.ToDTO())
			: query.Take(nbItems).ToList().ConvertAll(entity => entity.ToDTO());
	}

	public async Task<int> CountWithPagination(PaginationParam pagination)
		=> (await _repository.GetWithPagination(pagination, 0))
			.SkipWhile(entity => pagination.LastValueID is not null && entity.ID != pagination.LastValueID)
			.Skip((pagination.LastValueID is null) ? 0 : 1)
			.Count();

	public async Task<TDTO> Add(T entity)
	{
		await _appUOW.StartTransaction();
		await _repository.Add(entity);
		_appUOW.Commit();
		await _appUOW.CommitTransaction();
		return entity.ToDTO();
	}

	public async Task<List<TDTO>> AddAll(IEnumerable<T> entities)
	{
		await _appUOW.StartTransaction();
		await _repository.AddRange(entities);
		_appUOW.Commit();
		await _appUOW.CommitTransaction();
		return entities.ToList().ConvertAll(entity => entity.ToDTO());
	}

	public async Task<TDTO> Update(T entity)
	{
		return (await _repository.ExecuteUpdateEntityAsync(entity) == 0)
			? throw new EntityNotFoundException(entity.ID)
			: entity.ToDTO();
	}

	public async Task<TDTO> UpdateTransaction(T entity)
	{
		T? entityToUpdate = await _repository.GetById(entity.ID);
		entity.Adapt(entityToUpdate);
		_appUOW.Commit();

		return entity.ToDTO();
	}

	public async Task<List<TDTO>> UpdateAll(IEnumerable<T> entities)
	{
		await _appUOW.StartTransaction();
		_repository.UpdateRange(entities);
		_appUOW.Commit();
		await _appUOW.CommitTransaction();

		return entities.ToList().ConvertAll(entity => entity.ToDTO());
	}

	public async Task Remove(int id)
	{
		await _appUOW.StartTransaction();
		await _repository.RemoveByID(id);
		_appUOW.Commit();
		await _appUOW.CommitTransaction();
	}

	public async Task RemoveByLifeSpan(TimeSpan lifeSpan) => await _repository.RemoveByLifeSpan(lifeSpan);

	public async Task RemoveAll(IEnumerable<T> entities)
	{
		await _appUOW.StartTransaction();
		foreach (T entity in entities)
			_repository.Remove(entity);

		_appUOW.Commit();
		await _appUOW.CommitTransaction();
	}
}