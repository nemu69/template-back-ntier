namespace Core.Shared.UnitOfWork.Interfaces;

/// <summary>
///     Interface IUnitOfWork defines the methods that are required to be implemented by a Unit of Work class.
/// </summary>
public interface IAppUOW : IDisposable
{
	object? GetRepoByType(Type repo);

	/// <summary>
	///     Saves changes made in this context to the underlying database.
	/// </summary>
	int Commit();

	Task StartTransaction();

	Task CommitTransaction();

	int GetTransactionCount();

	bool GetTransactionIsNull();
}