using System.Collections.Generic;

namespace ParityTests.Checks;

/// <summary>
/// Registry of all parity checks run by the harness.
/// </summary>
public static class ParityChecks {
	public static IReadOnlyList<IParityCheck> All { get; } = new IParityCheck[] {
		new GoodyHutImportCheck(),
		new ArmyRulesImportCheck(),
		new CommerceSliderCheck(),
	};
}
