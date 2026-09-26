using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSmogFullEnemySplit()
    {
        foreach (bool batch in new[] { false, true })
        for (int repeat = 0; repeat < 2; repeat++)
        {
            LoadValidationRoom(0, 0x60);
            _entities.Clear();
            _player.WarpTo(new(24, 24));
            var intro = _entities.Spawn<SmogCharacter>(new SmogEnemySpawn(new(72, 72), 0));
            // Isolate the terminal intro update without navigating its text.
            intro.UpdateIntro(false, _ => { }, (_, _) => { }, _ => { }, () => { }, () => 0);
            typeof(SmogCharacter).GetProperty("Counter2", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(intro, 1);
            var db = new EnemyDatabase();
            for (int slotIndex = 0; slotIndex < 15; slotIndex++)
                _entities.TryAllocateEnemy(slot =>
                {
                    var filler = new SmasherCharacter();
                    filler.InitializePending(db.ImportedEnemy(EnemyId.Smasher, 0), _currentRoom,
                        new(120, 104), new OracleRandom(), slot);
                    return new SmasherSlotValidationEntity(filler, (_, _) => { });
                });
            var writes = new List<(int, byte)>();
            void Observe(int channel, byte value)
            {
                FailIf(_sound.Channel(channel).Volume != value,
                    "Failed Smog allocation must write the authoritative sound-driver byte before observers run.");
                writes.Add((channel, value));
            }
            _entities.NativeChannelVolumeWritten += Observe;
            var text = _entities.TextActiveSource;
            try
            {
                _entities.TextActiveSource = () => true;
                StepGameplayUpdates(2, Vector2.Zero, batched: batch);
                FailIf(intro.Counter2 != 1 || writes.Count != 0, "Frozen intro cannot perform unchecked allocation writes.");
                _entities.TextActiveSource = text;
                StepGameplayUpdates(1, Vector2.Zero);
                FailIf(!intro.IsDead || _entities.Entities<SmogCharacter>().Count != 0 ||
                    _entities.Entities<SmasherCharacter>().Count != 15 || _entities.RoomEnemyCount != 0 ||
                    !writes.SequenceEqual(new (int, byte)[] { (3,0x7c),(4,1),(3,0x7c),(4,1) }) ||
                    _runtimeState.ReadWramByte(0xc08b) != 72 || _runtimeState.ReadWramByte(0xc08d) != 88 ||
                    _runtimeState.ReadWramByte(0xc08f) != 0,
                    "Full ENEMY pool must preserve15 fillers, complete intro deletion, and perform both ordered failed-child writes.");
                StepGameplayUpdates(2, Vector2.Zero, batched: batch);
                FailIf(writes.Count != 4 || _entities.Entities<SmogCharacter>().Count != 0,
                    "Deleted intro must not retry failed children when its own slot becomes free.");
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
