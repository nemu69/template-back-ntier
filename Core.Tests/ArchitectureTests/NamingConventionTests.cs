using ArchUnitNET.xUnit;
using Core.Shared.Services.Kernel;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Core.Tests.ArchitectureTests;

public class NamingConventionTests
{
	[Fact]
	public void EntityServices_ShouldBeNamed_Service()
	{
		Classes().That().Are(GeneratedArchitecture.EntityServices)
			.And().AreNot(typeof(BaseEntityService<,,>))
			.Should().HaveNameEndingWith("Service")
			.Check(GeneratedArchitecture.Architecture);
	}

	[Fact]
	public void EntityRepositories_ShouldBeNamed_Repository()
	{
		Classes().That().Are(GeneratedArchitecture.EntityRepositories)
			.Should().HaveNameEndingWith("Repository")
			.Check(GeneratedArchitecture.Architecture);
	}

	[Fact]
	public void EntityDtos_ShouldBeNamed_DTO()
	{
		Classes().That().Are(GeneratedArchitecture.EntityDtoTypes)
			.Should().HaveNameStartingWith("DTO")
			.Check(GeneratedArchitecture.Architecture);
	}

	[Fact]
	public void CarterModules_ShouldBeNamed_Endpoint()
	{
		Classes().That().Are(GeneratedArchitecture.CarterModules)
			.Should().HaveNameEndingWith("Endpoint")
			.Because("ICarterModule implementations are HTTP endpoints")
			.WithoutRequiringPositiveResults()
			.Check(GeneratedArchitecture.Architecture);
	}

	[Fact]
	public void SharedExceptions_ShouldBeNamed_Exception()
	{
		Classes().That()
			.ResideInNamespace("Core.Shared.Exceptions")
			.Should().HaveNameEndingWith("Exception")
			.AndShould().BeAssignableTo(typeof(Exception))
			.Check(GeneratedArchitecture.Architecture);
	}

	[Fact]
	public void CoreInterfaces_ShouldStartWith_I()
	{
		Interfaces().That()
			.ResideInAssembly(GeneratedArchitecture.CoreAssembly)
			.Should().HaveNameStartingWith("I")
			.Check(GeneratedArchitecture.Architecture);
	}

	[Fact]
	public void EntityServiceInterfaces_ShouldBeNamed_IEntityService()
	{
		Interfaces().That()
			.HaveFullNameContaining("Core.Entities.")
			.And().HaveNameEndingWith("Service")
			.Should().HaveNameStartingWith("I")
			.Check(GeneratedArchitecture.Architecture);
	}

	[Fact]
	public void EntityRepositoryInterfaces_ShouldBeNamed_IEntityRepository()
	{
		Interfaces().That()
			.HaveFullNameContaining("Core.Entities.")
			.And().HaveNameEndingWith("Repository")
			.Should().HaveNameStartingWith("I")
			.Check(GeneratedArchitecture.Architecture);
	}

	[Fact]
	public void SignalRHubs_ShouldBeNamed_Hub()
	{
		Classes().That()
			.HaveFullNameContaining("Core.Shared.SignalR")
			.And().DoNotHaveName("BaseHub`1")
			.And().DoNotHaveName("UserConnectionManager`1")
			.And().DoNotHaveName("SignalRExtensions")
			.Should().HaveNameEndingWith("Hub")
			.WithoutRequiringPositiveResults()
			.Check(GeneratedArchitecture.Architecture);
	}
}
