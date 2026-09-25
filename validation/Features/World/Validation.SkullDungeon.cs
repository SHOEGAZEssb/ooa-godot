using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullDungeonBladeTraps()
    {
        var database = new EnemyDatabase();
        var definition = database.ImportedEnemy(0x0e, 1);
        FailIf(definition is not { Health: 127, DamageQuarters: 2, RadiusX: 4, RadiusY: 4,
            TileBase: 0, Palette: 1, Animations.Length: 3 },
            "$0e:$01 lost enemy0eSubidData's extra-data $02, blue palette or trap OAM.");
        FailIf(!EnemyBehaviorTables.Shared.BladeTrap.Select(v => v.Value)
                .SequenceEqual(new[] { 8, 13, 60, 30, 120, 88, 7, 16 }) ||
            !EnemyBehaviorTables.Shared.BladeTrapCollisionEffects.Select(v => v.Value).SequenceEqual(new[] {
                0x3c, 0x06, 0x06, 0x06, 0x15, 0x16, 0x16, 0x16, 0x17, 0x15, 0, 0, 0x16, 0x1b, 0x15, 0,
                0, 0, 0, 0, 0, 0x2d, 0x1c, 0x1b, 0, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0 }),
            "Blue blade trap source operands or complete collision row $13 changed.");
        foreach (var (room, positions) in new[] {
            (0x70, new Vector2[] { new(0x28, 0x98), new(0x28, 0x18), new(0x48, 0x98), new(0x48, 0x18) }),
            (0x78, new Vector2[] { new(0xd8, 0x18), new(0xd8, 0x98) }),
            (0x8a, new Vector2[] { new(0x68, 0x18), new(0x68, 0x98) }) })
        {
            _saveData.SetRoomFlag(4, room, 0xff, false);
            LoadValidationRoom(4, room);
            FailIf(!_entities.Entities<BladeTrapCharacter>().Select(t => t.Position).SequenceEqual(positions),
                $"Room 4:{room:x2} lost the ordered blue traps from enemyData.s.");
            var records = database.GetRoomObjects(4, room).Where(r => r.Id == 0x0e).ToArray();
            FailIf(records.Any(r => database.EnemyHandlers.ResolveHandler(r).CombatSource(r, 0).CountsAsEnemy),
                $"Room 4:{room:x2} trap flag $02 incorrectly contributes to room-clear count.");
        }

        LoadValidationRoom(4, 0x70);
        var random = new OracleRandom();
        var sounds = new System.Collections.Generic.List<int>();
        var trap = new BladeTrapCharacter();
        Vector2 origin = new(0x28, 0x18), target = new(0x28, 0x58);
        trap.Initialize(definition, _currentRoom, origin, random, sounds.Add);
        trap.UpdateFrame(target);
        FailIf(trap.State != BladeTrapState.Initializing || random.Calls != 1 || trap.Position != origin,
            "Blade trap state0 must consume common var3d RNG without charging/moving.");
        trap.PrepareForScreenTransition();
        FailIf(trap.State != BladeTrapState.Initializing || random.Calls != 1 || trap.Position != origin,
            "Repeating blade trap preload must not rerun state0 RNG or dispatch state8's target acquisition.");
        trap.UpdateFrame(target);
        FailIf(trap.State != BladeTrapState.Charging || trap.Angle != 0x10 || trap.SpeedRaw != 60 ||
            trap.Position != origin || !sounds.SequenceEqual(new[] { OracleSoundEngine.SndUnknown5 }),
            $"Room 4:70 upper-left trap did not activate through its original corridor: {trap.State}, angle={trap.Angle:x2}.");
        // SPEED_180 is 1.5 pixels/update; first center-band position is 81.
        for (int i = 0; i < 37; i++) trap.UpdateFrame(target);
        FailIf(trap.State != BladeTrapState.Charging || trap.Position != new Vector2(40, 79.5f),
            "Blue trap retracted before entering the inclusive $58 +/- 7 center band.");
        trap.UpdateFrame(target);
        FailIf(trap.State != BladeTrapState.Retracting || trap.Position != new Vector2(40, 81) ||
            trap.Angle != 0 || trap.SpeedRaw != 30 || sounds.Last() != OracleSoundEngine.SndClink,
            "Blue trap lost the center-band boundary, reversed angle, SPEED_c0 or SND_CLINK.");
        for (int i = 0; i < 100 && trap.State == BladeTrapState.Retracting; i++) trap.UpdateFrame(target);
        FailIf(trap.State != BladeTrapState.Cooldown || trap.Counter != 16,
            "Blue trap must retract to its wall, then begin a full 16-update cooldown.");
        for (int i = 0; i < 15; i++) trap.UpdateFrame(target);
        FailIf(trap.State != BladeTrapState.Cooldown || trap.Counter != 1,
            "Blue trap cooldown ended before update16.");
        trap.UpdateFrame(target);
        FailIf(trap.State != BladeTrapState.Waiting, "Blue trap cooldown zero update must only enter waiting.");
        trap.UpdateFrame(target);
        FailIf(trap.State != BladeTrapState.Charging || random.Calls != 1,
            "Blue trap could not reactivate after its cooldown or consumed nonsource RNG.");
        trap.Free();

        var waiting = new BladeTrapCharacter();
        waiting.Initialize(definition, _currentRoom, origin, new OracleRandom(), _ => { });
        waiting.UpdateFrame(new Vector2(54, 88));
        waiting.UpdateFrame(new Vector2(54, 88));
        FailIf(waiting.State != BladeTrapState.Waiting, "Blue trap accepted an orthogonal offset of 14; source range is inclusive +/-13.");
        Vector2 obstruction = new(40, 56);
        var originalTile = _currentRoom.GetTerrainInfo(obstruction);
        byte tileId = _currentRoom.GetMetatile(obstruction);
        _currentRoom.SetPositionTileAndCollision(obstruction, tileId, 1, 0);
        waiting.UpdateFrame(new Vector2(53, 88));
        FailIf(waiting.State != BladeTrapState.Waiting,
            "Blue trap ignored a partly solid intermediate tile's collision byte.");
        _currentRoom.SetPositionTileAndCollision(obstruction, tileId, originalTile.Collision, 0);
        waiting.UpdateFrame(new Vector2(53, 88));
        FailIf(waiting.State != BladeTrapState.Charging, "Blue trap rejected the inclusive 13-pixel activation boundary after removing an obstruction.");
        var source = database.GetRoomObjects(4, 0x70).First(r => r.Id == 0x0e);
        var adapter = new BladeTrapRoomEntity(waiting,
            database.EnemyHandlers.ResolveHandler(source).CombatSource(source, 0), _ => { });
        var effects = new System.Collections.Generic.List<RoomEntitySpawn>();
        FailIf(!adapter.ApplySwordHit(waiting.CollisionBounds, origin + Vector2.Down * 12, 255,
                EnemyKnockbackStrength.Normal, effects) || waiting.Health != 127 ||
            waiting.InvincibilityCounter != -28 || effects.OfType<EnemyClinkSpawn>().Count() != 1 ||
            !adapter.TryGetSwordAttackerKnockback(EnemyKnockbackStrength.Normal, out var recoil) || recoil.Frames != 19,
            "Blade trap armor lost ENEMYDMG_$34 invincibility, clink or LINKDMG_$14 recoil.");
        Vector2 beforeArmorUpdate = waiting.Position;
        waiting.UpdateFrame(target);
        FailIf(waiting.Position == beforeArmorUpdate || waiting.InvincibilityCounter != -27,
            "A sword strike paused blue trap movement or decremented its invincibility incorrectly.");
        waiting.InvincibilityCounter = 0;
        foreach (int seed in new[] { 0x20, 0x21, 0x24 })
            FailIf(adapter.ApplySeedHit(waiting.CollisionBounds, origin, seed, effects) != SeedHitResult.Activate || waiting.Health != 127,
                $"Blade trap must consume seed ${seed:x2} through effect $20 without damage/burning.");
        FailIf(adapter.IsSeedBurning || adapter.ApplyExpertPunch(waiting.CollisionBounds, origin, 4, effects) ||
            adapter.ApplyItemCollision(RoomEntityItemCollision.Bomb, waiting.CollisionBounds, origin, 16, effects),
            "Blade trap accepted an unsupported burn, expert punch or bomb damage path.");
        waiting.Free();

        void Step(int count) =>
            StepGameplayUpdates(count, Vector2.Zero, [], [], batched: true);
        var startRandom = CaptureOracleRandomForValidation();
        (Vector2 Position, BladeTrapState State, int Angle, int Counter)[] Run(bool batched)
        {
            RestoreOracleRandomForValidation(startRandom);
            LoadValidationRoom(4, 0x70);
            FailIf(_currentRoom.IsSolid(target), "Blade trap gameplay fixture must stand in the actual open corridor.");
            _player.WarpTo(target);
            if (batched) Step(10);
            else for (int i = 0; i < 10; i++) Step(1);
            return _entities.Entities<BladeTrapCharacter>().Select(t => (t.Position, t.State, t.Angle, t.Counter)).ToArray();
        }
        var singles = Run(false);
        var batch = Run(true);
        FailIf(!singles.SequenceEqual(batch) || batch[1].Position != new Vector2(40, 36) ||
            batch[1].State != BladeTrapState.Charging,
            "Blue trap actual gameplay phases differed between ten individual updates and one ten-update host frame.");
        LoadValidationRoom(4, 0x91);
        FailIf(_entities.Entities<BladeTrapCharacter>().Count != 0, "Leaving 4:70 retained outgoing blue traps.");
        LoadValidationRoom(4, 0x70);
        FailIf(_entities.Entities<BladeTrapCharacter>().Any(t => t.State != BladeTrapState.Uninitialized),
            "Blue trap re-entry retained a previous charge/cooldown state.");
        var textSource = _entities.TextActiveSource;
        try
        {
            _entities.TextActiveSource = () => true;
            int calls = _random.Calls;
            Step(1);
            FailIf(_random.Calls != calls + 4 || _entities.Entities<BladeTrapCharacter>().Any(t =>
                !t.Visible || t.State != BladeTrapState.Initializing || t.SpeedRaw != 8),
                "Text-active enemy dispatch must run the four state0 traps and their ordered var3d RNG exactly once.");
            Step(24);
            FailIf(_random.Calls != calls + 4 || _entities.Entities<BladeTrapCharacter>().Any(t => t.State != BladeTrapState.Initializing),
                "Initialized blue traps must freeze under text without acquiring Link or consuming RNG.");
        }
        finally { _entities.TextActiveSource = textSource; }
        GD.Print("Validated Skull Dungeon blade traps in 4:70/78/8a: ordered placements, non-counting flags, source collision row, common RNG, corridor activation, center limit, retraction and exact cooldown/retrigger.");
    }

    private void ValidateSkullDungeonFallingRopes()
    {
        void Step(int count = 1) =>
            StepGameplayUpdates(count, Vector2.Zero, [], [], batched: true);

        _saveData.SetRoomFlag(4, 0x73, 0xff, false);
        LoadValidationRoom(4, 0x73);
        var ropes = _entities.Entities<RopeCharacter>().ToArray();
        FailIf(ropes.Length != 4 || ropes.Any(r => r.Record.SubId != 1 || r.Visible || r.CollisionEnabled),
            "group4Map73EnemyObjectData must allocate four initially invisible $10:$01 Ropes from random flags $80.");
        Vector2[] starts = ropes.Select(r => r.Position).ToArray();
        int calls = _entities.RandomCalls;
        Step();
        FailIf(_entities.RandomCalls != calls + 4 || ropes.Any(r => r.State != RopeState.FallSetup || r.Visible),
            "$10:$01 state 0 must consume var3d RNG and remain invisible without a fall-delay roll.");
        Step();
        FailIf(_entities.RandomCalls != calls + 8 || ropes.Any(r => r.State != RopeState.WaitingToFall ||
            r.Counter < 1 || r.Counter > 57 || (r.Counter & 7) != 1 || r.CollisionEnabled),
            "$10:$01 state 8 must roll (random & $38)+1 once per Rope and keep collisions disabled.");
        Step(120);
        FailIf(ropes.Any(r => !r.Visible || !r.CollisionEnabled || r.ZFixed != 0 ||
            r.State is RopeState.WaitingToFall or RopeState.Falling),
            "Room 4:73 Ropes did not finish their wait/fall through the batched gameplay scheduler: " +
            string.Join(", ", ropes.Select(r => $"{r.State}/{r.Counter}/z={r.ZFixed}/visible={r.Visible}/collision={r.CollisionEnabled}")));

        var database = new EnemyDatabase();
        foreach (int camera in new[] { 0, 32 })
        {
            var random = new OracleRandom();
            var prediction = new OracleRandom();
            var sounds = new System.Collections.Generic.List<int>();
            var rope = new RopeCharacter();
            Vector2 start = starts[0];
            rope.Initialize(database.ImportedEnemy(0x10, 1), _currentRoom, start, random, sounds.Add, () => camera);
            int scentCounter = prediction.Next().Value;
            int delay = (prediction.Next().Value & 0x38) + 1;
            rope.UpdateFrame(Vector2.Zero, start + Vector2.Right * 32);
            rope.UpdateFrame(Vector2.Zero, start + Vector2.Right * 32);
            FailIf(rope.Counter != delay || rope.ScentAttractionCounter != scentCounter || random.Calls != 2,
                "$10:$01 lost the ordered var3d/delay rolls or accepted a Scent Seed before falling.");
            for (int i = 1; i < delay; i++) rope.UpdateFrame(Vector2.Zero);
            FailIf(rope.Visible || rope.Counter != 1 || sounds.Count != 0,
                "$10:$01 became visible or sounded before its delay reached zero.");
            rope.UpdateFrame(Vector2.Zero);
            int c = (-(int)start.Y - 8) & 0xff;
            int a = camera + c;
            if (a > 255) a = c;
            if (a < 128) a = 128;
            int initialZ = (a - 256) * 256;
            FailIf(rope.ZFixed != initialZ || rope.SpeedZ != 256 || !rope.CollisionEnabled ||
                rope.Position != start || !sounds.SequenceEqual(new[] { OracleSoundEngine.SndFallInHole }),
                "$10:$01 did not preserve ecom_setZAboveScreen's camera/carry clamp, speedZ=$0100 and falling sound.");
            int fallUpdates = 0;
            // Source integrates speed before adding $0e gravity: after n
            // updates the displacement is 256*n + 7*n*(n-1).
            while (initialZ + 256 * fallUpdates + 7 * fallUpdates * (fallUpdates - 1) < 0) fallUpdates++;
            for (int i = 1; i < fallUpdates; i++) rope.UpdateFrame(Vector2.Zero, start + Vector2.Right * 32);
            FailIf(rope.State != RopeState.Falling || rope.Position != start || random.Calls != 2,
                "$10:$01 accepted scent/movement or landed before its source gravity boundary.");
            var turn = prediction.Next();
            rope.UpdateFrame(Vector2.Zero, start + Vector2.Right * 32);
            FailIf(rope.State != RopeState.Wandering || rope.ZFixed != 0 || rope.SpeedZ != 0 ||
                rope.Angle != (turn.High & 0x18) || rope.Counter != 0x70 + (turn.Low & 0x70) ||
                random.Calls != 3 || sounds.Last() != OracleSoundEngine.SndBombLand,
                "$10:$01 landing lost its exact boundary, direction roll, cleared speedZ or SND_BOMB_LAND.");
            rope.UpdateFrame(Vector2.Zero, start + Vector2.Right * 32);
            FailIf(rope.State != RopeState.FollowingScentSeed,
                "$10:$01 did not enable scent attraction on the update after landing.");
            rope.Free();
        }
        LoadValidationRoom(4, 0x91);
        LoadValidationRoom(4, 0x73);
        FailIf(_entities.Entities<RopeCharacter>().Count != 4 || _entities.Entities<RopeCharacter>().Any(r => r.Visible),
            "Room 4:73 re-entry did not reset the falling Rope lifecycle.");
        GD.Print("Validated Skull Dungeon 4:73 falling Ropes: source placement count, RNG order, invisible wait, camera height, gravity, collision/scent gates, landing, batched gameplay and re-entry.");
    }

    private void ValidateSkullDungeonShroudedStalfos()
    {
        var database = new EnemyDatabase();
        void Step(int count = 1) =>
            StepGameplayUpdates(count, Vector2.Zero, [], [], batched: true);

        // enemyData.s uses the same direct extra-data row $0e for both subids
        // of $22/$49; subid $01 only changes the sword chase cooldown.
        foreach (int id in new[] { 0x22, 0x49 })
        {
            var definition = database.ImportedEnemy(id, 1);
            FailIf(definition is not { Health: 4, DamageQuarters: 2, RadiusX: 6, RadiusY: 6,
                TileBase: 0, Palette: 0, Animations.Length: 4 } ||
                definition.Sprites.Single() != "spr_shroudedstalfos_tile_candle",
                $"Skull Dungeon ${id:x2}:$01 lost source extra-data $0e or its OAM graphics.");
        }

        _saveData.SetRoomFlag(4, 0x8f, 0xff, false);
        LoadValidationRoom(4, 0x8f);
        var swords = _entities.Entities<SwordEnemyCharacter>().ToArray();
        var archer = _entities.Entities<ArrowMoblinCharacter>().Single();
        FailIf(swords.Length != 2 || swords.Any(enemy => enemy.Record is not { Id: 0x49, SubId: 1 }) ||
            !swords.Select(enemy => enemy.Position).SequenceEqual(new Vector2[] { new(0x88, 0x98), new(0xc8, 0x78) }) ||
            archer.Record is not { Id: 0x22, SubId: 1 } || archer.Position != new Vector2(0xc8, 0x28),
            "group4Map8fEnemyObjectData lost the two $49:$01 swords followed by the $22:$01 archer.");
        Step();
        FailIf(swords.Any(enemy => enemy.Counter2 != 0x10 || enemy.State != SwordEnemyState.Wandering) ||
            archer.State != ArrowMoblinState.Moving,
            "Room 4:8f did not dispatch its subid $01 enemies and source $10 sword cooldown.");
        Step(7);
        FailIf(swords.Any(enemy => enemy.Counter2 != 9),
            "Room 4:8f sword cooldown did not advance once per original update in a batched frame.");

        // Source state $09 stands for exactly eight updates; the subid $01
        // archer uses the same two RNG calls and alternate-shot gate as $00.
        var random = new OracleRandom();
        var prediction = new OracleRandom();
        var isolated = new ArrowMoblinCharacter();
        isolated.Initialize(database.ImportedEnemy(0x22, 1), _currentRoom, new Vector2(0xc8, 0x28), random);
        int initialScentCounter = prediction.Next().Value;
        int initialAngle = prediction.Next().Value & 0x18;
        int initialCounter = 0x30 + (prediction.Next().Value & 0x3f);
        isolated.UpdateFrame(Vector2.Zero);
        FailIf(isolated.Angle != initialAngle || isolated.Counter != initialCounter || random.Calls != 3 ||
            isolated.ScentAttractionCounter != initialScentCounter,
            "$22:$01 initialization lost enemyStandardUpdate's var3d roll before the direction and duration RNG.");
        for (int i = 0; i < 0x70 && isolated.State == ArrowMoblinState.Moving; i++) isolated.UpdateFrame(Vector2.Zero);
        FailIf(isolated.State != ArrowMoblinState.Turning || isolated.Counter != 8, "$22:$01 did not enter the eight-update stand.");
        for (int i = 0; i < 7; i++) isolated.UpdateFrame(Vector2.Zero);
        int nextAngle = prediction.Next().Value & 0x18;
        int nextCounter = 0x30 + (prediction.Next().Value & 0x3f);
        Vector2 direction = nextAngle switch { 0 => Vector2.Up, 8 => Vector2.Right, 16 => Vector2.Down, _ => Vector2.Left };
        int shot = isolated.UpdateFrame(isolated.Position + direction * 32);
        FailIf(shot != nextAngle || isolated.Counter != nextCounter || random.Calls != 5 || isolated.MoveCycles != 1,
            "$22:$01 did not fire its first alternating arrow at the end of the eight-update stand.");
        isolated.Free();
        LoadValidationRoom(4, 0x91);
        LoadValidationRoom(4, 0x8f);
        FailIf(_entities.Entities<SwordEnemyCharacter>().Count != 2 || _entities.Entities<ArrowMoblinCharacter>().Count != 1,
            "Room 4:8f lost its subid $01 enemies on re-entry.");
        GD.Print("Validated Skull Dungeon 4:8f shrouded Stalfos source placements, graphics, archer RNG/shot, sword cooldown, batched updates and re-entry.");
    }
}
