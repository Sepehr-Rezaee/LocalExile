using System.Net.Sockets;
using GameServer.Core.Protocol.Instance;
using GameServer.Core.Protocol.Login;

static partial class QuestobjectsRules
{
    // A Trial of Ascendancy's room objects where the room fit places them (LabyrinthTrials, Agents/docs/game/
    // labyrinth-trials.md): the trial's door, its levers and locked doors, the plaque and the portal back. What this
    // instance transitioned stays so (InstanceState.Transition); the plaque and the portal are lit once the character
    // has the trial's flag. A closed door closes its line across the way between the area's spawn and the plaque on the
    // server's walk estimate (QuestBarriers), as the client's TriggerableBlockage does on its side.
    internal static async Task SpawnTrialObjects(string remote, NetworkStream stream, ConnectionCiphers ciphers, ClientState conn)
    {
        if (LabyrinthTrials.For(conn.Layout.Id) is not { } trial) return;
        var notes = new List<string>();
        var objects = AreaPopulation.LoadObjects(conn.Layout.Id, conn.Seed, notes).Where(o => LabyrinthTrials.Handles(o.Type)).ToList();
        if (objects.Count == 0)
        {
            Console.WriteLine($"[gateway] {remote} trial {trial.Flag}: no trial objects in {conn.Layout.Id} seed {conn.Seed} ({string.Join("; ", notes)})");
            return;
        }
        var plaque = objects.FirstOrDefault(o => o.Type == LabyrinthTrials.Plaque);
        bool lit = ClientHasQuestFlag(conn, trial.Flag) || plaque is not null && conn.Instance.Transitioned(plaque.Type, plaque.X, plaque.Y);
        var from = conn.Layout.Near((conn.Layout.SpawnX, conn.Layout.SpawnY));
        List<(int X, int Y)> guarded = plaque is null ? [] : [conn.Layout.Near((plaque.X, plaque.Y))];
        foreach (var o in objects)
        {
            bool open = o.Type is LabyrinthTrials.Plaque or LabyrinthTrials.Portal ? lit : conn.Instance.Transitioned(o.Type, o.X, o.Y);
            uint id = conn.NextObjectId++;
            lock (conn.Monsters) conn.TrialObjects[id] = o;
            await Send(remote, stream, ciphers, InstanceEntities.CreateObject(id, InstanceEntities.ObjectTypeHash(o.Type),
                    LabyrinthTrials.Blob(o.Type, o.Model, o.X, o.Y, o.Angle, o.Scale, open ? LabyrinthTrials.Open : LabyrinthTrials.Closed)),
                $"0x02b9 trial object 0x{id:x} {o.Type} at ({o.X}, {o.Y}), angle {o.Angle:0.###}, {(open ? "open" : "closed")}");
            await Send(remote, stream, ciphers, InstanceProtocol.EnterWakeZone(id), null);
            if (open || o.Type is not (LabyrinthTrials.IntroDoor or LabyrinthTrials.Door)) continue;
            var cells = conn.Layout.Walkable is { } walkable
                ? QuestBarriers.Cells(walkable, conn.Layout.GridW, conn.Layout.GridH, (o.X, o.Y), o.Angle, from, guarded)
                : [];
            Console.WriteLine($"[gateway] {remote} trial door 0x{id:x}: {cells.Count} walkable cell(s) closed on the server between " +
                $"({from.X}, {from.Y}) and {string.Join(", ", guarded)}" + (cells.Count == 0 ? "" : $", from {cells.Min()} to {cells.Max()}"));
            lock (conn.Monsters)
            {
                conn.DoorBarriers[id] = cells;
                conn.UpdateBarrier();
            }
        }
    }

    // A trial object used: the player walks up to it, then the trial's door opens, a lever opens the nearest locked door,
    // the plaque completes the trial (its flag) and opens the portal, and the open portal returns to the trial's door.
    // False when the object is not a trial's.
    internal static async Task<bool> UseTrialObject(string remote, NetworkStream stream, ConnectionCiphers ciphers, ClientState conn,
        InstanceTransfer.UseSkillOnObject use)
    {
        AreaPopulation.RoomObject? o;
        lock (conn.Monsters) o = conn.TrialObjects.GetValueOrDefault(use.TargetId);
        if (o is null || LabyrinthTrials.For(conn.Layout.Id) is not { } trial) return false;
        int action = Interlocked.Increment(ref conn.ActionCount);
        double now = conn.Clock.Elapsed.TotalSeconds;
        int size = LabyrinthTrials.Size(o.Type);
        var (here, stop, arrive) = conn.Walk.WalkUpTo(o.X, o.Y, size, now);
        await Send(remote, stream, ciphers, InstanceProtocol.ActionStarted(PlayerId, here.X, here.Y, o.X, o.Y, use.SkillId, use.Sequence, use.Flags, targetObjectId: use.TargetId),
            $"0x01ab player uses trial object 0x{use.TargetId:x} {o.Type} from ({here.X}, {here.Y}): reached at ({stop.X}, {stop.Y}) in {arrive - now:0.00} s");
        if (arrive > now) await Task.Delay(TimeSpan.FromSeconds(arrive - now));
        if (conn.ActionCount != action || conn.Closed) return true;
        if (!GameServer.Core.World.PlayerWalk.InWalkUpRange(stop, (o.X, o.Y), size))
            Console.WriteLine($"[gateway] {remote} trial object 0x{use.TargetId:x} out of reach from ({stop.X}, {stop.Y})");
        else switch (o.Type)
        {
            case LabyrinthTrials.IntroDoor:
                await OpenTrialObject(remote, stream, ciphers, conn, use.TargetId, o);
                break;
            case LabyrinthTrials.Lever:
                if (!await OpenTrialObject(remote, stream, ciphers, conn, use.TargetId, o)) break;
                if (NearestTrialObject(conn, LabyrinthTrials.Door, (o.X, o.Y), closed: true) is { } door)
                    await OpenTrialObject(remote, stream, ciphers, conn, door.Id, door.Object);
                break;
            case LabyrinthTrials.Plaque:
                if (!await OpenTrialObject(remote, stream, ciphers, conn, use.TargetId, o)) break;
                await SetQuestFlags(remote, stream, ciphers, conn, [trial.Flag]);
                List<KeyValuePair<uint, AreaPopulation.RoomObject>> portals;
                lock (conn.Monsters) portals = [.. conn.TrialObjects.Where(t => t.Value.Type == LabyrinthTrials.Portal)];
                foreach (var (id, portal) in portals)
                    await Send(remote, stream, ciphers, InstanceEntities.TransitionableUpdate(id, LabyrinthTrials.TransitionableIndex(portal.Type), LabyrinthTrials.Open),
                        $"0x02ba trial portal 0x{id:x} opens: trial {trial.Flag} complete");
                break;
            case LabyrinthTrials.Portal:
                if (!TrialLit(conn, trial)) break;
                if (NearestTrialObject(conn, LabyrinthTrials.IntroDoor, (o.X, o.Y), closed: false) is not { } entrance) break;
                var to = conn.Layout.Near((entrance.Object.X, entrance.Object.Y));
                lock (conn.Monsters)
                {
                    Interlocked.Increment(ref conn.ActionCount);
                    conn.Walk.Teleport(to.X, to.Y, conn.Clock.Elapsed.TotalSeconds);
                }
                await Send(remote, stream, ciphers, InstanceProtocol.ActionEnded(PlayerId), null);
                await Send(remote, stream, ciphers, InstanceProtocol.Teleport(PlayerId, to.X, to.Y),
                    $"0x01b7 trial portal: back to the trial's door at ({to.X}, {to.Y})");
                return true;
        }
        await Send(remote, stream, ciphers, InstanceProtocol.ActionEnded(PlayerId), null);
        return true;
    }

    // Transitions a trial object once in this instance: its Transitionable steps to the open state (the client plays the
    // .ot's on_transition), a door also unblocks (0x1c6) and leaves the server's walk estimate. False when already open.
    static async Task<bool> OpenTrialObject(string remote, NetworkStream stream, ConnectionCiphers ciphers, ClientState conn, uint id,
        AreaPopulation.RoomObject o)
    {
        if (!conn.Instance.Transition(o.Type, o.X, o.Y)) return false;
        await Send(remote, stream, ciphers, InstanceEntities.TransitionableUpdate(id, LabyrinthTrials.TransitionableIndex(o.Type), LabyrinthTrials.Open),
            $"0x02ba trial object 0x{id:x} {o.Type} at ({o.X}, {o.Y}) opens");
        if (o.Type is not (LabyrinthTrials.IntroDoor or LabyrinthTrials.Door)) return true;
        await Send(remote, stream, ciphers, InstanceEntities.BlockageUpdate(id, false), $"0x01c6 trial door 0x{id:x} unblocked");
        lock (conn.Monsters)
        {
            conn.DoorBarriers.Remove(id);
            conn.UpdateBarrier();
        }
        return true;
    }

    // Whether the plaque is lit: the character has the trial's flag, or this instance's plaque was used.
    static bool TrialLit(ClientState conn, LabyrinthTrials.Trial trial)
    {
        if (ClientHasQuestFlag(conn, trial.Flag)) return true;
        lock (conn.Monsters)
            return conn.TrialObjects.Values.Any(t => t.Type == LabyrinthTrials.Plaque && conn.Instance.Transitioned(t.Type, t.X, t.Y));
    }

    // The trial object of this type nearest to a cell, closed or not in this instance.
    static (uint Id, AreaPopulation.RoomObject Object)? NearestTrialObject(ClientState conn, string type, (int X, int Y) at, bool closed)
    {
        lock (conn.Monsters)
            return conn.TrialObjects.Where(t => t.Value.Type == type && (!closed || !conn.Instance.Transitioned(type, t.Value.X, t.Value.Y)))
                .OrderBy(t => (long)(t.Value.X - at.X) * (t.Value.X - at.X) + (long)(t.Value.Y - at.Y) * (t.Value.Y - at.Y))
                .Select(t => ((uint, AreaPopulation.RoomObject)?)(t.Key, t.Value)).FirstOrDefault();
    }
}
