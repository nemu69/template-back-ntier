using ArchUnitNET.xUnit;
using Core.Shared.Data;
using Core.Shared.Repositories.Kernel.Interfaces;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using ReflectionAssembly = System.Reflection.Assembly;

namespace Core.Tests.ArchitectureTests;

/// <summary>
/// Assembly and namespace dependency direction for the generated N-tier template.
/// </summary>
public class LayerDependencyTests
{
	[Fact]
	public void Core_ShouldNotDependOn_PresentationHosts()
	{
		Types().That().Are(GeneratedArchitecture.CoreLayer)
			.Should().NotDependOnAny(GeneratedArchitecture.PresentationLayer)
			.Because("Core must stay independent of Api / host projects")
			.WithoutRequiringPositiveResults()
			.Check(GeneratedArchitecture.Architecture);
	}

	[Fact]
	public void PresentationHosts_ShouldNotDependOn_EachOther()
	{
		ReflectionAssembly[] hosts = GeneratedArchitecture.HostAssemblies;
		for (int i = 0; i < hosts.Length; i++)
		{
			ReflectionAssembly source = hosts[i];
			ReflectionAssembly[] others = [.. hosts.Where((_, index) => index != i)];
			if (others.Length == 0)
				continue;

			Types().That().ResideInAssembly(source)
				.Should().NotDependOnAny(Types().That().ResideInAssembly(others[0], [.. others.Skip(1)]))
				.Because("each host is a vertical API slice that only talks to Core")
				.Check(GeneratedArchitecture.Architecture);
		}
	}

	[Fact]
	public void EntityModels_ShouldNotDependOn_ServicesOrRepositories()
	{
		Classes().That().Are(GeneratedArchitecture.EntityTypes)
			.Should().NotDependOnAny(GeneratedArchitecture.EntityServices)
			.Because("entities must not call services")
			.Check(GeneratedArchitecture.Architecture);
		Classes().That().Are(GeneratedArchitecture.EntityTypes)
			.Should().NotDependOnAny(GeneratedArchitecture.EntityRepositories)
			.Because("entities must not call repositories")
			.Check(GeneratedArchitecture.Architecture);
	}

	[Fact]
	public void EntityDtos_ShouldNotDependOn_ServicesOrRepositories()
	{
		Classes().That().Are(GeneratedArchitecture.EntityDtoTypes)
			.Should().NotDependOnAny(GeneratedArchitecture.EntityServices)
			.Check(GeneratedArchitecture.Architecture);
		Classes().That().Are(GeneratedArchitecture.EntityDtoTypes)
			.Should().NotDependOnAny(GeneratedArchitecture.EntityRepositories)
			.Check(GeneratedArchitecture.Architecture);
	}

	[Fact]
	public void EntityServices_ShouldNotDependOn_CarterModules()
	{
		Classes().That().Are(GeneratedArchitecture.EntityServices)
			.Should().NotDependOnAny(GeneratedArchitecture.CarterModules)
			.Because("business logic must not depend on HTTP endpoints")
			.WithoutRequiringPositiveResults()
			.Check(GeneratedArchitecture.Architecture);
	}

	[Fact]
	public void EntityRepositories_ShouldNotDependOn_ServicesOrEndpoints()
	{
		Classes().That().Are(GeneratedArchitecture.EntityRepositories)
			.Should().NotDependOnAny(GeneratedArchitecture.EntityServices)
			.Check(GeneratedArchitecture.Architecture);
		Classes().That().Are(GeneratedArchitecture.EntityRepositories)
			.Should().NotDependOnAny(GeneratedArchitecture.CarterModules)
			.WithoutRequiringPositiveResults()
			.Check(GeneratedArchitecture.Architecture);
	}

	[Fact]
	public void Presentation_ShouldNotDependOn_RepositoriesOrDbContext()
	{
		Types().That().Are(GeneratedArchitecture.PresentationLayer)
			.Should().NotDependOnAny(Classes().That().ImplementInterface(typeof(IBaseEntityRepository<,>)))
			.Because("hosts call services, not repositories")
			.WithoutRequiringPositiveResults()
			.Check(GeneratedArchitecture.Architecture);
		Types().That().Are(GeneratedArchitecture.PresentationLayer)
			.Should().NotDependOnAny(typeof(AppDbContext))
			.Because("hosts must not use the EF context directly")
			.WithoutRequiringPositiveResults()
			.Check(GeneratedArchitecture.Architecture);
	}
}
