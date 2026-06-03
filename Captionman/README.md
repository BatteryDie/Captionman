# Captionman

Captionman is an accessibility closed captions mod for R.E.P.O. game, making the game more accessible to players who are deaf or hard of hearing.

## What It Does

Never miss an important sound again! Captionman shows captions for:
- **Enemy sounds**
- **Item sounds**
- **Environmental sounds** 
- **Game event sounds**

## Customization

To adjust the caption settings, you can use [MenuLib](https://github.com/IsThatTheRealNick/MenuLib)+[REPOConfig](https://github.com/IsThatTheRealNick/REPOConfig) or edit the config file directly at `BepInEx/config/BatteryDie.Captionman.cfg` to customize:

| Setting | Default | What It Does |
|---------|---------|--------------|
| **EnableCaptionsUI** | On | Master toggle for caption rendering across menus, loading, lobby, and gameplay |
| **GameAudioCaptions** | On | Enable closed captions for game audio |
| **GameAudioRepeatCooldownSeconds** | 4.0 | Prevent the same game-audio caption from appearing too often |
| **GameAudioCaptionFile** | captionsEN.csv | Set caption CSV filename to load (falls back to captionsEN.csv if missing) |
| **BackgroundOpacity** | 0.7 | Make caption background darker or lighter (0.0 = invisible, 1.0 = solid) |
| **TextSize** | 16.0 | Make text bigger or smaller (10-25) |
| **DisableTextColour** | Off | Disable custom text colour tags |
| **TextLeftAlign** | Off | Align caption text to the left instead of centered |
| **HorizontalPosition** | 0.0 | Horizontal position offset of the caption panel (-270.0 to 260.0) |
| **VerticalPosition** | 50.0 | Vertical position offset of the caption panel (0.0 to 350.0) |
| **EnableDebug** | Off | Enable debug logging for troubleshooting |

Caption files are packaged flat beside the DLL and loaded by the `GameAudioCaptionFile` filename config.

## Feedback & Support

Found a bug or have a suggestion? Reach out on [GitHub](https://github.com/BatteryDie/Captionman/issues) or join the conversation in the [R.E.P.O. modding server](https://discord.gg/vPJtKhYAFe) ([Thread Link](https://discord.com/channels/1344557689979670578/1511107709913530459)).

## Help Wanted/Contribute

I'm looking for volunteers to help improve caption transcription quality and coverage. If you're interested, please open an issue or pull request and add the label "closed-captions-transcript" in the GitHub repository or get in touch.

## License

Captionman is licensed under the MIT License. See [LICENSE](https://github.com/BatteryDie/Captionman/blob/main/LICENSE) file for details.