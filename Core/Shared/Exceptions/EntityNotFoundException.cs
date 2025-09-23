namespace Core.Shared.Exceptions;

public class EntityNotFoundException : Exception
{
	public EntityNotFoundException()
	{
	}

	public EntityNotFoundException(string message)
		: base(message)
	{
	}

	public EntityNotFoundException(string message, Exception inner)
		: base(message, inner)
	{
	}

	public EntityNotFoundException(string entity, string rid)
		: base("Could not find a '" + entity + "' with rid{" + rid + "}.")
	{
	}

	public EntityNotFoundException(string entity, int id)
		: base("Could not find a '" + entity + "' with id{" + id + "}.")
	{
	}

	public EntityNotFoundException(int id)
		: base("Could not find the requested entity with id{" + id + "}.")
	{
	}
}