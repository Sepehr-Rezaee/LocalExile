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
            ["life_gained_on_killing_ignited_enemies"] = 20, ["recover_%_maximum_life_on_killing_chilled_enemy"] = 1,
            ["gain_X_energy_shield_on_killing_shocked_enemy"] = 9, ["recover_X_mana_on_killing_frozen_enemy"] = 6,
        };
        var k = KillRecovery.From(raw.GetValueOrDefault, level: 20);
        Assert.Equal(new KillRecovery(30, 3, 4, 3, 7, 5), k.For(false, false, false, false));
        Assert.Equal(new KillRecovery(50, 4, 10, 3, 16, 5), k.For(true, true, true, true));
        Assert.Equal(new KillRecovery(30, 3, 10, 3, 7, 5), k.For(false, false, false, frozen: true));
        raw["cannot_recover_life"] = 1;
        raw["cannot_recover_energy_shield"] = 1;
        Assert.Equal(new KillRecovery(0, 0, 10, 3, 0, 0), KillRecovery.From(raw.GetValueOrDefault, level: 20).For(true, true, true, true));
    }

    [Fact]
    public void A_kill_recovers_at_once_by_the_corpses_ailments_and_a_dead_player_gains_nothing()
    {
        var conn = new ClientState(new(1, "kill", 300, 300, 100, 100, null, []), 1, 1)
        {
            Pools = new(10, 1000, 200, 2, 0), PlayerLife = 500, PlayerMana = 50,
        };
        Game.LeechOf(conn).OnKill = new KillRecovery(10, 2, 5, 3, 0, 0) { When = new(IgnitedLife: 7, 0, 0, FrozenMana: 4) };
        var corpse = new SpawnedMonster("kill", new(100, 100, 0), 0, 0, 100);
        Game.RecoverOnKill(conn, corpse);
        Assert.Equal(530u, conn.PlayerLife);   // 10 + 2% of 1000
        Assert.Equal(61u, conn.PlayerMana);    // floor(5 + 3% of 200); it was not frozen
        Assert.True(Game.LeechOf(conn).InstantLifeChanged && Game.LeechOf(conn).InstantManaChanged);
        corpse.Ailments.ShockOnKill(0, _ => 0, _ => 0);   // Within the Tempest shocks the corpse after the death
        Assert.True(corpse.Ailments.Magnitude(MonsterElementalAilments.Kind.Shock, 1) > 0);
        Assert.False(corpse.Ailments.ShockedAtDeath(1));
        corpse.Ailments.Shatter();             // a shattered kill was of a frozen enemy
        corpse.Ignites.Clear(burnt: true);     // and its ignite's last tick killed it
        Game.RecoverOnKill(conn, corpse);
        Assert.Equal(567u, conn.PlayerLife);   // 530 + 10 + 7 + 2% of 1000
        Assert.Equal(76u, conn.PlayerMana);    // floor(5 + 4 + 3% of 200)
        conn.PlayerLife = 0;
        Game.RecoverOnKill(conn, corpse);
        Assert.Equal(0u, conn.PlayerLife);
        Assert.Equal(76u, conn.PlayerMana);
    }
}
