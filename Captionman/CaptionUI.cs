using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Captionman;

/// <summary>
/// Persistent caption UI that can render game audio captions and external API captions
/// across all game states.
/// </summary>
public class CaptionUI : MonoBehaviour
{
    public static CaptionUI? Instance { get; private set; }

    private static readonly Queue<PendingCaption> PendingCaptions = new Queue<PendingCaption>();
    private const int MaxPendingCaptions = 40;

    private readonly Queue<CaptionEntry> _captionQueue = new Queue<CaptionEntry>();
    private const int MaxCaptions = 6;
    private const float MinDisplayDuration = 3f;
    private const float MaxDisplayDuration = 14f;

    private static readonly Regex WhitespaceRegex = new Regex(@"\s+", RegexOptions.Compiled);
    private static readonly Regex ColorOpenTagRegex = new Regex(@"<c:[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ColorCloseTagRegex = new Regex(@"</c>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Dictionary<string, string> ApprovedTextColors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["red"] = "#FF5A5A",
        ["yellow"] = "#FFD85A",
        ["green"] = "#6DDB7B",
        ["blue"] = "#73B7FF",
        ["cyan"] = "#66E0FF",
        ["orange"] = "#FFAD5A",
        ["pink"] = "#FF7CC8",
        ["white"] = "#FFFFFF",
        ["gray"] = "#B8B8B8",
        ["grey"] = "#B8B8B8"
    };
    private const float MaxPanelWidth = 500f;
    private const float MinPanelWidth = 180f;
    private const float MinPanelHeight = 38f;
    private const float MaxPanelHeight = 260f;

    private GameObject? _uiContainer;
    private RectTransform? _containerRect;
    private Image? _backgroundPanel;
    private TextMeshProUGUI? _captionText;
    private CanvasGroup? _canvasGroup;

    private readonly Color _backgroundBaseColor = new Color(0f, 0f, 0f, 1f);
    private readonly Color _textColor = new Color(1f, 1f, 1f, 1f);
    private const float DefaultFontSize = 16f;
    private const float PanelPadding = 10f;
    private float _lastAppliedOpacity = -1f;
    private float _lastAppliedFontSize = -1f;
    private bool? _lastAppliedTextLeftAlign;
    private float _lastAppliedHorizontalPosition = float.NaN;
    private float _lastAppliedVerticalPosition = float.NaN;

    internal enum CaptionKind
    {
        Speaker,
        GameAudio,
        System
    }

    private class CaptionEntry
    {
        public string Speaker { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public float Timestamp { get; set; }
        public float DisplayDuration { get; set; }
        public CaptionKind Kind { get; set; }
    }

    private class PendingCaption
    {
        public string Speaker { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public CaptionKind Kind { get; set; }
    }

    internal static void EnsureInstance()
    {
        if (Instance != null)
        {
            return;
        }

        try
        {
            var existing = FindObjectOfType<CaptionUI>();
            if (existing != null)
            {
                Instance = existing;
                return;
            }

            var uiObject = new GameObject("CaptionUI");
            uiObject.AddComponent<CaptionUI>();
        }
        catch (Exception ex)
        {
            Captionman.LogWarning($"Failed to create Caption UI: {ex.Message}");
        }
    }

    public static bool AddSpeakerCaptionSafe(string speaker, string text)
    {
        return AddCaptionSafe(speaker, text, CaptionKind.Speaker);
    }

    public static bool AddSystemCaptionSafe(string text)
    {
        return AddCaptionSafe(string.Empty, text, CaptionKind.System);
    }

    internal static bool AddGameAudioCaptionSafe(string text)
    {
        return AddCaptionSafe(string.Empty, text, CaptionKind.GameAudio);
    }

    private static bool AddCaptionSafe(string speaker, string text, CaptionKind kind)
    {
        if (kind == CaptionKind.Speaker && string.IsNullOrWhiteSpace(speaker))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        EnsureInstance();
        if (Instance != null)
        {
            Instance.EnqueueCaption(speaker, text, kind);
            return true;
        }

        PendingCaptions.Enqueue(new PendingCaption
        {
            Speaker = speaker,
            Text = text,
            Kind = kind
        });

        while (PendingCaptions.Count > MaxPendingCaptions)
        {
            PendingCaptions.Dequeue();
        }

        return false;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        TryInitializeUI();
        FlushPendingCaptions();
    }

    private void Update()
    {
        if (_uiContainer == null || _captionText == null)
        {
            TryInitializeUI();
            FlushPendingCaptions();
        }

        if (_uiContainer == null)
        {
            return;
        }

        ApplyBackgroundOpacity();
        ApplyFontSize();
        ApplyTextAlignment();
        ApplyContainerPosition();

        if (Captionman.Instance == null || !Captionman.Instance.EnableCaptionsUI.Value)
        {
            _uiContainer.SetActive(false);
            return;
        }

        CleanupOldCaptions();

        if (_captionQueue.Count == 0)
        {
            _uiContainer.SetActive(false);
            return;
        }

        _uiContainer.SetActive(true);
        UpdateCaptionDisplay();
    }

    private bool TryInitializeUI()
    {
        if (_uiContainer != null)
        {
            return true;
        }

        var parentRect = ResolveOrCreateParentCanvasRect();
        if (parentRect == null)
        {
            return false;
        }

        Initialize(parentRect);
        return _uiContainer != null;
    }

    private RectTransform? ResolveOrCreateParentCanvasRect()
    {
        if (HUDCanvas.instance != null && HUDCanvas.instance.rect != null)
        {
            transform.SetParent(HUDCanvas.instance.transform, false);
            return HUDCanvas.instance.rect;
        }

        var fallbackCanvasObj = new GameObject("CaptionmanOverlayCanvas");
        fallbackCanvasObj.transform.SetParent(transform, false);

        var canvas = fallbackCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2000;

        fallbackCanvasObj.AddComponent<CanvasScaler>();
        fallbackCanvasObj.AddComponent<GraphicRaycaster>();

        return fallbackCanvasObj.GetComponent<RectTransform>();
    }

    private void Initialize(RectTransform parentRect)
    {
        _uiContainer = new GameObject("CaptionUIContainer");
        _containerRect = _uiContainer.AddComponent<RectTransform>();
        _containerRect.SetParent(parentRect, false);

        _containerRect.anchorMin = new Vector2(0.5f, 0f);
        _containerRect.anchorMax = new Vector2(0.5f, 0f);
        _containerRect.pivot = new Vector2(0.5f, 0f);
        _containerRect.anchoredPosition = new Vector2(0f, 50f);
        _containerRect.sizeDelta = new Vector2(MaxPanelWidth, 120f);

        _canvasGroup = _uiContainer.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 1f;

        var panelObject = new GameObject("CaptionPanel");
        var panelRect = panelObject.AddComponent<RectTransform>();
        panelRect.SetParent(_containerRect, false);
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        _backgroundPanel = panelObject.AddComponent<Image>();
        ApplyBackgroundOpacity(force: true);

        var textObject = new GameObject("CaptionText");
        var textRect = textObject.AddComponent<RectTransform>();
        textRect.SetParent(panelRect, false);
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(PanelPadding, PanelPadding);
        textRect.offsetMax = new Vector2(-PanelPadding, -PanelPadding);

        _captionText = textObject.AddComponent<TextMeshProUGUI>();
        _captionText.alignment = TextAlignmentOptions.Bottom;
        _captionText.fontStyle = FontStyles.Bold;
        _captionText.color = _textColor;
        _captionText.enableWordWrapping = true;
        _captionText.overflowMode = TextOverflowModes.Overflow;
        ApplyFontSize(force: true);
        TrySetGameFont();

        _uiContainer.SetActive(false);
        Captionman.LogDebug("Caption UI initialized");
    }

    private void TrySetGameFont()
    {
        if (_captionText == null)
        {
            return;
        }

        try
        {
            var haul = GameObject.Find("Tax Haul");
            if (haul == null)
            {
                return;
            }

            var haulText = haul.GetComponent<TMP_Text>();
            if (haulText != null && haulText.font != null)
            {
                _captionText.font = haulText.font;
            }
        }
        catch (Exception ex)
        {
            Captionman.LogDebug($"Unable to apply game font: {ex.Message}");
        }
    }

    private void EnqueueCaption(string speaker, string text, CaptionKind kind)
    {
        _captionQueue.Enqueue(new CaptionEntry
        {
            Speaker = speaker,
            Text = text,
            Timestamp = Time.time,
            DisplayDuration = ComputeReadDurationSeconds(speaker, text, kind),
            Kind = kind
        });

        while (_captionQueue.Count > MaxCaptions)
        {
            _captionQueue.Dequeue();
        }
    }

    private void FlushPendingCaptions()
    {
        if (Instance != this)
        {
            return;
        }

        while (PendingCaptions.Count > 0)
        {
            var pending = PendingCaptions.Dequeue();
            EnqueueCaption(pending.Speaker, pending.Text, pending.Kind);
        }
    }

    private void CleanupOldCaptions()
    {
        var now = Time.time;
        while (_captionQueue.Count > 0)
        {
            var oldest = _captionQueue.Peek();
            if ((now - oldest.Timestamp) < oldest.DisplayDuration)
            {
                break;
            }
            _captionQueue.Dequeue();
        }
    }

    private void UpdateCaptionDisplay()
    {
        if (_captionText == null || _canvasGroup == null)
        {
            return;
        }

        var lines = new List<string>(_captionQueue.Count);

        foreach (var entry in _captionQueue)
        {
            switch (entry.Kind)
            {
                case CaptionKind.GameAudio:
                    lines.Add(TransformColorTags(entry.Text));
                    break;
                case CaptionKind.Speaker:
                    lines.Add($"<b>{TransformColorTags(entry.Speaker)}:</b> {TransformColorTags(entry.Text)}");
                    break;
                default:
                    lines.Add(TransformColorTags(entry.Text));
                    break;
            }
        }

        _captionText.text = string.Join("\n", lines);
        UpdatePanelSize();
        _canvasGroup.alpha = 1f;
    }

    private static float ComputeReadDurationSeconds(string speaker, string text, CaptionKind kind)
    {
        var plainText = StripCustomColorTags(text);
        var plainSpeaker = StripCustomColorTags(speaker);
        var lineText = kind == CaptionKind.Speaker && !string.IsNullOrWhiteSpace(speaker)
            ? $"{plainSpeaker}: {plainText}"
            : plainText;

        if (string.IsNullOrWhiteSpace(lineText))
        {
            return MinDisplayDuration;
        }

        var normalized = WhitespaceRegex.Replace(lineText.Trim(), " ");
        var charCount = normalized.Length;
        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var wordCount = words.Length;

        // Estimate read time using both words and characters, then keep the slower estimate.
        var fromWords = wordCount / 3.0f;
        var fromChars = charCount / 14.0f;
        var estimated = 1.5f + Mathf.Max(fromWords, fromChars);
        return Mathf.Clamp(estimated, MinDisplayDuration, MaxDisplayDuration);
    }

    private static string StripCustomColorTags(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var withoutOpen = ColorOpenTagRegex.Replace(text, string.Empty);
        return ColorCloseTagRegex.Replace(withoutOpen, string.Empty);
    }

    private static string TransformColorTags(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var colorsEnabled = Captionman.Instance == null || !Captionman.Instance.DisableTextColour.Value;
        var result = new StringBuilder(text.Length + 16);
        var convertedStack = new Stack<bool>();

        for (var i = 0; i < text.Length;)
        {
            if (i + 3 < text.Length && text[i] == '<' && (text[i + 1] == 'c' || text[i + 1] == 'C') && text[i + 2] == ':')
            {
                var endIndex = text.IndexOf('>', i + 3);
                if (endIndex > i)
                {
                    var colorToken = text.Substring(i + 3, endIndex - (i + 3)).Trim();
                    if (colorsEnabled && TryResolveColorToken(colorToken, out var colorHex))
                    {
                        result.Append("<color=").Append(colorHex).Append('>');
                        convertedStack.Push(true);
                    }
                    else
                    {
                        convertedStack.Push(false);
                    }

                    i = endIndex + 1;
                    continue;
                }
            }

            if (i + 3 < text.Length
                && text[i] == '<'
                && text[i + 1] == '/'
                && (text[i + 2] == 'c' || text[i + 2] == 'C')
                && text[i + 3] == '>')
            {
                if (convertedStack.Count > 0 && convertedStack.Pop())
                {
                    result.Append("</color>");
                }

                i += 4;
                continue;
            }

            result.Append(text[i]);
            i++;
        }

        while (convertedStack.Count > 0)
        {
            if (convertedStack.Pop())
            {
                result.Append("</color>");
            }
        }

        return result.ToString();
    }

    private static bool TryResolveColorToken(string colorToken, out string colorHex)
    {
        colorHex = string.Empty;

        if (ApprovedTextColors.TryGetValue(colorToken, out var approvedHex))
        {
            colorHex = approvedHex;
            return true;
        }

        if (TryParseRgbColorToken(colorToken, out colorHex))
        {
            return true;
        }

        return false;
    }

    private static bool TryParseRgbColorToken(string colorToken, out string colorHex)
    {
        colorHex = string.Empty;

        var parts = colorToken.Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
        {
            return false;
        }

        if (!TryParseColorComponent(parts[0], out var r)
            || !TryParseColorComponent(parts[1], out var g)
            || !TryParseColorComponent(parts[2], out var b))
        {
            return false;
        }

        var color = new Color32((byte)r, (byte)g, (byte)b, 255);
        colorHex = $"#{ColorUtility.ToHtmlStringRGB(color)}";
        return true;
    }

    private static bool TryParseColorComponent(string raw, out int component)
    {
        component = 0;
        var trimmed = raw.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
        {
            component = Mathf.Clamp(intValue, 0, 255);
            return true;
        }

        if (!float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
        {
            return false;
        }

        if (floatValue <= 1f)
        {
            component = Mathf.Clamp(Mathf.RoundToInt(floatValue * 255f), 0, 255);
            return true;
        }

        component = Mathf.Clamp(Mathf.RoundToInt(floatValue), 0, 255);
        return true;
    }

    private void ApplyBackgroundOpacity(bool force = false)
    {
        if (_backgroundPanel == null || Captionman.Instance == null)
        {
            return;
        }

        var opacity = Mathf.Clamp01(Captionman.Instance.BackgroundOpacity.Value);
        if (!force && Mathf.Approximately(opacity, _lastAppliedOpacity))
        {
            return;
        }

        _backgroundPanel.color = new Color(
            _backgroundBaseColor.r,
            _backgroundBaseColor.g,
            _backgroundBaseColor.b,
            opacity
        );
        _lastAppliedOpacity = opacity;
    }

    private void UpdatePanelSize()
    {
        if (_captionText == null || _containerRect == null)
        {
            return;
        }

        var preferred = _captionText.GetPreferredValues(_captionText.text, MaxPanelWidth - (PanelPadding * 2f), 0f);
        var width = Mathf.Clamp(preferred.x + (PanelPadding * 2f), MinPanelWidth, MaxPanelWidth);
        var height = Mathf.Clamp(preferred.y + (PanelPadding * 2f), MinPanelHeight, MaxPanelHeight);
        _containerRect.sizeDelta = new Vector2(width, height);
    }

    private void ApplyFontSize(bool force = false)
    {
        if (_captionText == null || Captionman.Instance == null)
        {
            return;
        }

        var fontSize = Mathf.Clamp(Captionman.Instance.TextSize.Value, 10f, 25f);
        if (!force && Mathf.Approximately(fontSize, _lastAppliedFontSize))
        {
            return;
        }

        _captionText.fontSize = fontSize;
        _lastAppliedFontSize = fontSize;
    }

    private void ApplyTextAlignment(bool force = false)
    {
        if (_captionText == null || Captionman.Instance == null)
        {
            return;
        }

        var leftAlign = Captionman.Instance.TextLeftAlign.Value;
        if (!force && _lastAppliedTextLeftAlign.HasValue && _lastAppliedTextLeftAlign.Value == leftAlign)
        {
            return;
        }

        _captionText.alignment = leftAlign
            ? TextAlignmentOptions.BottomLeft
            : TextAlignmentOptions.Bottom;
        _lastAppliedTextLeftAlign = leftAlign;
    }

    private void ApplyContainerPosition(bool force = false)
    {
        if (_containerRect == null || Captionman.Instance == null)
        {
            return;
        }

        var horizontal = Mathf.Clamp(Captionman.Instance.HorizontalPosition.Value, -270f, 260f);
        var vertical = Mathf.Clamp(Captionman.Instance.VerticalPosition.Value, 0f, 350f);

        if (!float.IsFinite(horizontal))
        {
            horizontal = 0f;
        }

        if (!float.IsFinite(vertical))
        {
            vertical = 50f;
        }

        if (!force
            && Mathf.Approximately(horizontal, _lastAppliedHorizontalPosition)
            && Mathf.Approximately(vertical, _lastAppliedVerticalPosition))
        {
            return;
        }

        _containerRect.anchoredPosition = new Vector2(horizontal, vertical);
        _lastAppliedHorizontalPosition = horizontal;
        _lastAppliedVerticalPosition = vertical;
    }

    public void ClearCaptions()
    {
        _captionQueue.Clear();
        if (_captionText != null)
        {
            _captionText.text = string.Empty;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
