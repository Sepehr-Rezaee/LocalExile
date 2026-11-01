using System.Net.Sockets;
using System.Runtime.CompilerServices;
using GameServer.Core.Protocol.Login;

static partial class Game
{
    // Shield Charge's movement (11-skills.md "Shield Charge"). The client moves the charge at the player's own movement
    // speed: 0x141e7c0b0 reads movement_velocity_+% from the player's Stats, not the skill's. The game adds the gem's
    // base_movement_velocity_+% through the invisible buff shield_charge_movement_speed_while_charging (BuffDefinitions
    // 1352) for as long as the charge runs, and network stats run no calculators, so the server puts that bonus into the
    // movement_velocity_+% it sends (the pools) from the use until its own walk arrives, and walks its estimate to the aimed
    // cell at that speed instead of the generic movement skill's teleport. Without it the charge ran at walk speed.
    sealed class ShieldChargeRun { public int Percent, Token; }
    static readonly ConditionalWeakTable<ClientState, ShieldChargeRun> ShieldChargeRuns = new();

    // The running charge's movement bonus, for the pools (PoolsFor); 0 when none runs.
    internal static int ShieldChargeMovementPct(ClientState conn) => ShieldChargeRuns.TryGetValue(conn, out var run) ? run.Percent : 0;

    // A movement skill that needs a shield and carries its own movement bonus: it walks there faster, it does not blink.
    internal static int ShieldChargeBonusPct(ClientState conn, ushort skillId, int effect) =>
        SkillsFor(conn).IsMovement(effect) && SkillsFor(conn).RequiresShield(effect)
            && Stats.Key("base_movement_velocity_+%") is uint key ? SupportedSkill(conn, skillId).Stats.GetValueOrDefault(key) : 0;

    // In the use's StartAction: the bonus on, the pools and walk speed with it, and the walk to the aimed cell. Returns the
    // run's token, which only the latest charge's end may clear.
    internal static int BeginShieldCharge(ClientState conn, int percent, int x, int y, double at)
    {
        var run = ShieldChargeRuns.GetOrCreateValue(conn);
        lock (conn.Monsters)
        {
            run.Percent = percent;
            int token = ++run.Token;
            RecomputePoolsAndSpeed(conn);
            conn.Walk.Order(x, y, at);
            return token;
        }
    }

    // After the use's echo: the faster movement speed to the client, then back when the server's walk arrives (a later
    // charge takes over the run and its own end).
    internal static async Task RunShieldCharge(string remote, NetworkStream stream, ConnectionCiphers ciphers, ClientState conn,
        int token, double arrivesAt)
    {
        await SendPlayerStatSnapshot(remote, stream, ciphers, conn, () => PlayerStatsUpdateAtGate(conn, PlayerStatsWithCleared(conn)),
            $"0x01b3 player stats (Shield Charge starts: movement_velocity_+% {conn.Pools.MovementVelocityPct}, " +
            $"+{ShieldChargeMovementPct(conn)} while charging, until t={arrivesAt:0.000})");
        double wait = arrivesAt - conn.Clock.Elapsed.TotalSeconds;
        if (wait > 0) await Task.Delay(TimeSpan.FromSeconds(wait));
        if (!ShieldChargeRuns.TryGetValue(conn, out var run) || run.Token != token || conn.Closed) return;
        lock (conn.Monsters)
        {
            run.Percent = 0;
            RecomputePoolsAndSpeed(conn);
        }
        await SendPlayerStatSnapshot(remote, stream, ciphers, conn, () => PlayerStatsUpdateAtGate(conn, PlayerStatsWithCleared(conn)),
            $"0x01b3 player stats (Shield Charge ends: movement_velocity_+% {conn.Pools.MovementVelocityPct})");
    }
}
