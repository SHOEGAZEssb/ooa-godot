using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private OracleRoomData Room060MovementFixture()
    {
        OracleRoomData room = _world.LoadRoom(0, 0x60);
        byte floor = room.Layout[2 * room.WidthInTiles + 4];
        for (int y = 0; y < room.Height; y += 16)
        for (int x = 0; x < room.Width; x += 16)
            room.SetPositionTileAndCollision(new Vector2(x, y), floor, 0, 0);
        return room;
    }

    private void ValidateTektiteSourceBehavior()
    {
        var database = new EnemyDatabase();
        OracleRoomData room = Room060MovementFixture();
        var sounds = new List<int>();
        for (int subid = 0; subid < 3; subid++)
        {
            ImportedEnemyDefinition definition = database.ImportedEnemy(0x30, subid);
            FailIf(definition.TileBase != 22 || definition.Palette != (subid == 0 ? 3 : 1) ||
                definition.Health != (subid == 0 ? 2 : 3) || definition.RadiusX != 6 ||
                definition.RadiusY != 6 || definition.DamageQuarters != 2 || definition.Animations.Length != 3,
                $"ENEMY_TEKTITE $30:${subid:x2}: source properties or terminal subid alias changed.");
            var random = new OracleRandom();
            var enemy = new TektiteCharacter();
            Vector2 start = new(0x20, 0x48);
            Vector2 target = new(0x90, 0x48);
            enemy.Initialize(definition, room, start, random, sounds.Add);
            FailIf(enemy.Visible || random.Calls != 0, "$30 consumed RNG or became visible at construction.");
            enemy.UpdateFrame(target);
            // bank0 enemyStandardUpdate: $5e for var3d, then state0: $d4.
            FailIf(enemy.State != TektiteState.Waiting || enemy.Counter1 != 85 || random.Calls != 2,
                "$30 state0 must consume $5e/$d4 and wait ($d4 & $7f)+1 = 85 updates.");
            for (int frame = 0; frame < 84; frame++) enemy.UpdateFrame(target);
            FailIf(enemy.State != TektiteState.Waiting || enemy.Counter1 != 1,
                "$30 waiting ended before the zero-counter update.");
            enemy.UpdateFrame(target);
            int minimum = subid == 1 ? 45 : 90;
            FailIf(enemy.State != TektiteState.Crouching || enemy.Counter1 != 0x38 + minimum ||
                enemy.Counter2 != 24 || enemy.AnimationIndex != 1 || random.Calls != 3,
                $"$30:${subid:x2} lost the otherwise overwritten crouch RNG or var31 bit-0 rule.");
            for (int frame = 0; frame < 23; frame++) enemy.UpdateFrame(target);
            FailIf(enemy.State != TektiteState.Crouching || enemy.Counter2 != 1,
                "$30 crouch did not last exactly $18 updates.");
            enemy.UpdateFrame(target);
            FailIf(enemy.State != TektiteState.Ready || enemy.AnimationIndex != 2 ||
                enemy.ZFixed != 0 || random.Calls != 3, "$30 ready update launched early.");
            enemy.UpdateFrame(target);
            FailIf(enemy.State != TektiteState.Leaping || enemy.SpeedZ != -342 || enemy.Gravity != 14 ||
                enemy.Angle != 8 || enemy.ZFixed != 0 || enemy.Position != start || random.Calls != 4 ||
                sounds[^1] != 0x8f, "$30 smallLeap must launch at $feaa/$0e without integrating on launch.");
            // Independent closed form: z(n)=-342*n+7*n*(n-1), x(n)=32+1.25*n.
            for (int n = 1; n <= 49; n++)
            {
                enemy.UpdateFrame(target);
                FailIf(enemy.ZFixed != -342 * n + 7 * n * (n - 1) ||
                    enemy.Position != start + new Vector2(1.25f * n, 0) || enemy.AnimationIndex != 2,
                    $"$30 smallLeap update {n}: expected independent signed 8.8 arc and SPEED_140.");
            }
            Vector2 beforeLanding = enemy.Position;
            enemy.UpdateFrame(target);
            FailIf(enemy.State != TektiteState.Waiting || enemy.ZFixed != 0 ||
                enemy.Position != beforeLanding || enemy.Counter1 != 0x69 + minimum ||
                enemy.AnimationIndex != 0 || random.Calls != 5,
                "$30 landing update must skip XY movement, restore animation 0 and draw a fresh wait.");
            enemy.UpdateFrame(target);
            FailIf(enemy.Counter1 != 0x68 + minimum, "$30 did not resume its grounded wait after landing.");
            enemy.Free();
        }

        // Explicit RNG state: the fourth call is $00, so the big leap wins.
        var bigRandom = new OracleRandom();
        bigRandom.RestoreState(bigRandom.CaptureState() with { Rng1 = 0, Rng2 = 0 });
        var big = new TektiteCharacter();
        big.Initialize(database.ImportedEnemy(0x30), room, new Vector2(0x20, 0x48), bigRandom, sounds.Add);
        big.UpdateFrame(new Vector2(0x90, 0x48));
        big.UpdateFrame(new Vector2(0x90, 0x48));
        for (int i = 0; i < 25; i++) big.UpdateFrame(new Vector2(0x90, 0x48));
        FailIf(big.State != TektiteState.Leaping || big.SpeedZ != -384 || big.Gravity != 12,
            "$30 zero low three random bits must select bigLeap $fe80/$0c.");
        for (int n = 1; n <= 64; n++)
        {
            big.UpdateFrame(new Vector2(0x90, 0x48));
            FailIf(big.ZFixed != -384 * n + 6 * n * (n - 1), $"$30 bigLeap arc diverged on update {n}.");
        }
        Vector2 bigBeforeLanding = big.Position;
        big.UpdateFrame(new Vector2(0x90, 0x48));
        FailIf(big.State != TektiteState.Waiting || big.Position != bigBeforeLanding || big.Counter1 != 90,
            "$30 bigLeap did not land on update 65 without lateral movement.");
        big.Free();
        GD.Print("Validated Tektite $30:$00-$02 properties, initialization RNG, crouch/ready boundaries, both exact arcs and landing waits.");
    }

    private void ValidateSwordMaskedMoblinSourceBehavior()
    {
        var database = new EnemyDatabase();
        OracleRoomData room = Room060MovementFixture();
        var random = new OracleRandom();
        var enemy = new SwordEnemyCharacter();
        enemy.Initialize(database.ImportedEnemy(0x4a), room, new Vector2(0x40, 0x40), random);
        enemy.UpdateFrame(new Vector2(0x90, 0x70), swordSlotAvailable: false);
        FailIf(enemy.State != SwordEnemyState.Uninitialized || enemy.Visible || random.Calls != 1,
            "$4a:$00 must retry state0 when no PART_ENEMY_SWORD slot is available, consuming only standard initialization RNG.");
        enemy.Free();
        random = new OracleRandom();
        enemy = new SwordEnemyCharacter();
        enemy.Initialize(database.ImportedEnemy(0x4a), room, new Vector2(0x40, 0x40), random);
        enemy.UpdateFrame(new Vector2(0x90, 0x70));
        FailIf(enemy.Angle != 0x10 || enemy.Counter1 != 1 || enemy.Counter2 != 20 ||
            enemy.ScentAttractionCounter != 0x5e || random.Calls != 2,
            "$4a:$00 state0 must consume $5e then $d4, face down and use a $14-update cooldown.");
        Vector2 beforeRoute = enemy.Position;
        enemy.UpdateFrame(new Vector2(0x90, 0x70));
        FailIf(enemy.Position != beforeRoute || enemy.Counter1 != 116 || enemy.Counter2 != 19 ||
            enemy.Angle != 0 || random.Calls != 4,
            "$4a route selection must use H=$64 for $50+(H&$3f), L=$7c for targeting, then random $64 for angle; no movement.");
        enemy.UpdateFrame(new Vector2(0x90, 0x70));
        FailIf(enemy.Position != beforeRoute + new Vector2(0, -0.5f) || enemy.Counter1 != 115,
            "$4a wander movement must begin on the update following route selection at SPEED_80.");
        for (int i = 0; i < 11; i++) enemy.UpdateFrame(new Vector2(0x90, 0x70));
        FailIf(enemy.AnimationIndex != 0 || enemy.AnimationFrame != 1 ||
            enemy.Animation.CurrentOffset != new Vector2(-8, -18) ||
            enemy.CurrentAnimationTexture.GetWidth() != 16 || enemy.CurrentAnimationTexture.GetHeight() != 26,
            "$4a up-frame1 must preserve the sword's signed OAM Y=$fe instead of cropping it to a 32-pixel canvas.");
        enemy.Free();

        enemy = new SwordEnemyCharacter();
        enemy.Initialize(database.ImportedEnemy(0x4a), room, new Vector2(0x40, 0x40), new OracleRandom());
        enemy.UpdateFrame(enemy.Position + Vector2.Right * 32);
        for (int i = 0; i < 19; i++) enemy.UpdateFrame(enemy.Position + Vector2.Right * 32);
        FailIf(enemy.State != SwordEnemyState.Wandering || enemy.Counter2 != 1,
            "$4a chase cooldown expired before update $14.");
        enemy.UpdateFrame(enemy.Position + Vector2.Right * 32);
        FailIf(enemy.State != SwordEnemyState.PreparingChase || enemy.Counter1 != 16 || enemy.Angle != 8,
            "$4a must enter its $10-update chase preparation on the cooldown zero update.");
        for (int i = 0; i < 16; i++) enemy.UpdateFrame(enemy.Position + Vector2.Right * 32);
        FailIf(enemy.State != SwordEnemyState.Chasing || enemy.Counter1 != 96,
            "$4a chase preparation did not enter the $60-update chase.");
        for (int i = 0; i < 4; i++) enemy.UpdateFrame(enemy.Position + Vector2.Left * 32);
        FailIf(enemy.Angle != 9, "$4a objectNudgeAngleTowards must increment on the exact opposite-angle tie.");
        for (int i = 0; i < 92; i++) enemy.UpdateFrame(enemy.Position + Vector2.Left * 32);
        FailIf(enemy.State != SwordEnemyState.Wandering || enemy.Counter1 != 0 || enemy.Counter2 != 20,
            "$4a chase completion must retain counter1=$00 and restore cooldown $14.");
        enemy.UpdateFrame(new Vector2(0x90, 0x70));
        FailIf(enemy.Counter1 != 255, "$4a counter1 must wrap $00 to $ff after a chase.");
        enemy.Free();
        GD.Print("Validated sword Masked Moblin $4a:$00 slot retry, source RNG operands, no-movement route selection, cooldown and chase boundaries.");
    }

    private void ValidateRoom060EnemyCombat()
    {
        var database = new EnemyDatabase();
        OracleRoomData room = Room060MovementFixture();
        var random = new OracleRandom();
        random.RestoreState(random.CaptureState() with { Rng1 = 0, Rng2 = 0 });
        var tektite = new TektiteCharacter();
        tektite.Initialize(database.ImportedEnemy(0x30), room, new Vector2(0x40, 0x48), random, _ => { });
        RoomObjectRecord source = database.GetRoomObjects(0, 0x60)[2];
        var adapter = new TektiteRoomEntity(tektite,
            database.EnemyHandlers.ResolveHandler(source).CombatSource(source, 2), _ => { });
        var spawns = new List<RoomEntitySpawn>();
        for (int i = 0; i < 27; i++) tektite.UpdateFrame(new Vector2(0x90, 0x48));
        FailIf(tektite.State != TektiteState.Leaping, "$30 combat fixture did not reach its source leap.");
        tektite.Position = new Vector2(159, 0x48);
        tektite.UpdateFrame(new Vector2(0x90, 0x48));
        FailIf(tektite.Angle != 0x18 || tektite.Position.X != 157.75f,
            "$30 did not reflect off the right screen boundary before movement.");
        for (int i = 0; i < 19; i++) tektite.UpdateFrame(new Vector2(0x20, 0x48));
        int airborneZ = tektite.ZFixed;
        int airborneSpeed = tektite.SpeedZ;
        _player.WarpTo(tektite.Position, recordSafe: false);
        int health = _inventory.HealthQuarters;
        adapter.HandleLinkContact(_player);
        FailIf(_inventory.HealthQuarters != health ||
            adapter.ApplySeedHitAtHeight(tektite.CollisionBounds, tektite.Position, 0, 0x21, spawns) != SeedHitResult.None,
            "$30 airborne contact or Scent Seed ignored the original Z collision window.");
        FailIf(!adapter.ApplySwordHit(tektite.CollisionBounds, tektite.Position + Vector2.Right * 16,
                1, EnemyKnockbackStrength.Low, spawns), "$30 did not accept its ordinary sword collision.");
        tektite.UpdateFrame(new Vector2(0x20, 0x48));
        FailIf(tektite.ZFixed != airborneZ || tektite.SpeedZ != airborneSpeed ||
            tektite.State != TektiteState.Leaping, "$30 sword knockback advanced or reset its suspended leap.");
        tektite.Free();

        LoadValidationRoom(0, 0x60);
        _player.WarpTo(new Vector2(0x48, 0x38), recordSafe: false);
        base._Process(1.0 / 60.0);
        FailIf(_entities.EntityAdapters<EnemySwordRoomEntity>().Count() != 2,
            "Room 0:60 did not allocate two separate PART_ENEMY_SWORD $1d slots in its first enemy pass.");
        SwordEnemyCharacter moblin = _entities.Entities<SwordEnemyCharacter>()[0];
        EnemySwordRoomEntity blade = _entities.EntityAdapters<EnemySwordRoomEntity>().First();
        for (int i = 0; i < 8; i++)
        {
            _player.WarpTo(moblin.Position + OracleObjectMath.CardinalVector(moblin.AnimationIndex * 8) * 16,
                recordSafe: false);
            base._Process(1.0 / 60.0);
            if (moblin.SwordBlocking && blade.CollisionEnabled) break;
        }
        FailIf(!moblin.SwordBlocking || !blade.CollisionEnabled, "$4a:$00 never published a guarded body and enabled blade.");
        var recoil = new List<SwordAttackerKnockback>();
        int moblinHealth = moblin.Health;
        FailIf(!_entities.ApplySwordHit(blade.CollisionBounds, _player.Position, 1, EnemyKnockbackStrength.Low,
                true, recoil.Add, swordState: SwordActionState.Swing, swordLevel: 1) ||
            recoil is not [{ Frames: 8 }] || blade.InvincibilityCounter != -11 ||
            moblin.KnockbackCounter != 0 || moblin.Health != moblinHealth,
            "$1d blocked hit must write part -11 invincibility and Link recoil8 before transferring anything to $4a.");
        base._Process(1.0 / 60.0);
        FailIf(moblin.KnockbackCounter != 9 || moblin.InvincibilityCounter != -11 || blade.InvincibilityCounter != -10,
            "$1d did not copy ENEMYDMG_$4c to its parent in the part pass after the enemy update.");
        base._Process(1.0 / 60.0);
        FailIf(moblin.KnockbackCounter != 8 || !moblin.SwordBlocking || !blade.CollisionEnabled,
            "$4a recoil must select the blocked mode unconditionally (checkIgnoreCollision returns NZ), retaining its blade.");

        // Direct item dispatch uses the bomb row even when the body is guarding;
        // defeat remains delayed until recoil ends, then its blade retires.
        for (int i = 0; i < 24; i++) base._Process(1.0 / 60.0);
        SwordEnemyRoomEntity body = _entities.EntityAdapters<SwordEnemyRoomEntity>().First();
        spawns.Clear();
        FailIf(!body.ApplyItemCollision(RoomEntityItemCollision.Bomb, moblin.CollisionBounds,
                moblin.Position + Vector2.Right * 16, 8, spawns) || !moblin.PendingKnockbackDeath,
            "$4a bomb row failed to bypass guarding and defer death through recoil.");
        for (int i = 0; i < 40; i++) base._Process(1.0 / 60.0);
        FailIf(!blade.Finished || _entities.EntityAdapters<EnemySwordRoomEntity>().Contains(blade),
            "$1d survived its parent's death or retained a shared part slot.");
        // Development direct loads deliberately clear recent defeats; use
        // the ordinary room-object lifetime for this re-entry assertion.
        _entities.LoadRoom(0, _world.LoadRoom(0, 0x50));
        _entities.LoadRoom(0, _world.LoadRoom(0, 0x60));
        FailIf(_entities.Entities<SwordEnemyCharacter>().Count != 1 ||
            _entities.Entities<TektiteCharacter>().Count != 2,
            "Room 0:60 re-entry did not suppress the defeated fixed enemy while preserving its other source placements.");
        GD.Print("Validated room 0:60 Tektite boundary reflection, airborne collision and suspended leap; blade allocation, deferred recoil, death and recent-defeat re-entry.");
    }

    private void ValidateRoom060Enemies()
    {
        var database = new EnemyDatabase();
        foreach (int id in new[] { 0x40, 0x42, 0x60 })
        {
            var placements = database.GetRoomObjects(0, id);
            FailIf(placements.Count != 3 ||
                placements[0] is not { Kind: RoomObjectKind.FixedEnemy, Id: 0x4a, SubId: 0, X: 0x48, Y: 0x28 } ||
                placements[1] is not { Kind: RoomObjectKind.FixedEnemy, Id: 0x4a, SubId: 0, X: 0x58, Y: 0x38 } ||
                placements[2] is not { Kind: RoomObjectKind.RandomEnemy, Id: 0x30, SubId: 0, Count: 2, Flags: 0x40 },
                $"Room 0:{id:x2} lost its shared source alias: two fixed $4a:$00 then two random $30:$00.");
        }
        LoadValidationRoom(0, 0x60);
        var swords = _entities.Entities<SwordEnemyCharacter>();
        var tektites = _entities.Entities<TektiteCharacter>();
        FailIf(swords.Count != 2 || tektites.Count != 2 || _entities.RoomEnemyCount != 4 ||
            swords.Any(e => e.Visible) || tektites.Any(e => e.Visible),
            "Room 0:60 did not construct all four source enemies, hidden until state0.");
        _player.WarpTo(new Vector2(0x48, 0x38), recordSafe: false);
        FailIf(_currentRoom.IsSolid(_player.Position), "Room 0:60 validation Link entry is inside solid geometry.");
        int calls = _entities.RandomCalls;
        base._Process(1.0 / 60.0);
        FailIf(_entities.RandomCalls != calls + 8 || swords.Any(e => e.State != SwordEnemyState.Wandering) ||
            tektites.Any(e => e.State != TektiteState.Waiting),
            "Room 0:60 gameplay update did not initialize all four enemies in order with two RNG calls each.");
        for (int i = 0; i < 180; i++) base._Process(1.0 / 60.0);
        FailIf(_sound.PlayRequestsFor(0x8f) == 0, "Room 0:60 Tektites never reached their source jump sound in gameplay.");

        // Actual room transition preload runs state0, then freezes its state,
        // animation, coordinates and shared RNG until the scroll completes.
        LoadValidationRoom(0, 0x50);
        OracleRoomData incoming = _world.LoadRoom(0, 0x60);
        _entities.BeginScreenTransition(0, incoming, Vector2.Down * 128);
        tektites = _entities.Entities<TektiteCharacter>();
        var frozen = tektites.Select(e => (e.Position, e.Counter1, e.AnimationFrame)).ToArray();
        calls = _entities.RandomCalls;
        for (int i = 0; i < 20; i++) _entities.Update(1.0 / 60.0, _player);
        FailIf(tektites.Count != 2 || tektites.Any(e => e.State != TektiteState.Waiting) ||
            !tektites.Select(e => (e.Position, e.Counter1, e.AnimationFrame)).SequenceEqual(frozen) ||
            _entities.RandomCalls != calls, "Room 0:60 incoming Tektites advanced during scrolling.");
        _entities.FinishScreenTransition();
        _entities.Update(1.0 / 60.0, _player);
        FailIf(tektites.Select(e => e.Counter1).SequenceEqual(frozen.Select(e => e.Counter1)),
            "Room 0:60 incoming Tektites remained frozen after scrolling.");

        string Cadence(bool batched)
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(0, 0x60);
            _player.WarpTo(new Vector2(0x48, 0x38), recordSafe: false);
            if (batched) base._Process(180.0 / 60.0);
            else for (int i = 0; i < 180; i++) base._Process(1.0 / 60.0);
            return string.Join(";", _entities.Entities<TektiteCharacter>().Select(e =>
                $"{e.State}:{e.Counter1}:{e.Counter2}:{e.Position}:{e.ZFixed}:{e.SpeedZ}:{e.AnimationFrame}")) +
                string.Join(";", _entities.Entities<SwordEnemyCharacter>().Select(e =>
                $"{e.State}:{e.Counter1}:{e.Counter2}:{e.Position}:{e.Angle}:{e.AnimationFrame}")) +
                $":{_entities.RandomCalls}:{_inventory.HealthQuarters}";
        }
        FailIf(Cadence(false) != Cadence(true),
            "Room 0:60 enemy/player/part gameplay diverged between individual updates and a batched host frame.");
        GD.Print("Validated room 0:60 source aliases, all four enemies, gameplay RNG, jumping, transition freeze/resume and host-frame cadence.");
    }
}
