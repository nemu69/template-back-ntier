using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using Carter;
using Core.Shared.Models.DB.Kernel;
using Core.Shared.Models.DB.Kernel.Interfaces;
using Core.Shared.Models.DTO.Kernel.Interfaces;
using Core.Shared.Repositories.Kernel.Interfaces;
using Core.Shared.Services.Kernel.Interfaces;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using ClrType = System.Type;
using ReflectionAssembly = System.Reflection.Assembly;

namespace Core.Tests.ArchitectureTests;

/// <summary>
/// N-tier layout for generated APIs: host projects (Api*) depend on Core.
/// Core holds entities, DTOs, services, repositories, UoW and shared kernel.
/// </summary>
internal static class GeneratedArchitecture
{
	internal static readonly ReflectionAssembly CoreAssembly = typeof(BaseEntity).Assembly;

	internal static readonly ReflectionAssembly[] HostAssemblies = DiscoverHostAssemblies();

	internal static readonly Architecture Architecture = Load();

	internal static readonly IObjectProvider<IType> CoreLayer =
		Types().That().ResideInAssembly(CoreAssembly).As("Core");

	internal static readonly IObjectProvider<IType> PresentationLayer =
		HostAssemblies.Length == 0
			? Types().That().HaveFullName("ArchitectureTests.NoHostAssembly").As("Presentation")
			: Types().That().ResideInAssembly(HostAssemblies[0], [.. HostAssemblies.Skip(1)]).As("Presentation");

	internal static readonly IObjectProvider<Class> EntityTypes =
		Classes().That().ImplementInterface(typeof(IBaseEntity<,>)).As("Entities implementing IBaseEntity");

	internal static readonly IObjectProvider<Class> EntityDtoTypes =
		Classes().That().ImplementInterface(typeof(IDTO<,>)).As("DTOs implementing IDTO");

	internal static readonly IObjectProvider<Class> EntityServices =
		Classes().That()
			.ImplementInterface(typeof(IBaseEntityService<,>))
			.And().DoNotResideInNamespace("Core.Shared.Services.Kernel")
			.As("Entity services");

	internal static readonly IObjectProvider<Class> EntityRepositories =
		Classes().That()
			.ImplementInterface(typeof(IBaseEntityRepository<,>))
			.And().DoNotResideInNamespace("Core.Shared.Repositories.Kernel")
			.As("Entity repositories");

	internal static readonly IObjectProvider<Class> CarterModules =
		Classes().That().ImplementInterface(typeof(ICarterModule)).As("Carter modules");

	private static Architecture Load()
	{
		ArchLoader loader = new ArchLoader().LoadAssemblies(CoreAssembly);
		if (HostAssemblies.Length > 0)
			loader.LoadAssemblies(HostAssemblies);

		return loader.Build();
	}

	private static ReflectionAssembly[] DiscoverHostAssemblies()
	{
		return [.. typeof(GeneratedArchitecture).Assembly
			.GetReferencedAssemblies()
			.Where(static name => name.Name is not null && IsHostAssemblyName(name.Name))
			.Select(ReflectionAssembly.Load)];
	}

	private static bool IsHostAssemblyName(string name) =>
		name.StartsWith("Api", StringComparison.Ordinal);
}

internal static class ArchitecturePredicates
{
	internal static bool IsEntityService(ClrType type) =>
		type is { IsClass: true, IsAbstract: false }
		&& type.Namespace is not null
		&& type.Namespace.StartsWith("Core.Entities.", StringComparison.Ordinal)
		&& type.Namespace.EndsWith(".Services", StringComparison.Ordinal)
		&& ImplementsOpenGeneric(type, typeof(IBaseEntityService<,>));

	internal static bool IsEntityRepository(ClrType type) =>
		type is { IsClass: true, IsAbstract: false }
		&& type.Namespace is not null
		&& type.Namespace.StartsWith("Core.Entities.", StringComparison.Ordinal)
		&& type.Namespace.EndsWith(".Repositories", StringComparison.Ordinal)
		&& ImplementsOpenGeneric(type, typeof(IBaseEntityRepository<,>));

	internal static bool IsDomainEntity(ClrType type) =>
		type is { IsClass: true, IsAbstract: false }
		&& type.Namespace is not null
		&& type.Namespace.StartsWith("Core.Entities.", StringComparison.Ordinal)
		&& ImplementsOpenGeneric(type, typeof(IBaseEntity<,>));

	internal static bool ImplementsOpenGeneric(ClrType type, ClrType openGeneric) =>
		type.GetInterfaces().Any(iface => iface.IsGenericType && iface.GetGenericTypeDefinition() == openGeneric);
}
