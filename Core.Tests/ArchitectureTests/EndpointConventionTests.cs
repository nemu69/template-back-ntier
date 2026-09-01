using ArchUnitNET.xUnit;
using Carter;
using Core.Shared.Endpoints.Kernel;
using Core.Shared.Repositories.Kernel.Interfaces;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using ReflectionAssembly = System.Reflection.Assembly;

namespace Core.Tests.ArchitectureTests;

public class EndpointConventionTests
{
	[Fact]
	public void CarterModules_ShouldLiveIn_EndpointsNamespace()
	{
		Classes().That().Are(GeneratedArchitecture.CarterModules)
			.Should().HaveFullNameContaining(".Endpoints.")
			.WithoutRequiringPositiveResults()
			.Check(GeneratedArchitecture.Architecture);
	}

	[Fact]
	public void CarterModules_ShouldNotDependOn_Repositories()
	{
		Classes().That().Are(GeneratedArchitecture.CarterModules)
			.Should().NotDependOnAny(Classes().That().ImplementInterface(typeof(IBaseEntityRepository<,>)))
			.Because("endpoints inject services, not repositories")
			.WithoutRequiringPositiveResults()
			.Check(GeneratedArchitecture.Architecture);
	}

	[Fact]
	public void DomainCrudEndpoints_ShouldUse_BaseEntityEndpoint()
	{
		Classes().That()
			.HaveNameEndingWith("Endpoint")
			.And().AreAssignableTo(typeof(BaseEntityEndpoint<,,>))
			.Should().ImplementInterface(typeof(ICarterModule))
			.WithoutRequiringPositiveResults()
			.Check(GeneratedArchitecture.Architecture);
	}

	[Fact]
	public void CustomApiEndpoints_ShouldUse_BaseEndpoint()
	{
		ReflectionAssembly[] apiHosts = [.. GeneratedArchitecture.HostAssemblies
			.Where(static assembly => assembly.GetName().Name?.StartsWith("Api", StringComparison.Ordinal) == true)];
		if (apiHosts.Length == 0)
			return;

		Classes().That()
			.HaveNameEndingWith("Endpoint")
			.And().ResideInAssembly(apiHosts[0], [.. apiHosts.Skip(1)])
			.And().AreNotAssignableTo(typeof(BaseEntityEndpoint<,,>))
			.Should().BeAssignableTo(typeof(BaseEndpoint))
			.Because("non-CRUD Api* endpoints (FileService, Scheduler, …) still wrap calls with GenericEndpoint")
			.WithoutRequiringPositiveResults()
			.Check(GeneratedArchitecture.Architecture);
	}
}
