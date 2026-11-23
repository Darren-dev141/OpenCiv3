using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ParityTests.Fixtures;
using ParityTests.Raw;

namespace ParityTests.Checks;

/// <summary>
/// GH-A1 (kind A, import fidelity): raw TILE.GoodyHut / BIQ GoodyHut tile count
/// vs the number of imported tiles carrying the goodyHut overlay, for every
/// install SAV in Conquests\Saves and all 28 Conquest + MP scenario BIQs.
/// Matrix rows SPA-6 / SPA-18 / SPA-84, issue #934.
/// </summary>
public class GoodyHutImportCheck : IParityCheck {
	public string Id => "GH-A1";
	public string MatrixRow => "SPA-6/SPA-18/SPA-84";
	public string SpaIssue => "#934";
	public CheckKind Kind => CheckKind.A;
	public string Reference => "QueryCiv3 raw TILE.GoodyHut flag vs C7Engine ImportCiv3 output; install files via CIV3_HOME (manifest sha256)";
	public string Tolerance => "exact: imported goodyHut overlay count must equal raw GoodyHut tile count per fixture";

	public CheckResult Run(FixtureStore store) {
		throw new NotImplementedException();
	}
}
