using System;

namespace Oracles;

// data/ages/tile_properties/interactableTiles.s: six collision-mode pointers,
// including aliases and unterminated label fallthrough. $ff represents the
// source lookup miss; real parameters retain their direction/handler bits.
internal sealed class InteractableTileDatabase
{
    public string AssetRoot { get; }

    internal const byte NoEntry = 0xff;
    private byte[]? _parametersSourceCache;
    private byte[] _parameters => _parametersSourceCache ??= OracleAssetCache.ReadBytes(AssetRoot + "metadata/interactableTiles.bin");

    internal InteractableTileDatabase(string assetRoot)
    {
        AssetRoot = assetRoot;
        _parametersSourceCache = OracleAssetCache.ReadBytes(AssetRoot + "metadata/interactableTiles.bin");

        if (_parameters.Length != 6 * 256)
            throw new InvalidOperationException("interactableTiles.s: expected six complete tile lookup profiles (1536 bytes).");
        int records = 0;
        for (int index = 0; index < _parameters.Length; index++)
        {
            byte parameter = _parameters[index];
            if (parameter == NoEntry) continue;
            if ((parameter & 15) > 6)
                throw new NotSupportedException($"interactableTiles.s: unsupported Ages parameter ${parameter:x2} in collision mode ${index >> 8:x2}, tile ${index & 255:x2}.");
            records++;
        }
        if (records != 115)
            throw new InvalidOperationException($"interactableTiles.s: expected 115 source lookup entries, got {records}.");
    }

    internal byte Parameter(int mode, byte tile) => mode is >= 0 and < 6
        ? _parameters[mode * 256 + tile]
        : throw new NotSupportedException($"interactableTiles.s: unsupported collision mode ${mode:x2}.");
}
