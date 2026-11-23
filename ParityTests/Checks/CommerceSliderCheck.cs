using System;
using ParityTests.Fixtures;

namespace ParityTests.Checks;

/// <summary>
/// CM-C1 (kind C, transition/regression): reproduces https://github.com/C7-Game/OpenCiv3/issues/1080.
/// In the attached save, moving the sliders from 100% science to 90% science /
/// 10% luxury must move exactly one commerce per turn from science to
/// entertainment and leave the treasury untouched (city science 27 -> 26,
/// entertainment +1, treasury 0). The save is fetched from the issue
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

	public CheckResult Run(FixtureStore store) {
		throw new NotImplementedException();
	}
}
