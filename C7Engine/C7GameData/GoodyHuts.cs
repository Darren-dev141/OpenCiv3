using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using Serilog;

namespace C7GameData {
	// A unit entering a minor tribe's village (goody hut, issue #934).
	// Adapted from GBiebs/OpenCiv3 feature/civ3-complete (GoodyHuts.cs),
	// reduced to the first increment: the collect framework and two effects,
	// Gold and Nothing. The other outcomes of the odds table (city, tech,
	// settler, map, mercenaries, barbarians) are listed but not yet applied;
	// they drop out and the possible ones share the chance, as in the source.
	// Odds come from the rules (Rules.GoodyHutOdds); see GoodyHutOddsRow for
	// their provenance.
	public static class GoodyHuts {
		private static ILogger log = Log.ForContext(typeof(GoodyHuts));

		public enum Outcome { City, Tech, Gold, Settler, Map, Mercenaries, Nothing, Barbarians }

		// The rules' row for the game's difficulty: the one named after it,
		// else the one at its place among the difficulties (Monarch's if not found).
		public static GoodyHutOdds Odds(GameData gameData, Player player) {
			bool expansionist = player.civilization?.traits.Contains(Civilization.Trait.Expansionist) == true;
			List<GoodyHutOdds> table = expansionist ? gameData.rules.ExpansionistGoodyHutOdds : gameData.rules.GoodyHutOdds;
			if (table == null || table.Count == 0) {
				return null;
			}
			GoodyHutOdds named = table.Find(row => row.difficulty == gameData.gameDifficulty?.Name);
			if (named != null) {
				return named;
			}
			int index = gameData.difficulties.FindIndex(d => d.id == gameData.gameDifficulty?.id);
			return table[Math.Clamp(index < 0 ? 3 : index, 0, table.Count - 1)];
		}

		private static double Chance(GoodyHutOdds odds, Outcome outcome) {
			return outcome switch {
				Outcome.Gold => odds.gold,
				Outcome.Nothing => odds.nothing,
				_ => 0, // not implemented yet
			};
		}

		public static bool IsPossible(Outcome outcome, Tile tile) {
			return outcome switch {
				Outcome.Gold => tile.Resource == null || tile.Resource == Resource.NONE,
				Outcome.Nothing => true,
				_ => false,
			};
		}

		// The chance of each implemented, possible outcome; they add up to 1.
		// With none possible, the village is simply deserted.
		public static Dictionary<Outcome, double> OutcomeChances(GameData gameData, MapUnit unit, Tile tile) {
			GoodyHutOdds odds = Odds(gameData, unit.owner);
			Dictionary<Outcome, double> chances = Enum.GetValues<Outcome>()
				.Where(o => odds != null && Chance(odds, o) > 0 && IsPossible(o, tile))
				.ToDictionary(o => o, o => Chance(odds, o));
			double total = chances.Values.Sum();
			if (total <= 0) {
				return new() { [Outcome.Nothing] = 1 };
			}
			return chances.ToDictionary(kv => kv.Key, kv => kv.Value / total);
		}

		private static Random Rng() => GameData.rng ?? new Random();

		private static string TribeName(GameData gameData) {
			List<string> names = gameData.players.Find(p => p.isBarbarians)?.civilization?.cityNames;
			return names == null || names.Count == 0 ? "local" : names[Rng().Next(names.Count)];
		}

		// The unit enters the village: it's gone, and something happens.
		public static Outcome Enter(GameData gameData, MapUnit unit, Tile tile) {
			tile.hasGoodyHut = false;
			Outcome outcome = Outcome.Nothing;
			double roll = Rng().NextDouble();
			foreach ((Outcome o, double chance) in OutcomeChances(gameData, unit, tile)) {
				outcome = o;
				roll -= chance;
				if (roll < 0) {
					break;
				}
			}
			string text = Apply(outcome, gameData, unit, tile, TribeName(gameData));
			log.Information($"{unit.owner} entered a goody hut at {tile}: {text}");
			if (unit.owner.isHuman) {
				new MsgGoodyHut(unit.owner.id, outcome, text, tile).send();
			}
			return outcome;
		}

		public static string Apply(Outcome outcome, GameData gameData, MapUnit unit, Tile tile, string tribe) {
			switch (outcome) {
				case Outcome.Gold: {
						List<int> amounts = gameData.rules.GoodyHutGold;
						int gold = amounts.Count == 0 ? 0 : amounts[Rng().Next(amounts.Count)];
						unit.owner.gold += gold;
						return $"We got {gold} gold from the {tribe} tribe's village.";
					}
				default:
					return $"This {tribe} village is deserted.";
			}
		}
	}
}
