using GameServer.Core.World;

namespace GameServer.Gateway.Tests;

public sealed class WitheredOnHitTests
{
    // Withered on hit (this PR): 2 s or Withering Touch's duration, expiring slower by wither_expire_speed_+%; 6% increased
    // chaos damage taken per stack (with withered_effect_+%), up to 15 stacks with their own ends, on chaos damage only.
    [Fact]
    public void Withered_stacks_to_fifteen_with_their_own_ends_and_raise_chaos_damage_taken()
    {
        static Func<string, int> With(params (string Name, int Value)[] stats) =>
            name => stats.FirstOrDefault(s => s.Name == name).Value;
        var random = new Random(1);
        var wither = With(("withered_on_hit_for_2_seconds_%_chance", 100));
        Assert.Equal(0, Game.WitherRoll(With(), random));
        Assert.Equal(2, Game.WitherRoll(wither, random), 9);
        Assert.Equal(2.5, Game.WitherRoll(With(("withered_on_hit_chance_%", 100), ("support_withered_base_duration_ms", 2000),
            ("wither_expire_speed_+%", -20)), random), 9);

        var m = new SpawnedMonster("withered", new(105, 100, 0), 0, 0, 1000);
        for (int i = 0; i < 16; i++) Game.WitherOnHitLocked(m, wither, now: 10 + i * 0.1, random);
        Assert.Equal(15, m.Withered.Count);
        Assert.Equal(12.1, m.Withered.Min(s => s.Until), 9);
        Assert.Equal(90, Game.WitheredTaken(m, 11.6), 9);
        Assert.Equal(18, Game.WitheredTaken(m, 13.25), 9);
        Assert.Equal(190, Game.MonsterTakesMitigated(m, DamagePacket.Of(DamageType.Chaos, 100, 100), spell: false, now: 11.6));
        Assert.Equal(100, Game.MonsterTakesMitigated(m, DamagePacket.Of(DamageType.Fire, 100, 100), spell: false, now: 11.6));

        var stronger = new SpawnedMonster("withered more", new(105, 100, 0), 0, 0, 1000);
        Game.WitherOnHitLocked(stronger, With(("withered_on_hit_for_2_seconds_%_chance", 100), ("withered_effect_+%", 50)), 10, random);
        Assert.Equal(9, Game.WitheredTaken(stronger, 10.5), 9);
    }
}
