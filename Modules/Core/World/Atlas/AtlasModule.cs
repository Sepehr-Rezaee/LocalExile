using System.Net.Sockets;
using GameServer.Core.Protocol.Instance;
using GameServer.Core.Protocol.Login;
using GameServer.Modding;
using static Program;

namespace GameServer.Modules.Atlas;

/// <summary>The Atlas passive tree's choices and the Atlas's completed maps.</summary>
public sealed class AtlasModule : IModule
{
    /// <inheritdoc/>
    public void Load(IModuleContext module)
    {
        module.OnEnterMessages(player =>
        {
            var completed = AtlasProgress.Completed(module.World(player).CharacterData);
            return [($"0x01a3 Atlas progress, {completed.Count} map(s) completed", AtlasProgress.FullMessage(completed))];
        });
        module.OnMonsterKilled(async kill =>
        {
            if (AtlasProgress.OfArea(kill.Player.AreaId) is not { } node || !AtlasProgress.CompletesMap(node, kill.Monster)
                || !AtlasProgress.Complete(module.World(kill.Player).CharacterData, [node])) return;
            var conn = ConnOf(kill.Player);
            await Send(conn.Wire.Remote, conn.Wire.Stream, conn.Wire.Ciphers, AtlasProgress.NodeMessage(node),
                $"0x01a4 Atlas map completed: {node.Name} ({node.Id}) by {kill.Monster.Name}");
            await kill.Player.Tell($"Atlas: {node.Name} completed.");
        });
        module.OnChatCommand("atlas_complete", "/atlas_complete <map name or AtlasNode id | all> (dev: mark Atlas maps completed, saved)",
            async command =>
            {
                string what = string.Join(' ', command.Args);
                IReadOnlyList<AtlasProgress.Node> nodes = what.Equals("all", StringComparison.OrdinalIgnoreCase) ? AtlasProgress.Maps
                    : AtlasProgress.Find(what) is { } found ? [found] : [];
                if (nodes.Count == 0)
                {
                    await command.Player.Tell($"No Atlas map '{what}'. Usage: /atlas_complete <map name or AtlasNode id | all>");
                    return;
                }
                AtlasProgress.Complete(module.World(command.Player).CharacterData, nodes);
                await AtlasFeature.SendProgress(module, command.Player, "/atlas_complete");
                await command.Player.Tell(nodes.Count == 1 ? $"Atlas: {nodes[0].Name} completed." : $"Atlas: {nodes.Count} maps completed.");
            });
        module.OnChatCommand("atlas_reset", "/atlas_reset (dev: clear the Atlas's completed maps)", async command =>
        {
            AtlasProgress.Reset(module.World(command.Player).CharacterData);
            await AtlasFeature.SendProgress(module, command.Player, "/atlas_reset");
            await command.Player.Tell("Atlas: progress cleared.");
        });
        module.OnPacket(InstanceEntities.AtlasAllocateId, async p =>
        {
            var conn = ConnOf(p.Player);
            var (preset, node) = InstanceEntities.ReadAtlasAllocate(p.Bytes.ToArray());
            await AtlasFeature.ChangeAtlasPassive(conn.Wire.Remote, conn.Wire.Stream, conn.Wire.Ciphers, conn, preset, node, allocate: true);
            return true;
        });
        module.OnPacket(InstanceEntities.AtlasRefundId, async p =>
        {
            var conn = ConnOf(p.Player);
            var (preset, node, _) = InstanceEntities.ReadAtlasRefund(p.Bytes.ToArray());
            await AtlasFeature.ChangeAtlasPassive(conn.Wire.Remote, conn.Wire.Stream, conn.Wire.Ciphers, conn, preset, node, allocate: false);
            return true;
        });
    }
}

static class AtlasFeature
{
    internal static async Task SendProgress(IModuleContext module, IPlayer player, string label)
    {
        var completed = AtlasProgress.Completed(module.World(player).CharacterData);
        var conn = ConnOf(player);
        await Send(conn.Wire.Remote, conn.Wire.Stream, conn.Wire.Ciphers, AtlasProgress.FullMessage(completed),
            $"0x01a3 {label}: Atlas progress, {completed.Count} map(s) completed");
    }

    // C→S 0x3e/0x3f: a click in the Atlas passive tree. Validate the node and budget, save the preset and send all three
    // presets back so the client and the save agree. The client presents the tree's own connectivity; the server does
    // not yet have the Atlas graph, so it does not re-check pathing.
    internal static async Task ChangeAtlasPassive(string remote, NetworkStream stream, ConnectionCiphers ciphers, ClientState conn,
        byte preset, uint node, bool allocate)
    {
        if (preset >= conn.AtlasPassives.Length || node > ushort.MaxValue
            || AtlasPassiveNodes.Find((ushort)node) is not { } passive || passive.Id == "atlas_start_node")
        {
            Console.WriteLine($"[gateway] {remote} <= Atlas passive {(allocate ? "allocate" : "refund")} {node} refused: " +
                "preset or node unknown");
            await SendAtlasSlots(remote, stream, ciphers, conn, "Atlas refused");
            return;
        }
        var nodes = conn.AtlasPassives[preset];
        var character = CharacterName(conn);
        if (allocate)
        {
            if (nodes.Contains((ushort)node))
            {
                await SendAtlasSlots(remote, stream, ciphers, conn, "Atlas already allocated");
                return;
            }
            if (nodes.Count >= conn.AtlasPassivePoints)
            {
                Console.WriteLine($"[gateway] {remote} <= Atlas allocate {passive.Name} ({node}) refused: no points left");
                await SendAtlasSlots(remote, stream, ciphers, conn, "Atlas no points");
                return;
            }
            nodes.Add((ushort)node);
        }
        else if (!nodes.Remove((ushort)node))
        {
            await SendAtlasSlots(remote, stream, ciphers, conn, "Atlas not allocated");
            return;
        }
        if (character is not null)
            CharacterSaves.SetAtlasPassives(character, conn.AtlasPassives, conn.AtlasPassivePoints);
        Console.WriteLine($"[gateway] {remote} <= Atlas preset {preset} {(allocate ? "allocate" : "refund")} " +
            $"{passive.Name} ({node}), {nodes.Count}/{conn.AtlasPassivePoints} points");
        await SendAtlasSlots(remote, stream, ciphers, conn, "Atlas changed");
    }
}
