using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7Engine.Lua;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Probe-first specification for goody huts / minor tribes (issue #934).
// First increment: (a) huts placed at world creation, deterministic per seed;
// (b) a collect framework: a player unit entering a hut tile removes the hut;
// (c) two effects, GOLD and NOTHING, plus the sourced odds table.
//
// The API under test is called directly, so a rename fails at compile time.
// The shape follows GBiebs/OpenCiv3 feature/civ3-complete 75df1502
// (C7GameData.GoodyHuts, Tile.hasGoodyHut, Rules.GoodyHut*).
//
// Odds source: CivFanatics "Probabilities of goody huts (C3C)", as transcribed
// in GBiebs Rules.cs. UNVERIFIED against the article itself (not reachable
// when the survey ran). Gold amounts and spacing are approximations there.
public class GoodyHutProbeTest : IClassFixture<SaveGameFixture> {
	private readonly SaveGameFixture fixture;
	private readonly C7GameData.GameData gameData;

	public GoodyHutProbeTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		gameData = fixture.saveGame.Clone().ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		EngineStorage.messagesToUI.Clear();
	}

	private static bool HasHut(Tile tile) => tile.hasGoodyHut;

	private static void SetHut(Tile tile, bool value) => tile.hasGoodyHut = value;

	private static string Apply(GoodyHuts.Outcome outcome, C7GameData.GameData game, MapUnit unit, Tile tile) =>
		GoodyHuts.Apply(outcome, game, unit, tile, "test");

	private static List<Tile> Huts(C7GameData.GameData game) => game.map.tiles.Where(HasHut).ToList();

	private static int Distance(Tile a, Tile b) => a.DistanceTo(b);

	// ---- helpers ----

	private static C7GameData.GameData NewWorld(int seed) {
		GameMode.Config config = new("civ3");
		GameMode mode = GameMode.Load(PathUtils.GameModesDir, config);
		SaveGame save = mode.GetSave();
		WorldSize size = new() { width = 60, height = 60, numberOfCivs = 4, distanceBetweenCivs = 8, techRate = 240, optimalNumberOfCities = 20 };
		save.Seed = seed;
		save.BarbarianInfo.barbarianActivity = BarbarianActivity.Roaming;
		WorldCharacteristics wc = new() {
			landform = WorldCharacteristics.Landform.Pangaea,
			oceanCoverage = WorldCharacteristics.OceanCoverage.Percent_70,
			age = WorldCharacteristics.Age.Billion_4,
			climate = WorldCharacteristics.Climate.Normal,
			temperature = WorldCharacteristics.Temperature.Temperate,
			worldSize = size,
		};
		new GameSetup {
			playerCivilization = save.Civilizations.First(c => !c.isBarbarian),
			difficulty = save.Difficulties.First(),
			worldCharacteristics = wc,
			opponents = Enumerable.Repeat(new SelectedOpponent { isRandom = true }, size.numberOfCivs - 1).ToList(),
			victoryConditions = new VictoryConditions(),
		}.Populate(save);
		return save.ToGameData(mode.behaviors);
	}

	// A warrior of the first non-barbarian player and an empty land tile with
	// no city, unit or camp on it.
	private (MapUnit warrior, Tile hut) WarriorAndSpot() {
		Player player = gameData.players.First(p => !p.isBarbarians);
		Tile spot = gameData.map.tiles.First(t => Tile.IsTileValid(t) && t.IsLand() && !t.HasCity()
			&& t.unitsOnTile.Count == 0 && !t.HasBarbarianCamp() && !t.overlayTerrainType.impassable);
		gameData.SpawnUnit(player, gameData.unitPrototypes.Find(u => u.name == "Warrior"), spot);
		return (player.units.Last(), spot);
	}

	// ---- (a) placement at world creation ----

	[Fact]
	public void WorldCreationPlacesHutsOnLand() {
		C7GameData.GameData world = NewWorld(42);
		List<Tile> huts = Huts(world);

		Assert.NotEmpty(huts);
		Assert.All(huts, t => Assert.True(t.IsLand() && !t.HasCity() && !t.HasBarbarianCamp()));
		Assert.All(huts, t => Assert.DoesNotContain(t, world.map.startingLocations));
	}

	[Fact]
	public void HutsAreSpacedApartFromEachOtherAndFromCamps() {
		C7GameData.GameData world = NewWorld(42);
		List<Tile> huts = Huts(world);
		Assert.NotEmpty(huts); // vacuous truth is not a pass

		const int spacing = 4; // GBiebs Rules.GoodyHutSpacing
		foreach (Tile h in huts) {
			Assert.All(huts.Where(o => o != h), o => Assert.True(Distance(h, o) >= spacing));
			Assert.All(world.map.barbarianCamps, c => Assert.True(Distance(h, c) >= spacing));
		}
	}

	[Fact]
	public void HutPlacementIsDeterministicForAFixedSeed() {
		List<(int, int)> first = Huts(NewWorld(42)).Select(t => (t.XCoordinate, t.YCoordinate)).OrderBy(p => p).ToList();
		List<(int, int)> second = Huts(NewWorld(42)).Select(t => (t.XCoordinate, t.YCoordinate)).OrderBy(p => p).ToList();

		Assert.NotEmpty(first);
		Assert.Equal(first, second);
	}

	[Fact]
	public void HutsSurviveSaveAndLoad() {
		C7GameData.GameData world = NewWorld(42);
		Assert.NotEmpty(Huts(world));
		C7GameData.GameData reloaded = SaveGame.FromGameData(world).Clone().ToGameData(fixture.behaviors);

		Assert.Equal(
			Huts(world).Select(t => (t.XCoordinate, t.YCoordinate)).OrderBy(p => p),
			Huts(reloaded).Select(t => (t.XCoordinate, t.YCoordinate)).OrderBy(p => p));
	}

	// ---- (b) collect framework ----

	[Fact]
	public void UnitEnteringAHutTileRemovesTheHut() {
		(MapUnit warrior, Tile spot) = WarriorAndSpot();
		SetHut(spot, true);
		C7GameData.GameData.rng = new Random(1);
		warrior.owner.isHuman = true;
		int goldBefore = warrior.owner.gold;
		EngineStorage.messagesToUI.Clear();

		warrior.OnEnterTile(spot);

		Assert.False(HasHut(spot));
		// An outcome was applied: gold changed (only Gold is implemented) and a message was emitted.
		Assert.True(warrior.owner.gold > goldBefore);
		Assert.Contains(EngineStorage.messagesToUI, m => m is MsgGoodyHut);
	}

	[Fact]
	public void BarbariansDoNotCollectHuts() {
		C7GameData.GameData.rng = new Random(1);
		Player barbarians = gameData.players.First(p => p.isBarbarians);
		Tile spot = gameData.map.tiles.First(t => Tile.IsTileValid(t) && t.IsLand() && !t.HasCity()
			&& t.unitsOnTile.Count == 0 && !t.overlayTerrainType.impassable);
		gameData.SpawnUnit(barbarians, gameData.unitPrototypes.Find(u => u.name == "Warrior"), spot);
		SetHut(spot, true);

		barbarians.units.Last().OnEnterTile(spot);

		Assert.True(HasHut(spot));
	}

	// ---- (c) effects and sourced table ----

	[Fact]
	public void GoldEffectPaysWithinTheRuleRange() {
		C7GameData.GameData.rng = new Random(1);
		(MapUnit warrior, Tile spot) = WarriorAndSpot();
		int before = warrior.owner.gold;

		Apply(GoodyHuts.Outcome.Gold, gameData, warrior, spot);

		// GBiebs Rules.GoodyHutGold = [25, 50] (approximation per its comment).
		int gained = warrior.owner.gold - before;
		Assert.InRange(gained, 25, 50);
	}

	[Fact]
	public void NothingEffectChangesNoGold() {
		C7GameData.GameData.rng = new Random(1);
		(MapUnit warrior, Tile spot) = WarriorAndSpot();
		int before = warrior.owner.gold;

		string text = Apply(GoodyHuts.Outcome.Nothing, gameData, warrior, spot);

		Assert.Equal(before, warrior.owner.gold);
		Assert.False(string.IsNullOrEmpty(text));
	}

	// Regression guard: checks the shipped Standard table against typos in
	// GBiebs's transcription (the expected numbers below were copied from it).
	// It does NOT check the CivFanatics source; the table is unverified against
	// the article. Columns: city, tech, gold, settler, map, mercenaries,
	// nothing, barbarians (percent), easiest row first.
	[Fact]
	public void OddsTableMatchesGBiebsTranscriptionRegressionGuard() {
		List<GoodyHutOdds> list = gameData.rules.GoodyHutOdds;
		Assert.Equal(8, list.Count);

		double[][] expected = [
			[5, 35, 15, 15, 10, 10, 0, 10], [10, 25, 10, 10, 10, 10, 0, 25],
			[5, 20, 10, 10, 10, 10, 0, 35], [5, 15, 10, 10, 10, 10, 0, 40],
			[5, 10, 5, 5, 5, 5, 0, 65], [0, 5, 5, 5, 5, 5, 0, 75],
			[0, 0, 5, 0, 5, 5, 0, 85], [0, 0, 0, 0, 0, 0, 0, 90],
		];
		for (int r = 0; r < 8; r++) {
			GoodyHutOdds o = list[r];
			Assert.Equal(expected[r], new[] { o.city, o.tech, o.gold, o.settler, o.map, o.mercenaries, o.nothing, o.barbarians });
		}
	}

	// ---- OutcomeChances, Enter, Odds row selection ----

	private static GoodyHutOdds Row(string name, double gold, double nothing, double city = 0) =>
		new() { difficulty = name, gold = gold, nothing = nothing, city = city };

	// A warrior and a resource-free spot, for a non-Expansionist civ on a one-row Standard table.
	private (MapUnit warrior, Tile spot) WarriorWithTable(GoodyHutOdds row) {
		C7GameData.GameData.rng = new Random(1);
		gameData.rules.GoodyHutOdds = [row];
		(MapUnit warrior, Tile spot) = WarriorAndSpot();
		warrior.owner.civilization.traits.Remove(Civilization.Trait.Expansionist);
		gameData.gameDifficulty = new Difficulty { id = gameData.difficulties[0].id, Name = row.difficulty };
		spot.Resource = Resource.NONE;
		return (warrior, spot);
	}

	[Fact]
	public void OutcomeChancesExcludeZeroChanceAndUnimplementedOutcomes() {
		// city is unimplemented (dropped despite its chance); nothing=0 is dropped; gold is all that is left.
		(MapUnit warrior, Tile spot) = WarriorWithTable(Row("Row0", gold: 15, nothing: 0, city: 5));

		Dictionary<GoodyHuts.Outcome, double> chances = GoodyHuts.OutcomeChances(gameData, warrior, spot);

		Assert.Equal([GoodyHuts.Outcome.Gold], chances.Keys.ToList());
		Assert.Equal(1.0, chances[GoodyHuts.Outcome.Gold], 9);
	}

	[Fact]
	public void OutcomeChancesShareTheTotalAmongPossibleOutcomes() {
		(MapUnit warrior, Tile spot) = WarriorWithTable(Row("Row0", gold: 15, nothing: 5, city: 80));

		Dictionary<GoodyHuts.Outcome, double> chances = GoodyHuts.OutcomeChances(gameData, warrior, spot);

		Assert.Equal(2, chances.Count);
		Assert.Equal(0.75, chances[GoodyHuts.Outcome.Gold], 9);
		Assert.Equal(0.25, chances[GoodyHuts.Outcome.Nothing], 9);
		Assert.Equal(1.0, chances.Values.Sum(), 9);
	}

	[Fact]
	public void OutcomeChancesDropGoldOnAResourceTile() {
		(MapUnit warrior, Tile spot) = WarriorWithTable(Row("Row0", gold: 15, nothing: 5));
		spot.Resource = gameData.map.tiles.Select(t => t.Resource).First(r => r != null && r != Resource.NONE);

		Dictionary<GoodyHuts.Outcome, double> chances = GoodyHuts.OutcomeChances(gameData, warrior, spot);

		Assert.Equal([GoodyHuts.Outcome.Nothing], chances.Keys.ToList());
	}

	[Fact]
	public void OutcomeChancesWithNothingPossibleIsADesertedVillage() {
		(MapUnit warrior, Tile spot) = WarriorWithTable(Row("Row0", gold: 0, nothing: 0, city: 10));

		Dictionary<GoodyHuts.Outcome, double> chances = GoodyHuts.OutcomeChances(gameData, warrior, spot);

		Assert.Equal([GoodyHuts.Outcome.Nothing], chances.Keys.ToList());
		Assert.Equal(1.0, chances[GoodyHuts.Outcome.Nothing], 9);
	}

	[Fact]
	public void EnterWithAFixedSeedGivesTheSameOutcomeTwice() {
		// Gold and nothing both possible, so the roll matters.
		(MapUnit warrior, Tile spot) = WarriorWithTable(Row("Row0", gold: 50, nothing: 50));

		List<GoodyHuts.Outcome> first = [];
		List<GoodyHuts.Outcome> second = [];
		foreach (List<GoodyHuts.Outcome> run in new[] { first, second }) {
			C7GameData.GameData.rng = new Random(12345);
			for (int i = 0; i < 20; i++) {
				run.Add(GoodyHuts.Enter(gameData, warrior, spot));
			}
		}

		Assert.Equal(first, second);
		// Non-vacuous: both outcomes occur across the 20 rolls.
		Assert.Contains(GoodyHuts.Outcome.Gold, first);
		Assert.Contains(GoodyHuts.Outcome.Nothing, first);
	}

	[Fact]
	public void EnterRemovesTheHutEvenWhenNothingIsPossible() {
		(MapUnit warrior, Tile spot) = WarriorWithTable(Row("Row0", gold: 0, nothing: 0));
		SetHut(spot, true);

		GoodyHuts.Outcome outcome = GoodyHuts.Enter(gameData, warrior, spot);

		Assert.Equal(GoodyHuts.Outcome.Nothing, outcome);
		Assert.False(HasHut(spot));
	}

	[Fact]
	public void ExpansionistCivGetsTheExpansionistTableAndOthersGetStandard() {
		C7GameData.GameData.rng = new Random(1);
		GoodyHutOdds standard = Row("Row0", gold: 1, nothing: 0);
		GoodyHutOdds expansionist = Row("Row0", gold: 2, nothing: 0);
		gameData.rules.GoodyHutOdds = [standard];
		gameData.rules.ExpansionistGoodyHutOdds = [expansionist];
		gameData.gameDifficulty = new Difficulty { id = gameData.difficulties[0].id, Name = "Row0" };
		Player player = gameData.players.First(p => !p.isBarbarians);

		player.civilization.traits.Remove(Civilization.Trait.Expansionist);
		Assert.Same(standard, GoodyHuts.Odds(gameData, player));

		player.civilization.traits.Add(Civilization.Trait.Expansionist);
		Assert.Same(expansionist, GoodyHuts.Odds(gameData, player));
	}

	[Fact]
	public void OddsUsesTheRowAtTheDifficultysPlaceWhenItsNameIsNotInTheTable() {
		C7GameData.GameData.rng = new Random(1);
		List<GoodyHutOdds> table = Enumerable.Range(0, gameData.difficulties.Count).Select(i => Row("T" + i, gold: i + 1, nothing: 0)).ToList();
		gameData.rules.GoodyHutOdds = table;
		Player player = gameData.players.First(p => !p.isBarbarians);
		player.civilization.traits.Remove(Civilization.Trait.Expansionist);

		gameData.gameDifficulty = new Difficulty { id = gameData.difficulties[2].id, Name = "NoSuchRow" };

		Assert.Same(table[2], GoodyHuts.Odds(gameData, player));
	}

	[Fact]
	public void OddsFallsBackToIndexThreeWhenTheDifficultyIsNotAmongTheDifficulties() {
		C7GameData.GameData.rng = new Random(1);
		List<GoodyHutOdds> table = Enumerable.Range(0, 8).Select(i => Row("T" + i, gold: i + 1, nothing: 0)).ToList();
		gameData.rules.GoodyHutOdds = table;
		Player player = gameData.players.First(p => !p.isBarbarians);
		player.civilization.traits.Remove(Civilization.Trait.Expansionist);

		gameData.gameDifficulty = new Difficulty { id = ID.None("not-a-difficulty"), Name = "NoSuchRow" };

		Assert.Same(table[3], GoodyHuts.Odds(gameData, player));
	}
}
