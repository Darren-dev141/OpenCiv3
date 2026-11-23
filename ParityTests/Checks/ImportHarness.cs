using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using C7GameData;
using C7GameData.Save;
using ParityTests.Fixtures;
using QueryCiv3;

namespace ParityTests.Checks;

/// <summary>
/// Shared helpers for running the C7Engine importer over manifest fixtures.
/// </summary>
public static class ImportHarness {
	public const string GoodyHutOverlay = Tile.TileOverlays.GOODY_HUT;

	public static string DefaultBiqPath(FixtureStore store) {
		return store.InstallPath("Conquests/conquests.biq");
	}

	public static byte[] DefaultBiqBytes(FixtureStore store) {
		return File.ReadAllBytes(DefaultBiqPath(store));
	}

	// Mirrors EngineTests SaveTest: scenario BIQs resolve PediaIcons/Civilopedia
	// relative to their own Conquests subfolder; anything else falls back to the
	// base Conquests text files.
	public static Func<string, string> GetPediaIconsPath(FixtureStore store, string scenarioSubfolder) {
		return relativeModPath => {
			if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
				relativeModPath = relativeModPath.Replace("\\conquests\\", "/Conquests/");
			}
			string combined = Path.GetFullPath(Path.Combine(
				store.Civ3Home, scenarioSubfolder, relativeModPath, "Text", "PediaIcons.txt"));
			if (File.Exists(combined)) return combined;
			return Path.Combine(store.Civ3Home, "Conquests", "Text", "PediaIcons.txt");
		};
	}

	public static Func<string, string> GetCivilopediaTextPath(FixtureStore store) {
		return _ => Path.Combine(store.Civ3Home, "Conquests", "Text", "Civilopedia.txt");
	}

	// Subfolder under CIV3_HOME that contains a fixture's scenario text files.
	public static string ScenarioSubfolder(ResolvedFixture fixture) {
		string dir = Path.GetDirectoryName(fixture.Fixture.InstallPath)?.Replace('\\', '/');
		return dir switch {
			"Conquests/Conquests" => "Conquests/Conquests",
			"Conquests/Scenarios" => "Conquests/Scenarios",
			_ => "Conquests",
		};
	}

	public static SaveGame ImportFixture(FixtureStore store, ResolvedFixture fixture) {
		if (fixture.Fixture.Kind == "sav") {
			return ImportCiv3.ImportSav(fixture.LocalPath, DefaultBiqPath(store),
				GetPediaIconsPath(store, "Conquests"), GetCivilopediaTextPath(store));
		}
		return ImportCiv3.ImportBiq(fixture.LocalPath, DefaultBiqPath(store),
			GetPediaIconsPath(store, ScenarioSubfolder(fixture)), GetCivilopediaTextPath(store));
	}

	public static int CountGoodyHutTiles(SaveGame save) {
		return save.Map.tiles.Count(t => t.overlays.Contains(GoodyHutOverlay));
	}
}
