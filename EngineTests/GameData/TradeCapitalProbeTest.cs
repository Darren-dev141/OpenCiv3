using System.Linq;
using System.Collections.Generic;
using C7Engine;
using C7GameData;
using C7Engine.Pathing;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class TradeCapitalProbeTest : IClassFixture<SaveGameFixture> {
    private readonly C7GameData.GameData game;
    private readonly Player player;
    public TradeCapitalProbeTest(SaveGameFixture fixture) {
        game = fixture.saveGame.ToGameData(fixture.behaviors);
        EngineStorage.InitializeGameDataForTests(game);
        player = game.players.First(p => p.isHuman);
    }
    private City Found(string name) {
        Tile start = player.units[0].location;
        Tile site = game.map.tiles.Where(t => t.IsLand() && t.cityAtTile == null)
            .Where(t => game.cities.All(c => c.location.DistanceTo(t) >= 3))
            .OrderBy(t => t.DistanceTo(start)).First();
        return CityInteractions.BuildCity(site, player, name);
    }
    [Fact]
    public void RelocatedCapitalIsConnectedToItself() {
        City original = Found("Original");
        City earlier = Found("Earlier");
        City newCapital = Found("NewCapital");
        for (int i = 0; i < 4; i++)
            newCapital.AddCitizen(new CityResident { city = newCapital, citizenType = game.citizenTypes.Find(x => x.IsDefaultCitizen) });
        Assert.True(original.IsCapital());
        var network = game.GetTradeNetwork();
        Assert.True(network.ConnectedToCapital(player, original));
        CityInteractions.DestroyCity(original);
        Assert.True(newCapital.IsCapital());
        Assert.Same(earlier, player.cities[0]);
        network = game.GetTradeNetwork();
        Assert.True(network.ConnectedToCapital(player, newCapital));
        Assert.False(network.ConnectedToCapital(player, earlier));

        // Connect the cities with roads, then build a fresh network.
        var previous = new Dictionary<Tile, Tile> { [earlier.location] = null };
        var queue = new Queue<Tile>();
        queue.Enqueue(earlier.location);
        while (queue.Count > 0 && !previous.ContainsKey(newCapital.location)) {
            Tile current = queue.Dequeue();
            foreach (Tile next in current.neighbors.Values.Where(t => t.IsLand())) {
                if (previous.ContainsKey(next)) continue;
                previous[next] = current;
                queue.Enqueue(next);
            }
        }
        Assert.True(previous.ContainsKey(newCapital.location));
        var road = new TerrainImprovement("road", TerrainImprovement.Layer.Roads, movementCost: 1.0f / 3);
        var roadPath = new List<Tile>();
        for (Tile tile = newCapital.location; tile != null; tile = previous[tile]) {
            roadPath.Add(tile);
            if (!tile.IsRoaded()) tile.overlays.Add(road);
        }
        network = game.GetTradeNetwork();
        Assert.True(network.ConnectedToCapital(player, earlier));

        // Cutting an intermediate road must invalidate the cached network.
        Assert.True(roadPath.Count > 2);
        Tile cut = roadPath[roadPath.Count / 2];
        Assert.True(cut.HasRoad());
        cut.overlays.Remove(road);
        Assert.False(game.GetTradeNetwork().ConnectedToCapital(player, earlier));
    }
    private void ConnectWithRoads(City a, City b) {
        var previous = new Dictionary<Tile, Tile> { [a.location] = null };
        var queue = new Queue<Tile>();
        queue.Enqueue(a.location);
        while (queue.Count > 0 && !previous.ContainsKey(b.location)) {
            Tile current = queue.Dequeue();
            foreach (Tile next in current.neighbors.Values.Where(t => t.IsLand())) {
                if (previous.ContainsKey(next)) continue;
                previous[next] = current;
                queue.Enqueue(next);
            }
        }
        Assert.True(previous.ContainsKey(b.location));
        var road = new TerrainImprovement("road", TerrainImprovement.Layer.Roads, movementCost: 1.0f / 3);
        for (Tile tile = b.location; tile != null; tile = previous[tile]) {
            if (!tile.IsRoaded()) tile.overlays.Add(road);
        }
    }
    [Fact]
    public void TradeAccessToCapitalReducesCorruption() {
        City capital = Found("Capital");
        City remote = Found("Remote");
        player.DoCorruptionCalculations(game);
        float withoutRoads = remote.corruption;
        Assert.True(withoutRoads > 0);

        ConnectWithRoads(capital, remote);
        Assert.True(game.GetTradeNetwork().ConnectedToCapital(player, remote));

        player.DoCorruptionCalculations(game);
        Assert.True(remote.corruption < withoutRoads);
    }
    [Fact]
    public void WithoutCapitalNoCityIsConnectedToIt() {
        City city = Found("Capital");
        Assert.True(city.IsCapital());
        city.capital = false;
        var network = new TradeNetwork(game);
        Assert.False(network.ConnectedToCapital(player, city));
    }
}
