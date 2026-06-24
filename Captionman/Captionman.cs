using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Captionman;

[BepInPlugin("BatteryDie.Captionman", "Captionman", "1.0.0")]
public class Captionman : BaseUnityPlugin
{
    internal static Captionman Instance { get; private set; } = null!;
    internal new static ManualLogSource Logger => Instance._logger;
    private ManualLogSource _logger => base.Logger;
    internal Harmony? Harmony { get; set; }

    // Config - Captions
    internal ConfigEntry<bool> EnableCaptionsUI { get; private set; } = null!;
    internal ConfigEntry<bool> GameAudioCaptions { get; private set; } = null!;
    internal ConfigEntry<float> GameAudioRepeatCooldownSeconds { get; private set; } = null!;
    internal ConfigEntry<string> GameAudioCaptionFile { get; private set; } = null!;

    // Config - Appearance
    internal ConfigEntry<float> BackgroundOpacity { get; private set; } = null!;
    internal ConfigEntry<float> TextSize { get; private set; } = null!;
    internal ConfigEntry<bool> DisableTextColour { get; private set; } = null!;
    internal ConfigEntry<bool> TextLeftAlign { get; private set; } = null!;
    internal ConfigEntry<float> HorizontalPosition { get; private set; } = null!;
    internal ConfigEntry<float> VerticalPosition { get; private set; } = null!;

    // Config - Developer
    internal ConfigEntry<bool> EnableDebug { get; private set; } = null!;
    internal ConfigEntry<bool> StopConsoleSpam { get; private set; } = null!;
    private readonly Dictionary<string, float> _debugCooldowns = new Dictionary<string, float>();
    private const float DebugSpamCooldownSeconds = 5f;
    private GameAudioCaptionService? _gameAudioService;
    internal GameAudioCaptionService? GameAudioService => _gameAudioService;

    private void Awake()
    {
        Instance = this;
        
        // Prevent the plugin from being deleted
        this.gameObject.transform.parent = null;
        this.gameObject.hideFlags = HideFlags.HideAndDontSave;
        DontDestroyOnLoad(gameObject);

        // Bind Config
        EnableCaptionsUI = Config.Bind(
            "Captions",
            "EnableCaptionsUI",
            true,
            "Master toggle for caption rendering across the entire game"
        );

        GameAudioCaptions = Config.Bind(
            "Captions",
            "GameAudioCaptions",
            true,
            "Enable closed captions for game audio"
        );

        GameAudioRepeatCooldownSeconds = Config.Bind(
            "Captions",
            "GameAudioRepeatCooldownSeconds",
            4f,
            new ConfigDescription(
                "Minimum cooldown in seconds before the same game-audio caption text can appear again",
                new AcceptableValueRange<float>(0f, 10f)
            )
        );

        GameAudioCaptionFile = Config.Bind(
            "Captions",
            "GameAudioCaptionFile",
            "captionsEN.csv",
            "Caption CSV filename to load. If not found, captionsEN.csv is used as fallback."
        );

        BackgroundOpacity = Config.Bind(
            "Appearance",
            "BackgroundOpacity",
            0.7f,
            new ConfigDescription(
                "Opacity of caption background from 0.0 (transparent) to 1.0 (opaque)",
                new AcceptableValueRange<float>(0f, 1f)
            )
        );

        if (BackgroundOpacity.Value < 0f || BackgroundOpacity.Value > 1f)
        {
            BackgroundOpacity.Value = Mathf.Clamp01(BackgroundOpacity.Value);
            Config.Save();
        }
        TextSize = Config.Bind(
            "Appearance",
            "TextSize",
            16f,
            new ConfigDescription(
                "Caption font size from 10.0 to 25.0",
                new AcceptableValueRange<float>(10f, 25f)
            )
        );

        if (TextSize.Value < 10f || TextSize.Value > 25f)
        {
            TextSize.Value = Mathf.Clamp(TextSize.Value, 10f, 25f);
            Config.Save();
        }

        DisableTextColour = Config.Bind(
            "Appearance",
            "DisableTextColour",
            false,
            "Disable custom text colour tags (for example <c:red>Alert</c>)"
        );

        TextLeftAlign = Config.Bind(
            "Appearance",
            "TextLeftAlign",
            false,
            "Align caption text to the left instead of centered"
        );

        HorizontalPosition = Config.Bind(
            "Appearance",
            "HorizontalPosition",
            0f,
            new ConfigDescription(
                "Horizontal position offset for captions",
                new AcceptableValueRange<float>(-270f, 260f)
            )
        );

        if (HorizontalPosition.Value < -270f || HorizontalPosition.Value > 260f)
        {
            HorizontalPosition.Value = Mathf.Clamp(HorizontalPosition.Value, -270f, 260f);
            Config.Save();
        }

        VerticalPosition = Config.Bind(
            "Appearance",
            "VerticalPosition",
            50f,
            new ConfigDescription(
                "Vertical position offset for captions",
                new AcceptableValueRange<float>(0f, 350f)
            )
        );

        if (VerticalPosition.Value < 0f || VerticalPosition.Value > 350f)
        {
            VerticalPosition.Value = Mathf.Clamp(VerticalPosition.Value, 0f, 350f);
            Config.Save();
        }

        EnableDebug = Config.Bind(
            "Developer",
            "EnableDebug",
            false,
            "Enable debug logging for troubleshooting"
        );

        StopConsoleSpam = Config.Bind(
            "Developer",
            "StopConsoleSpam",
            false,
            "When enabled, repeated identical debug messages are suppressed for 5 seconds to reduce console noise"
        );

        GameAudioCaptionFile.SettingChanged += (_, _) => SoundCaptionCatalog.ReloadFromConfig();
        SoundCaptionCatalog.ReloadFromConfig();

        _gameAudioService = new GameAudioCaptionService(this);
        CaptionUI.EnsureInstance();

        Patch();

        Logger.LogInfo($"{Info.Metadata.GUID} v{Info.Metadata.Version} has loaded!");
    }

    internal void Patch()
    {
        Harmony ??= new Harmony(Info.Metadata.GUID);
        Harmony.PatchAll();
    }

    internal void Unpatch()
    {
        Harmony?.UnpatchSelf();
    }

    private void Update()
    {
        // Keep the UI alive even if scene transitions destroy transient objects.
        CaptionUI.EnsureInstance();
    }

    internal static void LogInfo(string message)
    {
        Logger.LogInfo($"{message}");
    }

    internal static void LogWarning(string message)
    {
        Logger.LogWarning($"{message}");
    }

    internal static void LogError(string message)
    {
        Logger.LogError($"{message}");
    }

    internal static void LogDebug(string message)
    {
        if (!Instance.EnableDebug.Value)
            return;

        if (Instance.StopConsoleSpam.Value)
        {
            var now = UnityEngine.Time.realtimeSinceStartup;
            if (Instance._debugCooldowns.TryGetValue(message, out var last) && now - last < DebugSpamCooldownSeconds)
                return;
            Instance._debugCooldowns[message] = now;
        }

        Logger.LogInfo($"[Debug] {message}");
    }

    internal static void LogOutput(string playerName, string text)
    {
        Logger.LogInfo($"{playerName}: {text}");
    }
}