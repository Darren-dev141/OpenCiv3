using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
// The feature does not exist yet, so the API under test is bound by name via
// reflection. That keeps this project compiling (and the existing tests
// running) while each test fails with "missing feature" until it is built.
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

	// ---- reflection binding to the not-yet-written API ----

	private static Exception Missing(string what) =>
		new Xunit.Sdk.XunitException($"Goody hut feature missing: {what}");

	private static bool HasHut(Tile tile) {
		FieldInfo f = typeof(Tile).GetField("hasGoodyHut", BindingFlags.Public | BindingFlags.Instance);
		if (f == null) throw Missing("Tile.hasGoodyHut");
		return (bool)f.GetValue(tile);
	}

	private static void SetHut(Tile tile, bool value) {
		FieldInfo f = typeof(Tile).GetField("hasGoodyHut", BindingFlags.Public | BindingFlags.Instance);
		if (f == null) throw Missing("Tile.hasGoodyHut");
		f.SetValue(tile, value);
	}

	private static Type GoodyHutsType() =>
		typeof(Tile).Assembly.GetType("C7GameData.GoodyHuts") ?? throw Missing("class C7GameData.GoodyHuts");

	private static object OutcomeValue(string name) {
		Type outcome = GoodyHutsType().GetNestedType("Outcome") ?? throw Missing("GoodyHuts.Outcome enum");
		return Enum.Parse(outcome, name);
	}

	private static string Apply(string outcome, C7GameData.GameData game, MapUnit unit, Tile tile) {
		MethodInfo m = GoodyHutsType().GetMethod("Apply", BindingFlags.Public | BindingFlags.Static)
			?? throw Missing("GoodyHuts.Apply(outcome, gameData, unit, tile, tribe)");
		return (string)m.Invoke(null, [OutcomeValue(outcome), game, unit, tile, "test"]);
	}

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

		warrior.OnEnterTile(spot);

		Assert.False(HasHut(spot));
	}

	[Fact]
	public void BarbariansDoNotCollectHuts() {
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
		(MapUnit warrior, Tile spot) = WarriorAndSpot();
		int before = warrior.owner.gold;

		Apply("Gold", gameData, warrior, spot);

		// GBiebs Rules.GoodyHutGold = [25, 50] (approximation per its comment).
		int gained = warrior.owner.gold - before;
		Assert.InRange(gained, 25, 50);
	}

	[Fact]
	public void NothingEffectChangesNoGold() {
		(MapUnit warrior, Tile spot) = WarriorAndSpot();
		int before = warrior.owner.gold;

		string text = Apply("Nothing", gameData, warrior, spot);

		Assert.Equal(before, warrior.owner.gold);
		Assert.False(string.IsNullOrEmpty(text));
	}

	// Standard odds, easiest row (percent): city, tech, gold, settler, map,
	// mercenaries, nothing, barbarians.
	[Fact]
	public void OddsTableMatchesTheSourcedStandardRows() {
		object rows = gameData.rules.GetType().GetProperty("GoodyHutOdds")?.GetValue(gameData.rules)
			?? gameData.rules.GetType().GetField("GoodyHutOdds")?.GetValue(gameData.rules)
			?? throw Missing("Rules.GoodyHutOdds");
		List<object> list = ((IEnumerable)rows).Cast<object>().ToList();
		Assert.Equal(8, list.Count);

		string[] columns = ["city", "tech", "gold", "settler", "map", "mercenaries", "nothing", "barbarians"];
		double[][] expected = [
			[5, 35, 15, 15, 10, 10, 0, 10], [10, 25, 10, 10, 10, 10, 0, 25],
			[5, 20, 10, 10, 10, 10, 0, 35], [5, 15, 10, 10, 10, 10, 0, 40],
			[5, 10, 5, 5, 5, 5, 0, 65], [0, 5, 5, 5, 5, 5, 0, 75],
			[0, 0, 5, 0, 5, 5, 0, 85], [0, 0, 0, 0, 0, 0, 0, 90],
		];
		for (int r = 0; r < 8; r++) {
			for (int c = 0; c < columns.Length; c++) {
				Type t = list[r].GetType();
				object v = t.GetField(columns[c])?.GetValue(list[r]) ?? t.GetProperty(columns[c])?.GetValue(list[r]);
				Assert.Equal(expected[r][c], Convert.ToDouble(v));
			}
		}
	}
}
