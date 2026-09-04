# The Economy Model

Everything here is implemented in `Assets/Scripts/Economy/EconomySystem.cs` and
`MarketSystem.cs`. All rates are **per second of simulation time**. The sim runs
at a fixed 10 Hz (`SimClock`), so nothing depends on frame rate.

---

## 1. Resources

| Resource | Role | Where it comes from |
|---|---|---|
| **Capital** | Money. The numeraire — always worth 1 on the market. | Trade routes, market sales, Exchange fees, insider operations |
| **Energy** | Input to almost everything. Runs short and the whole economy throttles. | Power Plant (burns Materials) |
| **Materials** | The base physical resource. | Worker gathering, Extraction Site |
| **Components** | Manufactured goods. The main export. | Fabricator (Materials + Energy) |
| **Data** | The strategic resource. Research, operations, automation. | Data Centre, Academy, Directorate, Data deposits |

Capital can go **negative**. Debt accrues interest at 0.5%/second of the
outstanding balance, which compounds fast enough that borrowing to over-expand is
a real and recoverable mistake rather than a free option.

## 2. The tick

Every 100 ms, for every faction, in this exact order:

1. **Capacity** — housing, security and labour *demand* recomputed from buildings;
   construction advances; sabotage timers tick down.
2. **Labour** — population and labour *supply* counted from units.
3. **Conversion** — recipes consume inputs and emit outputs.
4. **Production** — training queues advance; finished units spawn.
5. **Finance** — interest charged on debt.
6. **Progress** — age advancement and research.

The order matters and is why the sim is predictable: nothing in step 3 can be
affected by something that happens in step 4 of the same tick.

## 3. Conversion efficiency

A building runs at:

```
efficiency = min( labourEfficiency , supplyRatio )

labourEfficiency = clamp01( workers / (totalLabourSlots x labourRequirementMult) )
supplyRatio      = min over each required input k of clamp01( stock[k] / needed[k] )
```

Two consequences worth understanding:

- **Labour is global.** One shared pool across all your buildings. Building a
  Fabricator you cannot staff slows down your Power Plant too.
- **Shortage is proportional, not binary.** At 40% of the Energy you need, every
  energy-consuming building runs at 40%. Brownouts dim the economy rather than
  switching parts of it off, which makes the failure mode readable.

Output is then multiplied by the resource's own multiplier chain:

```
Materials   x civ.extraction    x logistics(1.15) x autonomy(1.25) x ageMultiplier
Components  x civ.manufacturing x assembly(1.20)  x autonomy(1.25) x ageMultiplier
Data        x civ.data                            x autonomy(1.25) x ageMultiplier
```

Every multiplier is named, visible in `Content.cs`, and applies without
exceptions. A player who loses should be able to point at the number.

## 4. Building recipes

| Building | Input /s | Output /s | Labour | Age |
|---|---|---|---|---|
| Power Plant | 0.7 Materials | 3.2 Energy | 2 | 0 |
| Extraction Site | 0.5 Energy | 1.6 Materials | 3 | 0 |
| Academy | 0.7 Energy | 0.30 Data | 3 | 0 |
| Fabricator | 1.1 Energy + 2.0 Materials | 1.0 Components | 4 | 1 |
| Exchange | 0.4 Energy | 0.6 Capital | 2 | 1 |
| Directorate | 0.6 Energy | 0.15 Data | 2 | 1 |
| Data Centre | 1.8 Energy + 0.25 Components | 0.85 Data | 3 | 2 |

Passive capacity: Capitol +8 housing, +10 security, 1 route slot · Habitat +8
housing · Directorate +25 security · Exchange +2 route slots.

Workers gather **1.4/second** (before multipliers) and carry 12 at a time.

## 5. The market

One global price per resource, quoted in Capital:

```
price(k) = basePrice(k) x clamp( 1 + pressure(k), 0.25, 4.0 )
```

`pressure` is the accumulated order flow:

- Buying `q` adds `q / depth(k)` to pressure.
- Selling `q` subtracts `q / depth(k)`.
- Pressure mean-reverts toward 0 at 12%/second.
- A small random walk keeps prices alive between trades.

| Resource | Base price | Depth |
|---|---|---|
| Materials | 1.0 | 1400 |
| Energy | 2.0 | 900 |
| Components | 5.0 | 500 |
| Data | 14.0 | 180 |

**Depth is the whole design.** A 500-unit order barely moves Materials and
violently moves Data. That is what makes cornering the Data market a viable
strategy and cornering Materials a waste of money.

### Spread

Each faction loses a cut on every trade:

```
spread = 0.06 x civ.marketSpreadMultiplier x marketDesk(0.75) + embargoPenalty
```

Meridian pays roughly 3.9%, Novaya roughly 6.9%. Over a match that difference is
the entire distance between those two civilisations' game plans. Embargoes add
3% each, so getting caught spying repeatedly makes you a worse trader.

Trading requires an operational **Exchange**.

## 6. Trade routes

A standing contract that sells one resource continuously at market price. Passive
income, and the thing espionage can quietly strangle — a cut route drops to 25%
throughput for 90 seconds and you may not notice for a minute.

Route slots come from the Capitol (1), each Exchange (2) and the Meridian civ
bonus (2).

## 7. Ages

| Age | Cost | Time | Output multiplier |
|---|---|---|---|
| 1 Industrial Consolidation | 900 C, 600 M | 60 s | 1.15 |
| 2 Information | 1800 C, 900 M, 400 P, 200 D | 75 s | 1.32 |
| 3 Automation | 3500 C, 1600 M, 900 P, 600 D | 90 s | 1.55 |

## 8. Known balance risks

Written down because they are untested, not because they are solved:

- **The Data market may be too thin.** Depth 180 means a 200-unit order moves the
  price by more than 100%. That could be exciting or it could be degenerate.
- **Debt interest may be too gentle.** 0.5%/s doubles a debt in about 140
  seconds; if players can outrun that with trade routes, borrowing becomes free.
- **Labour may be too forgiving.** `min(labour, supply)` means being at 80%
  labour costs exactly 20%. A superlinear penalty might make the decision sharper.
- **Age 3 may be unreachable** in a match of a sensible length. Nobody has played
  a full game yet.
