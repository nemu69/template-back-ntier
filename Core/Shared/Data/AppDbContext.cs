using Core.Shared.Models.DB.Kernel;
using Core.Shared.Models.DB.System.Logs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Core.Shared.Data;

public class AppDbContext : DbContext
{
	public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
	{
	}

	public DbSet<LogEntry> Logs => Set<LogEntry>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		// for the other conventions, we do a metadata model loop
		foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
		{
			// Equivalent of builder.Conventions.Remove<OneToManyCascadeDeleteConvention>();.
			entityType.GetForeignKeys()
				.Where(fk => !fk.IsOwnership && fk.DeleteBehavior == DeleteBehavior.Cascade)
				.ToList()
				.ForEach(fk => fk.DeleteBehavior = DeleteBehavior.ClientCascade);
		}

		base.OnModelCreating(modelBuilder);
	}

	#region OverrideEfCoreSaves

	public override int SaveChanges()
	{
		ConcurrencyCheck();
		return base.SaveChanges();
	}

	public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
	{
		ConcurrencyCheck();
		return base.SaveChangesAsync(cancellationToken);
	}

	private void ConcurrencyCheck()
	{
		IEnumerable<EntityEntry> entries = ChangeTracker
			.Entries()
			.Where(e => e.Entity is BaseEntity
				&& (
					e.State == EntityState.Added
						|| e.State == EntityState.Modified));

		foreach (EntityEntry entityEntry in entries)
		{
			// ConcurrencyCheck updated
			BaseEntity entity = (BaseEntity)entityEntry.Entity;
			entity.VersionConcurrency = Guid.NewGuid();
		}
	}

	#endregion OverrideEfCoreSaves
}