using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateOctogonEncounterRom()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (bool batched in new[] { false,true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(5,0x38);
            _inventory.EquipA(0); _inventory.EquipB(0);
            var data = new MermaidDungeonDatabase();
            var record = data.GetRoomRecords(5,0x38).Single(record => record.Kind == DungeonObjectKind.OctogonInitializer);
            var initializer = _entities.Entities<OctogonEncounterInitializerRoomEntity>().Single();
            FailIf(record.Kind != DungeonObjectKind.OctogonInitializer || record.Id != 0x90 || record.SubId != 0x0f ||
                record.Order != 1 || record.Predicate != DungeonObjectCondition.Always || initializer.Finished,
                "Original mainData.s room$5:$38 must admit pending positionless INTERAC$90:$0f at source order$01 without a completion-flag gate.");
            // Keep the actual admitted initializer and room geometry. Other
            // placed enemies/parts are outside this bounded encounter-state walk.
            var active = SomariaPrivate<List<IRoomEntity>>(_entities,"_activeEntities");
            var free = typeof(RoomEntityManager).GetMethod("FreeEntity",flags)!;
            void KeepInitializer()
            {
                foreach (var entity in active.Where(entity => entity is not OctogonEncounterInitializerRoomEntity).ToArray())
                { active.Remove(entity); free.Invoke(_entities,[entity]); }
            }
            KeepInitializer();
            var rom = new EnemyStatusRom(_currentRoom,_saveData,_random.Calls);
            var seed = _random.CaptureState(); rom[0xff94] = seed.Rng1; rom[0xff95] = seed.Rng2;
            int nativeSlot = (0xd0+_entities.InteractionSlot(initializer))*256+0x40;
            rom[nativeSlot] = 1; rom[nativeSlot+1] = 0x90; rom[nativeSlot+2] = 0x0f;
            byte[] original = Enumerable.Range(0,32).Select(index => (byte)(0x40+index)).ToArray();
            void SeedScratch()
            {
                for (int index = 0; index < original.Length; index++)
                    _runtimeState.SetWramByte(0xcfc0+index,rom[0xcfc0+index] = original[index]);
            }
            void CompareScratch(string phase)
            {
                for (int index = 0; index < original.Length; index++)
                    FailIf(_runtimeState.ReadWramByte(0xcfc0+index) != rom[0xcfc0+index],
                        $"Octogon {phase} batch={batched}: shared byte${0xcfc0+index:x4} differs from executed INTERAC$90:$0f.");
            }
            void AssertInitialState()
            {
                // Independent original instruction expectations, including
                // the omitted eighth write. These do not read imported rows.
                byte[] expected = [0,0,0xff,0x28,0x28,0x78,0xff];
                for (int index = 0; index < expected.Length; index++)
                    FailIf(_runtimeState.ReadWramByte(0xcfd0+index) != expected[index],
                        $"miscPuzzles_subid0f must write${0xcfd0+index:x4}=${expected[index]:x2}.");
                FailIf(_runtimeState.ReadWramByte(0xcfd7) != original[0x17] || !initializer.Finished,
                    "Octogon initialization must preserve the underwater position-fix request and delete its native interaction.");
                for (int index = 0; index < original.Length; index++)
                    if (index is < 0x10 or > 0x16)
                        FailIf(_runtimeState.ReadWramByte(0xcfc0+index) != original[index],
                            $"Octogon initialization must preserve unrelated union byte${0xcfc0+index:x4}.");
            }
            SeedScratch(); CompareScratch("before initialization");
            _dialogue.ShowGameplayMessage("Pending Octogon initialization",120); rom[0xcba0] = 1;
            StepGameplayUpdates(3,Vector2.Zero,batched:batched,afterUpdate:() =>
            {
                rom.Update(_entities.FrameCounter,_player.Position); CompareScratch("during text");
                FailIf(rom[nativeSlot] != 0 || !initializer.Finished,
                    "State-zero Octogon initializer must dispatch and delete under text, once, through the actual object loop.");
            });
            AssertInitialState(); _dialogue.Close(); rom[0xcba0] = 0;
            StepGameplayUpdates(3,Vector2.Zero,batched:batched,afterUpdate:() =>
            { rom.Update(_entities.FrameCounter,_player.Position); CompareScratch("after deletion"); });

            // A declared completed underwater body has published these live
            // bytes. Room reload/clear operations must preserve the encounter;
            // no disk save or feature-owned copy participates in the handoff.
            byte[] encounter = [1,1,8,0x1e,0x73,0xa6,0x0a,1];
            for (int index = 0; index < encounter.Length; index++)
                _runtimeState.SetWramByte(0xcfd0+index,encounter[index]);
            foreach (int room in new[] { 0x2d,0x36,0x2d })
            {
                LoadValidationRoom(5,room);
                for (int index = 0; index < encounter.Length; index++)
                    FailIf(_runtimeState.ReadWramByte(0xcfd0+index) != encounter[index],
                        $"Reloading Octogon room$5:${room:x2} must preserve encounter byte${0xcfd0+index:x4}.");
            }

            LoadValidationRoom(5,0x38); KeepInitializer();
            initializer = _entities.Entities<OctogonEncounterInitializerRoomEntity>().Single();
            _entities.InitializedObjectsDisabledSource = () => true;
            rom = new EnemyStatusRom(_currentRoom,_saveData,_random.Calls);
            seed = _random.CaptureState(); rom[0xff94] = seed.Rng1; rom[0xff95] = seed.Rng2;
            nativeSlot = (0xd0+_entities.InteractionSlot(initializer))*256+0x40;
            rom[nativeSlot] = 1; rom[nativeSlot+1] = 0x90; rom[nativeSlot+2] = 0x0f; rom[0xcc8a] = 2;
            SeedScratch();
            StepGameplayUpdates(3,Vector2.Zero,batched:batched,afterUpdate:() =>
            { rom.Update(_entities.FrameCounter,_player.Position); CompareScratch("disabled initialized objects"); });
            AssertInitialState(); _entities.InitializedObjectsDisabledSource = () => false;

            // Source state zero also dispatches during scroll preload. Start
            // from the real room, retain its admitted pools, and compare the
            // native dispatch with wScrollMode$08 instead of event stepping.
            original[0] = 0; // parseObjectData clears $cfc0 before destination state-zero dispatch.
            SeedScratch();
            _entities.BeginScreenTransition(5,_currentRoom,Vector2.Left*_currentRoom.Width,_player);
            KeepInitializer();
            for (int address = 0xd040; address < 0xe000; address += 256) rom[address] = 0;
            rom[nativeSlot] = 1; rom[nativeSlot+1] = 0x90; rom[nativeSlot+2] = 0x0f; rom[0xcd00] = 8;
            // Destination preload also executes the earlier INTERAC$20:$01
            // state0 before this isolated initializer can be retained.
            int signalSlot=nativeSlot-0x100;
            rom[signalSlot]=1; rom[signalSlot+1]=0x20; rom[signalSlot+2]=1; rom[0xcc39]=0x0c;
            rom.Update(_entities.FrameCounter,_player.Position); CompareScratch("scroll preload");
            FailIf(_entities.Entities<OctogonEncounterInitializerRoomEntity>().Count != 0 || rom[nativeSlot] != 0,
                "Octogon state-zero initializer must finish before the destination is exposed during scrolling.");
            StepGameplayUpdates(4,Vector2.Zero,batched:batched,afterUpdate:() => CompareScratch("scroll freeze"));
            _entities.FinishScreenTransition(); CompareScratch("scroll completion");
        }
        GD.Print("Validated executed clean-US Octogon initialization under text, object disable and scroll preload, exact seven-byte writes/deletion, preserved $cfd7/unrelated union bytes, repeated entry, and shared-state survival across $5:$2d/$36 reloads in split/batched gameplay. Boss combat and diving execution are not part of this fixture.");
    }
}
