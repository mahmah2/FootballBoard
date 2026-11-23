# Football Board Game — MVP Specification v1.3 (Decisions Addendum)

Date: 23 November 2026
Supersedes: v1.1 and v1.2 where they conflict. Sections not mentioned here are unchanged from v1.1.

---

## 1. Field and geometry

- **Minimum field:** 13 rows × 26 columns. Rows are always odd so the goal can be centred.
- **Growth:** Bigger fields add 2 cells to each dimension (for example 15 × 28, 17 × 30). Rows stay odd.
- **Goal:** 5 cells wide, centred on each short edge. On 13 rows, the goal covers rows 5–9 (counting from 1).
- **Penalty box:** 5 cells wide × 4 cells deep, in front of each goal. Scales with the field.
- **Goalkeeper:** Starts in the middle of goal. Moves only inside its penalty box, 1 or 2 cells per turn (a per-match setting chosen by the players).
- **No corners.** A ball leaving the pitch is handled by §4.
- **Distance metric:** Chebyshev (the larger of the row and column differences).

## 2. Occupancy

- A square holds at most 2 players, of any team combination.
- **Shared-square passing:** A carrier who shares a square with another player may pass only sideways or backwards. Forward passes are not allowed.
- "Forward" means toward the opponent's goal. "Sideways" means the two directions across the pitch (up and down on the board).

## 3. Turn structure

- Teams alternate: Red, then Blue. Both teams may reposition during every arrangement phase.
- Each team turn has two steps: an **arrangement** and an **action**.
- **Goal exception:** After a goal, the conceding team restarts from the centre and takes the next turn. Alternation resets from that team. Players are pushed back to their own sides.
- **Kick-off:** A coin toss decides which team starts with the ball.
- **Movement:** Simultaneous. Each eligible player may move one cell in any direction.

## 4. Ball leaving the pitch

- **Over the goal line, not into the goal:** Goal kick for the defending team. The ball is placed in the middle of the goal, and the defending goalkeeper owns it.
- **Over a sideline:** Throw-in for the team that did not touch the ball last. The ball is placed on the nearest in-bounds cell to where it left, and that team's nearest player on that cell owns it. A tie is decided by coin toss.
- **Misses:** A shot that is off target is a miss and is handled as a goal kick (above).

## 5. Ball carrier options (arrangement)

The ball carrier's team selects one option when the carrier is selected. The choice is declared immediately and validated at declaration.

| Option | Rule |
|---|---|
| **Move** | The carrier moves one cell. The carrier cannot pass or shoot in the same turn. |
| **Straight pass** | Passes to a chosen teammate. Success is 100% if the lane is clear. |
| **Long pass** | Passes over all players, including teammates and opponents. Success is 1/9. |
| **Shot** | Shot at the opponent's goal. Available from any position. |

- **Straight-pass lane:** A straight pass is blocked if any player, of either team, stands on a cell the lane passes through. A cell counts if the lane runs through its centre within 5% of a cell width. A blocked straight pass is not allowed. There is no fallback receiver.
- **Long pass:** Ignores all players. On success (1/9), the receiver takes the ball. On failure (8/9), the ball lands on one of the 8 adjacent cells, each with probability 1/9.
  - If the landing cell holds an opponent, that opponent owns the ball.
  - If the landing cell is empty, the first player to reach it owns the ball. Simultaneous arrival triggers a clash: one winner, picked by 50/50 randomization.
  - If the landing cell is off the pitch, §4 applies.
- **Contested destination:** A pass whose destination holds an opponent resolves at 50/50.

## 6. Shots

- **Target:** The shot targets the goal-line cell in the shooter's row. If that row is outside the 5-cell goal, the target is the nearest goal cell.
- **Accuracy (chance the shot is on target):** Smooth, not stepwise.
  - Inside the penalty box: 100%.
  - Beyond the penalty box: falls in a straight line from 100% at the box edge to 50% at the centre line. It stays at 50% beyond the centre line.
- **Miss:** A shot that is not on target is a miss, handled as a goal kick (§4).
- **On target:** Goalkeeper outcome depends on the distance from the target cell to the goalkeeper's cell:
  - 0 cells: 5% goal, 95% save.
  - 1 cell: 50% goal, 50% save.
  - 2 cells: 100% goal.
- **Opponent on the shooter's square:** The shot is contested again with a 50/50 randomization. If the shooter wins, the shot resolves with the on-target and goalkeeper rules above.
- **Blocking:** An opposing outfield player in the adjacent cell in the shooting direction blocks the shot. Blocking is checked on the board as it stands when the shot is declared.
- **After a shot resolves:** Any blocker is moved into the blocking position.
- **Save:** Possession passes to the defending team.
- **Goal:** One goal is added. The conceding team restarts (see §3).

## 7. Tackles

- When an opponent (not the carrier) moves into the carrier's square, that opponent may request a tackle.
- A tackle is a 50/50 randomization for possession between the two players.
- The tackle decides only the ball, not the movements.

## 8. Clock

- **Step length:** 30 seconds of match time per step. Each team turn is two steps (arrangement and action), so 60 seconds per team turn.
- **Clock is step-based, not wall-clock.** Only steps consume match time. Thinking time, handovers and animations do not. Pause stops the clock.
- **Presets (minutes):** 10 (default), 20, 30, 40, 50, 60, 70, 90.
- **Team turns:** A 10-minute match is 10 team turns in total, 5 each. A 90-minute match is 90 team turns, 45 each.
- **Expiry:** If time expires during a step, finish that step, apply its effects, then end the match. No new turn begins.

## 9. Ties

- A drawn match at the end of regulation asks both teams whether they want a **draw** or a **golden goal**.
- **Golden goal:** The next goal wins. The match ends on that goal.

## 10. Randomization and presentation

- Randomizations (50/50 contests, long-pass landings, goalkeeper outcomes, shared-arrival clashes, tackles) use an anime-style sequence: a single subject moves slowly over a static background.
- Animation is presentation only. The result is computed before playback. Animation time does not count against the match clock.

## 11. Open items

None. All decisions from the review are now in this addendum. The next step is to fold this addendum into a full v2.0 spec, or to start the rules-engine prototype (phase 1).
