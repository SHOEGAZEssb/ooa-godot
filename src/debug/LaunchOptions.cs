using Godot;
using System;
using System.Globalization;

namespace oracleofages;

public sealed class LaunchOptions
{
    private readonly string[] _arguments = OS.GetCmdlineUserArgs();

    public int StartingGroup => ParseHex("--group=", 0, 0, 5);
    public int StartingRoom => ParseHex("--room=", 0x11, 0, 0xff);
    public bool Has(string flag) => Array.Exists(
        _arguments, argument => argument.Equals(flag, StringComparison.OrdinalIgnoreCase));

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
