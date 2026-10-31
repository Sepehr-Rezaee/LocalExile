/// <summary>An NPC a quest decides in an instance (Agents/docs/game/campaign-quests.md; Quests.NpcRule, ExistsRule, AppearsRule):
/// where it stands, whether it stands there for a flag set, the state it starts in, and its object id while created (0:
/// not). The Game module's QuestNpcs.cs creates, removes and transitions them as the character's flags change.</summary>
sealed class QuestNpc(NpcCatalog.Npc npc, int x, int y, float angle, Func<Func<string, bool>, bool> present, bool roomState)
{
    public NpcCatalog.Npc Npc { get; } = npc;
    public int X { get; } = x;
    public int Y { get; } = y;
    public float Angle { get; } = angle;
    public Func<Func<string, bool>, bool> Present { get; } = present;
    /// <summary>A room NPC outside a town starts in Transitionable state 1 (Fairgraves on his ship); a town NPC in its
    /// room line's.</summary>
    public bool RoomState { get; } = roomState;
    public uint Id { get; set; }
}
