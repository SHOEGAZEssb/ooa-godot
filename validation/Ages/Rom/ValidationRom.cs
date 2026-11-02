using System.IO;
using System.Security.Cryptography;

namespace Oracles;

/// <summary>Explicit, verified ROM input for reference execution in validation only.</summary>
internal static class ValidationRom
{
    private const string CleanUsSha256 =
        "0b56b78a9e45452e98c33edd111234931f1e034dc097f6f23082eb8db6055474";

    private static readonly Lazy<ReadOnlyMemory<byte>> CleanUs = new(() => LoadVerified(OracleGame.Ages));
    private static readonly Lazy<ReadOnlyMemory<byte>> CleanSeasonsUs = new(() => LoadVerified(OracleGame.Seasons));

    // Each worker verifies its input once. ROM bytes are immutable; every
    // execution fixture still owns independent registers and writable memory.
    internal static ReadOnlyMemory<byte> LoadCleanUs(OracleGame game = OracleGame.Ages) =>
        game == OracleGame.Ages ? CleanUs.Value : game == OracleGame.Seasons ? CleanSeasonsUs.Value :
            throw new ArgumentOutOfRangeException(nameof(game));

    private static ReadOnlyMemory<byte> LoadVerified(OracleGame game)
    {
        string prefix = game == OracleGame.Ages ? "--validation-rom=" : "--validation-seasons-rom=";
        string expected = game == OracleGame.Ages ? CleanUsSha256 :
            "db45c29f56ab7f15d5b41a46dcb4c91976b9ba4707343ef154d5349e7d5d865b";
        string[] paths = OS.GetCmdlineUserArgs()
            .Where(argument => argument.StartsWith(prefix, StringComparison.Ordinal))
            .Select(argument => argument[prefix.Length..]).ToArray();
        if (paths.Length > 1 || (paths.Length == 1 && string.IsNullOrWhiteSpace(paths[0])))
            throw new InvalidOperationException($"Provide at most one nonempty {prefix}PATH.");
        string path = paths.Length == 1 ? paths[0] : ProjectSettings.GlobalizePath(
            $"res://Legend of Zelda, The - Oracle of {game} (U) [C][!].gbc");
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"ROM-backed validation requires clean US {game}. Use tools/validate_parallel.ps1 " +
                $"to resolve/build it or pass {prefix}PATH.", path);
        byte[] rom = File.ReadAllBytes(path);
        string hash = Convert.ToHexString(SHA256.HashData(rom)).ToLowerInvariant();
        if (rom.Length != 0x100000 || hash != expected)
            throw new InvalidDataException(
                $"Unsupported validation ROM '{path}': size=${rom.Length:x}, SHA-256={hash}; " +
                $"expected clean US {game} size=$100000, SHA-256={expected}.");
        return rom;
    }
}
