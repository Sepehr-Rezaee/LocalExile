namespace GameServer.Gateway.Tests;

public sealed class OnslaughtOnKillTests
{
    [Fact]
    public void Each_onslaught_on_kill_stat_rolls_its_own_duration_and_never_shortens_one()
    {
        var raw = new Dictionary<string, int>
        {
            ["chance_to_gain_onslaught_on_kill_%"] = 10, ["chance_to_gain_onslaught_on_kill_for_10_seconds_%"] = 100,
        };
        Assert.Equal([(10, 4.0), (100, 10.0)], KillOnslaught.From(raw.GetValueOrDefault));
        raw["onslaught_time_granted_on_kill_ms"] = 6000;
        raw["onslaught_buff_duration_on_kill_ms"] = 8000;       // Maraketh: always, for 8 s
        Assert.Equal([(10, 6.0), (100, 8.0), (100, 10.0)], KillOnslaught.From(raw.GetValueOrDefault));

        var o = new KillOnslaught { Rolls = [(100, 4.0)] };
        Assert.True(o.Kill(0, new Random(1)));
        Assert.Equal(4, o.Until);
        Assert.True(o.Kill(3, new Random(1)));       // 1 s left: a new 4 s gain lengthens it
        Assert.Equal(7, o.Until);
        o.Rolls = [(100, 4.0)];
        o.Until = 20;
        Assert.False(o.Kill(10, new Random(1)));     // 10 s left: a 4 s gain does not shorten it
        Assert.Equal(20, o.Until);
        Assert.False(new KillOnslaught { Rolls = [] }.Kill(0, new Random(1)));
    }

    [Fact]
    public void A_kills_onslaught_applies_once_told_and_counts_once_with_a_flasks()
    {
        var conn = new ClientState(new(1, "onslaught", 300, 300, 100, 100, null, []), 1, 1)
        {
            Pools = new(10, 1000, 200, 2, 0), PlayerLife = 500,
        };
        Game.KillOnslaughtOf(conn).Rolls = [(100, 4.0)];
        Assert.True(Game.OnslaughtOnKill(conn));
        Assert.Empty(Game.KillOnslaughtSources(conn, _ => 0));    // not until the client has the buff
        var o = Game.KillOnslaughtOf(conn);
        double now = o.Until - 4;                                // granted for 4 s at the kill
        Assert.Equal(((ushort)0, (double?)4, true), o.Present(now, alive: true, hidden: false));
        o.Buff = 9;
        Assert.Null(o.Present(now + 1, alive: true, hidden: false));               // nothing new to send
        Assert.Equal(((ushort)9, (double?)null, false), o.Present(now + 1, alive: true, hidden: true)); // a flask's shows
        var kill = Game.KillOnslaughtSources(conn, name => name == "onslaught_effect_+%" ? 50 : 0).ToList();
        Assert.Equal(["attack_speed_+%", "base_cast_speed_+%", "base_movement_velocity_+%"], kill.Select(s => s.Stat));
        Assert.All(kill, s => Assert.Equal(30, s.Value));      // 20% at 50% increased effect
        // A Silver Flask's Onslaught at 80% more flask effect is the same buff: the stronger counts, once.
        var flask = UtilityFlasks.OnslaughtSources(Program.Items.Onslaught!, 1.8);
        Assert.All(UtilityFlasks.Strongest(kill.Concat(flask)), s => Assert.Equal(36, s.Value));
        Assert.Equal(3, UtilityFlasks.Strongest(kill.Concat(flask)).Count);
        Assert.Equal(((ushort)0, (double?)null, true), o.Present(now + 2, alive: false, hidden: true)); // death ends it
        Assert.False(o.Applied);
        Assert.Empty(Game.KillOnslaughtSources(conn, _ => 0));
    }
}
