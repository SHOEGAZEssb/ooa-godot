using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    // Solve the source RNG arithmetic independently for the next requested A.
    private static void SetDiggingRoll(OracleRandom random, int value)
    {
        OracleRandomState state = random.CaptureState();
        for (int high = 0; high < 256; high++)
        for (int low = 0; low < 256; low++)
        {
            int multiplied = (((high << 8) | low) * 3) & 0xffff;
            if (((low + (multiplied >> 8)) & 255) != value) continue;
            random.RestoreState(state with { Rng1 = (byte)low, Rng2 = (byte)high });
            return;
        }
        throw new InvalidOperationException($"No RNG state produces ${value:x2}.");
    }

    private void ValidateDiggingEnemies()
    {
        LoadValidationRoom(0, 0x98);
        _entities.Clear();
        _player.WarpTo(new Vector2(128, 112), recordSafe: false);
        OracleRoomData room = _rooms.CurrentRoom;
        Vector2 point = new(72, 56);
        var drops = new ItemDropDatabase();
        var digging = new DiggingEnemyDatabase();
        ItemDropDatabaseVisualRecord visual = drops.GetVisual(0x0f);

        // Use the real $3a ground row (drop table $89) through ShovelController.
        // Only the probability and item-set draws belong to the tile attempt.
        var searchRandom = new OracleRandom();
        OracleRandomState searchState = searchRandom.CaptureState();
        bool found = false;
        for (int seed = 0; seed < 65536 && !found; seed++)
        {
            searchRandom.RestoreState(searchState with { Rng1 = (byte)seed, Rng2 = (byte)(seed >> 8) });
            int multiplied = (seed * 3) & 0xffff;
            int first = ((seed & 255) + (multiplied >> 8)) & 255;
            int nextState = (multiplied & 0xff00) | first;
            int nextMultiplied = (nextState * 3) & 0xffff;
            int second = (first + (nextMultiplied >> 8)) & 255;
            if (drops.ChooseDrop(0x89, (byte)first, (byte)second, _inventory, _saveData) != 0x0f)
                continue;
            _random.RestoreState(searchRandom.CaptureState());
            found = true;
        }
        FailIf(!found, "No source RNG state selected PART_ITEM_DROP:$0f from breakable table $89.");
        room.SetPositionTileAndCollision(point, 0x3a, 0, 0);
        int shovelCalls = _random.Calls;
        FailIf(!_shovel.TryDig(point, Vector2I.Left), "Room 0:98 ground $3a rejected a shovel dig.");
        ItemDropEffect buried = _entities.Entities<ItemDropEffect>().Single();
        FailIf(buried.SubId != 0x0f || buried.ElapsedFrames != 0 ||
            _random.Calls != shovelCalls + 2 || _entities.RoomEnemyCount != 0,
            "Shovel tile transaction resolved the $0f part before its state-0 update.");
        _entities.Clear();

        // Allocation order differs from update order when an existing part is
        // followed by an enemy. That enemy must consume RNG before the part.
        var pendingDrop = _entities.Spawn<ItemDropEffect>(new ItemDropSpawn(0x0f, point, DugUp: true));
        var producerDrop = _entities.Spawn<ItemDropEffect>(new ItemDropSpawn(0x0f, point));
        SetDiggingRoll(_random, 3);
        producerDrop.UpdateFrame(_player, 0);
        BeetleCharacter chargedBeetle = _entities.Entities<BeetleCharacter>().Single();
        for (int update = 0; update < 31; update++)
            chargedBeetle.UpdateFrame(_player.Position, 0);
        FailIf(chargedBeetle.Counter != 1, "Beetle $51:$02 did not reach the last charge update.");
        var expectedRandom = new OracleRandom();
        expectedRandom.RestoreState(_random.CaptureState());
        OracleRandomResult wanderRoll = expectedRandom.Next();
        OracleRandomResult dropRoll = expectedRandom.Next();
        _entities.Update(1.0 / 60, _player);
        FailIf(_random.Calls != expectedRandom.Calls || _random.LastResult != dropRoll ||
            chargedBeetle.Counter != digging.BeetleCounters[wanderRoll.High & 7] ||
            chargedBeetle.Angle != (wanderRoll.Low & 0x1c) ||
            pendingDrop.Finished != (dropRoll.Value < 0xe0),
            "Enemy/part phase order depended on allocation order or consumed a second species roll.");

        // Every possible A proves the threshold and low-bit table without
        // allowing a second random draw to masquerade as species selection.
        _entities.Clear();
        for (int roll = 0; roll < 256; roll++)
        {
            var rng = new OracleRandom();
            SetDiggingRoll(rng, roll);
            int calls = rng.Calls;
            int spawned = 0;
            var drop = new ItemDropEffect();
            drop.Initialize(0x0f, point, room, visual, dugUp: true, random: rng,
                spawnEnemy: (value, subId, position) =>
                {
                    spawned++;
                    FailIf(value != roll || subId != 3 || position != point ||
                        digging.Enemy(value, subId).Id != ((roll & 7) < 3 ? 0x10 : 0x51),
                        $"itemDrop_spawnEnemy roll ${roll:x2} lost A, var03, position, or table order.");
                });
            FailIf(rng.Calls != calls, "PART_ITEM_DROP:$0f drew RNG during allocation.");
            drop.UpdateFrame(_player, 1);
            FailIf(rng.Calls != calls + 1 || drop.Finished != (roll < 0xe0) ||
                spawned != (roll < 0xe0 ? 1 : 0) || drop.Collected,
                $"PART_ITEM_DROP:$0f roll ${roll:x2} violated its state-0 branch.");
            drop.Free();
        }

        var mapleRandom = new OracleRandom();
        var mapleDrop = new ItemDropEffect();
        mapleDrop.Initialize(0x0f, point, room, visual, random: mapleRandom,
            maplePresent: () => true, spawnEnemy: (_, _, _) =>
                throw new InvalidOperationException("Maple allowed a digging spawn."));
        mapleDrop.UpdateFrame(_player, 0);
        FailIf(!mapleDrop.Finished || mapleRandom.Calls != 0,
            "partCode01@state0 did not delete before RNG while Maple was present.");
        mapleDrop.Free();

        ItemDropEffect Spawn(int roll, bool dug = true)
        {
            SetDiggingRoll(_random, roll);
            int calls = _random.Calls;
            ItemDropEffect drop = _entities.Spawn<ItemDropEffect>(new ItemDropSpawn(
                0x0f, point, DugUp: dug));
            FailIf(_random.Calls != calls, "Spawning PART_ITEM_DROP:$0f consumed RNG early.");
            _entities.Update(1.0 / 60, _player);
            return drop;
        }

        _runtimeState.SetWramByte(OracleRuntimeState.DiggingUpEnemiesForbiddenAddress, 0x80);
        int before = _random.Calls;
        ItemDropEffect forbidden = Spawn(0);
        FailIf(!forbidden.Finished || _random.Calls != before + 1 || _entities.RoomEnemyCount != 0,
            "$ccde restriction did not discard the enemy after consuming exactly one RNG call.");
        ItemDropEffect rich = Spawn(0xe0);
        FailIf(rich.Finished || rich.State != DropState.Bouncing,
            "$ccde incorrectly suppressed the 100-rupee branch.");
        _entities.Clear();
        _runtimeState.SetWramByte(OracleRuntimeState.DiggingUpEnemiesForbiddenAddress, 0);

        ItemDropEffect ropeDrop = Spawn(2);
        RopeCharacter rope = _entities.Entities<RopeCharacter>().Single();
        FailIf(!ropeDrop.Finished || rope.Record.SubId != 3 || rope.Visible ||
            rope.Position != point || _entities.RoomEnemyCount != 1,
            "PART_ITEM_DROP:$0f did not allocate a counted, uninitialized Rope $10:$03.");
        _entities.Update(1.0 / 60, _player);
        FailIf(rope.State != RopeState.SpawnSetup || !rope.Visible || rope.CollisionEnabled,
            "Rope $10:$03 state 0 did not stop at visible state 8 with collisions disabled.");
        _player.FaceShooterDirection(Vector2I.Left);
        _entities.Update(1.0 / 60, _player);
        FailIf(rope.State != RopeState.SpawnBounce || rope.SpeedZ != -0x102 ||
            rope.Angle != 0x18 || rope.SpeedRaw != 0x1e || rope.Position != point,
            "rope_subid03@state8 failed its live Link direction, $fefe speedZ, or SPEED_c0 setup.");
        ValidateDiggingBounce(rope, null);

        _entities.Clear();
        Spawn(3);
        BeetleCharacter beetle = _entities.Entities<BeetleCharacter>().Single();
        FailIf(beetle.State != 0 || beetle.Visible || beetle.Record.SubId != 3,
            "PART_ITEM_DROP:$0f ran Beetle $51:$03 in the already completed enemy pass.");
        _entities.Update(1.0 / 60, _player);
        FailIf(beetle.State != 8 || beetle.Visible, "Beetle state 0 must remain hidden in state 8.");
        _entities.Update(1.0 / 60, _player);
        FailIf(beetle.State != 9 || !beetle.Visible || beetle.SpeedZ != -0x102 || beetle.Angle != 0x18,
            "beetle_subid3@state8 failed its launch setup.");
        ValidateDiggingBounce(null, beetle);

        foreach (int roll in new[] { 0, 7 })
        {
            _entities.Clear();
            Spawn(roll, dug: false);
            _entities.Update(1.0 / 60, _player);
            _entities.Update(1.0 / 60, _player);
            EnemyCharacter enemy = _entities.Entities<EnemyCharacter>().Single();
            FailIf(enemy.CollisionEnabled, "$02 entry enabled collisions during setup.");
            for (int update = 1; update <= 8; update++)
            {
                _entities.Update(1.0 / 60, _player);
                FailIf(enemy.CollisionEnabled != (update == 8),
                    $"Enemy ${(roll == 0 ? 0x10 : 0x51):x2}:$02 collision boundary is not eight updates.");
            }
        }

        // Fill the shared enemy pool in a single part pass so no enemy has
        // initialized or consumed species RNG before the final allocation.
        _entities.Clear();
        for (int index = 0; index < 16; index++)
        {
            SetDiggingRoll(_random, 3);
            ItemDropEffect drop = _entities.Spawn<ItemDropEffect>(new ItemDropSpawn(0x0f, point, DugUp: true));
            drop.UpdateFrame(_player, 0);
        }
        FailIf(_entities.RoomEnemyCount != 16, "getFreeEnemySlot did not allocate all 16 counted slots.");
        SetDiggingRoll(_random, 0);
        ItemDropEffect exhausted = _entities.Spawn<ItemDropEffect>(new ItemDropSpawn(0x0f, point, DugUp: true));
        before = _random.Calls;
        exhausted.UpdateFrame(_player, 0);
        FailIf(!exhausted.Finished || _entities.RoomEnemyCount != 16 || _random.Calls != before + 1,
            "Full enemy pool did not discard PART_ITEM_DROP:$0f after the RNG call.");

        BeetleCharacter released = _entities.Entities<BeetleCharacter>()[0];
        released.Swallow();
        SetDiggingRoll(_random, 0);
        ItemDropEffect replacementDrop = _entities.Spawn<ItemDropEffect>(new ItemDropSpawn(0x0f, point, DugUp: true));
        _entities.Update(1.0 / 60, _player);
        FailIf(_entities.RoomEnemyCount != 16 || _entities.Entities<RopeCharacter>().Count != 1,
            "getFreeEnemySlot did not reuse the deleted first enemy slot.");

        RopeCharacter frozen = _entities.Entities<RopeCharacter>().Single();
        _entities.BeginScreenTransition(0, _world.LoadRoom(0, 0x99), Vector2.Left * 160);
        _entities.Update(1.0, _player);
        FailIf(frozen.State != RopeState.SpawnSetup || frozen.Visible,
            "Outgoing dug enemy initialized during scrolling.");
        _entities.FinishScreenTransition();

        LoadValidationRoom(0, 0x98);
        OracleRandomState batchRandom = _random.CaptureState();
        (int, int, int, int, Vector2, int, OracleRandomResult) RunBatch(bool batched)
        {
            _entities.Clear();
            _random.RestoreState(batchRandom);
            SetDiggingRoll(_random, 3);
            _entities.Spawn<ItemDropEffect>(new ItemDropSpawn(0x0f, point, DugUp: true));
            if (batched) _entities.Update(48.0 / 60, _player);
            else for (int index = 0; index < 48; index++) _entities.Update(1.0 / 60, _player);
            BeetleCharacter subject = _entities.Entities<BeetleCharacter>().Single();
            return (subject.State, subject.Counter, subject.ZFixed, subject.AnimationFrame,
                subject.Position, _random.Calls, _random.LastResult);
        }
        FailIf(RunBatch(false) != RunBatch(true),
            "Batched updates changed digging spawn phase, Beetle motion/animation, or shared RNG.");

        _runtimeState.SetWramByte(OracleRuntimeState.DiggingUpEnemiesForbiddenAddress, 1);
        LoadValidationRoom(0, 0x98);
        FailIf(_runtimeState.ReadWramByte(OracleRuntimeState.DiggingUpEnemiesForbiddenAddress) != 0,
            "Room entry retained the previous room's $ccde restriction.");
        GD.Print("Validated digging enemy selection for all 256 rolls, state-0 RNG, Maple/restriction gates, enemy capacity/counting, and Rope/Beetle $02/$03 entry timing.");
    }

    private void ValidateDiggingBounce(RopeCharacter? rope, BeetleCharacter? beetle)
    {
        int z = 0, speed = -0x102;
        bool collisions = false;
        int calls = _random.Calls;
        int landingSounds = _sound.PlayRequestsFor(OracleSoundEngine.SndBombLand);
        int impacts = 0;
        for (int update = 1; update < 100; update++)
        {
            z += speed;
            bool done = false;
            if (z < 0) speed += 14;
            else
            {
                z = 0;
                int next = (-speed) >> 1;
                done = next > -128 || next == 0;
                if (!done) { speed = next; impacts++; }
            }
            if (!done && (speed >> 8) == 0) collisions = true;
            _entities.Update(1.0 / 60, _player);
            int actualZ = rope?.ZFixed ?? beetle!.ZFixed;
            int actualSpeed = rope?.SpeedZ ?? beetle!.SpeedZ;
            bool actualCollision = rope?.CollisionEnabled ?? beetle!.CollisionEnabled;
            FailIf(actualZ != z || actualSpeed != speed || actualCollision != collisions ||
                _random.Calls != calls + (done ? 1 : 0),
                $"Dug enemy bounce update {update}: expected Z={z}, speedZ={speed}, collisions={collisions}; got {actualZ}/{actualSpeed}/{actualCollision}.");
            FailIf(_sound.PlayRequestsFor(OracleSoundEngine.SndBombLand) != landingSounds + impacts,
                $"Dug enemy bounce update {update} did not request SND_BOMB_LAND only on continuing bounces.");
            if (done) return;
        }
        throw new InvalidOperationException("Dug enemy failed to finish bouncing within 100 updates.");
    }
}
