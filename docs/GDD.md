# Statecraft — Design Document

## 1. The one-sentence pitch

Age of Empires, moved to the modern era, where the war between civilisations is
fought over **prices and information** and espionage is the core mechanic rather
than a side system.

## 2. What is kept from Age of Empires

These are load-bearing. Break them and the game stops feeling like an RTS.

- **Workers gather and haul.** A worker walks to a deposit, gathers, carries the
  load back to a drop-off, repeats. Visible, interruptible, spatial.
- **Base building.** You place buildings on a map, they take time to construct,
  they occupy ground.
- **Ages.** Four era gates. Each costs a lump of resources, takes real time, and
  unlocks buildings, units, technologies and operations.
- **Civilisations with bonuses.** Four blocs, each with a readable identity.
- **Production queues.** Buildings train units over time.
- **Finite deposits.** Resources run out. The map is worth fighting over.

## 3. What is different

### 3.1 The economy is a supply chain, not five counters

Resources feed each other:

```
   Deposits ──workers──▶ Materials ──┬──▶ Power Plant ──▶ Energy
                                     │                      │
                                     └──▶ Fabricator ◀──────┤──▶ Components
                                                            │        │
                                          Data Centre ◀─────┴────────┘──▶ Data
```

Two global constraints make this bite:

- **Labour.** Every running building consumes labour slots from one shared pool
  filled by your workers. Build more factories than you can staff and
  *everything* slows down proportionally. Worker count is a strategic decision,
  not a "build until you stop thinking about it" number.
- **Energy.** Energy is an input like any other. When it runs short every
  downstream recipe throttles at once, so a power shortfall cascades. You feel
  it as your whole economy dimming, not as one building stopping.

### 3.2 Prices are a weapon

There is one global commodity market. Prices are not scripted — they move with
order flow, and thin markets (Data) move far more than deep ones (Materials).

That turns three real strategies into game actions:

- **Dumping** — sell hard into the commodity your rival funds themselves with.
- **Cornering** — buy up a thin market before they need to buy into it.
- **Front-running** — trade ahead of a move you know they are about to make.

The third one is only possible if you know their position. Which is the point.

### 3.3 The fog is over information, not terrain

You can see their buildings. You cannot see their **books**. Stockpiles,
technology, security posture and exposure are all hidden behind the
`IntelSystem`, which gives you a snapshot that decays: the older your intel, the
noisier the numbers you are shown, and the HUD tells you how much to trust them.

This is the design's central bet. In a normal RTS, scouting tells you what to
build. Here, intelligence tells you **where the money is**, and therefore which
economic attack is worth paying for. Attacking a rival's Data reserve is
brilliant if they are dependent on it and a waste of 450 Capital if they are not.

### 3.4 Espionage is the core loop

Eight operations, each a bet with two independent rolls: did it work, and were
you caught. See [ESPIONAGE.md](ESPIONAGE.md).

The economy of espionage is **cover**, not money. Money buys the attempt; cover
is the resource that degrades every time you use an agent, and agents regenerate
it slowly while idle. So the espionage game is portfolio management: a small
stable of agents whose usefulness you are constantly spending down.

## 4. Civilisations

| Bloc | Economic identity | Espionage identity |
|---|---|---|
| **Meridian Compact** | Trades at ~35% less friction, extra trade routes | Weak agents, expensive operations |
| **Novaya Directorate** | +25% extraction, +20% manufacturing, builds fast | Solid security, slow research, wide spreads |
| **Sable Union** | Weaker production across the board | +15 agent skill, −30% op cost, agents keep cover |
| **Kestrel Republic** | +35% Data, +30% research speed, thin extraction | Good counter-intelligence |

Every bonus is a plain multiplier on a named quantity. No hidden rules, no
"this civ secretly also gets X" — a player should be able to reason about why
they lost.

## 5. Ages

| # | Age | Theme | Unlocks |
|---|---|---|---|
| 0 | Reconstruction | Scarcity, no automation | Habitat, Power, Extractor, Academy |
| 1 | Industrial Consolidation | Heavy manufacturing, intelligence services founded | Fabricator, Exchange, Directorate, Agents |
| 2 | Information | Data becomes strategic; markets outpace armies | Data Centre, Industrial Espionage, Move the Market |
| 3 | Automation | Output decouples from labour | Autonomous Plant, Deep Cover |

Each age also multiplies all conversion output (1.00 → 1.15 → 1.32 → 1.55), so
advancing is always worth something even before the unlocks.

## 6. What is deliberately not in the game yet

- **Combat.** A `Security Detail` unit exists but does not fight. Combat is the
  most expensive system in any RTS and the least novel part of this design; it
  waits until the economic and espionage loops are proven fun.
- **Win conditions.** Currently a sandbox. The intended condition is economic:
  reach a Capital or market-share threshold, or force every rival into
  unrecoverable debt.
- **Pathfinding.** Units steer directly on a flat plane.

## 7. The open design questions

Honest list of what is not yet known to work:

1. **Is the market legible?** Price impact is invisible until you trade. The HUD
   shows a quote, but players may not connect "my sell crashed the price" to
   "that is why I got less than I expected".
2. **Is espionage too swingy?** A two-roll model with 40–65% success rates means
   long unlucky streaks. It may need a pity mechanic, or ops may need to be
   cheaper and more frequent.
3. **Does labour scarcity read as a bug?** "Everything is slower and I do not
   know why" is a real risk. The HUD shows a labour percentage; that may not be
   enough.
4. **Is intel decay frustrating rather than tense?** Being shown wrong numbers is
   only fun if the player knows they might be wrong. The confidence label is the
   mitigation; it is unproven.
5. **Does the AI actually pressure a human?** It plays the economy honestly, but
   it has no combat and cannot punish a defenceless player.
