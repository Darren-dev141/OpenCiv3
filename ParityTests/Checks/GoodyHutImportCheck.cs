using System;
using System.Linq;
using C7GameData.Save;
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
		CheckResult result = this.BaseResult();
		if (!store.IsAvailable) {
			return CheckResult.Skip(this, "CIV3_HOME not set or CI environment detected; install fixtures unavailable.");
		}

		byte[] defaultBiq = ImportHarness.DefaultBiqBytes(store);
		var fixtures = store.SavFixtures().Concat(store.ScenarioBiqFixtures()).ToList();

		foreach (Fixture fixture in fixtures) {
			FixtureResult row = new() { FixtureId = fixture.Id };
			ResolvedFixture resolved = store.Resolve(fixture);
			if (resolved.Error != null) {
				row.Status = "SKIP";
				row.Detail = resolved.Error;
				result.Fixtures.Add(row);
				continue;
			}

			try {
				int? raw = fixture.Kind == "sav"
					? RawSav.GoodyHutCountSav(resolved.LocalPath, defaultBiq)
					: RawSav.GoodyHutCountBiq(resolved.LocalPath);
				if (!raw.HasValue) {
					row.Status = "SKIP";
					row.Detail = "BIQ has no map, so there are no tiles to compare";
					result.Fixtures.Add(row);
					continue;
				}
				row.RawValue = raw.Value;
				SaveGame save = ImportHarness.ImportFixture(store, resolved);
				int imported = ImportHarness.CountGoodyHutTiles(save);

				row.ImportedValue = imported;
				row.Status = raw == imported ? "PASS" : "FAIL";
				row.Detail = $"raw GoodyHut tiles={raw}, imported goodyHut overlays={imported}";
			} catch (Exception ex) {
				row.Status = "ERROR";
				row.Detail = ex.GetType().Name + ": " + ex.Message;
			}
			result.Fixtures.Add(row);
		}

		return Finalize(result);
	}

	private CheckResult Finalize(CheckResult result) {
		int pass = result.Fixtures.Count(f => f.Status == "PASS");
		int fail = result.Fixtures.Count(f => f.Status == "FAIL");
		int error = result.Fixtures.Count(f => f.Status == "ERROR");
		int skip = result.Fixtures.Count(f => f.Status == "SKIP");

		result.Status = fail + error > 0 ? "FAIL" : pass > 0 ? "PASS" : "SKIP";
		result.Summary = $"{pass} fixtures match, {fail} diverge, {error} errors, {skip} skipped " +
			$"({result.Fixtures.Count} total: 6 SAVs + 28 scenario BIQs)";
		if (fail + error > 0) {
			FixtureResult first = result.Fixtures.First(f => f.Status is "FAIL" or "ERROR");
			result.Failure = $"first divergence at {first.FixtureId}: {first.Detail}";
		}
		return result;
	}
}
