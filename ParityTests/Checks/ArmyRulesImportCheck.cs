using System;
using ParityTests.Fixtures;

namespace ParityTests.Checks;

/// <summary>
/// AR-A2 (kind A, import fidelity): RULE.CitiesNeededToSupportAnArmy,
/// RULE.BuildArmyUnit and the BLDG flags AllowsBuildArmy /
/// RequiresVictoriousArmy / IncreasedArmyValue must survive BIQ -> SaveGame
/// import. Checked against conquests.biq and every scenario BIQ.
/// Matrix rows SPA-7 / SPA-21 / SPA-66 / SPA-67 / SPA-85, issue #1011.
/// </summary>
public class ArmyRulesImportCheck : IParityCheck {
	public string Id => "AR-A2";
	public string MatrixRow => "SPA-7/SPA-21/SPA-66/SPA-67/SPA-85";
	public string SpaIssue => "#1011";
	public CheckKind Kind => CheckKind.A;
	public string Reference => "QueryCiv3 BiqSections RULE/BLDG fields vs C7Engine ImportCiv3 output; install files via CIV3_HOME (manifest sha256)";
	public string Tolerance => "exact: every RULE army field and BLDG army flag must be represented in the imported SaveGame";

	public CheckResult Run(FixtureStore store) {
		throw new NotImplementedException();
	}
}
