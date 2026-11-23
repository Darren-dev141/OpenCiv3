using System;
using System.IO;
using System.Linq;

namespace ParityTests.Utils;

/// <summary>
/// Locates the repository root and install-relative paths without relying on
/// EngineTests' PathUtils (which resolves paths relative to the EngineTests
/// output directory and does not work from this project).
/// </summary>
public static class RepoPaths {
	public static string RepoRoot {
		get {
			DirectoryInfo dir = new(AppContext.BaseDirectory);
			while (dir != null) {
				if (Directory.Exists(Path.Combine(dir.FullName, "C7Engine"))
					&& Directory.Exists(Path.Combine(dir.FullName, "QueryCiv3"))
					&& Directory.Exists(Path.Combine(dir.FullName, "C7", "Lua"))) {
					return dir.FullName;
				}
				dir = dir.Parent;
			}
			throw new DirectoryNotFoundException("Could not locate the OpenCiv3 repository root.");
		}
	}

	// Game mode definitions live in C7/Lua (same as EngineTests PathUtils.GameModesDir).
	public static string GameModesDir => Path.Combine(RepoRoot, "C7", "Lua");

	public static string ManifestPath => Path.Combine(RepoRoot, "ParityTests", "Fixtures", "manifest.json");
}
