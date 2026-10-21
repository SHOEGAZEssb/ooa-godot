using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateLynelRom()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        LoadValidationRoom(1, 0x5b);
        var database = EnemyDatabase.Shared;
        FailIf(database.ImportedEnemy(0x0d, 0) is not { Health: 8, DamageQuarters: 4, RadiusX: 6, RadiusY: 6, Palette: 2, Animations.Length: 8 } ||
            database.ImportedEnemy(0x0d, 1) is not { Health: 12, DamageQuarters: 4, Palette: 1, Animations.Length: 8 },
            "enemy0dSubidData / extraEnemyData $17/$1b lost the red/blue Lynel attributes.");
        int shots = 0, failedShots = 0, charges = 0, randomTurns = 0, scentUpdates = 0;
        foreach (int subid in new[] { 0, 1 })
        foreach (int initialSeed in new[] { 0x0d37, 0x1234, 0x80ff, 0xffff })
        {
            var random = new OracleRandom();
            random.RestoreState(random.CaptureState() with { Rng1 = (byte)initialSeed, Rng2 = (byte)(initialSeed >> 8) });
            var rom = new EnemyAiRom();
            rom[0xcc08] = 0x95; rom[0xff94] = (byte)initialSeed; rom[0xff95] = (byte)(initialSeed >> 8);
            rom[0xd080] = 0x11; rom[0xd081] = 0x0d; rom[0xd082] = (byte)subid;
            rom.Word(0xd08a, 0x6880); rom.Word(0xd08c, 0x6880);
            rom[0xcc33] = (byte)_currentRoom.ActiveCollisions;
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
            {
                Vector2 p = new(x * 16 + 8, y * 16 + 8);
                rom[0xcf00 + y * 16 + x] = _currentRoom.GetMetatile(p);
                rom[0xce00 + y * 16 + x] = (byte)_currentRoom.GetTerrainInfo(p).Collision;
            }
            var enemy = new LynelCharacter();
            enemy.Initialize(database.ImportedEnemy(0x0d, subid), _currentRoom, new(104.5f, 104.5f), random);
            try
            {
                for (int update = 0; update < 1000; update++)
                {
                    // Only the enemy walk runs here. Native PART allocation is
                    // observed before the separately tested PART phase.
                    bool blocked = update < 200;
                    for (int slot = 0; slot < 16; slot++)
                        for (int offset = 0; offset < 64; offset++) rom[0xd0c0 + slot * 256 + offset] = 0;
                    if (blocked) for (int slot = 0; slot < 16; slot++) rom[0xd0c0 + slot * 256] = 1;
                    Vector2 link = update < 500 ? new(104, 24) : new(24, 104);
                    Vector2? scent = update is >= 320 and < 355 ? new Vector2(120, 56) : null;
                    rom[0xcc00] = (byte)update; rom[0xccd9] = (byte)(scent.HasValue ? 1 : 0);
                    rom[0xd00b] = rom[0xffb0] = (byte)link.Y;
                    rom[0xd00d] = rom[0xffb1] = (byte)link.X;
                    rom[0xffb2] = (byte)(scent ?? link).Y; rom[0xffb3] = (byte)(scent ?? link).X;
                    bool attempted = false;
                    rom.Update();
                    enemy.UpdateFrame(link, scent, update, () => { attempted = true; return !blocked; });
                    int timer = (int)typeof(EnemyAnimationPlayer).GetField("_frameCounter", flags)!.GetValue(enemy.Animation)!;
                    var actual = random.CaptureState();
                    FailIf(enemy.State != rom[0xd084] || enemy.Counter != rom[0xd086] || enemy.Angle != rom[0xd089] ||
                        enemy.Speed != rom[0xd090] || enemy.Position != new Vector2(rom.Word(0xd08c) / 256f, rom.Word(0xd08a) / 256f) ||
                        timer != rom[0xd0a0] || enemy.AnimationParameter != rom[0xd0a1] ||
                        actual.Rng1 != rom[0xff94] || actual.Rng2 != rom[0xff95] || random.Calls != rom.RandomCalls,
                        $"Lynel $0d:${subid:x2}, seed${initialSeed:x4}, update{update}: runtime state/counter/angle/speed={enemy.State}/{enemy.Counter}/{enemy.Angle}/{enemy.Speed}, XY={enemy.Position}, timer={timer}, RNG={random.Calls}; ROM={rom[0xd084]}/{rom[0xd086]}/{rom[0xd089]}/{rom[0xd090]}, XY=${rom.Word(0xd08c):x4}/${rom.Word(0xd08a):x4}, timer={rom[0xd0a0]}, RNG={rom.RandomCalls}.");
                    if (attempted && blocked) failedShots++;
                    if (attempted && !blocked)
                    {
                        shots++;
                        FailIf(rom[0xd0c0] == 0 || rom[0xd0c1] != 0x1b || rom[0xd0c9] != enemy.Angle ||
                            rom[0xd0cb] != (int)enemy.Position.Y || rom[0xd0cd] != (int)enemy.Position.X ||
                            rom.Word(0xd0d6) != 0xd080 || rom.Word(0xd098) != 0xd0c0,
                            "ecom_spawnProjectile must allocate PART$1b with copied high XY/angle and paired slot references.");
                    }
                    if (enemy.Speed == 0x28 && enemy.State == 9) charges++;
                    if (enemy.Speed == 0x14 && enemy.State == 9) randomTurns++;
                    if (enemy.State == 4) scentUpdates++;
                }
            }
            finally { enemy.Free(); }
        }
        FailIf(shots == 0 || failedShots == 0 || charges == 0 || randomTurns == 0 || scentUpdates == 0,
            "Lynel ROM matrix must exercise shots, full PART failures, charges, walks and scent entry/exit.");
        GD.Print("Validated both Lynel variants against clean-ROM AI: exact RNG, walls, walk/charge animation, counters, scent and successful/failed beam allocation.");
    }

    private void ValidateLynelGameplayRom()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (var spec in new[] { (Room: 0x7b, Subid: 0, Batched: false), (Room: 0x7b, Subid: 0, Batched: true), (Room: 0x5d, Subid: 1, Batched: false) })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(1, spec.Room);
            Vector2 floor = (from y in Enumerable.Range(1, 6) from x in Enumerable.Range(1, 8)
                let p = new Vector2(x * 16 + 8, y * 16 + 8)
                where !_collision.Collides(p) && _currentRoom.GetTerrainInfo(p).Hazard == HazardType.None select p).First();
            _player.WarpTo(floor);
            typeof(Player).GetField("_enemyInvincibilityFrames", flags)!.SetValue(_player, 100000f);
            var slots = (Dictionary<IRoomEntity, int>)typeof(RoomEntityManager).GetField("_enemySlots", flags)!.GetValue(_entities)!;
            var parts = (Dictionary<IRoomEntity, int>)typeof(RoomEntityManager).GetField("_partSlots", flags)!.GetValue(_entities)!;
            FailIf(_entities.Entities<LynelCharacter>().Count != 1 || _entities.Entities<LynelCharacter>()[0].Record.SubId != spec.Subid ||
                _entities.Entities<ArrowDarknutCharacter>().Count != 1, $"Room1:${spec.Room:x2} must allocate its source Lynel and Darknut together.");
            var rom = new EnemyStatusRom(_currentRoom, _saveData, _random.Calls);
            rom[0xcc08] = 0x95; rom[0xcc0a] = 0x98; rom[0xcc0c] = 0x8e;
            rom[0xcdd1] = 2;
            foreach (var pair in slots)
            {
                int address = 0xd080 + pair.Value * 256;
                bool lynel = pair.Key.Node is LynelCharacter;
                rom[address] = (byte)(((pair.Value + 1) << 4) | 1);
                rom[address + 1] = (byte)(lynel ? 0x0d : 0x21);
                rom[address + 2] = (byte)spec.Subid;
                rom.Word(address + 10, (int)(pair.Key.Node.Position.Y * 256));
                rom.Word(address + 12, (int)(pair.Key.Node.Position.X * 256));
            }
            var seed = _random.CaptureState(); rom[0xff94] = seed.Rng1; rom[0xff95] = seed.Rng2;
            int gate = 1, update = 0, shots = 0;
            _entities.TextActiveSource = () => gate == 1;
            _entities.NonInteractionObjectsDisabledSource = () => gate == 2;
            _entities.PaletteFadeActiveSource = () => gate == 3;
            void Compare()
            {
                rom[0xcba0] = (byte)(gate == 1 ? 1 : 0);
                rom[0xcc8a] = (byte)(gate == 2 ? 4 : 0);
                rom[0xc4ab] = (byte)(gate == 3 ? 1 : 0);
                rom.Update(_entities.FrameCounter, _player.Position);
                foreach (var pair in slots.Where(pair => pair.Key.Node is LynelCharacter))
                {
                    var enemy = (LynelCharacter)pair.Key.Node; int address = 0xd080 + pair.Value * 256;
                    int timer = (int)typeof(EnemyAnimationPlayer).GetField("_frameCounter", flags)!.GetValue(enemy.Animation)!;
                    FailIf(enemy.State != rom[address + 4] || enemy.Counter != rom[address + 6] || enemy.Angle != rom[address + 9] ||
                        enemy.Speed != rom[address + 16] || timer != rom[address + 0x20] ||
                        enemy.Position != new Vector2(rom.Word(address + 12) / 256f, rom.Word(address + 10) / 256f),
                        $"Lynel room1:${spec.Room:x2} gameplay update{update}, gate={gate}, batch={spec.Batched}: runtime={enemy.State}/{enemy.Counter}/{enemy.Angle}/{enemy.Speed}/{timer}/{enemy.Position}; ROM={rom[address + 4]}/{rom[address + 6]}/{rom[address + 9]}/{rom[address + 16]}/{rom[address + 0x20]}/${rom.Word(address + 12):x4},${rom.Word(address + 10):x4}.");
                }
                FailIf(RandomDiffers(seed, rom[0xff94], rom[0xff95], rom.RandomCalls),
                    $"Lynel/Darknut shared RNG differs at gameplay update{update} in room1:${spec.Room:x2}.");
                foreach (var pair in parts.Where(pair => pair.Key is LynelBeamRoomEntity))
                {
                    var beam = (LynelBeamRoomEntity)pair.Key; int address = 0xd0c0 + pair.Value * 256;
                    FailIf(rom[address] == 0 || rom[address + 1] != 0x1b || beam.State != rom[address + 4] ||
                        beam.Angle != rom[address + 9] || beam.Palette != (rom[address + 0x1c] & 7) ||
                        beam.Position != new Vector2(rom.Word(address + 12) / 256f, rom.Word(address + 10) / 256f) ||
                        beam.CollisionBounds.Size != new Vector2(rom[address + 0x27] * 2, rom[address + 0x26] * 2),
                        $"PART$1b slot${pair.Value:x2}, update{update}: runtime={beam.State}/{beam.Angle}/{beam.Palette}/{beam.Position}/{beam.CollisionBounds.Size}; ROM={rom[address + 4]}/{rom[address + 9]}/{rom[address + 0x1c]}/${rom.Word(address + 12):x4},${rom.Word(address + 10):x4}.");
                    shots++;
                }
                FailIf(parts.Count(pair => pair.Key is LynelBeamRoomEntity) != Enumerable.Range(0, 16)
                    .Count(slot => rom[0xd0c0 + slot * 256] != 0 && rom[0xd0c1 + slot * 256] == 0x1b),
                    $"Lynel beam allocation/lifetime differs in room1:${spec.Room:x2} at update{update}: runtime={parts.Count(pair => pair.Key is LynelBeamRoomEntity)}, native={Enumerable.Range(0, 16).Count(slot => rom[0xd0c0 + slot * 256] != 0 && rom[0xd0c1 + slot * 256] == 0x1b)}.");
                update++;
            }
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: spec.Batched, afterUpdate: Compare);
            Step(3); gate = 0; Step(200); gate = 1; Step(3); gate = 2; Step(3); gate = 3; Step(3); gate = 0; Step(400);
            FailIf(shots == 0, $"Lynel room1:${spec.Room:x2} never fired a real beam through the gameplay loop.");
        }
        GD.Print("Validated Lynel/Darknut gameplay slot and RNG order, PART$1b movement/radii/palette/lifetime, text/object/palette gates and batched host updates against ROM.");
    }

    private void ValidateLynelCombatRom()
    {
        LoadValidationRoom(1, 0x5b);
        var database = EnemyDatabase.Shared;
        foreach (int collision in new[] { 1,2,3,4,5,6,7,8,9,10,11,13,0x16,0x18,0x19,0x1a,0x1b,0x1c,0x1d,0x1e })
        {
            var random = new OracleRandom();
            var enemy = new LynelCharacter();
            enemy.Initialize(database.ImportedEnemy(0x0d), _currentRoom, new(104,104), random);
            var source = database.GetRoomObjects(1,0x5b).Single(row => row.Id == 0x0d);
            var adapter = new LynelRoomEntity(enemy, database.EnemyHandlers.ResolveHandler(source).CombatSource(source,1), _entities);
            var rom = new EnemyStatusRom(_currentRoom,_saveData,0);
            rom[0xcc08]=0x95; rom[0xd080]=0x11; rom[0xd081]=0x0d;
            rom.Word(0xd08a,0x6800); rom.Word(0xd08c,0x6800);
            var seed = random.CaptureState(); rom[0xff94]=seed.Rng1; rom[0xff95]=seed.Rng2;
            var spawns = new List<RoomEntitySpawn>();
            BurningEnemyPart? flame = null;
            try
            {
                Vector2 link = new(24,24);
                rom.Update(0,link); enemy.UpdateFrame(link,null,0,()=>false);
                int damage = collision is 5 or 8 or 0x1c ? 2 : collision is 6 or 7 or 11 or 0x18 ? 4 : 1;
                if (collision <= 3)
                {
                    damage = 0;
                    enemy.TryApplyShieldBump(enemy.CollisionBounds,enemy.Position-Vector2.Right,
                        collision==1?EnemyKnockbackStrength.High:EnemyKnockbackStrength.Normal);
                }
                else if (collision >= 0x1a)
                {
                    int item = collision switch { 0x1a=>ItemId.MysterySeed,0x1b=>ItemId.EmberSeed,0x1c=>ItemId.ScentSeed,0x1d=>ItemId.PegasusSeed,_=>ItemId.GaleSeed };
                    FailIf(!new SeedSatchelDatabase().TryGet(item, out var seedRecord), $"Missing seed${item:x2}.");
                    damage = -(sbyte)seedRecord.Damage;
                    adapter.ApplySeedCollision(enemy.CollisionBounds,enemy.Position-Vector2.Right,seedRecord,collision,spawns);
                    if (spawns.OfType<BurningEnemySpawn>().Any())
                    { flame = new BurningEnemyPart(); flame.Initialize(adapter); }
                }
                else adapter.ApplyItemCollision((RoomEntityItemCollision)collision,enemy.CollisionBounds,enemy.Position-Vector2.Right,damage,spawns);
                rom.HitWithSword(collision,damage);
                FailIf(enemy.Health != rom[0xd0a9] || (enemy.InvincibilityCounter & 255) != rom[0xd0ab] ||
                    enemy.KnockbackCounter != rom[0xd0ad] || enemy.StunCounter != rom[0xd0ae],
                    $"Lynel collision${collision:x2} immediate HP/invincibility/recoil/stun differs: runtime={enemy.Health}/{enemy.InvincibilityCounter}/{enemy.KnockbackCounter}/{enemy.StunCounter}, ROM={rom[0xd0a9]}/{rom[0xd0ab]}/{rom[0xd0ad]}/{rom[0xd0ae]}.");
                for (int update=1;update<=110;update++)
                {
                    rom.Update(update,link); enemy.UpdateFrame(link,null,update,()=>true);
                    if (flame is {Finished:false}) flame.UpdateFrame();
                    bool dead = rom[0xd080]==0;
                    FailIf(enemy.IsDead != dead || !dead && (enemy.Health != rom[0xd0a9] ||
                        (enemy.InvincibilityCounter & 255) != rom[0xd0ab] || enemy.StunCounter != rom[0xd0ae] ||
                        enemy.KnockbackCounter != rom[0xd0ad] || enemy.ZFixed != unchecked((short)rom.Word(0xd08e)) ||
                        enemy.State != rom[0xd084] || enemy.Counter != rom[0xd086] ||
                        enemy.Position != new Vector2(rom.Word(0xd08c)/256f,rom.Word(0xd08a)/256f)),
                        $"Lynel collision${collision:x2} update{update}: runtime dead/HP/state/counter/inv/stun/recoil/Z/XY={enemy.IsDead}/{enemy.Health}/{enemy.State}/{enemy.Counter}/{enemy.InvincibilityCounter}/{enemy.StunCounter}/{enemy.KnockbackCounter}/{enemy.ZFixed}/{enemy.Position}; ROM={dead}/{rom[0xd0a9]}/{rom[0xd084]}/{rom[0xd086]}/{rom[0xd0ab]}/{rom[0xd0ae]}/{rom[0xd0ad]}/{rom.Word(0xd08e)}/${rom.Word(0xd08c):x4},${rom.Word(0xd08a):x4}.");
                    if (dead) break;
                    // Successful shots are checked by ValidateLynelRom. Retire
                    // their native actors after dispatch in this status matrix.
                    for (int slot=0;slot<16;slot++) if (rom[0xd0c1+slot*256]==0x1b) rom[0xd0c0+slot*256]=0;
                }
            }
            finally { flame?.Free(); enemy.Free(); }
        }
        GD.Print("Validated Lynel weapon/seed collision damage, recoil, hit-update boundaries, stun, burn and Mystery death against ROM.");
    }
}
