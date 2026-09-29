using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateEnemyAiGameplayRom()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (bool batched in new[] { false, true })
        for (int gate = 0; gate < 5; gate++)
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(0, 0x33);
            _player.WarpTo(new Vector2(8, 120), recordSafe: false);
            if (gate == 4) StepGameplayUpdates(1, Vector2.Zero);
            var rom = new EnemyAiRom();
            var slots = (Dictionary<IRoomEntity, int>)typeof(RoomEntityManager)
                .GetField("_enemySlots", flags)!.GetValue(_entities)!;
            OracleRandomState seed = _random.CaptureState();
            rom[0xff94] = seed.Rng1;
            rom[0xff95] = seed.Rng2;
            if (gate == 4)
            {
                // Destination parsing advances the shared RNG before state-zero
                // preload. Execute that buffer generation independently as well.
                var placement = new PlacementRom();
                placement[0xff94] = seed.Rng1;
                placement[0xff95] = seed.Rng2;
                placement.Generate();
                rom[0xff94] = placement[0xff94];
                rom[0xff95] = placement[0xff95];
                _entities.BeginScreenTransition(0, room: _rooms.CurrentRoom, incomingOffset: Vector2.Left * 160);
                rom[0xcd00] = 8;
            }
            foreach ((IRoomEntity entity, int slot) in slots)
            {
                if (gate == 4 && slot < 3) continue; // initialized outgoing objects are frozen
                int address = 0xd080 + slot * 0x100;
                rom[address] = (byte)(((slot + 1) << 4) | 1);
                rom[address + 1] = (byte)(entity.Node is RiverZoraCharacter ? 0x08 : 0x18);
                rom.Word(address + 0x0a, (int)(entity.Node.Position.Y * 256));
                rom.Word(address + 0x0c, (int)(entity.Node.Position.X * 256));
            }
            OracleRoomData room = _rooms.CurrentRoom;
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
            {
                Vector2 point = new(x * 16 + 8, y * 16 + 8);
                rom[0xcf00 + y * 16 + x] = room.GetMetatile(point);
                rom[0xce00 + y * 16 + x] = (byte)room.GetTerrainInfo(point).Collision;
            }
            rom[0xcc33] = (byte)room.ActiveCollisions;
            if (gate == 4) rom.Update(); // BeginScreenTransition already ran state-zero preload
            bool frozen = gate != 0;
            // Feed the manager's existing gate inputs, retaining the actual
            // application scheduler and all gameplay update phases.
            _entities.TextActiveSource = () => gate == 1 && frozen;
            _entities.NonInteractionObjectsDisabledSource = () => gate == 2 && frozen;
            _entities.PaletteFadeActiveSource = () => gate == 3 && frozen;
            var parts = (Dictionary<IRoomEntity, int>)typeof(RoomEntityManager)
                .GetField("_partSlots", flags)!.GetValue(_entities)!;
            var reservations = new List<EnemyAiSlotReservation>();
            if (gate == 0)
            {
                // Occupy all slots until the first fire request. Releasing them
                // immediately afterward must not replay the consumed signal.
                FailIf(parts.Count != 0, "Enemy AI PART-capacity fixture must start with an empty pool.");
                for (int slot = 0; slot < 16; slot++)
                {
                    var reservation = new EnemyAiSlotReservation();
                    reservations.Add(reservation);
                    parts.Add(reservation, slot);
                    rom[0xd0c0 + slot * 0x100] = 1;
                }
            }
            int update = 0, releasedAt = -1, failedShots = 0;
            try
            {
                void CompareUpdate()
                {
                    rom[0xcc00] = (byte)_entities.FrameCounter;
                    rom[0xcba0] = (byte)(gate == 1 && frozen ? 1 : 0);
                    rom[0xcc8a] = (byte)(gate == 2 && frozen ? 4 : 0);
                    rom[0xc4ab] = (byte)(gate == 3 && frozen ? 1 : 0);
                    rom[0xcd00] = (byte)(gate == 4 && frozen ? 8 : 0);
                    bool shotAttempt = Enumerable.Range(0, 2).Any(slot =>
                        rom[0xd084 + slot * 0x100] == 0x0b && rom[0xd0a1 + slot * 0x100] is > 0 and < 0xff);
                    rom.Update();
                    foreach ((IRoomEntity entity, int slot) in slots)
                    {
                        if (gate == 4 && slot < 3) continue;
                        int address = 0xd080 + slot * 0x100;
                        EnemyCharacter enemy = (EnemyCharacter)entity.Node;
                        int state = enemy is RiverZoraCharacter z ? z.State : ((BuzzBlobCharacter)enemy).State;
                        int counter = enemy is RiverZoraCharacter rz ? rz.Counter : ((BuzzBlobCharacter)enemy).Counter;
                        int animationCounter = (int)typeof(EnemyAnimationPlayer).GetField("_frameCounter", flags)!.GetValue(enemy.Animation)!;
                        FailIf(state != rom[address + 4] || counter != rom[address + 6] ||
                            enemy.Position != new Vector2(rom.Word(address + 0x0c) / 256.0f, rom.Word(address + 0x0a) / 256.0f) ||
                            enemy.AnimationParameter != rom[address + 0x21] || animationCounter != rom[address + 0x20],
                            $"Gameplay enemy slot ${slot:x2}, update={update}, gate={gate}, frozen={frozen}, batched={batched}: " +
                            $"ROM state/counter/animation=${rom[address + 4]:x2}/${rom[address + 6]:x2}/${rom[address + 0x20]:x2}, " +
                            $"runtime=${state:x2}/${counter:x2}/${animationCounter:x2}, position={enemy.Position}; ROM XY=${rom.Word(address + 0x0c):x4}/${rom.Word(address + 0x0a):x4}, angle=${rom[address + 9]:x2}.");
                    }
                    OracleRandomState actual = _random.CaptureState();
                    FailIf(actual.Rng1 != rom[0xff94] || actual.Rng2 != rom[0xff95] || actual.Calls - seed.Calls != rom.RandomCalls + (gate == 4 ? 256 : 0),
                        $"Gameplay enemy RNG mismatch at update={update}, gate={gate}, batched={batched}: ROM calls={rom.RandomCalls}, runtime={actual.Calls - seed.Calls}.");
                    if (gate == 0 && releasedAt < 0 && shotAttempt)
                    {
                        FailIf(_entities.Entities<ZoraFireProjectile>().Count != 0,
                            "Full PART pool must reject the Zora fire request.");
                        failedShots++;
                        foreach (var reservation in reservations) parts.Remove(reservation);
                        for (int slot = 0; slot < 16; slot++) rom[0xd0c0 + slot * 0x100] = 0;
                        releasedAt = update;
                    }
                    if (releasedAt >= 0 && update <= releasedAt + 12)
                    {
                        int expected = Enumerable.Range(0, 16).Count(slot => rom[0xd0c0 + slot * 0x100] != 0);
                        FailIf(_entities.Entities<ZoraFireProjectile>().Count != expected,
                            $"Zora fire allocation/retry mismatch after releasing capacity at update {releasedAt}, now {update}.");
                    }
                    update++;
                }
                // State zero is eligible under each freeze gate. Initialized
                // objects remain frozen, then resume on the first cleared update.
                StepGameplayUpdates(16, Vector2.Zero, batched: batched, afterUpdate: CompareUpdate);
                if (gate == 4) _entities.FinishScreenTransition();
                frozen = false;
                StepGameplayUpdates(40, Vector2.Zero, batched: batched, afterUpdate: CompareUpdate);
                frozen = gate is >= 1 and <= 3;
                StepGameplayUpdates(7, Vector2.Zero, batched: batched, afterUpdate: CompareUpdate);
                frozen = false;
                StepGameplayUpdates(260, Vector2.Zero, batched: batched, afterUpdate: CompareUpdate);
                FailIf(gate == 0 && failedShots != 1, "PART-capacity regression never reached its blocked shot.");
            }
            finally
            {
                foreach (var reservation in reservations) { parts.Remove(reservation); reservation.Node.Free(); }
            }
        }
        GD.Print("Validated ROM enemy slot/RNG order through individual/batched gameplay updates, state-zero and initialized text/object/palette gates, scrolling preload/freeze/resume and full-PART failed-shot consumption.");
    }
}
