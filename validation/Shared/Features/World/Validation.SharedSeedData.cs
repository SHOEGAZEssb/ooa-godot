
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedSeedData(FrontendValidationRoot root)
    {
        var rooms = root._gameplaySession!.Rooms;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var inventory = root._scene.Player.Inventory;
        OracleGame game = rooms.SaveData.Game;
        bool ages = game == OracleGame.Ages;
        string assetRoot = rooms.AssetRoot;
        var database = new SeedSatchelDatabase(assetRoot);
        var native = ValidationRom.LoadCleanUs(game);
        int Word(int at) => native.Span[at] | native.Span[at + 1] << 8;
        int bank7 = 7 * 0x4000;
        int data = 0x3f * 0x4000 + (ages ? 0x23a5 : 0x23a3);
        int attrs = bank7 + (ages ? 0x25b3 : 0x2351);
        int animations = bank7 + (ages ? 0x2663 : 0x2401);
        int effects = bank7 + (ages ? 0x0ea0 : 0x0e4a);
        int oamBank = (ages ? 0x13 : 0x12) * 0x4000;
        var breakables = new BreakableTileDatabase(assetRoot);
        foreach (int id in new[] { 0x20, 0x21, 0x22, 0x23, 0x24 })
        {
            FailIf(!database.TryGet(id, out var record), $"{game}: native seed ITEM ${id:x2} is missing.");
            int at = (id - 0x20) * 4;
            int landAt = id == 0x21 ? 28 : id == 0x23 ? 20 : at;
            FailIf(record.AssetRoot != assetRoot || record.ParentItem != 0x19 || record.TreasureId != id ||
                record.Sprite != "spr_common_items" || record.FlameSprite != "spr_common_sprites" ||
                record.TileBase != native.Span[data + id * 3 + 1] || record.Palette != native.Span[data + id * 3 + 2] ||
                record.Collision != native.Span[attrs + id * 4] || record.Damage != native.Span[attrs + id * 4 + 2] ||
                record.CollisionRadiusY != native.Span[attrs + id * 4 + 1] >> 4 ||
                record.CollisionRadiusX != (native.Span[attrs + id * 4 + 1] & 15),
                $"{game}: ITEM ${id:x2} must retain its clean-ROM graphics/attributes and selected pack.");
            FailIf(record.FlameOamFlags != native.Span[effects + landAt] || record.FlameTileBase != native.Span[effects + landAt + 1] ||
                record.FlameCounter != native.Span[effects + landAt + 2] || record.FlameSound != native.Span[effects + landAt + 3] ||
                record.CollisionEffectOamFlags != native.Span[effects + at] || record.CollisionEffectTileBase != native.Span[effects + at + 1] ||
                record.CollisionEffectCounter != native.Span[effects + at + 2] || record.CollisionEffectSound != native.Span[effects + at + 3],
                $"{game}: ITEM ${id:x2} must retain separate native landed/collision effects.");
            FailIf(record.InitialBounces != (ages ? 3 : 0) || record.GaleOutdoorsMask != (ages ? 1 : 0xff) ||
                record.SpeedZ != -32 || record.Gravity != 28 ||
                record.InitialZ != (id == 0x23 ? -8 : -2) || record.SpeedRaw != (id == 0x23 ? 0 : 0x1e) ||
                record.LinkFrames != (id == 0x22 ? 0 : 8), $"{game}: ITEM ${id:x2} source initialization differs.");
            int animation = bank7 + (Word(animations + id * 2) & 0x3fff);
            int oam = bank7 + (Word(animations + 0x60 + id * 2) & 0x3fff);
            int count = id == 0x23 ? 3 : 5;
            int loop = id == 0x20 ? 2 : id == 0x23 ? 1 : id is 0x22 or 0x24 ? 4 : 0;
            CheckAnimation(record.Animation, Word(animation), oam, count, loop, $"ITEM ${id:x2} flight");
            CheckAnimation(record.EffectAnimation, Word(animation + (id == 0x21 ? 2 : 0)), oam,
                id == 0x21 ? 2 : count, id == 0x21 ? 0 : loop, $"ITEM ${id:x2} effect");
            for (int direction = 0; direction < 4; direction++)
            foreach (int linkZ in new[] { 0, -0x330 })
            {
                Vector2I facing = (Vector2I)OracleObjectMath.StrictCardinalVector(direction * 8);
                var seed = new EmberSeedEffect();
                try
                {
                    seed.Initialize(record, rooms.CurrentRoom, breakables, new(72, 64), facing,
                        _ => { }, (_, _) => { }, () => { }, () => 0, _ => null, rooms.SaveData,
                        rooms.ActiveGroup, mysteryEffect: 2, linkZFixed: linkZ);
                    Vector2 offset = id == 0x23 ? Vector2.Zero : direction switch
                    { 0 => new(0, -4), 1 => new(4, 1), 2 => new(0, 5), _ => new(-5, 1) };
                    FailIf(seed.Position != new Vector2(72, 64) + offset || seed.ZFixed != (record.InitialZ << 8) + linkZ ||
                        seed.SpeedZ != -32 || seed.BouncesRemaining != (ages ? 3 : 0),
                        $"{game}: ITEM ${id:x2}, direction ${direction:x2}, Link Z={linkZ}: native Satchel construction differs.");
                }
                finally { seed.Free(); }
            }
        }
        var pegasus = PegasusSeedRecord.Load(assetRoot);
        FailIf(database.RejectsUnderwater != ages || database.RejectsRaft != ages,
            $"{game}: Satchel's native Ages-only raft/underwater rejection differs.");
        FailIf(pegasus.Counter != 0x03c0 || pegasus.NormalSpeed != 0x3c || pegasus.GrassSpeed != 0x2d ||
            pegasus.StairsSpeed != 0x1e || pegasus.DustTile != 0x16 || pegasus.DustFlags != 0x0b || pegasus.StepSound != 0xa3,
            $"{game}: native Pegasus timer, terrain speeds and dust attributes differ.");
        int dustAnimations = bank7 + (Word(animations + 0x1a * 2) & 0x3fff);
        int dustOam = bank7 + (Word(animations + 0x60 + 0x1a * 2) & 0x3fff);
        for (int index = 0; index < 4; index++)
            CheckAnimation(pegasus.Animations[index], Word(dustAnimations + index * 2), dustOam,
                index == 0 ? 4 : 1, 0, $"Pegasus dust ${index:x2}");
        int counter = entities.RuntimeState.ReadField("wPegasusSeedCounter");
        _ = new SeedSatchelController(inventory, entities, database, rooms);
        FailIf(entities.RuntimeState.ReadField("wPegasusSeedCounter") != counter,
            $"{game}: composing Satchel must not activate Pegasus.");
        var foreign = new InventoryState(TreasureDatabase.For(assetRoot), OracleSaveData.CreateStandardGame(game));
        bool rejected = false;
        try { _ = new SeedSatchelController(foreign, entities, database, rooms); }
        catch (ArgumentException) { rejected = true; }
        FailIf(!rejected, $"{game}: Satchel admitted an independent inventory/runtime owner.");
        rejected = false;
        var invalidSeed = new EmberSeedEffect();
        try
        {
            var foreignRecord = database.Ember with { AssetRoot = ages ? SeasonsGameContent.AssetRoot : AgesGameContent.AssetRoot };
            invalidSeed.Initialize(foreignRecord, rooms.CurrentRoom, breakables, new(72, 64), Vector2I.Up,
                _ => { }, (_, _) => { }, () => { }, () => 0, _ => null, rooms.SaveData, rooms.ActiveGroup);
        }
        catch (ArgumentException) { rejected = true; }
        finally { invalidSeed.Free(); }
        FailIf(!rejected, $"{game}: physical seed admitted another game's native record.");
        if (!ages)
            FailIf(Godot.FileAccess.FileExists(assetRoot + "metadata/seed_shooter.tsv"),
                "Seasons Satchel/physical seed construction must succeed without importing Ages' ITEM_SHOOTER parent.");

        void CheckAnimation(string encoded, int pointer, int oam, int count, int loop, string context)
        {
            var imported = OracleGraphicsCache.GetAnimationDefinition(encoded);
            FailIf(imported.Frames.Length != count || imported.LoopStart != loop,
                $"{game}: {context} frame extent/loop differs from its native stream.");
            int start = bank7 + (pointer & 0x3fff);
            for (int index = 0; index < count; index++)
            {
                int at = start + index * 3;
                int cells = oamBank + (Word(oam + native.Span[at + 1]) & 0x3fff);
                var expected = new List<string>();
                for (int cell = 0; cell < native.Span[cells]; cell++)
                {
                    int part = cells + 1 + cell * 4;
                    expected.Add($"{native.Span[part]},{native.Span[part + 1]},{native.Span[part + 2]},{native.Span[part + 3]}");
                }
                var frame = imported.Frames[index];
                FailIf(frame.Duration != native.Span[at] || frame.Parameter != native.Span[at + 2] || frame.EncodedOam != string.Join(';', expected),
                    $"{game}: {context}, frame ${index:x2}: clean-ROM duration/parameter/OAM differs.");
            }
        }
    }
}
