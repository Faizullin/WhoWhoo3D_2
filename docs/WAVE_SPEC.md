# Wave System — Gameplay Spec

> **Status: v0.1 — Dice Wars-style defaults, ready to tune.**
>
> These are starter rules so the simulator can be built and tested. Once you can *feel* a wave fire on a real board, swap values in `WaveConfig` and re-run the scenario tests; failing tests will pinpoint exactly what behavior changed.

---

## Glossary

- **Cell** — a unit on the board (rendered as a cube, but topology-only). Has N **sides**.
- **Side** — one face of a cell. Holds at most one **tile stack**.
- **Tile stack** — `(owner: PlayerId, count: int)`. An "empty" side has count 0 and owner None.
- **Link** — a side may be linked to one other side on a different cell. Links are bidirectional.
- **Wave** — periodic event where any "full" side emits its stack to the linked neighbor.
- **Capacity** — both the *trigger* (side emits when count ≥ Capacity) and the *cap* (a side never holds more).

---

## Constants (v0.1)

| Name | Value | Notes |
|---|---|---|
| `Capacity` | **4** | Trigger and hard cap. |
| `Keep` | **1** | Tiles left behind on the source side after emission. Hardcoded. |
| `MaxChainSteps` | **5** | Cap on chain wave iterations per round. |

Derived: `Emit = Stack.Count - Keep` (always ≥ Capacity − 1 = 3 for an emitting side).

---

## High-Level Wave Loop

```
Simulate(board, cfg):
  for step in 0..MaxChainSteps:
    emissions = sides where stack.Count >= Capacity AND link != null,
                ordered deterministically by (cellId, sideIndex)
    if emissions empty: break
    for each emission in order: resolve(emission, cfg)
```

A single side may not be eligible by the time its turn comes if an earlier emission this step changed it. Skip silently in that case.

---

## Rules

### R1. Emission

A side emits when `stack.Count ≥ Capacity` AND it has a link.

- **Emit count** = `stack.Count - Keep` (= count − 1).
- After emission, the source side becomes `(originalOwner, Keep)` = `(owner, 1)`.

A side without a link never emits, even if full.

### R2. Empty Target

```
target.Stack = (attackerOwner, emitCount)
source.Stack = (attackerOwner, 1)
```

### R3. Same-Owner Target

Stacks add, then **clamp** at `Capacity`. Overflow is discarded.

```
combined    = min(target.Count + emitCount, Capacity)
target.Stack = (attackerOwner, combined)
source.Stack = (attackerOwner, 1)
```

### R4. Enemy Target — Subtract Rule

```
if emit > defender:
    target.Stack = (attackerOwner, emit - defender)   // attacker captures
elif emit < defender:
    target.Stack = (defenderOwner, defender - emit)   // defender holds
else:  // tie
    target.Stack = empty                              // mutual annihilation
source.Stack = (attackerOwner, 1)                     // always
```

### R5. Score Deltas

The simulator does **not** compute scores. It returns `SideStateChange { side, before, after }` events per step. Consumers (player roster, HUD) compute deltas as needed.

Reference formula: `playerScore = sum of stack.Count for all sides where stack.Owner == player`.

### R6. Chain Wave

After all emissions in a step resolve, recheck for full sides. If any exist, run another step. Stop when:

- No full sides remain, OR
- `MaxChainSteps` reached (defensive cap; in practice unreachable).

### R7. Player Elimination

Not a simulator concern. A player is alive while their `playerScore > 0`. The turn loop checks this between phases.

### R8. Victory

Not a simulator concern. `VictoryCheckPhase` declares the last alive player the winner.

---

## Worked Examples (Unit Test Cases)

Topology shorthand: cells `A`, `B`. Side index in brackets, e.g. `A[2]`. Stack notation: `P1×3` means PlayerId 1, count 3. Empty = `_`.

### Example 1: Empty target

```
Setup:
  A[0] = P1×4 (full), linked to B[0]
  B[0] = _

After 1 step:
  A[0] = P1×1
  B[0] = P1×3
```

### Example 2: Same-owner stack, no overflow

```
Setup:
  A[0] = P1×4, link → B[0]
  B[0] = P1×1

After:
  A[0] = P1×1
  B[0] = P1×4   (1 + 3 = 4, fits)
```

### Example 3: Same-owner stack, overflow clamped

```
Setup:
  A[0] = P1×4, link → B[0]
  B[0] = P1×3

After:
  A[0] = P1×1
  B[0] = P1×4   (3 + 3 = 6, clamped to 4; 2 tiles discarded)
```

### Example 4: Enemy collision, attacker wins

```
Setup:
  A[0] = P1×4, link → B[0]
  B[0] = P2×1

After:
  A[0] = P1×1
  B[0] = P1×2   (emit 3 vs defender 1 → P1 captures with 3-1=2)
```

### Example 5: Enemy collision, defender wins

```
Setup:
  A[0] = P1×4, link → B[0]
  B[0] = P2×4   (defender also full but has no link emit this step because we process A first)

After A's emission:
  A[0] = P1×1
  B[0] = P2×1   (emit 3 vs defender 4 → P2 holds with 4-3=1)
```

### Example 6: Enemy collision, tie

```
Setup:
  A[0] = P1×4, link → B[0]
  B[0] = P2×3

After:
  A[0] = P1×1
  B[0] = _      (emit 3 == defender 3 → mutual annihilation)
```

### Example 7: No link, no emission

```
Setup:
  A[0] = P1×4, link = none
  (no other sides full)

After:
  No emissions, simulator returns 0 steps.
```

### Example 8: Chain wave (two-step)

```
Setup:
  A[0] = P1×4, link → B[0]
  B[0] = _
  B[1] = P1×3, link → C[0]
  C[0] = _

Step 1:
  A[0] emits to B[0]: A[0] = P1×1, B[0] = P1×3
  B[1] is not full (3), no emission.

Step 2:
  No full sides. Stop.

Result: 1 step recorded.
```

To produce a real two-step chain we'd need an emission to push a side over Capacity. Since Capacity is a hard cap, chain waves require a different trigger — see Example 9.

### Example 9: Chain via capture

```
Setup:
  A[0] = P1×4, link → B[0]
  B[0] = P2×1
  B[1] = P2×3, link → C[0]   (not full)
  C[0] = P2×4, link → D[0]   (full but isolated)
  D[0] = _

Step 1:
  A[0]: P1×4 → emit 3 vs B[0] P2×1 → B[0] becomes P1×2, A[0] becomes P1×1
  C[0]: P2×4 → emit 3 vs D[0] empty → D[0] becomes P2×3, C[0] becomes P2×1

Step 2:
  No sides ≥ Capacity. Stop.

Result: 1 step with 4 changes.
```

### Example 10: Two emissions to the same target

```
Setup:
  A[0] = P1×4, link → C[0]
  B[0] = P1×4, link → C[1]
  C[0] = _, C[1] = _

Step 1 (A processed before B by deterministic order):
  A[0] emits to C[0]: A[0] = P1×1, C[0] = P1×3
  B[0] emits to C[1]: B[0] = P1×1, C[1] = P1×3

(They land on different sides of the same cell — no contention.)
```

For "two emissions to the *same side*", the second emission sees the result of the first; the rules in R2/R3/R4 apply with the updated state.

---

## Out of Scope

- Visual / VFX timing (handled by `WaveAnimator`)
- Network sync (handled by command-based MP layer in Phase 12)
- UI feedback (HUD subscribes to changes via the EventBus)
- Player elimination / victory detection (turn loop, not simulator)
