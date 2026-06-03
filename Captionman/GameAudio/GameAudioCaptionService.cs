using System;
using System.Collections.Generic;
using UnityEngine;

namespace Captionman;

/// <summary>
/// Receives game audio events from Harmony patches and routes them to CaptionUI.
/// Handles proximity filtering, cooldowns, and caption text lookup.
/// </summary>
internal class GameAudioCaptionService
{
    private readonly Captionman _plugin;

    // Proximity radius in Unity world units (~meters). Sounds beyond this are suppressed.
    private const float ProximityRadius = 15f;

    // Cooldown tracking: key = caption text, value = Time.time when last shown
    private readonly Dictionary<string, float> _cooldowns = new Dictionary<string, float>();

    public GameAudioCaptionService(Captionman plugin)
    {
        _plugin = plugin;
    }

    /// <summary>
    /// Called by Harmony patches when a game audio event fires.
    /// emitterPosition: world position of the sound source (pass null for global events).
    /// captionText: pre-resolved text from the caption dictionary.
    /// isGlobal: skip proximity check (extraction alarms etc.)
    /// </summary>
    internal void OnAudioEvent(string captionText, Vector3? emitterPosition, bool isGlobal = false)
    {
        if (!_plugin.EnableCaptionsUI.Value) return;
        if (!_plugin.GameAudioCaptions.Value) return;

        if (!isGlobal && emitterPosition.HasValue)
        {
            if (!IsWithinProximity(emitterPosition.Value))
            {
                Captionman.LogDebug($"GameAudio suppressed (out of range): {captionText}");
                return;
            }
        }

        if (IsOnCooldown(captionText)) return;

        SetCooldown(captionText);
        CaptionUI.AddGameAudioCaptionSafe(captionText);
        Captionman.LogDebug($"GameAudio caption: {captionText}");
    }

    private bool IsWithinProximity(Vector3 emitterPosition)
    {
        try
        {
            // Use local PlayerAvatar position if available, fall back to Camera
            var localPlayer = PlayerAvatar.instance;
            if (localPlayer != null)
            {
                return Vector3.Distance(localPlayer.transform.position, emitterPosition) <= ProximityRadius;
            }

            var cam = Camera.main;
            if (cam != null)
            {
                return Vector3.Distance(cam.transform.position, emitterPosition) <= ProximityRadius;
            }
        }
        catch (Exception ex)
        {
            Captionman.LogDebug($"Proximity check error: {ex.Message}");
        }

        // Fail open: show caption if we can't determine distance
        return true;
    }

    private bool IsOnCooldown(string captionText)
    {
        if (!_cooldowns.TryGetValue(captionText, out var lastTime)) return false;
        var duration = Mathf.Max(0f, _plugin.GameAudioRepeatCooldownSeconds.Value);
        return (Time.time - lastTime) < duration;
    }

    private void SetCooldown(string captionText)
    {
        _cooldowns[captionText] = Time.time;
    }
}
