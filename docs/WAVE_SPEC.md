# Match and wave rules

## Decisions and turn boundaries

A player claims an unowned slot on an exposed face where every occupied slot belongs to that player. A face’s capacity is its actual slot count, up to four. A gold diamond identifies a held face with a single empty slot where its owner could finish it on their turn.

| Action | Resolution |
| --- | --- |
| Relay | Release an owned slot. Claim its white partner and the lowest-index other legal white slot on the destination face. An owned source on ice can leave. |
| Freeze | Freeze one cube through the next living opponent’s action and wave. The opponent must have an ordinary claim both there and elsewhere. Ice blocks direct claims, Relay arrivals and Beacon placement or landing. Wave propagation continues. |
| Beacon | Mark two distinct legal white slots on one face. On the owner’s next turn, claim both together if both remain legal. Otherwise clear both. Elimination clears a pending Beacon. Resolve any opening wave before the owner acts. |
| Supply | In a Supply match, each seat earns one free refill after its fourth ordinary claim. On a later turn it may fill an empty card and still act. Abilities and waves do not earn supply. |

A confirmed ability spends its charge and the seat’s action. Preview and cancellation preserve both. Input waits for wave playback. Rematch restores opening charges.

## Expansion

1. Collect all nonempty faces whose slots belong entirely to one player. Process them in ordinal cell ID and side-index order and recheck each face before expanding.
2. For each source slot, send an arrival to its partner’s face. Prefer the partner if white, then the lowest-index white slot on that face, then the partner itself. If the target already belongs to the attacker, that arrival is spent.
3. Change the landing slot to the attacker. With the shipping `ConquestTakesFace` rule, also convert any opposing occupied siblings. Other white siblings stay white. Release each occupied source slot to white after its outgoing arrivals.
4. Repeat while changes occur and full faces remain, up to eight steps. Stop early when at most one player holds tiles. The cap or an early victory can leave full faces on the board.

For example, an arrival on a face with two enemy tiles and two whites takes a white and absorbs the two enemy tiles. That leaves three attacker tiles and one white, ready for a later expansion.

## Playback and result

The simulator emits `Claimed`, `Expanded`, `Absorbed` and `Released` changes with before/after owners. `Expanded` includes the actual source slot so a redirected landing draws correctly. Released slots drain, absorbed siblings change in place, and a pale frame marks the landing slot. Reduced motion keeps the landing and score updates at the same step boundaries.

Score is occupied slot count. After a settled wave, a seat that has held tiles and now has none is eliminated. A seat that started empty can still take its first turn. The last surviving seat wins. If all seats consecutively lack a legal move, the highest score wins and a tie draws. [Runtime architecture](ARCHITECTURE.md)
