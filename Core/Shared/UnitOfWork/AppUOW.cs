using Core.Shared.Data;
using Core.Shared.UnitOfWork.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace Core.Shared.UnitOfWork;

public class AppUOW : IAppUOW
{
	private readonly AppDbContext _appDbContext;
	private IDbContextTransaction? _transaction;
	private int _transactionCount;

	public AppUOW(AppDbContext appDbContext)
	{
		_appDbContext = appDbContext;
	}

	public object? GetRepoByType(Type repo)
	{
		return repo switch {
			_ => null,
		};
	}

	public int Commit()
	{
		const int maxRetries = 1;
		int retryCount = 0;

		while (retryCount < maxRetries)
		{
			try
			{
				return _appDbContext.SaveChanges();
			}
			catch (DbUpdateConcurrencyException e)
			{
				retryCount++;
				if (retryCount <= maxRetries)
				{
					HandleConcurrencyException(e);
					Console.WriteLine(
						$"Concurrency conflict. Attempt: {retryCount.ToString()}/{maxRetries.ToString()}");
				}
				else
				{
					Console.WriteLine("Failure to apply.");
				}
			}
			catch (Exception e)
			{
				if (_transaction is not null)
				{
					_transaction.Rollback();
					_transaction = null;
				}

				throw new("An error happened during SaveChanges", e);
			}
		}

		return -1;
	}

	/// <summary>
	///     Transaction is necessary in order to do a rollback after multiple saves in case an error is encountered
	/// </summary>
	/// <exception cref="Exception"></exception>
	public async Task StartTransaction()
	{
		_transactionCount += 1;
		if (_transaction is not null)
			return;

		try
		{
			_transaction = await _appDbContext.Database.BeginTransactionAsync();
		}
		catch (Exception e)
		{
			if (e is not InvalidOperationException)
				throw new(e.Message, e);

			throw new("An error happened when starting the transaction", e);
		}
	}

	public async Task CommitTransaction()
	{
		if (_transaction is not null && _transactionCount == 1)
		{
			try
			{
				await _transaction.CommitAsync();
				_transaction = null;
			}
			catch (Exception e)
			{
				_transaction?.Rollback();
				_transaction = null;
				throw new("An error happened when commiting transaction", e);
			}
		}

		_transactionCount -= 1;
	}

	public void Dispose()
	{
		_appDbContext.Dispose();
		GC.SuppressFinalize(this);
	}

	public int GetTransactionCount() => _transactionCount;

	public bool GetTransactionIsNull() => _transaction is null;

	private static void HandleConcurrencyException(DbUpdateConcurrencyException ex)
	{
		foreach (EntityEntry entry in ex.Entries)
		{
			PropertyValues? databaseEntry = entry.GetDatabaseValues();
			object? databaseValues = databaseEntry?.ToObject();

			if (databaseValues is not null)
			{
				PropertyValues originalValues = entry.OriginalValues;
				PropertyValues currentValues = entry.CurrentValues;

				foreach (IProperty property in entry.OriginalValues.Properties)
				{
					object? original = originalValues[property];
					object? current = currentValues[property];
					object? database = databaseEntry?[property];

					if (!Equals(database, original) && Equals(current, original))
						currentValues[property] = database;
				}

				if (databaseEntry is not null)
					entry.OriginalValues.SetValues(databaseEntry);
			}
		}
	}
}