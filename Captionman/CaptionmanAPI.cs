namespace Captionman;

/// <summary>
/// Public API for external mods to push captions into Captionman's UI.
/// </summary>
public static class CaptionmanApi
{
    /// Send a generic caption line to the caption UI.
    public static bool SendCaption(string text)
    {
        return CaptionUI.AddSystemCaptionSafe(text);
    }

    /// Send a speaker-formatted caption line, rendered as "Speaker: Text".
    public static bool SendCaption(string speaker, string text)
    {
        return CaptionUI.AddSpeakerCaptionSafe(speaker, text);
    }
}
