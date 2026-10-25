using System;

namespace Oracles;

internal static class OracleAssetPack
{
    internal const string Root = "res://assets/oracle/";

    internal static string RootFor(string path)
    {
        if (!path.StartsWith(Root, StringComparison.Ordinal))
            throw new ArgumentException($"Not a generated asset: {path}.", nameof(path));
        ReadOnlySpan<char> relative = path.AsSpan(Root.Length);
        int separator = relative.IndexOf('/');
        if (separator <= 0 || relative.Contains("..", StringComparison.Ordinal) || relative.Contains('\\'))
            throw new ArgumentException($"Generated asset requires a game pack: {path}.", nameof(path));
        foreach (char value in relative[..separator])
            if (value is not (>= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-'))
                throw new ArgumentException($"Invalid generated asset pack: {path}.", nameof(path));
        return path[..(Root.Length + separator + 1)];
    }
}
