namespace GameServer.Gateway.Tests;

public sealed class AilmentProhibitionTests
{
    static Func<string, int> With(params string[] flags) => name => flags.Contains(name) ? 1 : 0;

    [Fact]
    public void Each_prohibition_stops_its_own_ailments()
    {
        string[] all = ["ignite", "chill", "freeze", "shock"];
        Assert.All(all, a => Assert.True(MonsterElementalAilments.CannotInflict(a, With("cannot_inflict_status_ailments"))));
        Assert.All(all, a => Assert.True(MonsterElementalAilments.CannotInflict(a, With("never_ignite_chill_freeze_shock"))));
        Assert.All(all, a => Assert.False(MonsterElementalAilments.CannotInflict(a, With())));
        var cold = With("never_freeze_or_chill");
        Assert.Equal([false, true, true, false], all.Select(a => MonsterElementalAilments.CannotInflict(a, cold)));
        Assert.Equal([false, false, true, false], all.Select(a => MonsterElementalAilments.CannotInflict(a, With("never_freeze"))));
        Assert.Equal([false, false, false, true], all.Select(a => MonsterElementalAilments.CannotInflict(a, With("never_shock"))));
        Assert.Equal(MonsterIgnites.Settings.None,
            MonsterIgnites.Settings.From([], "main_hand", With("cannot_inflict_status_ailments", "base_chance_to_ignite_%")));
    }

    [Fact]
    public void Elemental_Focus_stops_a_critical_hits_chill_freeze_and_shock()
    {
        static List<MonsterElementalAilments.Effect> Hit(Func<string, int> attacker) =>
            new MonsterElementalAilments().Inflict(500, 500, critical: true, threshold: 1000, now: 0, attacker, _ => 0, new Random(1));
        Assert.Equal(3, Hit(With()).Count);
        Assert.Empty(Hit(With("cannot_inflict_status_ailments")));
        Assert.Equal([MonsterElementalAilments.Kind.Shock], Hit(With("never_freeze_or_chill")).Select(e => e.Kind));
    }
}
