# Sound and music

Every sound is generated in code at start-up (`Assets/Scripts/Presentation/SoundSynth.cs`), so the game works with no audio files. To replace a sound, drop an audio file (WAV, OGG or MP3) named exactly as below into **`Assets/Resources/Audio/`**. The game then uses your file instead and stops generating that sound. No code change is needed.

Sound on/off: the **SOUND** button on the main menu and the **speaker icon** next to MENU in a match. The choice is remembered between sessions.

## Music (seamless loops)

| File | Plays | Generated version | Prompt idea for an AI music tool |
|---|---|---|---|
| `music_theme` | Main menu and full-time screen | 118 BPM, C major, C–G–Am–F; drums, bass, pad, arpeggio, catchy lead | Upbeat, catchy instrumental theme for a casual football board game, 118 BPM, bright synths and light drums, stadium energy, loops seamlessly, no vocals |
| `music_match` | During a match (quieter) | Same harmony, lighter drums, no lead | Calm, focused background loop for a turn-based sports strategy game, 118 BPM, soft drums and arpeggios, unobtrusive, loops seamlessly, no vocals |

Keep music loops 15–60 seconds long and cut exactly on the bar so they loop without a click.

## Sound effects

| File | When it plays | Generated version |
|---|---|---|
| `sfx_select` | You select a player | Two quick rising blips |
| `sfx_target` | Shot aimed, pass receiver or long-pass square picked | Rising "lock-on" sweep with a sparkle |
| `sfx_click` | Move placed, option chosen, menu toggles | Short tick |
| `sfx_confirm` | Plan confirmed, match starts | Bright major chord blip |
| `sfx_error` | Invalid action, empty-plan warning | Two low buzzes |
| `sfx_step` | Players move | Soft studs on grass |
| `sfx_pass` | Short pass | Clean kick |
| `sfx_longball` | Long pass | Kick and a long whoosh |
| `sfx_shot` | Any shot is struck | Hard strike and a fast whoosh |
| `sfx_goal` | Goal | Brass fanfare over a roaring crowd |
| `sfx_save` | Keeper saves | Glove thud and the crowd's "ooh" |
| `sfx_miss` | Shot goes wide | Sad "wah-wah" trombone and a groan |
| `sfx_tackle` | Tackle, blocked shot | Body impact |
| `sfx_tacklewon` | Tackle won | Rising major stab |
| `sfx_tacklelost` | Tackle failed | Falling minor sigh |
| `sfx_clash` | 50/50 loose ball, shot under pressure | Whoosh into an impact |
| `sfx_whistle` | Goal kick, throw-in | Short referee whistle |
| `sfx_whistlelong` | Kick-off, golden goal | Long whistle |
| `sfx_finalwhistle` | Full time | Three whistles, the last one long |
| `sfx_swoosh` | A cutscene opens | Air swoosh |
| `sfx_coin` | Coin toss | Metallic ping |

Tips for generated replacements:
- Keep effects short (under 1 s), except goal, save, miss and the final whistle, which are 1.5–3.5 s. Trim silence at the start so they feel instant.
- Mono, 44.1 kHz is plenty. Normalise to about -3 dB.
- Music gets quieter on its own while the goal, save, miss and final-whistle sounds play.
- Check the tool's licence allows commercial use.
