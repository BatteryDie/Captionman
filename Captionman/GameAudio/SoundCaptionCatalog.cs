using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;

namespace Captionman;

/// <summary>
/// Loads sound-to-caption mappings from caption catalog CSV files.
/// Source of truth: rows with non-empty "caption" are enabled.
/// </summary>
internal sealed class SoundCaptionCatalog
{
    private static readonly object CatalogLock = new object();
    private const string DefaultCaptionFileName = "captionsEN.csv";
    private readonly Dictionary<string, Entry> _entriesByName;
    private static SoundCaptionCatalog _current = new SoundCaptionCatalog(new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase));

    internal static SoundCaptionCatalog Current
    {
        get
        {
            lock (CatalogLock)
            {
                return _current;
            }
        }
    }

    internal readonly struct Entry
    {
        internal Entry(string caption, bool isGlobal)
        {
            Caption = caption;
            IsGlobal = isGlobal;
        }

        internal string Caption { get; }
        internal bool IsGlobal { get; }
    }

    private SoundCaptionCatalog(Dictionary<string, Entry> entriesByName)
    {
        _entriesByName = entriesByName;
    }

    private sealed class ResolvedCaptionCatalog
    {
        internal ResolvedCaptionCatalog(string csvPath, string source)
        {
            CsvPath = csvPath;
            Source = source;
        }

        internal string CsvPath { get; }
        internal string Source { get; }
    }

    internal static void ReloadFromConfig()
    {
        var selector = NormalizeCaptionSelector(Captionman.Instance?.GameAudioCaptionFile?.Value);
        var resolvedCatalog = ResolveCsvPath(selector);

        var requestedName = string.IsNullOrWhiteSpace(selector)
            ? DefaultCaptionFileName
            : EnsureCsvExtension(selector);

        if (resolvedCatalog != null)
        {
            var loadedName = Path.GetFileName(resolvedCatalog.CsvPath);
            if (!string.Equals(requestedName, loadedName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(loadedName, DefaultCaptionFileName, StringComparison.OrdinalIgnoreCase))
            {
                Captionman.LogWarning($"Failed to load {requestedName}, falling back to {DefaultCaptionFileName}");
            }
            else
            {
                Captionman.LogInfo($"Successfully loaded {loadedName}");
            }
        }

        var loadedCatalog = Load(resolvedCatalog);

        lock (CatalogLock)
        {
            _current = loadedCatalog;
        }
    }

    private static SoundCaptionCatalog Load(ResolvedCaptionCatalog? resolvedCatalog)
    {
        if (resolvedCatalog == null)
        {
            Captionman.LogWarning("Caption CSV not found. Configure Captions.GameAudioCaptionFile with a CSV filename like captionsEN.csv. captionsEN.csv is always used as fallback when available.");
            return new SoundCaptionCatalog(new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase));
        }

        try
        {
            var csvPath = resolvedCatalog.CsvPath;
            var rows = File.ReadLines(csvPath).ToList();
            if (rows.Count == 0)
            {
                Captionman.LogWarning($"Caption CSV is empty: {csvPath}");
                return new SoundCaptionCatalog(new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase));
            }

            var header = ParseCsvLine(rows[0]);
            var nameIndex = FindColumnIndex(header, "name");
            var captionIndex = FindColumnIndex(header, "caption");
            var isGlobalIndex = FindColumnIndex(header, "isglobal");

            if (nameIndex < 0 || captionIndex < 0)
            {
                Captionman.LogError("Caption CSV is missing required columns: name, caption");
                return new SoundCaptionCatalog(new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase));
            }

            var map = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            var loaded = 0;
            var loadedGlobal = 0;

            for (var i = 1; i < rows.Count; i++)
            {
                var line = rows[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var fields = ParseCsvLine(line);
                if (fields.Count <= Math.Max(nameIndex, captionIndex))
                {
                    continue;
                }

                var name = fields[nameIndex].Trim();
                var caption = fields[captionIndex].Trim();
                var isGlobal = isGlobalIndex >= 0 && isGlobalIndex < fields.Count
                    ? ParseBool(fields[isGlobalIndex])
                    : false;

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(caption))
                {
                    continue;
                }

                map[name] = new Entry(caption, isGlobal);
                loaded++;
                if (isGlobal)
                {
                    loadedGlobal++;
                }
            }

            Captionman.LogInfo($"Loaded sound caption catalog: {loaded} entries ({loadedGlobal} global) from {Path.GetFileName(csvPath)} ({resolvedCatalog.Source})");
            return new SoundCaptionCatalog(map);
        }
        catch (Exception ex)
        {
            Captionman.LogError($"Failed to load sound caption CSV: {ex.Message}");
            return new SoundCaptionCatalog(new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase));
        }
    }

    internal bool TryResolve(string clipName, Sound sound, out string caption, out bool isGlobal)
    {
        caption = string.Empty;
        isGlobal = false;

        if (string.IsNullOrWhiteSpace(clipName))
        {
            return false;
        }

        if (!_entriesByName.TryGetValue(clipName, out var entry))
        {
            Captionman.LogDebug($"No caption for \"{clipName}\"");
            return false;
        }

        caption = entry.Caption;

        isGlobal = entry.IsGlobal
               || clipName.IndexOf(" global", StringComparison.OrdinalIgnoreCase) >= 0
               || sound.Type == AudioManager.AudioType.Global;

        return true;
    }

    private static ResolvedCaptionCatalog? ResolveCsvPath(string selector)
    {
        var requestedName = string.IsNullOrWhiteSpace(selector)
            ? DefaultCaptionFileName
            : EnsureCsvExtension(selector);

        var dirs = GetSearchDirectories();

        foreach (var dir in dirs)
        {
            var requestedInRoot = Path.Combine(dir, requestedName);
            if (File.Exists(requestedInRoot))
            {
                return new ResolvedCaptionCatalog(requestedInRoot, "requested-root");
            }

            var requestedInCaptions = Path.Combine(dir, "Captions", requestedName);
            if (File.Exists(requestedInCaptions))
            {
                return new ResolvedCaptionCatalog(requestedInCaptions, "requested-captions");
            }
        }

        if (!string.Equals(requestedName, DefaultCaptionFileName, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var dir in dirs)
            {
                var defaultInRoot = Path.Combine(dir, DefaultCaptionFileName);
                if (File.Exists(defaultInRoot))
                {
                    return new ResolvedCaptionCatalog(defaultInRoot, "default-root");
                }

                var defaultInCaptions = Path.Combine(dir, "Captions", DefaultCaptionFileName);
                if (File.Exists(defaultInCaptions))
                {
                    return new ResolvedCaptionCatalog(defaultInCaptions, "default-captions");
                }
            }
        }

        var legacyNames = new[]
        {
            "game_audio_captions.csv",
            "sound_caption_review.csv",
            "sound_captions.csv"
        };

        foreach (var dir in dirs)
        {
            foreach (var legacyName in legacyNames)
            {
                var inCaptionsFolder = Path.Combine(dir, "Captions", legacyName);
                if (File.Exists(inCaptionsFolder))
                {
                    Captionman.LogDebug($"Using legacy caption catalog fallback: {inCaptionsFolder}");
                    return new ResolvedCaptionCatalog(inCaptionsFolder, "legacy-captions");
                }

                var inRoot = Path.Combine(dir, legacyName);
                if (File.Exists(inRoot))
                {
                    Captionman.LogDebug($"Using legacy caption catalog fallback: {inRoot}");
                    return new ResolvedCaptionCatalog(inRoot, "legacy-root");
                }
            }
        }

        return null;
    }

    private static string EnsureCsvExtension(string fileName)
    {
        if (fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return fileName;
        }

        return $"{fileName}.csv";
    }

    private static string NormalizeCaptionSelector(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static List<string> GetSearchDirectories()
    {
        var dirs = new List<string>
        {
            Paths.PluginPath,
            Path.Combine(Paths.PluginPath, "BatteryDie.Captionman"),
            Path.Combine(Paths.PluginPath, "BatteryDie-Captionman"),
            Paths.GameRootPath,
            Paths.ConfigPath
        };

        var assemblyDir = Path.GetDirectoryName(typeof(Captionman).Assembly.Location);
        if (!string.IsNullOrWhiteSpace(assemblyDir))
        {
            dirs.Insert(0, assemblyDir!);
        }

        return dirs
            .Where(dir => !string.IsNullOrWhiteSpace(dir))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int FindColumnIndex(IReadOnlyList<string> header, string columnName)
    {
        for (var i = 0; i < header.Count; i++)
        {
            if (string.Equals(header[i].Trim(), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];

            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        result.Add(current.ToString());
        return result;
    }

    private static bool ParseBool(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        if (bool.TryParse(normalized, out var parsed))
        {
            return parsed;
        }

        return string.Equals(normalized, "1", StringComparison.OrdinalIgnoreCase)
               || string.Equals(normalized, "yes", StringComparison.OrdinalIgnoreCase)
               || string.Equals(normalized, "y", StringComparison.OrdinalIgnoreCase);
    }
}
