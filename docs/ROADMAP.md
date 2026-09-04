# Roadmap

Ordered by dependency, not by excitement. Each milestone should end with
something you can play and form an opinion about.

---

## M0 — Where the repo is now ✅

Playable economic sandbox. Build, gather, convert, advance ages, research, trade,
spy, watch an AI do the same. No combat, no win condition, no art.

---

## M1 — Make the core loop legible

*Goal: a first-time player understands why their economy is doing what it is doing.*

- [ ] **Bottleneck callout.** A single line in the HUD: "Short on Energy" /
      "Understaffed — 4 more workers". The labour percentage alone is not enough,
      and "everything is slower and I do not know why" is the biggest risk in the
      current build.
- [ ] **Price impact preview.** Show the price *before and after* a proposed trade
      in the market panel, so the player learns that their own order moves it.
- [ ] **Intel ageing made visible.** Ghost the previous estimate next to the
      current one so decay is something you watch happen.
- [ ] **Tutorial skirmish.** Scripted first five minutes against a passive AI.

## M2 — Make the match end

*Goal: it is a game rather than a toy.*

- [ ] Win conditions: economic dominance (X% of total market value for 90 s),
      bankruptcy elimination, or a timed high score.
- [ ] Post-match screen from `EconomyStats` — it already tracks everything needed.
- [ ] Match setup screen: pick civ, opponents, map size, AI difficulty.
- [ ] Save / load a match.

## M3 — Combat, minimally

*Goal: a defenceless player can be punished, and the map is worth holding.*

- [ ] Give `Security Detail` an actual attack, and buildings the ability to be
      destroyed by one.
- [ ] Raiding: units that damage deposits, workers and trade infrastructure.
      Combat here should be an *economic* attack, not a separate war game.
- [ ] Static defence around key buildings.
- [ ] Physical protection of agents: an enemy Directorate raid should be a real
      counter to a spy-heavy opponent.

Deliberately last of the "core" milestones. Combat is the most expensive system
in any RTS and the least novel part of this design.

## M4 — Scale and feel

- [ ] Flow-field pathfinding with local avoidance. Not NavMeshAgent-per-unit — it
      does not scale past a couple of hundred units.
- [ ] Terrain: heightmap, chokepoints, deposits that are worth defending.
- [ ] Building footprints with real collision and grid snapping.
- [ ] Port the HUD to UI Toolkit.
- [ ] Assembly definitions (`Statecraft.Core`, `Statecraft.Editor`) once compile
      times bite.
- [ ] Port `Content.cs` to ScriptableObjects once the numbers stop moving weekly.

## M5 — Depth

- [ ] Double agents and turned assets — an agent inside their Directorate that
      feeds you continuous intel instead of one-shot snapshots.
- [ ] Futures contracts: commit to deliver Components at a price in 60 seconds.
      Makes market manipulation devastating and gives sabotage a second purpose.
- [ ] Real diplomacy: non-aggression pacts, joint embargoes, intel sharing.
- [ ] Civ-unique buildings and operations rather than only numeric bonuses.
- [ ] More civilisations.

## M6 — Multiplayer

- [ ] Deterministic lockstep. `SimClock` was built with this in mind; the blockers
      are `UnityEngine.Random` in `EspionageSystem` and `MarketSystem` (needs a
      seeded PRNG in the sim) and float determinism across platforms.
- [ ] Spectator mode — this design is unusually watchable, since the whole game is
      visible in the market and intel panels.

---

## Standing technical debt

| Item | Where | Severity |
|---|---|---|
| `UnityEngine.Random` used inside the sim | `EspionageSystem`, `MarketSystem`, `AIController` | Blocks determinism and replays |
| `Entity.ApplyFactionColour` allocates a material per renderer | `Entity.cs` | Minor; fix with MaterialPropertyBlocks |
| No unit tests | everywhere | The economy tick is pure enough to test properly — start there |
| Buildings can overlap | `SelectionController.UpdatePlacement` | Placement validity check needed |
| No object pooling | `Spawner` | Will matter at a few hundred units |
