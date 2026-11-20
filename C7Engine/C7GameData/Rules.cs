using System.Collections.Generic;

namespace C7GameData {
	public class Rules {
		public int MaximumResearchTime;
		public int MinimumResearchTime;
		public int ShieldValueInGold;
		public int ForestValueInShields;
		public int CitizenValueInShields;
		public int TurnPenaltyForEachHurrySacrifice;
		public int MaximumLevel1CitySize;
		public int MaximumLevel2CitySize;
		public int FoodNeededToGrowForLevel1Cities = 20;
		public int FoodNeededToGrowForLevel2Cities = 40;
		public int FoodNeededToGrowForLevel3Cities = 60;
		public int CapitalNeighborDistance = 8; // for capital relocation calculations
		public float BuildingDiscountForCivTraits = .5f;
		public string StartUnitType1;
		public string StartUnitType2;
		public string ScoutUnitType;
		public int MaxRankOfWorkableTiles;
		public int MaxRankOfBarbarianCampTiles;
		public int BarbarianCampGold = 25; // gold for dispersing a barbarian camp; not in Civ3's BIQ, so this is also the default for Civ3 saves and older OpenCiv3 saves
		public int DefaultDealDuration;
		public float TreasuryInterestRate = .05f;
		public int MaxInterest = 50;
		public int ShieldCostPerGold;
		public float ShieldRateForDisbanding; // per cent
		public bool AllowLesserUnitProduction; // for example, allow building a Spearman/Pikeman when we can build a Musketman (simultaneously)
		public int RadarTileVisibility; // how many tiles, a unit with the Radar ability, can see ahead

		// Goody huts (GoodyHuts, issue #934). Civ3's gold amounts aren't
		// documented; these follow players' reports. Not in Civ3's BIQ.
		public List<int> GoodyHutGold = [25, 50];
		public List<GoodyHutOdds> GoodyHutOdds = GoodyHutOddsRow.Standard();
		public List<GoodyHutOdds> ExpansionistGoodyHutOdds = GoodyHutOddsRow.Expansionist();
	}

	// One difficulty's goody hut chances, in percent. They needn't add up to
	// 100: the possible outcomes share the total (see GoodyHuts).
	public class GoodyHutOdds {
		public string difficulty;
		public double city;
		public double tech;
		public double gold;
		public double settler;
		public double map;
		public double mercenaries;
		public double nothing;
		public double barbarians;
	}

	// Table adapted from GBiebs/OpenCiv3 feature/civ3-complete (Rules.cs,
	// GoodyHutOddsRow), itself from CivFanatics' "Probabilities of goody huts
	// (C3C)". UNVERIFIED against the article.
	//
	// KNOWN LIMITATION: with only Gold implemented every hut pays gold:
	// nothing=0 in every Standard row, so "nothing" is never rolled, and the
	// barbarian chances drop out. Expansionist rows are an inference (next
	// easier level, no barbarians), and several rows sum to 75, not 100.
	public static class GoodyHutOddsRow {
		private static readonly string[] Difficulties = ["Chieftain", "Warlord", "Regent", "Monarch", "Emperor", "Demigod", "Deity", "Creator"];

		private static List<GoodyHutOdds> Table(double[][] rows) {
			List<GoodyHutOdds> table = [];
			for (int i = 0; i < rows.Length; ++i) {
				double[] r = rows[i];
				table.Add(new() {
					difficulty = Difficulties[i],
					city = r[0], tech = r[1], gold = r[2], settler = r[3],
					map = r[4], mercenaries = r[5], nothing = r[6], barbarians = r[7],
				});
			}
			return table;
		}

		// city, tech, gold, settler, map, mercenaries, nothing, barbarians.
		public static List<GoodyHutOdds> Standard() => Table([
			[5, 35, 15, 15, 10, 10, 0, 10],
			[10, 25, 10, 10, 10, 10, 0, 25],
			[5, 20, 10, 10, 10, 10, 0, 35],
			[5, 15, 10, 10, 10, 10, 0, 40],
			[5, 10, 5, 5, 5, 5, 0, 65],
			[0, 5, 5, 5, 5, 5, 0, 75],
			[0, 0, 5, 0, 5, 5, 0, 85],
			[0, 0, 0, 0, 0, 0, 0, 90],
		]);

		// An Expansionist civ: the next easier level's, with no barbarians.
		public static List<GoodyHutOdds> Expansionist() => Table([
			[10, 35, 15, 15, 10, 10, 5, 0],
			[10, 35, 15, 15, 10, 10, 5, 0],
			[10, 25, 10, 10, 10, 10, 0, 0],
			[5, 20, 10, 10, 10, 10, 0, 0],
			[5, 15, 10, 10, 10, 10, 0, 0],
			[5, 10, 5, 5, 5, 5, 0, 0],
			[0, 5, 5, 5, 5, 5, 0, 0],
			[0, 0, 5, 0, 5, 5, 0, 0],
		]);
	}
}
