namespace GameServer.Gateway.Tests;

public sealed class BlindOnHitTests
{
    // Blind on hit (this PR): the general chance, the attack-only one and the critical one sum into one roll; a Blind lasts
    // 4 s with blind_duration_+%, from the latest hit, and counts as the player's Blind for that long.
    [Fact]
    public void Hits_blind_by_their_chances_for_four_seconds_and_blind_duration()
    {
        static Func<string, int> With(params (string Name, int Value)[] stats) =>
            name => stats.FirstOrDefault(s => s.Name == name).Value;
        var random = new Random(1);
        Assert.True(Game.BlindRoll(With(("global_chance_to_blind_on_hit_%", 100)), spell: true, critical: false, random));
        Assert.True(Game.BlindRoll(With(("attacks_chance_to_blind_on_hit_%", 100)), spell: false, critical: false, random));
        Assert.False(Game.BlindRoll(With(("attacks_chance_to_blind_on_hit_%", 100)), spell: true, critical: false, random));
        Assert.True(Game.BlindRoll(With(("%_chance_to_blind_on_critical_strike", 100)), spell: true, critical: true, random));
        Assert.False(Game.BlindRoll(With(("%_chance_to_blind_on_critical_strike", 100)), spell: true, critical: false, random));

        var blind = With(("global_chance_to_blind_on_hit_%", 100), ("blind_duration_+%", 50));
        Assert.Equal(6, Game.BlindSeconds(blind), 9);
        var m = new SpawnedMonster("blinded", new(105, 100, 0), 0, 0, 100);
        Game.HitDebuffsOnHitLocked(m, blind, spell: false, critical: false, now: 10);
        Game.HitDebuffsOnHitLocked(m, With(("global_chance_to_blind_on_hit_%", 100)), spell: false, critical: false, now: 11);
        Assert.Equal(16, m.BlindedUntil, 9);
        Assert.Equal(double.NegativeInfinity, m.IntimidatedUntil);
    }
}
