using System;
using System.IO;
using System.Linq;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;
using Xunit.Abstractions;
using Game = C7GameData.GameData;

namespace EngineTests.GameData;

public class MilitaryLeaderTest(SaveGameFixture fixture, ITestOutputHelper output)
	: IClassFixture<SaveGameFixture> {
	[Theory]
	[InlineData(false, false, 160000, 1.0 / 16, 0.0035)]
	[InlineData(true, false, 160000, 1.0 / 32, 0.0025)]
	[InlineData(false, true, 20000, 0, 0)]
	public void EliteVictoryFrequency(bool defending, bool barbarian, int trials, double expected, double tolerance) {
		Game game = Load();
		(MapUnit elite, MapUnit opponent) = Combatants(game, barbarian);
		CombatRole role = defending ? CombatRole.Defense : CombatRole.Attack;
		int spawned = 0;
		for (int i = 0; i < trials; i++) {
			Roll(elite, opponent, role);
			MapUnit leader = Leaders(elite.owner).SingleOrDefault();
			if (leader != null) {
				spawned++;
				leader.owner.units.Remove(leader);
				leader.location.unitsOnTile.Remove(leader);
				game.mapUnits.Remove(leader);
				elite = NewElite(game, elite.owner, elite.location);
			}
		}
		output.WriteLine("{0}/{1} leaders ({2:P4})", spawned, trials, (double)spawned / trials);
		Assert.InRange((double)spawned / trials, expected - tolerance, expected + tolerance);
	}

	[Fact]
	public void LiveMilitaryLeaderBlocksAnother() {
		Game game = Load();
		(MapUnit elite, MapUnit opponent) = Combatants(game);
		for (int i = 0; i < 500; i++) {
			Roll(NewElite(game, elite.owner, elite.location), opponent, CombatRole.Attack);
		}
		Assert.Single(Leaders(elite.owner));
	}

	[Fact]
	public void BattleSiteAndNameSurviveSaveLoadAndLeaderCanHurrySmallWonder() {
		Game game = Load();
		(MapUnit elite, MapUnit opponent) = Combatants(game);
		for (int i = 0; i < 500 && !Leaders(elite.owner).Any(); i++) {
			Roll(elite, opponent, CombatRole.Attack);
		}
		MapUnit leader = Assert.Single(Leaders(elite.owner));
		Assert.Same(opponent.location, leader.location);
		Assert.Contains(leader, opponent.location.unitsOnTile);
		Assert.False(leader.IsCombatUnit());
		Assert.Equal(3, leader.unitType.movement);
		Assert.False(string.IsNullOrWhiteSpace(leader.name));

		string path = Path.Combine(Path.GetTempPath(), $"spa64-{Guid.NewGuid():N}.json");
		try {
			SaveGame.FromGameData(game).Save(path);
			EngineStorage.gameData = SaveGame.Load(path, null, null).ToGameData(fixture.behaviors);
		} finally {
			File.Delete(path);
		}
		game = EngineStorage.gameData;
		MapUnit restored = game.mapUnits.Single(unit => unit.id == leader.id);
		Assert.Equal(leader.name, restored.name);
		Assert.Equal(new TileLocation(leader.location), new TileLocation(restored.location));

		City city = new(restored.location, restored.owner, "Test City", ID.None("city"));
		restored.location.cityAtTile = city;
		city.itemBeingProduced = new Building(new SaveBuilding(), game) { name = "Great Wonder", shieldCost = 100, greatWonderProperties = new() };
		Assert.False(Hurry(restored));
		Assert.Contains(restored, game.mapUnits);
		Assert.Equal(0, city.shieldsStored);

		city.itemBeingProduced = new Building(new SaveBuilding(), game) { name = "Small Wonder", shieldCost = 100, isSmallWonder = true };
		Assert.True(Hurry(restored));
		Assert.Equal(restored.owner.ShieldCost(city.itemBeingProduced), city.shieldsStored);
		Assert.DoesNotContain(restored, game.mapUnits);
	}

	private Game Load() {
		Game game = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.gameData = game;
		Game.rng = new Random(77431);
		return game;
	}

	private static (MapUnit elite, MapUnit opponent) Combatants(Game game, bool barbarian = false) {
		MapUnit attacker = game.mapUnits.First(unit => !unit.owner.isBarbarians);
		Player enemy = game.players.First(player => player != attacker.owner && player.isBarbarians == barbarian);
		Tile battle = attacker.location.neighbors.Values.First(tile => tile != Tile.NONE && tile != attacker.location);
		return (NewElite(game, attacker.owner, attacker.location), new(ID.None("opponent")) {
			owner = enemy, location = battle, unitType = attacker.unitType,
		});
	}

	private static MapUnit NewElite(Game game, Player owner, Tile tile) => new(ID.None("elite")) {
		owner = owner,
		location = tile,
		unitType = owner.units.First().unitType,
		experienceLevel = game.experienceLevels.Last(),
	};

	private static System.Collections.Generic.IEnumerable<MapUnit> Leaders(Player player) =>
		player.units.Where(unit => unit.unitType.name == "Leader");

	// The base branch has only the old promotion hook. Keep the probe runnable against both APIs.
	private static readonly System.Reflection.MethodInfo CombatPromotion = typeof(MapUnit).GetMethod(
		nameof(MapUnit.RollToPromote), [typeof(MapUnit), typeof(CombatRole), typeof(Tile)]);

	private static void Roll(MapUnit elite, MapUnit opponent, CombatRole role) {
		if (CombatPromotion == null) {
			elite.RollToPromote(opponent);
		} else {
			CombatPromotion.Invoke(elite, [opponent, role, role == CombatRole.Attack ? opponent.location : elite.location]);
		}
	}

	private static bool Hurry(MapUnit leader) =>
		typeof(MapUnit).GetMethod("TryHurryProduction", Type.EmptyTypes)?.Invoke(leader, null) is true;
}
