using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
	[InlineData(true, true, 20000, 0, 0)]
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
		game.rules.MilitaryLeaderChance = 1;
		for (int i = 0; i < 20000; i++) {
			Roll(NewElite(game, elite.owner, elite.location), opponent, CombatRole.Attack);
		}
		Assert.Single(Leaders(elite.owner));
	}

	[Fact]
	public void MilitaryCapDoesNotCountScientificLeaders() {
		Game game = Load();
		(MapUnit elite, MapUnit opponent) = Combatants(game);
		game.rules.MilitaryLeaderChance = 1;
		elite.owner.units.Add(new MapUnit(ID.None("scientific-leader")) {
			owner = elite.owner,
			unitType = game.unitPrototypes.Single(unit => unit.name == "Leader"),
			leaderKind = LeaderKind.Scientific,
		});
		Roll(elite, opponent, CombatRole.Attack);
		Assert.Single(Leaders(elite.owner));
	}

	[Fact]
	public void ConfiguredDefenseRateAndMilitaryCapAreApplied() {
		Game game = Load();
		(MapUnit elite, MapUnit opponent) = Combatants(game);
		game.rules.MilitaryLeaderChance = 1;
		game.rules.DefendingMilitaryLeaderChanceMultiplier = 0;
		game.rules.MaximumMilitaryLeaders = 2;
		Roll(elite, opponent, CombatRole.Defense);
		Assert.Empty(Leaders(elite.owner));

		game.rules.DefendingMilitaryLeaderChanceMultiplier = 1;
		Roll(elite, opponent, CombatRole.Defense);
		Assert.Single(Leaders(elite.owner));
		Roll(NewElite(game, elite.owner, elite.location), opponent, CombatRole.Attack);
		Assert.Equal(2, Leaders(elite.owner).Count());
		Roll(NewElite(game, elite.owner, elite.location), opponent, CombatRole.Attack);
		Assert.Equal(2, Leaders(elite.owner).Count());
	}

	[Fact]
	public void MilitaryLeaderFormsArmyAndIsConsumed() {
		Game game = Load();
		(MapUnit elite, MapUnit opponent) = Combatants(game);
		game.rules.MilitaryLeaderChance = 1;
		Roll(elite, opponent, CombatRole.Attack);
		MapUnit leader = Assert.Single(Leaders(elite.owner));
		City city = new(leader.location, leader.owner, "Test City", ID.None("city"));
		leader.location.cityAtTile = city;
		Assert.Equal("Army", game.rules.BuildArmyUnit);
		Assert.Contains(UnitAction.FormArmy, leader.GetAvailableActions());
		Assert.True(leader.TryFormArmy());
		Assert.DoesNotContain(leader, game.mapUnits);
		Assert.DoesNotContain(leader, leader.owner.units);
		Assert.DoesNotContain(leader, leader.location.unitsOnTile);
		MapUnit army = Assert.Single(game.mapUnits.Where(unit => unit.owner == elite.owner && unit.unitType.name == "Army"));
		Assert.Same(city.location, army.location);
		Assert.False(leader.TryFormArmy());
	}

	[Fact]
	public async Task BattleSiteAndNameSurviveSaveLoadAndLeaderCanHurrySmallWonder() {
		Game game = Load();
		(MapUnit elite, MapUnit opponent) = Combatants(game);
		elite.owner.civilization.militaryLeaderNames = ["Test General"];
		game.rules.MilitaryLeaderChance = 1;
		game.observerMode = true;
		elite.unitType = new UnitPrototype { name = "Attacker", attack = 1, movement = 1 };
		elite.hitPointsRemaining = 2;
		opponent.unitType = new UnitPrototype { name = "Defender", attack = 1, defense = 0, movement = 1 };
		opponent.hitPointsRemaining = 1;
		Assert.Equal(CombatResult.DefenderKilled, await elite.Fight(opponent));
		MapUnit leader = Assert.Single(Leaders(elite.owner));
		Assert.Same(opponent.location, leader.location);
		Assert.Contains(leader, opponent.location.unitsOnTile);
		Assert.False(leader.IsCombatUnit());
		Assert.Equal(3, leader.unitType.movement);
		Assert.Equal("Test General", leader.name);

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
		Assert.Equal(LeaderKind.Military, restored.leaderKind);
		Assert.Equal(1, game.rules.MilitaryLeaderChance);
		Assert.Equal(new TileLocation(leader.location), new TileLocation(restored.location));

		City city = new(restored.location, restored.owner, "Test City", ID.None("city"));
		restored.location.cityAtTile = city;
		city.itemBeingProduced = new Building(new SaveBuilding(), game) { name = "Great Wonder", shieldCost = 100, greatWonderProperties = new() };
		Assert.False(restored.TryHurryProduction());
		Assert.Contains(restored, game.mapUnits);
		Assert.Equal(0, city.shieldsStored);

		city.itemBeingProduced = new Building(new SaveBuilding(), game) { name = "Small Wonder", shieldCost = 100, isSmallWonder = true };
		Assert.Contains(UnitAction.HurryProduction, restored.GetAvailableActions());
		Assert.True(restored.TryHurryProduction());
		Assert.Equal(restored.owner.ShieldCost(city.itemBeingProduced), city.shieldsStored);
		Assert.DoesNotContain(restored, game.mapUnits);
		Assert.False(restored.TryHurryProduction());
	}

	private Game Load() {
		Game game = fixture.saveGame.ToGameData(fixture.behaviors);
		game.rules.MaximumMilitaryLeaders = 1;
		game.rules.MilitaryLeaderChance = 1.0 / 16;
		game.rules.MilitaryLeaderChanceWithEpic = 1.0 / 12;
		game.rules.DefendingMilitaryLeaderChanceMultiplier = 0.5;
		EngineStorage.gameData = game;
		Game.rng = new Random(77431);
		return game;
	}

	private static (MapUnit elite, MapUnit opponent) Combatants(Game game, bool barbarian = false) {
		MapUnit attacker = game.mapUnits.First(unit => !unit.owner.isBarbarians);
		Player enemy = game.players.First(player => player != attacker.owner && player.isBarbarians == barbarian);
		Tile battle = attacker.location.neighbors.Values.First(tile => tile != Tile.NONE && tile != attacker.location);
		return (NewElite(game, attacker.owner, attacker.location), new(ID.None("opponent")) {
			owner = enemy, nationality = enemy.civilization, location = battle, unitType = game.unitPrototypes.First(unit => unit.name == "Warrior"),
		});
	}

	private static MapUnit NewElite(Game game, Player owner, Tile tile) => new(ID.None("elite")) {
		owner = owner,
		nationality = owner.civilization,
		location = tile,
		unitType = game.unitPrototypes.First(unit => unit.name == "Warrior"),
		experienceLevel = game.experienceLevels.Last(),
	};

	private static System.Collections.Generic.IEnumerable<MapUnit> Leaders(Player player) =>
		player.units.Where(unit => unit.IsMilitaryLeader());

	private static void Roll(MapUnit elite, MapUnit opponent, CombatRole role) =>
		elite.RollToPromote(opponent, role, role == CombatRole.Attack ? opponent.location : elite.location);
}
