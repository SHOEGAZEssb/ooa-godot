using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateShield()
    {
        OracleSaveData save = OracleSaveData.CreateStandardGame();
        var inventory = new InventoryState(_treasures, save);
        inventory.GiveTreasure(_treasures.GetObject("TREASURE_OBJECT_SHIELD_00"));
        inventory.EquipA(InventoryState.ItemShield);
        if (inventory.ShieldLevel != 1 ||
            inventory.EquippedA != InventoryState.ItemShield ||
            save.ReadWramByte(0xc6af) != 1)
        {
            throw new InvalidOperationException(
                "TREASURE_SHIELD mode $08 did not persist/equip its level-1 item.");
        }
        ValidateShieldDisplay(inventory, level: 1, sprite: 0x93,
            palette: 0x00, textLow: 0x20);

        var world = new ValidationRingPlayerWorld();
        var player = new Player { Name = "ShieldValidationPlayer" };
        AddChild(player);
        player.Initialize(world, inventory, new Vector2(80, 80), new OracleRandom());
        if (!player.IsShieldEquipped || player.IsUsingShield ||
            player.ShieldAtlasPixelHash == 0)
        {
            throw new InvalidOperationException(
                "An equipped Wooden Shield did not select the source Link pose atlas.");
        }

        Vector2I[] directions =
            { Vector2I.Up, Vector2I.Right, Vector2I.Down, Vector2I.Left };
        Vector2[] centers =
            { new(81, 73), new(86, 80), new(79, 86), new(73, 80) };
        Vector2[] radii =
            { new(6, 1), new(1, 7), new(6, 1), new(1, 7) };
        for (int direction = 0; direction < directions.Length; direction++)
        {
            player.Face(directions[direction]);
            Rect2 bounds = player.ShieldCollisionBounds;
            if (bounds.GetCenter() != centers[direction] ||
                bounds.Size / 2.0f != radii[direction] ||
                player.ShieldGraphicsIndex != 0x68 + direction)
            {
                throw new InvalidOperationException(
                    $"ITEM_SHIELD direction {direction} lost its source center, radius, or equipped graphics.");
            }
        }

        player.Face(Vector2I.Up);
        player.UpdateShieldForValidation(attackHeld: true, itemHeld: false);
        if (!player.IsUsingShield || player.ShieldGraphicsIndex != 0x70 ||
            world.Sounds.Count(sound => sound == OracleSoundEngine.SndShield) != 1)
        {
            throw new InvalidOperationException(
                "Holding the equipped A-button shield did not select wUsingShield level 1 or SND_SHIELD.");
        }
        player.UpdateShieldForValidation(attackHeld: true, itemHeld: false);
        if (world.Sounds.Count(sound => sound == OracleSoundEngine.SndShield) != 1)
        {
            throw new InvalidOperationException(
                "ITEM_SHIELD replayed SND_SHIELD while its parent item remained held.");
        }
        player.BeginScrollingTransition(player.Position, Vector2I.Right);
        if (player.IsUsingShield || player.ShieldGraphicsIndex != 0x69)
            throw new InvalidOperationException(
                "wScrollMode $08 did not lower the shield while retaining its parent item.");
        player.FinishScrollingTransition(player.Position);
        player.UpdateShieldForValidation(attackHeld: true, itemHeld: false);
        if (!player.IsUsingShield || player.ShieldGraphicsIndex != 0x71 ||
            world.Sounds.Count(sound => sound == OracleSoundEngine.SndShield) != 1)
        {
            throw new InvalidOperationException(
                "The retained shield parent did not resume silently after scrolling.");
        }

        WarpToBushTest();
        player.Face(Vector2I.Up);
        var enemies = new EnemyDatabase();
        Vector2 shieldCenter = player.ShieldCollisionBounds.GetCenter();
        var rock = new OctorokRockProjectile();
        rock.Initialize(
            enemies.OctorokProjectile, _currentRoom, shieldCenter, angle: 0);
        rock.UpdateFrame(player); // State 0 setup-only update.
        int healthBeforeBlock = player.HealthQuarters;
        rock.UpdateFrame(player);
        if (rock.State != OctorokRockProjectile.RockState.Bouncing ||
            rock.Angle != 0x10 || rock.Counter != 0x20 || rock.ZFixed != 0 ||
            player.HealthQuarters != healthBeforeBlock ||
            world.Sounds.Count(sound => sound == OracleSoundEngine.SndClink2) != 1)
        {
            throw new InvalidOperationException(
                "A raised shield did not send PART_OCTOROK_PROJECTILE through ENEMYDMG_$34/LINKDMG_$20.");
        }

        var arrow = new EnemyArrowProjectile();
        arrow.Initialize(enemies.EnemyArrow, _currentRoom, Vector2.Zero, angle: 0);
        arrow.Position = shieldCenter;
        arrow.UpdateFrame(player);
        if (arrow.State != EnemyArrowProjectile.ArrowState.Bouncing ||
            arrow.Counter != 0x20 || player.HealthQuarters != healthBeforeBlock ||
            world.Sounds.Count(sound => sound == OracleSoundEngine.SndClink2) != 2)
        {
            throw new InvalidOperationException(
                "A raised shield did not deflect PART_ENEMY_ARROW with the shared bounce path.");
        }

        player.UpdateShieldForValidation(attackHeld: false, itemHeld: false);
        if (player.IsUsingShield || player.ShieldGraphicsIndex != 0x68)
            throw new InvalidOperationException(
                "Releasing ITEM_SHIELD did not restore the equipped-but-lowered pose.");

        var unblockedRock = new OctorokRockProjectile();
        unblockedRock.Initialize(
            enemies.OctorokProjectile, _currentRoom, player.Position, angle: 0);
        unblockedRock.UpdateFrame(player);
        unblockedRock.UpdateFrame(player);
        if (!unblockedRock.Finished || player.HealthQuarters >= healthBeforeBlock ||
            world.Sounds.Count(sound => sound == OracleSoundEngine.SndDamageLink) != 1)
        {
            throw new InvalidOperationException(
                "An equipped but lowered shield incorrectly blocked an Octorok projectile.");
        }

        inventory.GiveTreasure(_treasures.GetObject("TREASURE_OBJECT_SHIELD_01"));
        if (inventory.ShieldLevel != 2 || player.ShieldGraphicsIndex != 0x6c)
            throw new InvalidOperationException(
                "The Iron Shield upgrade did not select the level-2 equipped pose.");
        ValidateShieldDisplay(inventory, level: 2, sprite: 0x94,
            palette: 0x05, textLow: 0x21);
        player.UpdateShieldForValidation(attackHeld: true, itemHeld: false);
        if (!player.IsUsingShield || player.ShieldGraphicsIndex != 0x74)
            throw new InvalidOperationException(
                "The raised Iron Shield did not select the shared level-2/3 pose.");
        player.UpdateShieldForValidation(attackHeld: false, itemHeld: false);
        inventory.GiveTreasure(_treasures.GetObject("TREASURE_OBJECT_SHIELD_02"));
        ValidateShieldDisplay(inventory, level: 3, sprite: 0x95,
            palette: 0x04, textLow: 0x22);
        inventory.EquipB(InventoryState.ItemShield);
        player.UpdateShieldForValidation(attackHeld: false, itemHeld: true);
        if (inventory.ShieldLevel != 3 ||
            inventory.EquippedB != InventoryState.ItemShield ||
            inventory.EquippedA == InventoryState.ItemShield ||
            !player.IsUsingShield || player.ShieldGraphicsIndex != 0x74 ||
            world.Sounds.Count(sound => sound == OracleSoundEngine.SndShield) != 3)
        {
            throw new InvalidOperationException(
                "The Mirror Shield upgrade/B-button parent did not preserve the level-3 shared pose and activation.");
        }

        rock.Free();
        arrow.Free();
        unblockedRock.Free();
        player.Free();

        GD.Print("Validated ITEM_SHIELD's held-button parent, level-aware equipped/raised " +
            "Link and inventory/HUD frames, directional hitbox, sounds, and " +
            "Octorok-rock/Moblin-arrow deflection.");
    }

    private void ValidateShieldDisplay(
        InventoryState inventory,
        int level,
        int sprite,
        int palette,
        int textLow)
    {
        TreasureDatabase.DisplayRecord display =
            _treasures.GetButtonDisplay(InventoryState.ItemShield, inventory);
        TreasureDatabase.DisplayRecord parameterDisplay =
            _treasures.GetTreasureDisplay(
                TreasureDatabase.TreasureShield, level, inventory);
        if (display != parameterDisplay ||
            display.TreasureId != TreasureDatabase.TreasureShield ||
            display.LeftSprite != sprite || display.LeftPalette != palette ||
            display.RightSprite != 0 || display.RightPalette != 0 ||
            display.ExtraMode != 0 || display.TextLow != textLow ||
            inventory.LevelForInventoryDisplay(
                TreasureDatabase.TreasureShield) != level ||
            ItemIconAtlas.EquippedLeftPalette(
                display.LeftSprite, display.LeftPalette) != palette)
        {
            throw new InvalidOperationException(
                $"Shield level {level} did not select its exact inventory/equipped display row.");
        }

        Image icons1 = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_item_icons_1_spr.png");
        Image icons2 = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_item_icons_2.png");
        Image icons3 = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_item_icons_3.png");
        if (!ItemIconAtlas.Select(
                display.LeftSprite, icons1, icons2, icons3,
                out Image source, out int cell) ||
            source != icons2 || cell != sprite - 0x90 ||
            ItemIconAtlas.DecodedCellHash(source, cell) == 0)
        {
            throw new InvalidOperationException(
                $"Shield level {level} did not resolve to its source item-icons-2 cell.");
        }
    }

    private void ValidateShovel()
    {
        WarpToBushTest();
        Vector2 tileCenter = new(24, 56);
        _player.WarpTo(tileCenter + Vector2.Down * 8.0f);
        _player.Face(Vector2I.Up);
        _currentRoom.SetPositionTileAndCollision(
            tileCenter, 0x01, null, (long)_animationTicks);
        _saveData.WriteWramByte(0xc65f, 0);
        _saveData.WriteWramByte(0xc660, 0);
        _sound.ClearPlayRequestAudit();
        int debrisBefore = _entities.Entities<ShovelDebrisEffect>().Count;

        _player.StartShovelActionForValidation(Vector2.Up);
        if (!_player.IsUsingShovel || _player.ShovelFrame != 0 ||
            _player.ShovelChildActive ||
            _player.ShovelChildOffset != new Vector2(0, -8))
        {
            throw new InvalidOperationException(
                "ITEM_SHOVEL did not initialize LINK_ANIM_MODE_DIG_2 at its up-facing offset.");
        }

        _player.AdvanceShovelForValidation(3);
        if (_player.ShovelFrame != 3 || _currentRoom.GetMetatile(tileCenter) != 0x01 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDig) != 0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndClink) != 0)
        {
            throw new InvalidOperationException(
                "ITEM_SHOVEL attempted its tile collision before animation update 4.");
        }

        _player.AdvanceShovelForValidation(1);
        List<ShovelDebrisEffect> debris = _entities.Entities<ShovelDebrisEffect>();
        if (_player.ShovelFrame != 4 || !_player.ShovelChildActive ||
            _currentRoom.GetMetatile(tileCenter) != 0x1c ||
            _saveData.GashaMaturity != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDig) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndClink) != 0 ||
            debris.Count != debrisBefore + 1)
        {
            throw new InvalidOperationException(
                "The update-4 shovel child did not replace dirt, mature gasha state, " +
                "play SND_DIG, and spawn INTERAC_SHOVELDEBRIS exactly once.");
        }

        ShovelDebrisEffect chip = debris[^1];
        Vector2 debrisStart = chip.PrecisePosition;
        chip.UpdateFrame();
        if (chip.ElapsedFrames != 1 || chip.PrecisePosition != debrisStart + Vector2.Up * 0.5f ||
            chip.SpeedZ != -0x1e0 || chip.ZFixed != -0x240)
        {
            throw new InvalidOperationException(
                "INTERAC_SHOVELDEBRIS did not apply SPEED_80 and its original 8.8 Z integration.");
        }
        for (int frame = 1; frame < 14; frame++)
            chip.UpdateFrame();
        if (!chip.Finished || chip.ElapsedFrames != 14)
            throw new InvalidOperationException(
                "INTERAC_SHOVELDEBRIS did not end with its 14-update animation.");

        _player.AdvanceShovelForValidation(3);
        if (_player.ShovelFrame != 7 || !_player.ShovelChildActive)
            throw new InvalidOperationException(
                "ITEM_SHOVEL's four-update collision child ended before update 8.");
        _player.AdvanceShovelForValidation(1);
        if (_player.ShovelFrame != 8 || _player.ShovelChildActive)
            throw new InvalidOperationException(
                "ITEM_SHOVEL did not enter graphics $fc and remove its collision child on update 8.");
        _player.AdvanceShovelForValidation(14);
        if (!_player.IsUsingShovel || _player.ShovelFrame != 22)
            throw new InvalidOperationException(
                "LINK_ANIM_MODE_DIG_2 ended before update 23.");
        _player.AdvanceShovelForValidation(1);
        if (_player.IsUsingShovel)
            throw new InvalidOperationException(
                "LINK_ANIM_MODE_DIG_2 did not end on update 23.");

        _sound.ClearPlayRequestAudit();
        _player.StartShovelAction();
        _player.AdvanceShovelForValidation(4);
        if (_currentRoom.GetMetatile(tileCenter) != 0x1c ||
            _saveData.GashaMaturity != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndClink) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDig) != 0)
        {
            throw new InvalidOperationException(
                "Shoveling a non-breakable tile did not preserve state and play SND_CLINK once.");
        }
        _player.WarpTo(_player.Position);

        // Tile $cb sets effect bits 7/6. Its break tables add 50 maturity and
        // current-room flag bit 7 before ITEM_SHOVEL adds its own one point.
        _currentRoom.SetPositionTileAndCollision(
            tileCenter, 0xcb, null, (long)_animationTicks);
        _saveData.WriteWramByte(0xc65f, 0);
        _saveData.WriteWramByte(0xc660, 0);
        _saveData.SetRoomFlag(_activeGroup, _currentRoom.Id, OracleSaveData.RoomFlag80, false);
        _sound.ClearPlayRequestAudit();
        _player.StartShovelAction();
        _player.AdvanceShovelForValidation(4);
        if (_currentRoom.GetMetatile(tileCenter) != 0xd2 ||
            _saveData.GashaMaturity != 51 ||
            !_saveData.HasRoomFlag(_activeGroup, _currentRoom.Id, OracleSaveData.RoomFlag80) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDig) != 1)
        {
            throw new InvalidOperationException(
                "Shovel tile $cb did not apply its room flag, +50/+1 maturity, " +
                "SND_SOLVEPUZZLE, and SND_DIG table effects.");
        }
        _player.WarpTo(_player.Position);

        Vector2[] expectedOffsets =
        {
            new(0, -8), new(6, 4), new(0, 7), new(-7, 4)
        };
        Vector2I[] directions =
        {
            Vector2I.Up, Vector2I.Right, Vector2I.Down, Vector2I.Left
        };
        for (int index = 0; index < directions.Length; index++)
        {
            _player.Face(directions[index]);
            if (_player.ShovelChildOffset != expectedOffsets[index])
                throw new InvalidOperationException(
                    $"ITEM_SHOVEL direction {index} lost its signed Y/X child offset.");
        }

        GD.Print("Validated ITEM_SHOVEL timing, invisible child offsets, tile effects, " +
            "gasha maturity, debris, and clink/success sounds.");
    }

    private void ValidateSeedSatchel()
    {
        var database = new SeedSatchelDatabase();
        SeedSatchelDatabase.SeedRecord record = database.Ember;
        if (record.ParentItem != 0x19 || record.SeedItem != 0x20 ||
            record.TreasureId != 0x20 || record.Sprite != "spr_common_items" ||
            record.TileBase != 0x12 || record.Palette != 2 ||
            record.Collision != 0x9b || record.CollisionRadiusY != 4 ||
            record.CollisionRadiusX != 4 || record.Damage != 0xfe ||
            record.InitialZ != -2 || record.SpeedZ != -0x20 ||
            record.Gravity != 0x1c || record.SpeedRaw != 0x1e ||
            record.LinkFrames != 8 || record.FlameSprite != "spr_common_sprites" ||
            record.FlameTileBase != 0x06 || record.FlameOamFlags != 0x0a ||
            record.FlamePalette != 2 || record.FlameCounter != 0x3a ||
            record.LandingSound != 0x52 || record.FlameSound != 0x72)
        {
            throw new InvalidOperationException(
                "The imported ITEM_SEED_SATCHEL/ITEM_EMBER_SEED record diverged from its source tables.");
        }

        Vector2I[] directions =
            { Vector2I.Up, Vector2I.Right, Vector2I.Down, Vector2I.Left };
        Vector2I[] offsets =
            { new(0, -4), new(4, 1), new(0, 5), new(-5, 1) };
        for (int index = 0; index < directions.Length; index++)
        {
            if (record.Offset(directions[index]) != offsets[index])
                throw new InvalidOperationException(
                    $"Satchel direction {index} lost its signed Y/X child offset.");
        }

        OracleSaveData save = OracleSaveData.CreateStandardGame();
        var inventory = new InventoryState(_treasures, save);
        int grantInventoryChanges = 0;
        int grantSaveChanges = 0;
        inventory.Changed += () => grantInventoryChanges++;
        save.Changed += () => grantSaveChanges++;
        inventory.GiveTreasure(new TreasureDatabase.TreasureObjectRecord(
            "VALIDATION_SATCHEL", 0x19, 0, 1, 0xff, 0, string.Empty));
        if (inventory.EmberSeeds != 0x20 || grantInventoryChanges != 1 ||
            grantSaveChanges != 1)
        {
            throw new InvalidOperationException(
                "The Seed Satchel did not expose its initial BCD 20 Ember Seeds in the grant transaction.");
        }
        OracleGraphicsCache.AnimationDefinition emberAnimation =
            OracleGraphicsCache.GetAnimationDefinition(record.Animation);
        string[] expectedOam =
        {
            "8,4,0,0",
            "8,4,0,0",
            "8,0,2,0;8,8,2,32",
            "8,0,4,7;8,8,4,39",
            "8,0,4,0;8,8,4,32"
        };
        if (emberAnimation.LoopStart != 2 || emberAnimation.Frames.Length != 5 ||
            !emberAnimation.Frames.Select(frame => frame.EncodedOam)
                .SequenceEqual(expectedOam))
        {
            throw new InvalidOperationException(
                "itemAnimation1e818 did not resolve its item20OamDataPointers compositions.");
        }
        int inventoryChanges = 0;
        int saveChanges = 0;
        inventory.Changed += () => inventoryChanges++;
        save.Changed += () => saveChanges++;
        if (!inventory.TryConsumeSelectedSatchelSeed(out int seedItem) ||
            seedItem != 0x20 || inventory.EmberSeeds != 0x19 ||
            save.ReadWramByte(0xc6b9) != 0x19 ||
            inventoryChanges != 1 || saveChanges != 1)
        {
            throw new InvalidOperationException(
                "decNumActiveSeeds did not decrement/persist the selected Satchel count as packed BCD once.");
        }
        for (int count = 0; count < 19; count++)
            inventory.TryConsumeSelectedSatchelSeed(out _);
        if (inventory.EmberSeeds != 0 || inventory.TryConsumeSelectedSatchelSeed(out _))
            throw new InvalidOperationException(
                "The Satchel consumed a seed at zero or failed to reach BCD $00 from $20.");

        WarpToBushTest();
        Vector2 linkPosition = new(80, 80);
        // State 0 consumes one update. Eight moving/gravity updates put this
        // up-facing throw at (80,70), where its flame expires.
        Vector2 flamePoint = new(80, 70);
        _currentRoom.SetPositionTileAndCollision(
            flamePoint, 0xc5, null, (long)_animationTicks);
        var sounds = new List<int>();
        var hazards = new List<OracleRoomData.HazardType>();
        var emberSpawns = new List<RoomEntitySpawn>();
        int tileChanges = 0;
        var ember = new EmberSeedEffect();
        ember.Initialize(
            record, _currentRoom, new BreakableTileDatabase(), linkPosition,
            Vector2I.Up, sounds.Add, (_, hazard) => hazards.Add(hazard),
            () => tileChanges++, () => (long)_animationTicks,
            _ => null, _saveData, _activeGroup);
        Image flameSource = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_common_sprites.png");
        Texture2D expectedFlameTexture = NpcCharacter.BuildOamTextureUncachedForValidation(
            flameSource, emberAnimation.Frames[2].EncodedOam,
            record.FlameTileBase, record.FlamePalette);
        ulong expectedFlameHash = OracleGraphicsCache.PixelHash(
            expectedFlameTexture.GetImage());
        expectedFlameTexture.Dispose();
        if (ember.FlameTextureHashForValidation(2) != expectedFlameHash)
        {
            throw new InvalidOperationException(
                "ITEM_EMBER_SEED ignition did not switch OAM flag `$0a to " +
                "GFXH_COMMON_SPRITES bank-1 flame tiles `$06/`$08/`$0a.");
        }
        ember.UpdateFrame(emberSpawns);
        if (ember.State != EmberSeedEffect.EmberState.Flying ||
            ember.ElapsedFrames != 1 || ember.ZFixed != -0x200 ||
            ember.PrecisePosition != linkPosition + (Vector2)record.UpOffset)
        {
            throw new InvalidOperationException(
                "ITEM_EMBER_SEED state 0 did not preserve the setup-only update and initial Z/offset.");
        }
        for (int frame = 0; frame < 7; frame++)
            ember.UpdateFrame(emberSpawns);
        if (ember.State != EmberSeedEffect.EmberState.Flying ||
            ember.ZFixed != -0x94 || ember.SpeedZ != 0xa4)
        {
            throw new InvalidOperationException(
                "ITEM_EMBER_SEED did not retain its SPEED_c0/$1c 8.8 flight arc before landing.");
        }
        ember.UpdateFrame(emberSpawns);
        if (ember.State != EmberSeedEffect.EmberState.Burning ||
            ember.ElapsedFrames != 9 || ember.ZFixed != 0 ||
            ember.PrecisePosition != flamePoint || ember.FlameCounter != 0x3a ||
            ember.AnimationFrame != 1 ||
            !sounds.SequenceEqual(new[] { 0x52, 0x72 }) || hazards.Count != 0)
        {
            throw new InvalidOperationException(
                "The Satchel Ember Seed did not land on update 9 and initialize its flame/sounds exactly.");
        }
        ember.UpdateFrame(emberSpawns);
        if (ember.FlameCounter != 0x39 || ember.AnimationFrame != 1)
            throw new InvalidOperationException(
                "emberSeedBurn did not decrement before advancing itemAnimation1e818.");
        for (int frame = 1; frame < 58; frame++)
            ember.UpdateFrame(emberSpawns);
        if (!ember.Finished || ember.ElapsedFrames != 67 ||
            _currentRoom.GetMetatile(flamePoint) != 0x3a || tileChanges != 1)
        {
            throw new InvalidOperationException(
                "The Ember flame did not apply BREAKABLETILESOURCE_EMBER_SEED on counter $3a expiry.");
        }
        ember.Free();

        var standardSubstitutions = new StandardTileSubstitutionDatabase();
        if (standardSubstitutions.RecordCount != 50)
        {
            throw new InvalidOperationException(
                "The imported standard tile-substitution table did not retain all 50 Ages rows.");
        }

        OracleSaveData watchedTreeSave = OracleSaveData.CreateStandardGame();
        var watchedTreeRooms = new RoomSession(
            0, 0x48, () => 0, () => { }, watchedTreeSave);
        OracleRoomData watchedTreeRoom = watchedTreeRooms.CurrentRoom;
        Vector2 watchedTreePoint = new(0x88, 0x68);
        var watcherDatabase = new RoomTileChangeWatcherDatabase();
        RoomTileChangeWatcherDatabase.Record[] watcherRecords = watcherDatabase
            .GetRoomRecords(0, 0x48).ToArray();
        if (watcherDatabase.RecordCount != 8 || watcherRecords is not
            [{ Position: 0x68, RoomFlag: 0x02, Order: 1 }] ||
            watchedTreeRoom.GetPackedPosition(watchedTreePoint) != 0x68 ||
            watchedTreeRoom.GetMetatile(watchedTreePoint) != 0xce)
        {
            throw new InvalidOperationException(
                "Room 0:48 did not retain its imported $dc:$08 watcher and burnable tree $68/$ce.");
        }

        var watcherRoot = new Node();
        AddChild(watcherRoot);
        var watcherManager = new RoomEntityManager(
            watcherRoot, new NpcDatabase(), new EnemyDatabase(), watchedTreeSave);
        watcherManager.LoadRoom(0, watchedTreeRoom);
        if (!watcherManager.Entities<Node2D>().Any(
                node => node.Name == "TileChangeWatcher_1"))
        {
            throw new InvalidOperationException(
                "RoomEntityFactory did not instantiate room 0:48's imported $dc:$08 watcher.");
        }
        var watchedTreeSpawns = new List<RoomEntitySpawn>();
        watcherManager.Update(1.0 / 60.0, _player);
        if (watchedTreeSave.HasRoomFlag(0, 0x48, 0x02))
            throw new InvalidOperationException(
                "Room 0:48's $dc:$08 watcher set flag $02 during its snapshot state.");

        var watchedTreeSeed = new EmberSeedEffect();
        watchedTreeSeed.Initialize(
            record, watchedTreeRoom, new BreakableTileDatabase(),
            watchedTreePoint + new Vector2(0, 10), Vector2I.Up,
            _ => { }, (_, _) => { }, () => { }, () => 0,
            _ => null, watchedTreeSave, 0);
        for (int frame = 0; frame < 67; frame++)
            watchedTreeSeed.UpdateFrame(watchedTreeSpawns);
        if (!watchedTreeSeed.Finished ||
            watchedTreeRoom.GetMetatile(watchedTreePoint) != 0x3a ||
            watchedTreeSave.HasRoomFlag(0, 0x48, 0x02))
        {
            throw new InvalidOperationException(
                "Room 0:48's tree did not burn to $3a before its watcher update.");
        }
        watcherManager.Update(1.0 / 60.0, _player);
        if (!watchedTreeSave.HasRoomFlag(0, 0x48, 0x02) ||
            watcherManager.Entities<Node2D>().Any(
                node => node.Name == "TileChangeWatcher_1"))
        {
            throw new InvalidOperationException(
                "Room 0:48's $dc:$08 watcher did not set room flag $02 after tile $68 changed.");
        }
        watchedTreeSeed.Free();
        watcherManager.Clear();
        RemoveChild(watcherRoot);
        watcherRoot.Free();
        watchedTreeRooms.Load(0, 0x47);
        if (watchedTreeRooms.Load(0, 0x48).GetMetatile(watchedTreePoint) != 0x3a)
            throw new InvalidOperationException(
                "Room 0:48's single-tile change did not preserve burnt tree $68/$3a on re-entry.");
        if (!OracleSaveData.TryDeserialize(
                watchedTreeSave.Serialize(), out OracleSaveData? reloadedTreeSave) ||
            new RoomSession(0, 0x48, () => 0, () => { }, reloadedTreeSave!)
                .CurrentRoom.GetMetatile(watchedTreePoint) != 0x3a)
        {
            throw new InvalidOperationException(
                "Room 0:48's burnt tree did not remain removed after save serialization and reload.");
        }

        OracleSaveData persistentSave = OracleSaveData.CreateStandardGame();
        var persistentRooms = new RoomSession(
            0, 0x8a, () => 0, () => { }, persistentSave);
        int burnGroup = -1;
        int burnRoomId = -1;
        Vector2 burnPoint = Vector2.Zero;
        for (int group = 0; group <= 3 && burnRoomId < 0; group++)
        for (int roomId = 0; roomId <= 0xff && burnRoomId < 0; roomId++)
        {
            if (!persistentRooms.World.HasRoom(group, roomId))
                continue;
            OracleRoomData candidate = persistentRooms.World.LoadRoom(group, roomId);
            if (candidate.ActiveCollisions != 0)
                continue;
            for (int y = 0; y < candidate.HeightInTiles && burnRoomId < 0; y++)
            for (int x = 0; x < candidate.WidthInTiles; x++)
            {
                Vector2 point = new(
                    x * OracleRoomData.MetatileSize + 8,
                    y * OracleRoomData.MetatileSize + 8);
                if (candidate.GetMetatile(point) != 0xcf)
                    continue;
                burnGroup = group;
                burnRoomId = roomId;
                burnPoint = point;
                break;
            }
        }
        if (burnRoomId < 0)
            throw new InvalidOperationException("Could not find an overworld burnable tree tile $cf.");

        OracleRoomData burnRoom = persistentRooms.Load(burnGroup, burnRoomId);
        int maturityBeforeBurn = persistentSave.GashaMaturity;
        var burnSounds = new List<int>();
        var burnSpawns = new List<RoomEntitySpawn>();
        var burningTreeSeed = new EmberSeedEffect();
        burningTreeSeed.Initialize(
            record, burnRoom, new BreakableTileDatabase(),
            burnPoint + new Vector2(0, 10), Vector2I.Up,
            burnSounds.Add, (_, _) => { }, () => { }, () => 0,
            _ => null, persistentSave, burnGroup);
        for (int frame = 0; frame < 67; frame++)
            burningTreeSeed.UpdateFrame(burnSpawns);
        if (!burningTreeSeed.Finished || burnRoom.GetMetatile(burnPoint) != 0xdc ||
            !persistentSave.HasRoomFlag(burnGroup, burnRoomId, OracleSaveData.RoomFlag80) ||
            persistentSave.GashaMaturity != maturityBeforeBurn + 30 ||
            burnSounds.Count(sound => sound == OracleSoundEngine.SndSolvePuzzle) != 1)
        {
            throw new InvalidOperationException(
                "Burning overworld tree $cf did not set room flag $80, add 30 maturity, " +
                "play SND_SOLVEPUZZLE, and become $dc.");
        }
        burningTreeSeed.Free();

        int otherRoomId = Enumerable.Range(0, 0x100).First(roomId =>
            roomId != burnRoomId && persistentRooms.World.HasRoom(burnGroup, roomId));
        persistentRooms.Load(burnGroup, otherRoomId);
        OracleRoomData sameSessionReload = persistentRooms.Load(burnGroup, burnRoomId);
        if (sameSessionReload.GetMetatile(burnPoint) != 0xdc)
        {
            throw new InvalidOperationException(
                "ROOMFLAG $80 did not retain standard substitution $cf->$dc after live re-entry.");
        }

        if (!OracleSaveData.TryDeserialize(
                persistentSave.Serialize(), out OracleSaveData? restoredPersistentSave))
        {
            throw new InvalidOperationException(
                "The burnable-tree room flag did not survive save-image serialization.");
        }
        var reloadedRooms = new RoomSession(
            burnGroup, burnRoomId, () => 0, () => { }, restoredPersistentSave!);
        if (reloadedRooms.CurrentRoom.GetMetatile(burnPoint) != 0xdc)
        {
            throw new InvalidOperationException(
                "ROOMFLAG $80 did not reapply standard substitution $cf->$dc after saved re-entry.");
        }

        if (_inventory.EmberSeeds == 0)
        {
            _inventory.GiveTreasure(new TreasureDatabase.TreasureObjectRecord(
                "VALIDATION_EMBER_SEED", 0x20, 0, 1, 0xff, 0, string.Empty));
        }
        _inventory.SelectSatchelSeeds(0);
        int beforeAmount = _inventory.EmberSeeds;
        int beforeEntities = _entities.Entities<EmberSeedEffect>().Count;
        _player.WarpTo(linkPosition);
        _player.StartSeedSatchelActionForValidation(Vector2.Right);
        int expectedAmount = ((beforeAmount >> 4) * 10 + (beforeAmount & 0x0f)) - 1;
        expectedAmount = ((expectedAmount / 10) << 4) | expectedAmount % 10;
        if (!_player.IsUsingSeedSatchel || _player.SeedSatchelFrame != 0 ||
            _player.FacingVector != Vector2I.Right ||
            _inventory.EmberSeeds != expectedAmount ||
            _entities.Entities<EmberSeedEffect>().Count != beforeEntities + 1)
        {
            throw new InvalidOperationException(
                "ITEM_SEED_SATCHEL did not allocate its child, decrement BCD ammo, and lock Link.");
        }

        var hudQuantity = _hud.QuantityOverlayForValidation(
            InventoryState.ItemSeedSatchel, isA: false);
        var inventoryQuantity = _inventoryScreen.QuantityOverlayForValidation(
            InventoryState.ItemSeedSatchel);
        int expectedTens = 0x10 + ((expectedAmount >> 4) & 0x0f);
        int expectedOnes = 0x10 + (expectedAmount & 0x0f);
        if (hudQuantity is not { } hud || hud.TensTile != expectedTens ||
            hud.OnesTile != expectedOnes || hud.Position != new Vector2(16, 8) ||
            inventoryQuantity is not { } menu || menu.TensTile != expectedTens ||
            menu.OnesTile != expectedOnes || menu.Attributes != 0x07)
        {
            throw new InvalidOperationException(
                "drawTreasureExtraTiles mode $01 did not expose both selected-seed BCD digits.");
        }

        _player.AdvanceSeedSatchelForValidation(7);
        if (!_player.IsUsingSeedSatchel || _player.SeedSatchelFrame != 7)
            throw new InvalidOperationException(
                "LINK_ANIM_MODE_21 ended before its eighth update.");
        _player.AdvanceSeedSatchelForValidation(1);
        if (_player.IsUsingSeedSatchel)
            throw new InvalidOperationException(
                "LINK_ANIM_MODE_21 did not end on update 8.");

        int activeSeedAmount = _inventory.EmberSeeds;
        int activeSeedCount = _entities.Entities<EmberSeedEffect>().Count;
        _player.StartSeedSatchelActionForValidation(Vector2.Left);
        if (_player.IsUsingSeedSatchel ||
            _inventory.EmberSeeds != activeSeedAmount ||
            _entities.Entities<EmberSeedEffect>().Count != activeSeedCount)
        {
            throw new InvalidOperationException(
                "ITEM_SEED_SATCHEL allocated or consumed ammo while its first seed was still active.");
        }

        GD.Print("Validated ITEM_SEED_SATCHEL immediate BCD-20 grant/persistence, quantity overlays, " +
            "distinct inventory/equipped icon sheets and equipped palette transform, " +
            "four offsets, Link pose, one-active-seed cap, Ember flight/Z, " +
            "fixed-bank-1 flame OAM/sounds, break effects, direct ROOMFLAG-$80 tree ignition, " +
            "and room 0:48's watcher-backed permanent tree removal across re-entry/save reload.");
    }

    private void ValidateSwordBush()
    {
        OracleRandom.Result ExpectedNextRandom()
        {
            var replay = new OracleRandom();
            for (int call = 0; call < _random.Calls; call++)
                replay.Next();
            return replay.Next();
        }

        int SoundFor(OracleRandom.Result result) => (result.Value & 0x07) switch
        {
            0 or 3 or 4 or 6 or 7 => OracleSoundEngine.SndSwordSlash,
            1 or 5 => OracleSoundEngine.SndUnknown5,
            _ => OracleSoundEngine.SndBoomerang
        };

        WarpToBushTest();
        Vector2 bushPoint = new(24, 56);
        if (_currentRoom.GetMetatile(bushPoint) != 0xc5)
            throw new InvalidOperationException("Expected overworld bush $c5 in room 69 at $31.");
        Vector2 objectPosition = _player.Position;
        _sound.ClearPlayRequestAudit();
        int randomCalls = _random.Calls;
        OracleRandom.Result expectedRandom = ExpectedNextRandom();
        _player.StartSwordAttack();
        int slashRequests = _sound.PlayRequestsFor(OracleSoundEngine.SndSwordSlash) +
            _sound.PlayRequestsFor(OracleSoundEngine.SndUnknown5) +
            _sound.PlayRequestsFor(OracleSoundEngine.SndBoomerang);
        if (slashRequests != 1 || _player.SwordState != Player.SwordActionState.Swing ||
            _player.SwordStateFrame != 0 || _player.SwordArcIndex != 0 ||
            _random.Calls != randomCalls + 1 || _random.LastResult != expectedRandom ||
            _sound.PlayRequestsFor(SoundFor(expectedRandom)) != 1)
        {
            throw new InvalidOperationException(
                "Starting ITEM_SWORD did not select one entry from the original 8-sound table " +
                "from shared RNG and initialize LINK_ANIM_MODE_22 at sword arc $00.");
        }
        _player.AdvanceSwordForValidation(2, buttonHeld: false);
        _sound.ClearPlayRequestAudit();
        randomCalls = _random.Calls;
        _player.StartSwordAttack();
        if (_player.SwordCanRestart || _player.SwordStateFrame != 2 ||
            _random.Calls != randomCalls ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSwordSlash) +
            _sound.PlayRequestsFor(OracleSoundEngine.SndUnknown5) +
            _sound.PlayRequestsFor(OracleSoundEngine.SndBoomerang) != 0)
        {
            throw new InvalidOperationException(
                "The protected first three sword updates accepted an equal-priority restart.");
        }
        _player.AdvanceSwordForValidation(1, buttonHeld: false);
        if (!_player.SwordCanRestart)
            throw new InvalidOperationException(
                "The sword did not become restartable when animation parameter `$02 cleared enabled bit 7.");
        randomCalls = _random.Calls;
        expectedRandom = ExpectedNextRandom();
        _player.StartSwordAttackForValidation(Vector2.Right);
        slashRequests = _sound.PlayRequestsFor(OracleSoundEngine.SndSwordSlash) +
            _sound.PlayRequestsFor(OracleSoundEngine.SndUnknown5) +
            _sound.PlayRequestsFor(OracleSoundEngine.SndBoomerang);
        if (slashRequests != 1 || _player.SwordStateFrame != 0 ||
            _player.SwordArcIndex != 1 || _player.FacingVector != Vector2I.Right ||
            _player.SwordCanRestart || _random.Calls != randomCalls + 1 ||
            _random.LastResult != expectedRandom ||
            _sound.PlayRequestsFor(SoundFor(expectedRandom)) != 1)
        {
            throw new InvalidOperationException(
                "An equal-priority sword press did not consume shared RNG, restart, and " +
                "retarget the single swing after update 3.");
        }
        _player.AdvanceSwordForValidation(3, buttonHeld: false);
        _sound.ClearPlayRequestAudit();
        randomCalls = _random.Calls;
        expectedRandom = ExpectedNextRandom();
        _player.StartSwordAttackForValidation(Vector2.Up);
        slashRequests = _sound.PlayRequestsFor(OracleSoundEngine.SndSwordSlash) +
            _sound.PlayRequestsFor(OracleSoundEngine.SndUnknown5) +
            _sound.PlayRequestsFor(OracleSoundEngine.SndBoomerang);
        if (slashRequests != 1 || _player.SwordStateFrame != 0 ||
            _player.SwordArcIndex != 0 || _player.FacingVector != Vector2I.Up ||
            _player.SwordCanRestart || _random.Calls != randomCalls + 1 ||
            _random.LastResult != expectedRandom ||
            _sound.PlayRequestsFor(SoundFor(expectedRandom)) != 1)
        {
            throw new InvalidOperationException(
                "Spammed sword input did not consume shared RNG and retarget a subsequent swing upward.");
        }
        if (_player.AttackSpriteOrigin != new Vector2(-8, -8))
            throw new InvalidOperationException(
                $"Sword frame $ac displaced Link from the standard OAM origin: {_player.AttackSpriteOrigin}.");
        if (_player.SwordSpritePosition != new Vector2(16, -4))
            throw new InvalidOperationException(
                $"Sword arc phase $00 did not include the child item's -2 Z draw offset: {_player.SwordSpritePosition}.");
        _player._Process(7.0 / 60.0);
        if (_player.Position != objectPosition)
            throw new InvalidOperationException("Swinging the sword changed Link's object position.");
        if (_player.AttackSpriteOrigin != new Vector2(-8, -11))
            throw new InvalidOperationException(
                $"Sword frame $b4 did not apply only its original OAM $08 pose offset: {_player.AttackSpriteOrigin}.");
        if (_player.SwordSpritePosition != new Vector2(-4, -19))
            throw new InvalidOperationException(
                $"Sword arc phase $08 did not include the child item's -2 Z draw offset: {_player.SwordSpritePosition}.");
        if (_currentRoom.GetMetatile(bushPoint) != 0x3a)
            throw new InvalidOperationException("The level-1 sword did not replace bush $c5 with ground $3a.");
        if (_currentRoom.IsSolid(bushPoint))
            throw new InvalidOperationException("The cut bush's replacement tile remained solid.");
        if (_sound.PlayRequestsFor(OracleSoundEngine.SndCutGrass) != 1)
            throw new InvalidOperationException("INTERAC_GRASSDEBRIS did not request SND_CUTGRASS once.");

        // Complete LINK_ANIM_MODE_22 while preserving the initiating button.
        // State 6 must re-enable movement but keep turning disabled and expose
        // the fourth normal swordArcData row continuously while charging.
        _player.AdvanceSwordForValidation(9, buttonHeld: true);
        if (_player.SwordState != Player.SwordActionState.Swing ||
            _player.SwordStateFrame != 16)
            throw new InvalidOperationException("The sword swing ended before its 17th update.");
        _player.AdvanceSwordForValidation(1, buttonHeld: true);
        if (_player.SwordState != Player.SwordActionState.Held ||
            !_player.SwordAllowsMovement || !_player.SwordCanRestart || _player.SwordArcIndex != 12 ||
            _player.SwordSpritePosition != new Vector2(-4, -12))
        {
            throw new InvalidOperationException(
                "Holding the sword button did not enter the movable ITEMCOLLISION_SWORD_HELD state " +
                "with the original up-facing arc $0c.");
        }
        if (Player.GetHeldSwordBodyAnimationFrameForValidation(
                Player.SwordActionState.Held, walking: true, walkTime: 0.0f) != 0 ||
            Player.GetHeldSwordBodyAnimationFrameForValidation(
                Player.SwordActionState.Held, walking: true, walkTime: 0.10f) != 1 ||
            Player.GetHeldSwordBodyAnimationFrameForValidation(
                Player.SwordActionState.Charged, walking: true, walkTime: 0.20f) != 0 ||
            Player.GetHeldSwordBodyAnimationFrameForValidation(
                Player.SwordActionState.Held, walking: false, walkTime: 0.10f) != 0 ||
            Player.GetHeldSwordBodyAnimationFrameForValidation(
                Player.SwordActionState.Swing, walking: true, walkTime: 0.10f) != -1)
        {
            throw new InvalidOperationException(
                "Held/charged sword state did not select Link's ordinary standing/walking body.");
        }
        if (Player.GetSwordSpritePositionForValidation(13) != new Vector2(12, 0) ||
            Player.GetSwordSpritePositionForValidation(15) != new Vector2(-12, 0))
        {
            throw new InvalidOperationException(
                "Held horizontal sword sprites did not apply the child item's -2 Z draw offset.");
        }

        _player.AdvanceSwordForValidation(40, buttonHeld: true);
        if (_player.SwordState != Player.SwordActionState.Held ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndChargeSword) != 0)
            throw new InvalidOperationException("Sword counter `$28 charged without the original underflow update.");
        _player.AdvanceSwordForValidation(1, buttonHeld: true);
        if (_player.SwordState != Player.SwordActionState.Charged ||
            _player.SwordCanRestart || _player.SwordUsesChargedPalette ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndChargeSword) != 1)
            throw new InvalidOperationException("The 41st held update did not enter the charged state with SND_CHARGE_SWORD.");
        _player.AdvanceSwordForValidation(3, buttonHeld: true);
        if (_player.SwordUsesChargedPalette)
            throw new InvalidOperationException("The charged sword selected palette 5 before counter bit 2 was set.");
        _player.AdvanceSwordForValidation(1, buttonHeld: true);
        if (!_player.SwordUsesChargedPalette)
            throw new InvalidOperationException("The charged sword did not select palette 5 when counter bit 2 became set.");

        _player.AdvanceSwordForValidation(1, buttonHeld: false);
        if (_player.SwordState != Player.SwordActionState.Spin ||
            _player.SwordStateFrame != 0 || _player.SwordAllowsMovement ||
            _player.SwordArcIndex != 16 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSwordSpin) != 1)
        {
            throw new InvalidOperationException(
                "Releasing a charged up-facing sword did not begin the immobilized arc `$10 spin with SND_SWORDSPIN.");
        }
        _player.AdvanceSwordForValidation(2, buttonHeld: false);
        if (_player.SwordArcIndex != 16)
            throw new InvalidOperationException("Swordspin arc `$10 did not retain its original 3-update duration.");
        _player.AdvanceSwordForValidation(1, buttonHeld: false);
        if (_player.SwordArcIndex != 17)
            throw new InvalidOperationException("Swordspin did not enter diagonal arc `$11 on update 3.");
        _player.AdvanceSwordForValidation(2, buttonHeld: false);
        if (_player.SwordArcIndex != 18)
            throw new InvalidOperationException("Swordspin did not enter right-facing arc `$12 on update 5.");
        _player.AdvanceSwordForValidation(17, buttonHeld: false);
        if (_player.SwordState != Player.SwordActionState.Spin ||
            _player.SwordStateFrame != 22 || _player.SwordArcIndex != 16)
            throw new InvalidOperationException("Swordspin did not retain its wrapped arc through update 22.");
        _player.AdvanceSwordForValidation(1, buttonHeld: false);
        if (_player.IsAttacking)
            throw new InvalidOperationException("Swordspin did not end on its original 23rd update.");

        // A held sword pressed into a full wall switches to LINK_ANIM_MODE_1f,
        // clears weapon collision for 12 updates, and emits the ordinary clink.
        _currentRoom.SetPositionTileAndCollision(
            bushPoint, 0x3a, 0x0f, (long)_animationTicks);
        _player.WarpTo(new Vector2(bushPoint.X, 66));
        _player.Face(Vector2I.Up);
        _player.StartSwordAttack();
        _player.AdvanceSwordForValidation(17, buttonHeld: true);
        _sound.ClearPlayRequestAudit();
        _combat.ClearClinkEffectAudit();
        _player.AdvanceSwordForValidation(1, buttonHeld: true, movementInput: Vector2.Up);
        if (_player.SwordState != Player.SwordActionState.Poke ||
            !_player.SwordCanRestart || _player.GetSwordHitbox().Size != Vector2.Zero ||
            _player.AttackSpriteOrigin != new Vector2(-8, -11) ||
            _player.SwordSpritePosition != new Vector2(-4, -19) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndClink) != 1)
        {
            throw new InvalidOperationException(
                "Held-sword wall pressure did not enter the collision-disabled 12-update poke and play SND_CLINK.");
        }
        ClinkEffect? ordinaryClink = _combat.LastClinkEffect;
        Vector2 expectedClinkPosition = _player.Position + new Vector2(0, -14);
        if (_combat.ClinkEffectsSpawned != 1 || ordinaryClink is null ||
            ordinaryClink.Position != expectedClinkPosition || !ordinaryClink.Flickers ||
            ordinaryClink.DurationFrames != 8 || ordinaryClink.AnimationFrame != 0 ||
            !ordinaryClink.EffectVisible)
        {
            throw new InvalidOperationException(
                "Ordinary wall pressure did not spawn flickering INTERAC_CLINK at the up-facing `$f2/$00 probe.");
        }
        ordinaryClink.AdvanceForValidation(1.0 / 60.0);
        if (ordinaryClink.EffectVisible || ordinaryClink.AnimationFrame != 0)
            throw new InvalidOperationException("INTERAC_CLINK did not flicker during its first 4-update frame.");
        ordinaryClink.AdvanceForValidation(3.0 / 60.0);
        if (!ordinaryClink.EffectVisible || ordinaryClink.AnimationFrame != 1)
            throw new InvalidOperationException("INTERAC_CLINK did not enter its second OAM frame after 4 updates.");
        _player.AdvanceSwordForValidation(11, buttonHeld: true);
        if (_player.SwordState != Player.SwordActionState.Poke ||
            _player.SwordStateFrame != 11)
            throw new InvalidOperationException("LINK_ANIM_MODE_1f ended before update 12.");
        _player.AdvanceSwordForValidation(1, buttonHeld: true);
        if (_player.SwordState != Player.SwordActionState.Held ||
            _player.SwordArcIndex != 12)
            throw new InvalidOperationException("A wall poke did not reinitialize the held sword after update 12.");
        _player.AdvanceSwordForValidation(1, buttonHeld: false);
        if (_player.IsAttacking)
            throw new InvalidOperationException("Releasing an uncharged held sword did not clear the parent item.");

        // Bombable wall tiles bypass the poke-only ordinary clink condition.
        _currentRoom.SetPositionTileAndCollision(
            bushPoint, 0xc1, null, (long)_animationTicks);
        _sound.ClearPlayRequestAudit();
        _combat.ClearClinkEffectAudit();
        _player.StartSwordAttack();
        _player.AdvanceSwordForValidation(6, buttonHeld: false);
        ClinkEffect? bombableClink = _combat.LastClinkEffect;
        if (_sound.PlayRequestsFor(OracleSoundEngine.SndClink2) != 1 ||
            _combat.ClinkEffectsSpawned != 1 || bombableClink is null ||
            bombableClink.Position != expectedClinkPosition || bombableClink.Flickers ||
            !bombableClink.EffectVisible)
        {
            throw new InvalidOperationException(
                "Bombable overworld tile `$c1 did not play SND_CLINK2 and spawn non-flickering INTERAC_CLINK.");
        }
        _player.AdvanceSwordForValidation(11, buttonHeld: false);
        _currentRoom.SetPositionTileAndCollision(
            bushPoint, 0x3a, null, (long)_animationTicks);

        (int RadiusY, int RadiusX, int OffsetY, int OffsetX)[] expectedArcs =
        {
            (9, 6, -2, 16), (6, 9, -14, 0), (9, 6, 0, -15), (6, 9, -14, 0),
            (7, 7, -11, 13), (7, 7, -11, 13), (7, 7, 17, -13), (7, 7, -11, -13),
            (9, 6, -17, -4), (6, 9, 2, 19), (9, 6, 21, 3), (6, 9, 2, -19),
            (9, 6, -10, -4), (4, 9, 2, 12), (9, 6, 16, 3), (6, 9, 2, -12),
            (9, 9, -17, -4), (9, 9, -14, 16), (9, 9, 2, 19), (9, 9, 18, 16),
            (9, 9, 21, 3), (9, 9, 17, -13), (9, 9, 2, -19), (9, 9, -11, -13)
        };
        Vector2 auditPosition = new(80, 64);
        for (int index = 0; index < expectedArcs.Length; index++)
        {
            var arc = expectedArcs[index];
            Rect2 expected = new(
                auditPosition + new Vector2(
                    arc.OffsetX - arc.RadiusX,
                    arc.OffsetY - arc.RadiusY),
                new Vector2(arc.RadiusX * 2, arc.RadiusY * 2));
            Rect2 actual = Player.GetSwordHitboxForValidation(auditPosition, index);
            if (actual != expected)
                throw new InvalidOperationException(
                    $"swordArcData row `${index:x2} mismatch: expected {expected}, got {actual}.");
        }

        // wScrollMode $08 freezes initialized item objects. The held sword
        // parent and its locked facing must therefore survive both ends of a
        // scrolling transition without charging or observing button release.
        _player.Face(Vector2I.Up);
        _player.StartSwordAttack();
        _player.AdvanceSwordForValidation(17, buttonHeld: true);
        _transitions.BeginScroll(_player, Vector2I.Right, 0x6a);
        if (!_transitions.ScrollActive ||
            _player.SwordState != Player.SwordActionState.Held ||
            _player.FacingVector != Vector2I.Up || _player.SwordArcIndex != 12)
        {
            throw new InvalidOperationException(
                "Scrolling right did not preserve the up-facing held sword parent item.");
        }
        _player._Process(1.0);
        if (_player.SwordState != Player.SwordActionState.Held ||
            _player.SwordStateFrame != 0 || _player.FacingVector != Vector2I.Up)
        {
            throw new InvalidOperationException(
                "wScrollMode $08 did not freeze the held sword for the scrolling transition.");
        }
        _transitions.UpdateScroll(1.0);
        if (_transitions.ScrollActive ||
            _player.SwordState != Player.SwordActionState.Held ||
            _player.FacingVector != Vector2I.Up || _player.SwordArcIndex != 12)
        {
            throw new InvalidOperationException(
                "Finishing the scrolling transition cleared or redirected the held sword.");
        }
        _player.AdvanceSwordForValidation(1, buttonHeld: false);

        GD.Print(
            "Validated ITEM_SWORD's 17-update swing/3-update directional restart gate, " +
            "held collision/movement and scrolling-transition persistence, " +
            "41-update charge, " +
            "held/charged standing/walking body, child-item Z/layer rendering, charged palette cadence, " +
            "12-update wall poke/clinks with 8-update INTERAC_CLINK sprites, 23-update swordspin, " +
            "shared-RNG slash sounds, blocked-restart RNG preservation, grass break, " +
            "and all 24 swordArcData hitboxes.");
    }

    private void ValidateHealth()
    {
        _dialogue.Close();
        _player.RefillHealth();
        _statusBar.SynchronizeHealth();

        if (_player.HealthQuarters != 12 || _hud.HealthQuarters != 12 ||
            _hud.MaxHealthQuarters != _player.MaxHealthQuarters)
            throw new InvalidOperationException("Expected Link and the HUD to start with three full hearts.");

        _player.ApplyDamage(1);
        if (_player.HealthQuarters != 11 || _hud.HealthQuarters != 12)
            throw new InvalidOperationException("Direct quarter-heart damage changed the HUD before its update.");
        _statusBar.Update(1.0 / 60.0);
        if (_hud.HealthQuarters != 11)
            throw new InvalidOperationException("Displayed damage did not subtract one quarter per update.");

        _player.Heal(1);
        if (_player.HealthQuarters != 12 || _hud.HealthQuarters != 11)
            throw new InvalidOperationException("Direct healing changed the HUD before its divisor-4 update.");
        for (int update = 0; update < 4 && _hud.HealthQuarters != 12; update++)
            _statusBar.Update(1.0 / 60.0);
        if (_hud.HealthQuarters != 12)
            throw new InvalidOperationException("Displayed healing did not add a quarter on a divisor-4 update.");

        _activeGroup = 0;
        ClearDeactivatedWarp();
        _currentRoom = _world.LoadRoom(_activeGroup, 0x03);
        _roomView.SetRoom(_currentRoom.Texture);
        Vector2 safe = new(56, 8);
        _player.WarpTo(safe);
        _player.WarpTo(new Vector2(8, 24), recordSafe: false);

        ValidateDrowningSequence(safe, OracleRoomData.HazardType.Lava);
        if (_player.HealthQuarters != 10 || _hud.HealthQuarters != 12)
            throw new InvalidOperationException(
                "Lava hazard changed displayed health before updateStatusBar_body.");
        _statusBar.Update(2.0 / 60.0);
        if (_hud.HealthQuarters != 10)
            throw new InvalidOperationException(
                "Lava hazard did not synchronize its delayed half-heart damage to the HUD.");

        GD.Print("Validated quarter-heart health, divisor-4 healing display/SND_GAINHEART cadence, " +
            "per-update damage display, and delayed half-heart terrain damage.");
    }

    private void ValidateAnimations()
    {
        OracleRoomData water = _world.LoadRoom(0, 0xb8);
        ulong waterStart = water.GetAnimationChecksum(0);
        bool waterChanged = false;
        for (int tick = 1; tick <= 120 && !waterChanged; tick++)
            waterChanged = water.GetAnimationChecksum(tick) != waterStart;

        OracleRoomData lava = _world.LoadRoom(0, 0x03);
        ulong lavaStart = lava.GetAnimationChecksum(0);
        bool lavaChanged = false;
        for (int tick = 1; tick <= 60 && !lavaChanged; tick++)
            lavaChanged = lava.GetAnimationChecksum(tick) != lavaStart;

        if (!waterChanged || !lavaChanged)
            throw new InvalidOperationException(
                $"Expected animated water and lava frames; water={waterChanged}, lava={lavaChanged}.");

        OracleRoomData waterfall = _world.LoadRoom(0, 0x45);
        const int settledWaterfallTick = 32;
        if (waterfall.AnimationGroup != 0 ||
            !waterfall.HasAnimationOverride(4, settledWaterfallTick) ||
            !waterfall.HasAnimationOverride(236, settledWaterfallTick) ||
            !waterfall.HasAnimationOverride(238, settledWaterfallTick))
        {
            throw new InvalidOperationException(
                "Waterfall animation did not preserve its three alternating VRAM destination writes.");
        }

        // Room textures are cached independently, but the original engine has
        // one shared animated-tile VRAM state. Prime 0:56 with a stale phase,
        // then verify that beginning the 0:55 -> 0:56 scroll synchronizes it
        // to the outgoing room before both textures are shown together.
        OracleRoomData target = _world.LoadRoom(0, 0x56);
        target.UpdateAnimation(0);
        int staleTargetSignature = target.CurrentAnimationSignature;
        _activeGroup = 0;
        _currentRoom = _world.LoadRoom(0, 0x55);
        OracleRoomData source = _currentRoom;
        if (!Mathf.IsZeroApprox((float)_animationTicks))
            throw new InvalidOperationException("Changing animation groups did not reset their shared clock.");
        _animationTicks = 13.0;
        source.UpdateAnimation((long)_animationTicks);
        if (source.CurrentAnimationSignature == staleTargetSignature)
            throw new InvalidOperationException("Animation phase-lock validation chose indistinguishable ticks.");

        _roomView.SetRoom(source.Texture);
        _entities.LoadRoom(_activeGroup, source);
        _player.WarpTo(new Vector2(source.Width + 2.0f, source.Height / 2.0f));
        CheckRoomExit(_player);
        if (!IsTransitioning || _activeGroup != 0 || _currentRoom.Id != 0x56)
            throw new InvalidOperationException("Room 0:55 did not begin its rightward scroll into 0:56.");
        if (source.AnimationGroup != _currentRoom.AnimationGroup ||
            source.CurrentAnimationSignature != _currentRoom.CurrentAnimationSignature)
        {
            throw new InvalidOperationException(
                "Outgoing and incoming water/waterfall tiles began the scroll in different phases.");
        }

        ValidateLinkScrollsForOneTransitionFrame();
        if (source.CurrentAnimationSignature != _currentRoom.CurrentAnimationSignature)
            throw new InvalidOperationException("Animated-tile phases diverged during the frozen scroll.");
        FinishActiveScrollingTransitionForValidation();

        GD.Print("Validated disassembly-driven water and lava animation plus persistent " +
            "three-range waterfall VRAM updates and 0:55 -> 0:56 phase locking.");
    }
}
