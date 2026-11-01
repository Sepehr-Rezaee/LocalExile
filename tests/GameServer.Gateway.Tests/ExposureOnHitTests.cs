using GameServer.Core.World;

namespace GameServer.Gateway.Tests;

public sealed class ExposureOnHitTests
{
    // Exposure on hit (this PR): -10% by default, a magnitude stat's own value, the extra and minimum stats, 4 s by
    // element and magnitude, the highest running one lowering the monster's resistance to hits and burns.
    [Fact]
    public void Hits_expose_by_element_and_the_highest_running_exposure_lowers_resistance()
    {
        static Func<string, int> With(params (string Name, int Value)[] stats) =>
            name => stats.FirstOrDefault(s => s.Name == name).Value;
        var random = new Random(1);
        Assert.Equal(-10, Game.ExposureRoll(With(("base_inflict_fire_exposure_on_hit_%_chance", 100)), DamageType.Fire, false, random));
        Assert.Equal(0, Game.ExposureRoll(With(("base_inflict_fire_exposure_on_hit_%_chance", 100)), DamageType.Cold, false, random));
        var withCold = With(("chance_to_inflict_cold_exposure_on_hit_with_cold_damage_%", 100));
        Assert.Equal(0, Game.ExposureRoll(withCold, DamageType.Cold, withType: false, random));
        Assert.Equal(-10, Game.ExposureRoll(withCold, DamageType.Cold, withType: true, random));
        Assert.Equal(-14, Game.ExposureRoll(With(("lightning_exposure_on_hit_magnitude", -14)), DamageType.Lightning, false, random));
        Assert.Equal(-22, Game.ExposureRoll(With(("inflict_all_exposure_on_hit", 1),
            ("cold_exposure_you_inflict_applies_extra_cold_resistance_+%", -12)), DamageType.Cold, false, random));
        Assert.Equal(-25, Game.ExposureRoll(With(("inflict_all_exposure_on_hit", 1),
            ("exposure_you_inflict_has_minimum_resistance_%", -25)), DamageType.Fire, false, random));
        Assert.Equal(0, Game.ExposureRoll(With(("inflict_all_exposure_on_hit", 1), ("cannot_inflict_exposure", 1)),
            DamageType.Fire, false, random));

        var m = new SpawnedMonster("exposed", new(105, 100, 0), 0, 0, 100);
        var fire = DamagePacket.Of(DamageType.Fire, 10, 10);
        Game.ExposureOnHitLocked(m, With(("base_inflict_fire_exposure_on_hit_%_chance", 100)), fire, now: 10, random);
        Game.ExposureOnHitLocked(m, With(("fire_exposure_on_hit_magnitude", -14)), fire, now: 8, random);
        int unexposed = Game.MonsterHitResistance(m, DamageType.Fire, default).Value;
        Assert.Equal(unexposed - 14, Game.MonsterHitResistance(m, DamageType.Fire, default, now: 11).Value);
        Assert.Equal(unexposed - 10, Game.MonsterHitResistance(m, DamageType.Fire, default, now: 12.5).Value);
        Assert.Equal(unexposed, Game.MonsterHitResistance(m, DamageType.Fire, default, now: 14).Value);
        Assert.Equal(unexposed, Game.MonsterHitResistance(m, DamageType.Cold, default, now: 11).Value);
        Assert.Equal(14, m.ExposureUntil[0]);
    }

    // Elemental Equilibrium (this PR): an elemental hit removes Exposure of its own elements and inflicts -25% of the
    // others, scaled by elemental_equilibrium_effect_+%; a hit without elemental damage does neither.
    [Fact]
    public void Elemental_equilibrium_removes_the_hits_exposure_and_exposes_the_other_elements()
    {
        static Func<string, int> With(params (string Name, int Value)[] stats) =>
            name => stats.FirstOrDefault(s => s.Name == name).Value;
        var random = new Random(1);
        var m = new SpawnedMonster("balanced", new(105, 100, 0), 0, 0, 100);
        var keystone = With(("keystone_elemental_equilibrium", 1));
        var physical = DamagePacket.Of(DamageType.Physical, 10, 10);
        Game.ExposureOnHitLocked(m, With(("base_inflict_fire_exposure_on_hit_%_chance", 100)), physical, now: 10, random);
        Game.ExposureOnHitLocked(m, keystone, physical, now: 10.5, random);
        Assert.Equal(-10, Game.MonsterExposure(m, DamageType.Fire, 11));
        Assert.Equal(0, Game.MonsterExposure(m, DamageType.Cold, 11));

        Game.ExposureOnHitLocked(m, keystone, DamagePacket.Of(DamageType.Cold, 10, 10), now: 11, random);
        Assert.Equal(-25, Game.MonsterExposure(m, DamageType.Fire, 12));
        Assert.Equal(-25, Game.MonsterExposure(m, DamageType.Lightning, 12));
        Assert.Equal(0, Game.MonsterExposure(m, DamageType.Cold, 12));

        Game.ExposureOnHitLocked(m, With(("keystone_elemental_equilibrium", 1), ("elemental_equilibrium_effect_+%", 20)),
            DamagePacket.Of(DamageType.Fire, 10, 10), now: 12, random);
        Assert.Equal(0, Game.MonsterExposure(m, DamageType.Fire, 12.5));
        Assert.Equal(double.NegativeInfinity, m.ExposureUntil[0]);
        Assert.Equal(-30, Game.MonsterExposure(m, DamageType.Cold, 12.5));
        Assert.Equal(-30, Game.MonsterExposure(m, DamageType.Lightning, 12.5));
    }
}
