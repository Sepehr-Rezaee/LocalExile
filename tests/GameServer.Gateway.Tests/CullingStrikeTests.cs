namespace GameServer.Gateway.Tests;

public sealed class CullingStrikeTests
{
    static Dictionary<uint, int> With(params (string Name, int Value)[] stats) =>
        stats.ToDictionary(s => Program.Stats.Key(s.Name)!.Value, s => s.Value);

    [Fact]
    public void Culling_threshold_comes_from_the_character_and_the_skill()
    {
        Assert.Equal(default, Game.Cull.Of(null, null, critical: true, spell: true));
        // The support: its implicit stat and its quality's life recovery.
        Assert.Equal(new Game.Cull(10, 2), Game.Cull.Of(null,
            With(("kill_enemy_on_hit_if_under_10%_life", 1), ("recover_%_maximum_life_on_cull", 2)), false, false));
        var crits = With(("crits_have_culling_strike", 1));
        Assert.Equal(0, Game.Cull.Of(crits, null, critical: false, spell: false).Threshold);
        Assert.Equal(10, Game.Cull.Of(crits, null, critical: true, spell: false).Threshold);
        var spells = With(("spells_have_culling_strike", 1));
        Assert.Equal(0, Game.Cull.Of(spells, null, critical: false, spell: false).Threshold);
        Assert.Equal(10, Game.Cull.Of(spells, null, critical: false, spell: true).Threshold);
        Assert.Equal(20, Game.Cull.Of(With(("kill_enemy_on_hit_if_under_20%_life", 1)),
            With(("kill_enemy_on_hit_if_under_10%_life", 1)), false, false).Threshold);
    }

    [Fact]
    public void A_hit_that_leaves_the_enemy_at_the_threshold_kills_it()
    {
        var conn = new ClientState(new(1, "cull", 300, 300, 100, 100, null, []), 1, 1)
        {
            Pools = new(1, 1000, 100, 2, 0), PlayerLife = 500,
        };
        var cull = new Game.Cull(10, 2);
        SpawnedMonster Monster(MonsterModSet? mods = null) => new("cull target", new(100, 100, 0), 0, 0, 1000)
            { Mods = mods ?? MonsterModSet.None };

        var above = Monster();
        Assert.Equal(101u, Game.TakeOwnerHitLocked(conn, above, 899, cull).Life);
        Assert.Equal(500u, conn.PlayerLife);

        var at = Monster();
        Assert.Equal(0u, Game.TakeOwnerHitLocked(conn, at, 900, cull).Life);
        Assert.Equal(520u, conn.PlayerLife);   // 2% of 1000 maximum Life

        var immune = Monster(new([], [], new(new Dictionary<string, int> { ["immune_to_culling_strike"] = 1 })));
        Assert.Equal(100u, Game.TakeOwnerHitLocked(conn, immune, 900, cull).Life);
        Assert.Equal(100u, Game.TakeOwnerHitLocked(conn, Monster(), 900, default).Life);
    }
}
