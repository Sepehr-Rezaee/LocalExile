using GameServer.Core.World;

namespace GameServer.Gateway.Tests;

public sealed class SupportHitMultiplierTests
{
    // Overcharge, Bonechill, Deadly Ailments, Ancestral Call, Infused Channelling and Bloodthirst (this PR): each support's
    // more multiplies a hit once, and Bonechill's counts only for a hit with cold damage, in the elemental group.
    [Fact]
    public void Six_supports_multiply_a_hit_and_bonechill_needs_cold_damage()
    {
        (string, int)[] stats = [("support_pure_shock_damage_+%_final", -25), ("support_bonechill_cold_damage_+%_final", 22),
            ("support_better_ailments_hit_damage_+%_final", -80), ("support_spirit_strike_damage_+%_final", 20),
            ("support_storm_barrier_damage_+%_final", 37), ("support_blood_thirst_damage_+%_final", 17)];
        HashSet<int> spell = [1];
        var (general, typed) = GemSupportDamage.Multipliers(DamageTypeHistory.Cold, stats, spell);
        Assert.Equal(.75 * .2 * 1.2 * 1.37 * 1.17, general, 9);
        Assert.Equal(1.22, typed, 9);
        Assert.Equal(1, GemSupportDamage.Multipliers(DamageTypeHistory.Physical, stats, spell).ElementalOrChaos);
    }

    // Overcharge's Shock as though dealing 690% more Damage and "Inflict non-Damaging Ailments as though dealing 100% more
    // Damage" (this PR): a lightning crit of 1% of the threshold shocks for 18% instead of 7%; a cold crit of 3% chills for
    // 16% instead of 12% and freezes for 0.36 s instead of being discarded under 0.3 s; the chance to shock is unchanged.
    [Fact]
    public void As_though_stats_raise_the_damage_the_ailments_read_but_not_the_chance()
    {
        static int None(string _) => 0;
        static List<MonsterElementalAilments.Effect> Hit(double cold, double lightning, bool critical, Func<string, int> attacker) =>
            new MonsterElementalAilments().Inflict(cold, lightning, critical, 100, 0, attacker, None, new Random(1));
        Func<string, int> overcharge = s => s == "support_pure_shock_shock_as_though_damage_+%_final" ? 690 : 0;
        Func<string, int> ailments = s => s == "non_damaging_ailments_as_though_damage_+%_final" ? 100 : 0;

        Assert.Equal(7, Assert.Single(Hit(0, 1, true, None)).Magnitude);
        Assert.Equal(18, Assert.Single(Hit(0, 1, true, overcharge)).Magnitude);
        Assert.Empty(Hit(0, 1, false, overcharge));
        Assert.Equal(12, Assert.Single(Hit(3, 0, true, None)).Magnitude);
        var cold = Hit(3, 0, true, ailments);
        Assert.Equal(16, cold.Single(e => e.Kind == MonsterElementalAilments.Kind.Chill).Magnitude);
        Assert.Equal(.36, cold.Single(e => e.Kind == MonsterElementalAilments.Kind.Freeze).Until, 9);
    }
}
