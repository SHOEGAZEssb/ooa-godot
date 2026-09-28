using Godot;

namespace oracleofages;

/// <summary>Port presentation preferences, independent of the original save image.</summary>
internal sealed class PresentationSettings
{
    private const string DefaultPath = "user://presentation.cfg";
    internal bool HudBottom { get; set; }
    internal bool RoomOverlayEnabled { get; set; } = true;

    internal void Load(string path = DefaultPath)
    {
        using var config = new ConfigFile();
        bool loaded = config.Load(path) == Error.Ok;
        HudBottom = loaded &&
            config.GetValue("display", "hud_bottom", false).AsBool();
        RoomOverlayEnabled = !loaded ||
            config.GetValue("display", "room_overlay_enabled", true).AsBool();
    }

    internal Error Save(string path = DefaultPath)
    {
        using var config = new ConfigFile();
        config.SetValue("display", "hud_bottom", HudBottom);
        config.SetValue("display", "room_overlay_enabled", RoomOverlayEnabled);
        return config.Save(path);
    }
}
