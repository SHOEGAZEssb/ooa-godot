using Godot;

namespace oracleofages;

/// <summary>Port presentation preferences, independent of the original save image.</summary>
internal sealed class PresentationSettings
{
    private const string DefaultPath = "user://presentation.cfg";
    internal bool HudBottom { get; set; }

    internal void Load(string path = DefaultPath)
    {
        using var config = new ConfigFile();
        HudBottom = config.Load(path) == Error.Ok &&
            config.GetValue("display", "hud_bottom", false).AsBool();
    }

    internal Error Save(string path = DefaultPath)
    {
        using var config = new ConfigFile();
        config.SetValue("display", "hud_bottom", HudBottom);
        return config.Save(path);
    }
}
