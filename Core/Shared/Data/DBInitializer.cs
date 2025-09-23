namespace Core.Shared.Data;

/// <summary>
/// Functions to be called upon startup of one Api per station/server to initialise the database.
/// </summary>
public static class DBInitializer
{
	public static Task Initialize(AppDbContext _) => Task.CompletedTask;
}