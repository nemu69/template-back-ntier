using System.Linq.Expressions;
using Core.Shared.DataProcessings;
using Core.Shared.Models.DB.Kernel.Interfaces;
using Core.Shared.Models.DTO.Kernel.Interfaces;
using Core.Shared.Paginations;

namespace Core.Shared.Services.Kernel.Interfaces;

public interface IBaseEntityService<T, TDTO>
	where T : class, IBaseEntity<T, TDTO>
	where TDTO : class, IDTO<T, TDTO>
{
	Task<TDTO> GetByID(
		int id,
		Expression<Func<T, bool>>[]? filters = null,
		bool withTracking = true,
		params string[] includes);

	Task<TDTO> GetByWithDataProcess(DataProcessing dataProcessing);

	Task<List<TDTO>> GetAll(
		Expression<Func<T, bool>>[]? filters = null,
		Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
		bool withTracking = true,
		int? maxCount = null,
		params string[] includes);

	Task<List<TDTO>> GetAllWithDataProcess(DataProcessing dataProcessing);

	Task<List<TDTO>> GetWithPagination(PaginationParam pagination, int nbItems);
	Task<int> CountWithPagination(PaginationParam pagination);

	Task<TDTO> Add(T entity);
	Task<List<TDTO>> AddAll(IEnumerable<T> entities);
	Task<TDTO> Update(T entity);
	Task<TDTO> UpdateTransaction(T entity);
	Task<List<TDTO>> UpdateAll(IEnumerable<T> entities);
	Task Remove(int id);
	Task RemoveByLifeSpan(TimeSpan lifeSpan);
	Task RemoveAll(IEnumerable<T> entities);
}