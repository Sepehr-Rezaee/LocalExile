using GameServer.Core.World;

namespace GameServer.Gateway.Tests;

public sealed class RecoupTests
{
    // Life Recoup (this PR): a share of each hit's damage taken, raised by the Life recovery rate, recovered evenly over the
    // 4 s after the hit (3 s with Nine Lives); overlapping hits keep their own ends.
    [Fact]
    public void Life_recoup_returns_a_share_of_each_hit_over_four_seconds_by_type_and_recovery_rate()
    {
        static Func<string, int> With(params (string Name, int Value)[] stats) =>
            name => stats.FirstOrDefault(s => s.Name == name).Value;
        var hit = DamagePacket.Of(DamageType.Fire, 100, 100).Add(DamageType.Physical, new(50, 50));
        const string All = "damage_taken_goes_to_life_over_4_seconds_%";
        Assert.Equal(40, Game.LifeRecoupOf(With((All, 10), ("fire_damage_taken_goes_to_life_over_4_seconds_%", 20),
            ("physical_damage_taken_recouped_as_life_%", 10)), hit), 9);
        Assert.Equal(0, Game.LifeRecoupOf(With(), hit), 9);
        Assert.Equal(22.5, Game.LifeRecoupOf(With((All, 10), ("life_recovery_rate_+%", 50)), hit), 9);
        Assert.Equal(0, Game.LifeRecoupOf(With((All, 10), ("cannot_recover_life", 1)), hit), 9);
        Assert.Equal(4, Game.LifeRecoupSeconds(With()));
        Assert.Equal(3, Game.LifeRecoupSeconds(With(("recoup_effects_apply_over_3_seconds_instead", 1))));
        Assert.Equal(3, Game.LifeRecoupSeconds(With(("recoup_life_effects_apply_over_3_seconds_instead", 1))));

        var life = new RecoupRecovery();
        life.Add(10, 40, 4);
        life.Add(12, 20, 4);
        Assert.Equal(10, life.Take(11), 9);
        Assert.Equal(25, life.Take(13), 9);
        Assert.Equal(25, life.Take(20), 9);
        Assert.Equal(0, life.Take(21), 9);
        life.Add(30, 8, 4);
        Assert.Equal(0, life.Take(29), 9);
        Assert.Equal(8, life.Take(34), 9);
    }
}
