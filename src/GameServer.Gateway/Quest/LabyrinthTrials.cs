using System.Text.Json;
using GameServer.Core.Protocol.Instance;

/// <summary>
/// The Trials of Ascendancy (Agents/docs/game/labyrinth-trials.md): LabyrinthTrials.dat names each trial's area. Its
/// rooms (the area's <c>labyrinth_intro</c> / <c>labyrinth_end</c> nodes) hold the door, the levers and doors, the
/// plaque and the portal back as server object lines. The client counts a trial done when the character has the row's
/// quest flag (column 7, e.g. NormalLabyrinthCompletedPrison) or the league quest flag of column 1 (0x141f69230).
/// </summary>
static class LabyrinthTrials
{
    public sealed record Trial(string Area, string Flag, int LeagueFlag);

    public const string IntroDoor = "Metadata/Terrain/Labyrinth/Objects/LabyrinthIntroDoor",
        Door = "Metadata/Terrain/Labyrinth/Objects/Puzzle_Parts/Door_Closed",
        Lever = "Metadata/Terrain/Labyrinth/Objects/Puzzle_Parts/Switch_Once",
        Plaque = "Metadata/QuestObjects/Labyrinth/LabyrinthTrialPlaque",
        Portal = "Metadata/Terrain/Labyrinth/Objects/LabyrinthTrialReturnPortal";

    /// <summary>The components each type's 0x2b9 blob reads, in its order (scripts/re/ot_components.py; BaseEvents reads
    /// nothing but counts for the 0x2ba component index).</summary>
    static readonly Dictionary<string, string[]> Components = new(StringComparer.Ordinal)
    {
        [IntroDoor] = ["Positioned", "BaseEvents", "Animated", "InteractionAction", "Transitionable", "TriggerableBlockage"],
        [Door] = ["Positioned", "InteractionAction", "BaseEvents", "Animated", "Transitionable", "TriggerableBlockage", "MinimapIcon"],
        [Lever] = ["Positioned", "Transitionable", "InteractionAction", "BaseEvents", "Animated", "MinimapIcon"],
        [Plaque] = ["Positioned", "BaseEvents", "Animated", "Transitionable", "MinimapIcon", "InteractionAction"],
        [Portal] = ["Positioned", "InteractionAction", "BaseEvents", "Animated", "Transitionable", "MinimapIcon"],
    };

    /// <summary>Transitionable states: 1 runs create_untransitioned (closed, unpulled, dark), 2 the transition (num_states 2).</summary>
    public const byte Closed = 1, Open = 2;

    /// <summary>Positioned sizes (object_size − 1): the doors and the lever 4, the plaque and the portal 6.</summary>
    public static int Size(string type) => type is Plaque or Portal ? 5 : 3;

    public static bool Handles(string type) => Components.ContainsKey(type);

    /// <summary>The Transitionable component's index in the type's component order.</summary>
    public static byte TransitionableIndex(string type) => (byte)Array.IndexOf(Components[type], "Transitionable");

    /// <summary>The trial in this area, or null.</summary>
    public static Trial? For(string areaId) => All.Value.GetValueOrDefault(areaId);

    static readonly Lazy<Dictionary<string, Trial>> All = new(() =>
    {
        try
        {
            if (!File.Exists("data/tables/LabyrinthTrials.json") || !File.Exists("data/tables/WorldAreas.json")) return [];
            using var areas = JsonDocument.Parse(File.ReadAllBytes("data/tables/WorldAreas.json"));
            var areaIds = areas.RootElement.EnumerateArray().Select(a => a.GetProperty("Id").GetString() ?? "").ToList();
            var flags = Quests.ParseRows(File.Exists("data/tables/QuestFlags.json") ? File.ReadAllText("data/tables/QuestFlags.json") : "[]")
                .ToDictionary(kv => kv.Value, kv => kv.Key);
            return Parse(File.ReadAllText("data/tables/LabyrinthTrials.json"), areaIds, flags).ToDictionary(t => t.Area);
        }
        catch (Exception ex) when (ex is IOException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            Console.WriteLine($"[gateway] data/tables/LabyrinthTrials.json: {ex.Message}");
            return [];
        }
    });

    /// <summary>LabyrinthTrials rows: WorldAreas key, column 1 the league quest flag id, column 7 the QuestFlags row.</summary>
    public static List<Trial> Parse(string json, IReadOnlyList<string> areaIds, IReadOnlyDictionary<int, string> flagIds)
    {
        using var doc = JsonDocument.Parse(json);
        var trials = new List<Trial>();
        foreach (var row in doc.RootElement.EnumerateArray())
        {
            int area = row.GetProperty("WorldAreas").GetInt32(), flag = row.GetProperty("__col7").GetInt32();
            if (area < 0 || area >= areaIds.Count || !flagIds.TryGetValue(flag, out var name)) continue;
            trials.Add(new Trial(areaIds[area], name, row.GetProperty("__col1").GetInt32()));
        }
        return trials;
    }

    /// <summary>A trial object's 0x2b9 blob: its type's components (<see cref="Components"/>) with the room line's model,
    /// angle and scale. TriggerableBlockage (0x141e44290) is blocked with no sections while closed.</summary>
    public static byte[] Blob(string type, string model, int x, int y, float angle, float scale, byte state)
    {
        var w = new BlobWriter().U8(0).U8(0);
        foreach (string c in Components[type])
            switch (c)
            {
                // x, y, rotation, u16, flags0 (0x20: uniform scale, then the x / y stretches), flags1 0x02 (shown).
                case "Positioned":
                    bool scaled = scale != 1;
                    w.I32(x).I32(y).F32(angle).U16(0).U8((byte)(scaled ? 0x20 : 0)).U8(0x02);
                    if (scaled) w.F32(scale).F32(1).F32(1);
                    w.U8(0).U8(1);
                    break;
                case "Animated": if (model.EndsWith(".ao", StringComparison.OrdinalIgnoreCase)) w.U8(1).WStr32(model); else w.U8(0); break;
                case "InteractionAction": w.U8(0); break;
                case "Transitionable": w.U8(state); break;
                case "TriggerableBlockage": if (state == Closed) w.U8(1).U8(0); else w.U8(0); break;
                case "MinimapIcon": w.U8(0); break;
            }
        return w.U8(0).ToArray();   // tasks
    }
}
