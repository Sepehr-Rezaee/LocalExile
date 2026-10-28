using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using GameServer.Core.Protocol.Login;
using GameServer.Core.Protocol.Instance;

namespace GameServer.Gateway.Tests;

public class OpeningSceneTests
{
    static readonly NpcCatalog.Npc Exile = new(6, TutorialObjects.Exile, "Dying Exile", ["Transitionable"], null, 1);
    static Task Invoke(string method, Wire wire, ClientState conn, params object[] tail) =>
        (Task)typeof(Program).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, ["opening-scene-test", wire.Stream, wire.Ciphers, conn, .. tail])!;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_get_up_action_is_stationary_and_only_runs_for_a_washed_up_player(bool washedUp)
    {
        using var wire = new Wire();
        var conn = wire.Conn;
        conn.PeerTemplate = new PlayerObject("Beach",1,10,10,50,40) { WashedUp=washedUp };
        var use = new InstanceProtocol.UseSkillAtLocation(20,25,TutorialObjects.WashedUpSkillId,1,0);
        await Invoke("WakeUpOnBeach",wire,conn,use,conn.Clock.Elapsed.TotalSeconds);
        Assert.Equal((10,10),conn.Walk.Destination);
        Assert.Equal(washedUp,conn.TutorialGettingUp);
        if(washedUp)
        {
            int accepted = conn.ActionCount;
            await Invoke("WakeUpOnBeach",wire,conn,use,conn.Clock.Elapsed.TotalSeconds);
            Assert.Equal(accepted,conn.ActionCount);
        }
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public async Task Closing_dialogue_skips_only_the_active_exile_scene(bool exile, bool running, bool skips)
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

    [Fact]
    public async Task Skipping_a_long_voice_starts_the_bite_now_and_waits_only_for_its_animation()
    {
        using var wire = new Wire();
        var skipped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        wire.Conn.TutorialSceneRunning = true;
        var elapsed = Stopwatch.StartNew();
        var scene = Invoke("RunOpeningScene", wire, wire.Conn, (uint)123, new SpawnedNpc(Exile, 10, 10), 3600.0, skipped.Task);
        skipped.SetResult();
        await wire.ReadTransition().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(scene.IsCompleted); // The combat handoff must retain the bite animation.
        await scene.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.True(elapsed.Elapsed.TotalSeconds >= TutorialObjects.BiteSeconds);
        Assert.False(wire.Conn.TutorialSceneRunning);
        Assert.Null(wire.Conn.TutorialDialogueSkipped);
    }

    [Fact]
    public async Task Skipping_during_pause_waits_for_resume_before_the_bite()
    {
        using var wire = new Wire();
        wire.Conn.Clock.Stop();
        var scene = Invoke("RunOpeningScene", wire, wire.Conn, (uint)123, new SpawnedNpc(Exile, 10, 10), 3600.0, Task.CompletedTask);
        await Task.Delay(150);
        Assert.False(wire.HasBytes);
        wire.Conn.Clock.Start();
        await wire.ReadTransition().WaitAsync(TimeSpan.FromSeconds(5));
        wire.Conn.Closed = true;
        await scene.WaitAsync(TimeSpan.FromSeconds(5));
    }

    sealed class Wire : IDisposable
    {
        readonly TcpListener listener = new(IPAddress.Loopback, 0);
        readonly TcpClient writer = new();
        readonly TcpClient reader;
        readonly ConnectionCiphers decrypt = new(new byte[64], true);
        public readonly ConnectionCiphers Ciphers = new(new byte[64], false);
        public readonly ClientState Conn = new(new(0, TutorialObjects.Strand, 32, 32, 10, 10, null, []), 0, 1);
        public NetworkStream Stream => writer.GetStream();
        public bool HasBytes => reader.Available > 0;
        public Wire()
        {
            listener.Start();
            var accepting = listener.AcceptTcpClientAsync();
            writer.Connect(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
            reader = accepting.GetAwaiter().GetResult();
        }
        public async Task ReadTransition()
        {
            var bytes = new byte[16];
            await reader.GetStream().ReadExactlyAsync(bytes);
            decrypt.Receive.Process(bytes);
            Assert.Equal("02BA0000007B00000000000000020002", Convert.ToHexString(bytes));
        }
        public void Dispose() { Conn.Closed = true; reader.Dispose(); writer.Dispose(); listener.Stop(); }
    }
}
