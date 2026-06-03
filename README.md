<p align="center">
  <img src="Captionman/icon.png" width="128" height="128" alt="Captionman icon">
</p>

# Captionman

Captionman is an accessibility closed captions mod for R.E.P.O. game, making the game more accessible to players who are deaf or hard of hearing.

## Mod Dependencies
Required:
- [BepInEx 5.x](https://github.com/BepInEx/BepInEx)

Optional:
- [MenuLib](https://github.com/IsThatTheRealNick/MenuLib) for REPOConfig
- [REPOConfig](https://github.com/IsThatTheRealNick/REPOConfig) for in-game configuration UI that let you adjust the caption settings

## Technical Details

<details><summary>Game Audio Captions</summary>

- Hooks into R.E.P.O.'s runtime `Sound.Play(...)` and `Sound.PlayLoop(...)` paths to detect game audio events (enemy, item, player, and event sounds)
- Uses `Captions.GameAudioCaptionFile` to load a direct caption CSV filename (for example `captionsEN.csv`)
- If configured file is missing, falls back to `captionsEN.csv`
- Automatically treats sounds as global when clip names contain `global` or when the runtime sound type is global
- Applies proximity filtering for non-global sounds to avoid captioning distant events
- Uses cooldowns to reduce repeated caption spam from loops or rapid-fire sound events
- Displays game audio captions in the caption UI

</details>

<details><summary>Caption API</summary>

- Provides `CaptionmanApi.SendCaption(text)` for generic caption lines
- Provides `CaptionmanApi.SendCaption(speaker, text)` for speaker-formatted lines
- API callers can push captions into the same persistent caption UI used by game-audio captions

</details>

## Configuration
The config file is located at `BepInEx/config/BatteryDie.Captionman.cfg`

| Category | ConfigEntry | Default Value | Description |
|----------|-------------|---------------|-------------|
| **Captions** | EnableCaptionsUI | `true` | Master toggle for caption rendering across menus, loading, lobby, and gameplay |
| **Captions** | GameAudioCaptions | `true` | Enable closed captions for game audio |
| **Captions** | GameAudioRepeatCooldownSeconds | `4.0` | Minimum cooldown before the same game-audio caption text can appear again |
| **Captions** | GameAudioCaptionFile | `captionsEN.csv` | Caption CSV filename to load; falls back to `captionsEN.csv` if missing |
| **Appearance** | BackgroundOpacity | `0.7` | Opacity of the background for captions (0.0 to 1.0) |
| **Appearance** | TextSize | `16.0` | Font size of captions (10.0 to 25.0) |
| **Appearance** | DisableTextColour | `false` | Disable custom text colour tags |
| **Appearance** | TextLeftAlign | `false` | Align caption text to the left instead of centered |
| **Appearance** | HorizontalPosition | `0.0` | Horizontal position offset of the caption panel (-270.0 to 260.0) |
| **Appearance** | VerticalPosition | `50.0` | Vertical position offset of the caption panel (0.0 to 350.0) |
| **Developer** | EnableDebug | `false` | Enable debug logging for troubleshooting | 

## Text Colour Markup

Caption lines support custom color tags:

- Named palette: `<c:orange>[Beep-beep-beeeeeeeeeeep] User Fatality.</c>`
- RGB values: `<c:225,225,225>[BatteryDie]</c>: Yes?`

RGB values accept `0-255` components (values are clamped if out of range).

Italic markup is also supported with TextMeshPro rich text tags:

- `<i>[EXPLOSION!]</i>`

Approved palette:

- `red`
- `yellow`
- `green`
- `blue`
- `cyan`
- `orange`
- `pink`
- `white`
- `gray`/`grey`

Note: Unknown or invalid color tags are ignored safely and text still displays.

## Help Wanted/Contribute

I'm looking for volunteers to help improve caption transcription quality and coverage. If you're interested, please open an issue or pull request and add the label "closed-captions-transcript" in the GitHub repository or get in touch.

## License

Captionman is licensed under the MIT License. See [LICENSE](LICENSE) file for details.
