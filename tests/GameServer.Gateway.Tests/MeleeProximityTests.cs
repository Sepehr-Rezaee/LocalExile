namespace GameServer.Gateway.Tests;

public sealed class MeleeProximityTests
{
    // Impact, Close Combat and Shield Crush (this PR): "up to X% more Melee Damage, based on proximity", whole up to 15
    // units, linear to none at 40 (PoB MeleeProximity), each its own multiplier.
    [Fact]
    public void Proximity_more_is_whole_near_and_gone_at_40_units()
    {
        static int Impact(string name) => name == "slayer_damage_+%_final_from_distance" ? 15 : 0;
        Assert.Equal(1.15, Game.MeleeProximityMore(Impact, 0), 9);
        Assert.Equal(1.15, Game.MeleeProximityMore(Impact, 15), 9);
        Assert.Equal(1.075, Game.MeleeProximityMore(Impact, 27.5), 9);
        Assert.Equal(1, Game.MeleeProximityMore(Impact, 40));
        Assert.Equal(1, Game.MeleeProximityMore(Impact, 60));
        static int Both(string name) => name switch
        {
            "slayer_damage_+%_final_from_distance" => 15,
            "support_slashing_damage_+%_final_from_distance" => 39,
            _ => 0,
        };
        Assert.Equal(1.15 * 1.39, Game.MeleeProximityMore(Both, 10), 9);
        Assert.Equal(1.03 * 1.078, Game.MeleeProximityMore(Both, 35), 9);
    }
}
