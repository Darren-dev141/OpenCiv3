using System.Collections.Generic;
using ParityTests.Fixtures;

namespace ParityTests.Checks;

public enum CheckKind {
	A, // Import fidelity: raw value vs imported value, exact
	B, // Derived state: recompute from loaded state vs stored original value
	C, // Transition: paired saves / scripted action comparison
	D, // Statistical: frequency vs documented table within predeclared band
	E, // Census: generator output properties vs original-game census
}

public enum ParityStatus {
	Pass,
	Fail,
	Skip,
	NoReference,
}

public static class ParityStatusText {
	public static string Of(ParityStatus status) => status switch {
		ParityStatus.Pass => "PASS",
		ParityStatus.Fail => "FAIL",
		ParityStatus.Skip => "SKIP",
		ParityStatus.NoReference => "NO-REFERENCE",
		_ => status.ToString(),
	};
}

/// <summary>Per-fixture row inside a check result.</summary>
public class FixtureResult {
	public string FixtureId { get; set; }
	public string Status { get; set; }       // PASS / FAIL / SKIP / ERROR
	public string Detail { get; set; }
	public long? RawValue { get; set; }
	public long? ImportedValue { get; set; }
}

public class CheckResult {
	public string Id { get; set; }
	public string MatrixRow { get; set; }
	public string SpaIssue { get; set; }
	public string Kind { get; set; }
	public string Reference { get; set; }
	public string Tolerance { get; set; }
	public string Status { get; set; }
	public string Summary { get; set; }
	public List<FixtureResult> Fixtures { get; set; } = new();
	public string Failure { get; set; }

	public static CheckResult Skip(IParityCheck check, string reason) {
		return new CheckResult {
			Id = check.Id,
			MatrixRow = check.MatrixRow,
			SpaIssue = check.SpaIssue,
			Kind = check.Kind.ToString(),
			Reference = check.Reference,
			Tolerance = check.Tolerance,
			Status = ParityStatusText.Of(ParityStatus.Skip),
			Summary = reason,
		};
	}
}

/// <summary>
/// One parity check. Id, MatrixRow, SpaIssue, Kind, Reference and Tolerance are
/// all declared up front, before the check runs.
/// </summary>
public interface IParityCheck {
	string Id { get; }
	string MatrixRow { get; }
	string SpaIssue { get; }
	CheckKind Kind { get; }
	string Reference { get; }
	string Tolerance { get; }
	CheckResult Run(FixtureStore store);
}

public static class ParityCheckExtensions {
	/// <summary>A result pre-filled with the check's declared metadata.</summary>
	public static CheckResult BaseResult(this IParityCheck check) {
		return new CheckResult {
			Id = check.Id,
			MatrixRow = check.MatrixRow,
			SpaIssue = check.SpaIssue,
			Kind = check.Kind.ToString(),
			Reference = check.Reference,
			Tolerance = check.Tolerance,
		};
	}
}
