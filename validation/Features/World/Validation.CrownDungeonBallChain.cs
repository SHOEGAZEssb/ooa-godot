using Godot;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateCrownDungeonBallChainSeeds()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput", flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates", flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate", flags)!.CreateDelegate(typeof(Action), this);
        var seeds = new SeedSatchelDatabase();
        var shooter = SeedShooterRecord.Load();
        var random = CaptureOracleRandomForValidation();
        foreach (bool batch in new[] { false, true })
        foreach (int item in new[] { 0x20, 0x21, 0x22, 0x23 })
        {
            RestoreOracleRandomForValidation(random);
            void Step(int count = 1, Vector2 movement = default)
            {
                input.CaptureForValidation([], [], movement);
                if (batch) scheduler.Advance(count / 60.0, update);
                else for (int i = 0; i < count; i++) scheduler.Advance(1.0 / 60.0, update);
            }
            LoadValidationRoom(4, 0xa0);
            var soldier = _entities.Entities<BallChainSoldierCharacter>().Single();
            _player.WarpTo(new(232,144));
            Step(16, Vector2.Left); Step(8, Vector2.Down);
            for (int i = 0; i < 128 && soldier.State != 9; i++) Step(movement: Vector2.Left);
            FailIf(soldier.State != 9, "$4:$a0 seed fixture must approach through the entrance and open floor.");
            FailIf(!seeds.TryGet(item, out var seed), $"Missing seed ${item:x2}.");
            EmberSeedEffect Shoot() => _entities.Spawn<EmberSeedEffect>(new EmberSeedSpawn(
                soldier.Position + Vector2.Down * 12 - shooter.Offsets[0], Vector2I.Up, seed, 4, SeedLaunchKind.Shooter));
            int count = _entities.RoomEnemyCount;
            int hits = item == 0x21 ? 4 : 1;
            for (int hit = 1; hit <= hits; hit++)
            {
                var projectile = Shoot();
                Step();
                for (int i = 0; i < 12 && !projectile.HasPendingNativeCollision && projectile.CollisionEnabled; i++) Step();
                int expected = item == 0x21 ? 8 - hit * 2 : 8;
                FailIf(!projectile.HasPendingNativeCollision || soldier.Health != expected || soldier.KnockbackCounter != 0 ||
                    soldier.GaleCollisionDisabled || _entities.Entities<BurningEnemyPart>().Any(),
                    $"$4b seed ${item:x2} hit{hit}: expected HP{expected}, got{soldier.Health}; effect$0b/$20 must preserve no-knockback/immunity.");
                if (item != 0x21)
                {
                    Step(16);
                    FailIf(projectile.CollisionEnabled || soldier.Health != 8 || soldier.InvincibilityCounter != 0 ||
                        soldier.GaleCollisionDisabled || _entities.Entities<BurningEnemyPart>().Any(),
                        $"$4b immune seed ${item:x2} must finish its hit response without delayed damage or a status transition.");
                    continue;
                }
                FailIf(soldier.InvincibilityCounter != 32 || !soldier.PendingHit || soldier.IsDead,
                    "$4b Scent hit must publish JUST_HIT and 32 invincibility before the next enemy pass.");
                if (hit != hits) { Step(32); continue; }
                FailIf(soldier.CollisionEnabled, "$4b lethal Scent must disable collision immediately.");
                Step();
                FailIf(soldier.IsDead || _entities.RoomEnemyCount != count,
                    "$4b lethal JUST_HIT must retain the enemy and count for its normal AI dispatch.");
                Step();
                FailIf(_entities.Entities<BallChainSoldierCharacter>().Any() || _entities.Entities<SpikedBallPart>().Any() ||
                    _entities.RoomEnemyCount != count,
                    "$4b death must delete its four linked parts and transfer its count to the death puff.");
                Step(32);
                FailIf(_entities.RoomEnemyCount != count - 1, "$4b death puff must release exactly one enemy count.");
            }
        }
        GD.Print("Validated Crown soldier live seed immunity, repeated Scent damage, deferred death, linked-part removal and count transfer in single/batched updates.");
    }

    private void ValidateCrownDungeonBallChainRoom()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput", flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates", flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate", flags)!.CreateDelegate(typeof(Action), this);
        var random = CaptureOracleRandomForValidation();
        foreach (bool batch in new[] { false, true })
        {
            RestoreOracleRandomForValidation(random);
            void Step(int count = 1, Vector2 movement = default)
            {
                input.CaptureForValidation([], [], movement);
                if (batch) scheduler.Advance(count / 60.0, update);
                else for (int i = 0; i < count; i++) scheduler.Advance(1.0 / 60.0, update);
            }
            LoadValidationRoom(4, 0xa0);
            var soldier = _entities.Entities<BallChainSoldierCharacter>().Single();
            FailIf(soldier.Position != new Vector2(0x78,0x58), "$4:$a0 must retain its fixed $4b:$00 source position.");
            _player.WarpTo(new(232,144));
            Step(16, Vector2.Left);
            Step(8, Vector2.Down);
            FailIf(_player.Position != new Vector2(216,152), "$4:$a0 fixture must reach the open row below the pillars from the right entrance.");
            for (int i = 0; i < 128 && soldier.State != 9; i++) Step(movement: Vector2.Left);
            FailIf(soldier.State != 9 || soldier.Counter1 != 90, "$4:$a0 real approach must start the soldier's source windup.");
            var parts = _entities.Entities<SpikedBallPart>();
            FailIf(parts.Count != 4 || !parts.Select(p => p.SubId).SequenceEqual(new[] { 0,1,2,3 }),
                "$4b must allocate the head and three links in original native part order.");
            var slots = (Dictionary<IRoomEntity, int>)typeof(RoomEntityManager).GetField("_partSlots", flags)!.GetValue(_entities)!;
            FailIf(!_entities.EntityAdapters<SpikedBallRoomEntity>().Select(p => slots[p]).SequenceEqual(new[] { 0,1,2,3 }),
                "$4:$a0 head and links must occupy four separate native slots $d0-$d3.");
            var head = parts[0];
            Step(89);
            FailIf(soldier.State != 9 || soldier.Counter1 != 1 || head.State != 2,
                "Actual enemy/part loop must keep the full windup without skipping the zero boundary.");
            Step();
            FailIf(soldier.State != 10 || soldier.Counter1 != 1 || head.State is not (3 or 4),
                "Actual enemy pass must signal the later head pass to align/release.");
            for (int i = 0; i < 96 && soldier.Counter1 != 0; i++) Step();
            FailIf(soldier.Counter1 != 0 || soldier.State != 10,
                "Head retraction must publish completion after the soldier has already updated.");
            Step();
            FailIf(soldier.State is not (8 or 9) || head.State is not (1 or 2),
                "Soldier must consume the completed throw on the next enemy update.");
            LoadValidationRoom(4, 0x99);
            Step();
            FailIf(_entities.Entities<BallChainSoldierCharacter>().Count != 1 || _entities.Entities<SpikedBallPart>().Count != 4,
                "The second Crown placement $4:$99 must use the same shared soldier and four native parts.");
        }
        GD.Print("Validated actual Crown $4:$99/$a0 soldier placements, entrance approach, ordered four-part spawn, windup and throw handoff in single/batched updates.");
    }

    private void ValidateCrownDungeonSpikedBallCollisions()
    {
        var visual = new SpikedBallDatabase();
        foreach (var (state, level, recoil, invincibility) in new[] {
            (SwordActionState.Swing, 1, 11, -28),
            (SwordActionState.Spin, 1, 19, -28),
            (SwordActionState.Spin, 2, 0, -20) })
        {
            var soldier = new BallChainSoldierCharacter();
            soldier.Initialize(new EnemyDatabase().ImportedEnemy(0x4b), Room060MovementFixture(), new(64,64), new());
            soldier.UpdateFrame(new(64,100), new(64,100), 4, () => { });
            var ball = new SpikedBallPart(soldier, visual, 0);
            var adapter = new SpikedBallRoomEntity(ball, _ => { });
            ball.UpdateFrame(new(64,100));
            var spawns = new List<RoomEntitySpawn>();
            adapter.SetLinkSwordState(state, level);
            FailIf(!adapter.ApplySwordHit(ball.CollisionBounds, new(64,100), 1, EnemyKnockbackStrength.Low, spawns) ||
                ball.InvincibilityCounter != invincibility || !ball.PendingCollision || soldier.InvincibilityCounter != 0 ||
                spawns.OfType<EnemyClinkSpawn>().Count() != 1,
                "PART $2a sword effects must publish pending status and clink without protecting the parent during collision.");
            bool bumped = adapter.TryGetSwordAttackerKnockback(EnemyKnockbackStrength.Low, out var response);
            FailIf(bumped != (recoil != 0) || response.Frames != recoil,
                "PART $2a must use distinct L1 swing/spin and L2 spin recoil profiles.");
            FailIf(adapter.ApplySwordHit(ball.CollisionBounds, new(64,100), 1, EnemyKnockbackStrength.Low, spawns),
                "PART $2a must reject a second item collision while invincible/pending.");
            ball.UpdateFrame(new(64,100));
            FailIf(ball.PendingCollision || ball.InvincibilityCounter != invincibility + 1 || soldier.InvincibilityCounter != -12,
                "PART $2a advances its own invincibility before publishing parent$f4 on the following part pass.");
            soldier.InvincibilityCounter = -5;
            ball.InvincibilityCounter = 0;
            adapter.ApplySwordHit(ball.CollisionBounds, new(64,100), 1, EnemyKnockbackStrength.Low, spawns);
            ball.UpdateFrame(new(64,100));
            FailIf(soldier.InvincibilityCounter != -5, "PART $2a must preserve existing parent invincibility.");
            adapter.ClearHealthAndCollision();
            soldier.InvincibilityCounter = 0;
            int angle = ball.Angle;
            ball.UpdateFrame(new(64,100));
            FailIf(ball.Finished || ball.CollisionEnabled || ball.Health != 0 || ball.Angle != ((angle + 1) & 31) ||
                soldier.InvincibilityCounter != -12,
                "Zero-health PART $2a must keep moving and revisit the retained sword status while collision stays disabled.");
            ball.Free(); soldier.Free();
        }
        var parent = new BallChainSoldierCharacter();
        parent.Initialize(new EnemyDatabase().ImportedEnemy(0x4b), Room060MovementFixture(), new(64,64), new());
        parent.UpdateFrame(new(64,100), new(64,100), 4, () => { });
        var head = new SpikedBallPart(parent, visual, 0);
        var target = new SpikedBallRoomEntity(head, _ => { });
        head.UpdateFrame(new(64,100));
        var effects = new List<RoomEntitySpawn>();
        var seeds = new SeedSatchelDatabase();
        foreach (int item in new[] { 0x20,0x21,0x22,0x23,0x24 })
        {
            FailIf(!seeds.TryGet(item, out var seed), $"Missing seed ${item:x2}.");
            var response = target.ApplySeedCollision(head.CollisionBounds, head.Position, seed, seed.Collision & 0x7f, effects);
            FailIf(!response.Contact || response.Effect == SeedHitResult.None || head.Health != 64 ||
                head.PendingCollision || head.InvincibilityCounter != 0,
                $"PART $2a must consume seed ${item:x2} through effect$20 without status/damage.");
        }
        FailIf(target.ApplyExpertPunch(head.CollisionBounds, head.Position, 4, effects) ||
            target.ApplyItemCollision(RoomEntityItemCollision.Bomb, head.CollisionBounds, head.Position, 4, effects),
            "PART $2a active mask excludes Expert Punch and bombs.");
        FailIf(!target.ApplyItemCollision(RoomEntityItemCollision.ThrownObject, head.CollisionBounds, head.Position, 4, effects) ||
            !head.PendingCollision || head.Health != 64 || head.InvincibilityCounter != 0,
            "PART $2a thrown-object effect$1c must publish status without damage/invincibility.");
        head.UpdateFrame(new(64,100));
        FailIf(parent.InvincibilityCounter != 0, "Thrown-object status must not grant sword/shield parent protection.");
        var chain = new SpikedBallPart(parent, visual, 1, head);
        var decorative = new SpikedBallRoomEntity(chain, _ => { });
        chain.UpdateFrame(new(64,100));
        FailIf(decorative.ApplySwordHit(chain.CollisionBounds, chain.Position, 1, EnemyKnockbackStrength.Low, effects),
            "Decorative PART $2a chain links must never enable item collisions.");
        chain.Free(); head.Free(); parent.Free();
        GD.Print("Validated isolated spiked-ball sword recoil, deferred parent protection, retained dead status, seed immunity, thrown status and decorative collision exclusion.");
    }

    private void ValidateCrownDungeonBallChainMotion()
    {
        var random = new OracleRandom();
        var soldier = new BallChainSoldierCharacter();
        soldier.Initialize(new EnemyDatabase().ImportedEnemy(0x4b), Room060MovementFixture(), new(64,64), random);
        var movementMemory = new OracleRuntimeState();
        soldier.BindMovementMemory(movementMemory);
        var visual = new SpikedBallDatabase();
        SpikedBallPart[] parts = [];
        Vector2 link = new(64,100);
        void Spawn()
        {
            var head = new SpikedBallPart(soldier, visual, 0);
            parts = [head, new(soldier, visual, 1, head), new(soldier, visual, 2, head), new(soldier, visual, 3, head)];
        }
        void Tick()
        {
            soldier.UpdateFrame(link, link, 4, Spawn);
            foreach (var part in parts) part.UpdateFrame(link);
        }
        soldier.UpdateFrame(link, link, 3, Spawn);
        soldier.Health = 3;
        soldier.UpdateFrame(link, link, 3, Spawn);
        FailIf(soldier.State != 0 || soldier.Visible || parts.Length != 0 || random.Calls != 2 || soldier.Health != 8,
            "$4b clean-US allocation gate must retry state0 and consume RNG while fewer than4 enemy slots remain.");
        Tick();
        var ball = parts[0];
        // Clean US $c09b + 7*$50 + angle1*2 contains -251/+49.
        // The final chain link scales by radius2 after the head and links1/2.
        FailIf(movementMemory.ReadWramByte(0xcec0) != 0x0a || movementMemory.ReadWramByte(0xcec1) != 0xfe ||
            movementMemory.ReadWramByte(0xcec2) != 0x62 || movementMemory.ReadWramByte(0xcec3) != 0,
            "The final spiked-chain part must leave its scaled radius2/angle1 words in shared movement scratch.");
        FailIf(soldier.State != 8 || random.Calls != 3 || ball.State != 1 || ball.Angle != 1 || ball.Radius != 10 ||
            !parts.Skip(1).Select(p => p.Radius).SequenceEqual(new[] { 7,4,2 }),
            "$4b initialization must create head then three links; link radii shift before multiplying.");
        Tick();
        FailIf(soldier.State != 9 || soldier.Counter1 != 90 || soldier.BallSignal != 1 ||
            ball.State != 2 || ball.Angle != 3 || soldier.AnimationIndex != 1,
            "$4b close Link must start full90-update windup and accelerate the ball in the later part pass.");
        for (int i = 0; i < 89; i++) Tick();
        FailIf(soldier.Counter1 != 1 || soldier.State != 9 || ball.Angle != 21,
            "$4b windup must last90 enemy updates; PART $2a rotates by2 each update.");
        Tick();
        FailIf(soldier.State != 10 || soldier.Counter1 != 1 || soldier.BallSignal != 2 || ball.State != 3,
            "$4b zero countdown must leave counter1=1 while the ball aligns for its throw.");
        for (int i = 0; i < 16 && ball.State == 3; i++) Tick();
        FailIf(ball.State != 4 || ball.Radius != 13, "PART $2a alignment must publish radius13 before the release update.");
        Tick();
        FailIf(ball.State != 5 || ball.Radius != 18 || ball.ExtensionSpeed != 0x340,
            "PART $2a throw must initialize radius18 and nonstandard16-bit speed$0340.");
        // Independent transcription of the high-byte-only extension sequence.
        int[] radii = [21,24,27];
        // Source speed drops$20 each update. Check representative boundaries
        // directly rather than reproducing the runtime arithmetic as an oracle.
        for (int i = 0; i < 3; i++) { Tick(); FailIf(ball.Radius != radii[i], "$2a initial extension high-byte arithmetic changed."); }
        Tick();
        FailIf(ball.Radius != 29 || ball.ExtensionSpeed != 0x2c0,
            "$2a fourth extension must use speed high$02, retaining the low-byte deceleration remainder.");
        ball.PublishCollision(4);
        Tick();
        FailIf(ball.ExtensionSpeed != -0x20 || ball.Radius != 29 || soldier.InvincibilityCounter != -12,
            "$2a pending sword collision must protect parent and stop outward speed before its next radius update.");
        Tick();
        FailIf(ball.Radius != 28 || ball.ExtensionSpeed != -0x40 || soldier.InvincibilityCounter != -11,
            "$2a retract must add signed high$ff, while the enemy independently advances its invincibility.");
        for (int i = 0; i < 64 && soldier.Counter1 != 0; i++) Tick();
        FailIf(soldier.Counter1 != 0 || soldier.State != 10 || ball.Radius < 10,
            "$2a full retraction must clear parent counter in the part pass without storing a radius below10.");
        Tick();
        FailIf(soldier.State != 9 || soldier.Counter1 != 90 || ball.State != 2,
            "$4b must observe completed retraction on its next enemy update and repeat against nearby Link.");
        for (int i = 0; i < 90; i++) Tick();
        for (int i = 0; i < 16 && ball.State == 3; i++) Tick();
        FailIf(ball.State != 4, "$2a repeated throw never reached alignment.");
        Tick();
        for (int i = 1; i <= 48; i++)
        {
            Tick();
            if (i is 19 or 27) FailIf(ball.Radius != 51, "$2a maximum radius must remain51 while speed high is0.");
            if (i == 28) FailIf(ball.Radius != 50, "$2a negative high-byte speed must start retracting on extension update28.");
        }
        FailIf(ball.Radius != 12 || ball.ExtensionSpeed != -0x2c0 || soldier.Counter1 != 1,
            "$2a uninterrupted throw must retain radius12 through extension update48.");
        Tick();
        FailIf(soldier.Counter1 != 0 || ball.Radius != 12 || ball.ExtensionSpeed != -0x2c0,
            "$2a update49 must signal retraction without writing radius9 or decelerating again.");
        link = new(120,112);
        Tick();
        FailIf(soldier.State != 8 || soldier.BallSignal != 0 || soldier.AnimationIndex != 0 || ball.State != 1,
            "$4b distant Link after retraction must restore slow rotation and walking animation.");
        link = new(64,100);
        Tick();
        int before = soldier.Counter1;
        soldier.BeginSwitchHook(link);
        Tick();
        FailIf(soldier.State != 3 || soldier.SwitchHookSubstate != 1 || soldier.Counter1 != before || soldier.ReturnState != 9,
            "$4b Switch Hook must preserve windup state/counter and continue its independent ball.");
        soldier.CopySwitchHookPosition(soldier.Position, -1);
        soldier.SwapSwitchHook(); soldier.ReleaseSwitchHook();
        for (int i = 0; i < 16 && soldier.State == 3; i++) Tick();
        FailIf(soldier.State != 9 || soldier.Counter1 != before, "$4b landing must restore saved state without consuming its countdown.");
        soldier.InvincibilityCounter = 0;
        FailIf(!soldier.TakeSwordHit(link, 8), "$4b isolated lethal sword hit was rejected.");
        Tick();
        FailIf(soldier.IsDead || soldier.Counter1 != before - 1 || soldier.CollisionEnabled || soldier.KnockbackCounter != 0,
            "$4b JUST_HIT continues AI even at0HP and never gives sword recoil.");
        Tick();
        FailIf(!soldier.IsDead || parts.Any(p => !p.Finished),
            "$4b next no-health dispatch must delete its head and all three linked parts in part order.");
        foreach (var part in parts) part.Free();
        soldier.Free();
        GD.Print("Validated isolated Ball & Chain Soldier allocation, windup, part ordering, alignment, signed retraction, block protection, repeat, Switch Hook and deferred death.");
    }

    private void ValidateCrownDungeonBallChainSourceData()
    {
        var data = EnemyBehaviorTables.Shared.BallChain;
        FailIf(data.SpeedRaw != 15 || data.AttackDistance != 0x38 || data.WindupFrames != 90 ||
            data.RequiredEnemySlots != 4 || data.OriginOffset != -5 || data.OrbitRadius != 10 ||
            data.ReleaseRadius != 13 || data.ThrowRadius != 18 || data.ExtensionSpeed != 0x340 ||
            data.ExtensionDeceleration != 0x20 || data.SlowRotation != 1 || data.FastRotation != 2 ||
            data.AngleMask != 0x1f || data.ParentBlockInvincibility != -12,
            "$4b/$2a source windup, clean-US enemy-slot gate, orbit, throw or block operands changed.");
        int[] body = [2,6,5,5,11,11,11,11,11,11,0,11,0x16,0x2e,0x1b,0x25,
            0,0,0,0,0,0x2f,11,0x1b,11,11,0x20,0x20,11,0x20,0x20,0];
        int[] ball = [2,0x17,0x16,0x16,0x15,0x15,0x15,0x16,0x1b,0x15,0,0,0,0,0,0,
            0,0,0,0,0,0x2d,0x1c,0x1b,0,0x20,0x20,0x20,0x20,0x20,0x20,0];
        FailIf(!data.BodyEffects.Select(v => v.Value).SequenceEqual(body) ||
            !data.BallEffects.Select(v => v.Value).SequenceEqual(ball) ||
            string.Concat(data.BodyMask.Select(v => v.Value)) != "11111111110111110000011111111110" ||
            string.Concat(data.BallMask.Select(v => v.Value)) != "11111111110000000000011101111110" ||
            !data.PartData.Select(v => v.Value).SequenceEqual(new[] { 0x99,0x74,0x66,0xfc,0x40,8,2,0 }),
            "$4b collision$37 and PART_SPIKED_BALL $2a collision$74 source masks/effects/properties changed.");
        var enemy = new EnemyDatabase().ImportedEnemy(0x4b);
        FailIf(enemy.Health != 8 || enemy.DamageQuarters != 2 || enemy.TileBase != 0 ||
            enemy.Palette != 2 || enemy.RadiusX != 6 || enemy.RadiusY != 6 ||
            enemy.Animations.Length != 2 ||
            enemy.Animations[0] != "16@8,0,0,0;8,8,2,0|16,1@8,0,4,0;8,8,6,0" ||
            enemy.Animations[1] != "8@8,0,0,0;8,8,2,0|8@8,0,4,0;8,8,6,0",
            "$4b soldier health, graphics, OAM or animation timing changed.");
        var weapon = new SpikedBallDatabase();
        FailIf(weapon.Sprite != "spr_ballandchain_likelike" || weapon.TileBase != 8 ||
            weapon.Palette != 2 || weapon.RadiusX != 6 || weapon.RadiusY != 6 || weapon.Damage != 2 ||
            weapon.Animations[0] != "127,0@8,0,0,0;8,8,0,32" ||
            weapon.Animations[1] != "127,0@8,4,2,0",
            "PART_SPIKED_BALL $2a must retain its own tile base and collision radii.");
        GD.Print("Validated Ball & Chain Soldier and spiked-ball source operands, collision tables and graphics.");
    }
}
