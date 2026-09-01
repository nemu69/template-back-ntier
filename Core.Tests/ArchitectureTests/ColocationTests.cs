using Core.Shared.Models.DB.Kernel.Interfaces;
using Shouldly;

namespace Core.Tests.ArchitectureTests;

/// <summary>
/// Pairs that ArchUnitNET cannot express as a single fluent rule (interface generic args, same namespace).
/// </summary>
public class ColocationTests
{
	[Theory]
	[MemberData(nameof(EntityServiceTypes))]
	public void EntityService_ShouldShareNamespace_WithMatchingInterface(Type serviceType)
	{
		Type? iface = serviceType.GetInterface("I" + serviceType.Name, ignoreCase: false);
		iface.ShouldNotBeNull($"expected interface I{serviceType.Name} for {serviceType.FullName}");
		iface!.Namespace.ShouldBe(serviceType.Namespace);
	}

	[Theory]
	[MemberData(nameof(EntityRepositoryTypes))]
	public void EntityRepository_ShouldShareNamespace_WithMatchingInterface(Type repositoryType)
	{
		Type? iface = repositoryType.GetInterface("I" + repositoryType.Name, ignoreCase: false);
		iface.ShouldNotBeNull($"expected interface I{repositoryType.Name} for {repositoryType.FullName}");
		iface!.Namespace.ShouldBe(repositoryType.Namespace);
	}

	[Theory]
	[MemberData(nameof(DomainEntityTypes))]
	public void DomainEntity_ShouldPair_WithDtoNamedDTOEntity(Type entityType)
	{
		Type? idto = entityType.GetInterfaces()
			.FirstOrDefault(iface =>
				iface.IsGenericType
				&& iface.GetGenericTypeDefinition() == typeof(IBaseEntity<,>)
				&& iface.GetGenericArguments()[0] == entityType);
		idto.ShouldNotBeNull($"{entityType.Name} should implement IBaseEntity<T, TDTO>");

		Type dtoType = idto!.GetGenericArguments()[1];
		dtoType.Name.ShouldBe("DTO" + entityType.Name);
		dtoType.Namespace.ShouldNotBeNull();
		dtoType.Namespace.ShouldContain(".Models.DTO");
	}

	[Theory]
	[MemberData(nameof(EntityServiceTypes))]
	public void EntityService_ShouldLiveNextTo_MatchingRepository(Type serviceType)
	{
		string entityName = serviceType.Name[..^"Service".Length];
		string? servicesNamespace = serviceType.Namespace;
		servicesNamespace.ShouldNotBeNull();
		string repositoriesNamespace = servicesNamespace.Replace(".Services", ".Repositories");

		Type? repository = GeneratedArchitecture.CoreAssembly.GetTypes()
			.FirstOrDefault(type => type.Name == entityName + "Repository" && type.Namespace == repositoriesNamespace);
		repository.ShouldNotBeNull(
			$"{serviceType.Name} should have {entityName}Repository in {repositoriesNamespace}");
	}

	public static TheoryData<Type> EntityServiceTypes() => Types(ArchitecturePredicates.IsEntityService);

	public static TheoryData<Type> EntityRepositoryTypes() => Types(ArchitecturePredicates.IsEntityRepository);

	public static TheoryData<Type> DomainEntityTypes() => Types(ArchitecturePredicates.IsDomainEntity);

	private static TheoryData<Type> Types(Func<Type, bool> predicate) => [.. GeneratedArchitecture.CoreAssembly.GetTypes().Where(predicate).OrderBy(static type => type.FullName)];
}
