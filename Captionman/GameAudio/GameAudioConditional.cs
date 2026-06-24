using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Captionman;

/// <summary>
/// Resolves entity component names for sound instance by walking its parent hierarchy.
/// Results are cached per Sound instance to avoid repeated GetComponentsInParent calls.
/// </summary>
internal static class GameAudioConditional
{
    private static readonly ConditionalWeakTable<Sound, HashSet<string>> _cache =
        new ConditionalWeakTable<Sound, HashSet<string>>();

    // Per-type cache of Sound fields, so reflection only runs once per MonoBehaviour type.
    private static readonly Dictionary<Type, FieldInfo[]> _soundFieldsByType =
        new Dictionary<Type, FieldInfo[]>();

    /// <summary>
    /// Returns the set of MonoBehaviour type names found in the sound's parent hierarchy.
    /// Used by SoundCaptionCatalog.TryResolve to match entity-specific caption entries.
    /// The entity column in the CSV should contain the exact C# class name such as "ItemRubberDuck".
    /// 
    /// The Transform passed to Sound.Play(Transform, ...) when Sound.Source is null.
    /// Many sounds (e.g. ItemMelee.soundHit) have no pre-assigned AudioSource, so the
    /// follow-target transform is the only way to walk the owner's parent hierarchy.
    /// When both Source and fallbackTransform are null the result is not cached so that
    /// the subsequent call with valid fallbackTransform can still populate the cache.
    /// </summary>
    internal static HashSet<string> GetEntityNames(Sound sound, Transform? fallbackTransform = null)
    {
        if (_cache.TryGetValue(sound, out var cached))
            return cached;

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Transform? root = null;
        if (sound.Source != null)
            root = sound.Source.transform;
        else if (fallbackTransform != null)
            root = fallbackTransform;
        else
            root = FindOwnerTransform(sound);   // last-resort reflection scan

        if (root != null)
        {
            foreach (var mb in root.GetComponentsInParent<MonoBehaviour>())
                names.Add(mb.GetType().Name);
            // Cache only when we have a reliable root so the result is reusable.
            _cache.Add(sound, names);
        }
        // When root is null (source null + no fallback + no scene owner found), return the
        // empty set without caching so a later call with a valid fallbackTransform can still
        // populate the cache.

        return names;
    }

    /// <summary>
    /// Scans all active MonoBehaviours in the scene for a public or private field of type
    /// <see cref="Sound"/> whose value is the given <paramref name="sound"/> instance.
    /// Returns the owning MonoBehaviour's Transform, or null if not found.
    /// Per-type field lists are cached so each MonoBehaviour type is only reflected once.
    /// </summary>
    private static Transform? FindOwnerTransform(Sound sound)
    {
        foreach (var mb in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())
        {
            if (mb == null) continue;
            var type = mb.GetType();

            if (!_soundFieldsByType.TryGetValue(type, out var fields))
            {
                var list = new List<FieldInfo>();
                for (var t = type; t != null && t != typeof(MonoBehaviour); t = t.BaseType)
                {
                    foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    {
                        if (f.FieldType == typeof(Sound))
                            list.Add(f);
                    }
                }
                fields = list.ToArray();
                _soundFieldsByType[type] = fields;
            }

            foreach (var field in fields)
            {
                if (ReferenceEquals(field.GetValue(mb), sound))
                    return mb.transform;
            }
        }
        return null;
    }
}
