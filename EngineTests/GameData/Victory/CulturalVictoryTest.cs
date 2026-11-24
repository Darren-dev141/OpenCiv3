using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.Lua;
using C7GameData;
using EngineTests.Utils;
using QueryCiv3;
using Xunit;

namespace EngineTests.GameData.Victory;

public class CulturalVictoryTest : IClassFixture<SaveGameFixture> {
	private readonly SaveGameFixture fixture;

	public CulturalVictoryTest(SaveGameFixture fixture) {
		this.fixture = fixture;
	}

	[Theory]
	[InlineData("Standard", true)]
	[InlineData("Huge", true)]
	[InlineData("Standard", false)]
	[InlineData("Huge", false)]
	public async Task CityWinsOnTheTurnItReachesTwentyThousand(string size, bool enabled) {
		var (human, rival) = await StartGame(size, enabled, 1, 2);
		SeedNextTurnTotal(human, 19_999);
		SeedNextTurnTotal(rival, 30_000);
		await AdvanceOneTurn();
		Assert.Null(EngineStorage.gameData.winner);

		await AdvanceOneTurn();
		Assert.Equal(20_000, human.cities.Single().GetCulture());
		Assert.Equal(enabled ? human : null, EngineStorage.gameData.winner);
		Assert.Equal(enabled, EngineStorage.gameData.gameOver);
		Assert.Equal(enabled ? 1 : 0, EngineStorage.messagesToUI.OfType<MsgVictory>().Count());

		if (enabled) {
			await AdvanceOneTurn();
			Assert.Same(human, EngineStorage.gameData.winner);
			Assert.Empty(EngineStorage.messagesToUI.OfType<MsgVictory>());
		}
	}

	[Theory]
	[InlineData("Standard", 100_000, true)]
	[InlineData("Huge", 160_000, true)]
	[InlineData("Huge", 160_000, false)]
	public async Task EmpireNeedsBothTheMapThresholdAndTwiceTheRival(string size, int limit, bool enabled) {
		var (human, rival) = await StartGame(size, enabled, 9, 5);

		await SetTotalsAndAdvance(size == "Huge" ? 100_000 : limit - 1, 40_000);
		Assert.Null(EngineStorage.gameData.winner);
		await SetTotalsAndAdvance(limit, (int)Math.Round(limit / 1.9));
		Assert.Null(EngineStorage.gameData.winner);
		await SetTotalsAndAdvance(limit, limit / 2 + 1);
		Assert.Null(EngineStorage.gameData.winner);
		await SetTotalsAndAdvance(limit, limit / 2);
		Assert.Equal(enabled ? human : null, EngineStorage.gameData.winner);
		Assert.Equal(enabled, EngineStorage.gameData.gameOver);

		async Task SetTotalsAndAdvance(int ownTotal, int rivalTotal) {
			SeedNextTurnTotal(human, ownTotal);
			SeedNextTurnTotal(rival, rivalTotal);
			await AdvanceOneTurn();
			Assert.Equal(ownTotal, human.cities.Sum(c => c.GetCulture()));
			Assert.Equal(rivalTotal, rival.cities.Sum(c => c.GetCulture()));
			Assert.All(human.cities.Concat(rival.cities), c => Assert.True(c.GetCulture() < 20_000));
		}
	}

	[Fact]
	public async Task ScenarioWorldThresholdSurvivesSaveRoundTripAndAppliesWithoutRivals() {
		var (human, rival) = await StartGame("Huge", true, 3, 0, empireThreshold: 40_000);
		rival.defeated = true;
		SeedNextTurnTotal(human, 40_000);
		await AdvanceOneTurn();
		Assert.All(human.cities, c => Assert.True(c.GetCulture() < 20_000));
		Assert.Same(human, EngineStorage.gameData.winner);
	}

	[SkippableFact]
	public void ImportMesoamericaPreservesCustomCultureThresholds() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");
		string root = Path.Combine(Civ3Location.GetCiv3Path(), "Conquests");
		string scenario = Path.Combine(root, "Conquests", "5 Mesoamerica.biq");
		string defaults = Path.Combine(root, "conquests.biq");
		AssertFixtureHash(scenario, "8519E37A7DE6749E99F9EBF8D99D4BF47451A287C61EC07E15A844A53E27CDA6");
		AssertFixtureHash(defaults, "1BD610AB1420217F7D64A9214B60C0BC7686E4781A0D6E66590171535495609C");

		var save = ImportCiv3.ImportBiq(scenario, defaults,
			mod => Path.GetFullPath(Path.Combine(root, "Conquests", mod.Replace('\\', Path.DirectorySeparatorChar), "Text", "PediaIcons.txt")),
			_ => Path.Combine(root, "Text", "Civilopedia.txt"));
		Assert.Equal(2_000, save.Rules.CityCultureVictoryThreshold);
		Assert.Equal(7_000, save.Rules.EmpireCultureVictoryThresholdOverride);

		static void AssertFixtureHash(string path, string expected) {
			Skip.If(!File.Exists(path), $"Fixture not installed: {path}");
			using var stream = File.OpenRead(path);
			Skip.If(Convert.ToHexString(SHA256.HashData(stream)) != expected, $"Fixture hash differs: {path}");
		}
	}

	private async Task<(Player human, Player rival)> StartGame(string size, bool enabled, int ownCities, int rivalCities, int? empireThreshold = null) {
		var save = GameMode.Load(PathUtils.GameModesDir, new GameMode.Config("civ3")).GetSave();
		save.Seed = fixture.saveGame.Seed;
		var world = save.WorldSizes.Single(w => w.name == size);
		world.empireCultureVictoryThreshold = empireThreshold;
		new GameSetup {
			playerCivilization = save.Civilizations.First(c => !c.isBarbarian),
			difficulty = save.Difficulties.First(),
			worldCharacteristics = new WorldCharacteristics {
				worldSize = world,
				landform = WorldCharacteristics.Landform.Pangaea,
				oceanCoverage = WorldCharacteristics.OceanCoverage.Percent_70,
			},
			opponents = [new SelectedOpponent { isRandom = true }],
			victoryConditions = new VictoryConditions { AllowCulturalVictory = enabled },
		}.Populate(save);

		var human = await CreateGame.createGame(empireThreshold.HasValue ? save.Clone() : save, _ => fixture.behaviors);
		var game = EngineStorage.gameData;
		var rival = game.players.Single(p => !p.isHuman && !p.isBarbarians);
		Assert.Equal(world.width, game.map.numTilesWide);
		FoundCities(human, ownCities);
		FoundCities(rival, rivalCities);
		return (human, rival);

		void FoundCities(Player player, int count) {
			var start = player.units.First().location;
			for (int i = 0; i < count; i++) {
				var site = game.map.tiles.Where(t => t.IsLand()
					&& game.cities.All(c => c.location.DistanceTo(t) >= 3))
					.OrderBy(t => t.DistanceTo(start)).First();
				CityInteractions.BuildCity(site, player, $"City {i + 1}");
			}
		}
	}

	private static void SeedNextTurnTotal(Player player, int total) {
		int count = player.cities.Count;
		for (int i = 0; i < count; i++) {
			var city = player.cities[i];
			int target = total / count + (i < total % count ? 1 : 0);
			city.perPlayerCulture[player] = target - city.GetCulturePerTurn();
		}
	}

	private static async Task AdvanceOneTurn() {
		EngineStorage.messagesToUI.Clear();
		var game = EngineStorage.gameData;
		int turn = game.turn;
		foreach (var player in game.players)
			player.hasPlayedThisTurn = true;
		await TurnHandling.AdvanceTurn();
		Assert.Equal(turn + 1, game.turn);
	}
}
