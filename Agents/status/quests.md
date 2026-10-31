# Status: Quests

Part of [Status](../Status.md); what to build next in this area is in
[roadmap/quests.md](../roadmap/quests.md).

## Works (verified with the real client)

- Quests: a1q1: new characters
  start on The Twilight Strand's beach and log in there until they reach a town;
  coming within 60 cells of Hillock sets `a1q1-SeenHillock` ("Kill Hillock");
  Hillock has the boss life bar at the top of the screen once he aggroes (StateMachine `boss_life_bar`, 0x2ba), unique
  life (311 at level 1) and a second stage: at half life he pulls out his sword (ZombieBossExtract, Transitionable → 2)
  and his life fills up again; the force wall (`Tutorial_Blocker_3`, the room's angle and scale 2.7) stands across the
  way to the gate until he dies, and the server closes the way on its own walk map too (`QuestBarriers`), so its
  position estimate stops where the client stops; after his death the quest log says to enter the gate (every
  character has `CompletedAllTutorials`); his death levels the character to 2, removes the wall and creates the gate
  into Lioneye's Watch (the Gateway checks the flag too); Portal Scrolls do nothing on the Strand (its area mod
  MapNoPortals); no Resurrect in Town before a town.
  Code: `Act1.cs`, `BossFights.cs`, `QuestBarriers` (QuestObjects module), `AreaModCatalog.cs`.
  Docs: [12](../docs/protocol/12-quests.md) §5–6.
- a1q4 "Breaking Some Eggs" in The Mud Flats (played through live, seed 1): the three Rhoa Nests stand at the captured
  tile records and each drops its glyph once per instance while the character still needs it.
  Picking a glyph up sets `a1q4-KnowsAboutGlyphs` and `a1q4-HaveGlyphN`; coming near the Glyph Wall sets
  `a1q4-SeenGlyphWall`.
  Using the wall with all three glyphs takes them, sets `a1q4-PlacedGlyphs` and plays the wall's transition (0x2ba to
  Transitionable).
  The client's invisible wall then opens, and the exit into The Submerged Passage appears; until then the server's
  walk estimate stops at the same invisible wall as the client.
  Progress is in the save: after a server restart the wall stays done and the passage open.
  Tarkleigh's "Glyph Reward" offers the six skill gems and sets `a1q4-QuestRewardTaken`.
  Needs the area's `-Walkmaps` capture (`1_1_3_seed<seed>.objects.json`).
  Code: `GlyphQuest.cs`, the QuestObjects module, `Act1.cs`.
  Docs: [12](../docs/protocol/12-quests.md) §1–4.
- a1q5 "Mercy Mission" (played through live, The Tidal Island seed 1): Nessa gives the quest and Bestel his clue
  through their own NPCTalk rows, and the quest tracker follows the flags.
  Hailrake stands at the shipwreck's `mapboss` (the medicine_boat room's captured placement) and drops the Medicine
  Chest, unless Nessa has it, the character carries one or one lies on the instance's ground.
  `a1q5-HaveMedicineChest` follows the carried chest; Nessa's "The Medicine Chest" takes it with
  `a1q5-NessaCongratulated` in one save, and a chest found after that is taken away.
  Needs the area's `-Walkmaps` captures (seed 1 has them): without them Hailrake is not placed.
  Her two rewards give a normal flask (item level 4) and a support gem of the class, once each.
  Progress survives relogs and server restarts.
  Hailrake's drop is a rule of the engine's table, the chest's flags and hand-in its `.it` Quest component's.
  Code: `MercyMission.cs`, `Act1.cs`, `QuestItems.cs`, `QuestRewardCatalog.cs`.
  Docs: [12](../docs/protocol/12-quests.md) §7.
## Partly working

- The quest engine and all of Act 1 on it, checked by the Act 1 oracle and not yet played with the client on the
  modules (a1q1, a1q4 and a1q5 under "Works" were played before they moved onto it; the other Act 1 quests below were
  played on the engine before the game moved into modules): flags per character in the save, one declarative table per
  quest, one file per act (`Modules/Core/World/QuestObjects/Acts/Act1.cs`, registered by the QuestObjects module and run
  only for the leagues that list it; [campaign quests](../docs/game/campaign-quests.md) "Adding a quest"), `/quest_flag`.
  Kills, sightings, room objects come near, exits, cleared areas, quest chests, room NPCs, NPCs that turn into
  monsters, second forms and mirrored blockers are rules; quest items (their `.it` Quest flags: held, picked up,
  handed in, removed, used), dialogue flags and scripts, session flags and the rewards come from the client's data.
  Quest passive points (QuestStaticRewards) are counted from the flags into the tree's `extra_skill_points` and stay
  after a relog; refund points are credited once (the Book of Regrets: 54 → 56); QuestStaticRewards' stats are stat
  sources. `QuestSimulator` plays an act's events through the engine; `QuestEngineTests` checks Act 1 against
  `Act1Complete`.
  What existing saves see: a character with `a10q3-SeenBoss` but without `EndGameAvailable` earned no longer has the
  Act 10 −30% resistance penalty (it follows `EndGameAvailable`, QuestStaticRewards 39); a second copy of a quest book
  (from `/item`) is used up without its points once the first was used; a quest item lying on the ground after its
  quest is done with it (a Medicine Chest after Nessa has one) goes when clicked instead of being picked up.
  Code: Gateway `Quest/` (`Quests.cs`, `QuestItems.cs`, `QuestStaticRewards.cs`, `QuestStates.cs`, `QuestSimulator.cs`),
  Game `World/Quest/`, the QuestObjects module.
  Docs: [12](../docs/protocol/12-quests.md) §5, [campaign quests](../docs/game/campaign-quests.md).
- a1q8 "A Dirty Job" and a1q7 "The Dweller of the Deep":
  Kadavrus and the Dweller spawn at `mapboss`; seeing and killing them set their flags; the last of The Fetid Pool's 96
  monsters sets `a1q8-Cleared`. Tarkleigh's rows, then his book rewards (offers 5, 6, which opened nothing before) are
  claimed; using the Book of Regrets gives two refund points, the Book of Skill a passive point (`extra_skill_points` 1).
  Docs: [12](../docs/protocol/12-quests.md) §8.
- a1q2 "The Caged Brute": Brutus spawns in the Warden's
  section (`mapboss2`); Piety's room sets `SeenPietyPrison`; his death creates the exit to Prisoner's Gate, entered;
  Tarkleigh's reward is claimed.
  Docs: [12](../docs/protocol/12-quests.md) §9.
- a1q6 "The Marooned Mariner": Fairgraves stands on his
  ship; "Marooned" gives the quest; the slave girl's chest in The Ship Graveyard Cave drops the Allflame; "Thank You"
  hands it in and turns him into the boss, who fights; his death, Bestel's book and its passive point follow.
  Docs: [12](../docs/protocol/12-quests.md) §10.
- a1q3 "The Siren's Cadence" and Act 2: Merveil, the Siren
  spawns in her arena, her death brings Merveil, the Twisted, whose death sets `a1q3-KilledSeaWitch` and creates the
  exit; the character entered The Southern Forest. Nessa's help and reward are claimed.
  Docs: [12](../docs/protocol/12-quests.md) §9.
- a1q9 "The Way Forward", Act 1: Piety's blockage at Prisoner's
  Gate sets `a1q9-SeenBlockage` and `a1q9-RoadBlocked`; the road to The Western Forest stays closed (Act 2 opens it).
  After all of Act 1 the save had every `Act1Complete` flag.
  Docs: [12](../docs/protocol/12-quests.md) §9.

- New characters pick up their class weapon, talk to the dying exile and kill the biting zombie for their skill gem.
  The support gem comes from its chest, which requires the player to walk into range.
  Placements use captured room data and gem claims are saved once; existing characters keep their items and progress.
  The complete Witch weapon, zombie/gem and chest/support-gem sequence was checked live.
  Tutorial popup acknowledgements and force walls 1/2 remain unimplemented, and placement still needs local captures.
  See [opening encounter](../docs/protocol/12-quests.md#opening-encounter-2026-10-25).
- Quest exits with a Transitionable (Merveil's exit, the arena doors) open without their animation (plain
  AreaTransition), and the arena doors are not created at all.
- Characters whose activated waypoints carry a quest flag as an access flag (a1q1's Hillock for the Coast) skip that
  flag's world changes: with OfflineWitch, The Twilight Strand's wall was not created and its gate stood open.

## Not working yet

Nothing yet.
