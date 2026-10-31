using GameServer.Core.Protocol.Instance;

namespace GameServer.Gateway.Tests;

public sealed class LabyrinthTrialTests
{
    [Fact]
    public void TrialDoorBlobAndUnblockFollowTheClientReaders()
    {
        const string model = "Metadata/Terrain/Doodads/Labyrinth/LabyrinthDoor.ao";
        byte[] closed = LabyrinthTrials.Blob(LabyrinthTrials.IntroDoor, model, 160, 57, 0, 1, LabyrinthTrials.Closed);
        byte[] open = LabyrinthTrials.Blob(LabyrinthTrials.IntroDoor, model, 160, 57, 0, 1, LabyrinthTrials.Open);

        // Positioned (18), Animated (1 + u32 count + UTF-16), InteractionAction, Transitionable, TriggerableBlockage, tasks.
        int animated = 2 + 18, after = animated + 1 + 4 + model.Length * 2;
        Assert.Equal(160, BitConverter.ToInt32(closed, 2));
        Assert.Equal(1, closed[animated]);
        Assert.Equal(model.Length, BitConverter.ToInt32(closed, animated + 1));
        Assert.Equal(new byte[] { 0, LabyrinthTrials.Closed, 1, 0, 0 }, closed[after..]);
        Assert.Equal(new byte[] { 0, LabyrinthTrials.Open, 0, 0 }, open[after..]);
        Assert.Equal(4, LabyrinthTrials.TransitionableIndex(LabyrinthTrials.IntroDoor));
        Assert.Equal(1, LabyrinthTrials.TransitionableIndex(LabyrinthTrials.Lever));

        // 0x1c6: id, world header, u8 0 (unblocked: no sections follow).
        Assert.Equal(new byte[] { 0x01, 0xc6, 0, 0, 0x30, 0x05, 0, 0, 0, 0, 0, 0, 0 }, InstanceEntities.BlockageUpdate(0x3005, false));
    }

    [Fact]
    public void TrialsTakeTheirAreaAndCompletionFlag()
    {
        const string json = """
            [{"WorldAreas":1,"__col1":263,"__col7":2,"__col2":151},
             {"WorldAreas":9,"__col1":264,"__col7":2},
             {"WorldAreas":0,"__col1":265,"__col7":7}]
            """;
        var flags = new Dictionary<int, string> { [2] = "NormalLabyrinthCompletedPrison" };

        var trials = LabyrinthTrials.Parse(json, ["1_1_1", "1_1_7_1"], flags);

        Assert.Equal([new LabyrinthTrials.Trial("1_1_7_1", "NormalLabyrinthCompletedPrison", 263)], trials);
    }
}
