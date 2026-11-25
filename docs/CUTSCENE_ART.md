# Cutscene art brief

Short illustrated scenes play when a random roll decides something:

| Scene | Plays for | What moves |
|---|---|---|
| **Keeper dive** | every shot that reaches the goal | the defending keeper flies across the frame; the ball drifts the same way, spinning |
| **Duel** | tackles, 50/50 loose balls, contested passes, shots under pressure | the attacker runs in from the left, the defender lunges in from the right; the ball squirts out toward the camera |
| **Long ball** | long passes | the ball sails overhead into the distance; a runner chases it from below |

Each scene lasts about 2 seconds with a caption bar at the bottom (for example *SHOT! on target 55% · beats the keeper 60%*). Tap to skip. Until real images exist the game draws simple placeholder art, so you can add images one at a time.

## Where the files go

Put the PNGs in **`Assets/Resources/Cutscenes/`** with exactly these names. Unity picks them up on its own; no code changes are needed.

| File | Size (any size with this shape works) | Background |
|---|---|---|
| `bg_goal.png` | 1920 × 1080 (16:9) | full picture, no transparency |
| `bg_pitch.png` | 1920 × 1080 (16:9) | full picture, no transparency |
| `keeper_dive_red.png` | 1536 × 1024 (3:2, landscape) | flat magenta |
| `keeper_dive_blue.png` | 1536 × 1024 | flat magenta |
| `player_run_red.png` | 1024 × 1536 (2:3, portrait) | flat magenta |
| `player_run_blue.png` | 1024 × 1536 | flat magenta |
| `player_tackle_red.png` | 1024 × 1536 | flat magenta |
| `player_tackle_blue.png` | 1024 × 1536 | flat magenta |
| `ball.png` | 1024 × 1024 (square) | flat magenta |

## Transparency: use a magenta background

Most image generators cannot produce real transparent PNGs. Ask for a **solid flat magenta background (#FF00FF)** instead. On import the game removes it automatically, with soft edges and no pink fringe. Magenta is used because none of our kits, skin or grass colours are close to it (green would clash with the teal keeper kit and the grass).

- A PNG that already has real transparency is also fine; it is left as is.
- Background images (names starting with `bg_`) are never keyed.
- Check the Unity console after importing: it logs `keyed out background colour #FF00FF` for each sprite. If it warns instead, the background was not flat enough. Regenerate the image, or fill the background with pure #FF00FF in any paint program.

## Camera: the same for every image

All nine images must look as if shot by **one camera standing on the pitch, low, about knee height (50 cm above the grass), a few metres in front of the action, looking slightly upward**. Characters are then pasted on top of a background, so:

- **Backgrounds** show the place only: no people on the pitch. The camera is *on the pitch*, never inside the goal or behind the net. Keep the **centre of the frame calm** (sky, darker stands, simple shapes) because the characters are drawn there.
- **Characters** are seen **from the side, from slightly below**, at the same low angle, so they sit naturally in front of the backgrounds.
- **Lighting:** the keeper scene is at **night under floodlights** (cool white light, blue rim light). The pitch scene is **daytime** (warm sun from the upper left). The red and blue versions of a character use the same lighting.

## Rules for every character image

- **Original characters only.** Do not name or imitate an existing anime, manga, game or real player, and use no national flags, sponsor logos, club badges, shirt numbers or text.
- **All characters face RIGHT.** The game mirrors them when needed.
- **The whole figure is in frame, with a small margin.** No ground, no grass, no shadow on the floor, no motion-blur streaks touching the edge: only the figure on flat magenta.
- **No magenta or pink** anywhere on the character.
- **Same style for every image.** Generate them in one session, reuse the style line below word for word, and keep the same two characters for red and blue (only the kit colour changes).

**Style line (put this at the start of every prompt):**
> Dynamic anime-inspired sports illustration, original character, clean bold ink outlines, cel shading with two-tone shadows, vivid saturated colours, dramatic action pose, slight motion energy, high detail, consistent character design,

**Kits** (match the board pieces):

| Team | Outfield shirt | Shorts | Socks | Keeper shirt | Keeper gloves |
|---|---|---|---|---|---|
| Red | red #D94A3D with cream #F6E3D3 trim | cream #F6E3D3 | red | orange #F08A24 | yellow #FFD23F |
| Blue | blue #2F6BD6 with pale blue #E3ECF8 trim | pale blue #E3ECF8 | blue | teal #1FB5A8 | yellow #FFD23F |

## Prompts

### `bg_goal.png`
> Dynamic anime-inspired sports illustration background, the camera stands ON THE PITCH about four metres in front of a football goal, very low near the grass at knee height, looking straight at the goal and slightly upward. The white goal frame (two posts and the crossbar) is clearly visible and fills about two thirds of the frame width, with the goal net hanging BEHIND the goal frame, seen from the front and slightly out of focus. Behind and above the goal: packed stands with a blurred cheering crowd, bright floodlights with starburst glow, a dark blue night sky. Green grass in the lower quarter, the goal line and the six-yard box line visible. The centre of the image is calm and uncluttered (no strong details inside the goal mouth). Slight radial motion blur at the edges, dramatic and cinematic, no people on the pitch, no goalkeeper, no ball, no text, no logos, 16:9 landscape.
>
> Not: a view from inside the goal, a view through the net, or the net in front of the camera.

### `bg_pitch.png`
> Dynamic anime-inspired sports illustration background, the camera stands ON THE PITCH in midfield, very low near the grass at knee height, looking sideways across the pitch toward the main stand and slightly upward. Green grass with mowing stripes fills the lower third and is sharp close to the camera, then blurs. Behind: a large stadium stand with a blurred cheering crowd, and above it a bright blue daytime sky with big white clouds, warm sunlight from the upper left. No goal in view. The centre of the image is calm and open, because two players will be drawn there. Subtle diagonal speed lines, cinematic depth of field, no people on the pitch, no ball, no scoreboard, no text, no logos, 16:9 landscape.

### `keeper_dive_red.png` (repeat with the blue kit for `keeper_dive_blue.png`)
> [style line] a goalkeeper in full horizontal mid-air dive toward the right, body fully stretched, both arms reaching forward to the right with yellow goalkeeper gloves, determined shouting expression, short dark hair, orange long-sleeve goalkeeper jersey, black shorts, orange socks, football boots, seen from the side and slightly below by a low camera on the pitch, lit by cool white stadium floodlights at night with a blue rim light, full body in frame, isolated on a solid flat magenta #FF00FF background, no ball, no goal, no net, no ground, no shadow, no text, landscape 3:2.

*Blue version:* replace "orange long-sleeve goalkeeper jersey … orange socks" with "teal long-sleeve goalkeeper jersey … teal socks".

### `player_run_red.png` (repeat with the blue kit for `player_run_blue.png`)
> [style line] a football player sprinting hard toward the right, leaning forward, arms pumping, one leg high, fierce focused expression, messy brown hair, red short-sleeve football shirt with cream trim, cream shorts, red socks, football boots, seen from the side and slightly below by a low camera on the pitch, bright daytime sunlight from the upper left, full body in frame, isolated on a solid flat magenta #FF00FF background, no ball, no ground, no shadow, no text, portrait 2:3.

*Blue version:* "blue short-sleeve football shirt with pale blue trim, pale blue shorts, blue socks", and give this character black spiky hair so the two teams look like different people.

### `player_tackle_red.png` (repeat with the blue kit for `player_tackle_blue.png`)
> [style line] a football player lunging into a sliding tackle toward the right, front leg stretched far forward low to the ground, body leaning back, one arm out for balance, gritted teeth, short black hair, red short-sleeve football shirt with cream trim, cream shorts, red socks, football boots, seen from the side and slightly below by a low camera on the pitch, bright daytime sunlight from the upper left, full body in frame, isolated on a solid flat magenta #FF00FF background, no ball, no ground, no grass spray, no shadow, no text, portrait 2:3.

*Blue version:* swap in the blue kit as above, and use a different hairstyle from the red tackler.

### `ball.png`
> [style line] a classic black and white football (soccer ball) with pentagon panels, glossy highlight, bold ink outline, slightly squashed as if just kicked, lit from the upper left, centred, isolated on a solid flat magenta #FF00FF background, no motion trail, no shadow, square 1:1.

## Tips

- If a generator keeps adding a floor or shadow, add "floating, isolated sticker style".
- If a background comes out with the camera in the wrong place (inside the goal, high up in the stands), repeat the camera sentence at the end of the prompt as well; generators weight the start and end most.
- Judge a background by imagining a figure pasted in the middle: if the centre is too busy to read a person against, ask for "calmer, less detailed centre".
- Keep the same seed or reference image between the red and blue versions so the faces match.
- Before you ship, make sure you have the rights to use the generated images commercially under that tool's terms.
- To turn cutscenes off, untick **Play Cutscenes** on the `MatchController` object in the Match scene.
