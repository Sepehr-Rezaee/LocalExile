using GameServer.Core.Protocol.Instance;
using GameServer.Modules.M_questobjects;
using static Act1Areas;
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
        Assert.Equal([1], QuestObjectsModule.Acts().Select(a => a.Number));
        Assert.Equal(["a1q1", "a1q5", "a1q4", "a1q8", "a1q7", "a1q2", "a1q6", "a1q3", "a1q9"], All.Select(q => q.Id));
        Assert.True(RunFor(new HashSet<string> { "Game", "QuestObjects" }));
        Assert.False(RunFor(new HashSet<string> { "Game" }));
        using (Register("QuestObjects", []))
        {
            Assert.Empty(All);
            Assert.False(RunFor(new HashSet<string> { "QuestObjects" }));
        }
        Assert.Empty(All);
        registered.Dispose();
        using var again = Register("QuestObjects", QuestObjectsModule.Acts());

        // World pieces the rules use: a quest portal replaces the layout's exit without a model to its destination (not
        // a modelled one, a gate or a quest's own exit); a later form stands at its numbered boss point.
        var exits = new List<AreaTransitionObject>
        {
            new(10, 10, 0x1234), new(20, 20, 0x1234, "Metadata/x.ao"), new(30, 30, 0x1234) { SourceType = "Metadata/QuestObjects/Portal" },
            new(40, 40, 0x5678),
        };
        Assert.Equal(1, AreaTransitionCatalog.ReplacePointExits(exits, 0x1234));
        Assert.Equal([(20, 20), (30, 30), (40, 40)], exits.Select(t => (t.GridX, t.GridY)));
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
    /// a1q3 and the step into Act 2 (the same order as the campaign driver's).</summary>
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
    }
}
