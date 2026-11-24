using System;
using System.Linq;
using C7Engine.Lua;
using C7GameData;
using C7GameData.Save;
using ParityTests.Fixtures;
using ParityTests.Utils;

namespace ParityTests.Checks;

/// <summary>
/// CM-C1 (kind C, transition/regression): reproduces https://github.com/C7-Game/OpenCiv3/issues/1080.
/// In the attached save, moving the sliders from 100% science to 90% science /
/// 10% luxury must move exactly one commerce per turn from science to
/// entertainment and leave the treasury untouched. The save is fetched from the issue
/// attachment at test time (sha256-pinned in the manifest), never committed.
/// Matrix rows SPA-5 / SPA-83 / SPA-40.
/// </summary>
public class CommerceSliderCheck : IParityCheck {
	public string Id => "CM-C1";
	public string MatrixRow => "SPA-5/SPA-83/SPA-40";
	public string SpaIssue => "#1080";
	public CheckKind Kind => CheckKind.C;
	public string Reference => "https://github.com/C7-Game/OpenCiv3/issues/1080 + attached save openciv3test1.json (sha256 30ab29fa...)";
	public string Tolerance => "exact: empire-wide delta science -1, entertainment +1, treasury 0 when luxury slider goes 0% -> 10%";

	private const string FixtureId = "issue-1080-openciv3test1";
	private const string Allegheny = "Allegheny";

	public CheckResult Run(FixtureStore store) {
		CheckResult result = this.BaseResult();
		Fixture fixture = store.Manifest.Fixtures.FirstOrDefault(f => f.Id == FixtureId);
		if (fixture == null) {
			return CheckResult.Skip(this, $"fixture {FixtureId} not in manifest");
		}

		ResolvedFixture resolved = store.Resolve(fixture);
		if (resolved.Error != null || resolved.Bytes == null) {
			return CheckResult.Skip(this, "issue-1080 fixture unavailable: " + (resolved.Error ?? "no bytes"));
		}

		try {
			Evaluate(resolved, result);
		} catch (Exception ex) {
			result.Status = "ERROR";
			result.Summary = ex.GetType().Name + ": " + ex.Message;
		}
		return result;
	}

	private void Evaluate(ResolvedFixture resolved, CheckResult result) {
		SaveGame save = SaveGame.Load(resolved.LocalPath,
			_ => _, _ => _);
		BehaviorEngine behaviors = GameMode.Load(RepoPaths.GameModesDir, new GameMode.Config("civ3")).behaviors;
		GameData gd = save.ToGameData(behaviors);

		Player human = gd.players.FirstOrDefault(p => p.isHuman) ?? gd.players.First();
		City allegheny = human.cities.FirstOrDefault(c => c.name == Allegheny);

		(int sci, int lux, int tax) = (human.scienceRate, human.luxuryRate, human.taxRate);
		try {
			// Baseline: science slider at 100% (10/10), luxury and tax at 0.
			human.scienceRate = 10; human.luxuryRate = 0; human.taxRate = 0;
			CommerceBreakdown before = TotalCommerce(human);
			CommerceBreakdown alleyBefore = allegheny?.CurrentCommerceYield() ?? default;

			// Issue #1080 setting: science 90%, luxury 10%, tax 0%.
			human.scienceRate = 9; human.luxuryRate = 1; human.taxRate = 0;
			CommerceBreakdown after = TotalCommerce(human);
			CommerceBreakdown alleyAfter = allegheny?.CurrentCommerceYield() ?? default;

			int dBeakers = after.beakers - before.beakers;
			int dHappiness = after.happiness - before.happiness;
			int dTaxes = after.taxes - before.taxes;

			FixtureResult row = new() {
				FixtureId = FixtureId,
				Detail = $"empire commerce @100% sci: beakers={before.beakers} happiness={before.happiness} taxes={before.taxes}; " +
					$"@90/10: beakers={after.beakers} happiness={after.happiness} taxes={after.taxes}; " +
					$"deltas=({dBeakers},{dHappiness},{dTaxes}); " +
					$"{Allegheny}: beakers {alleyBefore.beakers}->{alleyAfter.beakers}, " +
					$"happiness {alleyBefore.happiness}->{alleyAfter.happiness}, taxes {alleyBefore.taxes}->{alleyAfter.taxes}",
			};
			result.Fixtures.Add(row);

			bool empireOk = dBeakers == -1 && dHappiness == 1 && dTaxes == 0;
			// Issue #1080's city-level symptom: Allegheny's remaining useful
			// commerce must not be dumped into tax revenue at 90/10.
			bool alleyOk = allegheny != null && alleyAfter.taxes == 0 && alleyBefore.taxes == 0;

			result.Status = empireOk && alleyOk ? "PASS" : "FAIL";
			row.Status = result.Status;
			result.Summary = result.Status == "PASS"
				? "sliders 100% sci -> 90/10 move exactly 1 commerce sci->luxury, treasury untouched"
				: $"expected empire deltas (-1,+1,0) and Allegheny taxes=0; got ({dBeakers},{dHappiness},{dTaxes}), alley taxes {alleyAfter.taxes}";
			if (result.Status == "FAIL") {
				result.Failure = row.Detail;
			}
		} finally {
			human.scienceRate = sci; human.luxuryRate = lux; human.taxRate = tax;
		}
	}

	private static CommerceBreakdown TotalCommerce(Player player) {
		CommerceBreakdown total = default;
		foreach (City city in player.cities) {
			CommerceBreakdown c = city.CurrentCommerceYield();
			total.beakers += c.beakers;
			total.happiness += c.happiness;
			total.taxes += c.taxes;
			total.wealth += c.wealth;
			total.corrupted += c.corrupted;
		}
		return total;
	}
}
