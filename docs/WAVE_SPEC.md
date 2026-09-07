# Wave System — Gameplay Spec

> **Status: v0.2 — edge-tile model.** Supersedes v0.1, which put one tile *stack* per face
> and moved tiles along cell-to-cell face links. That model is gone.

---

## Glossary

- **Cell** — a unit on the board, drawn as a cube. Has N **sides**.
- **Side / face** — one face of a cell.
- **Join** — two cells pressed together. Both touching faces become **interior**: buried,
  invisible, unplayable, and forbidden from holding slots.
- **Exposed face** — any face that is not interior. The exposed faces of a cluster form one
  continuous surface, and that surface is the whole playing field.
- **Slot** — a tile position at the midpoint of one edge of an exposed face. A face has at
  most 4 (one per edge); it may have fewer, or none.
- **Pair** — every slot is paired with the slot on the other side of its shared edge. The
  partner may be another face of the same cube (around a corner) or the matching face of a
  neighbouring cube (across a seam). **A slot without a partner is invalid.**
- **Tile** — the thing drawn in a slot. Either **white** (exists, unclaimed, `PlayerId.None`)
  or claimed by a player.
- **Full face** — every slot on that face claimed by the *same* player. Capacity is per-face:
  a 2-slot face is full at 2, a 4-slot face at 4.
- **Expansion** — what a full face does.

---

## Constants

| Name | Value | Notes |
|---|---|---|
| `MaxChainSteps` | **8** | Defensive cap. Faces can re-fill each other, so chains can oscillate. |
| `ConquestTakesFace` | **true** | See R4. |

There is no `Capacity` constant any more — it is a property of each face.

---

## Rules

### R1. Selection (whose turn does what)

A player may claim a **white** slot on a face, provided **no other player holds a tile on
that face**. One enemy tile locks the entire face; the only way in is expansion.

A face where everyone's tiles are white is open to anybody.

### R2. Expansion trigger

A face expands when **every slot it has is claimed by one player**, and it has at least one
slot. Faces are processed in `(cellId, sideIndex)` order so a wave replays identically
everywhere — the property the deterministic multiplayer plan depends on.

### R3. Expansion effect

For each slot on the expanding face, the owner claims that slot's **partner** — the tile
across the shared edge. Then every slot on the source face is **released to white**.

The face trades its own tiles for a foothold on each neighbouring face. That is what makes
chains possible, and why a player's tile count can fall while their reach grows.

### R3b. An arriving tile always takes a free slot

The tile needs a home of its own, so a **white slot is always preferred**:

1. Its own slot, if that is white — it crosses its own edge, which is what the flight draws.
2. Otherwise the lowest-numbered white slot on that face.
3. Otherwise its own slot, converting whatever is there.
4. If the whole face is already the attacker's, the tile is spent for nothing.

This is separate from, and additional to, conquest (R4). Landing on an enemy tile instead of
a free slot would merely repaint something R4 is about to take anyway — the face would
change colour without the attacker's count growing at all. Landing on the attacker's own
tile is worse: it evaporates.

Worked through: a face holding 2 enemy tiles and 2 white slots, invaded by a full face,
ends as **3 attacker tiles and 1 white** — two conquered plus one arrival. Not 2 and 2.

Because the destination is **not necessarily the origin's partner**, `SlotStateChange.Origin`
is recorded rather than derived (see R5b).

### R4. Conquest takes the whole face

When a slot is claimed from another player, every *other* tile that player holds on the same
target face **also changes hands to the attacker**. White tiles on that face are untouched —
they stay open for anyone.

Landing on an enemy face therefore takes the face, not just the one tile. This is what makes
expansion into enemy territory decisive rather than a nibble.

It also preserves the invariant **at most one player holds tiles on any face**. Without that,
expansion can leave a face holding two players' tiles, which R1 then makes unclickable by
either of them — faces would silently accumulate into a frozen board.

Set `ConquestTakesFace = false` to flip only the targeted slot. If you do, contested faces
become reachable and R1 must be replaced with a majority rule or the game can stall.

### R5. Chain

After a step resolves, recheck for full faces. The chain stops when:

- no full faces remain (`Quiescent = true`), or
- a step produced no changes, or
- **at most one player still holds tiles** (`Decided = true`), or
- `MaxChainSteps` is reached.

The **Decided** condition matters more than it looks. Without it, a board one player has
saturated runs the full `MaxChainSteps` releasing and refilling its own faces — verified:
a wave that eliminated the last opponent on step 1 went on to simulate seven more steps,
and left three full faces standing afterwards. With it, that same wave stops at step 1.

A wave that stops because it was decided may legitimately leave `Quiescent = false`; the
game is over, so the leftover full faces will never be played.

### R5b. Every change records *why*

`SlotStateChange` carries a `SlotChangeCause`:

| Cause | Meaning | Drawn as |
|---|---|---|
| `Claimed` | A player clicked a white tile | no travel |
| `Expanded` | Arrived from an expanding face (possibly redirected, R3b) | **a flying tile, from `Origin`** |
| `Absorbed` | Taken because *another* slot on the same face was conquered (R4) | changes colour in place |
| `Released` | Spent by the expanding face, back to white (R3) | fades to white |

This is recorded, never inferred. An earlier animator worked out a tile's origin by looking
up the changed slot's partner — correct until R4 started taking whole faces, after which
`Absorbed` slots (which have partners of their own, elsewhere on the cluster) produced
flying tiles launched from unrelated parts of the board.

**Only `Expanded` involves travel, and its `Origin` field says from where.** Anything that
infers movement — from the cause alone, or by looking up the destination's partner — will
reproduce that bug, and R3b's redirection makes the partner wrong even for real arrivals.

**Presentation note:** a `Released` slot drains to white *over the flight duration*, not on
landing, so a face visibly empties as its tiles leave.

**Anything showing a score must read the drawn state, not the model.** `WaveSimulator`
finishes the entire chain before the first frame animates, so the model already holds the
final counts. A HUD reading `ComputeScore(board)` therefore jumps to the outcome while the
cubes are still mid-chain, and a player wiped out on step 1 of a 5-step chain is not shown
as out until the wave ends. `GameController.DisplayScoreFor` / `DisplayIsOut` follow the
frame on screen; `WaveAnimator` maintains the running tally as it applies each frame.
Authoritative elimination still happens in `VictoryCheckPhase` once the board has settled —
the drawn state is presentation only.

### R6. Score

`playerScore` = number of slots the player holds. White tiles count for nobody. The
simulator does not compute scores; it returns `SlotStateChange { slot, before, after }`
per step and consumers derive what they need.

### R7. Elimination and victory

Mostly not simulator concerns — except R5's Decided stop, which exists so a finished game
is not simulated further.

A player is eliminated once they **have held tiles and lost them all**; a player who has not
claimed anything yet is still in, which is what makes an all-white opening board playable.
`VictoryCheckPhase` declares the last one standing.

### R8. Stalemate

If a full round passes with **no player able to move**, the game ends and the player holding
the most tiles wins (a tie wins for nobody). This is reachable when a chain hits
`MaxChainSteps` and leaves full faces standing: a full face has no white slots, so a player
whose only faces are full has no legal move. Without this rule the turn loop skips every
player in turn until `GameController`'s transition cap trips and halts the game with an
error, which reads as a crash rather than a result.

---

## Worked Examples

Shorthand: `A[0]@2` = cell A, face 0, edge 2. `P1` = claimed by player 1, `_` = white.

### Example 1: Simple expansion

```
Setup:  A[0] has 4 slots, all P1. Their partners are all white.
After:  A[0]@0..3 = _     (source released)
        each partner      = P1
Net:    P1's tile count is unchanged (4 spent, 4 gained) but has spread to 4 faces.
```

### Example 2: Two-slot face

```
Setup:  A[0] has only 2 slots, both P1.
After:  Expands exactly as above. Capacity is the face's own slot count, not 4.
```

### Example 3: Conquest

```
Setup:  A[0] full of P1. One partner slot B[4]@1 = P2.
After:  B[4]@1 = P1        (taken)
        A[0]  = all white
```

### Example 4: Arrival plus conquest (R3b + R4)

```
Setup:  A[0] full of P1. Face B[4] holds P2 on @1 and @3, white on @0 and @2.
After:  B[4]@0 = P1        (the ARRIVING tile takes a free slot -- R3b)
        B[4]@1 = P1        (conquered -- R4)
        B[4]@3 = P1        (conquered -- R4)
        B[4]@2 = _         (still free)
```

P1 ends with **3** tiles on that face, not 2. Verified against the running simulator:
`red=2 blue=0 free=2` becomes `red=0 blue=3 free=1`.

### Example 5: No trigger

```
Setup:  A[0] has 4 slots: three P1, one white.
After:  Nothing. A face one tile short does not expand.
```

### Example 6: Mixed face never triggers

```
Setup:  A[0] has 4 slots: three P1, one P2.
After:  Nothing. The face is full of tiles but not full for anyone.
        (Reachable only with ConquestTakesFace = false.)
```

### Example 7: Chain

```
Setup:  A[0] full of P1. Claiming its partners completes face B[4].
Step 1: A[0] expands; B[4] becomes full of P1.
Step 2: B[4] expands onto its own partners; B[4] released.
```

### Example 8: Oscillation is capped

```
Setup:  Every slot on a single cube owned by P1 — every face is full.
Result: Faces release and re-fill each other every step. The simulator runs
        MaxChainSteps and returns Quiescent = false. Verified behaviour, not a bug:
        the cap exists precisely for this.
```

---

## Out of Scope

- Visual timing — `WaveAnimator` replays `WaveResult` after the fact and never writes to the model
- Network sync — command-based MP layer, Stage 13
- UI feedback — HUD subscribes to changes
- Elimination / victory — turn loop, not simulator
