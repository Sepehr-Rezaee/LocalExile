# Trials of Ascendancy

The trials in the acts that open each Labyrinth tier (#450, roadmap "The Labyrinth" in
[roadmap/world.md](../../roadmap/world.md)).
The Lower Prison's Normal trial is verified with the real client: its door, lever, Locked Door, plaque and the
Trial Plaque window work.
Traps, the trials' monsters and the other trials' puzzles are not built yet.

## Where a trial is

`LabyrinthTrials.dat` has 18 rows: the six Normal, three Cruel and three Merciless trials in act areas, and the six
Eternal ones (`EndGame_Labyrinth_trials_*`, reached from maps).
The server's level generator already generates every one of these areas.

A trial is two rooms of the area's graph: the `labyrinth_intro` node and the `labyrinth_end` node.
In The Lower Prison they take `Labyrinth/RoomSets/Prison/Rooms/Unique/axiom_intro.arm` and `axiom_intro_end.arm`.
Some trials have both ends in one room (`crypt_intro.arm`, `crematorium_intro.arm`).
The rooms' object lines hold the whole trial; `data/room-points.json` keeps them with their type, model, angle and
scale, and `AreaPopulation.LoadObjects` places them through the room's placement.

| Room | Objects (besides doodads) |
|---|---|
| `axiom_intro` (Lower Prison) | `LabyrinthIntroDoor`, `Door_Closed`, `Switch_Once`, 16 `LabyrinthCascadeSpikeTrap`, `Trigger_PulsePrisonWG`, the glyph |
| `axiom_intro_end` | `LabyrinthTrialPlaque`, `LabyrinthTrialReturnPortal`, 10 spike traps, two delay triggers, `PrisonPoacherSpawner` |
| `catacomb_intro` / `_end` | the door, one `Door_Closed` and `Switch_Once`, roombas; the plaque and the portal |
| `crypt_intro` | the door, `Door_Closed`, `Switch`, `Switch_Multi`, `Switch_Once`, spinners, the plaque, the portal, `CryptFrostPriestSpawner` |
| `crematorium_intro` | the door, three `Door_Closed` and `Switch_Once`, five `TimerGears`, lava, the plaque, the portal, `CremPyroSpawner` |
| `intro` / `intro_end` (Chamber of Sins) | the door, two `Door_Open` and `Switch`, sawblades; the plaque and the portal |

The objects are `server_only` (BaseEvents): the client creates none of them, so the server places every one.

## The objects

Each type's 0x2b9 blob follows its own component order (`scripts/re/ot_components.py`); BaseEvents reads nothing
but counts for the 0x2ba component index.

| Type | Components read | Transitionable index |
|---|---|---|
| `Labyrinth/Objects/LabyrinthIntroDoor` | Positioned, BaseEvents, Animated, InteractionAction, Transitionable, TriggerableBlockage | 4 |
| `Puzzle_Parts/Door_Closed` ("Locked Door") | Positioned, InteractionAction, BaseEvents, Animated, Transitionable, TriggerableBlockage, MinimapIcon | 4 |
| `Puzzle_Parts/Switch_Once` (lever) | Positioned, Transitionable, InteractionAction, BaseEvents, Animated, MinimapIcon | 1 |
| `QuestObjects/Labyrinth/LabyrinthTrialPlaque` | Positioned, BaseEvents, Animated, Transitionable, MinimapIcon, InteractionAction | 3 |
| `Labyrinth/Objects/LabyrinthTrialReturnPortal` | Positioned, InteractionAction, BaseEvents, Animated, Transitionable, MinimapIcon | 4 |

Animated carries the room line's model (`LabyrinthDoor.ao`, `doorbar_prison.ao`, `lever_v01_04.ao`,
`labyrinth_plaque.ao`, `LabyrinthAirlockPortal.ao`).
Transitionable state 1 runs `create_untransitioned` (closed, unpulled, dark), 2 the transition: the client plays the
`.ot`'s `on_transition` itself (the door's opening, the plaque's glow, Izaro's speech, the portal opening).
TriggerableBlockage is u8 blocked, then while blocked a u8 count of sections (none sent).

**What the Gateway does** (`LabyrinthTrials` in the Gateway, `Rules.LabyrinthTrials.cs` in QuestObjects):
- on entering a trial's area it creates these objects; a door also closes its line across the way between the
  area's spawn and the plaque on the server's walk estimate (`QuestBarriers`, `ClientState.DoorBarriers`);
- using the trial's door opens it (0x2ba state 2, then 0x1c6 unblocked);
- a lever opens itself and the nearest Locked Door that is still closed (our rule: no file links a lever to its door);
- the plaque sets the trial's quest flag, opens the portal and opens the Trial Plaque window; using it again only
  opens the window;
- the open portal teleports the player back to the trial's door;
- what an instance opened stays open for later entries (`InstanceState.Transition`).

## Unblocking a door

TriggerableBlockage has no 0x2ba reader (its vtables' slot 20 is `ret`).
Its own world message is **S→C 0x1c6** (read `0x141fe14b0`, world handler `0x142187480`): the world header, u8
blocked, and while blocked a u8 count and that many u8 sections.
Unblocked calls the component's +0xb8 (`0x141e43b60`), which writes back the cells it saved when it blocked unless
the type's `disable_save` (+0x11) is set; blocked calls +0xb0 with the sections.
Of the `.ot` keys (`0x141e43200`) `start_blocked` is +0x10, `disable_save` +0x11, `shunt_objects_away` +0x12;
`save_as_diff` is accepted and not kept.

This opens the trial's door.
The Locked Door (`doorbar_prison.ao`, whose Hull fills the gap between the two posts of the `PDwall_blocking01` doodad
at the same spot) stayed closed to walking after 0x1c6, and also when created again open and unblocked; a movement
skill still crossed it (live 2026-10-31).
The client's object pass `0x141d88690` stamps an object's Hull (`0x141d876e0`) unconditionally for some types and only
while their TriggerableBlockage is blocked for the others (type +0x158 → +0x19, type +0x1e7 bit 1); which flag puts the
Locked Door in the first group is not found.
So the Gateway removes an opened Locked Door (0x2bb) 1.5 s after its opening, when the bars have lifted, and does not
create it again in that instance.

## A completed trial

The client counts a trial done (`0x141f69230`, for each of a Labyrinths row's `RequiredTrials`) when the player has
**the row's quest flag** (column 7, a QuestFlags row: `NormalLabyrinthCompletedPrison` …
`MapLabyrinthCompletedCatacombs`, rows 2564–2581, tested by `0x1400f42b0`) or **the league quest flag** of column 1 (263–286; Player +0x1d0, bit id − 1,
`0x14012aae0`).
The real game shares trials across a league's characters; the league flags are how, but the Gateway does not save
league quest flags yet, so a trial is the character's own quest flag.
Columns 2 and 3 are not read.
A Labyrinths row is unlocked by its own `QuestFlag` (`LabyrinthComplete1` …).

**The Trial Plaque window** (`AscendancyMapWindowTitle`, built by `0x140b75b90`) is **S→C 0x7b kind 9**: instance
+0x19f8 → `0x140dcda40`, which shows the live HUD's +0x7e8.
It lights each trial from the flags above (`0x140b76170` → `0x141f69230`); the message carries nothing for it.
Kind 0x66 (HUD virtual +0x1028) shows the live HUD's +0xa30, Mirage's Wishes window, not this one: the other HUD class
(`0x140d65b50`) keeps the plaque window at +0xa30.

## Not built yet

- Traps (`LabyrinthCascadeSpikeTrap`, spinners, sawblades, roombas, lava, darts) and their triggers.
- The other puzzle parts: `Switch` and `Door_Open` (toggling), `Switch_Multi`, `TimerGears` (timed levers).
- The trials' monster spawners (`PrisonPoacherSpawner`, `CryptFrostPriestSpawner`, `CremPyroSpawner`).
- The glyph (`LabyrinthTrialGlyphPrison`, an NPC with no `.ot` of its own).
- Saving league quest flags, so that trials are shared across a league's characters.
- The Eternal trials' plaques drop an Offering to the Goddess.
