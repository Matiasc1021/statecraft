# Architecture

## The shape of it

```
SimClock  ──OnTick(0.1s)──▶  EconomySystem
                             MarketSystem
                             TradeRouteSystem
                             EspionageSystem
                             DiplomacySystem
                             AIController

FactionRegistry  ◀── every system asks this "who owns what"
     │
     ├── Faction (plain C# object: stockpile, age, techs, multipliers)
     └── Entity  (MonoBehaviour: BuildingEntity, UnitEntity, WorkerUnit, AgentUnit)

Content (static)  ── every balance number, in code, diffable
```

## Five rules the codebase follows

**1. The simulation runs on a fixed tick, never in `Update()`.**
`SimClock` fires at 10 Hz. Every economic rate is expressed per second and stays
true at 30 fps or 240 fps. It also means a replay system or deterministic
lockstep netcode can be added later without rewriting every system.
`Update()` is only used for things that must be smooth: movement and input.

**2. `Faction` holds state and derived multipliers. It never mutates the world.**
Systems read and write it on tick. You can dump a `Faction` and know exactly why
its numbers are what they are.

**3. Nothing calls `FindObjectsByType` in a hot loop.**
`FactionRegistry` is the single index. An RTS runs "my buildings" and "nearest
free deposit" dozens of times per tick, and scanning the scene is the classic
reason these games start fine and die at 300 units.

**4. Content is code, not assets.**
Every cost, rate and probability lives in `Data/Content.cs`. A balance change is
a reviewable diff, not an opaque `.asset` binary. When the numbers stop moving
weekly, port it to ScriptableObjects — not before.

**5. The world is built from code, never from a scene file.**
`MatchBootstrap` constructs the camera, lighting, map, factions, systems and HUD
at runtime from primitives. The repo runs from a fresh clone with no wiring, no
missing references, and no NavMesh bake. When real art arrives, give each def a
prefab field and swap the bodies out in `Spawner` — nothing else in the codebase
knows what a unit looks like.

## Deliberate shortcuts, and what replaces them

| Shortcut | Why | Replace with |
|---|---|---|
| Direct steering, no NavMesh | "Press Play" works on a fresh clone; no bake step | Flow fields (what an RTS with hundreds of units actually wants), not NavMeshAgent-per-unit |
| IMGUI HUD | No Canvas, no prefabs, no TMP import; genuinely good for a data-dense HUD while numbers move daily | UI Toolkit once the layout settles |
| Legacy `Input` API | Two lines instead of an InputActions asset | Input System — touches only `SelectionController` and `RTSCameraController` |
| Primitives for all art | Readable, zero asset pipeline | Prefab references on `UnitDef` / `BuildingDef` |
| No assembly definitions | One less thing to get wrong at 4 000 lines | Add `Statecraft.Core` + `Statecraft.Editor` asmdefs when compile times bite |
| Singletons | Fast to write, easy to reason about at this size | A service locator or DI container if the systems list keeps growing |

## Namespaces

`Statecraft.Core`, `.Data`, `.Factions`, `.Economy`, `.Espionage`, `.Entities`,
`.AI`, `.Player`, `.EditorTools`.

Note this differs from the flat, namespace-free style of the earlier Shadow Moses
project. At this file count namespaces stop being ceremony and start preventing
collisions — `Entity`, `Content` and `HUD` are all names you would otherwise
collide with the moment you import any asset from the Store.

## Verifying changes without opening Unity

The scripts type-check against a small stub of the UnityEngine API. That catches
the whole class of "compiles in my head" errors before Unity ever opens the
project. If you keep working on this headlessly, keeping such a stub around is
worth the ten minutes it costs.
