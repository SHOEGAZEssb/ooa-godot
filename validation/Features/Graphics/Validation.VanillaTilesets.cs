using Godot;
using System;
using System.Security.Cryptography;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateVanillaTilesets()
    {
        // Independently decoded from the clean-US ROM with upstream common.py,
        // gfxHeaderTable $01:$69da and uniqueGfxHeaderTable $04:$5b28.
        foreach ((int id, string expected) in new (int, string)[]
        {
            (0x00, "27da3d1d955ac06f0f50d04d23ed175b340b35393717de18c2cb5cba684d7485"),
            (0x13, "c79aa552ef0e039c500595b0a450e0d240091a09c02e4ed6a857d5e5c6835fc8"),
            (0x22, "d911423e53f891e264f1e866ee107e70f0511553a8355fba4031762fa3b0b1b3"),
            (0x3e, "605379a906c68d2c9b508a09d67140a4693052a484e341b47b9a7b7ce4f77aa2"),
            (0x5f, "7240fb3ecf2e7f6b827ea7201ad5bb78fe38e69547f0807547f33d7bac9d0473")
        })
        {
            using Image atlas = Image.LoadFromFile($"res://assets/oracle/gfx/gfx_tileset{id:x2}.png");
            FailIf(atlas.GetWidth() != 128 || atlas.GetHeight() != 128,
                $"Vanilla tileset ${id:x2} atlas dimensions changed.");
            byte[] pixels = new byte[128 * 128];
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                    pixels[y * 128 + x] = (byte)Mathf.RoundToInt(atlas.GetPixel(x, y).R * 255);
            FailIf(Convert.ToHexString(SHA256.HashData(pixels)).ToLowerInvariant() != expected,
                $"Vanilla tileset ${id:x2} main/unique graphics composition differs from clean US.");
        }

        // Hashes of master source layout files, not generated outputs. Layout
        // $20 aliases $1b in tilesetHeaders.s; tileset $34 selects that alias.
        foreach ((string path, string expected) in new (string, string)[]
        {
            ("tilesetMappings16", "098aaeadc14b66a6d82c1fe8d86ef268a80262494eb82289c15d665f3773babf"),
            ("tilesetMappings26", "cd16fb264d27c5a9e2d82663f8043173549522ebcfde8e5f76fdaffc66243c01"),
            ("tilesetMappings34", "7db23e795269ee5b4e855ac80fa877a2b384cb74bca9ddad58a47a9d8d2f6acf"),
            ("tilesetCollisions34", "7e67a6f0e5ab68ea93bbfb702e57e6dddcc13f8fa02cce6e5b23925d4e287124")
        })
            FailIf(Convert.ToHexString(SHA256.HashData(Godot.FileAccess.GetFileAsBytes(
                $"res://assets/oracle/layouts/{path}.bin"))).ToLowerInvariant() != expected,
                $"Vanilla shared layout {path} differs from source.");

        var world = new OracleWorldData();
        foreach ((int group, int roomId, bool recolor) in new (int, int, bool)[]
        {
            (1, 0x39, true), (1, 0x38, false), (0, 0x39, false),
            (4, 0x04, false), (1, 0x39, true), (1, 0x38, false)
        })
        {
            OracleRoomData room = world.LoadRoom(group, roomId);
            byte[] original = Godot.FileAccess.GetFileAsBytes(
                $"res://assets/oracle/layouts/tilesetMappings{room.TilesetId:x2}.bin");
            for (int tile = 0; tile < 256; tile++)
            {
                room.SetPositionTileAndCollision(new Vector2(8, 8), (byte)tile, 0, 0);
                for (int quarter = 0; quarter < 4; quarter++)
                {
                    byte expected = original[tile * 8 + 4 + quarter];
                    if (recolor && tile >= 0x40 && tile < 0x80 && (expected & 7) == 6)
                        expected &= 0xf8;
                    FailIf(room.GetBackgroundAttributeForValidation(quarter % 2, quarter / 2) != expected,
                        $"Cliff palette boundary/cache changed in room {group:x1}:{roomId:x2}, tile ${tile:x2}.");
                }
            }
        }
        // roomLayoutData.s aliases: $040e->$0400, $05ff->$0500,
        // and $05ec->$05d0. Hashes come from those upstream binary files.
        foreach ((string room, string expected) in new (string, string)[]
        {
            ("040e", "86d2cf5b090f43ee54d8f7c1dcf746a853951191457ff6dac96269a9d24860b9"),
            ("05ff", "86d2cf5b090f43ee54d8f7c1dcf746a853951191457ff6dac96269a9d24860b9"),
            ("05ec", "2a7abeb6ceb8993e016f5815a4e69618904ae70d2695965da7e9b9227d8eb5bf")
        })
            FailIf(Convert.ToHexString(SHA256.HashData(Godot.FileAccess.GetFileAsBytes(
                $"res://assets/oracle/rooms/large/room{room}.bin"))).ToLowerInvariant() != expected,
                $"Vanilla room ${room} alias differs from source.");

        var maps = new DungeonMapDatabase();
        foreach (int dungeon in new[] { 0x00, 0x09 })
        {
            FailIf(!maps.TryGetNeighbor(dungeon, 0x03, Vector2I.Down, out int neighbor) || neighbor != 0x04,
                $"Vanilla dungeon ${dungeon:x2} shared layout lost room $03->$04 adjacency.");
            FailIf(!maps.GetDungeon(dungeon).TryGetRoom(0x04, out DungeonCell cell) ||
                cell.Floor != 0 || cell.X != 3 || cell.Y != 4,
                $"Vanilla dungeon ${dungeon:x2} alias lost room $04's source map cell.");
        }
        GD.Print("Validated vanilla graphics uploads, shared layout aliases and room-specific cliff palettes.");
    }
}
