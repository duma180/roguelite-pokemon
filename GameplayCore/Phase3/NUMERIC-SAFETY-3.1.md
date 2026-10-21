# Roguelite numeric contract for future battle integration

Phase 3.1 does not implement the difficulty modes, enhancement purchases, Gym ceilings, rewards, or scaling. These approved policy values must remain compatible with the battle core.

## Difficulty and zone values

Use exact rational/integer inputs:

| Mode | Additive base rate per zone tier | Enemy-only multiplier |
|---|---:|---:|
| Normal | 1,500 basis points (+15 percentage points) | 10,000 bp (×1.00) |
| Hard | 2,500 bp (+25 percentage points) | 11,000 bp (×1.10) |
| Nightmare | 4,500 bp (+45 percentage points) | 12,000 bp (×1.20) |

Enemy scaling is `(1 + zoneTier × baseRate) × enemyMultiplier`. The player enhancement ceiling uses only `1 + zoneTier × baseRate`; it must never include the enemy-only multiplier. The zone modifier is additive in tier count, not compounded once per tier.

## Individual enhancement tracks

Each individual Pokémon has six independent, uncapped, bankable stat-rank tracks. Each purchased rank adds five percentage points to that stat's permanent base-stat enhancement. Purchases remain individual to each Pokémon and stat. Ranks above the currently Gym-unlocked effective ceiling stay banked and become effective as that ceiling grows. After the first-clear record for all 16 HGSS Gym Leaders is set, there is no rank or effective-power cap.

For one-based purchase rank `r`, the PP price is `min(100, 10 + 5 × floor((r - 1) / 3))`: 10 PP for ranks 1–3, +5 PP each three ranks, capped at 100 PP per purchase. Currency and cumulative costs require `BigInteger`. Gym completion is a stable 16-bit first-clear mask plus a permanent all-leaders-cleared state; content release additions must not invalidate it.

## Exact arithmetic boundary

Represent percentages as exact integer numerators/denominators or basis points; never use floating-point to store purchases or apply these rules. Use `BigInteger` for unbounded ranks, balances, and intermediate effective values. Apply explicit HGSS floor points at reviewed formula boundaries. Do not cast a scaled stat back to `int` without an approved overflow policy: that would silently reinstate an effective cap.

The battle engine in Phase 3.1 still uses the validated bounded Phase 1 `int` Gen IV formulas and does not accept enhancement/zone modifiers. Before integration, add a BigInteger calculation path and differential tests proving zero-modifier parity against frozen Phase 1 golden fixtures. The enemy multiplier and player ceiling must be distinct API inputs so the enemy-only multiplier cannot leak into player enhancements.
