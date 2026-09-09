using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateDebugObjectPreviews()
    {
        LoadValidationRoom(4, 0x04);
        var enemies = new EnemyDatabase();
        var catalog = new DebugObjectCatalog(enemies);
        var previews = new DebugObjectPreview(enemies);
        int calls = _random.Calls;
        int entityCount = _entities.Entities<Node2D>().Count;
        foreach (DebugObjectEntry entry in catalog.Enemies)
        {
            Texture2D? texture = previews.GetTexture(entry, false);
            FailIf(entry.Supported && texture is null,
                $"Spawnable enemy ${entry.Id:x2}:${entry.SubId:x2} lacks a sprite preview.");
            FailIf(!ReferenceEquals(texture, previews.GetTexture(entry, false)),
                $"Revisiting enemy ${entry.Id:x2}:${entry.SubId:x2} rebuilt its preview.");
        }
        foreach (DebugObjectEntry entry in catalog.Drops)
        {
            Texture2D? texture = previews.GetTexture(entry, true);
            FailIf(texture is null,
                $"PART_ITEM_DROP ${entry.SubId:x2} lacks a sprite preview.");
        }
        FailIf(_random.Calls != calls || _entities.Entities<Node2D>().Count != entityCount,
            "Browsing sprite previews consumed gameplay RNG or allocated room actors.");

        DebugObjectSpawnerScreen screen = _scene.DebugObjectSpawnerScreen;
        FailIf(!_debugObjectSpawner.TryOpen(), "Could not open sprite preview menu.");
        screen.MoveVertical(1);
        while (screen.Selection.Id != 0x32 || screen.Selection.SubId != 0)
            screen.MoveObject(1);
        TextureRect preview = screen.GetNode<TextureRect>("Preview");
        FailIf(preview.Texture is null || screen.GetNode<Label>("NoPreview").Visible,
            "Selecting Keese $32:$00 did not show its imported image.");

        // PART_ITEM_DROP renders the first imported OAM frame in ordinary play.
        // Compare actual sprite pixels, ignoring the gameplay renderer's canvas padding.
        screen.MoveVertical(-1);
        screen.MoveHorizontal(1);
        screen.MoveObject(1);
        FailIf(!screen.IsDrop || screen.Selection.SubId != ItemDropDatabase.Heart,
            "Preview category switch failed to select PART_ITEM_DROP $01.");
        FailIf(!_entities.TrySpawnDebugItemDrop(ItemDropDatabase.Heart, new Vector2(80, 80), out _),
            "Could not create reference heart pickup.");
        using Image actual = _entities.Entities<ItemDropEffect>().Last().CurrentTexture.GetImage();
        using Image shown = preview.Texture!.GetImage();
        using Image actualSprite = actual.GetRegion(actual.GetUsedRect());
        using Image shownSprite = shown.GetRegion(shown.GetUsedRect());
        FailIf(actualSprite.GetSize() != shownSprite.GetSize() ||
            !actualSprite.GetData().SequenceEqual(shownSprite.GetData()),
            "Heart preview pixels/palette differ from the actual PART_ITEM_DROP $01 sprite.");

        screen.MoveHorizontal(-1);
        for (int index = 0; index < catalog.Enemies.Count && preview.Texture is not null; index++)
            screen.MoveObject(1);
        FailIf(preview.Texture is not null || !screen.GetNode<Label>("NoPreview").Visible,
            "An entry without graphics retained the previous object's preview.");
        _debugObjectSpawner.Close();
        GD.Print("Validated imported spawner previews, all supported variants, pickup pixels, caching, missing-image feedback, and RNG isolation.");
    }

    private void ValidateDebugObjectSpawner()
    {
        LoadValidationRoom(4, 0x04);
        DebugObjectSpawnerScreen screen = _scene.DebugObjectSpawnerScreen;
        FailIf(!InputMap.HasAction("debug_object_spawner") ||
            screen.GetParent() != _scene.InterfaceLayer ||
            screen.Position != new Vector2(0, 16) || screen.Size != new Vector2(160, 128),
            "F4 spawner must be registered and owned by the screen-space scene.");

        _debugFlagMenu.OpenImmediatelyForValidation();
        FailIf(_debugObjectSpawner.TryOpen(), "Spawner stole another menu's pause lease.");
        _debugFlagMenu.CloseImmediatelyForValidation();
        _player.Position = new Vector2(184, 152);
        FailIf(!_debugObjectSpawner.TryOpen() || !screen.Visible ||
            !_gameplayPause.IsOwnedBy(_debugObjectSpawner) ||
            _player.IsProcessing() || _player.IsPhysicsProcessing() ||
            screen.SpawnPosition != _player.Position,
            "F4 failed to freeze Link or retain large-room coordinates beyond the viewport.");
        screen.MoveVertical(1);
        for (int index = 0; index < 256 &&
            (screen.Selection.Id != 0x32 || screen.Selection.SubId != 0); index++)
            screen.MoveHorizontal(1);
        FailIf(screen.Selection.Id != 0x32 || screen.Selection.SubId != 0,
            "Spawner catalog is missing imported ENEMY_KEESE $32:$00.");

        int before = _entities.Entities<KeeseCharacter>().Count;
        int roomCount = _entities.RoomEnemyCount;
        int randomCalls = _random.Calls;
        var input = new ApplicationInputBuffer();
        input.CaptureForValidation(["attack"], ["attack"], Vector2.Zero);
        var scheduler = new ApplicationFixedUpdateScheduler();
        scheduler.Advance(4.0 / 60.0, () =>
        {
            Input.BeginOriginalUpdate(input.ConsumeOriginalUpdate());
            try { FailIf(!_debugObjectSpawner.Update(), "Spawner lost modal update ownership."); }
            finally { Input.EndOriginalUpdate(); }
        });
        FailIf(_entities.Entities<KeeseCharacter>().Count != before + 1 ||
            _entities.Entities<KeeseCharacter>().Last().Position != new Vector2(184, 152) ||
            _entities.RoomEnemyCount != roomCount || _random.Calls != randomCalls,
            "A batched A edge must spawn one $32:$00 at room coordinates without re-parsing RNG or counting for shutters.");

        input.CaptureForValidation(["item", "attack"], ["item", "attack"], Vector2.Zero);
        Input.BeginOriginalUpdate(input.ConsumeOriginalUpdate());
        try { FailIf(!_debugObjectSpawner.Update(), "Closing must consume the original update."); }
        finally { Input.EndOriginalUpdate(); }
        FailIf(_debugObjectSpawner.IsActive || screen.Visible || _gameplayPause.IsLeased ||
            !_player.IsProcessing() || !_player.IsPhysicsProcessing() ||
            _entities.Entities<KeeseCharacter>().Count != before + 1,
            "B close failed to restore processing or leaked a simultaneous spawn edge.");

        foreach (Vector2 invalid in new[] { new Vector2(240, 80), new Vector2(80, 176),
            new Vector2(-1, 0), new Vector2(float.NaN, 0), new Vector2(1.5f, 8) })
            FailIf(_entities.TrySpawnDebugEnemy(0x32, 0, invalid, out _),
                "Debug spawn accepted padding, an out-of-room position, or fractional/invalid coordinates.");
        FailIf(_entities.TrySpawnDebugEnemy(0xff, 0xff, new Vector2(80, 80), out string error) ||
            !error.Contains("$ff:$ff", StringComparison.Ordinal) || _random.Calls != randomCalls,
            "Unsupported enemy must produce hexadecimal diagnostics without consuming RNG.");

        int allocated = 1;
        while (_entities.TrySpawnDebugEnemy(0x32, 0, new Vector2(80, 80), out error))
        {
            FailIf(++allocated > 16, "Debug enemies exceeded the shared $10-slot pool.");
        }
        FailIf(allocated < 2 || !error.Contains("slots", StringComparison.Ordinal),
            "Debug allocation did not stop at shared enemy-slot exhaustion.");

        LoadValidationRoom(4, 0x04);
        FailIf(_entities.Entities<KeeseCharacter>().Count != before ||
            !_entities.TrySpawnDebugEnemy(0x32, 0, new Vector2(80, 80), out _),
            "Room reload retained debug entities or leaked enemy-slot reservations.");

        // Exercise every advertised standalone enemy variant through its real factory.
        var catalog = new DebugObjectCatalog(new EnemyDatabase());
        foreach (DebugObjectEntry entry in catalog.Enemies.Where(entry => entry.Supported))
        {
            LoadValidationRoom(4, 0x04);
            FailIf(!_entities.TrySpawnDebugEnemy(entry.Id, entry.SubId, new Vector2(80, 80), out error),
                $"Advertised debug enemy ${entry.Id:x2}:${entry.SubId:x2} failed: {error}");
        }
        LoadValidationRoom(0, 0x11);
        randomCalls = _random.Calls;
        FailIf(_entities.TrySpawnDebugEnemy(0x28, 0, new Vector2(80, 80), out _) ||
            _random.Calls != randomCalls,
            "Wallmaster $28:$00 outside dungeon metadata must be rejected before RNG/allocation.");
        FailIf(_entities.TrySpawnDebugItemDrop(0xfe, new Vector2(80, 80), out _),
            "Unsupported PART_ITEM_DROP $fe was accepted.");
        foreach (DebugObjectEntry entry in catalog.Drops)
            FailIf(!_entities.TrySpawnDebugItemDrop(entry.SubId, new Vector2(80, 80), out error),
                $"Debug PART_ITEM_DROP ${entry.SubId:x2} failed: {error}");
        FailIf(_entities.Entities<ItemDropEffect>().Count != catalog.Drops.Count,
            "Debug drop catalog did not create the normal collectible entities.");
        GD.Print("Validated F4 object spawning, imported variants, pause/input ownership, room coordinates, shared slots, rejection, and reload cleanup.");
    }
}
