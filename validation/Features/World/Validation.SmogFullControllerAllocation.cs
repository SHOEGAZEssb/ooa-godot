using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSmogFullControllerAllocation()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var db = new EnemyDatabase();
        void Fill(int count)
        {
            for (int i = 0; i < count; i++)
                _entities.TryAllocateEnemy(slot =>
                {
                    var filler = new SmasherCharacter();
                    filler.InitializePending(db.ImportedEnemy(0x74, 0), _currentRoom,
                        new(120, 104), new OracleRandom(), slot);
                    return new SmasherSlotValidationEntity(filler, (_, _) => { });
                });
        }
        foreach (bool batch in new[] { false, true })
        {
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batch);
            var writes = new List<(int, byte)>();
            void Observe(int channel, byte value)
            {
                FailIf(_sound.Channel(channel).Volume != value, "Controller alias must reach sound-driver memory.");
                writes.Add((channel, value));
            }
            _entities.NativeChannelVolumeWritten += Observe;
            var text = _entities.TextActiveSource;
            try
            {
                _saveData.SetRoomFlag(4, 0xbf, 0x80, false);
                _saveData.SetRoomFlag(4, 0xbf, 0x40, false);
                LoadValidationRoom(4, 0xbf);
                _player.WarpTo(new(120, 136));
                Step();
                Fill(15);
                var controller = _entities.EntityAdapters<SmogEncounterRoomEntity>().Single().Controller;
                ((BossShutterSignal)typeof(RoomEntityManager).GetField("_bossShutterSignal", flags)!.GetValue(_entities)!).Clear();
                _runtimeState.SetWramByte(0xc087, 0x66);
                _runtimeState.SetWramByte(0xc08f, 0x66);
                Step();
                FailIf(controller.State != 1 || controller.SpawnIndex != 0 || _entities.RoomEnemyCount != 1 ||
                    !writes.SequenceEqual(new (int, byte)[] { (3,0x7c),(5,0),(6,0) }) ||
                    _runtimeState.ReadWramByte(0xc08b) != 88 || _runtimeState.ReadWramByte(0xc08d) != 120 ||
                    _runtimeState.ReadWramByte(0xc088) != 0 || _runtimeState.ReadWramByte(0xc087) != 0x66 ||
                    _runtimeState.ReadWramByte(0xc08f) != 0x66,
                    "Failed intro allocation must advance index and copy the first source row without touching counter2/Z or enemy count.");
                writes.Clear();
                typeof(SmogEncounterController).GetProperty("State", flags)!.SetValue(controller, 7);
                typeof(SmogEncounterController).GetField("_counter", flags)!.SetValue(controller, 1);
                Step();
                FailIf(controller.State != 8 || controller.SpawnIndex != 2 || _entities.RoomEnemyCount != 1 ||
                    !writes.SequenceEqual(new (int, byte)[] { (3,0x7c),(5,2),(6,0),(3,0x7c),(5,2),(6,0) }) ||
                    _runtimeState.ReadWramByte(0xc08b) != 0x88 || _runtimeState.ReadWramByte(0xc08d) != 0x88 ||
                    _runtimeState.ReadWramByte(0xc088) != 3,
                    "Phase0 must consume both source rows even when both allocations fail.");

                LoadValidationRoom(0, 0x60); _entities.Clear(); _player.WarpTo(new(8,8));
                var first = _entities.Spawn<SmogCharacter>(new SmogEnemySpawn(new(72,72),2,2,1));
                var second = _entities.Spawn<SmogCharacter>(new SmogEnemySpawn(new(74,74),0x82,2,3));
                Step(); Fill(14); writes.Clear();
                _runtimeState.SetWramByte(0xc08f, 0x66);
                FailIf(!_entities.TryMergeSmogClouds(2) || first.SubId != 6 || second.SubId != 6 ||
                    first.IsDead || second.IsDead || _entities.RoomEnemyCount != 2 ||
                    !writes.SequenceEqual(new (int, byte)[] { (3,0x7c),(5,0x83),(6,2) }) ||
                    _runtimeState.ReadWramByte(0xc087) != 5 || _runtimeState.ReadWramByte(0xc088) != 1 ||
                    _runtimeState.ReadWramByte(0xc08b) != 72 || _runtimeState.ReadWramByte(0xc08d) != 72 ||
                    _runtimeState.ReadWramByte(0xc08f) != 0x66,
                    "Failed merge must mark both originals and write merged fields without allocating or incrementing count.");
                _entities.TextActiveSource = () => true; Step(2);
                FailIf(first.IsDead || second.IsDead, "Frozen originals must retain their pending merged-deletion states.");
                _entities.TextActiveSource = text; Step(2);
                FailIf(_entities.Entities<SmogCharacter>().Count != 0 || _entities.RoomEnemyCount != 0 ||
                    _entities.Entities<SmasherCharacter>().Count != 14 || writes.Count != 3,
                    "Both originals must delete on eligible enemy updates, without retrying a replacement.");
            }
            finally
            {
                _entities.TextActiveSource = text;
                _entities.NativeChannelVolumeWritten -= Observe;
            }
        }
        LoadValidationRoom(0, 0x60);
    }
}
