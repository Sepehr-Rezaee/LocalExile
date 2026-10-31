using GameServer.Core.Protocol.Instance;
using GameServer.Data.Terrain.Generation;
using GameServer.Modules.M_questobjects;
using static Act1Areas;
using static Act2Areas;
using static Act3Areas;
using static Quests;

namespace GameServer.Gateway.Tests;

/// <summary>
/// The quest engine (Agents/docs/game/campaign-quests.md, 12-quests.md §5): its generic parts read from the client's data,
/// and the campaign oracle, which plays an act's sequence of game events through the engine (<see cref="QuestSimulator"/>:
/// the act tables the QuestObjects module registers, NPCTalk, the quest items' flags, the exits and the client's own area
/// gates). After the act the character's flags must contain CharacterStartQuestState's preset for it and every quest of the
/// preset must reach its QuestStates "complete" row. A later act adds its sequence after <see cref="Act1"/> and its preset.
/// </summary>
public sealed class QuestEngineTests
{
    static readonly ItemCatalog Items = Program.Items;
    static QuestItemFlags Flags(string item) => Items.Find(item)!.Quest!;
    static Func<string, bool> With(params string[] flags) => flags.ToHashSet().Contains;

    const string Tarkleigh = "Metadata/NPC/Act1/Tarkleigh", Nessa = "Metadata/NPC/Act1/Nessa", Bestel = "Metadata/NPC/Act1/Bestel",
        Fairgraves = "Metadata/NPC/Act1/Fairgraves";
    const string BookA1q6 = "Metadata/Items/QuestItems/SkillBooks/Book-a1q6", BookA1q7 = "Metadata/Items/QuestItems/SkillBooks/Book-a1q7",
        BookA1q8 = "Metadata/Items/QuestItems/SkillBooks/Book-a1q8";
    const string BookA1q9 = "Metadata/Items/QuestItems/SkillBooks/Book-a1q9", BookA2q5 = "Metadata/Items/QuestItems/SkillBooks/Book-a2q5";
    const string Eramir = "Metadata/NPC/Act2/Eramir", Yeena = "Metadata/NPC/Act2/Yeena", Greust = "Metadata/NPC/Act2/Greust",
        Silk = "Metadata/NPC/Act2/Silk", Helena = "Metadata/NPC/Act2/HelenaTown";
    const string BookA3q11 = "Metadata/Items/QuestItems/SkillBooks/Book-a3q11v2", BookA3q9 = "Metadata/Items/QuestItems/SkillBooks/Book-a3q9";
    const string Maramoa = "Metadata/NPC/Act3/Maramoa", Clarissa = "Metadata/NPC/Act3/ClarissaTown", Grigor = "Metadata/NPC/Act3/Grigor",
        Hargan = "Metadata/NPC/Act3/Hargan", Dialla = "Metadata/NPC/Act3/Dialla", Siosa = "Metadata/NPC/Act3/Siosa",
        FairgravesDocks = "Metadata/NPC/Act3/Fairgraves", DiallaTower = "Metadata/NPC/Act3/DiallaTower";

    /// <summary>The engine's data-driven parts: quest items by their .it Quest component (flags, drops, the carried sync,
    /// hand-ins, pickups), QuestStaticRewards (passive, refund and ascendancy points, stats; refunds credited once in the
    /// save, session flags never saved), NPCTalk scripts, reward offers without a taken flag, the generic quest object blob,
    /// the act registry and its league scoping, the world pieces the rules use (quest portals replacing an exit without a
    /// model, later forms' boss points), and every rule of every registered act naming real data.</summary>
    [Fact]
    public void The_engine_reads_quest_items_rewards_dialogue_and_objects_from_the_data()
    {
        using var registered = Register("QuestObjects", QuestObjectsModule.Acts());

        // Quest items: the .it Quest component's grant, extra, remove and use flags.
        Assert.Equal(new QuestItemFlags("a1q5-HaveMedicineChest", null, "a1q5-NessaCongratulated", null), Flags(Act1Items.MedicineChest));
        Assert.Equal(new QuestItemFlags("a1q6-HaveAllFlame", null, "a1q6-DeliveredAllFlame", null), Flags(Act1Items.Allflame));
        Assert.Equal(new QuestItemFlags("a1q4-HaveGlyph2", "a1q4-KnowsAboutGlyphs", "a1q4-PlacedGlyphs", null), Flags(GlyphQuest.Glyphs[1]));
        Assert.Equal(new QuestItemFlags("a1q8-HaveRewardBook", null, "a1q8-UsedRewardBook", "a1q8-UsedRewardBook"), Flags(BookA1q8));
        // "minus" stands for "-" only where the literal name is no flag (Act 2's Thaumetic Emblem).
        Assert.Equal("a1q9-RoadBlocked", Flags("Metadata/Items/QuestItems/SpikeSealKey").Extra);
        Assert.Null(Items.Find("Metadata/Items/Currency/CurrencyIdentification")!.Quest);

        // A quest item drops until one is carried or on the ground, or its remove_flag is set (the stash does not count).
        var chest = Flags(Act1Items.MedicineChest);
        Assert.True(QuestItems.Drops(chest, With(), carried: false, onGround: false));
        Assert.False(QuestItems.Drops(chest, With(), carried: true, onGround: false));
        Assert.False(QuestItems.Drops(chest, With(), carried: false, onGround: true));
        Assert.False(QuestItems.Drops(chest, With("a1q5-NessaCongratulated"), carried: false, onGround: false));

        // Grant flags follow the items carried, the extra flag stays, done items go with their grant flag.
        var types = Items.QuestItemTypes.Select(t => t.Quest!).ToList();
        var (set, clear, remove) = QuestItems.Sync(types, new HashSet<QuestItemFlags> { Flags(GlyphQuest.Glyphs[0]) }, With());
        Assert.Equal(["a1q4-HaveGlyph1", "a1q4-KnowsAboutGlyphs"], set.Order());
        Assert.Empty(clear);
        Assert.Empty(remove);
        (set, clear, _) = QuestItems.Sync(types, new HashSet<QuestItemFlags>(), With("a1q4-HaveGlyph1", "a1q4-KnowsAboutGlyphs"));
        Assert.Equal(["a1q4-HaveGlyph1"], clear);
        Assert.Empty(set);
        (_, clear, remove) = QuestItems.Sync(types, new HashSet<QuestItemFlags> { chest }, With("a1q5-NessaCongratulated", "a1q5-HaveMedicineChest"));
        Assert.Equal([chest], remove);
        Assert.Equal(["a1q5-HaveMedicineChest"], clear);

        // A row that sets an item's remove_flag hands it in; pickups set the grant and extra flags until the item is done.
        Assert.Equal(chest, QuestItems.HandedIn("a1q5-NessaCongratulated", types, With()));
        Assert.Equal(Flags(Act1Items.Allflame), QuestItems.HandedIn("a1q6-DeliveredAllFlame", types, With()));
        Assert.Null(QuestItems.HandedIn("a1q6-DeliveredAllFlame", types, With("a1q6-DeliveredAllFlame")));
        Assert.Null(QuestItems.HandedIn("a1q5-ReceivedQuest", types, With()));
        Assert.Equal(["a1q4-HaveGlyph3", "a1q4-KnowsAboutGlyphs"], QuestItems.PickupFlags(Flags(GlyphQuest.Glyphs[2]), With()));
        Assert.Empty(QuestItems.PickupFlags(Flags(GlyphQuest.Glyphs[2]), With("a1q4-PlacedGlyphs")));

        // QuestStaticRewards: the books' points, the bandits' stats, the Labyrinth's ascendancy points.
        var rewards = QuestStaticRewards.Default.Value;
        Assert.Equal(3, rewards.PassivePoints(["a1q6-UsedRewardBook", "a1q7-UsedRewardBook", "a1q9-UsedRewardBook", "a1q8-UsedRewardBook"]));
        var (points, credited) = rewards.NewRefunds(["a1q8-UsedRewardBook", "a1q7-UsedRewardBook"], []);
        Assert.Equal(2, points);
        Assert.Equal(["a1q8-UsedRewardBook"], credited);
        Assert.Equal(0, rewards.NewRefunds(["a1q8-UsedRewardBook"], ["a1q8-UsedRewardBook"]).Points);
        Assert.Equal(new Dictionary<string, int> { ["base_movement_velocity_+%"] = 8 }, rewards.Stats(["a2q1-QuestRewardTaken"]));
        Assert.Empty(rewards.Stats([EndGameAvailable]));   // every character's flag, Kitava's penalty row: no stats
        Assert.Equal(2, rewards.AscendancyPoints(["LabyrinthComplete1"]));

        // In the save: refund points credited once, session flags never saved.
        string path = Path.Combine(Path.GetTempPath(), $"localexile-quests-{Guid.NewGuid():N}.db");
        Saves.Open(path);
        try
        {
            CharacterSaves.ChangeQuestFlags("QuestEngine", ["a1q8-UsedRewardBook", "a1q6-FairgravesAggro"], true);
            var save = CharacterSaves.Load("QuestEngine")!;
            Assert.Equal(2, save.RefundPoints ?? 0);
            Assert.DoesNotContain("a1q6-FairgravesAggro", save.QuestFlags!);
            CharacterSaves.ChangeQuestFlags("QuestEngine", ["a1q8-UsedRewardBook"], false);
            CharacterSaves.ChangeQuestFlags("QuestEngine", ["a1q8-UsedRewardBook"], true);
            Assert.Equal(2, CharacterSaves.Load("QuestEngine")!.RefundPoints ?? 0);
        }
        finally
        {
            Saves.Db.Dispose();
            foreach (string suffix in new[] { "", "-wal", "-shm" }) File.Delete(path + suffix);
        }

        // NPCTalk scripts: Fairgraves' rows set, clear, transition him into the boss; an event's flag is not a row's.
        var npcs = NpcCatalog.Load();
        var rows = npcs.TalksOf(npcs.Find(Fairgraves)!).ToList();
        var marooned = rows.Single(t => t.Option == "Marooned");
        Assert.Contains("a1q6-ReceivedQuest", marooned.ScriptSets);
        Assert.Equal(["a1q6-DeliveredAllFlame"], marooned.ScriptClears);
        var thanks = rows.First(t => t.Option == "Thank You");
        Assert.Contains("a1q6-FairgravesAggro", thanks.ScriptSets);
        Assert.Equal([Act1Monsters.Fairgraves], thanks.TransitionsVarieties);
        Assert.True(QuestStates.Default.Value.IsSession("a1q6-FairgravesAggro"));
        Assert.Equal("a1q6-DeliveredAllFlame", TalkStartFlag(thanks));
        Assert.Null(TalkStartFlag(rows.Single(t => t.Option == "Thank You" && t.StartFlag == "a1q6-KilledFairgraves")));
        Assert.Equal(["a2q7-QuestRewardTaken", "a2b-DeliveredAmulets"],
            npcs.TalksOf(npcs.Find("Metadata/NPC/Act2/Eramir")!).First(t => t.Row == 95).ScriptSets);
        // Several quest items to their NPC in one click: Fairgraves' "A Swig of Hope" row 496 (both carried) takes the plum
        // and the decanter, his quest row 493 nothing; Hargan's "Victario's Holdfasts" every bust carried.
        Assert.Equal([Act3Items.Plum, Act3Items.Decanter], HandsIn(Act3Objects.Fairgraves, "A Swig of Hope", 496));
        Assert.Empty(HandsIn(Act3Objects.Fairgraves, "A Swig of Hope", 493));
        Assert.Equal(Act3Items.Busts, HandsIn(Act3Objects.Hargan, "Victario's Holdfasts", 355));

        // A book offer has no taken flag: its book's flags tell it taken.
        var offers = QuestRewardCatalog.Load();
        var dweller = offers.Find(6)!;
        Assert.Null(dweller.TakenFlag);
        var book = Items.Row(dweller.Choices.Single().ItemRow)!;
        Assert.Equal(BookA1q7, book.Id);
        Assert.False(QuestRewardCatalog.Taken(null, book.Quest, With("a1q7-Congratulated")));
        Assert.True(QuestRewardCatalog.Taken(null, book.Quest, With("a1q7-HaveRewardBook")));
        Assert.True(QuestRewardCatalog.Taken(null, book.Quest, With("a1q7-UsedRewardBook")));
        Assert.Equal("a1q2-NessaRewardTaken", offers.Find(8)!.TakenFlag);

        // The generic quest object blob writes the two objects traced live (a1q4's nest and wall); a chest without an .ot
        // is its Chests.dat row.
        var at = new GlyphQuest.Placement(GlyphQuest.NestTypes[0], "Metadata/Terrain/Beach/x.ao", 100, 200, 1.5f, -12.5f, 0);
        var nest = QuestObjectTypes.Find(GlyphQuest.NestTypes[0])!;
        Assert.Equal(GlyphQuest.NestBlob(at, true), new QuestObject(at.X, at.Y, nest.Components, at.Rotation, at.Z, at.Model, Opened: true).Blob());
        var wall = QuestObjectTypes.Find(GlyphQuest.Wall)!;
        Assert.Equal(GlyphQuest.WallBlob(at with { Type = GlyphQuest.Wall }, true),
            new QuestObject(at.X, at.Y, wall.Components, at.Rotation, at.Z, at.Model, TransitionState: 2).Blob());
        Assert.Equal((2, 7), (wall.States, wall.Size));
        var altar = QuestObjectTypes.Find("Metadata/Chests/QuestChests/ChurchAltar")!;
        Assert.Equal(QuestObjectTypes.Find("Metadata/Chests/QuestChests/QuestChest")!.Components, altar.Components);

        // The registry: the module's acts, in order, until the registration goes; they run only for a league that lists
        // the module that gave them.
        Assert.Equal([1, 2, 3], QuestObjectsModule.Acts().Select(a => a.Number));
        Assert.Equal(["a1q1", "a1q5", "a1q4", "a1q8", "a1q7", "a1q2", "a1q6", "a1q3", "a1q9"], All.Take(9).Select(q => q.Id));
        Assert.Equal(["a2q11", "a2q10", "a2q6", "a2q4", "a2q5", "a2q9", "a2q7", "a2q8"], All.Where(q => q.Id.StartsWith("a2")).Select(q => q.Id));
        Assert.Equal(["a3q1", "a3q2", "a3q11", "a3q3", "a3q4", "a3q5", "a3q8", "a3q12", "a3q13", "a3q9", "a3q10"],
            All.Where(q => q.Id.StartsWith("a3")).Select(q => q.Id));
        int own = All.Count;
        Assert.Same(Everything, For(new HashSet<string> { "Game", "QuestObjects" }));
        Assert.True(For(new HashSet<string> { "Game" }).IsEmpty);
        Assert.Empty(For(new HashSet<string> { "Game" }).OnMonsterKilled(Strand, Act1Monsters.Hillock));
        Assert.Equal("QuestObjects", Everything.SourceOf(Everything.ExitOf(Strand, LioneyesWatch)!));
        using (Register("Other", [new Act1()]))
        {
            // Each source on its own: a league runs only the tables of the modules it lists.
            Assert.True(For(new HashSet<string> { "Other" }).Includes("Other") && !For(new HashSet<string> { "Other" }).Includes("QuestObjects"));
            Assert.Equal(own + 9, All.Count);
        }
        using (Register("QuestObjects", []))
        {
            Assert.Empty(All);
            Assert.True(For(new HashSet<string> { "QuestObjects" }).IsEmpty);
        }
        Assert.Empty(All);
        registered.Dispose();
        using var again = Register("QuestObjects", QuestObjectsModule.Acts());

        // World pieces the rules use. A disabled entrance (a room point "entranceNdisabled" the game enables by script) is
        // created only when a quest gates it, marked as the quest's (it stands only where those tables run); one no quest
        // gates stays an arrival point. A quest portal marks the layout's exit without a model to its destination (not a
        // modelled one, a gate or a quest's own exit) as replaced: it stands only where the tables do not run. A later
        // form stands at its numbered boss point.
        const ushort strand = 0x1111, town = 0x2222, coast = 0x3333;
        AreaTransitionCatalog.AtlasConnection[] connections = [new(1, LioneyesWatch, town), new(2, Coast, coast)];
        PlacedExit Disabled(int number, int x) => new(new("Metadata/MiscellaneousObjects/AreaTransition", $"exit{number}.ao", x, 100, 0, float.NaN),
            ExitKind.Entrance, number, Enabled: false, Label: null, Target: null, Room: "room.arm", Why: "test");
        var gated = Assert.Single(AreaTransitionCatalog.FromExits(Strand, strand, connections, [Disabled(1, 100), Disabled(2, 300)], []));
        Assert.Equal((town, 100, 100, "QuestObjects", false), (gated.DestinationHash16, gated.GridX, gated.GridY, gated.QuestSource, gated.QuestReplaced));
        var exits = new List<AreaTransitionObject>
        {
            new(10, 10, 0x1234), new(20, 20, 0x1234, "Metadata/x.ao"), new(30, 30, 0x1234) { SourceType = "Metadata/QuestObjects/Portal" },
            new(40, 40, 0x5678),
        };
        Assert.Equal(1, AreaTransitionCatalog.ReplacePointExits(exits, 0x1234, "QuestObjects"));
        Assert.Equal([true, false, false, false], exits.Select(t => t.QuestReplaced && t.QuestSource == "QuestObjects"));
        var trinity = new PackCatalog.AreaPacks("2_9_8", 60, [], new Dictionary<string, PackCatalog.Pack>(), [7, 8]) { Absent = new HashSet<int> { 1 } };
        Assert.Equal(new Dictionary<int, (int X, int Y)> { [8] = (50, 60) },
            AreaPopulation.LaterFormPoints(trinity, [new("mapboss1", 10, 10), new("mapboss2", 50, 60)], (_, _) => true));

        // Every rule names real flags, monsters, items and NPCs, and the objects the engine creates are ones it can write.
        var flags = ParseRows(File.ReadAllText("data/tables/QuestFlags.json"));
        var monsters = new MonsterCatalog();
        foreach (var rule in All.SelectMany(q => q.Rules))
        {
            IEnumerable<string> named = rule switch
            {
                KillRule r => r.Flags, SeenRule r => r.Flags, NearRule r => r.Flags.Concat(r.Needs ?? []), NearExitRule r => r.Flags,
                EnteredRule r => r.Flags, UsedRule r => r.Flags.Concat(r.Needs ?? []), ClearedRule r => r.Flags.Append(r.After ?? r.Flags[0]),
                ExitRule r => [r.Flag], NpcRule r => new[] { r.GoneWith, r.TransitionedBy, r.ShownBy }.OfType<string>(),
                BlockerRule r => [r.UnblockedBy], WallRule r => [r.RemovedBy], ObjectRule r => r.TransitionedBy is { } t ? [t] : [],
                ChestRule r => r.OpensWith is { } o ? [o] : [], ExistsRule r => new[] { r.From, r.Until }.OfType<string>(),
                AppearsRule r => r.GoneWith is { } gone ? [gone] : [], ThresholdRule r => r.Flags, KilledAllRule r => r.Flags,
                TalkRowRule r => r.Flags, NearSpawnRule r => r.Until is { } u ? [u] : [], _ => [],
            };
            Assert.All(named.Where(f => !IsInstanceFlag(f)), f => Assert.True(flags.ContainsKey(f), $"{rule}: no flag {f}"));
            string? variety = rule switch
            {
                KillRule r => r.Variety, SeenRule r => r.Variety, DropRule r => r.Variety, LevelRule r => r.Variety, TurnsIntoRule r => r.Variety,
                SecondFormRule r => r.Next, FormsRule r => r.Next, SpawnRule r => r.Variety, AppearsRule r => r.Variety,
                ThresholdRule r => r.Variety, NearSpawnRule r => r.Variety, _ => null,
            };
            if (variety is not null) Assert.True(monsters.Find(variety)?.Id == variety, $"{rule}: no monster {variety}");
            string? item = rule switch { DropRule r => r.Item, ChestRule r => r.Item, HandInRule r => r.Item, _ => null };
            if (item is not null) Assert.True(Items.Find(item)?.Id == item, $"{rule}: no item {item}");
            string? npc = rule switch { NpcRule r => r.NpcType, AppearsRule r => r.NpcType, TurnsIntoRule r => r.NpcType, _ => null };
            if (npc is not null) Assert.True(npcs.Find(npc) is { Supported: true } n && n.Id == npc, $"{rule}: no supported NPC {npc}");
            string? created = rule switch { ObjectRule r => r.ObjectType, ChestRule r => r.ChestType, SpawnRule r => r.ObjectType, _ => null };
            if (created is not null)
                Assert.True(QuestObjectTypes.Find(created) is { } info && QuestObject.CanSerialize(info.Components),
                    $"{rule}: {created} is not a quest object the engine can create ({string.Join(", ", QuestObjectTypes.Find(created)?.Components ?? [])})");
        }
    }

    /// <summary>Act 1 in the game's order: a1q1, a1q5, a1q4, a1q8, a1q7, a1q2 (with a1q9's blockage on the way), a1q6,
    /// a1q3 and the step into Act 2.</summary>
    internal static void Act1(QuestSimulator s)
    {
        // a1q1 Enemy at the Gate
        s.Enter(Strand).See(Act1Monsters.Hillock).Kill(Act1Monsters.Hillock).Enter(LioneyesWatch)
            .Talk(Tarkleigh, "Introduction").Reward(Tarkleigh, "Hillock Reward");
        // a1q5 Mercy Mission
        s.Talk(Nessa, "Lioneye's Watch").Talk(Bestel, "The Medicine Chest")
            .Enter(Coast).Enter(TidalIsland).Kill(Act1Monsters.Hailrake).PickUp(Act1Items.MedicineChest)
            .Enter(LioneyesWatch, travel: true).Talk(Nessa, "The Medicine Chest")
            .Reward(Nessa, "Medicine Chest Reward 2").Reward(Nessa, "Medicine Chest Reward");
        // a1q4 Breaking Some Eggs (the Rhoa Nests give the glyphs: GlyphQuest's objects)
        s.Enter(Coast, travel: true).Enter(MudFlats).Talk(Tarkleigh, "Rhoas");
        foreach (string glyph in GlyphQuest.Glyphs) s.PickUp(glyph, given: true);
        s.Use(GlyphQuest.Wall).Enter(SubmergedPassage).Enter(LioneyesWatch, travel: true)
            .Reward(Tarkleigh, "Glyph Reward 2").Reward(Tarkleigh, "Glyph Reward");
        // a1q8 A Dirty Job
        s.Talk(Tarkleigh, "The Fetid Pool").Enter(MudFlats, travel: true).Enter(FetidPool)
            .See(Act1Monsters.BoneRhoa).See(Act1Monsters.Kadavrus).Kill(Act1Monsters.Kadavrus).Clear()
            .Enter(LioneyesWatch, travel: true).Talk(Tarkleigh, "The Necromancer").Reward(Tarkleigh, "Necromancer Reward").UseItem(BookA1q8);
        // a1q7 The Dweller of the Deep
        s.Talk(Tarkleigh, "Dweller of the Deep").Enter(SubmergedPassage, travel: true).Enter(FloodedDepths)
            .See(Act1Monsters.Dweller).Kill(Act1Monsters.Dweller)
            .Enter(LioneyesWatch, travel: true).Talk(Tarkleigh, "Dweller of the Deep").Reward(Tarkleigh, "Dweller Reward").UseItem(BookA1q7);
        // a1q2 The Caged Brute (The Ledge, The Climb, The Lower Prison on the way)
        s.Enter(SubmergedPassage, travel: true).Enter("1_1_5").Enter("1_1_6").Enter("1_1_7_1")
            .Enter(LioneyesWatch, travel: true).Talk(Tarkleigh, "Brutus").Talk(Nessa, "Brutus").Reward(Nessa, "Prison Reward")
            .Talk(Tarkleigh, "Reaching the Forest")
            .Enter("1_1_7_1", travel: true).Enter(UpperPrison).Near("Metadata/NPC/Act1/PietyPrison")
            .See(Act1Monsters.Brutus).Kill(Act1Monsters.Brutus).Enter(PrisonersGate)
            // a1q9 The Way Forward: Piety's blockage on the road to The Western Forest
            .Near("Metadata/NPC/Act1/PietyBlockage")
            .Enter(LioneyesWatch, travel: true).Talk(Tarkleigh, "Brutus").Reward(Tarkleigh, "Brutus Reward");
        // a1q6 The Marooned Mariner
        s.Enter(PrisonersGate, travel: true).Enter(ShipGraveyard).Talk(Fairgraves, "Introduction").Talk(Fairgraves, "Marooned")
            .Enter(ShipGraveyardCave).OpenChest("Metadata/Chests/QuestChests/Fairgraves/AllFlameSlaveGirl").PickUp(Act1Items.Allflame)
            .Enter(ShipGraveyard).Talk(Fairgraves, "Thank You").Kill(Act1Monsters.Fairgraves)
            .Enter(LioneyesWatch, travel: true).Talk(Bestel, "Captain Fairgraves").Reward(Bestel, "Fairgraves Reward").UseItem(BookA1q6);
        // a1q3 The Siren's Cadence, and Act 2
        s.Talk(Nessa, "Merveil").Enter(ShipGraveyard, travel: true).Enter("1_1_11_1")
            .Enter(LioneyesWatch, travel: true).Talk(Nessa, "Merveil's Lair").Reward(Nessa, "Merveil Reward")
            .Enter("1_1_11_1", travel: true).Enter(CavernOfAnger)
            .See(Act1Monsters.Merveil).Kill(Act1Monsters.Merveil).Kill(Act1Monsters.MerveilTwisted).Enter(SouthernForest);
    }

    /// <summary>Act 2 as the driver plays it (order.py): a2q11 (a1q9's road and Bestel's book), a2q10, a2q6, a2q4, a2q5,
    /// a2q9, a2q7 killing all three bandits, a2q8 and the step into Act 3. Areas off the main road are reached by waypoint
    /// (travel), as the driver does; the client's own gates still apply.</summary>
    internal static void Act2(QuestSimulator s)
    {
        // a2q11 The Way Forward: Arteri's emblem opens the pass; Bestel's book
        s.Enter(ForestEncampment, travel: true).Talk(Silk, "Blocked Passage")
            .Enter(WesternForest, travel: true).See(Act2Monsters.Arteri).Kill(Act2Monsters.Arteri).PickUp(Act2Items.ThaumeticEmblem)
            .Use(Act2Objects.ThaumeticSeal).Enter(PrisonersGate)
            .Enter(LioneyesWatch, travel: true).Talk(Bestel, "Inspirational Poem").Reward(Bestel, "Road Reward").UseItem(BookA1q9);
        // a2q10 The Great White Beast
        s.Enter(ForestEncampment, travel: true).Talk(Yeena, "The Great White Beast")
            .Enter(OldFields, travel: true).Enter(Den).See(Act2Monsters.GreatWhiteBeast).Kill(Act2Monsters.GreatWhiteBeast)
            .Enter(ForestEncampment, travel: true).Talk(Yeena, "The Great White Beast").Reward(Yeena, "Great White Beast Reward");
        // a2q6 Intruders in Black: Helena below the ledge, Fidelitas, the Strange Device
        s.Enter("1_2_6_1", travel: true).Enter(ChamberOfSins2).Near(Act2Objects.HelenaSins).Near(Act2Objects.PietySins)
            .Kill(Act2Monsters.Fidelitas).Talk(Act2Objects.HelenaSins, "Rescued").OpenChest(Act2Objects.StrangeDevice).PickUp(Act2Items.BalefulGem)
            .Enter(ForestEncampment, travel: true).Talk(Helena, "Rescued").Talk(Greust, "Helena").Reward(Greust, "Blackguard Reward");
        // a2q4 Sharp and Cruel
        s.Talk(Helena, "Maligaro's Spike").Talk(Silk, "Maligaro's Spike")
            .Enter(WeaversChambers, travel: true).See(Act2Monsters.Weaver).Kill(Act2Monsters.Weaver).PickUp(Act2Items.MaligarosSpike)
            .Enter(ForestEncampment, travel: true).Talk(Silk, "Maligaro's Spike").Reward(Silk, "Maligaro's Spike Reward");
        // a2q5 Through Sacred Ground
        s.Talk(Yeena, "The Fellshrine Hand").Enter(CryptLevel2, travel: true).OpenChest(Act2Objects.GoldenHandAltar).PickUp(Act2Items.GoldenHand)
            .Enter(ForestEncampment, travel: true).Talk(Yeena, "The Fellshrine Hand").Reward(Yeena, "Fellshrine Reward").UseItem(BookA2q5);
        // a2q9 The Root of the Problem
        s.Talk(Helena, "Lorrata").Enter(Wetlands, travel: true).Near(Act2Objects.PoisonTree).Use(Act2Objects.PoisonTree).Enter(VaalRuins);
        // a2q7 Deal with the Bandits: kill all three, Eramir's point and The Apex
        s.Enter(ForestEncampment, travel: true).Talk(Eramir, "The Bandit Scourge")
            .Enter(BrokenBridge, travel: true).Kill(Act2Monsters.Kraityn).PickUp(Act2Items.KraitynsAmulet)
            .Enter(WesternForest, travel: true).Kill(Act2Monsters.Alira).PickUp(Act2Items.AlirasAmulet)
            .Enter(Wetlands, travel: true).Kill(Act2Monsters.Oak).PickUp(Act2Items.OaksAmulet)
            .Enter(ForestEncampment, travel: true).Talk(Eramir, "Bandit Lords").Talk(Eramir, "Bandit Amulets").Talk(Eramir, "Bandit Amulets")
            .Reward(Eramir, "Take the Apex");
        // a2q8 Shadow of the Vaal: the Ancient Seal, the Apex door, the Dark Altar, the Vaal Oversoul, Act 3
        s.Enter(VaalRuins, travel: true).Use(Act2Objects.AncientSeal).Enter(NorthernForest)
            .Enter(ForestEncampment, travel: true).Talk(Yeena, "The Darkness").Talk(Eramir, "Yeena's Vision")
            .Enter(Caverns, travel: true).Near(Act2Objects.ApexDoor).Enter(AncientPyramid).Near(Act2Objects.DarkAltar).Use(Act2Objects.DarkAltar)
            .See(Act2Monsters.VaalOversoul).Kill(Act2Monsters.VaalOversoul).Enter(CityOfSarn).Enter(SarnEncampment);
    }

    /// <summary>Act 3 as the driver plays it (order.py): a2q8a3 (arrived in Act 2), a3q1 with a3q2 (Clarissa, the bracelet,
    /// the Sewer Keys), a3q11, a3q4 with a3q3, a3q5, a3q13, a3q8, a3q9, a3q12, a3q10 and the step into Act 4.</summary>
    internal static void Act3(QuestSimulator s)
    {
        // a3q1 Lost in Love: Clarissa, Tolman's bracelet, Maramoa's reward; a3q2: the Sewer Keys open the sewers
        s.Enter(SarnEncampment, travel: true).Talk(Maramoa, "Clarissa")
            .Enter(CityOfSarn, travel: true).Near(Act3Objects.ClarissaSlums).Kill(Act3Monsters.GuardCaptain).Talk(Act3Objects.ClarissaSlums, "Tolman")
            .Enter(Slums, travel: true).Enter(Crematorium).See(Act3Monsters.PietyEscape).OpenChest(Act3Objects.TolmanChair).PickUp(Act3Items.TolmansBracelet)
            .Enter(SarnEncampment, travel: true).Talk(Clarissa, "Tolman").Talk(Maramoa, "Clarissa").Reward(Maramoa, "Clarissa Reward")
            .Talk(Clarissa, "Tolman").Reward(Clarissa, "Take Sewer Keys")
            .Enter(Slums, travel: true).Near(Act3Objects.SewerGrating).Use(Act3Objects.SewerGrating).Enter(Sewers);
        // a3q11 Victario's Secrets
        s.Enter(SarnEncampment, travel: true).Talk(Hargan, "Introduction").Talk(Hargan, "Victario").Enter(Sewers, travel: true);
        for (int i = 0; i < 3; i++) s.OpenChest(Act3Objects.Stashes[i]).PickUp(Act3Items.Busts[i]);
        s.Enter(SarnEncampment, travel: true).Talk(Hargan, "Victario's Holdfasts").Reward(Hargan, "Platinum Bust Reward").UseItem(BookA3q11);
        // a3q3 The Gemling Queen and a3q4 The Ribbon Spool
        s.Talk(Grigor, "The Gemling Queen").Enter(Battlefront, travel: true).OpenChest(Act3Objects.RibbonSpoolChest).PickUp(Act3Items.RibbonSpool)
            .Enter(SolarisTemple2, travel: true).Talk(Dialla, "Introduction").Talk(Dialla, "The Ribbon Spool").Reward(Dialla, "Ribbon Spool Reward");
        // a3q5 Fiery Dust
        s.Talk(Dialla, "The Sewers").Talk(Dialla, "Thaumetic Sulphite")
            .Enter(Docks, travel: true).OpenChest(Act3Objects.SulphiteChest).PickUp(Act3Items.ThaumeticSulphite)
            .Enter(SolarisTemple2, travel: true).Talk(Dialla, "Thaumetic Sulphite").Talk(Dialla, "Infernal Talc").Reward(Dialla, "Take Infernal Talc")
            .Enter(Sewers, travel: true).Near(Act3Objects.BioWall).Use(Act3Objects.BioWall).Enter(EbonyBarracks);
        // a3q13 A Swig of Hope (Fairgraves died in Act 1): row 496 takes the plum and the decanter in one click
        s.Enter(Marketplace, travel: true).OpenChest(Act3Objects.DecanterChest).PickUp(Act3Items.Decanter)
            .Enter(ImperialGardens, travel: true).OpenChest(Act3Objects.PlumTree).PickUp(Act3Items.Plum)
            .Enter(Docks, travel: true).Talk(FairgravesDocks, "Introduction");
        while (!s.Has("a3q13-DeliveredItems")) s.Talk(FairgravesDocks, "A Swig of Hope");
        s.Reward(FairgravesDocks, "Swig of Hope Reward");
        // a3q8 Sever the Right Hand
        s.Enter(SarnEncampment, travel: true).Talk(Maramoa, "General Gravicius").Enter(Battlefront, travel: true).Near(Act3Objects.BridgeBarrier)
            .Enter(EbonyBarracks, travel: true).See(Act3Monsters.Gravicius).Kill(Act3Monsters.Gravicius)
            .Enter(SarnEncampment, travel: true).Talk(Maramoa, "General Gravicius").Reward(Maramoa, "Gravicius Reward");
        // a3q9 Piety's Pets
        s.Talk(Grigor, "Piety").Enter(LunarisTemple2, travel: true).See(Act3Monsters.Piety).Kill(Act3Monsters.Piety).PickUp(Act3Items.TowerKey)
            .Enter(SarnEncampment, travel: true).Talk(Grigor, "Piety").Reward(Grigor, "Piety Reward").UseItem(BookA3q9);
        // a3q12 A Fixture of Fate: the loose candle opens the hidden door for the instance only
        s.Enter(Library, travel: true).Talk(Siosa, "Introduction").Talk(Siosa, "Four Golden Pages")
            .Use(Act3Objects.LooseCandle).Enter(Archives);
        for (int i = 0; i < 4; i++) s.OpenChest(Act3Objects.BookStands[i]).PickUp(Act3Items.GoldenPages[i]);
        s.Enter(Library).Talk(Siosa, "Four Golden Pages").Talk(Siosa, "Four Golden Pages").Reward(Siosa, "Golden Pages Reward");
        // a3q10 The Sceptre of God, and Act 4
        s.Enter(ImperialGardens, travel: true).Use(Act3Objects.EpicDoorLock).Enter(SceptreOfGod).Enter(UpperSceptreOfGod)
            .See(Act3Monsters.Dominus).Kill(Act3Monsters.Dominus).Kill(Act3Monsters.DominusAscendant)
            .Talk(DiallaTower, "Dominus").Talk(DiallaTower, "Highgate").Enter(Aqueduct).Enter("1_4_town");
    }

    /// <summary>Plays an act's sequence, failing the test with the step the game would refuse.</summary>
    static void Play(QuestSimulator s, Action<QuestSimulator> act)
    {
        try { act(s); }
        catch (QuestSimulator.StepFailed e) { Assert.Fail($"{e.Message}\nafter: {string.Join(" ", s.Log.TakeLast(12))}"); }
    }

    /// <summary>The CharacterStartQuestState preset's flags are all set (but <paramref name="later"/>, a later act's step a
    /// preset also holds) and every quest of it with a log reached its "complete" row.</summary>
    static void PresetHolds(QuestSimulator s, string presetId, params string[] later)
    {
        var states = QuestStates.Default.Value;
        var preset = states.FindPreset(presetId)!;
        Assert.Equal([], preset.Flags.Where(f => !s.Has(f) && !later.Contains(f)).ToArray());
        // A quest without QuestStates rows (a3q2, the Sewer Keys: shown through a3q1's log) has no "complete" row to reach.
        Assert.Empty(preset.Quests.Where(q => states.Of(q).Count > 0 && !states.Complete(q, s.Has))
            .Select(q => $"{presetId} {q}: {states.Current(q, s.Has)?.Text ?? "not started"}"));
    }

    /// <summary>The Act 1 oracle: the act's events give CharacterStartQuestState's Act1Complete and every quest's "complete"
    /// row, the books' points, and the way into Act 2; the exits stay closed until their bosses die.</summary>
    [Fact]
    public void Act1_plays_through_the_engine_to_its_preset()
    {
        using var registered = Register("QuestObjects", QuestObjectsModule.Acts());
        var s = new QuestSimulator();
        try { Act1(s); }
        catch (QuestSimulator.StepFailed e) { Assert.Fail($"{e.Message}\nafter: {string.Join(" ", s.Log.TakeLast(12))}"); }
        var states = QuestStates.Default.Value;
        var preset = states.FindPreset("Act1Complete")!;
        Assert.Equal([], preset.Flags.Where(f => !s.Has(f)).ToArray());
        Assert.Empty(preset.Quests.Where(q => states.Of(q).Count > 0 && !states.Complete(q, s.Has))
            .Select(q => $"{q}: {states.Current(q, s.Has)?.Text ?? "not started"}"));
        // Books of Skill from a1q6 and a1q7 (a1q9's comes in Act 2), the Book of Regrets' two refund points; the quest items
        // all handed in or used.
        Assert.Equal((2, 2), (s.PassivePoints, s.RefundPoints));
        Assert.Equal(SouthernForest, s.Area);
        Assert.Empty(s.Carried);
        // a1q9 is under way: Piety's blockage seen, the road still blocked (it opens in Act 2).
        Assert.True(s.Has("a1q9-QuestReceived") && s.Has("a1q9-SeenBlockage") && s.Has("a1q9-RoadBlocked"));
        Assert.False(s.Has("a1q9-RoadOpened") || states.Complete("a1q9", s.Has));
        // Hillock's kill levels the character to 2; Fairgraves and Merveil's second form come with their fights.
        Assert.Equal(2, LevelAfterKill(Strand, Act1Monsters.Hillock));
        Assert.True(QuestSpawned(ShipGraveyard, Act1Monsters.Fairgraves) && QuestSpawned(CavernOfAnger, Act1Monsters.MerveilTwisted));
        Assert.False(QuestSpawned(CavernOfAnger, Act1Monsters.Merveil));

        // Brutus must die before Prisoner's Gate, Merveil in her second form before The Southern Forest; the road to The
        // Western Forest waits for Act 2.
        var brutus = new QuestSimulator();
        brutus.Set(["a1q1-KilledHillock", GlyphQuest.PlacedGlyphs]).Enter("1_1_7_1", travel: true).Enter(UpperPrison);
        Assert.Throws<QuestSimulator.StepFailed>(() => brutus.Enter(PrisonersGate));
        brutus.Kill(Act1Monsters.Brutus).Enter(PrisonersGate);
        Assert.Throws<QuestSimulator.StepFailed>(() => brutus.Enter(WesternForest));
        var merveil = new QuestSimulator();
        merveil.Set(["a1q1-KilledHillock", GlyphQuest.PlacedGlyphs, "a1q2-KilledBrutus"]).Enter(CavernOfAnger, travel: true).Kill(Act1Monsters.Merveil);
        Assert.Throws<QuestSimulator.StepFailed>(() => merveil.Enter(SouthernForest));
        merveil.Kill(Act1Monsters.MerveilTwisted).Enter(SouthernForest);

        // Act 2 after Act 1, killing all three bandits: Act2CompleteEramir (it also has Act 3's first step, Clarissa rescued
        // in The City of Sarn); a1q9's and a2q5's books and Eramir's point, a2q5's refund points; The Apex went on the altar.
        Play(s, Act2);
        PresetHolds(s, "Act2CompleteEramir", "a3q1-RescuedGirl");
        Assert.Equal((5, 4), (s.PassivePoints, s.RefundPoints));
        Assert.Equal(SarnEncampment, s.Area);
        Assert.Empty(s.Carried);
        Assert.True(s.Has("FinishedVaalOversoulQuest") && s.Has("a1q9-RoadOpened"));
        // Act 3 after Act 2: Act3Complete (the Act 2 preset's Clarissa too); a3q11's and a3q9's books, a3q11's refund points.
        Play(s, Act3);
        PresetHolds(s, "Act2CompleteEramir");
        PresetHolds(s, "Act3Complete");
        Assert.Equal((7, 6), (s.PassivePoints, s.RefundPoints));
        Assert.Equal("1_4_town", s.Area);
        Assert.Empty(s.Carried);
        Assert.True(QuestSpawned(AncientPyramid, Act2Monsters.VaalOversoul) && QuestSpawned(UpperSceptreOfGod, Act3Monsters.DominusAscendant));

        // The Thaumetic Seal and the tree need their items, the Strange Device Helena's rescue.
        var seal = new QuestSimulator();
        seal.Set(["a1q3-KilledSeaWitch", "a1q2-KilledBrutus"]).Enter(WesternForest, travel: true);
        Assert.Throws<QuestSimulator.StepFailed>(() => seal.Use(Act2Objects.ThaumeticSeal));
        Assert.Throws<QuestSimulator.StepFailed>(() => seal.Enter(PrisonersGate));
        seal.Kill(Act2Monsters.Arteri).PickUp(Act2Items.ThaumeticEmblem).Use(Act2Objects.ThaumeticSeal).Enter(PrisonersGate);
        Assert.DoesNotContain(Act2Items.ThaumeticEmblem, seal.Carried);
        seal.Enter(Wetlands, travel: true);
        Assert.Throws<QuestSimulator.StepFailed>(() => seal.Use(Act2Objects.PoisonTree));
        Assert.Throws<QuestSimulator.StepFailed>(() => seal.Enter(VaalRuins));
        seal.Enter(ChamberOfSins2, travel: true).Kill(Act2Monsters.Fidelitas);
        Assert.Throws<QuestSimulator.StepFailed>(() => seal.OpenChest(Act2Objects.StrangeDevice));

        // The Archives open with the loose candle for the instance only (nothing saved); the tower top's exit to The
        // Aqueduct (disabled in the layout, created because a3q10 gates it) waits for Dominus' second form.
        string[] sarn = ["a1q3-KilledSeaWitch", "a1q2-KilledBrutus", "FinishedVaalOversoulQuest", "a3q11-UsedSewerKeys", "a3q5-UsedTalc"];
        var library = new QuestSimulator();
        library.Set(sarn).Enter(Library, travel: true);
        Assert.Throws<QuestSimulator.StepFailed>(() => library.Enter(Archives));
        library.Use(Act3Objects.LooseCandle);
        Assert.DoesNotContain(Act3Objects.HiddenDoorOpen, library.Flags);
        library.Enter(Archives);
        Assert.Equal("a3q10-KilledDominus", FlagToEnter(UpperSceptreOfGod, Aqueduct));
        var tower = new QuestSimulator();
        tower.Set([.. sarn, "a3q9-UsedTowerKey"]).Enter(UpperSceptreOfGod, travel: true).Kill(Act3Monsters.Dominus);
        Assert.Throws<QuestSimulator.StepFailed>(() => tower.Enter(Aqueduct));
        tower.Kill(Act3Monsters.DominusAscendant).Enter(Aqueduct);
    }
}
