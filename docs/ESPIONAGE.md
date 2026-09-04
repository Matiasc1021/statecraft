# Espionage

Implemented in `Assets/Scripts/Espionage/EspionageSystem.cs` and `IntelSystem.cs`.

---

## 1. The loop

1. Build a **Directorate** (Age 1).
2. Train **Field Agents**. Each has a `skill` and a `cover` value.
3. Pick a target faction and an operation. The HUD shows you the odds *before*
   you commit.
4. The operation runs for its duration, then resolves.
5. Cover is spent whether it worked or not. Cover regenerates slowly while the
   agent is idle at home.

**Money buys the attempt. Cover is the resource you are actually spending.**
That is what stops espionage from collapsing into "spend all Capital on ops":
you have a small stable of agents whose usefulness degrades every time you use
them, and rebuilding it takes time you could have spent on the economy.

## 2. Resolution — two independent rolls

```
success  = clamp( base
                + (skill - 50) x 0.006
                + (cover / 100) x 0.15
                - targetSecurity x 0.004,   0.05, 0.95 )

detected = clamp( base x attackerDetectionMult
                + targetSecurity x 0.003
                - (cover / 100) x 0.25,     0.00, 0.95 )
```

They are independent **on purpose**, which gives four outcomes instead of two:

| | Undetected | Detected |
|---|---|---|
| **Success** | The good one. | It worked and they know it was you. Expect an embargo. |
| **Failure** | You wasted the money. Nobody knows. | Blown. 45% chance the agent is burned outright. |

A defender never buys immunity — they buy a **better distribution**. That is why
security is worth building even though it blocks nothing outright, and it is why
the game stays tense rather than degenerating into "whoever has more security
wins".

## 3. Operations

| Operation | Age | Cost | Success | Detect | Cover | Effect |
|---|---|---|---|---|---|---|
| Exfiltrate Data | 1 | 150 C, 15 D | 62% | 28% | 18 | Take 30% of their Data (max 250) |
| Insider Position | 1 | 130 C, 30 D | 66% | 20% | 15 | +10% of their Capital to you, −5% from them |
| Plant Disinformation | 1 | 190 C, 40 D | 70% | 24% | 12 | Their intel on you is badly wrong for 2 min |
| Cut Trade Route | 1 | 210 C, 10 D | 60% | 36% | 20 | One of their routes → 25% for 90 s |
| Sabotage Plant | 1 | 260 C, 30 P | 56% | 42% | 26 | One production building offline 60 s |
| Counter-Intel Sweep | 1 | 320 C, 20 D | 78% | — | 0 | *Defensive.* +25 security for 3 min; may roll up hostile agents mid-operation |
| Move the Market | 2 | 520 C, 40 D | 50% | 30% | 22 | Their largest holding drops 25% |
| Industrial Espionage | 2 | 450 C, 40 P, 60 D | 42% | 46% | 32 | Copy one technology they have and you do not |

Base values before skill, cover and security are applied. Sable Union pays 30%
less for all of it; Meridian pays 15% more.

**Every successful operation also refreshes your intelligence on the target.**
That is often the real reason to run a cheap one.

## 4. The intelligence model

Rival numbers are never shown raw. `IntelSystem` keeps, per (observer, target),
a snapshot captured at the moment of your last successful operation.

```
confidence = exp( -elapsed / (75s / targetIntelDecayMultiplier) )
reported   = truth x (1 + noise x 0.85 x (1 - confidence))
```

- Confidence falls to ~37% after 75 seconds, faster against a faction that has
  researched **Encryption Standard** (which halves the half-life).
- Noise is deterministic per five-second bucket, so a stale estimate drifts
  slowly rather than flickering every frame. Readable, and still untrustworthy.
- The HUD shows a confidence band — CONFIRMED / RELIABLE / STALE / UNRELIABLE —
  so the player always knows how much to trust what they are reading.

### Disinformation

`Plant Disinformation` adds a multiplicative bias (roughly 0.3–0.55x or 1.7–2.6x)
on top of the noise for two minutes. The victim's HUD still shows a high
confidence label if their intel is recent.

**A confident-looking estimate can be confidently wrong, and there is no tell.**
That is the intended experience: it is the one place the game lies to you, and it
lies because another player paid to make it lie.

## 5. Diplomacy

Only *detected* operations have diplomatic cost. Tension accumulates (2 points
for a detected success, 1 for a detected failure) and decays at 0.06/second.

| Tension | Stance | Effect |
|---|---|---|
| 0–3 | Correct | — |
| 3–7 | Strained | AI starts prioritising you |
| 7–12 | Hostile | AI targets you preferentially |
| 12+ | **Embargo** | +3% market spread for you, permanently while it lasts |

Cheating is profitable. Cheating **visibly** is expensive.

## 6. Known risks

- **Swinginess.** 40–65% success rates mean long unlucky streaks are common and
  feel terrible. May need a pity mechanic or cheaper, more frequent operations.
- **Counter-Intel Sweep may be strictly better than attacking** while the AI is
  passive, since 78% success and no detection risk is a very clean deal.
- **Industrial Espionage is probably underpowered** at 42% success and 46%
  detection for one technology.
- **There is no way to lose an agent to bad luck alone** other than a blown
  operation, so a cautious player accumulates agents with no downside.
