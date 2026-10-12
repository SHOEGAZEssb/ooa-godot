using Godot;
using System;
using System.Globalization;

namespace oracleofages;

public sealed class LaunchOptions
{
    private readonly string[] _arguments = OS.GetCmdlineUserArgs();

    public int StartingGroup => ParseHex("--group=", GetDefaultGroup(), 0, 7);
    public int StartingRoom => ParseHex("--room=", GetDefaultRoom(), 0, 0xff);
    public int DebugWarpGroup => ParseHex("--debug-warp-group=", 4, 0, 7);
    public int DebugWarpRoom => ParseHex("--debug-warp-room=", 0x11, 0, 0xff);
    public bool HasWorldOverride => HasArgument("--group=") || HasArgument("--room=");
    public bool ShowMainMenu => !HasValidationFlag() && !HasWorldOverride && !Has("--skip-menu");
    public bool Has(string flag) => Array.Exists(
        _arguments, argument => argument.Equals(flag, StringComparison.OrdinalIgnoreCase));

    private int GetDefaultGroup()
    {
        // Keep room-only development commands and validation
        // in overworld group 0. The development launch starts at Jabu-Jabu's
        // Belly entrance, room $5:$56.
        return HasValidationFlag() || HasArgument("--room=") ? 0 : 5;
    }

    private int GetDefaultRoom()
    {
        // Preserve the historical room $11 fallback when a group or a
        // validation was explicitly requested without a room.
        return HasValidationFlag() || HasArgument("--group=") ? 0x11 : 0x56;
    }

    private bool HasValidationFlag() => Has("--validate");

    private bool HasArgument(string prefix) => Array.Exists(
        _arguments,
        argument => argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private int ParseHex(string prefix, int fallback, int minimum, int maximum)
    {
        foreach (string argument in _arguments)
        {
            if (!argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;
            string value = argument[prefix.Length..].Replace("0x", "", StringComparison.OrdinalIgnoreCase);
            if (int.TryParse(value, NumberStyles.HexNumber, null, out int parsed) &&
                parsed >= minimum && parsed <= maximum)
                return parsed;
        }
        return fallback;
    }
}
