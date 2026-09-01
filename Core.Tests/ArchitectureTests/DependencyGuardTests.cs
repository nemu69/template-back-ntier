using ArchUnitNET.xUnit;
using Core.Shared.Data;
using Microsoft.EntityFrameworkCore;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Core.Tests.ArchitectureTests;

/// <summary>
/// Guards against infrastructure types leaking into the wrong N-tier slice.
/// </summary>
public class DependencyGuardTests
{
	[Fact]
	public void EntityModels_ShouldNotDependOn_CarterOrAspNetCoreHttp()
	{
		Classes().That().Are(GeneratedArchitecture.EntityTypes)
			.Should().NotDependOnAnyTypesThat().ResideInNamespace("Carter")
			.Because("domain entities must not know about HTTP modules")
			.Check(GeneratedArchitecture.Architecture);
	}

	[Fact]
	public void EntityServices_ShouldNotDependOn_EntityFrameworkOrCarter()
	{
		Classes().That().Are(GeneratedArchitecture.EntityServices)
			.Should().NotDependOnAny(typeof(DbContext))
			.Because("services go through repositories / UoW, not DbContext")
			.Check(GeneratedArchitecture.Architecture);
		Classes().That().Are(GeneratedArchitecture.EntityServices)
			.Should().NotDependOnAnyTypesThat().ResideInNamespace("Carter")
			.Check(GeneratedArchitecture.Architecture);
	}

	[Fact]
	public void EntityRepositories_ShouldNotDependOn_Carter()
	{
		Classes().That().Are(GeneratedArchitecture.EntityRepositories)
			.Should().NotDependOnAnyTypesThat().ResideInNamespace("Carter")
			.Check(GeneratedArchitecture.Architecture);
	}

	[Fact]
	public void Presentation_ShouldNotDependOn_EntityFramework()
	{
		Types().That().Are(GeneratedArchitecture.PresentationLayer)
			.Should().NotDependOnAny(typeof(DbContext))
			.Because("EF stays in Core (repositories, UoW, DbContext)")
			.WithoutRequiringPositiveResults()
			.Check(GeneratedArchitecture.Architecture);
	}

	[Fact]
	public void EntityServices_ShouldNotDependOn_ConcreteRepositories()
	{
		Classes().That().Are(GeneratedArchitecture.EntityServices)
			.Should().NotDependOnAny(
				Classes().That()
					.HaveNameEndingWith("Repository")
					.And().HaveFullNameContaining("Core.Entities."))
			.Because("services resolve repositories from IAppUOW, never new up concrete repositories")
			.Check(GeneratedArchitecture.Architecture);
	}

	[Fact]
	public void CarterModules_ShouldNotDependOn_AppDbContext()
	{
		Classes().That().Are(GeneratedArchitecture.CarterModules)
			.Should().NotDependOnAny(typeof(AppDbContext))
			.WithoutRequiringPositiveResults()
			.Check(GeneratedArchitecture.Architecture);
	}
}
