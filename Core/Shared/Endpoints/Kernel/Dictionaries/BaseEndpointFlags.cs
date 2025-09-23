namespace Core.Shared.Endpoints.Kernel.Dictionaries;

[Flags]
public enum BaseEndpointFlags
{
	None = 0,
	Create = 1,
	Read = 2,
	Update = 4,
	Delete = 8,
	All = Create | Read | Update | Delete,
	ToLogs = 16,
}