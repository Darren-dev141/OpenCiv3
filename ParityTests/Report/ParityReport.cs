using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ParityTests.Checks;
using ParityTests.Fixtures;
using Newtonsoft.Json;

namespace ParityTests.Report;

/// <summary>
/// Aggregates all parity check results into parity-report.json / .md.
/// </summary>
public class ParityReport {
	public string GeneratedAtUtc { get; set; }
	public string Civ3Home { get; set; }
	public bool Civ3Available { get; set; }
	public List<CheckResult> Results { get; set; } = new();

	public static ParityReport RunAll(FixtureStore store) {
		ParityReport report = new() {
			GeneratedAtUtc = DateTime.UtcNow.ToString("o"),
			Civ3Home = store.Civ3Home,
			Civ3Available = store.IsAvailable,
		};

		foreach (IParityCheck check in ParityChecks.All) {
			try {
				report.Results.Add(check.Run(store));
			} catch (Exception ex) {
				CheckResult result = check.BaseResult();
				result.Status = "FAIL";
				result.Summary = "check threw: " + ex.GetType().Name;
				result.Failure = ex.Message;
				report.Results.Add(result);
			}
		}
		return report;
	}

	public string ToJson() {
		return JsonConvert.SerializeObject(this, Formatting.Indented);
	}

	public string ToMarkdown() {
		StringBuilder sb = new();
		sb.AppendLine("# Parity report");
		sb.AppendLine();
		sb.AppendLine($"Generated: {GeneratedAtUtc}");
		sb.AppendLine($"CIV3_HOME: {(string.IsNullOrEmpty(Civ3Home) ? "(unset)" : Civ3Home)} (available: {Civ3Available})");
		sb.AppendLine();
		sb.AppendLine("| Check | Kind | Matrix row | Issue | Status | Summary |");
		sb.AppendLine("|---|---|---|---|---|---|");
		foreach (CheckResult r in Results) {
			sb.AppendLine($"| {r.Id} | {r.Kind} | {r.MatrixRow} | {r.SpaIssue} | {r.Status} | {Escape(r.Summary)} |");
		}
		sb.AppendLine();
		foreach (CheckResult r in Results.Where(r => r.Fixtures.Count > 0)) {
			sb.AppendLine($"## {r.Id} — {r.Status}");
			sb.AppendLine();
			if (!string.IsNullOrEmpty(r.Failure)) {
				sb.AppendLine($"First divergence: {r.Failure}");
				sb.AppendLine();
			}
			sb.AppendLine("| Fixture | Status | Raw | Imported | Detail |");
			sb.AppendLine("|---|---|---|---|---|");
			foreach (FixtureResult f in r.Fixtures) {
				sb.AppendLine($"| {f.FixtureId} | {f.Status} | {f.RawValue?.ToString() ?? "-"} | {f.ImportedValue?.ToString() ?? "-"} | {Escape(f.Detail)} |");
			}
			sb.AppendLine();
		}
		return sb.ToString();
	}

	private static string Escape(string s) {
		if (s == null) return "";
		return s.Replace("|", "\\|").Replace("\n", " ");
	}
}
