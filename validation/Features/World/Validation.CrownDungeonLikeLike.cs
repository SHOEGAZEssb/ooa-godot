using Godot;
using System.Linq;
using System;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateCrownDungeonLikeLikeSeeds()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput", flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates", flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate", flags)!.CreateDelegate(typeof(Action), this);
        var random = CaptureOracleRandomForValidation();
        var seeds = new SeedSatchelDatabase();
        var shooter = SeedShooterRecord.Load();
        foreach (bool batch in new[] { false, true })
        foreach (var (item, health) in new[] { (0x20, 5), (0x20, 1), (0x22, 5), (0x23, 5) })
        {
            RestoreOracleRandomForValidation(random);
            void Step(int count = 1)
            {
                input.CaptureForValidation([], [], Vector2.Zero);
                if (batch) scheduler.Advance(count / 60.0, update);
                else for (int i = 0; i < count; i++) scheduler.Advance(1.0 / 60.0, update);
            }
            LoadValidationRoom(4, 0x9f);
            _player.WarpTo(new(120,24));
            Step(2);
            var target = _entities.Entities<LikeLikeCharacter>()[1];
            target.Health = health;
            var adapter = _entities.EntityAdapters<LikeLikeRoomEntity>().ElementAt(1);
            int enemies = _entities.RoomEnemyCount;
            FailIf(!seeds.TryGet(item, out var seed), $"Missing seed ${item:x2}.");
            var projectile = _entities.Spawn<EmberSeedEffect>(new EmberSeedSpawn(
                target.Position + Vector2.Down * 16 - shooter.Offsets[0], Vector2I.Up, seed, 4, SeedLaunchKind.Shooter));
            for (int i = 0; i < 16 && target.StunCounter == 0 && !adapter.GaleCaught; i++) Step();
            FailIf(projectile.CollisionEnabled || target.StunCounter == 0 && !adapter.GaleCaught,
                $"Like Like $24 did not consume seed ${item:x2}.");
            if (item == 0x20)
            {
                int damagedHealth = Math.Max(0, health - 2);
                FailIf(target.Health != damagedHealth || target.StunCounter != 90 || target.InvincibilityCounter != -90 || !target.PendingHit ||
                    target.CollisionEnabled != (damagedHealth != 0),
                    "ENEMYDMG_2c must apply Ember damage2, stun90, $a6 invincibility and JUST_HIT.");
                Step();
                var flame = _entities.Entities<BurningEnemyPart>().Single();
                FailIf(target.Health != 1 || flame.Counter != 58 || target.StunCounter != 90,
                    "First burn part pass must borrow health after the enemy's JUST_HIT return.");
                Step(58);
                FailIf(target.Health != damagedHealth || target.InvincibilityCounter != 0 || target.IsDead || _entities.RoomEnemyCount != enemies,
                    "Burn completion must restore damaged health before the next enemy pass, without releasing the room count.");
                if (damagedHealth == 0)
                {
                    Step();
                    FailIf(_entities.Entities<LikeLikeCharacter>().Contains(target) || _entities.RoomEnemyCount != enemies,
                        "Lethal burn must transfer the Like Like count to its death puff.");
                    Step(32);
                    FailIf(_entities.RoomEnemyCount != enemies - 1,
                        "The Like Like death puff must release exactly one enemy count.");
                }
            }
            else if (item == 0x22)
            {
                FailIf(target.Health != 5 || target.StunCounter != 240 || target.InvincibilityCounter != -16 || target.PendingHit,
                    "ENEMYDMG_38 must preserve health and omit JUST_HIT while applying Pegasus stun.");
                int counter = target.Counter1;
                int remaining = 480 - ((_entities.FrameCounter & 1) == 0 ? 1 : 0);
                Step(remaining);
                FailIf(target.StunCounter != 0 || target.Counter1 != counter || target.Health != 5,
                    "Like Like Pegasus stun must consume exactly240 odd-frame ticks without advancing its movement countdown.");
            }
            else
            {
                FailIf(!adapter.GaleCaught || target.Health != 5,
                    "Like Like collision$1e must enter native Gale capture without health damage.");
                for (int i = 0; i < 240 && _entities.Entities<LikeLikeCharacter>().Contains(target); i++) Step();
                FailIf(_entities.Entities<LikeLikeCharacter>().Contains(target) || _entities.RoomEnemyCount != enemies - 1,
                    "Like Like Gale completion must delete exactly one enemy and release its count.");
            }
        }
        GD.Print("Validated Crown Like Like live Ember/Pegasus/Gale collision, burn health handoff, stun duration and enemy-count completion in single/batched updates.");
    }

    private void ValidateCrownDungeonLikeLikeContact()
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
            LoadValidationRoom(4, 0x9f);
            _player.WarpTo(new(120,8));
            Step(16, Vector2.Down);
            var target = _entities.Entities<LikeLikeCharacter>()[1];
            _inventory.GiveTreasure(TreasureDatabase.TreasureShield, 2);
            _inventory.EquipA(InventoryState.ItemShield);
            Step(32, Vector2.Left);
            for (int i = 0; i < 64 && !_player.EnemyGrabPending; i++) Step(movement: Vector2.Down);
            FailIf(!_player.EnemyGrabPending, $"Like Like approach missed: Link {_player.Position}, target {target.Position}.");
            Step();
            FailIf(target.State != 11 || target.Counter2 != 90 || !_player.EnemyGrabActive,
                "$4:$9f actual contact must initialize Link and Like Like on the following object pass.");
            Step(89);
            FailIf(target.Counter2 != 1 || _dialogue.IsOpen, "Shield loss must wait for hold update90.");
            Step();
            FailIf(target.State != 12 || target.Counter2 != 60 || !_dialogue.IsOpen ||
                _inventory.HasTreasure(TreasureDatabase.TreasureShield) || _player.EnemyGrabSubstate != 4,
                "$4:$9f capture must lose the shield and open TX_510b before Link's release update.");
            Step();
            FailIf(_player.EnemyGrabActive || _player.InvincibilityFrames != -107 || target.Counter2 != 60,
                "Link release must run during shield dialogue while the enemy cooldown remains paused.");
            _dialogue.Close();
            Step(107);
            FailIf(_player.InvincibilityFrames != 0 || !target.CollisionEnabled,
                "Like Like and Link must both become vulnerable after their separate cooldowns.");
            for (int i = 0; i < 64 && !_player.EnemyGrabPending; i++) Step(movement: Vector2.Left);
            FailIf(!_player.EnemyGrabPending, "$4:$9f Like Like must be reachable again through the same open floor.");
            Step();
            FailIf(target.State != 11 || target.Counter2 != 90, "Repeated contact must restart the full hold.");
            Step(90);
            FailIf(target.State != 12 || _dialogue.IsOpen || _player.EnemyGrabSubstate != 4,
                "A second capture without an owned shield must release without requesting shield-loss dialogue.");
            Step();
            FailIf(_player.EnemyGrabActive || _player.InvincibilityFrames != -107,
                "A repeat release must retain the same Link timing.");
        }
        GD.Print("Validated Crown $4:$9f Like Like approach, real capture, shield dialogue/release, cooldown and repeat in single/batched gameplay updates.");
    }

    private void ValidateCrownDungeonLikeLikeSourceData()
    {
        var data = EnemyBehaviorTables.Shared.LikeLike;
        // Independent transcription of likelike.s and linkState0d. Mashing
        // avoids shield loss, not the 90-update hold; the zero update skips
        // reading buttons. Release sets native signed invincibility $94.
        FailIf(data.SpeedRaw != 10 || data.AngleMask != 0x18 ||
            data.DurationMask != 0x30 || data.DurationBase != 0x38 ||
            data.HoldFrames != 90 || data.ShieldEscapePresses != 19 ||
            data.CooldownFrames != 60 || data.ShieldLostText != 0x510b ||
            data.ReleaseInvincibility != -108,
            "ENEMY_LIKE_LIKE $24 source movement, capture or release operands changed.");
        int[] effects = [0x3a,0,0,0,8,9,9,10,10,8,8,10,13,0x2e,8,0x25,
            0,0,0,0x22,13,0x2f,9,0x22,10,9,0x20,0x27,8,0x28,0x29,0];
        const string mask = "10001111111111110001111111111110";
        FailIf(!data.CollisionEffects.Select(v => v.Value).SequenceEqual(effects) ||
            string.Concat(data.ActiveCollisions.Select(v => v.Value)) != mask ||
            !data.LinkDamage.Select(v => v.Value).SequenceEqual(new[] { 0x62,0xf4,0,0 }) ||
            !data.EnemyDamage.Select(v => v.Value).SequenceEqual(new[] { 0x40,0,0,0 }),
            "ENEMY_LIKE_LIKE $24 collision $22 or deferred grab status changed.");
        FailIf(data.CollisionEffects.Any(v => !v.Source.StartsWith(
            "data/ages/objectCollisionTable.s:objectCollisionTable+$0440")),
            "ENEMY_LIKE_LIKE $24 collision rows lost source identity.");
        var definition = new EnemyDatabase().ImportedEnemy(0x24);
        FailIf(definition.Health != 5 || definition.DamageQuarters != 2 ||
            definition.TileBase != 12 || definition.Palette != 3 ||
            definition.RadiusX != 6 || definition.RadiusY != 6 || definition.Animations.Length != 2,
            "ENEMY_LIKE_LIKE $24 enemyData/extraEnemyData/$11 graphics and properties changed.");
        // enemyAnimation3756a/37596 loop to their first frame. OAM pointer
        // aliases resolve through enemyOamData4d35e..379 and4d47e.
        FailIf(definition.Animations[0] != "24@8,0,0,0;8,8,0,32|24,1@8,0,2,0;8,8,2,32" ||
            definition.Animations[1] != "10@8,0,4,0;8,8,4,32|8@8,0,6,0;8,8,6,32|9@8,0,8,0;8,8,8,32",
            "ENEMY_LIKE_LIKE $24 animation durations, parameters, loops or ordered OAM changed.");
        FailIf(new EnemyDatabase().LikeLikeShieldText != "Your \\col(1)shield\\col(0)\nwas eaten!",
            "TX_510b must preserve shield color commands and the source line break.");
        GD.Print("Validated Like Like $24 source operands, collision masks, graphics and shield-loss text.");
    }

    private void ValidateCrownDungeonLikeLikeStateMachine()
    {
        LoadValidationRoom(4, 0x91);
        var record = new EnemyDatabase().ImportedEnemy(0x24);
        var room = Room060MovementFixture();
        var random = new OracleRandom();
        var enemy = new LikeLikeCharacter();
        enemy.Initialize(record, room, new(64,64), random);
        int textRequests = 0;
        void Tick(bool pressed = false) => enemy.UpdateFrame(_player, pressed, 0, () => textRequests++);
        Tick();
        FailIf(enemy.State != 8 || enemy.CollisionEnabled || random.Calls != 1,
            "$24 state0 must consume var3d RNG and leave collision disabled until state8.");
        Tick();
        // Seed $0d37 yields $27a5 then $761a. Direction uses H, duration L.
        FailIf(enemy.State != 10 || enemy.Angle != 0x10 || enemy.Counter1 != 0x48 || random.Calls != 2,
            "$24 state8/9 must choose angle H&$18 and duration$38+(L&$30) from one RNG call.");
        for (int i = 0; i < 71; i++) Tick();
        FailIf(enemy.Counter1 != 1 || enemy.Position != new Vector2(64,81.75f),
            "$24 must move SPEED_40 on exactly71 nonzero countdown updates.");
        Tick();
        FailIf(enemy.State != 9 || enemy.Position != new Vector2(64,81.75f) || random.Calls != 2,
            "$24 counterzero only selects state9; choosing the next route waits one update.");
        enemy.BeginPegasusHit();
        enemy.UpdateFrame(_player, false, 1, () => textRequests++);
        FailIf(enemy.StunCounter != 239 || enemy.InvincibilityCounter != -15 || enemy.PendingHit,
            "ENEMYDMG_38 must stun on the first odd update without a JUST_HIT dispatch delay.");
        enemy.Free();

        foreach (int presses in new[] { 0,18,19 })
        {
            _player.WarpTo(new(64,80));
            _inventory.GiveTreasure(TreasureDatabase.TreasureShield, 2);
            _inventory.EquipA(InventoryState.ItemShield);
            random = new(); enemy = new(); textRequests = 0;
            enemy.Initialize(record, room, new(64,64), random);
            Tick(); Tick();
            FailIf(!_player.RequestLikeLikeGrab(), "Like Like state fixture requires vulnerable Link.");
            _player.AdvanceApplicationUpdate();
            enemy.MarkCapture(); Tick();
            FailIf(enemy.State != 11 || enemy.Counter2 != 90 || enemy.Counter1 != 0 ||
                enemy.CollisionEnabled || _player.PatchCollisionsEnabled || enemy.AnimationIndex != 1,
                "$24 capture must initialize both counters, eating animation and both collision gates.");
            for (int i = 0; i < 89; i++) Tick(i < presses);
            FailIf(enemy.State != 11 || enemy.Counter2 != 1 || textRequests != 0 ||
                !_inventory.HasTreasure(TreasureDatabase.TreasureShield),
                "$24 must hold Link for all90 updates, including when19 presses already protected the shield.");
            Tick(true); // The release update does NOT count this button press.
            FailIf(enemy.State != 12 || enemy.Counter2 != 60 || enemy.Counter1 != presses ||
                enemy.Angle != 0x18 || random.Calls != 3 || textRequests != (presses < 19 ? 1 : 0) ||
                _inventory.HasTreasure(TreasureDatabase.TreasureShield) != (presses >= 19) ||
                _inventory.ShieldLevel != 2 || !_player.PatchCollisionsEnabled || _player.EnemyGrabSubstate != 4,
                "$24 release lost the19-press threshold, late-edge exclusion, shield level retention or single RNG draw.");
            Vector2 released = enemy.Position;
            for (int i = 0; i < 59; i++) Tick();
            FailIf(enemy.State != 12 || enemy.Counter2 != 1 || enemy.CollisionEnabled,
                "$24 must keep collision disabled for the full cooldown.");
            Vector2 beforeZero = enemy.Position;
            Tick();
            FailIf(enemy.State != 9 || !enemy.CollisionEnabled || enemy.Position != beforeZero ||
                enemy.Position == released, "$24 cooldownzero must reenable collision without moving on that update.");
            enemy.Free();
        }
        _player.WarpTo(new(120,112));
        GD.Print("Validated Like Like $24 RNG/movement boundaries,90-update capture,19 presses,shield loss and60-update cooldown.");
    }
}
