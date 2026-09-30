using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateTileBreakGameplayRom()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (bool batched in new[] { false, true })
        for (int full = 0; full < 4; full++)
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(0, 0x33); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.Sword, 1);
            _inventory.EquipA(TreasureId.Sword); _inventory.EquipB(0);
            for (int y = 8; y < 128; y += 16)
            for (int x = 8; x < 160; x += 16)
                _currentRoom.SetPositionTileAndCollision(new(x, y), 0x3a, 0, 0);
            Vector2 point = new(72, 72);
            _currentRoom.SetPositionTileAndCollision(point, 0xc5, 0x0f, 0);
            _currentRoom.SetUnderlyingMetatile(point, 0xc5);
            _player.WarpTo(new(72, 86)); _player.Face(Vector2I.Up);
            FailIf(_currentRoom.IsSolid(_player.Position), "Sword fixture must start outside the bush collision.");
            var rom = new TileBreakRom();
            var replay = new OracleRandom();
            int seed;
            // Find a native rupee result so the drop cannot consume fairy RNG
            // or transform into an enemy during the subsequent gameplay updates.
            for (seed = 0; seed < 0x10000; seed++)
            {
                replay.RestoreState(replay.CaptureState() with { Rng1 = (byte)seed, Rng2 = (byte)(seed >> 8) });
                replay.Next(); // Sword slash sound is chosen before the tile probe.
                var state = replay.CaptureState();
                rom.Reset(_saveData, state.Rng1 | state.Rng2 << 8);
                rom.SetTile(0, 0xc5, 0xc5, _currentRoom); rom.Break(1);
                if (rom[0xd0c0] != 0 && rom[0xd0c2] is 2 or 3) break;
            }
            FailIf(seed == 0x10000, "ROM fixture did not find a rupee drop seed.");
            _random.RestoreState(_random.CaptureState() with { Rng1 = (byte)seed, Rng2 = (byte)(seed >> 8) });
            long before = _random.CaptureState().Calls;
            replay.RestoreState(_random.CaptureState()); replay.Next();
            var afterSlash = replay.CaptureState();
            rom.Reset(_saveData, afterSlash.Rng1 | afterSlash.Rng2 << 8);
            rom.SetTile(0, 0xc5, 0xc5, _currentRoom);
            var reservations = new List<(Dictionary<IRoomEntity, int> Slots, IRoomEntity Entity)>();
            try
            {
                for (int pool = 0; pool < 2; pool++)
                {
                    if ((full & (1 << pool)) == 0) continue;
                    var slots = (Dictionary<IRoomEntity, int>)typeof(RoomEntityManager)
                        .GetField(pool == 0 ? "_partSlots" : "_interactionSlots", flags)!.GetValue(_entities)!;
                    for (int slot = 0; slot < 16; slot++)
                    {
                        var reservation = new EnemyAiSlotReservation();
                        slots.Add(reservation, slot); reservations.Add((slots, reservation));
                        rom[0xd000 + slot * 0x100 + (pool == 0 ? 0xc0 : 0x40)] = 1;
                        rom[0xd001 + slot * 0x100 + (pool == 0 ? 0xc0 : 0x40)] = 0xff;
                    }
                }
                FailIf(!rom.Break(1), "Native sword must break bush $c5.");
                bool compared = false;
                void Compare()
                {
                    if (compared || _currentRoom.GetMetatile(point) == 0xc5) return;
                    compared = true;
                    var state = _random.CaptureState();
                    FailIf(_currentRoom.GetMetatile(point) != rom[0xcf44] || _currentRoom.GetUnderlyingMetatile(point) != rom.Underlying ||
                        _currentRoom.GetTerrainInfo(point).Collision != rom[0xce44] || state.Rng1 != rom[0xff94] || state.Rng2 != rom[0xff95] ||
                        state.Calls - before != 1 + rom.RandomCalls,
                        $"Sword tile/RNG handoff differs from ROM (pools={full}, batched={batched}).");
                    var drops = _entities.Entities<ItemDropEffect>();
                    int nativeDrops = Enumerable.Range(0, 16).Count(slot => rom[0xd0c0 + slot * 0x100] != 0 && rom[0xd0c1 + slot * 0x100] == 1);
                    FailIf(drops.Count != nativeDrops || drops.Any(drop => drop.SubId != rom[0xd0c2] || drop.Position != point),
                        $"Sword drop allocation/content differs from the ROM: pools={full}, native=${rom[0xd0c2]:x2}, runtime=[{string.Join(',', drops.Select(drop => drop.SubId))}].");
                    int nativeDebris = Enumerable.Range(2, 14).Count(slot => rom[0xd040 + slot * 0x100] != 0 && rom[0xd041 + slot * 0x100] == 0);
                    FailIf(_entities.Entities<GrassDebrisEffect>().Count != nativeDebris,
                        "Sword debris allocation differs from the ROM.");
                }
                StepGameplayUpdates(24, Vector2.Zero, ["attack"], ["attack"], batched, Compare);
                FailIf(!compared, "Actual sword input never broke the bush.");
                StepGameplayUpdates(24, Vector2.Zero, batched: batched);
                long after = _random.CaptureState().Calls;
                StepGameplayUpdates(24, Vector2.Zero, ["attack"], ["attack"], batched);
                FailIf(_random.CaptureState().Calls != after + 1,
                    "Repeated sword input on the replacement floor must consume only its slash RNG.");
            }
            finally
            {
                foreach (var (slots, entity) in reservations) { slots.Remove(entity); entity.Node.Free(); }
            }
            int dropsBefore = _entities.Entities<ItemDropEffect>().Count;
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(_entities.Entities<ItemDropEffect>().Count > dropsBefore, "A freed pool must not replay a failed tile drop.");
        }
        GD.Print("Validated actual sword tile/drop handoff, full PART/INTERAC pools, repeated hits and individual/batched gameplay updates against the ROM.");
    }
}
