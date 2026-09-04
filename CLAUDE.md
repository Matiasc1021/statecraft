# Statecraft — notes for Claude

Read this before changing anything. Read `docs/ARCHITECTURE.md` for the reasoning
behind these rules.

## What this project is

A modern-era RTS built on Age of Empires' structure, where the differentiators
are an **economy with real supply chains and a live commodity market** and
**espionage as the core mechanic**. Unity 6000.4.1f1, URP, C#.

## Hard rules

1. **Simulation logic goes in a `SimClock.OnTick` handler, never in `Update()`.**
   Rates are per second. `Update()` is only for movement and input.
2. **All balance numbers live in `Assets/Scripts/Data/Content.cs`.** Never
   hardcode a cost, rate or probability anywhere else.
3. **Never call `FindObjectsByType` in per-tick code.** Ask `FactionRegistry`.
4. **The world is built from code** (`MatchBootstrap`, `Spawner`). Do not
   introduce a dependency on a saved `.scene` file or on prefabs.
5. **`Faction` holds state, systems mutate it.** Do not put world-mutating logic
   on `Faction`.
6. **Bonuses are plain named multipliers.** No hidden or conditional rules — a
   player must be able to reason about why they lost.

## Style

Matches the author's existing Unity work: `// ── Section ─────` separators, XML
doc comments on every public type explaining *why* the thing exists, aligned
field declarations, `[Header]` groupings, `Debug.Log` with a `[Tag]` prefix.

Unlike the earlier Shadow Moses project, this one **does use namespaces**
(`Statecraft.*`) — at this file count they prevent collisions with Asset Store
imports.

## Verifying a change without opening Unity

The scripts type-check against a stub of the UnityEngine API (MonoBehaviour,
GameObject, Vector3, Mathf, GUI/GUILayout, and so on). Rebuild that stub and run
`mcs -target:library` over `Assets/Scripts` to catch type errors before Unity
opens. It has already caught real mistakes; it is worth maintaining.

## Where things are

| Want to change | File |
|---|---|
| Any cost, rate, probability, civ bonus | `Data/Content.cs` |
| How production works | `Economy/EconomySystem.cs` |
| How prices move | `Economy/MarketSystem.cs` |
| Operation odds and effects | `Espionage/EspionageSystem.cs` |
| How wrong rival numbers are | `Espionage/IntelSystem.cs` |
| What the AI prioritises | `AI/AIController.cs` — the *order* of the checks is the strategy |
| The HUD | `UI/HUD.cs` (IMGUI) |
| What a match starts with | `Core/MatchBootstrap.cs` |

## Current state

Playable economic sandbox. No combat, no win condition, no art. See
`docs/ROADMAP.md` — do not start M3 (combat) before M1 (legibility) is done.
