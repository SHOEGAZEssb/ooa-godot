using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateRoom043Enemies()
    {
        var database = new EnemyDatabase();
        IReadOnlyList<RoomObjectRecord> rows = database.GetRoomObjects(0, 0x43);
        FailIf(rows.Count != 2 || rows[0] is not { Id: 0x08, SubId: 0, Count: 2, Kind: RoomObjectKind.RandomEnemy } ||
            rows[1] is not { Id: 0x18, SubId: 0, Count: 1, Kind: RoomObjectKind.RandomEnemy },
            "Room 0:43 lost its ordered two River Zoras $08:00 and one Buzz Blob $18:00.");
        LoadValidationRoom(0, 0x43);
        FailIf(_entities.Entities<RiverZoraCharacter>().Count != 2 ||
            _entities.Entities<BuzzBlobCharacter>().Count != 1,
            "Room 0:43 did not construct all three source enemies.");
        OracleRoomData room = _rooms.CurrentRoom;
        var random = new OracleRandom();
        var spawns = new List<RoomEntitySpawn>();
        var zora = new RiverZoraCharacter();
        zora.Initialize(database.ImportedEnemy(0x08), room, new Vector2(40, 40), random);
        zora.UpdateFrame(Vector2.Zero, spawns);
        FailIf(zora.State != 9 || zora.Visible || zora.CollisionEnabled || random.Calls != 0,
            "River Zora $08 state 0 consumed RNG or enabled presentation/collision.");
        int rejected = 0;
        for (int attempts = 0; zora.State == 9 && attempts < 512; attempts++)
        {
            int before = random.Calls;
            zora.UpdateFrame(Vector2.Zero, spawns);
            FailIf(random.Calls != before + 1, "River Zora $08 resurfacing did not consume one global RNG call.");
            if (zora.State == 9) rejected++;
        }
        FailIf(zora.State != 0x0a || zora.Counter != 48 || !zora.Visible || zora.CollisionEnabled ||
            room.GetMetatile(zora.Position) is < 0xf9 or > 0xfd ||
            ((int)zora.Position.X & 15) != 8 || ((int)zora.Position.Y & 15) != 8,
            $"River Zora $08 failed its source water-only search in 0:43 after {rejected} rejections.");
        for (int i = 0; i < 47; i++) zora.UpdateFrame(Vector2.Zero, spawns);
        FailIf(zora.Counter != 1 || zora.CollisionEnabled, "River Zora $08 surfaced before update 48.");
        zora.UpdateFrame(Vector2.Zero, spawns);
        FailIf(zora.State != 0x0b || !zora.CollisionEnabled || zora.AnimationIndex != 1,
            "River Zora $08 did not become vulnerable on surfacing update 48.");
        for (int i = 0; i < 56; i++) zora.UpdateFrame(Vector2.Zero, spawns);
        FailIf(spawns.OfType<ZoraFireSpawn>().Any(), "River Zora $08 shot before consuming animation parameter 1.");
        zora.UpdateFrame(Vector2.Zero, spawns);
        FailIf(spawns.OfType<ZoraFireSpawn>().Count() != 1, "River Zora $08 did not create PART_ZORA_FIRE $19.");
        int rngBeforeDive = random.Calls;
        for (int i = 0; i < 80 && zora.State == 0x0b; i++) zora.UpdateFrame(Vector2.Zero, spawns);
        FailIf(zora.State != 8 || zora.Visible || zora.CollisionEnabled ||
            zora.Counter is < 24 or > 55 || random.Calls != rngBeforeDive + 1 ||
            spawns.OfType<ZoraFireSpawn>().Count() != 1 || spawns.OfType<EnemySplashSpawn>().Count() != 1,
            "River Zora $08 failed its single shot, splash, or randomized underwater delay.");
        int hidden = zora.Counter;
        for (int i = 0; i < hidden; i++) zora.UpdateFrame(Vector2.Zero, spawns);
        FailIf(zora.State != 9 || random.Calls != rngBeforeDive + 1,
            "River Zora $08 attempted another water search on the hidden counter's zero update.");

        var blobRandom = new OracleRandom();
        var blob = new BuzzBlobCharacter();
        blob.Initialize(database.ImportedEnemy(0x18), room,
            _entities.Entities<BuzzBlobCharacter>().Single().Position, blobRandom);
        blob.UpdateFrame(null);
        FailIf(blob.State != 8 || blobRandom.Calls != 0, "Buzz Blob $18 state 0 consumed RNG.");
        blob.UpdateFrame(null);
        FailIf(blob.State != 9 || blob.Angle != 4 || blob.Counter != 80 || blobRandom.Calls != 1,
            $"Buzz Blob $18 lost high/low RNG masks; angle=${blob.Angle:x2}, counter={blob.Counter}.");
        Vector2 previous = blob.Position;
        blob.UpdateFrame(null);
        FailIf(blob.Counter != 79 || blob.Position == previous, "Buzz Blob $18 did not roam at SPEED_40.");
        Vector2 movement = blob.Position - previous;
        FailIf(Mathf.Abs(movement.X) != 45 / 256.0f || Mathf.Abs(movement.Y) != 45 / 256.0f,
            $"Buzz Blob $18 diagonal SPEED_40 must move $002d on each axis, got {movement}.");
        int health = blob.Health;
        var adapter = new BuzzBlobRoomEntity(blob, database.EnemyHandlers.ResolveHandler(rows[1]).CombatSource(rows[1], 0),
            _ => { }, (_, _, _) => { });
        FailIf(!adapter.ApplySwordHit(blob.CollisionBounds, blob.Position + Vector2.Down * 12, 2,
            EnemyKnockbackStrength.Normal, spawns) || blob.CollisionEnabled || blob.Health != health,
            "Buzz Blob $18 sword collision did not disable collisions without hurting it.");
        adapter.UpdateFrame(new RoomEntityFrame(_player, 0, false), spawns);
        FailIf(blob.State != 0x0a || blob.Counter != 60 || !_player.ElectricShockActive,
            "Buzz Blob $18 did not enter its 60-update electric shock state.");
        Color[,] originalPalettes = room.BackgroundPalettes.Capture();
        Vector2 frozenLink = _player.Position;
        _player.AdvanceApplicationUpdate();
        FailIf(_player.ElectricShockCounter != 45, "Link shock did not initialize its $2d counter.");
        for (int i = 0; i < 5; i++) _player.AdvanceApplicationUpdate();
        FailIf(_player.ElectricShockCounter != 40 || _player.Position != frozenLink || _player.Material is null ||
            room.BackgroundPalettes.Resolve(2, 2) != ElectricShockPalette.Background[2, 2],
            "Link shock did not freeze Link and load PALH_0c on counter $28.");
        for (int i = 0; i < 8; i++) _player.AdvanceApplicationUpdate();
        FailIf(_player.ElectricShockCounter != 32 || _player.Material is not null ||
            room.BackgroundPalettes.Resolve(2, 2) != originalPalettes[2, 2],
            "Link shock did not restore the captured palette on counter $20.");
        for (int i = 0; i < 31; i++) _player.AdvanceApplicationUpdate();
        FailIf(_player.ElectricShockCounter != 1 || _player.Position != frozenLink,
            "Link shock released movement before its final update.");
        _player.AdvanceApplicationUpdate();
        FailIf(_player.ElectricShockActive || _player.Material is not null ||
            room.BackgroundPalettes.Resolve(2, 2) != originalPalettes[2, 2],
            "Link shock did not release control and restore palettes at zero.");
        for (int i = 0; i < 59; i++) blob.UpdateFrame(blob.Position + Vector2.Right * 30);
        FailIf(blob.Counter != 1 || blob.CollisionEnabled || blob.State != 0x0a,
            "Buzz Blob $18 resumed collisions/Scent attraction before shock update 60.");
        blob.UpdateFrame(null);
        FailIf(blob.State != 8 || !blob.CollisionEnabled, "Buzz Blob $18 failed to recover on shock update 60.");
        FailIf(adapter.ApplySeedHit(blob.CollisionBounds, blob.Position, 0x24, spawns) != SeedHitResult.Activate ||
            !blob.IsCukeman || blob.AnimationIndex != 2 || blob.Health != health,
            "Mystery Seed did not transform Buzz Blob $18 into Cukeman without health damage.");
        int beforeText = blobRandom.Calls;
        int text = blob.ChooseText();
        FailIf(text is < 0x2f1e or > 0x2f25 || string.IsNullOrWhiteSpace(database.CukemanText(text)) ||
            blobRandom.Calls != beforeText + 1, "Cukeman did not select one of TX_2f1e..TX_2f25 with one RNG call.");
        blob.UpdateFrame(blob.Position + Vector2.Right * 40);
        FailIf(blob.State != 4 || blob.Angle != 8, "Cukeman lost Scent Seed attraction.");
        blob.UpdateFrame(null);
        FailIf(blob.State != 8, "Buzz Blob $18 did not leave Scent state on the target's zero update.");
        _player.WarpTo(blob.Position.Floor() + Vector2.Down * 14, recordSafe: false);
        _player.Face(Vector2I.Up);
        int beforeTalk = blobRandom.Calls;
        FailIf(!adapter.TryInteract(_player) || blobRandom.Calls != beforeTalk ||
            blob.OverlapsLink(_player.Position) && _player.ApplyEnemyContactDamage(blob.Position, 2),
            "Cukeman A-button interaction did not defer text RNG or protect Link from contact.");
        // State 8 chooses its next movement in this pass as well as selecting text.
        adapter.UpdateFrame(new RoomEntityFrame(_player, 0, false), spawns);
        FailIf(blobRandom.Calls != beforeTalk + 2,
            "Cukeman's enemy pass did not consume text RNG before its next movement RNG.");

        var fire = new ZoraFireProjectile(new ZoraFireSpawn(new Vector2(80, 48)), new ZoraFireDatabase(),
            point => point + Vector2.Down * 16);
        var frame = new RoomEntityFrame(_player, 0, false, new Vector2(112, 48));
        fire.UpdateFrame(frame);
        for (int i = 0; i < 7; i++) fire.UpdateFrame(frame);
        FailIf(fire.State != 1 || fire.Counter != 1 || fire.Position != new Vector2(80, 48),
            "PART_ZORA_FIRE $19 moved before its eight-update aim delay.");
        fire.UpdateFrame(frame);
        FailIf(fire.State != 2 || fire.Angle != 8 || fire.Position != new Vector2(80, 48),
            "PART_ZORA_FIRE $19 did not aim at the active Scent target on delay update 8.");
        fire.UpdateFrame(frame);
        FailIf(fire.Position != new Vector2(81.5f, 48), "PART_ZORA_FIRE $19 lost SPEED_180 movement.");
        var fireAdapter = new ZoraFireRoomEntity(fire);
        FailIf(fireAdapter.ApplyItemCollision(RoomEntityItemCollision.Bomb, fire.CollisionBounds,
            fire.Position, 4, spawns) || fire.Finished ||
            !fireAdapter.ApplySwordHit(fire.CollisionBounds, fire.Position, 2, EnemyKnockbackStrength.Low, spawns) ||
            !fire.Finished, "PART_ZORA_FIRE $19 did not ignore bombs and delete on a sword collision.");

        // Ember uses motionless-enemy effect $34, not the ordinary 59-update burn.
        for (int i = 0; i < 1024 && zora.State != 0x0b; i++) zora.UpdateFrame(Vector2.Zero, spawns);
        var zoraAdapter = new RiverZoraRoomEntity(zora,
            database.EnemyHandlers.ResolveHandler(rows[0]).CombatSource(rows[0], 1), _ => { }, () => Vector2.Zero);
        FailIf(zoraAdapter.ApplySeedHit(zora.CollisionBounds, zora.Position, 0x20, spawns) != SeedHitResult.Activate ||
            !zora.IsDead || !spawns.OfType<EnemyDeathPuffSpawn>().Any(puff => puff.EnemyId == 0x08),
            "River Zora $08 did not die immediately with its counted puff on an Ember collision.");

        _entities.Entities<BuzzBlobCharacter>().Single().BecomeCukeman();
        LoadValidationRoom(0, 0x43);
        FailIf(_entities.Entities<BuzzBlobCharacter>().Single().IsCukeman ||
            _entities.Entities<RiverZoraCharacter>().Count != 2,
            "Room 0:43 re-entry retained a transient Cukeman form or lost its source Zoras.");
        _entities.BeginScreenTransition(0, _world.LoadRoom(0, 0x43), Vector2.Left * 160);
        int transitionRng = _entities.RandomCalls;
        _entities.Update(1.0, _player);
        FailIf(_entities.Entities<RiverZoraCharacter>().Any(enemy => enemy.State != 9 || enemy.Visible) ||
            _entities.Entities<BuzzBlobCharacter>().Single().State != 8 || _entities.RandomCalls != transitionRng,
            "Room 0:43 destination enemies advanced or consumed RNG during scrolling preload.");
        _entities.FinishScreenTransition();
        fire.Free();
        blob.Free();
        zora.Free();
        GD.Print("Validated room 0:43 River Zoras, water search, surfacing, fireball, Buzz Blob shock, Cukeman and Scent behavior.");
    }
}
