using Soenneker.Utils.AutoBogus.Context;
using Soenneker.Utils.AutoBogus.Generators;

namespace Core.Tests.UnitTests.Setup;

public class IDOverride : AutoFakerGeneratorOverride
{
	public override bool CanOverride(AutoFakerContext context)
	{
		return context.GenerateName?.Equals("ID", StringComparison.OrdinalIgnoreCase) == true
			&& (context.GenerateType == typeof(int) || context.GenerateType == typeof(long));
	}

	public override void Generate(AutoFakerOverrideContext context) => context.Instance = new Random().Next(1, 1000);
}

public class YearOverride : AutoFakerGeneratorOverride
{
	public override bool CanOverride(AutoFakerContext context)
	{
		return context.GenerateName?.Equals("Year", StringComparison.OrdinalIgnoreCase) == true
			&& context.GenerateType == typeof(int);
	}

	public override void Generate(AutoFakerOverrideContext context)
		=> context.Instance = new Random().Next(1900, DateTime.Now.Year);
}