using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateBombs()
    {
        BombRecord record = new BombDatabase().Data;
        AnimationDefinition fuse =
            OracleGraphicsCache.GetAnimationDefinition(record.FuseAnimation);
        AnimationDefinition explosion =
            OracleGraphicsCache.GetAnimationDefinition(
                record.ExplosionAnimation);
        FailIf(
            record.Item != InventoryState.ItemBomb ||
            record.TreasureId != TreasureDatabase.TreasureBombs ||
            record.Sprite != "spr_common_items" ||
            record.TileBase != 0x10 || record.Palette != 0x04 ||
            record.Collision != 0x18 ||
            record.RadiusY != 4 || record.RadiusX != 4 ||
            record.BaseDamage != 4 ||
            record.ExplosionSprite != "spr_common_sprites" ||
            record.ExplosionTileBase != 0x0c ||
            record.ExplosionPalette != 2 ||
            record.PickupSound != OracleSoundEngine.SndPickup ||
            record.ThrowSound != OracleSoundEngine.SndThrow ||
            record.LandingSound != OracleSoundEngine.SndBombLand ||
            record.ExplosionSound != OracleSoundEngine.SndExplosion ||
            record.Gravity != 0x1c ||
            record.InitialSpeedZ != -0xf0 ||
            record.SpeedRaw != 0x3c ||
            record.TossSpeedRaw != 0x64 ||
            record.ConveyorSpeedRaw != 0x14 ||
            record.LiftLowFrames != 7 ||
            record.LiftMidFrames != 4 ||
            record.LiftHighFrames != 2 ||
            record.ThrowFrames != 8,
            "The imported ITEM_BOMB record diverged from itemData, " +
            "itemAttributes, bombsBraceletParent, or weight-0 throwing data.");
        FailIf(
            !record.EdgeOffsets.SequenceEqual(
                [new Vector2I(0, -3), new Vector2I(3, 0),
                 new Vector2I(0, 7), new Vector2I(-3, 0)]) ||
            record.ReducedBounceSpeed(0) != 0 ||
            record.ReducedBounceSpeed(0x3c) != 0x1e ||
            record.ReducedBounceSpeed(0x64) != 0x32 ||
            !record.BreakProbes.SequenceEqual(
            [
                new BombBreakProbe(-8, new Vector2I(-13, -13)),
                new BombBreakProbe(-8, new Vector2I(-13, 12)),
                new BombBreakProbe(-8, new Vector2I(12, 12)),
                new BombBreakProbe(-8, new Vector2I(12, -13)),
                new BombBreakProbe(-12, new Vector2I(-13, 0)),
                new BombBreakProbe(-12, new Vector2I(0, 12)),
                new BombBreakProbe(-12, new Vector2I(12, 0)),
                new BombBreakProbe(-12, new Vector2I(0, -13)),
                new BombBreakProbe(-14, Vector2I.Zero)
            ]) ||
            fuse.Frames.Length != 11 ||
            fuse.Frames.Take(10).Sum(frame => frame.Duration) != 116 ||
            fuse.Frames[^1].Parameter != 1 ||
            explosion.Frames.Length != 7 ||
            !explosion.Frames.Select(frame => frame.Parameter)
                .SequenceEqual([6, 6, 6, 10, 15, 0x40, 0xff]),
            "ITEM_BOMB edge probes, bounce mapping, 116-update fuse, or " +
            "explosion-radius animation diverged from the imported source.");

        OracleSaveData save = OracleSaveData.CreateStandardGame();
        var inventory = new InventoryState(_treasures, save);
        inventory.GiveTreasure(TreasureDatabase.TreasureBombs, 0x10);
        var rooms = new RoomSession(
            0, 0x06, () => 0, () => { }, save);
        var root = new Node { Name = "BombValidationRoot" };
        AddChild(root);
        using var fixture = RoomEntityValidationFixture.ForRoot(
            root,
            new()
            {
                SaveData = save,
                Inventory = inventory,
                Treasures = _treasures,
                Rooms = rooms
            });
        RoomEntityManager manager = fixture.Manager;
        manager.LoadRoom(0, rooms.CurrentRoom);
        var sounds = new List<int>();
        manager.SoundRequested += sounds.Add;
        var player = new Player { Name = "BombValidationPlayer" };
        root.AddChild(player);
        player.Initialize(
            new ValidationRingPlayerWorld(),
            inventory,
            new Vector2(80, 80),
            new OracleRandom());
        var controller = new BombController(
            inventory,
            manager,
            rooms,
            sounds.Add,
            static () => false,
            new BombDatabase());

        int inventoryChanges = 0;
        int saveChanges = 0;
        inventory.Changed += () => inventoryChanges++;
        save.Changed += () => saveChanges++;
        FailIf(
            !controller.TryUse(player) ||
            controller.State != BombParentState.Lifting ||
            controller.Bomb is not { State: BombState.Held } ||
            manager.ActiveBombCount != 1 ||
            inventory.Bombs != 0x09 ||
            inventoryChanges != 1 || saveChanges != 1 ||
            sounds.Count(sound => sound == OracleSoundEngine.SndPickup) != 1,
            "ITEM_BOMB did not allocate before one packed-BCD decrement and " +
            "begin the shared lift parent.");
        BombEffect allocated = controller.Bomb ??
            throw new InvalidOperationException(
                "Bomb allocation validation lost its actor.");
        manager.Update(1.0 / 60.0, player);
        for (int update = 1; update <= 13; update++)
        {
            bool movementLocked =
                controller.Update(
                    player,
                    Vector2.Zero,
                    itemButtonJustPressed: false);
            manager.Update(1.0 / 60.0, player);
            FailIf(
                update < 13 && !movementLocked,
                $"ITEM_BOMB lift released movement before update {update}.");
        }
        FailIf(
            controller.State != BombParentState.Holding ||
            !player.IsCarryingObject ||
            player.BraceletLiftCollisionsDisabled ||
            allocated.State != BombState.Held,
            "ITEM_BOMB did not enter its carried state after 7/4/2 lift updates.");

        player.Face(Vector2I.Right);
        FailIf(
            !controller.Update(
                player,
                Vector2.Zero,
                itemButtonJustPressed: true) ||
            controller.State != BombParentState.Throwing ||
            allocated.State != BombState.Thrown ||
            allocated.ThrowDirection != Vector2I.Zero ||
            allocated.SpeedZ != 0 ||
            allocated.SpeedRaw != 0 ||
            player.IsCarryingObject ||
            sounds.Count(sound => sound == OracleSoundEngine.SndThrow) != 1,
            "ITEM_BOMB did not preserve wLinkAngle=$ff as an in-place " +
            "weight-0 drop when no direction was held.");
        manager.Update(1.0 / 60.0, player);
        for (int update = 1; update <= record.ThrowFrames; update++)
        {
            controller.Update(
                player,
                Vector2.Zero,
                itemButtonJustPressed: false);
        }
        Vector2 thrownPosition = allocated.Position;
        player.SetScriptedPosition(new Vector2(16, 16));
        int bombsBeforeCapProbe = inventory.Bombs;
        FailIf(
            controller.State != BombParentState.Idle ||
            controller.TryUse(player) ||
            inventory.Bombs != bombsBeforeCapProbe ||
            manager.ActiveBombCount != 1,
            "The normal ITEM_BOMB object cap did not reject a distant second " +
            "allocation without consuming ammo.");

        player.SetScriptedPosition(thrownPosition);
        FailIf(
            !controller.TryUse(player) ||
            controller.Bomb != allocated ||
            inventory.Bombs != bombsBeforeCapProbe ||
            manager.ActiveBombCount != 1,
            "ITEM_BOMB did not prioritize re-picking a touching live Bomb " +
            "without consuming another count.");
        for (int update = 1; update <= 13; update++)
        {
            controller.Update(
                player,
                Vector2.Zero,
                itemButtonJustPressed: false);
            manager.Update(1.0 / 60.0, player);
        }
        FailIf(
            !controller.Update(
                player,
                Vector2.Left,
                itemButtonJustPressed: true) ||
            allocated.ThrowDirection != Vector2I.Left ||
            allocated.SpeedZ != record.InitialSpeedZ ||
            allocated.SpeedRaw != record.SpeedRaw ||
            player.FacingVector != Vector2I.Left,
            "A held direction did not select Link's current input-facing " +
            "weight-0 Bomb throw.");
        controller.Interrupt(player, discard: true);
        manager.Update(1.0 / 60.0, player);
        FailIf(
            manager.ActiveBombCount != 0,
            "Discarded ITEM_BOMB remained allocated in the room-entity set.");

        InventoryState Wearing(RingId ring)
        {
            OracleSaveData ringSave = OracleSaveData.CreateStandardGame();
            ringSave.WriteWramByte(0xc6cc, 1);
            ringSave.WriteWramByte(0xc6c6, (byte)ring);
            var wearing = new InventoryState(_treasures, ringSave);
            FailIf(
                !wearing.EquipRingAt(0),
                $"Could not equip ring ${(int)ring:x2} for Bomb validation.");
            return wearing;
        }
        FailIf(
            RingEffects.BombObjectLimit(inventory) != 1 ||
            RingEffects.BombObjectLimit(Wearing(RingId.Bombers)) != 2,
            "BOMBERS_RING no longer raises the active ITEM_BOMB cap from one to two.");

        OracleRoomData effectRoom = rooms.CurrentRoom;
        Vector2 explosionPoint = new(80, 80);
        player.SetScriptedPosition(explosionPoint);
        FailIf(
            effectRoom.ActiveCollisions is not (0 or 4),
            "Bomb validation room no longer uses a breakable collision set.");
        effectRoom.SetPositionTileAndCollision(
            explosionPoint, 0xc5, null, 0);
        var effectSpawns = new List<RoomEntitySpawn>();
        var effectSounds = new List<int>();
        var effect = new BombEffect();
        effect.Initialize(
            record,
            effectRoom,
            new BreakableTileDatabase(),
            player,
            0,
            bomb => bomb.ReleaseExploding(player, Vector2I.Zero),
            effectSounds.Add,
            (_, _, _) => { },
            () => { },
            () => 0,
            _ => null,
            save,
            null);
        effect.SetHeldOffset(player, Vector2I.Zero);
        effect.UpdateFrame(player, effectSpawns);
        for (int update = 0; update < 116; update++)
            effect.UpdateFrame(player, effectSpawns);
        FailIf(
            effect.State != BombState.Exploding ||
            effect.ElapsedFrames != 117 ||
            effect.AnimationFrame != 0 ||
            effect.ExplosionRadius != 6 ||
            effect.Damage != 4 ||
            !effectSounds.SequenceEqual([OracleSoundEngine.SndExplosion]),
            "ITEM_BOMB did not initialize its radius-6 explosion on fuse update 116.");
        int healthBeforeExplosion = player.HealthQuarters;
        effect.UpdateFrame(player, effectSpawns);
        FailIf(
            effectRoom.GetMetatile(explosionPoint) != 0x3a ||
            effect.BreakProbe != 7 ||
            player.HealthQuarters != healthBeforeExplosion - 4 ||
            !effectSpawns.Any(spawn => spawn is GrassDebrisSpawn),
            "ITEM_BOMB's first explosion update did not damage Link and apply " +
            "the center-first BREAKABLETILESOURCE_BOMB probe.");
        while (!effect.Finished && effect.ElapsedFrames < 200)
            effect.UpdateFrame(player, effectSpawns);
        FailIf(
            !effect.Finished || effect.ElapsedFrames != 152,
            "ITEM_BOMB explosion did not delete on parameter $ff after 35 updates.");
        effect.Free();

        InventoryState peaceInventory = Wearing(RingId.Peace);
        var peacePlayer = new Player { Name = "PeaceBombValidationPlayer" };
        peacePlayer.Initialize(
            new ValidationRingPlayerWorld(),
            peaceInventory,
            explosionPoint,
            new OracleRandom());
        var peaceBomb = new BombEffect();
        peaceBomb.Initialize(
            record, effectRoom, new BreakableTileDatabase(),
            peacePlayer, 0, _ => { }, _ => { }, (_, _, _) => { },
            () => { }, () => 0, _ => null, saveData: null,
            linkedRoomNeighbor: null);
        peaceBomb.UpdateFrame(peacePlayer, effectSpawns);
        for (int update = 0; update < 200; update++)
            peaceBomb.UpdateFrame(peacePlayer, effectSpawns);
        FailIf(
            peaceBomb.State != BombState.Held ||
            peaceBomb.AnimationFrame != 0 ||
            peaceBomb.AnimationCounter != 0x50,
            "PEACE_RING did not reset a held Bomb's fuse to animation 0 each update.");
        peaceBomb.Free();
        peacePlayer.Free();

        InventoryState blastInventory = Wearing(RingId.Blast);
        var blastPlayer = new Player { Name = "BlastBombValidationPlayer" };
        blastPlayer.Initialize(
            new ValidationRingPlayerWorld(),
            blastInventory,
            explosionPoint,
            new OracleRandom());
        var blastBomb = new BombEffect();
        blastBomb.Initialize(
            record, effectRoom, new BreakableTileDatabase(),
            blastPlayer, 0,
            bomb => bomb.ReleaseExploding(blastPlayer, Vector2I.Zero),
            _ => { }, (_, _, _) => { }, () => { }, () => 0,
            _ => null, saveData: null, linkedRoomNeighbor: null);
        int blastHealth = blastPlayer.HealthQuarters;
        for (int update = 0; update < 118; update++)
            blastBomb.UpdateFrame(blastPlayer, effectSpawns);
        FailIf(
            blastBomb.State != BombState.Exploding ||
            blastBomb.Damage != 6 ||
            blastPlayer.HealthQuarters != blastHealth - 6,
            "BLAST_RING did not raise live Bomb and own-Bomb damage from 4 to 6.");
        blastBomb.Free();
        blastPlayer.Free();

        InventoryState bombproofInventory = Wearing(RingId.Bombproof);
        var bombproofPlayer = new Player
        {
            Name = "BombproofBombValidationPlayer"
        };
        bombproofPlayer.Initialize(
            new ValidationRingPlayerWorld(),
            bombproofInventory,
            explosionPoint,
            new OracleRandom());
        var bombproofBomb = new BombEffect();
        bombproofBomb.Initialize(
            record, effectRoom, new BreakableTileDatabase(),
            bombproofPlayer, 0,
            bomb => bomb.ReleaseExploding(
                bombproofPlayer, Vector2I.Zero),
            _ => { }, (_, _, _) => { }, () => { }, () => 0,
            _ => null, saveData: null, linkedRoomNeighbor: null);
        for (int update = 0; update < 118; update++)
            bombproofBomb.UpdateFrame(bombproofPlayer, effectSpawns);
        FailIf(
            bombproofPlayer.HealthQuarters !=
                bombproofPlayer.MaxHealthQuarters,
            "BOMBPROOF_RING did not suppress live own-Bomb damage.");
        bombproofBomb.Free();
        bombproofPlayer.Free();

        var sideRooms = new RoomSession(
            6, 0x29, () => 0, () => { }, save);
        OracleRoomData sideRoom = sideRooms.CurrentRoom;
        FailIf(
            (sideRoom.TilesetFlags & 0x20) == 0,
            "The ITEM_BOMB side-view validation did not load source room 6:29.");
        bool foundSideLanding = false;
        Vector2 sideLanding = default;
        for (int y = 8; y < sideRoom.Height - 6 && !foundSideLanding; y++)
        for (int x = 8; x < sideRoom.Width - 8; x++)
        {
            Vector2 candidate = new(x, y);
            if (!sideRoom.IsSolid(candidate) &&
                sideRoom.IsSolid(candidate + new Vector2(0, 5)) &&
                sideRoom.GetTerrainInfo(candidate).Hazard == HazardType.None)
            {
                sideLanding = candidate;
                foundSideLanding = true;
                break;
            }
        }
        FailIf(
            !foundSideLanding,
            "Room 6:29 no longer contains a clear side-view Bomb landing probe.");

        player.SetScriptedPosition(sideLanding);
        var sideBombSounds = new List<int>();
        var sideBomb = new BombEffect();
        sideBomb.Initialize(
            record, sideRoom, new BreakableTileDatabase(),
            player, 6, _ => { }, sideBombSounds.Add, (_, _, _) => { },
            () => { }, () => 0, _ => null, saveData: null,
            linkedRoomNeighbor: null);
        sideBomb.UpdateFrame(player, effectSpawns);
        sideBomb.Throw(
            player,
            -player.FacingVector,
            Vector2I.Zero,
            speedZ: 0x80,
            speedRaw: 0);
        sideBomb.UpdateFrame(player, effectSpawns);
        FailIf(
            sideBomb.State != BombState.Thrown ||
            sideBomb.SpeedZ != 0x80 ||
            sideBombSounds.Count != 0,
            "A side-view Bomb bounced on the first floor collision instead " +
            "of setting Item.var3b bit 4.");
        sideBomb.UpdateFrame(player, effectSpawns);
        FailIf(
            sideBomb.State != BombState.Grounded ||
            sideBomb.SpeedZ != 0 ||
            !sideBombSounds.SequenceEqual(
                [OracleSoundEngine.SndBombLand]),
            "A side-view Bomb did not stop on its second consecutive floor " +
            "collision.");
        sideBomb.Free();

        // bombUpdateThrowingVerticallyAndCheckDelete skips the ordinary room
        // boundary for wrapping Y high bytes $00-$07 and $f8-$ff.
        player.SetScriptedPosition(new Vector2(80, 1));
        var wrappingBomb = new BombEffect();
        wrappingBomb.Initialize(
            record, sideRoom, new BreakableTileDatabase(),
            player, 6, _ => { }, _ => { }, (_, _, _) => { },
            () => { }, () => 0, _ => null, saveData: null,
            linkedRoomNeighbor: null);
        wrappingBomb.UpdateFrame(player, effectSpawns);
        wrappingBomb.Throw(
            player,
            Vector2I.Zero,
            Vector2I.Zero,
            speedZ: 0,
            speedRaw: 0);
        wrappingBomb.UpdateFrame(player, effectSpawns);
        FailIf(
            wrappingBomb.Finished,
            "A side-view Bomb at wrapping Y high byte $01 was incorrectly " +
            "deleted by the ordinary room boundary.");
        wrappingBomb.Free();

        // itemMergeZPositionIfSidescrollingArea also runs when a held Bomb
        // starts exploding and dropLinkHeldItem releases it.
        player.SetScriptedPosition(new Vector2(80, 80));
        var heldSideBomb = new BombEffect();
        heldSideBomb.Initialize(
            record, sideRoom, new BreakableTileDatabase(),
            player, 6,
            bomb => bomb.ReleaseExploding(
                player, new Vector2I(0, -8)),
            _ => { }, (_, _, _) => { }, () => { }, () => 0,
            _ => null, saveData: null, linkedRoomNeighbor: null);
        heldSideBomb.SetHeldOffset(player, new Vector2I(0, -8));
        heldSideBomb.UpdateFrame(player, effectSpawns);
        for (int update = 0; update < 116; update++)
            heldSideBomb.UpdateFrame(player, effectSpawns);
        FailIf(
            heldSideBomb.State != BombState.Exploding ||
            heldSideBomb.ZFixed != 0 ||
            heldSideBomb.PrecisePosition != new Vector2(80, 72),
            "A released exploding side-view Bomb did not merge signed zh " +
            "into yh and clear Z.");
        heldSideBomb.Free();

        manager.SoundRequested -= sounds.Add;
        manager.Clear();
        root.RemoveChild(player);
        player.Free();
        RemoveChild(root);
        root.Free();

        GD.Print(
            "Validated ITEM_BOMB imported OAM/physics/probes, packed-BCD " +
            "allocation, live pickup and object cap, 7/4/2 lift, eight-update " +
            "angle-$ff in-place drop, directional throw, 116-update fuse, " +
            "center-first bombable tile break, " +
            "35-update expanding explosion, and Bomber/Peace/Blast/Bombproof rings.");
    }

    private void ValidateLinkTerrainEffects()
    {
        Vector2 grassPosition = new(56, 120);
        Vector2 puddlePosition = new(56, 72);
        TerrainInfo grassTerrain =
            _world.LoadRoom(0, 0x06).GetTerrainInfo(grassPosition);
        TerrainInfo puddleTerrain =
            _world.LoadRoom(0, 0x38).GetTerrainInfo(puddlePosition);
        FailIf(
            grassTerrain.Tile != 0xf8 ||
            grassTerrain.Type != TerrainType.Grass ||
            puddleTerrain.Tile != 0xf9 ||
            puddleTerrain.Type != TerrainType.Puddle,
            "Canonical rooms 0:06 `$73 and 0:38 `$43 no longer expose " +
            "Ages grass `$f8 and shallow water `$f9.");

        OracleSaveData save = OracleSaveData.CreateStandardGame();
        var inventory = new InventoryState(_treasures, save);
        var world = new ValidationRingPlayerWorld
        {
            ActiveTerrain = new ActiveTerrainInfo(
                grassTerrain, grassPosition, grassPosition, 0x73)
        };
        var player = new Player { Name = "TerrainEffectValidationPlayer" };
        AddChild(player);
        player.Initialize(
            world, inventory, grassPosition, new OracleRandom());

        ReadOnlySpan<PlayerGroundDrawPass> groundDrawPasses =
            Player.GroundDrawPasses;
        FailIf(
            groundDrawPasses.Length != 2 ||
            groundDrawPasses[0] != PlayerGroundDrawPass.Body ||
            groundDrawPasses[1] != PlayerGroundDrawPass.TerrainEffect,
            "Link's grass/puddle OAM no longer has foreground priority " +
            "over his body.");

        LinkTerrainEffectFrame? grass = player.CurrentTerrainEffect;
        FailIf(
            grass is null ||
            grass.Kind != LinkTerrainEffectKind.Grass ||
            grass.Tile != 0xf8 || grass.Frame != 0 || grass.Duration != 0 ||
            grass.Sound != 0 || grass.SoundStart != 0 ||
            grass.SoundPeriod != 0 || grass.SoundDuration != 0 ||
            grass.Offset != new Vector2(-7, 1) ||
            grass.Texture.GetSize() != new Vector2(14, 16) ||
            !grass.Source.Contains(
                "greenGrassAnimationFrame0",
                StringComparison.Ordinal),
            "Link did not select green grass terrain-effect OAM frame 0 " +
            "at its source `(-7,+1)` anchor in room 0:06.");
        ulong grassFrame0Hash =
            OracleGraphicsCache.PixelHash(grass.Texture.GetImage());
        FailIf(
            grassFrame0Hash != 0x75dc0350c7f62b4aUL,
            "Link grass terrain-effect frame 0 pixel hash changed: " +
            $"{grassFrame0Hash:x16}.");

        player.SetScriptedPosition(new Vector2(60, 120));
        LinkTerrainEffectFrame? grassFrame1 = player.CurrentTerrainEffect;
        FailIf(
            grassFrame1 is null ||
            grassFrame1.Kind != LinkTerrainEffectKind.Grass ||
            grassFrame1.Frame != 1 || grassFrame1.Duration != 0 ||
            grassFrame1.Sound != 0 ||
            grassFrame1.Offset != new Vector2(-7, 1) ||
            grassFrame1.Texture.GetSize() != new Vector2(14, 16) ||
            !grassFrame1.Source.Contains(
                "greenGrassAnimationFrame1",
                StringComparison.Ordinal),
            "Link's `(xh XOR yh) bit 2 did not select green grass " +
            "terrain-effect OAM frame 1 within room 0:06 tile `$73.");
        ulong grassFrame1Hash =
            OracleGraphicsCache.PixelHash(grassFrame1.Texture.GetImage());
        FailIf(
            grassFrame1Hash != 0xe204832ae00b118aUL,
            "Link grass terrain-effect frame 1 pixel hash changed: " +
            $"{grassFrame1Hash:x16}.");
        player.SetScriptedPosition(puddlePosition);

        world.ActiveTerrain = new ActiveTerrainInfo(
            puddleTerrain, puddlePosition, puddlePosition, 0x43);
        int[] counters = [0, 7, 8, 15, 16, 23, 24, 31, 32];
        int[] expectedFrames = [0, 0, 1, 1, 2, 2, 3, 3, 0];
        Vector2[] expectedOffsets =
        [
            new(-5, 6),
            new(-6, 6),
            new(-7, 7),
            new(-8, 8)
        ];
        Vector2[] expectedSizes =
        [
            new(10, 16),
            new(12, 16),
            new(14, 16),
            new(16, 16)
        ];
        ulong[] expectedPuddleHashes =
        [
            0x60764db388700bdaUL,
            0x3a7d8efe3de45a9fUL,
            0x4295bf38614c83beUL,
            0xb5f1e08fe1b74228UL
        ];
        for (int index = 0; index < counters.Length; index++)
        {
            world.FrameCounter = counters[index];
            LinkTerrainEffectFrame? puddle = player.CurrentTerrainEffect;
            int expectedFrame = expectedFrames[index];
            FailIf(
                puddle is null ||
                puddle.Kind != LinkTerrainEffectKind.Puddle ||
                puddle.Tile != 0xf9 ||
                puddle.Frame != expectedFrame ||
                puddle.Duration != 8 ||
                puddle.Sound != OracleSoundEngine.SndSplash ||
                puddle.SoundStart != 3 ||
                puddle.SoundPeriod != 18 ||
                puddle.SoundDuration != 6 ||
                puddle.Offset != expectedOffsets[expectedFrame] ||
                puddle.Texture.GetSize() != expectedSizes[expectedFrame] ||
                !puddle.Source.Contains(
                    $"puddleAnimationFrame{expectedFrame}",
                    StringComparison.Ordinal),
                $"Link shallow-water terrain effect selected the wrong " +
                $"OAM frame at global update ${counters[index]:x2}; " +
                $"expected frame {expectedFrame}.");
            ulong puddleHash =
                OracleGraphicsCache.PixelHash(puddle.Texture.GetImage());
            FailIf(
                puddleHash != expectedPuddleHashes[expectedFrame],
                $"Link shallow-water terrain-effect frame {expectedFrame} " +
                $"pixel hash changed: {puddleHash:x16}.");
        }

        world.Sounds.Clear();
        player.AdvanceTerrainWalkAnimation(walking: false);
        int[] expectedSplashUpdates = [3, 21, 39, 57];
        for (int update = 1; update <= expectedSplashUpdates[^1]; update++)
        {
            int soundsBefore = world.Sounds.Count;
            player.ApplyTerrainWalkSoundParameter();
            bool expectedSplash =
                Array.IndexOf(expectedSplashUpdates, update) >= 0;
            FailIf(
                (world.Sounds.Count != soundsBefore) != expectedSplash ||
                (expectedSplash &&
                    world.Sounds[^1] != OracleSoundEngine.SndSplash),
                $"Link's puddle walk animation selected the wrong sound " +
                $"cadence at update {update}; expected splash={expectedSplash}.");
            player.AdvanceTerrainWalkAnimation(walking: true);
        }

        player.AdvanceTerrainWalkAnimation(walking: false);
        world.Sounds.Clear();
        world.ActiveTerrain = new ActiveTerrainInfo(
            new TerrainInfo(0x00, 0x00, TerrainType.Normal, HazardType.None),
            Vector2.Zero,
            Vector2.Zero,
            0);
        for (int update = 1; update <= 3; update++)
        {
            player.ApplyTerrainWalkSoundParameter();
            player.AdvanceTerrainWalkAnimation(walking: true);
        }
        world.ActiveTerrain = new ActiveTerrainInfo(
            puddleTerrain, puddlePosition, puddlePosition, 0x43);
        player.ApplyTerrainWalkSoundParameter();
        player.ApplyTerrainWalkSoundParameter();
        player.AdvanceTerrainWalkAnimation(walking: true);
        player.ApplyTerrainWalkSoundParameter();
        FailIf(
            world.Sounds.Count != 1 ||
            world.Sounds[0] != OracleSoundEngine.SndSplash,
            "Link's unconsumed walk-animation sound parameter did not " +
            "survive until he entered shallow water, or played twice.");

        player.AdvanceTerrainWalkAnimation(walking: false);
        world.Sounds.Clear();
        player.ApplyTerrainWalkSoundParameter();
        player.AdvanceTerrainWalkAnimation(walking: true);
        player.ApplyTerrainWalkSoundParameter();
        player.AdvanceTerrainWalkAnimation(walking: true);
        world.MovementDisabled = true;
        player.ApplyTerrainWalkSoundParameter();
        world.MovementDisabled = false;
        player.ApplyTerrainWalkSoundParameter();
        FailIf(
            world.Sounds.Count != 0,
            "wLinkImmobilized did not consume and suppress the pending " +
            "shallow-water step sound.");
        player.AdvanceTerrainWalkAnimation(walking: false);

        player.BeginForcedRoomEntryMovement(Vector2I.Right);
        FailIf(
            !player.Walking || player.CurrentTerrainEffect is null,
            "Walking Link lost the shallow-water terrain effect.");
        player.EndForcedRoomEntryMovement();

        world.ScreenScrolling = true;
        FailIf(
            player.CurrentTerrainEffect is not null,
            "wScrollMode `$08 did not suppress Link's grounded terrain effect.");
        world.ScreenScrolling = false;
        world.SideScrolling = true;
        FailIf(
            player.CurrentTerrainEffect is not null,
            "TILESETFLAG_SIDESCROLL did not suppress Link's grounded terrain effect.");
        world.SideScrolling = false;
        world.RidingObject = true;
        FailIf(
            player.CurrentTerrainEffect is null,
            "wLinkRidingObject incorrectly suppressed Link's grounded " +
            "terrain effect without a source visible-bit-6 clear.");
        world.RidingObject = false;
        world.ActiveTerrain = new ActiveTerrainInfo(
            new TerrainInfo(0x00, 0x00, TerrainType.Normal, HazardType.None),
            Vector2.Zero,
            Vector2.Zero,
            0);
        FailIf(
            player.CurrentTerrainEffect is not null,
            "Ordinary terrain incorrectly selected a Link grass/puddle effect.");

        player.Free();
        GD.Print(
            "Validated Link grass/shallow-water terrain effects: canonical " +
            "`$f8/`$f9 probes, position-selected grass OAM, four eight-update " +
            "puddle frames, foreground OAM priority, positioned pixels, " +
            "18-update splash cadence/suppression, draw-state suppression, " +
            "and walking.");
    }

    private void ValidateLinkItemGeneratedData()
    {
        LinkItemDatabase database = LinkItemDatabase.Shared;
        LinkItemConstants constants = database.Constants;
        FailIf(
            constants.SwordSwingFrames != 17 ||
            constants.SwordTileHitFrame != 6 ||
            constants.SwordRestartFrame != 3 ||
            constants.SwordChargeCounter != 0x28 ||
            constants.SwordPokeFrames != 12 ||
            constants.SwordSpinFrames != 23 ||
            constants.ShovelActionFrames != 23 ||
            constants.ShovelDigFrame != 4 ||
            constants.ShovelSecondPoseFrame != 8 ||
            constants.ShieldSound != OracleSoundEngine.SndShield ||
            constants.ShieldCollisionEffect != 0x1f ||
            constants.ShieldLinkResponse != 0x20 ||
            constants.ShieldProjectileResponse != 0x34 ||
            constants.ProjectileCollisionMode != 0x06 ||
            constants.RingProjectileCollisionMode != 0x07 ||
            !constants.SwingPhaseStarts.SequenceEqual([0, 3, 6, 14]) ||
            !constants.SpinPhaseStarts.SequenceEqual(
                [0, 3, 5, 8, 10, 13, 15, 18, 20]),
            "Imported Link/item action constants lost their exact source boundaries.");

        int[] expectedSwingPhases =
            [0, 0, 0, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 3];
        for (int frame = 0; frame < expectedSwingPhases.Length; frame++)
        {
            FailIf(
                database.SwingPhase(frame) != expectedSwingPhases[frame],
                $"Imported sword swing selected the wrong phase at update {frame}.");
        }
        int[] expectedSpinPhases =
            [0, 0, 0, 1, 1, 2, 2, 2, 3, 3, 4, 4, 4, 5, 5, 6, 6, 6, 7, 7, 0];
        for (int frame = 0; frame < expectedSpinPhases.Length; frame++)
        {
            FailIf(
                database.SpinPhase(frame) != expectedSpinPhases[frame],
                $"Imported swordspin selected the wrong phase at update {frame}.");
        }

        Vector2[] attackPoseOffsets =
            [new(0, -3), new(3, 0), new(0, 3), new(-3, 0)];
        Vector2[] shovelOffsets =
            [new(0, -8), new(6, 4), new(0, 7), new(-7, 4)];
        Vector2[] shieldCenters =
            [new(1, -7), new(6, 0), new(-1, 6), new(-7, 0)];
        Vector2[] shieldRadii =
            [new(6, 1), new(1, 7), new(6, 1), new(1, 7)];
        Vector2[] swordTileOffsets =
        [
            new(0, -14), new(13, -14), new(13, 0), new(13, 13),
            new(0, 13), new(-14, 13), new(-14, 0), new(-14, -14),
            Vector2.Zero
        ];
        Vector2I[,] braceletOffsets =
        {
            { new(0, -8), new(7, 0), new(0, 6), new(-8, 0) },
            { new(0, -6), new(3, -8), new(0, 4), new(-4, -8) },
            { new(0, -13), new(0, -14), new(0, -13), new(0, -14) },
            { new(0, -13), new(0, -13), new(0, -13), new(0, -13) }
        };
        for (int direction = 0; direction < 4; direction++)
        {
            FailIf(
                database.AttackPoseOffset(direction) !=
                    attackPoseOffsets[direction] ||
                database.ShovelOffset(direction) != shovelOffsets[direction] ||
                database.ShieldCenterOffset(direction) !=
                    shieldCenters[direction] ||
                database.ShieldCollisionRadius(direction) !=
                    shieldRadii[direction],
                $"Imported directional Link/item geometry row {direction} changed.");
            for (int frame = 0; frame < 4; frame++)
            {
                FailIf(
                    database.BraceletLiftOffset(frame, direction) !=
                        braceletOffsets[frame, direction],
                    $"Imported Bracelet weight-0 offset {frame}/{direction} changed.");
            }
        }
        for (int direction = 0; direction < swordTileOffsets.Length; direction++)
        {
            FailIf(
                database.SwordTileOffset(direction) !=
                    swordTileOffsets[direction],
                $"Imported sword-tile probe offset {direction} changed.");
        }

        int[,] expectedAnimations =
        {
            { 2, 1, 0, 0 },
            { 0, 1, 2, 2 },
            { 6, 5, 4, 4 },
            { 0, 7, 6, 6 }
        };
        for (int direction = 0; direction < 4; direction++)
        for (int phase = 0; phase < 4; phase++)
        {
            FailIf(
                database.SwordAnimation(direction, phase) !=
                    expectedAnimations[direction, phase],
                $"Imported sword animation row {direction}/{phase} changed.");
        }

        int[] expectedSounds =
        [
            OracleSoundEngine.SndSwordSlash,
            OracleSoundEngine.SndUnknown5,
            OracleSoundEngine.SndBoomerang,
            OracleSoundEngine.SndSwordSlash,
            OracleSoundEngine.SndSwordSlash,
            OracleSoundEngine.SndUnknown5,
            OracleSoundEngine.SndSwordSlash,
            OracleSoundEngine.SndSwordSlash
        ];
        for (int index = 0; index < expectedSounds.Length; index++)
        {
            FailIf(
                database.SwordSlashSound(index) != expectedSounds[index],
                $"Imported sword slash-sound row {index} changed.");
        }

        SwordPart[][] expectedOam =
        [
            [new SwordPart(8, 4, 4)],
            [new SwordPart(8, 0, 8, true), new SwordPart(8, 8, 6, true)],
            [new SwordPart(8, 0, 2, true), new SwordPart(8, 8, 0, true)],
            [
                new SwordPart(8, 0, 8, true, true),
                new SwordPart(8, 8, 6, true, true)
            ],
            [new SwordPart(8, 4, 4, false, true)],
            [
                new SwordPart(8, 0, 6, false, true),
                new SwordPart(8, 8, 8, false, true)
            ],
            [new SwordPart(8, 0, 0), new SwordPart(8, 8, 2)],
            [new SwordPart(8, 0, 6), new SwordPart(8, 8, 8)]
        ];
        for (int animation = 0; animation < expectedOam.Length; animation++)
        {
            FailIf(
                !database.SwordOam(animation).SequenceEqual(
                    expectedOam[animation]),
                $"Imported ITEM_SWORD OAM composition {animation} changed.");
        }
        SwordArc[] punchArcs =
        [
            new(5, 5, -12, -3), new(5, 5, 0, 12),
            new(5, 5, 12, 3), new(5, 5, 0, -12)
        ];
        FailIf(
            database.SwordArcs.Count != 28 ||
            !database.SwordArcs.Skip(24).SequenceEqual(punchArcs),
            "Imported punch aliases at swordArcData rows $18-$1b changed.");

        int[] attackBases = [0xac, 0xb0, 0xb4];
        for (int phase = 0; phase < attackBases.Length; phase++)
        for (int direction = 0; direction < 4; direction++)
        {
            LinkGraphicRecord record =
                database.Graphic("attack", 0, phase, direction);
            FailIf(
                record.GraphicsIndex != attackBases[phase] + direction ||
                string.IsNullOrEmpty(record.Oam),
                $"Imported attack graphic {phase}/{direction} changed.");
        }
        int[] shovelBases = [0xf8, 0xfc];
        for (int phase = 0; phase < shovelBases.Length; phase++)
        for (int direction = 0; direction < 4; direction++)
        {
            FailIf(
                database.Graphic("shovel", 0, phase, direction).GraphicsIndex !=
                    shovelBases[phase] + direction,
                $"Imported shovel graphic {phase}/{direction} changed.");
        }
        int[] braceletBases = [0xdc, 0xe0, 0xb0];
        for (int pose = 0; pose < braceletBases.Length; pose++)
        for (int direction = 0; direction < 4; direction++)
        {
            FailIf(
                database.Graphic("bracelet", pose, 0, direction).GraphicsIndex !=
                    braceletBases[pose] + direction,
                $"Imported Bracelet Link graphic {pose}/{direction} changed.");
        }
        for (int variant = 0; variant < 4; variant++)
        for (int phase = 0; phase < 2; phase++)
        for (int direction = 0; direction < 4; direction++)
        {
            int expectedGraphics =
                0x68 + variant * 4 + phase * 0x2c + direction;
            FailIf(
                database.Graphic(
                    "shield", variant, phase, direction).GraphicsIndex !=
                    expectedGraphics,
                $"Imported shield graphic {variant}/{phase}/{direction} changed.");
        }

        ClinkTileRecord[] clinkRows = database.ClinkRows.ToArray();
        FailIf(
            clinkRows.Length != 68 ||
            clinkRows.Count(record => record.Terminal) != 12 ||
            clinkRows.Any(record =>
                record.Terminal && (record.Tile != 0 || record.Order < 0)) ||
            !database.IsBombableClinkTile(0, 0xc1) ||
            !database.IsBombableClinkTile(4, 0xcf) ||
            !database.IsBombableClinkTile(1, 0x69) ||
            !database.IsBombableClinkTile(2, 0x30) ||
            !database.IsBombableClinkTile(3, 0x12) ||
            !database.IsBombableClinkTile(5, 0x38) ||
            !database.IsSilentClinkTile(0, 0xfd) ||
            !database.IsSilentClinkTile(4, 0xff) ||
            !database.IsSilentClinkTile(1, 0x0a) ||
            !database.IsSilentClinkTile(2, 0x0b) ||
            database.IsSilentClinkTile(3, 0x0a) ||
            !database.IsSilentClinkTile(5, 0x0b),
            "Imported aliased clinkSoundTable rows or terminal zeroes changed.");

        ulong[] braceletHashes = new ulong[12];
        for (int pose = 0; pose < 3; pose++)
        for (int direction = 0; direction < 4; direction++)
        {
            braceletHashes[pose * 4 + direction] =
                _player.BraceletActionPixelHash(pose, direction);
        }
        ulong[] expectedBraceletHashes =
        [
            0x130b4ac9d7502459UL, 0xf1012272bded617eUL,
            0xa3ab9759565b4828UL, 0x69a466130e99f61eUL,
            0x440f60ecb7d47160UL, 0xa71e2d9d3d2bc769UL,
            0x2c3be5cae23b9510UL, 0xef69b079d1a96f19UL,
            0x997ee6d2d76d2925UL, 0x0030bdac653b5e39UL,
            0xfdd494921f9b713dUL, 0x3a8738decc4cd051UL
        ];
        FailIf(
            _player.AttackAtlasPixelHash != 0xd97fede215433c60UL ||
            _player.ShovelAtlasPixelHash != 0x5d009ee6640952eeUL ||
            _player.SwordAtlasPixelHash != 0x61e9abb0e1173ec7UL ||
            _player.ShieldAtlasPixelHash != 0xe4598edf4d896e5cUL ||
            !braceletHashes.SequenceEqual(expectedBraceletHashes),
            $"Imported Link/item OAM pixels changed " +
            $"(shield={_player.ShieldAtlasPixelHash:x16}).");

        GD.Print(
            "Validated generated Link/item/sword data: ordered action, graphics, " +
            "offset, arc, OAM, sound, aliased clink, and terminal-zero records.");
    }

    private void ValidateShield()
    {
        OracleSaveData save = OracleSaveData.CreateStandardGame();
        var inventory = new InventoryState(_treasures, save);
        inventory.GiveTreasure(_treasures.GetObject("TREASURE_OBJECT_SHIELD_00"));
        inventory.EquipA(InventoryState.ItemShield);
        FailIf(
            inventory.ShieldLevel != 1 ||
            inventory.EquippedA != InventoryState.ItemShield ||
            save.ReadWramByte(0xc6af) != 1,
            "TREASURE_SHIELD mode $08 did not persist/equip its level-1 item.");
        ValidateShieldDisplay(inventory, level: 1, sprite: 0x93,
            palette: 0x00, textLow: 0x20);

        ValidationRingPlayerWorld world = new ValidationRingPlayerWorld();
        var player = new Player { Name = "ShieldValidationPlayer" };
        AddChild(player);
        player.Initialize(world, inventory, new Vector2(80, 80), new OracleRandom());
        FailIf(
            !player.IsShieldEquipped || player.IsUsingShield ||
            player.ShieldAtlasPixelHash == 0,
            "An equipped Wooden Shield did not select the source Link pose atlas.");

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
            FailIf(
                bounds.GetCenter() != centers[direction] ||
                bounds.Size / 2.0f != radii[direction] ||
                player.ShieldGraphicsIndex != 0x68 + direction,
                $"ITEM_SHIELD direction {direction} lost its source center, radius, or equipped graphics.");
        }

        player.Face(Vector2I.Up);
        player.UpdateShieldForValidation(attackHeld: true, itemHeld: false);
        FailIf(
            !player.IsUsingShield || player.ShieldGraphicsIndex != 0x70 ||
            world.Sounds.Count(sound => sound == OracleSoundEngine.SndShield) != 1,
            "Holding the equipped A-button shield did not select wUsingShield level 1 or SND_SHIELD.");
        player.UpdateShieldForValidation(attackHeld: true, itemHeld: false);
        FailIf(
            world.Sounds.Count(sound => sound == OracleSoundEngine.SndShield) != 1,
            "ITEM_SHIELD replayed SND_SHIELD while its parent item remained held.");
        player.BeginScrollingTransition(player.Position, Vector2I.Right);
        FailIf(
            player.IsUsingShield || player.ShieldGraphicsIndex != 0x69,
            "wScrollMode $08 did not lower the shield while retaining its parent item.");
        player.FinishScrollingTransition(player.Position);
        player.UpdateShieldForValidation(attackHeld: true, itemHeld: false);
        FailIf(
            !player.IsUsingShield || player.ShieldGraphicsIndex != 0x71 ||
            world.Sounds.Count(sound => sound == OracleSoundEngine.SndShield) != 1,
            "The retained shield parent did not resume silently after scrolling.");

        LoadBushValidationRoom();
        player.Face(Vector2I.Up);
        var enemies = new EnemyDatabase();
        Vector2 shieldCenter = player.ShieldCollisionBounds.GetCenter();
        var rock = new OctorokRockProjectile();
        rock.Initialize(
            enemies.OctorokProjectile, _currentRoom, shieldCenter, angle: 0);
        rock.UpdateFrame(player); // State 0 setup-only update.
        int healthBeforeBlock = player.HealthQuarters;
        rock.UpdateFrame(player);
        FailIf(
            rock.State != HostileProjectileState.Bouncing ||
            rock.Angle != 0x10 || rock.Counter != 0x20 || rock.ZFixed != 0 ||
            player.HealthQuarters != healthBeforeBlock ||
            world.Sounds.Count(sound => sound == OracleSoundEngine.SndClink2) != 1,
            "A raised shield did not send PART_OCTOROK_PROJECTILE through ENEMYDMG_$34/LINKDMG_$20.");

        var arrow = new EnemyArrowProjectile();
        arrow.Initialize(enemies.EnemyArrow, _currentRoom, Vector2.Zero, angle: 0);
        arrow.Position = shieldCenter;
        arrow.UpdateFrame(player); // State 0 setup-only update.
        arrow.UpdateFrame(player);
        FailIf(
            arrow.State != HostileProjectileState.Bouncing ||
            arrow.Counter != 0x20 || player.HealthQuarters != healthBeforeBlock ||
            world.Sounds.Count(sound => sound == OracleSoundEngine.SndClink2) != 2,
            "A raised shield did not deflect PART_ENEMY_ARROW with the shared bounce path.");

        player.UpdateShieldForValidation(attackHeld: false, itemHeld: false);
        FailIf(
            player.IsUsingShield || player.ShieldGraphicsIndex != 0x68,
            "Releasing ITEM_SHIELD did not restore the equipped-but-lowered pose.");

        var unblockedRock = new OctorokRockProjectile();
        unblockedRock.Initialize(
            enemies.OctorokProjectile, _currentRoom, player.Position, angle: 0);
        unblockedRock.UpdateFrame(player);
        unblockedRock.UpdateFrame(player);
        FailIf(
            !unblockedRock.Finished || player.HealthQuarters >= healthBeforeBlock ||
            world.Sounds.Count(sound => sound == OracleSoundEngine.SndDamageLink) != 1,
            "An equipped but lowered shield incorrectly blocked an Octorok projectile.");

        inventory.GiveTreasure(_treasures.GetObject("TREASURE_OBJECT_SHIELD_01"));
        FailIf(
            inventory.ShieldLevel != 2 || player.ShieldGraphicsIndex != 0x6c,
            "The Iron Shield upgrade did not select the level-2 equipped pose.");
        ValidateShieldDisplay(inventory, level: 2, sprite: 0x94,
            palette: 0x05, textLow: 0x21);
        player.UpdateShieldForValidation(attackHeld: true, itemHeld: false);
        FailIf(
            !player.IsUsingShield || player.ShieldGraphicsIndex != 0x74,
            "The raised Iron Shield did not select the shared level-2/3 pose.");
        player.UpdateShieldForValidation(attackHeld: false, itemHeld: false);
        inventory.GiveTreasure(_treasures.GetObject("TREASURE_OBJECT_SHIELD_02"));
        ValidateShieldDisplay(inventory, level: 3, sprite: 0x95,
            palette: 0x04, textLow: 0x22);
        inventory.EquipB(InventoryState.ItemShield);
        player.UpdateShieldForValidation(attackHeld: false, itemHeld: true);
        FailIf(
            inventory.ShieldLevel != 3 ||
            inventory.EquippedB != InventoryState.ItemShield ||
            inventory.EquippedA == InventoryState.ItemShield ||
            !player.IsUsingShield || player.ShieldGraphicsIndex != 0x74 ||
            world.Sounds.Count(sound => sound == OracleSoundEngine.SndShield) != 3,
            "The Mirror Shield upgrade/B-button parent did not preserve the level-3 shared pose and activation.");

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
        DisplayRecord display =
            _treasures.GetButtonDisplay(InventoryState.ItemShield, inventory);
        DisplayRecord parameterDisplay =
            _treasures.GetTreasureDisplay(
                TreasureDatabase.TreasureShield, level, inventory);
        FailIf(
            display != parameterDisplay ||
            display.TreasureId != TreasureDatabase.TreasureShield ||
            display.LeftSprite != sprite || display.LeftPalette != palette ||
            display.RightSprite != 0 || display.RightPalette != 0 ||
            display.ExtraMode != 0 || display.TextLow != textLow ||
            inventory.LevelForInventoryDisplay(
                TreasureDatabase.TreasureShield) != level ||
            ItemIconAtlas.EquippedLeftPalette(
                display.LeftSprite, display.LeftPalette) != palette,
            $"Shield level {level} did not select its exact inventory/equipped display row.");

        Image icons1 = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_item_icons_1_spr.png");
        Image icons2 = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_item_icons_2.png");
        Image icons3 = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_item_icons_3.png");
        FailIf(
            !ItemIconAtlas.Select(
                display.LeftSprite, icons1, icons2, icons3,
                out Image source, out int cell) ||
            source != icons2 || cell != sprite - 0x90 ||
            ItemIconAtlas.DecodedCellHash(source, cell) == 0,
            $"Shield level {level} did not resolve to its source item-icons-2 cell.");
    }

    private void ValidateShovel()
    {
        LoadBushValidationRoom();
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
        FailIf(
            !_player.IsUsingShovel || _player.ShovelFrame != 0 ||
            _player.ShovelChildActive ||
            _player.ShovelChildOffset != new Vector2(0, -8),
            "ITEM_SHOVEL did not initialize LINK_ANIM_MODE_DIG_2 at its up-facing offset.");

        _player.AdvanceShovelForValidation(3);
        FailIf(
            _player.ShovelFrame != 3 || _currentRoom.GetMetatile(tileCenter) != 0x01 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDig) != 0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndClink) != 0,
            "ITEM_SHOVEL attempted its tile collision before animation update 4.");

        _player.AdvanceShovelForValidation(1);
        List<ShovelDebrisEffect> debris = _entities.Entities<ShovelDebrisEffect>();
        FailIf(
            _player.ShovelFrame != 4 || !_player.ShovelChildActive ||
            _currentRoom.GetMetatile(tileCenter) != 0x1c ||
            _saveData.GashaMaturity != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDig) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndClink) != 0 ||
            debris.Count != debrisBefore + 1,
            "The update-4 shovel child did not replace dirt, mature gasha state, " +
            "play SND_DIG, and spawn INTERAC_SHOVELDEBRIS exactly once.");

        ShovelDebrisEffect chip = debris[^1];
        Vector2 debrisStart = chip.PrecisePosition;
        chip.UpdateFrame();
        FailIf(
            chip.ElapsedFrames != 1 || chip.PrecisePosition != debrisStart + Vector2.Up * 0.5f ||
            chip.SpeedZ != -0x1e0 || chip.ZFixed != -0x240,
            "INTERAC_SHOVELDEBRIS did not apply SPEED_80 and its original 8.8 Z integration.");
        for (int frame = 1; frame < 14; frame++)
            chip.UpdateFrame();
        FailIf(
            !chip.Finished || chip.ElapsedFrames != 14,
            "INTERAC_SHOVELDEBRIS did not end with its 14-update animation.");

        _player.AdvanceShovelForValidation(3);
        FailIf(
            _player.ShovelFrame != 7 || !_player.ShovelChildActive,
            "ITEM_SHOVEL's four-update collision child ended before update 8.");
        _player.AdvanceShovelForValidation(1);
        FailIf(
            _player.ShovelFrame != 8 || _player.ShovelChildActive,
            "ITEM_SHOVEL did not enter graphics $fc and remove its collision child on update 8.");
        _player.AdvanceShovelForValidation(14);
        FailIf(
            !_player.IsUsingShovel || _player.ShovelFrame != 22,
            "LINK_ANIM_MODE_DIG_2 ended before update 23.");
        _player.AdvanceShovelForValidation(1);
        FailIf(_player.IsUsingShovel, "LINK_ANIM_MODE_DIG_2 did not end on update 23.");

        _sound.ClearPlayRequestAudit();
        _player.StartShovelAction();
        _player.AdvanceShovelForValidation(4);
        FailIf(
            _currentRoom.GetMetatile(tileCenter) != 0x1c ||
            _saveData.GashaMaturity != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndClink) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDig) != 0,
            "Shoveling a non-breakable tile did not preserve state and play SND_CLINK once.");
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
        FailIf(
            _currentRoom.GetMetatile(tileCenter) != 0xd2 ||
            _saveData.GashaMaturity != 51 ||
            !_saveData.HasRoomFlag(_activeGroup, _currentRoom.Id, OracleSaveData.RoomFlag80) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDig) != 1,
            "Shovel tile $cb did not apply its room flag, +50/+1 maturity, " +
            "SND_SOLVEPUZZLE, and SND_DIG table effects.");
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
            FailIf(
                _player.ShovelChildOffset != expectedOffsets[index],
                $"ITEM_SHOVEL direction {index} lost its signed Y/X child offset.");
        }

        GD.Print("Validated ITEM_SHOVEL timing, invisible child offsets, tile effects, " +
            "gasha maturity, debris, and clink/success sounds.");
    }

    private void ValidateSeedSatchel()
    {
        var database = new SeedSatchelDatabase();
        SeedRecord record = database.Ember;
        SeedRecord mystery = database.Mystery;
        FailIf(
            record.ParentItem != 0x19 || record.SeedItem != 0x20 ||
            record.TreasureId != 0x20 || record.Sprite != "spr_common_items" ||
            record.TileBase != 0x12 || record.Palette != 2 ||
            record.Collision != 0x9b || record.CollisionRadiusY != 4 ||
            record.CollisionRadiusX != 4 || record.Damage != 0xfe ||
            record.InitialZ != -2 || record.SpeedZ != -0x20 ||
            record.Gravity != 0x1c || record.SpeedRaw != 0x1e ||
            record.LinkFrames != 8 || record.FlameSprite != "spr_common_sprites" ||
            record.FlameTileBase != 0x06 || record.FlameOamFlags != 0x0a ||
            record.FlamePalette != 2 || record.FlameCounter != 0x3a ||
            record.LandingSound != 0x52 || record.FlameSound != 0x72,
            "The imported ITEM_SEED_SATCHEL/ITEM_EMBER_SEED record diverged from its source tables.");
        FailIf(
            mystery.ParentItem != 0x19 || mystery.SeedItem != 0x24 ||
            mystery.TreasureId != 0x24 ||
            mystery.Sprite != "spr_common_items" ||
            mystery.TileBase != 0x1a || mystery.Palette != 0 ||
            mystery.Collision != 0x9a ||
            mystery.CollisionRadiusY != 4 ||
            mystery.CollisionRadiusX != 4 ||
            mystery.Damage != 0xfe ||
            mystery.FlameSprite != "spr_common_sprites" ||
            mystery.FlameTileBase != 0x18 ||
            mystery.FlameOamFlags != 0x08 ||
            mystery.FlameCounter != 0 ||
            mystery.LandingSound != 0x52 ||
            mystery.FlameSound != OracleSoundEngine.SndMysterySeed,
            "The imported ITEM_MYSTERY_SEED record diverged from " +
            "itemCode24 / itemAttributes.");

        Vector2I[] directions =
            { Vector2I.Up, Vector2I.Right, Vector2I.Down, Vector2I.Left };
        Vector2I[] offsets =
            { new(0, -4), new(4, 1), new(0, 5), new(-5, 1) };
        for (int index = 0; index < directions.Length; index++)
        {
            FailIf(
                record.Offset(directions[index]) != offsets[index],
                $"Satchel direction {index} lost its signed Y/X child offset.");
        }

        OracleSaveData save = OracleSaveData.CreateStandardGame();
        var inventory = new InventoryState(_treasures, save);
        int grantInventoryChanges = 0;
        int grantSaveChanges = 0;
        inventory.Changed += () => grantInventoryChanges++;
        save.Changed += () => grantSaveChanges++;
        inventory.GiveTreasure(new TreasureObjectRecord(
            "VALIDATION_SATCHEL", 0x19, 0, 1, 0xff, 0, string.Empty));
        FailIf(
            inventory.EmberSeeds != 0x20 || grantInventoryChanges != 1 ||
            grantSaveChanges != 1,
            "The Seed Satchel did not expose its initial BCD 20 Ember Seeds in the grant transaction.");
        AnimationDefinition emberAnimation =
            OracleGraphicsCache.GetAnimationDefinition(record.Animation);
        string[] expectedOam =
        {
            "8,4,0,0",
            "8,4,0,0",
            "8,0,2,0;8,8,2,32",
            "8,0,4,7;8,8,4,39",
            "8,0,4,0;8,8,4,32"
        };
        FailIf(
            emberAnimation.LoopStart != 2 || emberAnimation.Frames.Length != 5 ||
            !emberAnimation.Frames.Select(frame => frame.EncodedOam)
                .SequenceEqual(expectedOam),
            "itemAnimation1e818 did not resolve its item20OamDataPointers compositions.");
        int inventoryChanges = 0;
        int saveChanges = 0;
        inventory.Changed += () => inventoryChanges++;
        save.Changed += () => saveChanges++;
        FailIf(
            !inventory.TryConsumeSelectedSatchelSeed(out int seedItem) ||
            seedItem != 0x20 || inventory.EmberSeeds != 0x19 ||
            save.ReadWramByte(0xc6b9) != 0x19 ||
            inventoryChanges != 1 || saveChanges != 1,
            "decNumActiveSeeds did not decrement/persist the selected Satchel count as packed BCD once.");
        for (int count = 0; count < 19; count++)
            inventory.TryConsumeSelectedSatchelSeed(out _);
        FailIf(
            inventory.EmberSeeds != 0 || inventory.TryConsumeSelectedSatchelSeed(out _),
            "The Satchel consumed a seed at zero or failed to reach BCD $00 from $20.");
        ValidateSeedInventorySubmenu();

        LoadBushValidationRoom();
        Vector2 linkPosition = new(80, 80);
        // State 0 consumes one update. Eight moving/gravity updates put this
        // up-facing throw at (80,70), where its flame expires.
        Vector2 flamePoint = new(80, 70);
        _currentRoom.SetPositionTileAndCollision(
            flamePoint, 0xc5, null, (long)_animationTicks);
        var sounds = new List<int>();
        var hazards = new List<HazardType>();
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
        FailIf(
            ember.FlameTextureHashForValidation(2) != expectedFlameHash,
            "ITEM_EMBER_SEED ignition did not switch OAM flag `$0a to " +
            "GFXH_COMMON_SPRITES bank-1 flame tiles `$06/`$08/`$0a.");
        ember.UpdateFrame(emberSpawns);
        FailIf(
            ember.State != EmberState.Flying ||
            ember.ElapsedFrames != 1 || ember.ZFixed != -0x200 ||
            ember.PrecisePosition != linkPosition + (Vector2)record.UpOffset,
            "ITEM_EMBER_SEED state 0 did not preserve the setup-only update and initial Z/offset.");
        for (int frame = 0; frame < 7; frame++)
            ember.UpdateFrame(emberSpawns);
        FailIf(
            ember.State != EmberState.Flying ||
            ember.ZFixed != -0x94 || ember.SpeedZ != 0xa4,
            "ITEM_EMBER_SEED did not retain its SPEED_c0/$1c 8.8 flight arc before landing.");
        ember.UpdateFrame(emberSpawns);
        FailIf(
            ember.State != EmberState.Burning ||
            ember.ElapsedFrames != 9 || ember.ZFixed != 0 ||
            ember.PrecisePosition != flamePoint || ember.FlameCounter != 0x3a ||
            ember.AnimationFrame != 1 ||
            !sounds.SequenceEqual(new[] { 0x52, 0x72 }) || hazards.Count != 0,
            "The Satchel Ember Seed did not land on update 9 and initialize its flame/sounds exactly.");
        ember.UpdateFrame(emberSpawns);
        FailIf(
            ember.FlameCounter != 0x39 || ember.AnimationFrame != 1,
            "emberSeedBurn did not decrement before advancing itemAnimation1e818.");
        for (int frame = 1; frame < 58; frame++)
            ember.UpdateFrame(emberSpawns);
        FailIf(
            !ember.Finished || ember.ElapsedFrames != 67 ||
            _currentRoom.GetMetatile(flamePoint) != 0x3a || tileChanges != 1,
            "The Ember flame did not apply BREAKABLETILESOURCE_EMBER_SEED on counter $3a expiry.");
        ember.Free();

        var standardSubstitutions = new StandardTileSubstitutionDatabase();
        FailIf(
            standardSubstitutions.RecordCount != 50,
            "The imported standard tile-substitution table did not retain all 50 Ages rows.");

        OracleSaveData watchedTreeSave = OracleSaveData.CreateStandardGame();
        var watchedTreeRooms = new RoomSession(
            0, 0x48, () => 0, () => { }, watchedTreeSave);
        OracleRoomData watchedTreeRoom = watchedTreeRooms.CurrentRoom;
        Vector2 watchedTreePoint = new(0x88, 0x68);
        var watcherDatabase = new RoomTileChangeWatcherDatabase();
        RoomTileChangeWatcherDatabaseRecord[] watcherRecords = watcherDatabase
            .GetRoomRecords(0, 0x48).ToArray();
        FailIf(
            watcherDatabase.RecordCount != 8 || watcherRecords is not
            [{ Position: 0x68, RoomFlag: 0x02, Order: 1 }] ||
            watchedTreeRoom.GetPackedPosition(watchedTreePoint) != 0x68 ||
            watchedTreeRoom.GetMetatile(watchedTreePoint) != 0xce,
            "Room 0:48 did not retain its imported $dc:$08 watcher and burnable tree $68/$ce.");

        var watcherRoot = new Node();
        AddChild(watcherRoot);
        using var watcherManagerFixture = RoomEntityValidationFixture.ForRoot(
            watcherRoot, new() { SaveData = watchedTreeSave });
        RoomEntityManager watcherManager = watcherManagerFixture.Manager;
        watcherManager.LoadRoom(0, watchedTreeRoom);
        FailIf(
            !watcherManager.Entities<Node2D>().Any(
            node => node.Name == "TileChangeWatcher_1"),
            "RoomEntityFactory did not instantiate room 0:48's imported $dc:$08 watcher.");
        var watchedTreeSpawns = new List<RoomEntitySpawn>();
        watcherManager.Update(1.0 / 60.0, _player);
        FailIf(
            watchedTreeSave.HasRoomFlag(0, 0x48, 0x02),
            "Room 0:48's $dc:$08 watcher set flag $02 during its snapshot state.");

        var watchedTreeSeed = new EmberSeedEffect();
        watchedTreeSeed.Initialize(
            record, watchedTreeRoom, new BreakableTileDatabase(),
            watchedTreePoint + new Vector2(0, 10), Vector2I.Up,
            _ => { }, (_, _) => { }, () => { }, () => 0,
            _ => null, watchedTreeSave, 0);
        for (int frame = 0; frame < 67; frame++)
            watchedTreeSeed.UpdateFrame(watchedTreeSpawns);
        FailIf(
            !watchedTreeSeed.Finished ||
            watchedTreeRoom.GetMetatile(watchedTreePoint) != 0x3a ||
            watchedTreeSave.HasRoomFlag(0, 0x48, 0x02),
            "Room 0:48's tree did not burn to $3a before its watcher update.");
        watcherManager.Update(1.0 / 60.0, _player);
        FailIf(
            !watchedTreeSave.HasRoomFlag(0, 0x48, 0x02) ||
            watcherManager.Entities<Node2D>().Any(
                node => node.Name == "TileChangeWatcher_1"),
            "Room 0:48's $dc:$08 watcher did not set room flag $02 after tile $68 changed.");
        watchedTreeSeed.Free();
        watcherManager.Clear();
        RemoveChild(watcherRoot);
        watcherRoot.Free();
        watchedTreeRooms.Load(0, 0x47);
        FailIf(
            watchedTreeRooms.Load(0, 0x48).GetMetatile(watchedTreePoint) != 0x3a,
            "Room 0:48's single-tile change did not preserve burnt tree $68/$3a on re-entry.");
        FailIf(
            !OracleSaveData.TryDeserialize(
                watchedTreeSave.Serialize(), out OracleSaveData? reloadedTreeSave) ||
            new RoomSession(0, 0x48, () => 0, () => { }, reloadedTreeSave!)
                .CurrentRoom.GetMetatile(watchedTreePoint) != 0x3a,
            "Room 0:48's burnt tree did not remain removed after save serialization and reload.");

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
        FailIf(burnRoomId < 0, "Could not find an overworld burnable tree tile $cf.");

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
        FailIf(
            !burningTreeSeed.Finished || burnRoom.GetMetatile(burnPoint) != 0xdc ||
            !persistentSave.HasRoomFlag(burnGroup, burnRoomId, OracleSaveData.RoomFlag80) ||
            persistentSave.GashaMaturity != maturityBeforeBurn + 30 ||
            burnSounds.Count(sound => sound == OracleSoundEngine.SndSolvePuzzle) != 1,
            "Burning overworld tree $cf did not set room flag $80, add 30 maturity, " +
            "play SND_SOLVEPUZZLE, and become $dc.");
        burningTreeSeed.Free();

        int otherRoomId = Enumerable.Range(0, 0x100).First(roomId =>
            roomId != burnRoomId && persistentRooms.World.HasRoom(burnGroup, roomId));
        persistentRooms.Load(burnGroup, otherRoomId);
        OracleRoomData sameSessionReload = persistentRooms.Load(burnGroup, burnRoomId);
        FailIf(
            sameSessionReload.GetMetatile(burnPoint) != 0xdc,
            "ROOMFLAG $80 did not retain standard substitution $cf->$dc after live re-entry.");

        FailIf(
            !OracleSaveData.TryDeserialize(
            persistentSave.Serialize(), out OracleSaveData? restoredPersistentSave),
            "The burnable-tree room flag did not survive save-image serialization.");
        var reloadedRooms = new RoomSession(
            burnGroup, burnRoomId, () => 0, () => { }, restoredPersistentSave!);
        FailIf(
            reloadedRooms.CurrentRoom.GetMetatile(burnPoint) != 0xdc,
            "ROOMFLAG $80 did not reapply standard substitution $cf->$dc after saved re-entry.");

        if (_inventory.EmberSeeds == 0)
        {
            _inventory.GiveTreasure(new TreasureObjectRecord(
                "VALIDATION_EMBER_SEED", 0x20, 0, 1, 0xff, 0, string.Empty));
        }
        _inventory.SelectSatchelSeeds(0);
        int beforeAmount = _inventory.EmberSeeds;
        int beforeEntities = _entities.Entities<EmberSeedEffect>().Count;
        _player.WarpTo(linkPosition);
        _player.StartSeedSatchelActionForValidation(Vector2.Right);
        int expectedAmount = ((beforeAmount >> 4) * 10 + (beforeAmount & 0x0f)) - 1;
        expectedAmount = ((expectedAmount / 10) << 4) | expectedAmount % 10;
        FailIf(
            !_player.IsUsingSeedSatchel || _player.SeedSatchelFrame != 0 ||
            _player.FacingVector != Vector2I.Right ||
            _inventory.EmberSeeds != expectedAmount ||
            _entities.Entities<EmberSeedEffect>().Count != beforeEntities + 1,
            "ITEM_SEED_SATCHEL did not allocate its child, decrement BCD ammo, and lock Link.");

        var hudQuantity = _hud.QuantityOverlayForValidation(
            InventoryState.ItemSeedSatchel, isA: false);
        var inventoryQuantity = _inventoryScreen.QuantityOverlayForValidation(
            InventoryState.ItemSeedSatchel);
        int expectedTens = 0x10 + ((expectedAmount >> 4) & 0x0f);
        int expectedOnes = 0x10 + (expectedAmount & 0x0f);
        FailIf(
            hudQuantity is not { } hud || hud.TensTile != expectedTens ||
            hud.OnesTile != expectedOnes || hud.Position != new Vector2(16, 8) ||
            inventoryQuantity is not { } menu || menu.TensTile != expectedTens ||
            menu.OnesTile != expectedOnes || menu.Attributes != 0x07,
            "drawTreasureExtraTiles mode $01 did not expose both selected-seed BCD digits.");

        _player.AdvanceSeedSatchelForValidation(7);
        FailIf(
            !_player.IsUsingSeedSatchel || _player.SeedSatchelFrame != 7,
            "LINK_ANIM_MODE_21 ended before its eighth update.");
        _player.AdvanceSeedSatchelForValidation(1);
        FailIf(_player.IsUsingSeedSatchel, "LINK_ANIM_MODE_21 did not end on update 8.");

        int activeSeedAmount = _inventory.EmberSeeds;
        int activeSeedCount = _entities.Entities<EmberSeedEffect>().Count;
        _player.StartSeedSatchelActionForValidation(Vector2.Left);
        FailIf(
            _player.IsUsingSeedSatchel ||
            _inventory.EmberSeeds != activeSeedAmount ||
            _entities.Entities<EmberSeedEffect>().Count != activeSeedCount,
            "ITEM_SEED_SATCHEL allocated or consumed ammo while its first seed was still active.");

        GD.Print("Validated ITEM_SEED_SATCHEL immediate BCD-20 grant/persistence, quantity overlays, " +
            "owned-seed/Mystery selection submenu, distinct inventory/equipped icon " +
            "sheets and equipped palette transform, " +
            "four offsets, Link pose, one-active-seed cap, Ember flight/Z, " +
            "fixed-bank-1 flame OAM/sounds, break effects, direct ROOMFLAG-$80 tree ignition, " +
            "and room 0:48's watcher-backed permanent tree removal across re-entry/save reload.");
    }

    private void ValidateSeedInventorySubmenu()
    {
        MenuPresentationDatabase layouts = MenuPresentationDatabase.Shared;
        InventoryItemSubmenuLayout twoOptions =
            layouts.InventoryItemSubmenu(2);
        InventoryItemSubmenuLayout fiveOptions =
            layouts.InventoryItemSubmenu(5);
        FailIf(
            twoOptions.MaxWidth != 8 ||
            !twoOptions.Positions.Select(position => position.RawX)
                .SequenceEqual([0x07, 0x0b]) ||
            fiveOptions.MaxWidth != 0x10 ||
            !fiveOptions.Positions.Select(position => position.RawX)
                .SequenceEqual([0x03, 0x06, 0x09, 0x0c, 0x0f]) ||
            !layouts.InventorySeedSubmenuSprites
                .Select(sprite => sprite.Tile)
                .SequenceEqual([0x06, 0x08, 0x0a, 0x0c, 0x0e]) ||
            !layouts.InventorySeedSubmenuSprites
                .Select(sprite => sprite.Attributes)
                .SequenceEqual([0x0a, 0x0b, 0x09, 0x09, 0x08]) ||
            layouts.InventorySeedSubmenuSprites.Any(
                sprite => sprite.VramBank != 1),
            "Imported inventoryMenuState2 widths, table_5ae5 positions, or " +
            "seedAndHarpSpriteTable seed OAM/VRAM-bank selection diverged " +
            "from bank2.s.");

        OracleSaveData save = OracleSaveData.CreateStandardGame();
        var inventory = new InventoryState(_treasures, save);
        inventory.GiveTreasure(TreasureDatabase.TreasureSeedSatchel, 0);
        inventory.GiveTreasure(
            TreasureDatabase.TreasureEmberSeeds + 4, 0x20);
        // New button items occupy B first; move the Satchel into storage so
        // the inventory cursor can exercise inventoryMenuState2.
        inventory.SwapStorageSlotWithButton(0, isA: false);
        FailIf(
            inventory.StorageItemAt(0) != InventoryState.ItemSeedSatchel ||
            !inventory.ObtainedSeedTypes().SequenceEqual([0, 4]) ||
            inventory.MysterySeeds != 0x20,
            "Seed Satchel/Mystery grants did not produce the source ordered " +
            "owned-seed list [Ember, Mystery].");

        var screen = new InventoryScreen
        {
            Name = "SeedInventorySubmenuValidation",
            Visible = false
        };
        AddChild(screen);
        screen.Initialize(_treasures, inventory);
        screen.Open();
        var menu = new InventoryMenuController(
            screen,
            _saveQuitScreen,
            _menuLifecycle,
            () => true,
            () => true,
            () => SaveResult.Succeeded,
            () => { },
            _sound.PlaySound);
        int selectRequests =
            _sound.PlayRequestsFor(OracleSoundEngine.SndSelectItem);
        FailIf(
            menu.EquipToAForValidation() ||
            !screen.ItemSubmenuActive ||
            screen.ItemSubmenuReady ||
            screen.ItemSubmenuWidth != 0 ||
            screen.ItemSubmenuHeight != 1 ||
            screen.ItemSubmenuOptionForValidation != 0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSelectItem) !=
                selectRequests,
            "Equipping a Satchel with two owned seed types did not enter " +
            "inventoryMenuState2 without swapping or playing SND_SELECTITEM.");
        for (int update = 0; update < 14; update++)
            menu.UpdateItemSubmenuForValidation(HarpFrame);
        FailIf(
            screen.ItemSubmenuReady ||
            screen.ItemSubmenuWidth != 8 ||
            screen.ItemSubmenuHeight != 4,
            "Two-seed Satchel submenu reached input before source update 15.");
        menu.UpdateItemSubmenuForValidation(HarpFrame);
        FailIf(
            !screen.ItemSubmenuReady ||
            screen.ActiveTextKey != _treasures
                .GetButtonDisplay(TreasureDatabase.TreasureEmberSeeds, inventory)
                .TextLow,
            "Two-seed Satchel submenu was not ready with Ember text on " +
            "source update 15.");

        int moveRequests =
            _sound.PlayRequestsFor(OracleSoundEngine.SndMenuMove);
        FailIf(
            !menu.MoveItemSubmenuForValidation(-1) ||
            screen.ItemSubmenuIndex != 1 ||
            screen.ItemSubmenuOptionForValidation != 4 ||
            screen.ActiveTextKey != _treasures
                .GetButtonDisplay(
                    TreasureDatabase.TreasureEmberSeeds + 4, inventory)
                .TextLow ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndMenuMove) !=
                moveRequests + 1,
            "Satchel submenu did not wrap from Ember to the non-contiguous " +
            "owned Mystery Seed option and select its text.");
        FailIf(
            !menu.ConfirmItemSubmenuForValidation() ||
            screen.ItemSubmenuActive ||
            inventory.SatchelSelectedSeeds != 4 ||
            inventory.EquippedA != InventoryState.ItemSeedSatchel ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSelectItem) !=
                selectRequests + 1,
            "Satchel submenu did not select Mystery Seeds, equip to A, and " +
            "request SND_SELECTITEM.");
        var quantity = screen.QuantityOverlayForValidation(
            InventoryState.ItemSeedSatchel);
        FailIf(
            quantity is not { } selectedQuantity ||
            selectedQuantity.TensTile != 0x12 ||
            selectedQuantity.OnesTile != 0x10 ||
            !OracleSaveData.TryDeserialize(
                save.Serialize(), out OracleSaveData? restored) ||
            new InventoryState(_treasures, restored!).SatchelSelectedSeeds != 4,
            "Selected Mystery Seed quantity/display or " +
            "wSatchelSelectedSeeds persistence regressed.");
        screen.QueueFree();
    }

    private void ValidateSwordBush()
    {
        OracleRandomResult ExpectedNextRandom()
        {
            var replay = new OracleRandom();
            for (int call = 0; call < _random.Calls; call++)
                replay.Next();
            return replay.Next();
        }

        int SoundFor(OracleRandomResult result) => (result.Value & 0x07) switch
        {
            0 or 3 or 4 or 6 or 7 => OracleSoundEngine.SndSwordSlash,
            1 or 5 => OracleSoundEngine.SndUnknown5,
            _ => OracleSoundEngine.SndBoomerang
        };

        void StepEntities(int count = 1)
        {
            for (int frame = 0; frame < count; frame++)
                _entities.Update(1.0 / 60.0, _player);
        }

        LoadBushValidationRoom();
        Vector2 bushPoint = new(24, 56);
        FailIf(_currentRoom.GetMetatile(bushPoint) != 0xc5, "Expected overworld bush $c5 in room 69 at $31.");
        Vector2 objectPosition = _player.Position;
        _sound.ClearPlayRequestAudit();
        int randomCalls = _random.Calls;
        OracleRandomResult expectedRandom = ExpectedNextRandom();
        _player.StartSwordAttack();
        int slashRequests = _sound.PlayRequestsFor(OracleSoundEngine.SndSwordSlash) +
            _sound.PlayRequestsFor(OracleSoundEngine.SndUnknown5) +
            _sound.PlayRequestsFor(OracleSoundEngine.SndBoomerang);
        FailIf(
            slashRequests != 1 || _player.SwordState != SwordActionState.Swing ||
            _player.SwordStateFrame != 0 || _player.SwordArcIndex != 0 ||
            _player.SwordKnockbackStrength != EnemyKnockbackStrength.Low ||
            _random.Calls != randomCalls + 1 || _random.LastResult != expectedRandom ||
            _sound.PlayRequestsFor(SoundFor(expectedRandom)) != 1,
            "Starting ITEM_SWORD did not select one entry from the original 8-sound table " +
            "from shared RNG and initialize LINK_ANIM_MODE_22 at sword arc $00.");
        _player.AdvanceSwordForValidation(2, buttonHeld: false);
        _sound.ClearPlayRequestAudit();
        randomCalls = _random.Calls;
        _player.StartSwordAttack();
        FailIf(
            _player.SwordCanRestart || _player.SwordStateFrame != 2 ||
            _random.Calls != randomCalls ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSwordSlash) +
            _sound.PlayRequestsFor(OracleSoundEngine.SndUnknown5) +
            _sound.PlayRequestsFor(OracleSoundEngine.SndBoomerang) != 0,
            "The protected first three sword updates accepted an equal-priority restart.");
        _player.AdvanceSwordForValidation(1, buttonHeld: false);
        FailIf(
            !_player.SwordCanRestart,
            "The sword did not become restartable when animation parameter `$02 cleared enabled bit 7.");
        randomCalls = _random.Calls;
        expectedRandom = ExpectedNextRandom();
        _player.StartSwordAttackForValidation(Vector2.Right);
        slashRequests = _sound.PlayRequestsFor(OracleSoundEngine.SndSwordSlash) +
            _sound.PlayRequestsFor(OracleSoundEngine.SndUnknown5) +
            _sound.PlayRequestsFor(OracleSoundEngine.SndBoomerang);
        FailIf(
            slashRequests != 1 || _player.SwordStateFrame != 0 ||
            _player.SwordArcIndex != 1 || _player.FacingVector != Vector2I.Right ||
            _player.SwordCanRestart || _random.Calls != randomCalls + 1 ||
            _random.LastResult != expectedRandom ||
            _sound.PlayRequestsFor(SoundFor(expectedRandom)) != 1,
            "An equal-priority sword press did not consume shared RNG, restart, and " +
            "retarget the single swing after update 3.");
        _player.AdvanceSwordForValidation(3, buttonHeld: false);
        _sound.ClearPlayRequestAudit();
        randomCalls = _random.Calls;
        expectedRandom = ExpectedNextRandom();
        _player.StartSwordAttackForValidation(Vector2.Up);
        slashRequests = _sound.PlayRequestsFor(OracleSoundEngine.SndSwordSlash) +
            _sound.PlayRequestsFor(OracleSoundEngine.SndUnknown5) +
            _sound.PlayRequestsFor(OracleSoundEngine.SndBoomerang);
        FailIf(
            slashRequests != 1 || _player.SwordStateFrame != 0 ||
            _player.SwordArcIndex != 0 || _player.FacingVector != Vector2I.Up ||
            _player.SwordCanRestart || _random.Calls != randomCalls + 1 ||
            _random.LastResult != expectedRandom ||
            _sound.PlayRequestsFor(SoundFor(expectedRandom)) != 1,
            "Spammed sword input did not consume shared RNG and retarget a subsequent swing upward.");
        FailIf(
            _player.AttackSpriteOrigin != new Vector2(-8, -8),
            $"Sword frame $ac displaced Link from the standard OAM origin: {_player.AttackSpriteOrigin}.");
        FailIf(
            _player.SwordSpritePosition != new Vector2(16, -4),
            $"Sword arc phase $00 did not include the child item's -2 Z draw offset: {_player.SwordSpritePosition}.");
        _player._Process(7.0 / 60.0);
        FailIf(_player.Position != objectPosition, "Swinging the sword changed Link's object position.");
        FailIf(
            _player.AttackSpriteOrigin != new Vector2(-8, -11),
            $"Sword frame $b4 did not apply only its original OAM $08 pose offset: {_player.AttackSpriteOrigin}.");
        FailIf(
            _player.SwordSpritePosition != new Vector2(-4, -19),
            $"Sword arc phase $08 did not include the child item's -2 Z draw offset: {_player.SwordSpritePosition}.");
        FailIf(
            _currentRoom.GetMetatile(bushPoint) != 0x3a,
            "The level-1 sword did not replace bush $c5 with ground $3a.");
        FailIf(_currentRoom.IsSolid(bushPoint), "The cut bush's replacement tile remained solid.");
        GrassDebrisEffect bushDebris =
            _entities.Entities<GrassDebrisEffect>().SingleOrDefault()!;
        FailIf(
            bushDebris is null,
            "Cutting overworld bush $c5 did not create one " +
            "INTERAC_GRASSDEBRIS $00.");
        using (Image firstGrassDebrisFrame = bushDebris.CurrentTexture.GetImage())
        {
            ulong firstGrassDebrisHash =
                OracleGraphicsCache.PixelHash(firstGrassDebrisFrame);
            FailIf(
                bushDebris.Position != bushPoint ||
                firstGrassDebrisHash != 0xb2317fc7033b5eb0UL,
                "INTERAC_GRASSDEBRIS $00 did not use its tile-centered " +
                "first four-piece OAM frame " +
                $"(position={bushDebris.Position}, hash={firstGrassDebrisHash:x16}).");
        }
        var underwaterDebris = new GrassDebrisEffect();
        underwaterDebris.Initialize(bushPoint, underwater: true);
        using (Image underwaterGrassDebrisFrame =
            underwaterDebris.CurrentTexture.GetImage())
        {
            ulong underwaterGrassDebrisHash =
                OracleGraphicsCache.PixelHash(underwaterGrassDebrisFrame);
            FailIf(
                underwaterGrassDebrisHash != 0x00748b1a794afda4UL,
                "Underwater INTERAC_GRASSDEBRIS $00 did not apply " +
                "its specialized OBJ palette 6 " +
                $"(hash={underwaterGrassDebrisHash:x16}).");
        }
        underwaterDebris.Free();
        FailIf(
            bushDebris.Flickers ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndCutGrass) != 0,
            "The bush's non-flickering debris updated before its " +
            "interaction state-0 update.");
        StepEntities();
        FailIf(
            bushDebris.ElapsedUpdates != 1 ||
            bushDebris.AnimationFrame != 0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndCutGrass) != 1,
            "INTERAC_GRASSDEBRIS state 0 did not request SND_CUTGRASS " +
            "without advancing animation 0.");
        for (int frame = 1; frame <= 8; frame++)
        {
            StepEntities(4);
            FailIf(
                bushDebris.AnimationFrame != frame || bushDebris.Finished,
                $"INTERAC_GRASSDEBRIS did not enter animation frame " +
                $"{frame} after {frame * 4} state-1 updates.");
        }
        FailIf(
            (bushDebris.CurrentParameter & 0x80) == 0 ||
            bushDebris.ElapsedUpdates != 33,
            "INTERAC_GRASSDEBRIS did not expose terminal parameter $ff " +
            "after its eight 4-update frames.");
        StepEntities();
        FailIf(
            !bushDebris.Finished ||
            bushDebris.ElapsedUpdates != 34 ||
            _entities.Entities<GrassDebrisEffect>().Count != 0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndCutGrass) != 1,
            "INTERAC_GRASSDEBRIS did not delete one update after its " +
            "terminal frame without replaying SND_CUTGRASS.");

        // Breakable mode $00 is cuttable grass $f8. Effect bit 4 becomes
        // subid bit 0 on INTERAC_GRASSDEBRIS and flickers every update.
        _currentRoom.SetPositionTileAndCollision(
            bushPoint, 0xf8, null, (long)_animationTicks);
        _sound.ClearPlayRequestAudit();
        FailIf(
            !_combat.ApplySwordTileHit(
                _player, direction: 0, swordPoke: false) ||
            _currentRoom.GetMetatile(bushPoint) != 0x3a ||
            _entities.Entities<GrassDebrisEffect>() is not
                [{ Flickers: true }],
            "Cutting grass $f8 did not apply effect $10 as flickering " +
            "INTERAC_GRASSDEBRIS $00.");
        GrassDebrisEffect grassDebris =
            _entities.Entities<GrassDebrisEffect>().Single();
        StepEntities();
        FailIf(
            _sound.PlayRequestsFor(OracleSoundEngine.SndCutGrass) != 1 ||
            grassDebris.ElapsedUpdates != 1,
            "Flickering grass debris did not retain the state-0 " +
            "SND_CUTGRASS boundary.");
        StepEntities();
        bool firstFlickerVisibility = grassDebris.Visible;
        StepEntities();
        FailIf(
            grassDebris.Visible == firstFlickerVisibility ||
            grassDebris.AnimationFrame != 0,
            "INTERAC_GRASSDEBRIS subid $01 did not toggle visibility " +
            "on consecutive original updates.");
        StepEntities(30);
        FailIf(
            (grassDebris.CurrentParameter & 0x80) == 0 ||
            grassDebris.ElapsedUpdates != 33,
            "Flickering grass debris changed the shared 32-update " +
            "animation boundary.");
        StepEntities();
        FailIf(
            _entities.Entities<GrassDebrisEffect>().Count != 0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndCutGrass) != 1,
            "Flickering grass debris did not delete silently after its " +
            "terminal update.");

        // Complete LINK_ANIM_MODE_22 while preserving the initiating button.
        // State 6 must re-enable movement but keep turning disabled and expose
        // the fourth normal swordArcData row continuously while charging.
        _player.AdvanceSwordForValidation(9, buttonHeld: true);
        FailIf(
            _player.SwordState != SwordActionState.Swing ||
            _player.SwordStateFrame != 16,
            "The sword swing ended before its 17th update.");
        _player.AdvanceSwordForValidation(1, buttonHeld: true);
        FailIf(
            _player.SwordState != SwordActionState.Held ||
            !_player.SwordAllowsMovement || !_player.SwordCanRestart || _player.SwordArcIndex != 12 ||
            _player.SwordKnockbackStrength != EnemyKnockbackStrength.Low ||
            _player.SwordSpritePosition != new Vector2(-4, -12),
            "Holding the sword button did not enter the movable ITEMCOLLISION_SWORD_HELD state " +
            "with the original up-facing arc $0c.");
        FailIf(
            Player.GetHeldSwordBodyAnimationFrameForValidation(
                SwordActionState.Held, walking: true, walkTime: 0.0f) != 0 ||
            Player.GetHeldSwordBodyAnimationFrameForValidation(
                SwordActionState.Held, walking: true, walkTime: 0.10f) != 1 ||
            Player.GetHeldSwordBodyAnimationFrameForValidation(
                SwordActionState.Charged, walking: true, walkTime: 0.20f) != 0 ||
            Player.GetHeldSwordBodyAnimationFrameForValidation(
                SwordActionState.Held, walking: false, walkTime: 0.10f) != 0 ||
            Player.GetHeldSwordBodyAnimationFrameForValidation(
                SwordActionState.Swing, walking: true, walkTime: 0.10f) != -1,
            "Held/charged sword state did not select Link's ordinary standing/walking body.");
        FailIf(
            Player.GetSwordSpritePositionForValidation(13) != new Vector2(12, 0) ||
            Player.GetSwordSpritePositionForValidation(15) != new Vector2(-12, 0),
            "Held horizontal sword sprites did not apply the child item's -2 Z draw offset.");

        _player.AdvanceSwordForValidation(40, buttonHeld: true);
        FailIf(
            _player.SwordState != SwordActionState.Held ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndChargeSword) != 0,
            "Sword counter `$28 charged without the original underflow update.");
        _player.AdvanceSwordForValidation(1, buttonHeld: true);
        FailIf(
            _player.SwordState != SwordActionState.Charged ||
            _player.SwordCanRestart || _player.SwordUsesChargedPalette ||
            _player.SwordKnockbackStrength != EnemyKnockbackStrength.Low ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndChargeSword) != 1,
            "The 41st held update did not enter the charged state with SND_CHARGE_SWORD.");
        _player.AdvanceSwordForValidation(3, buttonHeld: true);
        FailIf(
            _player.SwordUsesChargedPalette,
            "The charged sword selected palette 5 before counter bit 2 was set.");
        _player.AdvanceSwordForValidation(1, buttonHeld: true);
        FailIf(
            !_player.SwordUsesChargedPalette,
            "The charged sword did not select palette 5 when counter bit 2 became set.");

        _player.AdvanceSwordForValidation(1, buttonHeld: false);
        FailIf(
            _player.SwordState != SwordActionState.Spin ||
            _player.SwordStateFrame != 0 || _player.SwordAllowsMovement ||
            _player.SwordArcIndex != 16 ||
            _player.SwordKnockbackStrength != EnemyKnockbackStrength.High ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSwordSpin) != 1,
            "Releasing a charged up-facing sword did not begin the immobilized arc `$10 spin with SND_SWORDSPIN.");
        _player.AdvanceSwordForValidation(2, buttonHeld: false);
        FailIf(
            _player.SwordArcIndex != 16,
            "Swordspin arc `$10 did not retain its original 3-update duration.");
        _player.AdvanceSwordForValidation(1, buttonHeld: false);
        FailIf(_player.SwordArcIndex != 17, "Swordspin did not enter diagonal arc `$11 on update 3.");
        _player.AdvanceSwordForValidation(2, buttonHeld: false);
        FailIf(_player.SwordArcIndex != 18, "Swordspin did not enter right-facing arc `$12 on update 5.");
        _player.AdvanceSwordForValidation(17, buttonHeld: false);
        FailIf(
            _player.SwordState != SwordActionState.Spin ||
            _player.SwordStateFrame != 22 || _player.SwordArcIndex != 16,
            "Swordspin did not retain its wrapped arc through update 22.");
        _player.AdvanceSwordForValidation(1, buttonHeld: false);
        FailIf(_player.IsAttacking, "Swordspin did not end on its original 23rd update.");

        // A held sword pressed into a full wall switches to LINK_ANIM_MODE_1f,
        // clears weapon collision for 12 updates, and emits the ordinary clink.
        _currentRoom.SetPositionTileAndCollision(
            bushPoint, 0x3a, 0x0f, (long)_animationTicks);
        _player.WarpTo(new Vector2(bushPoint.X, 66));
        _player.Face(Vector2I.Up);
        _player.StartSwordAttack();
        _player.AdvanceSwordForValidation(17, buttonHeld: true);
        _sound.ClearPlayRequestAudit();
        _combatEffectAudit.Clear();
        _player.AdvanceSwordForValidation(1, buttonHeld: true, movementInput: Vector2.Up);
        FailIf(
            _player.SwordState != SwordActionState.Poke ||
            !_player.SwordCanRestart || _player.GetSwordHitbox().Size != Vector2.Zero ||
            _player.AttackSpriteOrigin != new Vector2(-8, -11) ||
            _player.SwordSpritePosition != new Vector2(-4, -19) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndClink) != 1,
            "Held-sword wall pressure did not enter the collision-disabled 12-update poke and play SND_CLINK.");
        ClinkEffect? ordinaryClink = _combatEffectAudit.LastClinkEffect;
        Vector2 expectedClinkPosition = _player.Position + new Vector2(0, -14);
        FailIf(
            _combatEffectAudit.ClinkEffectsSpawned != 1 || ordinaryClink is null ||
            ordinaryClink.Position != expectedClinkPosition || !ordinaryClink.Flickers ||
            ordinaryClink.DurationFrames != 8 || ordinaryClink.AnimationFrame != 0 ||
            !ordinaryClink.EffectVisible,
            "Ordinary wall pressure did not spawn flickering INTERAC_CLINK at the up-facing `$f2/$00 probe.");
        ordinaryClink.AdvanceForValidation(1.0 / 60.0);
        FailIf(
            ordinaryClink.EffectVisible || ordinaryClink.AnimationFrame != 0,
            "INTERAC_CLINK did not flicker during its first 4-update frame.");
        ordinaryClink.AdvanceForValidation(3.0 / 60.0);
        FailIf(
            !ordinaryClink.EffectVisible || ordinaryClink.AnimationFrame != 1,
            "INTERAC_CLINK did not enter its second OAM frame after 4 updates.");
        _player.AdvanceSwordForValidation(11, buttonHeld: true);
        FailIf(
            _player.SwordState != SwordActionState.Poke ||
            _player.SwordStateFrame != 11,
            "LINK_ANIM_MODE_1f ended before update 12.");
        _player.AdvanceSwordForValidation(1, buttonHeld: true);
        FailIf(
            _player.SwordState != SwordActionState.Held ||
            _player.SwordArcIndex != 12,
            "A wall poke did not reinitialize the held sword after update 12.");
        _player.AdvanceSwordForValidation(1, buttonHeld: false);
        FailIf(_player.IsAttacking, "Releasing an uncharged held sword did not clear the parent item.");

        // Bombable wall tiles bypass the poke-only ordinary clink condition.
        _currentRoom.SetPositionTileAndCollision(
            bushPoint, 0xc1, null, (long)_animationTicks);
        _sound.ClearPlayRequestAudit();
        _combatEffectAudit.Clear();
        _player.StartSwordAttack();
        _player.AdvanceSwordForValidation(6, buttonHeld: false);
        ClinkEffect? bombableClink = _combatEffectAudit.LastClinkEffect;
        FailIf(
            _sound.PlayRequestsFor(OracleSoundEngine.SndClink2) != 1 ||
            _combatEffectAudit.ClinkEffectsSpawned != 1 || bombableClink is null ||
            bombableClink.Position != expectedClinkPosition || bombableClink.Flickers ||
            !bombableClink.EffectVisible,
            "Bombable overworld tile `$c1 did not play SND_CLINK2 and spawn non-flickering INTERAC_CLINK.");
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
            FailIf(
                actual != expected,
                $"swordArcData row `${index:x2} mismatch: expected {expected}, got {actual}.");
        }

        // wScrollMode $08 freezes initialized item objects. The held sword
        // parent and its locked facing must therefore survive both ends of a
        // scrolling transition without charging or observing button release.
        _player.Face(Vector2I.Up);
        _player.StartSwordAttack();
        _player.AdvanceSwordForValidation(17, buttonHeld: true);
        _transitions.BeginScroll(_player, Vector2I.Right, 0x6a);
        FailIf(
            !_transitions.ScrollActive ||
            _player.SwordState != SwordActionState.Held ||
            _player.FacingVector != Vector2I.Up || _player.SwordArcIndex != 12,
            "Scrolling right did not preserve the up-facing held sword parent item.");
        _player._Process(1.0);
        FailIf(
            _player.SwordState != SwordActionState.Held ||
            _player.SwordStateFrame != 0 || _player.FacingVector != Vector2I.Up,
            "wScrollMode $08 did not freeze the held sword for the scrolling transition.");
        _transitions.UpdateScroll(1.0);
        FailIf(
            _transitions.ScrollActive ||
            _player.SwordState != SwordActionState.Held ||
            _player.FacingVector != Vector2I.Up || _player.SwordArcIndex != 12,
            "Finishing the scrolling transition cleared or redirected the held sword.");
        _player.AdvanceSwordForValidation(1, buttonHeld: false);

        GD.Print(
            "Validated ITEM_SWORD's 17-update swing/3-update directional restart gate, " +
            "held collision/movement and scrolling-transition persistence, " +
            "41-update charge, " +
            "held/charged standing/walking body, child-item Z/layer rendering, charged palette cadence, " +
            "12-update wall poke/clinks with 8-update INTERAC_CLINK sprites, 23-update swordspin, " +
            "shared-RNG slash sounds, blocked-restart RNG preservation, exact grass/bush debris, " +
            "and all 24 swordArcData hitboxes.");
    }

    private void ValidateHealth()
    {
        _dialogue.Close();
        _player.RefillHealth();
        _statusBar.SynchronizeHealth();

        FailIf(
            _player.HealthQuarters != 12 || _hud.HealthQuarters != 12 ||
            _hud.MaxHealthQuarters != _player.MaxHealthQuarters,
            "Expected Link and the HUD to start with three full hearts.");

        _player.ApplyDamage(1);
        FailIf(
            _player.HealthQuarters != 11 || _hud.HealthQuarters != 12,
            "Direct quarter-heart damage changed the HUD before its update.");
        _statusBar.Update(1.0 / 60.0);
        FailIf(_hud.HealthQuarters != 11, "Displayed damage did not subtract one quarter per update.");

        _player.Heal(1);
        FailIf(
            _player.HealthQuarters != 12 || _hud.HealthQuarters != 11,
            "Direct healing changed the HUD before its divisor-4 update.");
        for (int update = 0; update < 4 && _hud.HealthQuarters != 12; update++)
            _statusBar.Update(1.0 / 60.0);
        FailIf(_hud.HealthQuarters != 12, "Displayed healing did not add a quarter on a divisor-4 update.");

        _activeGroup = 0;
        ClearDeactivatedWarp();
        _currentRoom = _world.LoadRoom(_activeGroup, 0x03);
        _roomView.SetRoom(_currentRoom.Texture);
        Vector2 safe = new(56, 8);
        _player.WarpTo(safe);
        _player.WarpTo(new Vector2(8, 24), recordSafe: false);

        ValidateDrowningSequence(safe, HazardType.Lava);
        FailIf(
            _player.HealthQuarters != 10 || _hud.HealthQuarters != 12,
            "Lava hazard changed displayed health before updateStatusBar_body.");
        _statusBar.Update(2.0 / 60.0);
        FailIf(
            _hud.HealthQuarters != 10,
            "Lava hazard did not synchronize its delayed half-heart damage to the HUD.");

        GD.Print("Validated quarter-heart health, divisor-4 healing display/SND_GAINHEART cadence, " +
            "per-update damage display, and delayed half-heart terrain damage.");
    }

    private void ValidatePlayerDamageAndDeath()
    {
        OracleSaveData damageSave = OracleSaveData.CreateStandardGame();
        var damageInventory = new InventoryState(_treasures, damageSave);
        var damageWorld = new ValidationRingPlayerWorld { FrameCounter = 0 };
        var damagePlayer = new Player { Name = "DamageBlinkValidationPlayer" };
        AddChild(damagePlayer);
        damagePlayer.Initialize(
            damageWorld,
            damageInventory,
            new Vector2(80, 80),
            new OracleRandom());

        int healthBefore = damagePlayer.HealthQuarters;
        FailIf(
            !damagePlayer.ApplyEnemyContactDamage(
                new Vector2(64, 80), quarters: 1) ||
            damagePlayer.HealthQuarters != healthBefore - 1 ||
            !damagePlayer.DamagePaletteActive ||
            damagePlayer.LinkAtlasPixelHash == damagePlayer.DamageLinkAtlasPixelHash ||
            Player.RecolorLinkPixel(
                Color.Color8(85, 85, 85),
                damagePalette: true) !=
                new Color(0x1f / 31.0f, 0x16 / 31.0f, 0x06 / 31.0f) ||
            damageWorld.Sounds.Count(
                sound => sound == OracleSoundEngine.SndDamageLink) != 1,
            "Accepted Link damage did not select standard sprite palette 5 " +
            "or request SND_DAMAGE_LINK $5f.");
        damageWorld.FrameCounter = 4;
        FailIf(
            damagePlayer.DamagePaletteActive ||
            damagePlayer.ApplyEnemyContactDamage(
                new Vector2(64, 80), quarters: 1) ||
            damageWorld.Sounds.Count(
                sound => sound == OracleSoundEngine.SndDamageLink) != 1,
            "Link's source bit-2 damage flash or contact invincibility regressed.");
        damagePlayer.Free();

        OracleSaveData potionSave = OracleSaveData.CreateStandardGame();
        var potionInventory = new InventoryState(_treasures, potionSave);
        potionInventory.GiveTreasure(TreasureDatabase.TreasurePotion, 1);
        var potionWorld = new ValidationRingPlayerWorld();
        var potionPlayer = new Player { Name = "PotionDeathValidationPlayer" };
        AddChild(potionPlayer);
        potionPlayer.Initialize(
            potionWorld,
            potionInventory,
            new Vector2(80, 80),
            new OracleRandom());
        FailIf(
            !potionPlayer.ApplyDamage(potionPlayer.MaxHealthQuarters) ||
            potionPlayer.IsDying ||
            potionPlayer.HealthQuarters != potionPlayer.MaxHealthQuarters ||
            potionInventory.HasTreasure(TreasureDatabase.TreasurePotion),
            "TREASURE_POTION $2f did not refill Link and clear itself " +
            "before wLinkDeathTrigger.");
        potionPlayer.Free();

        OracleSaveData deathSave = OracleSaveData.CreateStandardGame();
        var deathInventory = new InventoryState(_treasures, deathSave);
        var deathWorld = new ValidationRingPlayerWorld();
        var deathPlayer = new Player { Name = "DeathValidationPlayer" };
        AddChild(deathPlayer);
        deathPlayer.Initialize(
            deathWorld,
            deathInventory,
            new Vector2(80, 80),
            new OracleRandom());
        int gameOverRequests = 0;
        deathPlayer.GameOverRequested += () => gameOverRequests++;
        FailIf(
            !deathPlayer.ApplyEnemyContactDamage(
                new Vector2(64, 80),
                deathPlayer.MaxHealthQuarters) ||
            !deathPlayer.IsDying ||
            deathPlayer.DeathAnimationActive,
            "Lethal accepted contact did not arm LINK_STATE_DYING.");

        for (int update = 0; update < 15; update++)
            deathPlayer._PhysicsProcess(1.0 / 60.0);
        FailIf(
            deathPlayer.DeathAnimationActive ||
            deathPlayer.KnockbackFrames != 0 ||
            deathWorld.Sounds.Count(
                sound => sound == OracleSoundEngine.SndCtrlSlowFadeOut) != 1 ||
            deathWorld.Sounds.Count(
                sound => sound == OracleSoundEngine.SndLinkDead) != 0,
            "LINK_STATE_DYING did not wait through all 15 knockback updates " +
            "after starting SNDCTRL_SLOW_FADEOUT.");

        deathPlayer._PhysicsProcess(1.0 / 60.0);
        FailIf(
            !deathPlayer.DeathAnimationActive ||
            deathPlayer.DeathAnimationFrame != 2 ||
            deathPlayer.DeathAnimationCounter != 8 ||
            deathPlayer.DeathSpinLoopsRemaining != 4 ||
            deathPlayer.DeathAtlasPixelHash == 0 ||
            deathWorld.Sounds.Count(
                sound => sound == OracleSoundEngine.SndLinkDead) != 1,
            "Link's death spin did not initialize graphics $02 for eight " +
            "updates with SND_LINK_DEAD $64.");

        var displayedTwirlFrames = new List<int>
        {
            deathPlayer.DeathAnimationFrame
        };
        for (int update = 1; update < 135; update++)
        {
            deathPlayer._PhysicsProcess(1.0 / 60.0);
            displayedTwirlFrames.Add(deathPlayer.DeathAnimationFrame);
        }
        var expectedTwirlFrames = new List<int>();
        expectedTwirlFrames.AddRange(Enumerable.Repeat(2, 8));
        for (int loop = 0; loop < 3; loop++)
        {
            expectedTwirlFrames.AddRange(Enumerable.Repeat(1, 8));
            expectedTwirlFrames.AddRange(Enumerable.Repeat(0, 8));
            expectedTwirlFrames.AddRange(Enumerable.Repeat(3, 8));
            expectedTwirlFrames.AddRange(Enumerable.Repeat(2, 8));
        }
        expectedTwirlFrames.AddRange(Enumerable.Repeat(1, 8));
        expectedTwirlFrames.AddRange(Enumerable.Repeat(0, 8));
        expectedTwirlFrames.AddRange(Enumerable.Repeat(3, 8));
        expectedTwirlFrames.AddRange(Enumerable.Repeat(2, 7));
        FailIf(
            !displayedTwirlFrames.SequenceEqual(expectedTwirlFrames) ||
            deathPlayer.DeathAnimationFrame == 4 ||
            deathPlayer.DeathAnimationSequenceIndex != 4 ||
            deathPlayer.DeathAnimationCounter != 1 ||
            deathPlayer.DeathSpinLoopsRemaining != 1 ||
            gameOverRequests != 0,
            "animationData19e7b did not display its initial eight-update " +
            "$02 frame, three complete 8/8/8/(7+1) loops, and the final " +
            "8/8/8/7 pre-marker loop.");
        deathPlayer._PhysicsProcess(1.0 / 60.0);
        FailIf(
            deathPlayer.DeathAnimationFrame != 4 ||
            deathPlayer.DeathAnimationCounter != 0x4c ||
            deathPlayer.DeathSpinLoopsRemaining != 0 ||
            gameOverRequests != 0,
            "The fourth Link spin marker on animation update 135 did not " +
            "select the 76-update LINK_ANIM_MODE_COLLAPSED frame.");

        for (int update = 0; update < 75; update++)
            deathPlayer._PhysicsProcess(1.0 / 60.0);
        FailIf(
            gameOverRequests != 0 ||
            deathPlayer.DeathAnimationCounter != 1,
            "The collapsed Link pose ended before its 76th update.");
        deathPlayer._PhysicsProcess(1.0 / 60.0);
        FailIf(
            gameOverRequests != 1,
            "The collapsed Link pose did not request the game-over menu " +
            "on its terminal $ff animation parameter.");
        deathPlayer._PhysicsProcess(1.0);
        FailIf(gameOverRequests != 1, "Link requested game over more than once.");
        deathPlayer.Free();

        GD.Print(
            "Validated Link's palette-5 damage flash, contact invincibility, " +
            "Potion revival, 15-update lethal knockback, SND_LINK_DEAD, " +
            "the exact 135-update four-marker twirl cadence, 76-update collapse, " +
            "and one-shot game-over handoff.");
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

        FailIf(
            !waterChanged || !lavaChanged,
            $"Expected animated water and lava frames; water={waterChanged}, lava={lavaChanged}.");

        OracleRoomData waterfall = _world.LoadRoom(0, 0x45);
        const int settledWaterfallTick = 32;
        FailIf(
            waterfall.AnimationGroup != 0 ||
            !waterfall.HasAnimationOverride(4, settledWaterfallTick) ||
            !waterfall.HasAnimationOverride(236, settledWaterfallTick) ||
            !waterfall.HasAnimationOverride(238, settledWaterfallTick),
            "Waterfall animation did not preserve its three alternating VRAM destination writes.");

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
        FailIf(
            !Mathf.IsZeroApprox((float)_animationTicks),
            "Changing animation groups did not reset their shared clock.");
        _animationTicks = 13.0;
        source.UpdateAnimation((long)_animationTicks);
        FailIf(
            source.CurrentAnimationSignature == staleTargetSignature,
            "Animation phase-lock validation chose indistinguishable ticks.");

        _roomView.SetRoom(source.Texture);
        _entities.LoadRoom(_activeGroup, source);
        _player.WarpTo(new Vector2(source.Width + 2.0f, source.Height / 2.0f));
        _player.UpdatePushingState(Vector2.Right);
        CheckRoomExit(_player);
        FailIf(
            !IsTransitioning || _activeGroup != 0 || _currentRoom.Id != 0x56,
            "Room 0:55 did not begin its rightward scroll into 0:56.");
        FailIf(
            source.AnimationGroup != _currentRoom.AnimationGroup ||
            source.CurrentAnimationSignature != _currentRoom.CurrentAnimationSignature,
            "Outgoing and incoming water/waterfall tiles began the scroll in different phases.");

        ValidateLinkScrollsForOneTransitionFrame();
        FailIf(
            source.CurrentAnimationSignature != _currentRoom.CurrentAnimationSignature,
            "Animated-tile phases diverged during the frozen scroll.");
        FinishActiveScrollingTransitionForValidation();

        GD.Print("Validated disassembly-driven water and lava animation plus persistent " +
            "three-range waterfall VRAM updates and 0:55 -> 0:56 phase locking.");
    }
}
