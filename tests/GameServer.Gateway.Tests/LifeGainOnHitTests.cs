using GameServer.Core.World;

namespace GameServer.Gateway.Tests;

public sealed class LifeGainOnHitTests
{
    static readonly Dictionary<string, int> Raw = new()
    {
        ["base_life_gain_per_target"] = 44, ["mana_gain_per_target"] = 3, ["energy_shield_gain_per_target"] = 2,
    };
    static readonly Dictionary<string, int> Weapon = new()
    {
        ["local_life_gain_per_target"] = 5, ["local_mana_gain_per_target"] = 1, ["local_life_and_mana_gain_per_target"] = 10,
    };

    [Fact]
    public void Gain_per_enemy_hit_comes_from_attacks_and_their_weapon()
    {
        Assert.Equal((59, 14, 2), LeechSources.From(Raw.GetValueOrDefault, attack: true, Weapon.GetValueOrDefault).GainOnHit);
        Assert.Equal((44, 3, 2), LeechSources.From(Raw.GetValueOrDefault, attack: true).GainOnHit);
        Assert.Equal(default, LeechSources.From(Raw.GetValueOrDefault, attack: false, Weapon.GetValueOrDefault).GainOnHit);
    }

    [Fact]
    public void Every_enemy_hit_grants_its_gain_at_once()
    {
        var conn = new ClientState(new(1, "gain", 300, 300, 100, 100, null, []), 1, 1)
        {
            Pools = new(1, 1000, 100, 2, 0), PlayerLife = 900, PlayerMana = 50,
        };
        var sources = LeechSources.From(Raw.GetValueOrDefault, attack: true, Weapon.GetValueOrDefault);
        var first = new SpawnedMonster("first", new(100, 100, 0), 0, 0, 100);
        var second = new SpawnedMonster("second", new(100, 100, 0), 0, 0, 100);
        // A hit that deals no damage still hits: the gain does not scale with damage.
        Game.LeechOnHitLocked(conn, first, DamagePacket.Empty, 0, sources);
        Game.LeechOnHitLocked(conn, second, DamagePacket.Empty, 0, sources);
        Assert.Equal(1000u, conn.PlayerLife);   // 900 + 2 × 59, capped at maximum Life
        Assert.Equal(78u, conn.PlayerMana);
        Assert.True(Game.LeechOf(conn).InstantLifeChanged && Game.LeechOf(conn).InstantManaChanged);
        Game.LeechOnHitLocked(conn, new("dead", new(100, 100, 0), 0, 0, 100) { Dead = true }, DamagePacket.Empty, 0, sources);
        Assert.Equal(78u, conn.PlayerMana);
    }
}
