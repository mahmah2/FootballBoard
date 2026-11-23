# Rules engine — interpretation notes

Spec sources: `specs/Football_Board_Game_Specification_MVP_v1.1.pdf` + `specs/Football_Board_Game_Spec_MVP_v1.3_Decisions.md`
(v1.3 wins on conflicts). Every number below is tunable in `Assets/Resources/Rules/rules.json`.
These are the defaults chosen where the spec was silent or ambiguous; change them here and in code together.

| Topic | Decision |
|---|---|
| Coordinates | Column 0 = Red goal line, Red attacks toward higher columns. Rows 0-based (13 rows → goal rows 4–8 = rows 5–9 in the spec). |
| Turn order | Turn team submits its plan first (locked, hidden); the other team then submits moves. Both resolve together. |
| Who acts | Only the turn team's carrier may pass/shoot/move with the ball. Both teams reposition every turn. |
| Simultaneous moves | If a square would exceed 2 players, every move into it is cancelled (repeats until stable). Swaps are allowed. |
| Locks | Carrier is locked unless option = Move. Pass receiver is locked. The other team's carrier is locked. |
| Lane check | Straight pass lane checked at declaration (`revalidateLaneAfterMovement` = false). Any player of either team blocks. |
| Long pass miss | Lands on one of 8 cells around the receiver. Opponent there → opponent; only teammates → teammate; empty → loose ball; off pitch → restart. |
| Loose ball | Claimed by whoever stands on it after the next movement; two arrivals → random clash. |
| Tackle | Requested on the move order; happens if the tackler ends on the opposing carrier's square. Resolved before the action; a won tackle cancels the declared action. |
| Shot blocking | Opposing outfield player on the adjacent cell toward the shot target **at declaration** blocks; blocker gets the ball and is moved back to that cell. |
| Shot accuracy | Based on columns from the goal line: 100% within the box depth, linear down to 50% at the centre line. |
| Goalkeeper distance | Chebyshev from the shot target cell to the keeper's cell **after** movement. |
| Out of play | Past a goal line → goal kick to that end's keeper (in goal centre). Past a sideline → throw-in for the team that did not touch last; its nearest outfield player is moved to the nearest in-bounds cell. |
| Kick-off | Coin toss team kicks off and plays the first turn. After a goal both teams reset to formation; conceding team kicks off and plays next. |
| Clock | Each turn = 2 steps × 30 s. Expiry ends the match after the current step (no new turn). |
| Ties | Golden goal only if both teams pick it (`goldenGoalRequiresBothTeams`). |
