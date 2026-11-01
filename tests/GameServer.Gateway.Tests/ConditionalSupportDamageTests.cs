using GameServer.Core.World;

namespace GameServer.Gateway.Tests;

public sealed class ConditionalSupportDamageTests
{
    static Dictionary<uint, int> With(params (string Name, int Value)[] stats) =>
        stats.ToDictionary(s => Program.Stats.Key(s.Name)!.Value, s => s.Value);

    [Fact]
    public void Each_support_condition_multiplies_only_when_it_holds()
    {
        var skill = With(("support_hypothermia_damage_+%_vs_chilled_enemies_final", 29),
            ("support_executioner_damage_vs_enemies_on_low_life_+%_final", 49),
            ("support_damage_while_on_full_life_+%_final", 34));
        Assert.Equal(1, Game.ConditionalSupportMore(skill, attack: true, chilled: false, lowLife: false, fullLife: false));
        Assert.Equal(1.29, Game.ConditionalSupportMore(skill, attack: true, chilled: true, lowLife: false, fullLife: false), 9);
        Assert.Equal(1.49, Game.ConditionalSupportMore(skill, attack: false, chilled: false, lowLife: true, fullLife: false), 9);
        Assert.Equal(1.34, Game.ConditionalSupportMore(skill, attack: true, chilled: false, lowLife: false, fullLife: true), 9);
        Assert.Equal(1, Game.ConditionalSupportMore(skill, attack: false, chilled: false, lowLife: false, fullLife: true));
        Assert.Equal(1.29 * 1.49 * 1.34, Game.ConditionalSupportMore(skill, true, true, true, true), 9);
        // Awakened Increased Area of Effect: more damage for area skills only.
        var awakened = new[] { ("inc_aoe_plus_more_area_damage_+%_final", 24) };
        Assert.Equal(1.24, GemSupportDamage.Multipliers(DamageTypeHistory.Physical, awakened, new HashSet<int> { 0, 7 }).GeneralOrPhysical, 9);
        Assert.Equal(1, GemSupportDamage.Multipliers(DamageTypeHistory.Physical, awakened, new HashSet<int> { 0 }).GeneralOrPhysical);
    }

    [Fact]
    public void A_hit_on_a_low_life_enemy_gets_Beheads_more_damage()
    {
        var conn = new ClientState(new(1, "behead", 300, 300, 100, 100, null, []), 1, 1)
        {
            Pools = new(1, 1000, 100, 2, 0), PlayerLife = 900,
        };
        var skill = With(("support_executioner_damage_vs_enemies_on_low_life_+%_final", 30),
            ("support_damage_while_on_full_life_+%_final", 20));
        var hit = DamagePacket.Of(DamageType.Physical, 100, 100);
        var low = new SpawnedMonster("low", new(100, 100, 0), 0, 0, 1000) { Life = 500 };
        var healthy = new SpawnedMonster("healthy", new(100, 100, 0), 0, 0, 1000) { Life = 501 };
        Assert.Equal(130, Game.WithConditionalSupportDamageLocked(conn, skill, true, low, hit, 0).MinTotal, 9);
        Assert.Equal(100, Game.WithConditionalSupportDamageLocked(conn, skill, true, healthy, hit, 0).MinTotal, 9);
        conn.PlayerLife = 1000;   // full Life: Damage on Full Life joins in
        Assert.Equal(120, Game.WithConditionalSupportDamageLocked(conn, skill, true, healthy, hit, 0).MinTotal, 9);
    }
}
