using System.Linq.Expressions;
using Core.Shared.DataProcessings;
using Core.Shared.DataProcessings.Paginations;
using Core.Shared.Exceptions;
using Core.Shared.Models.DB.Kernel.Interfaces;
using Core.Shared.Models.DTO.Kernel.Interfaces;
using Core.Shared.Paginations;
using Core.Shared.Repositories.Kernel.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

using Core.Shared.Extensions;

namespace Core.Shared.Repositories.Kernel;

/// <summary>
///     Implements the <see cref="IBaseEntityRepository{T,TDTO}" /> interface
/// </summary>
/// <typeparam name="TContext"> <see cref="DbContext" /> of the project</typeparam>
/// <typeparam name="T">
///     Type that defines a table in the database and have to implement <see cref="IBaseEntity{T}" />
/// </typeparam>
/// <typeparam name="TDTO">
///     Type that defines a DTO of <typeref name="T" /> and have to implement <see cref="IDTO{T,TDTO}" />
/// </typeparam>
public class BaseEntityRepository<TContext, T, TDTO> : IBaseEntityRepository<T, TDTO>
	where TContext : DbContext
	where T : class, IBaseEntity<T, TDTO>
	where TDTO : class, IDTO<T, TDTO>
{
	protected readonly ICollection<Expression<Func<T, bool>>> _importFilters = [];
	private readonly string[] _requiredIncludes;

	protected readonly TContext _context;

	/// <summary>
	///     Constructor of the <see cref="BaseEntityRepository{TContext,T,TDTO}" />
	/// </summary>
	/// <param name="context"><see cref="DbContext" /> of the project</param>
	/// <param name="requiredIncludes">All includes made within foreign relations (eg: WorkingOrder.EquipmentI.EquipmentC)</param>
	public BaseEntityRepository(
		TContext context,
		string[] requiredIncludes)
	{
		_context = context;
		_requiredIncludes = requiredIncludes;
	}

	public Task<T?> GetById(
		int id,
		Expression<Func<T, bool>>[]? filters = null,
		bool withTracking = true,
		params string[] includes
		) => Query(filters, null, withTracking, includes: includes)
			.FirstOrDefaultAsync(x => x.ID == id);

	public async Task<T> GetByIdWithThrow(
		int id,
		Expression<Func<T, bool>>[]? filters = null,
		bool withTracking = true,
		params string[] includes
		) => await Query(filters, null, withTracking, includes: includes).FirstOrDefaultAsync(x => x.ID == id)
			?? throw new EntityNotFoundException(typeof(T).Name, id);

	public Task<T?> GetBy(
		Expression<Func<T, bool>>[]? filters = null,
		Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
		bool withTracking = true,
		params string[] includes
		) => Query(filters, orderBy, withTracking, includes: includes).FirstOrDefaultAsync();

	public async Task<T> GetByWithThrow(
		Expression<Func<T, bool>>[]? filters = null,
		Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
		bool withTracking = true,
		params string[] includes
		) => await Query(filters, orderBy, withTracking, includes: includes).FirstOrDefaultAsync()
			?? throw new EntityNotFoundException(typeof(T).Name + " not found");

	public async Task<T> GetByWithDataProcess(DataProcessing dataProcessing)
		=> await dataProcessing.Includes
			.Aggregate(
				_context.Set<T>().AsQueryable(),
				(current, value) => current.Include(value))
			.AsNoTracking()
			.ApplyDataProcess<T, TDTO>(dataProcessing)
			.FirstOrDefaultAsync()
			?? throw new EntityNotFoundException(typeof(T).Name + " not found");

	public Task<List<T>> GetAll(
		Expression<Func<T, bool>>[]? filters = null,
		Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
		bool withTracking = true,
		int? maxCount = null,
		params string[] includes
		) => Query(filters, orderBy, withTracking, maxCount, includes).ToListAsync();

	public Task<List<T>> GetAllWithDataProcess(DataProcessing dataProcessing)
		=> dataProcessing.Includes
			.Aggregate(
				_context.Set<T>().AsQueryable(),
				(current, value) => current.Include(value))
			.AsNoTracking()
			.ApplyDataProcess<T, TDTO>(dataProcessing)
			.ToListAsync();

	public Task<List<T>> GetWithPagination(PaginationParam pagination, int nbItems)
	{
		return pagination.DataProcessing.Includes
			.Aggregate(
				_context.Set<T>().AsQueryable(),
				(current, value) => current.Include(value))
			.AsNoTracking()
			.ApplyPagination<T, TDTO>(pagination)
			.ToListAsync();
	}

	public Task<List<T>> GetAllBy(Expression<Func<T, bool>> expression)
		=> _context.Set<T>().Where(expression).ToListAsync();

	public async Task Add(T entity) => await _context.Set<T>().AddAsync(entity);

	public Task AddRange(IEnumerable<T> entities) => _context.Set<T>().AddRangeAsync(entities);

	public void Remove(T entity) => _context.Set<T>().Remove(entity);

	public async Task RemoveByID(int id)
	{
		T entity = await GetByIdWithThrow(id);
		_context.Set<T>().Remove(entity);
	}

	public Task RemoveByLifeSpan(TimeSpan lifeSpan)
		=> _context.Set<T>().AsQueryable().Where(t => t.TS < DateTimeOffset.Now.Subtract(lifeSpan)).ExecuteDeleteAsync();

	public void RemoveRange(IEnumerable<T> entities) => _context.Set<T>().RemoveRange(entities);

	public void Update(T entity) => _context.Set<T>().Update(entity);

	public void UpdateRange(IEnumerable<T> entities) => _context.Set<T>().UpdateRange(entities);

	public Task<int> ExecuteUpdateByIdAsync(
		int id,
		Expression<Func<SetPropertyCalls<T>, SetPropertyCalls<T>>> properties)
			=> _context.Set<T>().Where(x => x.ID == id).ExecuteUpdateAsync(properties);

	public Task<int> ExecuteUpdateAsync(
		Expression<Func<T, bool>> predicate,
		Expression<Func<SetPropertyCalls<T>, SetPropertyCalls<T>>> properties)
			=> _context.Set<T>().Where(predicate).ExecuteUpdateAsync(properties);

	public Task<int> ExecuteUpdateEntityAsync(T entity)
			=> _context.Set<T>().Where(x => x.ID == entity.ID).ExecuteUpdateEntityAsync(entity);

	public Task<int> ExecuteDeleteAsync(Expression<Func<T, bool>> predicate)
		=> _context.Set<T>().Where(predicate).ExecuteDeleteAsync();

	public Task<bool> AnyPredicate(Expression<Func<T, bool>> predicate, bool withTracking = true, params string[] includes)
	{
		return Query(
			[predicate],
			null,
			withTracking,
			null,
			includes)
			.AnyAsync(predicate);
	}

	public Task<bool> Any(bool withTracking = true, params string[] includes)
	{
		return Query(
			[],
			null,
			withTracking,
			null,
			includes)
			.AnyAsync();
	}

	/// <summary>
	/// Executes a query on the database to retrieve entities of type T.
	/// </summary>
	/// <param name="filters">An array of filter expressions to apply to the query.</param>
	/// <param name="orderBy">A function to order the query results.</param>
	/// <param name="withTracking">A flag indicating whether to track changes in the entities.</param>
	/// <param name="maxCount">The maximum number of entities to retrieve.</param>
	/// <param name="includes">An array of navigation properties to include in the query.</param>
	/// <returns>An IQueryable of entities of type <typeparamref name="T" />.</returns>
	private IQueryable<T> Query(
		Expression<Func<T, bool>>[]? filters = null,
		Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
		bool withTracking = true,
		int? maxCount = null,
		params string[] includes
		)
	{
		IQueryable<T> query = _context.Set<T>().AsQueryable();

		Dictionary<string, string[]> mergedIncludes = GetMergedIncludes(
			new Dictionary<string, string[]> { { string.Empty, includes } });
		if (mergedIncludes.Count > 0)
			query = QueryIncludes(query, mergedIncludes);

		if (!withTracking)
			query = query.AsNoTracking();

		if (_importFilters.Count > 0)
			query = _importFilters.Aggregate(query, (current, filter) => current.Where(filter));

		if (filters is not null)
			query = filters.Aggregate(query, (current, filter) => current.Where(filter));

		if (maxCount is not null)
			query = query.Take(maxCount.Value);

		return (orderBy is not null) ? orderBy(query) : query;
	}

	/// <summary>
	/// Applies the specified includes to the given query.
	/// </summary>
	/// <param name="query">The query to apply includes to.</param>
	/// <param name="includes">The dictionary of includes to apply.</param>
	/// <returns>The modified query with includes applied.</returns>
	private static IQueryable<T> QueryIncludes(IQueryable<T> query, Dictionary<string, string[]> includes)
	{
		foreach (KeyValuePair<string, string[]> include in includes)
		{
			if (include.Key != string.Empty)
			{
				query = query.Include(include.Key);
				query = include.Value.Aggregate(
					query,
					(current, value) => current.Include(include.Key + "." + value));
			}
			else
			{
				query = include.Value.Aggregate(query, (current, value) => current.Include(value));
			}
		}

		if (includes.Count > 0)
		{
			// WARNING - https://learn.microsoft.com/fr-fr/ef/core/querying/single-split-queries
			query = query.AsSplitQuery();
		}

		return query;
	}

	/// <summary>
	/// Merges the user-defined includes with the base includes dictionary.
	/// User-defined includes override the base includes when needed.
	/// </summary>
	/// <param name="includes">The user-defined includes dictionary.</param>
	/// <returns>The merged includes dictionary.</returns>
	private Dictionary<string, string[]> GetMergedIncludes(Dictionary<string, string[]>? includes)
	{
		// Here, we copy the dictionary as user-defined includes should override baseConcatIncludes when needed and not otherwise.
		Dictionary<string, string[]> mergedIncludes =
			new(_requiredIncludes.ToDictionary(x => x, _ => Array.Empty<string>()));
		foreach ((string? key, string[]? value) in includes ?? [])
		{
			bool isExist = mergedIncludes.TryGetValue(key, out string[]? existingValue);
			mergedIncludes[key] = isExist
				? [.. existingValue, .. value]
				: value;
		}

		return mergedIncludes;
	}
}