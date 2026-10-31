# Roadmap: Quests

Part of the [Roadmap](../Roadmap.md); what already works in this area is in
[status/quests.md](../status/quests.md).
Items keep the Roadmap's priority groups, and are in rough priority order within each group.

## 3. Gameplay

### Quests

- **Known:** the quest engine with declarative tables per quest and Acts 1–6
  ([12](../docs/protocol/12-quests.md) §5–15), the oracle (`QuestSimulator`, `QuestEngineTests`); adding an act is a
  file of tables ([campaign quests](../docs/game/campaign-quests.md) "Adding an act").
- **Start:** Acts 7–10 and the Epilogue, a table per quest on the engine, each checked against the real
  game's behavior and added to the oracle with its CharacterStartQuestState preset, and the act's
  [campaign driver](../docs/dev/campaign-automation.md) step files (`scripts/dev/campaign/quests/`).
  Acts 2 to 6 and the Pantheon on the modules still need a run with the client (check that the Pantheon panel highlights
  the chosen gods); the bandit vote (a2q7's help path) and the Pantheon's souls (Divine Vessels, Sin's "Unlock Power":
  [pantheon](../docs/game/campaign/pantheon.md) §1) are not built.
  The Strand's visual pieces (the gate's animation, the Doorman;
  [12](../docs/protocol/12-quests.md) §6) and a1q4's waterfall animation (§4) are low priority.
- **Done when:** the campaign's quests can be played through, with their world changes, logs and rewards.
