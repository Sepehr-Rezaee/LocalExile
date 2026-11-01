namespace GameServer.Gateway.Tests;

public sealed class HitDebuffTests
{
    // Intimidate and Unnerve from hits (this PR): the attack-only and spell-only chances, Grattus Signet's crits, 4 s from
    // the latest hit, and 10% increased damage taken from attacks (Intimidate) or spells (Unnerve).
    [Fact]
    public void Hits_intimidate_and_unnerve_by_kind_and_raise_that_kinds_damage_taken()
    {
        static Func<string, int> With(params string[] stats) => name => stats.Contains(name) ? 100 : 0;
        var random = new Random(1);
        Assert.Equal((true, false), Game.HitDebuffRolls(With("attacks_intimidate_on_hit_%"), spell: false, critical: false, random));
        Assert.Equal((false, false), Game.HitDebuffRolls(With("attacks_intimidate_on_hit_%"), spell: true, critical: false, random));
        Assert.Equal((false, true), Game.HitDebuffRolls(With("chance_to_unnerve_on_hit_with_spells_%"), true, false, random));
        Assert.Equal((false, false), Game.HitDebuffRolls(With("chance_to_unnerve_on_hit_with_spells_%"), false, false, random));
        Assert.Equal((false, true), Game.HitDebuffRolls(With("attacks_inflict_unnerve_on_crit"), false, true, random));
        Assert.Equal((false, false), Game.HitDebuffRolls(With("attacks_inflict_unnerve_on_crit"), false, false, random));
        Assert.Equal((true, false), Game.HitDebuffRolls(With("spells_inflict_intimidate_on_crit"), true, true, random));
        Assert.Equal((true, true), Game.HitDebuffRolls(With("chance_to_intimidate_on_hit_%", "chance_to_unnerve_on_hit_%"), true, false, random));

        var m = new SpawnedMonster("intimidated", new(105, 100, 0), 0, 0, 100);
        Game.HitDebuffsOnHitLocked(m, With("chance_to_intimidate_on_hit_%"), spell: false, critical: false, now: 10);
        Game.HitDebuffsOnHitLocked(m, With("chance_to_intimidate_on_hit_%"), spell: false, critical: false, now: 12);
        Assert.Equal(16, m.IntimidatedUntil);
        Assert.Equal(10, Game.HitDebuffTaken(m, spell: false, 15.9));
        Assert.Equal(0, Game.HitDebuffTaken(m, spell: true, 15.9));
        Assert.Equal(0, Game.HitDebuffTaken(m, spell: false, 16));
    }
}
