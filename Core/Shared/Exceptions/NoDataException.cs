namespace Core.Shared.Exceptions;

public class NoDataException : Exception
{
	public NoDataException()
	{
	}

	public NoDataException(string message)
		: base(message)
	{
	}

	public NoDataException(string message, Exception inner)
		: base(message, inner)
	{
	}

	public NoDataException(string entity, string rid)
		: base("Could not find data for a '" + entity + "' with rid{" + rid + "}.")
	{
	}

	public NoDataException(string entity, int id)
		: base("Could not find data for a '" + entity + "' with id{" + id + "}.")
	{
	}

	public NoDataException(int id)
		: base("Could not find data for the requested entity with id{" + id + "}.")
	{
	}
}