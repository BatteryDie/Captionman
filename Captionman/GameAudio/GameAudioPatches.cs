using HarmonyLib;
using UnityEngine;

namespace Captionman;

/// <summary>
/// Patches Sound.Play and Sound.PlayLoop to intercept game audio events and route
/// captionable sounds to GameAudioCaptionService using the CSV catalog.
/// </summary>
[HarmonyPatch]
static class GameAudioPatches
{
    private static void HandlePlayPostfix(Sound __instance, Vector3 position, AudioSource __result, Transform? entityTransform = null)
    {
        if (__result == null || __instance.Sounds == null || __instance.Sounds.Length == 0)
        {
            return;
        }

        var service = Captionman.Instance?.GameAudioService;
        if (service == null)
        {
            return;
        }

        var clipName = __result.clip?.name;
        if (string.IsNullOrWhiteSpace(clipName))
        {
            clipName = __instance.Sounds[0]?.name;
        }

        if (string.IsNullOrWhiteSpace(clipName))
        {
            return;
        }

        if (!SoundCaptionCatalog.Current.TryResolve(clipName, __instance, out var caption, out var isGlobal, entityTransform))
        {
            return;
        }

        Captionman.LogDebug($"GameAudio CSV match: '{clipName}' -> '{caption}' (global={isGlobal})");
        service.OnAudioEvent(caption, position, isGlobal);
    }

    // Play(Vector3 position, float, float, float, float)
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Sound), nameof(Sound.Play), new[] { typeof(Vector3), typeof(float), typeof(float), typeof(float), typeof(float) })]
    private static void Sound_Play_Vector3_Postfix(Sound __instance, Vector3 position, AudioSource __result)
    {
        HandlePlayPostfix(__instance, position, __result);
    }

    // Play(Transform followTarget, float, float, float, float)
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Sound), nameof(Sound.Play), new[] { typeof(Transform), typeof(float), typeof(float), typeof(float), typeof(float) })]
    private static void Sound_Play_Transform_Postfix(Sound __instance, Transform followTarget, AudioSource __result)
    {
        HandlePlayPostfix(__instance, followTarget != null ? followTarget.position : Vector3.zero, __result, followTarget);
    }

    // Play(Transform followTarget, Vector3 contactPoint, float, float, float, float)
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Sound), nameof(Sound.Play), new[] { typeof(Transform), typeof(Vector3), typeof(float), typeof(float), typeof(float), typeof(float) })]
    private static void Sound_Play_TransformContact_Postfix(Sound __instance, Transform followTarget, Vector3 contactPoint, AudioSource __result)
    {
        HandlePlayPostfix(__instance, contactPoint, __result, followTarget);
    }

    // PlayLoop(bool playing, float fadeInSpeed, float fadeOutSpeed, float pitchMultiplier, float volumeMultiplier)
    // Called every Update() frame while the loop is active. Cooldown in GameAudioCaptionService
    // controls how often the caption repeats.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Sound), nameof(Sound.PlayLoop))]
    private static void Sound_PlayLoop_Prefix(Sound __instance, bool playing)
    {
        if (!playing) return;
        if (__instance.Source == null || !__instance.Source.enabled) return;
        if (__instance.Sounds == null || __instance.Sounds.Length == 0) return;

        var service = Captionman.Instance?.GameAudioService;
        if (service == null) return;

        // Source.clip may not be assigned yet (PlayLoop sets it on the first call),
        // so fall back to the first clip in the Sounds array.
        var clipName = __instance.Source.clip?.name;
        if (string.IsNullOrWhiteSpace(clipName))
            clipName = __instance.Sounds[0]?.name;
        if (string.IsNullOrWhiteSpace(clipName)) return;

        if (!SoundCaptionCatalog.Current.TryResolve(clipName, __instance, out var caption, out var isGlobal))
        {
            Captionman.LogDebug($"No caption for loop \"{clipName}\"");
            return;
        }

        var position = __instance.Source.transform.position;
        Captionman.LogDebug($"GameAudio CSV match (loop): '{clipName}' -> '{caption}' (global={isGlobal})");
        service.OnAudioEvent(caption, position, isGlobal);
    }
}
