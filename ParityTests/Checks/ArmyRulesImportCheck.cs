using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using C7GameData;
using C7GameData.Save;
using ParityTests.Fixtures;
using ParityTests.Raw;
using QueryCiv3;

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

	private static readonly (string RawFlag, SaveBuilding.WonderFlag ImportedFlag)[] BldgFlagMap = {
		("AllowsBuildArmy", SaveBuilding.WonderFlag.AllowsBuildArmy),
		("RequiresVictoriousArmy", SaveBuilding.WonderFlag.RequiresVictoriousArmy),
		("IncreasedArmyValue", SaveBuilding.WonderFlag.IncreasedArmyValue),
	};

	public CheckResult Run(FixtureStore store) {
		CheckResult result = this.BaseResult();
		if (!store.IsAvailable) {
			return CheckResult.Skip(this, "CIV3_HOME not set or CI environment detected; install fixtures unavailable.");
		}

		foreach (Fixture fixture in store.AllBiqFixtures()) {
			FixtureResult row = new() { FixtureId = fixture.Id };
			ResolvedFixture resolved = store.Resolve(fixture);
			if (resolved.Error != null) {
				row.Status = "SKIP";
				row.Detail = resolved.Error;
				result.Fixtures.Add(row);
				continue;
			}

			try {
				Evaluate(store, resolved, row);
			} catch (Exception ex) {
				row.Status = "ERROR";
				row.Detail = ex.GetType().Name + ": " + ex.Message;
			}
			result.Fixtures.Add(row);
		}

		int pass = result.Fixtures.Count(f => f.Status == "PASS");
		int fail = result.Fixtures.Count(f => f.Status == "FAIL");
		int error = result.Fixtures.Count(f => f.Status == "ERROR");
		result.Status = fail + error > 0 ? "FAIL" : pass > 0 ? "PASS" : "SKIP";
		result.Summary = $"{pass} BIQs fully import army rules, {fail} diverge, {error} errors ({result.Fixtures.Count} total)";
		if (fail + error > 0) {
			FixtureResult first = result.Fixtures.First(f => f.Status is "FAIL" or "ERROR");
			result.Failure = $"first divergence at {first.FixtureId}: {first.Detail}";
		}
		return result;
	}

	private void Evaluate(FixtureStore store, ResolvedFixture resolved, FixtureResult row) {
		string path = resolved.LocalPath;
		BiqData rawBiq = BiqData.LoadFile(path);
		RawSav.RuleSnapshot rule = RawSav.RuleSnapshotFromBiq(rawBiq);
		List<RawSav.BuildingFlagSnapshot> rawBuildings = RawSav.BuildingFlagsFromBiq(rawBiq);
		SaveGame save = ImportHarness.ImportFixture(store, resolved);

		List<string> divergences = new();
		int dataPoints = 0;
		int matched = 0;

		// --- RULE fields ---
		foreach ((string name, int rawValue, Func<Rules, object> getImported) in new (string, int, Func<Rules, object>)[] {
			("CitiesNeededToSupportAnArmy", rule?.CitiesNeededToSupportAnArmy ?? -1, r => GetMember(r, "CitiesNeededToSupportAnArmy")),
			("BuildArmyUnit", rule?.BuildArmyUnit ?? -1, r => GetMember(r, "BuildArmyUnit")),
			("BattleCreatedUnit", rule?.BattleCreatedUnit ?? -1, r => GetMember(r, "BattleCreatedUnit")),
		}) {
			dataPoints++;
			object imported = getImported(save.Rules);
			if (imported == null) {
				divergences.Add($"RULE.{name}: raw={rawValue}, imported=ABSENT");
			} else if (RuleValuesMatch(name, rawValue, imported, rawBiq)) {
				matched++;
			} else {
				divergences.Add($"RULE.{name}: raw={rawValue}, imported={imported}");
			}
		}

		// --- BLDG flags ---
		Dictionary<string, SaveBuilding> importedByName = save.Buildings
			.GroupBy(b => b.name)
			.ToDictionary(g => g.Key, g => g.First());

		foreach (RawSav.BuildingFlagSnapshot rawB in rawBuildings) {
			if (!importedByName.TryGetValue(rawB.Name, out SaveBuilding importedB)) {
				if (rawB.AllowsBuildArmy || rawB.RequiresVictoriousArmy || rawB.IncreasedArmyValue) {
					divergences.Add($"BLDG {rawB.Name}: raw has army flag(s), imported building ABSENT");
					dataPoints += 3;
				}
				continue;
			}
			foreach ((string rawName, SaveBuilding.WonderFlag flag) in BldgFlagMap) {
				bool rawHas = rawName switch {
					"AllowsBuildArmy" => rawB.AllowsBuildArmy,
					"RequiresVictoriousArmy" => rawB.RequiresVictoriousArmy,
					_ => rawB.IncreasedArmyValue,
				};
				dataPoints++;
				bool importedHas = importedB.wonderFlags.Contains(flag);
				if (rawHas == importedHas) {
					matched++;
				} else {
					divergences.Add($"BLDG {rawB.Name}.{rawName}: raw={rawHas}, imported={importedHas}");
				}
			}
		}

		row.RawValue = dataPoints;
		row.ImportedValue = matched;
		row.Status = divergences.Count == 0 ? "PASS" : "FAIL";
		row.Detail = divergences.Count == 0
			? $"all {dataPoints} army-rule data points imported"
			: string.Join("; ", divergences.Take(8)) + (divergences.Count > 8 ? $"; ... +{divergences.Count - 8} more" : "");
	}

	private static object GetMember(Rules rules, string name) {
		const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;
		PropertyInfo prop = typeof(Rules).GetProperty(name, flags);
		if (prop != null) return prop.GetValue(rules);
		FieldInfo field = typeof(Rules).GetField(name, flags);
		return field?.GetValue(rules);
	}

	private static bool RuleValuesMatch(string name, int rawValue, object imported, BiqData rawBiq) {
		if (imported is int i) return i == rawValue;
		if (imported is string s) {
			// Unit-type RULE fields may be imported as prototype names.
			string rawName = rawValue >= 0 && rawBiq.Prto != null && rawValue < rawBiq.Prto.Length
				? rawBiq.Prto[rawValue].Name
				: null;
			return rawName != null && string.Equals(rawName, s, StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}
}
