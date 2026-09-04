# Statecraft

A modern-era real-time strategy game in the Age of Empires mould, where the
battle between civilisations is fought with **production, prices and spies**
rather than with armies.

Unity 6 (6000.4.1f1) · URP · C#

---

## The pitch in one paragraph

You build a base, gather resources and advance through ages, exactly as you
would in Age of Empires. What is different is what you do with the economy once
you have one. Every resource has a **price on a shared market that your own
trades move**, so dumping a commodity your rival depends on selling is a real
attack. And you cannot see your rival's books — their stockpiles, their
technology, their exposure — without **running an operation to find out**.
Espionage is not a support mechanic here; it is the information layer that makes
economic warfare playable at all.

---

## Run it

1. Open the project in Unity **6000.4.1f1** (Unity Hub → Add → this folder).
2. Menu: **Statecraft ▸ New Skirmish Scene**.
3. Press **Play**.

There is nothing else to set up. No prefabs to wire, no NavMesh to bake, no
scene file you have to have open. The entire match — camera, lighting, terrain,
factions, systems, HUD — is constructed at runtime by `MatchBootstrap`. That
constraint is deliberate: a prototype that only runs from one blessed `.scene`
file rots the first time someone opens it on another machine.

### Controls

| Action | Input |
|---|---|
| Pan | `WASD` / arrows / screen edge |
| Rotate | `Q` / `E` |
| Zoom | Mouse wheel |
| Select | Left click, or drag a box |
| Add to selection | `Shift` + click |
| Order / rally | Right click |
| Place several buildings | Hold `Shift` while placing |
| Clear selection | `Esc` |

---

## What is actually implemented

| System | State |
|---|---|
| Fixed-tick simulation clock (10 Hz) | ✅ working |
| Five-resource economy with labour and energy constraints | ✅ working |
| Worker gathering loop (AoE-style) | ✅ working |
| Buildings, construction, conversion recipes, training queues | ✅ working |
| Four ages with cost gates and output multipliers | ✅ working |
| Eight technologies with prerequisites | ✅ working |
| Four civilisations with real, readable bonuses | ✅ working |
| Commodity market with price impact and mean reversion | ✅ working |
| Standing trade routes | ✅ working |
| Eight espionage operations with two-roll resolution | ✅ working |
| Decaying intelligence with noise and disinformation | ✅ working |
| Tension / embargo diplomacy | ✅ working |
| Economic AI opponent (no cheating) | ✅ working |
| Full IMGUI HUD: market, intel, operations, log | ✅ working |
| Combat | ❌ stub only — `security` units exist, they do not fight |
| Pathfinding | ❌ direct steering on a flat plane |
| Art, audio, animation | ❌ primitives and flat colours |
| Multiplayer | ❌ not started |
| Win conditions | ❌ not started |

See [`docs/ROADMAP.md`](docs/ROADMAP.md) for what to do about that, in order.

---

## Repository layout

```
Assets/Scripts/
  Core/        SimClock, EventLog, Spawner, MatchBootstrap, MaterialFactory
  Data/        ResourceKind, Defs, Content     ← every balance number lives here
  Factions/    Faction, FactionRegistry, DiplomacySystem
  Economy/     EconomySystem, MarketSystem, TradeRouteSystem, EconomyStats
  Espionage/   EspionageSystem, IntelSystem
  Entities/    Entity, UnitEntity, WorkerUnit, AgentUnit, BuildingEntity, ResourceNode
  AI/          AIController
  Player/      RTSCameraController, SelectionController
  UI/          HUD, HudPaint
  Editor/      StatecraftSceneSetup
docs/          GDD, ARCHITECTURE, ECONOMY, ESPIONAGE, ROADMAP
```

---

## Documentation

- [`docs/GDD.md`](docs/GDD.md) — what the game is and why each mechanic is there
- [`docs/ECONOMY.md`](docs/ECONOMY.md) — the production and price model, with the formulas
- [`docs/ESPIONAGE.md`](docs/ESPIONAGE.md) — operation resolution and the intel model
- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — how the code is organised and why
- [`docs/ROADMAP.md`](docs/ROADMAP.md) — milestones, in dependency order
