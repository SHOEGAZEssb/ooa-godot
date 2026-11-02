using System.Security.Cryptography;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSharedScrollGraphics()
    {
        var graphics = new ScreenTransitionGraphicsDatabase(SeasonsGameContent.AssetRoot);
        var bytes = new List<byte>();
        for (int id = 0; id < 0x29; id++)
        {
            int order = 0;
            foreach (var upload in graphics.Uploads(id))
            {
                bytes.AddRange([(byte)id, (byte)order++, (byte)upload.Address, (byte)(upload.Address >> 8),
                    (byte)upload.Tiles, unchecked((byte)upload.Palette)]);
                bytes.AddRange(upload.Data);
            }
        }
        // Independent ROM bank $04:$595e pointer walk, decoded by the
        // disassembly's tools/common.py rather than the importer's decoder.
        FailIf(bytes.Count != 29868 || Convert.ToHexString(SHA256.HashData(bytes.ToArray())) !=
            "C6142119DE6B9B7017A131DB9100EF6B17EAB08F5119CA292D4F7AE29FFCD2CD",
            "Seasons unique graphics lost native ordered uploads, palette commands or decompressed payloads.");
        bytes.Clear();
        for (int tileset = 0; tileset < 0x63; tileset++)
        for (int state = tileset < 0x1b ? 0 : -1; state < (tileset < 0x1b ? 4 : 0); state++)
        {
            var entry = graphics.ForTileset(tileset, state);
            bytes.AddRange([(byte)tileset, unchecked((byte)state), (byte)entry.Unique,
                (byte)entry.Entries, (byte)graphics.PaletteForTileset(tileset, state)]);
        }
        // tilesetData at $04:$4c84: 27 pointers to four season records,
        // followed by 72 concrete eight-byte tilesets.
        FailIf(bytes.Count != 900 || Convert.ToHexString(SHA256.HashData(bytes.ToArray())) !=
            "F87D9D51FE9D69DE3D41F94B586BBFAAD6A4B3CD2AFC80272A02D7F5751819B0",
            "Seasons transition records lost seasonal indirection or native header/palette identity.");

        var data = new SeasonsWorldData();
        var scroll = new RoomScrollController();
        // $b6 -> $a6 changes unique $02 to post-scroll $81. Its single
        // upload falls through into the finisher: 4 + 32 + 2 updates.
        var source = data.LoadRoom(0xb6, 0);
        var target = data.LoadRoom(0xa6, 0);
        FailIf(source.LoadsUniqueGraphicsAfterScroll || !target.LoadsUniqueGraphicsAfterScroll,
            "Seasons room tileset bit 7 must retain its post-scroll upload gate.");
        scroll.ResetGraphics(source);
        scroll.Begin(new(112.5f, 6.25f), Vector2I.Up, source, target);
        FailIf(scroll.TotalFrames != 38, "Native post-scroll scheduling added a fictitious setup update.");
        for (int update = 1; update <= 38; update++)
        {
            scroll.Advance();
            FailIf(scroll.Frame != Math.Clamp(update - 4, 0, 32) || scroll.Active != (update < 38),
                $"Seasons post-scroll source counters differ on update {update}.");
        }
        scroll.Begin(new(112.5f, 121.25f), Vector2I.Down, target, source);
        FailIf(scroll.TotalFrames != 39, "Native pre-scroll scheduling must include unique $02's one upload.");
    }
}
