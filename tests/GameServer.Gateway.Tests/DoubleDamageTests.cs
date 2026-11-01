using GameServer.Core.World;

namespace GameServer.Gateway.Tests;

public sealed class DoubleDamageTests
{
    // Double damage (this PR): the player's chance on every hit, a spell's chance on spells, the skill's own (Heavy Strike);
    // triple damage overrides it, so double only rolls when triple misses.
    [Fact]
    public void Hits_deal_double_damage_by_their_chances_and_triple_damage_overrides_it()
    {
        static Func<string, int> With(params (string Name, int Value)[] stats) =>
            name => stats.FirstOrDefault(s => s.Name == name).Value;
        var random = new Random(1);
        var hit = DamagePacket.Of(DamageType.Physical, 100, 100);
        double Dealt(GamblerModifiers gambler) => gambler.RollOutgoingHit(hit, random).MinTotal;

        Assert.Equal(100, Dealt(GamblerModifiers.From(With())));
        Assert.Equal(200, Dealt(GamblerModifiers.From(With(("chance_to_deal_double_damage_%", 100)))));
        Assert.Equal(300, Dealt(GamblerModifiers.From(
            With(("chance_to_deal_double_damage_%", 100), ("base_chance_to_deal_triple_damage_%", 100)))));
        var spells = GamblerModifiers.From(With(("spell_chance_to_deal_double_damage_%", 100)));
        Assert.Equal(100, Dealt(spells));
        Assert.Equal(100, Dealt(spells.ForSkill(With(), spell: false)));
        Assert.Equal(200, Dealt(spells.ForSkill(With(), spell: true)));
        Assert.Equal(200, Dealt(GamblerModifiers.From(With()).ForSkill(With(("chance_to_deal_double_damage_%", 100)), spell: false)));

        // Half triple and half double: a quarter of the hits are doubled, not half.
        var both = GamblerModifiers.From(With(("chance_to_deal_double_damage_%", 50), ("base_chance_to_deal_triple_damage_%", 50)));
        var counts = Enumerable.Range(0, 20000).Select(_ => Dealt(both)).GroupBy(d => d).ToDictionary(g => g.Key, g => g.Count());
        Assert.InRange(counts[300], 9600, 10400);
        Assert.InRange(counts[200], 4700, 5300);
        Assert.InRange(counts[100], 4700, 5300);
    }
}
