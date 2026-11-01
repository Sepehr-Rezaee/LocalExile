namespace GameServer.Gateway.Tests;

public sealed class RuthlessBlowTests
{
    // Ruthless (this PR): every third use of a supported skill is a Ruthless Blow with 98% more melee damage at level 20;
    // each skill keeps its own count, and a use without the support does not count.
    [Fact]
    public void Every_third_use_of_each_skill_is_a_ruthless_blow()
    {
        static int Ruthless(string name) => name switch
        {
            "support_ruthless_big_hit_max_count" => 3,
            "support_ruthless_big_hit_damage_+%_final" => 98,
            _ => 0,
        };
        static int Plain(string _) => 0;
        var counts = new Dictionary<ushort, int>();
        double Use(ushort skill, Func<string, int> stats) => Game.RuthlessBlowMore(counts, skill, stats);

        Assert.Equal(1, Use(1, Ruthless));
        Assert.Equal(1, Use(1, Ruthless));
        Assert.Equal(1, Use(2, Ruthless));
        Assert.Equal(1, Use(2, Plain));
        Assert.Equal(1.98, Use(1, Ruthless), 9);
        Assert.Equal(1, Use(1, Ruthless));
        Assert.Equal(1, Use(2, Ruthless));
        Assert.Equal(1.98, Use(2, Ruthless), 9);
        Assert.Equal(1, Use(3, Plain));
        Assert.False(counts.ContainsKey(3));
    }
}
