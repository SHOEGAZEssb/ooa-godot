using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateVolcanoTokayInteractionAdmissionRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool restored in new[] { false, true })
        foreach (int free in new[] { 0, 1 })
        {
            ReinitializeGameplayForValidation(); _saveData.SetGlobalFlag(AgesGlobalFlag.TuniNutPlaced, restored); LoadValidationRoom(1, 3);
            _inventory.SetScriptedEquippedItems(0, 0); _player.BeginCutsceneControl();
            var parent = _entities.EntityAdapters<VolcanoRoomEntity>().Single(entity => entity.Node.Name.ToString().Contains("06_"));
            // Isolate capacity at the $dc:$06 producer boundary. The actual
            // room's earlier $dc:$13 deletion is exercised separately below.
            foreach (var entity in _entities.EntityAdapters<IRoomEntity>().Where(entity => !ReferenceEquals(entity, parent)).ToArray())
                _entities.RetireEntity(entity);
            int parentAt = 0xd040 + _entities.InteractionSlot(parent.Node) * 256;
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 80, 72);
            native[parentAt] = 1; native[parentAt + 1] = 0xdc; native[parentAt + 2] = 6;
            native[parentAt + 11] = 24; native[parentAt + 13] = 80;
            FillInteractionAdmissionPool(native, free);
            _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 4); native[0xcc8a] = 4;
            StepGameplayUpdates(1, Vector2.Zero, batched: batched); native.AdvanceInteractions(_entities.FrameCounter);
            var children = _entities.EntityAdapters<VolcanoRoomEntity>().Where(entity => entity.Node.Name.ToString().Contains("b2_")).ToArray();
            var source = NativeAdmissionChildren(native, 0xb2, 1);
            int expected = restored ? 0 : free;
            FailIf(!parent.Finished || native[parentAt] != 0 || children.Length != expected || source.Length != expected ||
                children.Any(child => child.State != 1 || _entities.InteractionSlot(child.Node) != (source.Single() >> 8) - 0xd0),
                "INTERAC$dc:$06 must attempt checked $b2:$01 before deleting its own slot, without retrying failure.");
            if (free == 0)
            {
                _entities.RetireEntity(InteractionAdmissionPool.First(pair => pair.Key.Node is PuzzlePuffEffect).Key);
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
                FailIf(_entities.EntityAdapters<VolcanoRoomEntity>().Any(entity => entity.Node.Name.ToString().Contains("b2_")),
                    "Releasing capacity must not resurrect a deleted Volcano producer.");
            }
            _entities.Clear(); FailIf(InteractionAdmissionPool.Count != 0, "Volcano cancellation must release native slots.");
        }
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); _saveData.SetGlobalFlag(AgesGlobalFlag.TuniNutPlaced, false); LoadValidationRoom(1, 3);
            _inventory.SetScriptedEquippedItems(0, 0); _player.BeginCutsceneControl();
            _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 4);
            var seed = _random.CaptureState(); var native = new SomariaRom(_saveData, seed, _currentRoom, 0, 80, 72);
            native[0xcc8a] = 4; native[0xd240] = 1; native[0xd241] = 0xdc; native[0xd242] = 0x13;
            native[0xd340] = 1; native[0xd341] = 0xdc; native[0xd342] = 6; native[0xd34b] = 24; native[0xd34d] = 80;
            int age = 0;
            StepGameplayUpdates(63, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                age++; native.AdvanceInteractions(_entities.FrameCounter); native.AdvanceScreenShake();
                var child = _entities.EntityAdapters<VolcanoRoomEntity>().Single(entity => entity.Node.Name.ToString().Contains("b2_"));
                int at = NativeAdmissionChildren(native, 0xb2, 1).Single();
                int state = age == 1 ? 0 : 1;
                FailIf(_entities.InteractionSlot(child.Node) != 2 || child.State != state || native[at + 4] != state ||
                    _entities.ScreenShakeCounter != native[0xcd18] || _entities.HorizontalScreenShakeCounter != native[0xcd19] ||
                    _random.Calls - seed.Calls != native.RandomCalls,
                    $"Room $1:$03 update={age}: earlier-slot $b2 birth, shake counters and shared RNG must match the original slot walk.");
                if (age is 3 or 33 or 63)
                {
                    int expectedY = age == 3 ? 0 : age == 33 ? 29 : 179;
                    int expectedX = age == 3 ? 29 : age == 33 ? 0 : 179;
                    FailIf(native[0xcd18] != expectedY || native[0xcd19] != expectedX,
                        $"Native Volcano script must begin its three source steps on updates 3, 33 and 63; age={age}.");
                }
            });
        }

        // Vine allocation/cancellation now runs against the complete native
        // script in ValidateTokayVineScriptRom.

        foreach (bool batched in new[] { false, true })
        foreach (int free in new[] { 0, 1 })
        {
            ReinitializeGameplayForValidation(); _saveData.SetRoomFlag(1, 0xba, OracleSaveData.RoomFlag80, false);
            _inventory.GiveTreasure(TreasureId.TokayEyeball, 0); LoadValidationRoom(1, 0xba); _inventory.SetScriptedEquippedItems(0, 0);
            var parent = _entities.Entities<TokayEyeballSlotRoomEntity>().Single();
            var eye = _entities.Entities<TokayEntranceEyeRoomEntity>().Single();
            int parentAt = 0xd040 + _entities.InteractionSlot(parent) * 256, eyeAt = 0xd040 + _entities.InteractionSlot(eye) * 256;
            _player.WarpTo(parent.Position + Vector2.Down * 24); _player.Face(Vector2I.Up);
            StepGameplayUpdates(12, Vector2.Up, batched: batched);
            FailIf(_collision.Collides(_player.Position) || parent.State != TokayEyeballSlotState.Waiting,
                "Tokay socket must be approached over the room's actual floor before A insertion.");
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, (int)_player.Position.X, (int)_player.Position.Y);
            native[eyeAt] = 1; native[eyeAt + 1] = 0x80; native[eyeAt + 2] = 5;
            native[parentAt] = 1; native[parentAt + 1] = 0xc4; native[parentAt + 2] = 4; native[parentAt + 4] = 4;
            native.Word(parentAt + 0x18, 0x7ddd); native[parentAt + 11] = (byte)parent.Position.Y; native[parentAt + 13] = (byte)parent.Position.X;
            FillInteractionAdmissionPool(native, free); _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 4); native[0xcc8a] = 0x81;
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            native.AdvanceInteractions(_entities.FrameCounter); // orroomflag yields.
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            native.AdvanceInteractions(_entities.FrameCounter); // Checked spawn also yields.
            var inserted = _entities.Entities<TokayEntranceEyeRoomEntity>().Where(actor => actor.Record.SubId == 6).ToArray();
            var source = NativeAdmissionChildren(native, 0x80, 6);
            FailIf(inserted.Length != free || source.Length != free || parent.State != TokayEyeballSlotState.EyeWait,
                "Tokay socket must consume capacity for both placed owners and skip rejected dynamic $80:$06 without aborting insertion.");
            if (free == 1) FailIf(_entities.InteractionSlot(inserted.Single()) != (source.Single() >> 8) - 0xd0,
                "Tokay dynamic eye must reserve the source physical interaction slot.");
            StepGameplayUpdates(247, Vector2.Zero, batched: batched);
            FailIf(!parent.Finished || _inventory.HasTreasure(TreasureId.TokayEyeball) || _entities.PlayerMovementDisabled,
                "Rejected eye allocation must not prevent socket completion, treasure consumption or control release.");
            LoadValidationRoom(1, 0xba); StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(_entities.Entities<TokayEyeballSlotRoomEntity>().Count != 0 || _entities.Entities<TokayEntranceEyeRoomEntity>().Count != 2 ||
                _entities.Entities<TokayEntranceEyeRoomEntity>().Any(actor => !InteractionAdmissionPool.Keys.Any(owner => ReferenceEquals(owner.Node, actor))),
                "Persistent Tokay reload must replace the used socket with two enrolled static eye decorations.");
            _entities.Clear(); FailIf(InteractionAdmissionPool.Count != 0, "Tokay cancellation must release all physical owners.");
        }
        ReinitializeGameplayForValidation();
        GD.Print("Compared Volcano and Tokay checked interaction admission, earlier-slot birth, reachable actions, script continuation, reload and cancellation.");
    }
}
