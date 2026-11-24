using System;
using System.IO;
using ParityTests.Fixtures;
using ParityTests.Report;

namespace ParityRunner;

/// <summary>
/// Runs every parity check and writes parity-report.json + parity-report.md.
/// Usage: dotnet run --project _Console/ParityRunner -- [--report outdir]
/// </summary>
public static class Program {
	public static int Main(string[] args) {
		string outDir = "out";
		for (int i = 0; i < args.Length - 1; i++) {
			if (args[i] == "--report") outDir = args[i + 1];
		}
		Directory.CreateDirectory(outDir);

		FixtureStore store = FixtureStore.FromEnvironment();
		ParityReport report = ParityReport.RunAll(store);

		string jsonPath = Path.Combine(outDir, "parity-report.json");
		string mdPath = Path.Combine(outDir, "parity-report.md");
		File.WriteAllText(jsonPath, report.ToJson());
		File.WriteAllText(mdPath, report.ToMarkdown());

		foreach (var r in report.Results) {
			Console.WriteLine($"{r.Id,-6} {r.Status,-13} {r.Summary}");
		}
		Console.WriteLine($"Wrote {jsonPath}");
		Console.WriteLine($"Wrote {mdPath}");
		return 0;
	}
}
