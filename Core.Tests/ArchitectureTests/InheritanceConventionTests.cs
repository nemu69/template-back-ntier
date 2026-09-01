using ArchUnitNET.xUnit;
using Carter;
using Core.Shared.Endpoints.Kernel;
using Core.Shared.Models.DB.Kernel;
using Core.Shared.Models.DTO.Kernel;
using Core.Shared.Repositories.Kernel;
using Core.Shared.Services.Kernel;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using ReflectionAssembly = System.Reflection.Assembly;

namespace Core.Tests.ArchitectureTests;

public class InheritanceConventionTests
{
	[Fact]
	public void DomainEntities_ShouldInherit_BaseEntity()
	{
		Classes().That().Are(GeneratedArchitecture.EntityTypes)
			.Should().BeAssignableTo(typeof(BaseEntity))
			.Because("IBaseEntity implementations are persisted domain entities (owned types are excluded)")
			.Check(GeneratedArchitecture.Architecture);
	}

	[Fact]
	public void EntityDtos_ShouldInherit_DTOBaseEntity()
	{
		Classes().That().Are(GeneratedArchitecture.EntityDtoTypes)
			.Should().BeAssignableTo(typeof(DTOBaseEntity))
			.Check(GeneratedArchitecture.Architecture);
	}

	[Fact]
	public void EntityServices_ShouldInherit_BaseEntityService()
	{
		Classes().That().Are(GeneratedArchitecture.EntityServices)
			.Should().BeAssignableTo(typeof(BaseEntityService<,,>))
			.Check(GeneratedArchitecture.Architecture);
	}

	[Fact]
	public void EntityRepositories_ShouldInherit_BaseEntityRepository()
	{
		Classes().That().Are(GeneratedArchitecture.EntityRepositories)
			.Should().BeAssignableTo(typeof(BaseEntityRepository<,,>))
			.Check(GeneratedArchitecture.Architecture);
	}

	[Fact]
	public void ApiCarterModules_ShouldInherit_BaseEndpoint()
	{
		ReflectionAssembly[] apiHosts = [.. GeneratedArchitecture.HostAssemblies
			.Where(static assembly => assembly.GetName().Name?.StartsWith("Api", StringComparison.Ordinal) == true)];
		if (apiHosts.Length == 0)
			return;

		Classes().That()
			.ImplementInterface(typeof(ICarterModule))
			.And().ResideInAssembly(apiHosts[0], [.. apiHosts.Skip(1)])
			.Should().BeAssignableTo(typeof(BaseEndpoint))
			.Because("Api* Carter modules use GenericEndpoint via BaseEndpoint (FeatureToggle demo is excluded)")
			.Check(GeneratedArchitecture.Architecture);
	}

	[Fact]
	public void GeneratedCrudEndpoints_ShouldInherit_BaseEntityEndpoint()
	{
		Classes().That()
			.ImplementInterface(typeof(ICarterModule))
			.And().AreAssignableTo(typeof(BaseEntityEndpoint<,,>))
			.Should().HaveNameEndingWith("Endpoint")
			.WithoutRequiringPositiveResults()
			.Check(GeneratedArchitecture.Architecture);
	}
}
