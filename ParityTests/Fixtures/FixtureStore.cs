using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace ParityTests.Fixtures;

/// <summary>
/// A fixture is a real Civ3 (or community/OpenCiv3) save/scenario file that the
/// harness compares against. Game files are never committed: install fixtures
/// are referenced by CIV3_HOME-relative path + sha256, remote fixtures by URL +
/// sha256 and downloaded to a local cache at test time.
/// </summary>
public class Fixture {
	public string Id { get; set; }
	public string Kind { get; set; }        // "sav" | "biq-base" | "biq-scenario" | "remote-save"
	public string Source { get; set; }      // "install" | "url"
	public string InstallPath { get; set; } // relative to CIV3_HOME, forward slashes
	public string Url { get; set; }
	public string Sha256 { get; set; }
	public string Note { get; set; }
}

public class FixtureManifest {
	public List<Fixture> Fixtures { get; set; } = new();
}

/// <summary>
/// A fixture resolved to bytes on disk (or a download in progress).
/// </summary>
public class ResolvedFixture {
	public Fixture Fixture { get; set; }
	public string LocalPath { get; set; }
	public byte[] Bytes { get; set; }
	public string Error { get; set; }
}

/// <summary>
/// Resolves manifest fixtures against CIV3_HOME (or the remote cache),
/// verifying sha256. When CI is set or the install is absent the store reports
/// itself unavailable and checks must SKIP.
/// </summary>
public class FixtureStore {
	public string Civ3Home { get; }
	public string CacheDir { get; }
	public bool SimulateCi { get; }
	private readonly Lazy<FixtureManifest> _manifest;

	private static readonly string[] RequiredInstallFiles = {
		"Conquests/conquests.biq",
		"Conquests/Text/PediaIcons.txt",
		"Conquests/Text/Civilopedia.txt",
	};

	public FixtureStore(string civ3Home, string cacheDir, bool simulateCi = false) {
		Civ3Home = civ3Home;
		CacheDir = cacheDir;
		SimulateCi = simulateCi;
		_manifest = new Lazy<FixtureManifest>(LoadManifest);
	}

	public static FixtureStore FromEnvironment() {
		string civ3Home = Environment.GetEnvironmentVariable("CIV3_HOME");
		bool ci = Environment.GetEnvironmentVariable("CI") != null;
		string cacheDir = Path.Combine(Path.GetTempPath(), "openciv3-parity-cache");
		return new FixtureStore(civ3Home, cacheDir, ci);
	}

	public bool IsAvailable {
		get {
			if (SimulateCi || string.IsNullOrEmpty(Civ3Home)) return false;
			return RequiredInstallFiles.All(f => File.Exists(InstallPath(f)));
		}
	}

	public FixtureManifest Manifest => _manifest.Value;

	public string InstallPath(string relativePath) {
		return Path.Combine(Civ3Home, relativePath.Replace('/', Path.DirectorySeparatorChar));
	}

	private static FixtureManifest LoadManifest() {
		string path = Utils.RepoPaths.ManifestPath;
		string json = File.ReadAllText(path);
		return JsonConvert.DeserializeObject<FixtureManifest>(json);
	}

	public IEnumerable<Fixture> InstallFixtures() {
		return Manifest.Fixtures.Where(f => f.Source == "install");
	}

	public IEnumerable<Fixture> RemoteFixtures() {
		return Manifest.Fixtures.Where(f => f.Source == "url");
	}

	public IEnumerable<Fixture> SavFixtures() {
		return InstallFixtures().Where(f => f.Kind == "sav");
	}

	// The 28 Conquest single-player + multiplayer scenario BIQs (excludes the
	// base conquests.biq, which is tagged "biq-base").
	public IEnumerable<Fixture> ScenarioBiqFixtures() {
		return InstallFixtures().Where(f => f.Kind == "biq-scenario");
	}

	public IEnumerable<Fixture> AllBiqFixtures() {
		return InstallFixtures().Where(f => f.Kind == "biq-base" || f.Kind == "biq-scenario");
	}

	/// <summary>
	/// Reads a fixture's bytes, verifying the manifest sha256. Returns a
	/// ResolvedFixture with Error set instead of throwing when the file is
	/// missing or the hash does not match.
	/// </summary>
	public ResolvedFixture Resolve(Fixture fixture) {
		ResolvedFixture result = new() { Fixture = fixture };
		try {
			string path;
			if (fixture.Source == "install") {
				if (string.IsNullOrEmpty(Civ3Home)) {
					result.Error = "CIV3_HOME is not set";
					return result;
				}
				path = InstallPath(fixture.InstallPath);
			} else {
				path = CachePath(fixture);
				if (!File.Exists(path)) {
					Download(fixture, path);
				}
			}

			if (!File.Exists(path)) {
				result.Error = $"fixture file not found: {path}";
				return result;
			}

			result.LocalPath = path;
			result.Bytes = File.ReadAllBytes(path);
			string actual = Sha256Hex(result.Bytes);
			if (!string.Equals(actual, fixture.Sha256, StringComparison.OrdinalIgnoreCase)) {
				result.Error = $"sha256 mismatch: expected {fixture.Sha256}, got {actual}";
				result.Bytes = null;
			}
		} catch (Exception ex) {
			result.Error = ex.Message;
			result.Bytes = null;
		}
		return result;
	}

	public string CachePath(Fixture fixture) {
		string name = fixture.Id + Path.GetExtension(fixture.Url ?? fixture.InstallPath ?? "bin");
		return Path.Combine(CacheDir, name);
	}

	private void Download(Fixture fixture, string path) {
		Directory.CreateDirectory(CacheDir);
		using var client = new System.Net.Http.HttpClient();
		byte[] data = client.GetByteArrayAsync(fixture.Url).GetAwaiter().GetResult();
		File.WriteAllBytes(path, data);
	}

	public static string Sha256Hex(byte[] bytes) {
		using SHA256 sha = SHA256.Create();
		byte[] hash = sha.ComputeHash(bytes);
		StringBuilder sb = new(hash.Length * 2);
		foreach (byte b in hash) sb.Append(b.ToString("x2"));
		return sb.ToString();
	}
}
