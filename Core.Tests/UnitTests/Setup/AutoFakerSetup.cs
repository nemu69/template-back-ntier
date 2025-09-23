// <summary>es a new instance of the <see cref="AutoFakerFixture"/> class.
using Soenneker.Utils.AutoBogus;
using Soenneker.Utils.AutoBogus.Config;

namespace Core.Tests.UnitTests.Setup;

/// <summary>
/// 
/// </summary>
/// Configures AutoBogus with global settings, including custom conventions,
/// overrides, and repeat count.
/// </summary>
public static class AutoFakerSetup
{
	static AutoFakerSetup()
	{
	}

	public static AutoFaker Config()
	{
		AutoFakerConfig optionalConfig = new() {
			RepeatCount = 1,
			Overrides =
				[
					new IDOverride(),
					new YearOverride()
				],
			TreeDepth = 1,
		};

		return new(optionalConfig);
	}
}