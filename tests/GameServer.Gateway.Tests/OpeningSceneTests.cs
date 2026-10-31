using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using GameServer.Core.Protocol.Instance;
using GameServer.Core.Protocol.Login;

namespace GameServer.Gateway.Tests;

public class OpeningSceneTests
{
    static readonly NpcCatalog.Npc Exile = new(6, TutorialObjects.Exile, "Dying Exile", ["Transitionable"], null, 1);
    // A save written before WakeUpShown and WeaponPromptHeard existed.
    static CharacterSaves.OpeningTutorial Untouched() =>
        JsonSerializer.Deserialize<CharacterSaves.OpeningTutorial>("""{"SkillGem":{"Type":123},"SupportGem":{"Type":123}}""")!;
    static Task Invoke(string method, Wire wire, ClientState conn, params object[] tail) =>
        (Task)typeof(Game).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, ["opening-scene-test", wire.Stream, wire.Ciphers, conn, .. tail])!;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_untouched_character_lies_down_facing_the_room_and_gets_up_in_place_once(bool withSkills)
    {
        var tutorial = Untouched();
        Assert.True(TutorialObjects.ShouldStartWashedUp(TutorialObjects.Strand, tutorial, true));
        Assert.False(TutorialObjects.ShouldStartWashedUp("1_1_town", tutorial, true));
        Assert.False(TutorialObjects.ShouldStartWashedUp(TutorialObjects.Strand, null, true));
        Assert.False(TutorialObjects.ShouldStartWashedUp(TutorialObjects.Strand, tutorial, false));
        foreach (var progressed in new[] { tutorial with { WakeUpShown = true }, tutorial with { WeaponPromptHeard = true },
            tutorial with { Talked = true }, tutorial with { ZombieKilled = true }, tutorial with { ChestOpened = true },
            tutorial with { SkillClaimed = true }, tutorial with { SupportClaimed = true } })
            Assert.False(TutorialObjects.ShouldStartWashedUp(TutorialObjects.Strand, progressed, true));

        var player = new PlayerObject("Ab", 1, 115, 69, 50, 40,
            Skills: withSkills ? [new(0x103, 532, 42)] : null, Souls: [new(87, 30, 7)]);
        byte[] standing = player.Blob();
        byte[] washed = (player with { WashedUp = true, Rotation = MathF.PI }).Blob();
        Assert.Equal(MathF.PI, BitConverter.ToSingle(washed, 10));
        Assert.Equal((byte)1, washed[126]); // Player's WashedUp flag.
        Assert.Equal(PlayerObject.WashedUpStance, washed[145]); // Actor stance, before its variable-length skill/soul list.
        // Exactly one (row, capacity, current) tuple, followed by the cooldown count and tasks.
        Assert.EndsWith("01570000001E000000070000000000000000", Convert.ToHexString(washed));
        Array.Clear(washed, 10, 4);
        washed[126] = 0;
        washed[145] = 0;
        Assert.Equal(standing, washed);

        using var wire = new Wire();
        var conn = wire.Conn;
        var use = new InstanceProtocol.UseSkillAtLocation(20, 25, TutorialObjects.WashedUpSkillId, 1, 0);
        conn.PeerTemplate = player;
        await Invoke("WakeUpOnBeach", wire, conn, use, conn.Clock.Elapsed.TotalSeconds);
        Assert.False(conn.TutorialGettingUp);
        conn.PeerTemplate = player with { WashedUp = true };
        await Invoke("WakeUpOnBeach", wire, conn, use, conn.Clock.Elapsed.TotalSeconds);
        Assert.True(conn.TutorialGettingUp);
        Assert.Equal((10, 10), conn.Walk.Destination);
        int accepted = conn.ActionCount;
        await Invoke("WakeUpOnBeach", wire, conn, use, conn.Clock.Elapsed.TotalSeconds);
        Assert.Equal(accepted, conn.ActionCount);
    }

    [Fact]
    public async Task The_exile_picks_its_voice_line_and_closing_it_skips_only_the_running_scene()
    {
        Assert.Equal(((byte)1, (double?)null), TutorialObjects.Dialogue(false, false));
        Assert.Equal(((byte)1, (double?)null), TutorialObjects.Dialogue(false, true));
        Assert.Equal(((byte)2, (double?)22.8), TutorialObjects.Dialogue(true, Untouched().WeaponPromptHeard));
        Assert.Equal(((byte)3, (double?)8.2), TutorialObjects.Dialogue(true, true));

        foreach (var (exile, running, skips) in new[] { (true, true, true), (true, false, false), (false, true, false) })
        {
            using var wire = new Wire();
            var conn = wire.Conn;
            conn.TalkingTo = 123;
            conn.InNpcTalk = true;
            conn.TutorialSceneRunning = running;
            conn.TutorialDialogueSkipped = new(TaskCreationOptions.RunContinuationsAsynchronously);
            conn.Npcs[123] = new(exile ? Exile : Exile with { Id = "OtherNpc" }, 10, 10);

            await Invoke("CloseNpcDialogue", wire, conn);

            Assert.Equal(skips, conn.TutorialDialogueSkipped.Task.IsCompleted);
        }
    }

    sealed class Wire : IDisposable
    {
        readonly TcpListener listener = new(IPAddress.Loopback, 0);
        readonly TcpClient writer = new();
        readonly TcpClient reader;
        public readonly ConnectionCiphers Ciphers = new(new byte[64], false);
        public readonly ClientState Conn = new(new(0, TutorialObjects.Strand, 32, 32, 10, 10, null, []), 0, 1);
        public NetworkStream Stream => writer.GetStream();
        public Wire()
        {
            listener.Start();
            var accepting = listener.AcceptTcpClientAsync();
            writer.Connect(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
            reader = accepting.GetAwaiter().GetResult();
        }
        public void Dispose() { Conn.Closed = true; reader.Dispose(); writer.Dispose(); listener.Stop(); }
    }
}
