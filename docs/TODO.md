# To-do list

Track what is planned and what is done. Move items to **Done** (with the date) when they are finished; add new ideas under the matching section.

Legend: `[ ]` to do · `[~]` in progress · `[x]` done

## Next up

- [ ] **Goal celebration**: camera zoom on the scorer, confetti, net ripple, crowd swell, scorer in a celebrate pose
- [ ] **Animated players**: run while moving, kick on pass/shot, keeper dive on a save, celebrate after a goal (pose blending on the named joints)
- [ ] **Tutorial match**: 3–4 guided turns (select, move, pass, shoot) with the markers pointing at what to tap

## Game feel

- [ ] Light screen shake on tackles and shots
- [ ] Slow motion as a shot reaches the keeper
- [ ] Camera follows the ball smoothly while turns play out
- [ ] Haptics on phones (tackle, goal, error)
- [ ] Effect on the squares a selected player can move to (currently flat squares)

## Gameplay and depth

- [ ] Computer difficulty: Easy / Normal / Hard
- [ ] Short tournament or league (4–8 teams, trophy)
- [ ] Match stats at full time: shots, possession, tackles won, best player
- [ ] Unlockables: kits, balls, pitch styles (night, snow, retro)
- [ ] Defender may tackle a carrier already sharing its square (currently only on arrival)

## Clarity

- [ ] Replay both teams' planned arrows before a turn plays out
- [ ] Undo the last planned move
- [ ] Odds preview for passes and tackles (like the shot odds)
- [ ] Nicer hot-seat hand-over screen in team colours

## Social

- [ ] Online two-player matches (turn-based, with notifications; needs a backend)

## Art and audio

- [ ] Regenerate the tackle sprites as low sliding tackles (optional)
- [ ] Replace generated sound effects with recorded/AI versions where they sound weak (see `docs/AUDIO.md`)
- [ ] Check the save cutscene: the ball should end in the keeper's gloves
- [ ] Commit the GLB export script to the repo (e.g. `tools/`) so the 3D player can be edited later

## Release

- [ ] App icon and splash screen in the menu's comic style
- [ ] Android/iOS build settings: bundle ID, landscape lock, version numbers
- [ ] Store screenshots and a 15-second trailer
- [ ] Confirm commercial-use rights for all AI-generated art and music
- [ ] Register the game name as a trademark (optional)

## Done

### 2026-11-26
- [x] Master volume slider beside the speaker button; left end mutes; saved
- [x] Main menu layout: title band, dropdowns left, KICK OFF right (bottom-aligned)
- [x] Menu background art (`Resources/UI/bg_menu`), tinted 20%; pause screen is a 50% tint over the match
- [x] Dropdowns with a down-triangle marker; speaker icon on the main menu
- [x] `UiTheme` asset: fonts, button colours, title colours, menu background and tints in one place
- [x] Bangers display font everywhere (buttons, titles, HUD, shirt numbers)
- [x] Animated game buttons in colour styles; restyled dropdowns
- [x] Sound effects and music loops (synthesized, replaceable by files); user music tracks added and streamed
- [x] Automatic 50/50 tackle when a defender and the carrier end on the same square
- [x] Move plans as flowing dots with a yellow radar ring on the destination
- [x] Radar "ready" ring under selectable players; spinning team-coloured selection halo
- [x] Animated chevrons for pass and shot targets (single moving highlight); shots aim into the net
- [x] Tapping the drawn net aims at the goal square in front of it
- [x] Missed shots fly clearly wide; keeper cutscene shows the ball only on saves
- [x] Low-poly 3D player model (GLB via glTFast) with team kits; base plates removed
- [x] Match log (`match_log.txt` + console) for checking what happened
- [x] Warning before confirming a turn with nothing planned
- [x] Fix: camera pan error on the main menu

### 2026-11-25 and earlier
- [x] Cutscenes for random rolls (keeper dive, duel, long ball) with generated art
- [x] In-match MENU with pause / end match; menu centred on wide phones
- [x] Planning team's name pulses; taps near a player select the nearest teammate
- [x] Action toolbar shows only while the ball carrier is selected
- [x] Keeper limited near the ball; keeper dive reach 2 squares
- [x] Straight passes reach at most half the pitch; longer ones become long passes
- [x] Computer opponent, shot/long-pass targeting, ring-based shot accuracy
- [x] Ball carrier may move on the opponent's turn; possession team plans first; action before movement
- [x] Playable hot-seat match in Unity; rules engine with tests (phase 1–2)
