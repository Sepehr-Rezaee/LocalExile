namespace GameServer.Gateway.Tests;

public sealed class PointBlankTests
{
    [Fact]
    public void Point_blank_ramps_from_30_more_near_to_30_less_far()
    {
        Assert.Equal(1.3, AntiquarianEffects.PointBlank(0));
        Assert.Equal(1.3, AntiquarianEffects.PointBlank(10));
        Assert.Equal(1.15, AntiquarianEffects.PointBlank(22.5), 9);
        Assert.Equal(1, AntiquarianEffects.PointBlank(35), 9);
        Assert.Equal(.85, AntiquarianEffects.PointBlank(77.5), 9);
        Assert.Equal(.7, AntiquarianEffects.PointBlank(120));
        Assert.Equal(.7, AntiquarianEffects.PointBlank(150));
        // With Far Shot too, the two multiply: 1.3 × 0.8 at the muzzle, 0.7 × 1.6 at full range.
        Assert.Equal(1.04, AntiquarianEffects.PointBlank(5) * AntiquarianEffects.FarShot(5), 9);
        Assert.Equal(1.12, AntiquarianEffects.PointBlank(150) * AntiquarianEffects.FarShot(150), 9);
    }
}
