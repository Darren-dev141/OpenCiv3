using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using C7GameData.Save;
using QueryCiv3;

namespace ParityTests.Canonical;

/// <summary>
/// SPEC §6 canonical state projection: reduces a game state to a stable,
/// order-independent string so two representations (raw SavData vs imported
/// SaveGame) can be digested and compared field-by-field. Surrogate/stable IDs
/// are used where the original object identity is not meaningful.
/// </summary>
public static class Projection {
	public static string Sha256(string text) {
		return Fixtures.FixtureStore.Sha256Hex(Encoding.UTF8.GetBytes(text));
	}

	/// <summary>
	/// Canonical projection of an imported SaveGame. Only fields that survive
	/// the Civ3 -> SaveGame import are included.
	/// </summary>
	public static string Of(SaveGame save) {
		StringBuilder sb = new();
		sb.Append($"seed={save.Seed}\n");
		sb.Append($"turn={save.TurnNumber}\n");
		sb.Append($"map={save.Map.tilesWide}x{save.Map.tilesTall},wrapH={save.Map.wrapHorizontally},wrapV={save.Map.wrapVertically}\n");
		sb.Append($"tiles={save.Map.tiles.Count}\n");

		foreach (var group in save.Map.tiles
			.Select(t => string.Join(",", t.overlays.OrderBy(o => o)))
			.GroupBy(o => o).OrderBy(g => g.Key)) {
			sb.Append($"overlay[{group.Key}]={group.Count()}\n");
		}

		foreach (SavePlayer p in save.Players.OrderBy(p => p.id.ToString())) {
			sb.Append($"player[{p.id}] civ={p.civilization} human={p.human} sci={p.scienceRate} lux={p.luxuryRate} tax={p.taxRate} gold={p.gold} beakers={p.beakers}\n");
		}

		sb.Append($"cities={save.Cities.Count}\n");
		foreach (SaveCity c in save.Cities.OrderBy(c => c.id.ToString())) {
			sb.Append($"city[{c.id}] name={c.name} owner={c.owner} size={c.size} loc={c.location.X},{c.location.Y}\n");
		}

		sb.Append($"units={save.Units.Count}\n");
		foreach (var group in save.Units.GroupBy(u => u.prototype).OrderBy(g => g.Key)) {
			sb.Append($"unit[{group.Key}]={group.Count()}\n");
		}

		sb.Append($"buildings={save.Buildings.Count}\n");
		foreach (SaveBuilding b in save.Buildings.OrderBy(b => b.name)) {
			sb.Append($"building[{b.name}]\n");
		}

		return sb.ToString();
	}

	/// <summary>
	/// Canonical projection of a raw SavData (QueryCiv3 ground truth).
	/// </summary>
	public static string Of(SavData sav) {
		StringBuilder sb = new();
		sb.Append($"seed={sav.Wrld.WorldSeed}\n");
		sb.Append($"turn={sav.Game.TurnNumber}\n");
		sb.Append($"map={sav.Wrld.Width}x{sav.Wrld.Height},wrapH={sav.Wrld.XWrapping},wrapV={sav.Wrld.YWrapping}\n");
		sb.Append($"tiles={sav.Tile.Length}\n");
		sb.Append($"huts={sav.Tile.Count(t => t.GoodyHut)}\n");
		sb.Append($"camps={sav.Tile.Count(t => t.BarbarianCamp)}\n");
		sb.Append($"units={sav.Unit?.Length ?? 0}\n");
		sb.Append($"cities={sav.City?.Length ?? 0}\n");
		int civs = sav.Lead?.Count(l => l.RaceID != -1) ?? 0;
		sb.Append($"players={civs}\n");
		return sb.ToString();
	}

	public static string Digest(SaveGame save) => Sha256(Of(save));
	public static string Digest(SavData sav) => Sha256(Of(sav));
}
