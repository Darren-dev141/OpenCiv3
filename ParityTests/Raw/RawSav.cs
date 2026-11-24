using System;
using System.IO;
using System.Linq;
using QueryCiv3;

namespace ParityTests.Raw;

/// <summary>
/// QueryCiv3-only "ground truth" views over original Civ3 files. These read the
/// raw bytes directly and are independent of the C7Engine importer, so they can
/// be compared against what ImportCiv3 produces.
/// </summary>
public static class RawSav {
	/// <summary>
	/// Number of tiles with the goody-hut flag in a .SAV file.
	/// </summary>
	public static int GoodyHutCountSav(string savPath, byte[] defaultBiqBytes) {
		SavData sav = new(Util.ReadFile(savPath), defaultBiqBytes);
		return sav.Tile.Count(t => t.GoodyHut);
	}

	/// <summary>
	/// Number of tiles with the goody-hut flag in a .BIQ file.
	/// </summary>
	public static int? GoodyHutCountBiq(string biqPath) {
		BiqData biq = BiqData.LoadFile(biqPath);
		if (biq.Wmap == null || biq.Wmap.Length == 0) return null;
		return biq.Tile.Count(t => t.GoodyHut);
	}

	public class RuleSnapshot {
		public int CitiesNeededToSupportAnArmy;
		public int BuildArmyUnit;
		public int BattleCreatedUnit;
	}

	/// <summary>
	/// RULE-section army fields from a BIQ (RULE[0], matching ImportRules).
	/// </summary>
	public static RuleSnapshot RuleSnapshotFromBiq(string biqPath) {
		BiqData biq = BiqData.LoadFile(biqPath);
		return RuleSnapshotFromBiq(biq);
	}

	public static RuleSnapshot RuleSnapshotFromBiq(BiqData biq) {
		if (biq.Rule == null || biq.Rule.Length == 0) return null;
		QueryCiv3.Biq.RULE rule = biq.Rule[0];
		return new RuleSnapshot {
			CitiesNeededToSupportAnArmy = rule.CitiesNeededToSupportAnArmy,
			BuildArmyUnit = rule.BuildArmyUnit,
			BattleCreatedUnit = rule.BattleCreatedUnit,
		};
	}

	public class BuildingFlagSnapshot {
		public string Name;
		public bool AllowsBuildArmy;
		public bool RequiresVictoriousArmy;
		public bool IncreasedArmyValue;
	}

	/// <summary>
	/// BLDG army-related flags for every building in a BIQ.
	/// </summary>
	public static System.Collections.Generic.List<BuildingFlagSnapshot> BuildingFlagsFromBiq(string biqPath) {
		BiqData biq = BiqData.LoadFile(biqPath);
		return BuildingFlagsFromBiq(biq);
	}

	public static System.Collections.Generic.List<BuildingFlagSnapshot> BuildingFlagsFromBiq(BiqData biq) {
		var result = new System.Collections.Generic.List<BuildingFlagSnapshot>();
		if (biq.Bldg == null) return result;
		foreach (QueryCiv3.Biq.BLDG b in biq.Bldg) {
			result.Add(new BuildingFlagSnapshot {
				Name = b.Name,
				AllowsBuildArmy = b.AllowsBuildArmy,
				RequiresVictoriousArmy = b.RequiresVictoriousArmy,
				IncreasedArmyValue = b.IncreasedArmyValue,
			});
		}
		return result;
	}
}
