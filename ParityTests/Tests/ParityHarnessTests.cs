using System;
using System.IO;
using System.Linq;
using ParityTests.Checks;
using ParityTests.Fixtures;
using Xunit;

namespace ParityTests.Tests;

/// <summary>
/// Expected-state tests for the SPA-74 parity harness. These assert the
/// parity outcomes the harness is supposed to report: GH-A1 and AR-A2 expose
/// known importer gaps (FAIL) and CM-C1 pins the #1080 fix (PASS).
/// </summary>
public class ParityHarnessTests {
	private static FixtureStore RealStore() => FixtureStore.FromEnvironment();

	// A store that can never resolve install fixtures, regardless of env.
	private static FixtureStore UnavailableStore() =>
		new(null, Path.Combine(Path.GetTempPath(), "openciv3-parity-unavailable"), simulateCi: true);

	[Fact]
	public void Manifest_LoadsWithSha256PinnedFixtures() {
		FixtureStore store = UnavailableStore();
		var manifest = store.Manifest;

		Assert.Equal(36, manifest.Fixtures.Count);
		Assert.Equal(6, store.SavFixtures().Count());
		Assert.Equal(28, store.ScenarioBiqFixtures().Count());
		Assert.Equal(29, store.AllBiqFixtures().Count()); // conquests.biq + 28 scenarios
		Assert.All(manifest.Fixtures, f => Assert.Matches("^[0-9a-f]{64}$", f.Sha256));
		Assert.Contains(manifest.Fixtures, f => f.Source == "url" && f.Id == "issue-1080-openciv3test1");
	}

	[Fact]
	public void FixtureStore_Unavailable_WithoutInstallOrUnderCi() {
		Assert.False(UnavailableStore().IsAvailable);
		Assert.False(new FixtureStore(null, Path.GetTempPath()).IsAvailable);
	}

	[Fact]
	public void InstallBackedChecks_SkipCleanly_WithoutCiv3() {
		FixtureStore store = UnavailableStore();
		foreach (IParityCheck check in new IParityCheck[] { new GoodyHutImportCheck(), new ArmyRulesImportCheck() }) {
			CheckResult result = check.Run(store);
			Assert.Equal("SKIP", result.Status);
			Assert.Equal(check.Id, result.Id);
		}
	}

	[SkippableFact]
	public void GH_A1_ReportsRawVsImportedHutCounts() {
		FixtureStore store = RealStore();
		Skip.If(!store.IsAvailable, "No Civ3 install / CIV3_HOME (or CI set).");

		CheckResult result = new GoodyHutImportCheck().Run(store);

		// The importer does not import goody huts today, so the check must
		// report FAIL and carry raw-vs-imported counts for all 34 fixtures.
		Assert.Equal("FAIL", result.Status);
		Assert.Equal(34, result.Fixtures.Count);
		Assert.All(result.Fixtures, f => {
			Assert.NotNull(f.RawValue);
			Assert.NotNull(f.ImportedValue);
		});
		Assert.True(result.Fixtures.Sum(f => f.RawValue.Value) > 0);
		Assert.True(result.Fixtures.Any(f => f.RawValue.Value != f.ImportedValue.Value));
	}

	[SkippableFact]
	public void AR_A2_ReportsMissingArmyRuleImports() {
		FixtureStore store = RealStore();
		Skip.If(!store.IsAvailable, "No Civ3 install / CIV3_HOME (or CI set).");

		CheckResult result = new ArmyRulesImportCheck().Run(store);

		Assert.Equal("FAIL", result.Status);
		Assert.True(result.Fixtures.Count > 0);
	}

	[SkippableFact]
	public void CM_C1_ReproducesIssue1080() {
		FixtureStore store = RealStore();

		CheckResult result = new CommerceSliderCheck().Run(store);
		Skip.If(result.Status == "SKIP", result.Summary);

		Assert.Equal("PASS", result.Status);
	}
}
