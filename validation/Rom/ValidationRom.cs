using Godot;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace oracleofages;

/// <summary>Explicit, verified ROM input for reference execution in validation only.</summary>
internal static class ValidationRom
{
    private const string CleanUsSha256 =
        "0b56b78a9e45452e98c33edd111234931f1e034dc097f6f23082eb8db6055474";

    private static readonly Lazy<ReadOnlyMemory<byte>> CleanUs = new(LoadVerified);

    // Each worker verifies its input once. ROM bytes are immutable; every
    // execution fixture still owns independent registers and writable memory.
    internal static ReadOnlyMemory<byte> LoadCleanUs() => CleanUs.Value;

    private static ReadOnlyMemory<byte> LoadVerified()
    {
        const string prefix = "--validation-rom=";
        string[] paths = OS.GetCmdlineUserArgs()
            .Where(argument => argument.StartsWith(prefix, StringComparison.Ordinal))
            .Select(argument => argument[prefix.Length..]).ToArray();
        if (paths.Length > 1 || (paths.Length == 1 && string.IsNullOrWhiteSpace(paths[0])))
            throw new InvalidOperationException("Provide at most one nonempty --validation-rom=PATH.");
        string path = paths.Length == 1 ? paths[0] : ProjectSettings.GlobalizePath(
            "res://Legend of Zelda, The - Oracle of Ages (U) [C][!].gbc");
        if (!File.Exists(path))
            throw new FileNotFoundException(
                "ROM-backed validation requires the clean US ROM. Place it in the project root " +
                "or pass -Rom PATH to tools/validate_parallel.ps1.", path);
        byte[] rom = File.ReadAllBytes(path);
        string hash = Convert.ToHexString(SHA256.HashData(rom)).ToLowerInvariant();
        if (rom.Length != 0x100000 || hash != CleanUsSha256)
            throw new InvalidDataException(
                $"Unsupported validation ROM '{path}': size=${rom.Length:x}, SHA-256={hash}; " +
                $"expected clean US size=$100000, SHA-256={CleanUsSha256}.");
        return rom;
    }
}
