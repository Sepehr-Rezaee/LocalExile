namespace GameServer.Gateway.Tests;

public sealed class KillRecoveryTests
{
    [Fact]
    public void Kill_recovery_comes_from_flat_per_level_and_percent_stats()
    {
        var raw = new Dictionary<string, int>
        {
            ["base_life_gained_on_enemy_death"] = 10, ["life_gained_on_enemy_death_per_level"] = 1,
            ["recover_%_maximum_life_on_kill"] = 3, ["base_mana_gained_on_enemy_death"] = 4,
            ["recover_%_maximum_mana_on_kill"] = 2, ["maximum_mana_%_gained_on_kill"] = 1,
            ["base_energy_shield_gained_on_enemy_death"] = 7, ["recover_energy_shield_%_on_kill"] = 5,
        };
        Assert.Equal(new KillRecovery(30, 3, 4, 3, 7, 5), KillRecovery.From(raw.GetValueOrDefault, level: 20));
        raw["cannot_recover_life"] = 1;
        raw["cannot_recover_energy_shield"] = 1;
        Assert.Equal(new KillRecovery(0, 0, 4, 3, 0, 0), KillRecovery.From(raw.GetValueOrDefault, level: 20));
    }

    [Fact]
    public void A_kill_recovers_at_once_and_a_dead_player_gains_nothing()
    {
        var conn = new ClientState(new(1, "kill", 300, 300, 100, 100, null, []), 1, 1)
        {
            Pools = new(10, 1000, 200, 2, 0), PlayerLife = 500, PlayerMana = 50,
        };
        Game.LeechOf(conn).OnKill = new KillRecovery(10, 2, 5, 3, 0, 0);
        Game.RecoverOnKill(conn);
        Assert.Equal(530u, conn.PlayerLife);   // 10 + 2% of 1000
        Assert.Equal(61u, conn.PlayerMana);    // floor(5 + 3% of 200)
        Assert.True(Game.LeechOf(conn).InstantLifeChanged && Game.LeechOf(conn).InstantManaChanged);
        conn.PlayerLife = 0;
        Game.RecoverOnKill(conn);
        Assert.Equal(0u, conn.PlayerLife);
        Assert.Equal(61u, conn.PlayerMana);
    }
}
