using System.Collections.Generic;
using System.Linq;

namespace C7GameData;

public class CulturalVictory : IVictory {
	// The twice-rival rule applies only to empire culture.
	// https://forums.civfanatics.com/threads/what-does-20k-mean.213039/
	private const int RivalCultureMultiplier = 2;

	private readonly int cityThreshold;
	private readonly int empireThreshold;

	public CulturalVictory(int cityThreshold, int empireThreshold) {
		this.cityThreshold = cityThreshold;
		this.empireThreshold = empireThreshold;
	}

	public string Header() => "Cultural";

	public VictoryStatus Evaluate(Player player, GameData gameData) {
		long rivalCulture = gameData.players
			.Where(p => p != player && !p.isBarbarians && !p.defeated)
			.Select(p => p.cities.Sum(c => (long)c.GetCulture()))
			.DefaultIfEmpty()
			.Max();
		return new VictoryStatus {
			Player = player,
			HighestCityCulture = player.cities.Select(c => c.GetCulture()).DefaultIfEmpty().Max(),
			EmpireCulture = player.cities.Sum(c => (long)c.GetCulture()),
			RivalEmpireCulture = rivalCulture,
		};
	}

	public bool HasVictory(VictoryStatus status) =>
		status.HighestCityCulture >= cityThreshold
		|| (status.EmpireCulture >= empireThreshold
			&& status.EmpireCulture >= RivalCultureMultiplier * status.RivalEmpireCulture);

	public IEnumerable<string[]> GenerateStatusRows(VictoryStatus status, List<VictoryStatus> rivalStatuses) {
		var eligibleRivals = rivalStatuses.Where(r => !r.Player.isBarbarians && !r.Player.defeated);
		VictoryStatus bestRivalCity = eligibleRivals.MaxBy(r => r.HighestCityCulture);
		VictoryStatus bestRivalEmpire = eligibleRivals.MaxBy(r => r.EmpireCulture);
		yield return [
			"Single-city culture:",
			$"{cityThreshold}",
			"Your best city culture:",
			$"{status.HighestCityCulture}",
			bestRivalCity?.Player.civilization.name ?? "",
			bestRivalCity?.HighestCityCulture.ToString() ?? "",
		];
		yield return [
			"Empire culture (and 2× rival):",
			$"{empireThreshold}",
			"Your empire culture:",
			$"{status.EmpireCulture}",
			bestRivalEmpire?.Player.civilization.name ?? "",
			bestRivalEmpire?.EmpireCulture.ToString() ?? "",
		];
	}
}
