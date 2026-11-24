using C7Engine;
using System.Linq;
using C7GameData.Save;

namespace C7GameData;

public enum LeaderKind {
	None,
	Military,
	Scientific,
}

public partial class MapUnit {
	public LeaderKind leaderKind;
	public bool hasGeneratedMilitaryLeader;

	public bool IsMilitaryLeader() => leaderKind == LeaderKind.Military ||
		(leaderKind == LeaderKind.None && unitType.flags.Contains(SaveUnitPrototype.Flag.Leader));

	private void RollForMilitaryLeader(MapUnit opponent, CombatRole role, Tile battleTile) {
		// A battle against another civilization is required (D2).
		// https://github.com/C7-Game/OpenCiv3/issues/1006
		if (hasGeneratedMilitaryLeader || opponent.owner == owner || opponent.owner.isBarbarians ||
			battleTile == Tile.NONE || !IsCombatUnit() || !opponent.IsCombatUnit()) {
			return;
		}

		GameData game = EngineStorage.gameData;
		Rules rules = game.rules;
		if (rules.BattleCreatedUnit == null || owner.units.Count(unit => unit.IsMilitaryLeader()) >= rules.MaximumMilitaryLeaders) {
			return;
		}

		UnitPrototype prototype = game.unitPrototypes.Find(unit => unit.name == rules.BattleCreatedUnit);
		if (prototype == null) {
			return;
		}
		bool hasEpic = owner.OwnsBuilding(building => building.HasWonderFlag(SaveBuilding.WonderFlag.IncreasesLeaderChance));
		double chance = hasEpic ? rules.MilitaryLeaderChanceWithEpic : rules.MilitaryLeaderChance;
		if (role == CombatRole.Defense) {
			chance *= rules.DefendingMilitaryLeaderChanceMultiplier;
		}
		if (chance <= 0 || GameData.rng.NextDouble() >= chance) {
			return;
		}

		MapUnit leader = game.SpawnUnit(owner, prototype, battleTile);
		leader.leaderKind = LeaderKind.Military;
		var names = owner.civilization.militaryLeaderNames;
		// Standalone and scenarios without a name pool use the D3 fallback.
		// https://github.com/C7-Game/OpenCiv3/issues/1006
		leader.name = names is { Count: > 0 } ? names[GameData.rng.Next(names.Count)] : "Great General";
		hasGeneratedMilitaryLeader = true;
	}

	private bool CanUseMilitaryLeader() => IsMilitaryLeader() && hitPointsRemaining > 0 && loadedOnUnitId == null &&
		location.cityAtTile is City city && city.owner == owner;

	public bool CanFormArmy() => CanUseMilitaryLeader() &&
		EngineStorage.gameData.unitPrototypes.Any(unit => unit.name == EngineStorage.gameData.rules.BuildArmyUnit);

	public bool TryFormArmy() {
		if (!CanFormArmy()) {
			return false;
		}
		GameData game = EngineStorage.gameData;
		UnitPrototype army = game.unitPrototypes.Find(unit => unit.name == game.rules.BuildArmyUnit);
		game.SpawnUnit(owner, army, location);
		RemoveFromPlay();
		return true;
	}

	public bool CanHurryProduction() {
		// Conquests military leaders can rush improvements, small wonders and units.
		// https://forums.civfanatics.com/threads/civ3-conquests-additions-changes-list.104294/
		return CanUseMilitaryLeader() && location.cityAtTile.itemBeingProduced switch {
			Building building => building.greatWonderProperties == null &&
				location.cityAtTile.shieldsStored < owner.ShieldCost(building),
			UnitPrototype unit => location.cityAtTile.shieldsStored < owner.ShieldCost(unit),
			_ => false,
		};
	}

	public bool TryHurryProduction() {
		if (!CanHurryProduction()) {
			return false;
		}
		City city = location.cityAtTile;
		city.SetStoredShields(owner.ShieldCost(city.itemBeingProduced));
		RemoveFromPlay();
		return true;
	}
}
