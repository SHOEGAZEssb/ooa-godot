using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareOptionalPartAdmissionRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int free in new[] { 0, 1 })
        {
            // Native object opcode8 is checked even when its owning event
            // retires immediately because ROOMFLAG_ITEM is already set.
            foreach (bool itemFlag in new[] { false, true })
            {
                ReinitializeGameplayForValidation(); _saveData.SetRoomFlag(4, 0x56, OracleSaveData.RoomFlagItem, itemFlag);
                LoadValidationRoom(4, 0x56); _inventory.SetScriptedEquippedItems(0, 0); _player.WarpTo(new(88, 136));
                var parent = _entities.Entities<MoonlitGrottoArmosEventRoomEntity>().Single(); F1Retain(parent);
                var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 88, 136);
                native[0xd240] = 1; native[0xd241] = 0x21; native[0xd242] = 0x0a;
                native[0xcc8a] = 0x0c; _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0x0c);
                F1Fill(free, native);
                void Compare()
                {
                    native.AdvanceParts(_entities.FrameCounter); native.AdvanceInteractions(_entities.FrameCounter);
                    FailIf(_entities.Entities<DungeonOrbRoomEntity>().Count != F1Count(native, 3),
                        "Moonlit event initialization must skip the PART$03:$04 orb when getFreePartSlot fails.");
                }
                StepGameplayUpdates(2, Vector2.Zero, batched: batched, afterUpdate: Compare);
                FailIf(_entities.Entities<DungeonOrbRoomEntity>().Count != free || !parent.Initialized || parent.Finished != itemFlag,
                    "Moonlit checked orb failure must preserve event initialization and the independent room-item gate.");
                if (free == 0)
                {
                    _entities.RetireEntity(_entities.ResolvePartSlot(0)!);
                    for (int i = 0; i < 64; i++) native[0xd0c0 + i] = 0;
                    StepGameplayUpdates(2, Vector2.Zero, batched: batched, afterUpdate: Compare);
                    FailIf(_entities.Entities<DungeonOrbRoomEntity>().Count != 0, "Released capacity must not replay opcode8 after initialization.");
                }
                _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0); _entities.Clear();
            }

            ReinitializeGameplayForValidation(); LoadValidationRoom(4, 0x4d);
            _inventory.SetScriptedEquippedItems(0, 0); _player.WarpTo(new(16, 112)); _player.BeginCutsceneControl();
            var subOwner = _entities.EntityAdapters<IRoomEntity>().Single(actor => actor.Node is SubterrorBoss);
            var sub = (SubterrorBoss)subOwner.Node; F1Retain(subOwner); sub.Position = new(80, 72);
            F1Set(sub, "_initialized", true); F1Set(sub, "_dirtEnabled", true); F1Set(sub, "_dirtCounter", 1); F1Set(sub, "_counter2", 30);
            var dirtNative = F1Native(0x72, 0, 8, sub.Position); dirtNative[0xd0b0] = 7; dirtNative[0xd0b1] = 1; dirtNative[0xd087] = 30;
            F1Fill(free, dirtNative); StepGameplayUpdates(1, Vector2.Zero, batched: batched); F1Enemy(dirtNative); F1Birth(dirtNative);
            FailIf(_entities.Entities<SubterrorDirtEffect>().Count != free || F1Count(dirtNative, 0x32) != free ||
                SomariaPrivate<int>(sub, "_dirtCounter") != dirtNative[0xd0b1] || _currentRoom.GetMetatile(sub.Position) != 0xef || dirtNative[0xcf45] != 0xef,
                "Subterror dirt rejection must retain var31=$07 and the unconditional source tile$ef write.");
            _entities.Clear();

            ReinitializeGameplayForValidation(); LoadValidationRoom(0, 0x60); _entities.Clear();
            _inventory.SetScriptedEquippedItems(0, 0); _player.WarpTo(new(24, 24)); _player.BeginCutsceneControl();
            Vector2 point = new(88, 72); byte initial = _currentRoom.GetMetatile(point);
            var producer = new ItemDropProducer(); producer.Initialize(ItemDropDatabase.OneRupee, point, _currentRoom, _inventory, _saveData, _random);
            var owner = new ItemDropProducerRoomEntity(producer, 0); _entities.RegisterEnemySlot(owner, 0); _entities.AddEntity(owner);
            var dropNative = F1Native(0x59, ItemDropDatabase.OneRupee, 0, point); F1Fill(free, dropNative);
            void CompareDrop()
            {
                if (dropNative[0xd080] != 0) F1Enemy(dropNative);
                FailIf(producer.Finished != (dropNative[0xd080] == 0) || _entities.Entities<ItemDropEffect>().Count != F1Count(dropNative, 1),
                    $"ENEMY$59 must retry changed-tile PART allocation: free={free}, runtime={producer.Finished}/{_entities.Entities<ItemDropEffect>().Count}, native={dropNative[0xd080]:x2}/{F1Count(dropNative, 1)}, state={dropNative[0xd084]}, tile={dropNative[0xcf45]:x2}, captured={dropNative[0xd0b0]:x2}.");
            }
            StepGameplayUpdates(1, Vector2.Zero, batched: batched, afterUpdate: CompareDrop);
            byte next = (byte)(initial ^ 1); _currentRoom.SetPositionTileAndCollision(point, next, 0, 0); dropNative.SetTile(0x45, next);
            StepGameplayUpdates(3, Vector2.Zero, batched: batched, afterUpdate: CompareDrop);
            if (free == 0)
            {
                FailIf(producer.Finished || producer.SpawnedDrop, "Failed producer admission must not publish a consumed-placement outcome.");
                _entities.RetireEntity(_entities.ResolvePartSlot(0)!);
                for (int i = 0; i < 64; i++) dropNative[0xd0c0 + i] = 0;
                StepGameplayUpdates(2, Vector2.Zero, batched: batched, afterUpdate: CompareDrop);
            }
            FailIf(!producer.Finished || !producer.SpawnedDrop, "A released slot must complete ENEMY$59 exactly once.");
            _entities.Clear();
        }
        CompareDarkRoomPartAdmissionRom();
        CompareGalleryPartAdmission();
    }

    private void CompareDarkRoomPartAdmissionRom()
    {
        foreach (int free in new[] { 0, 1, 2 })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(5, 0xed);
            _inventory.SetScriptedEquippedItems(0, 0); _player.WarpTo(new(24, 152)); _player.BeginCutsceneControl();
            var parent = _entities.Entities<DarkRoomHandlerRoomEntity>().Single(); F1Retain(parent);
            int slot = F1Parts[parent];
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 24, 152);
            int at = 0xd0c0 + slot * 256; native[at] = 1; native[at + 1] = 8; native[0xcd00] = 1;
            F1Fill(free, native);
            StepGameplayUpdates(1, Vector2.Zero, batched: free == 0);
            F1Birth(native);
            FailIf(_entities.Entities<LightableTorchRoomEntity>().Count != free || F1Count(native, 0x06) != free ||
                parent.State.TotalTorches != 2 || native[at + 6] != 2,
                $"Dark-room scanning counts all torches: free={free}, runtime={_entities.Entities<LightableTorchRoomEntity>().Count}/{parent.State.TotalTorches}, native={F1Count(native, 0x06)}/{native[at + 6]}, state={native[at + 4]}.");
            _entities.Clear();
        }
    }

    private void CompareGalleryPartAdmission()
    {
        foreach (int free in new[] { 0, 1 })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(0, 0x33); _entities.Clear();
            _inventory.SetScriptedEquippedItems(0, 0); _player.WarpTo(new(24, 24)); _player.BeginCutsceneControl();
            var session = new ShootingGallerySession();
            var controller = _entities.Spawn<ShootingGalleryGameController>(new ShootingGalleryGameControllerSpawn(session));
            F1Set(controller, "_state", 4); F1Set(controller, "_counter", 1);
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 24, 24); F1Fill(free, native);
            // shootingGallery_createBallHere calls the original checked allocator
            // before constructing PART$38. The controller advances independently.
            int before = F1Parts.Count; StepGameplayUpdates(1, Vector2.Zero, batched: free == 0);
            int sourceBefore = Enumerable.Range(0, 16).Count(slot => native[0xd0c0 + slot * 256] != 0);
            SomariaPrivate<FrontendRom>(native, "_rom").Call(0x3e8e, 0);
            int admitted = Enumerable.Range(0, 16).Count(slot => native[0xd0c0 + slot * 256] != 0) - sourceBefore;
            FailIf(F1Parts.Count != before + admitted || _entities.Entities<ShootingGalleryBall>().Count != admitted ||
                controller.State != 5 || session.Round != 1,
                "Gallery ball admission must fail without an unpooled child while preserving the source round/state continuation.");
            _entities.Clear();
        }
    }
}
