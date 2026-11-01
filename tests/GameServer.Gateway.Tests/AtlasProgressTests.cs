using GameServer.Modding;
using GameServer.Modules.Atlas;

namespace GameServer.Gateway.Tests;

/// <summary>The Atlas's completed maps (21-hideouts.md §4): the nodes from AtlasNode.dat and the S→C 0x1a3 / 0x1a4 messages.</summary>
public sealed class AtlasProgressTests
{
    sealed class Data : ISavedData
    {
        readonly Dictionary<string, string> _values = [];
        public string? Get(string key) => _values.GetValueOrDefault(key);
        public bool Change(string key, Func<string?, string?> change)
        {
            if (change(Get(key)) is not { } value) return false;
            _values[key] = value;
            return true;
        }
    }

    static AtlasProgress.Node Chateau => AtlasProgress.Find("Chateau")!;

    [Fact]
    public void MapsAreAtlasNodeRowsWithTheirAreasAndBosses()
    {
        Assert.Equal((97, "MapWorldsChateau", (ushort)0x9a23), (Chateau.Row, Chateau.Id, Chateau.Hash16));
        Assert.Same(Chateau, AtlasProgress.OfArea("MapWorldsChateau"));
        Assert.Contains("Metadata/Monsters/GargoyleBoss/GargoyleMapBoss", Chateau.Bosses);
        Assert.All(AtlasProgress.Maps, n => Assert.InRange(n.Row, 0, 169));
        Assert.Null(AtlasProgress.OfArea("1_1_town"));
    }

    [Fact]
    public void OnlyTheBossOrAUniqueCompletesAMap()
    {
        Assert.True(AtlasProgress.CompletesMap(Chateau,
            new("Metadata/Monsters/GargoyleBoss/GargoyleMapBoss", "Boss", 68, MonsterRarity.Rare, 0, 0)));
        Assert.True(AtlasProgress.CompletesMap(Chateau, new("Metadata/Monsters/Other", "Unique", 68, MonsterRarity.Unique, 0, 0)));
        Assert.False(AtlasProgress.CompletesMap(Chateau, new("Metadata/Monsters/Other", "Rare", 68, MonsterRarity.Rare, 0, 0)));
    }

    [Fact]
    public void CompletedMapsAreSavedOnce()
    {
        var data = new Data();
        Assert.True(AtlasProgress.Complete(data, [Chateau]));
        Assert.False(AtlasProgress.Complete(data, [Chateau]));
        Assert.Equal(["MapWorldsChateau"], AtlasProgress.Completed(data));
        Assert.True(AtlasProgress.Reset(data));
        Assert.Empty(AtlasProgress.Completed(data));
        Assert.False(AtlasProgress.Reset(data));
    }

    [Fact]
    public void MessagesCarryTheNodesRowMasksAndHash()
    {
        byte[] full = AtlasProgress.FullMessage(new HashSet<string> { "MapWorldsChateau" });
        Assert.Equal(7 + 2 * 22, full.Length);
        Assert.Equal(new byte[] { 0x01, 0xa3, 0, 0, 0, 0x03, 0 }, full[..7]);
        Assert.Equal(1 << (97 & 7), full[7 + 97 / 8]);
        Assert.Equal(1 << (97 & 7), full[7 + 22 + 97 / 8]);
        Assert.Equal(2, full[7..].Count(b => b != 0));
        Assert.Equal(new byte[] { 0x01, 0xa4, 0x9a, 0x23, 0, 0, 0x02 }, AtlasProgress.NodeMessage(Chateau));
    }
}
