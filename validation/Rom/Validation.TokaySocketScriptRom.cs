using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateTokaySocketScriptRom()
    {
        foreach (bool batch in new[] { false, true })
        foreach (int free in new[] { 0, 1 })
        foreach (bool pause in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetRoomFlag(1, 0xba, OracleSaveData.RoomFlag80, false);
            _inventory.GiveTreasure(TreasureId.TokayEyeball, 0); LoadValidationRoom(1, 0xba);
            _inventory.SetScriptedEquippedItems(0, 0);
            var parent = _entities.Entities<TokayEyeballSlotRoomEntity>().Single();
            var eye = _entities.Entities<TokayEntranceEyeRoomEntity>().Single();
            Vector2 socketPosition = parent.Position;
            _player.WarpTo(parent.Position + Vector2.Down * 24); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position), "Socket approach must start on original floor.");
            StepGameplayUpdates(12, Vector2.Up, batched: batch);
            FailIf(_collision.Collides(_player.Position) || parent.State != TokayEyeballSlotState.Waiting,
                "Socket A input must be reachable through original collision geometry.");
            var seed = _random.CaptureState();
            var native = new SomariaRom(_saveData, seed, _currentRoom, 0, (int)_player.Position.X, (int)_player.Position.Y);
            int at = 0xd040 + _entities.InteractionSlot(parent) * 256;
            int eyeAt = 0xd040 + _entities.InteractionSlot(eye) * 256;
            native[eyeAt] = 1; native[eyeAt + 1] = 0x80; native[eyeAt + 2] = 5;
            // Accepted insertion prefix: original push/A selection, initial
            // stop-music command and native room parser are not replayed here.
            native[at] = 1; native[at + 1] = 0xc4; native[at + 2] = 4; native[at + 4] = 4;
            native[at + 11] = (byte)parent.Position.Y; native[at + 13] = (byte)parent.Position.X;
            native.Word(at + 0x18, 0x7ddd); native[0xcc8a] = 0x81; native[0xcc02] = 0x80;
            FillInteractionAdmissionPool(native, free);
            var audit = _sound.AttachPlayRequestAudit();
            int age = -1, held = 0; bool text = false;
            var previousText = _entities.TextActiveSource;
            _entities.TextActiveSource = () => text;
            int[] relevant = [0x6c, 0xb0, 0x4d, 0x70];
            void Compare()
            {
                if (!text) age++; else held++;
                native.AdvanceInteractions(_entities.FrameCounter); native.AdvanceScreenShake();
                int[] source = NativeAdmissionChildren(native, 0x80, 6);
                var inserted = _entities.Entities<TokayEntranceEyeRoomEntity>().Where(row => row.Record.SubId == 6).ToArray();
                FailIf(parent.Finished != (native[at] == 0) || parent.Counter != native[at + 6] ||
                    inserted.Length != source.Length || _saveData.GetRoomFlags(1, 0xba) != native[0xc8ba] ||
                    _runtimeState.ReadWramByte(WramAddress.wDisabledObjects) != native[0xcc8a] ||
                    _runtimeState.ReadWramByte(WramAddress.wMenuDisabled) != native[0xcc02],
                    $"Socket age{age}, free{free}, batch={batch}: counter={parent.Counter}/{native[at + 6]}, children={inserted.Length}/{source.Length}, masks=${_runtimeState.ReadWramByte(WramAddress.wDisabledObjects):x2}/${native[0xcc8a]:x2}.");
                FailIf(!audit.Requests.Where(relevant.Contains).SequenceEqual(native.Sounds.Where(relevant.Contains)),
                    $"Socket source cue order/boundary differs at age{age}.");
                FailIf(_entities.ScreenShakeCounter != native[0xcd18] || _entities.HorizontalScreenShakeCounter != native[0xcd19],
                    "shakescreen must use the source horizontal counter only.");
                FailIf(RandomDiffers(seed, native[0xff94], native[0xff95], native.RandomCalls),
                    $"Socket horizontal shake must preserve source RNG at age{age}.");
                foreach (int packed in new[] { 0x54, 0x55, 0x56 })
                    FailIf(_currentRoom.GetPackedStorageMetatile((byte)packed) != native[0xcf00 + packed],
                        $"Socket entrance tile ${packed:x2} differs at age{age}.");
                if (age == 0) FailIf(inserted.Length != 0 || native.Word(at + 0x18) != 0x7ddf || audit.Requests.Contains(0x6c),
                    "Room flag $80 must yield before eye allocation and SND_OPENCHEST.");
                if (age is >= 1 and <= 3)
                {
                    int pointer = age == 1 ? 0x7de4 : age == 2 ? 0x7de6 : 0x7de7;
                    FailIf(source.Length != free || native.Word(at + 0x18) != pointer || parent.Counter != (age == 3 ? 60 : 0),
                        "Checked spawn, cue and wait installation must retain three separate source dispatches.");
                }
            }
            try
            {
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batch, Compare);
                StepGameplayUpdates(10, Vector2.Zero, batched: batch, afterUpdate: Compare);
                if (pause)
                {
                    int before = parent.Counter;
                    text = true; native[0xcba0] = 1;
                    StepGameplayUpdates(2, Vector2.Zero, batched: batch, afterUpdate: Compare);
                    FailIf(parent.Counter != before, "Text must retain the active socket wait.");
                    text = false; native[0xcba0] = 0;
                }
                while (!parent.Finished && age < 260)
                {
                    if (pause && age == 247)
                    {
                        // Declared competing writes isolate enableinput's
                        // unconditional clear; no natural overlap is claimed.
                        _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 1); native[0xcc8a] = 1;
                        _runtimeState.SetWramByte(WramAddress.wMenuDisabled, 0x40); native[0xcc02] = 0x40;
                    }
                    StepGameplayUpdates(1, Vector2.Zero, batched: batch, afterUpdate: Compare);
                }
                FailIf(age != 248 || held != (pause ? 2 : 0) || !parent.Finished || native[at] != 0 ||
                    _inventory.HasTreasure(TreasureId.TokayEyeball) || _entities.PlayerMovementDisabled,
                    "Source insertion must finish exactly248 eligible updates after flag $80, consume the eyeball and release control.");
                StepGameplayUpdates(2, Vector2.Zero, batched: batch);
                LoadValidationRoom(1, 0xba); StepGameplayUpdates(1, Vector2.Zero, batched: batch);
                FailIf(_entities.Entities<TokayEyeballSlotRoomEntity>().Count != 0 || _entities.Entities<TokayEntranceEyeRoomEntity>().Count != 2,
                    "Completed insertion must reload with two placed eyes and no replayable socket.");
                _player.WarpTo(socketPosition + Vector2.Down * 24); _player.Face(Vector2I.Up);
                FailIf(_collision.Collides(_player.Position), "Completed socket re-entry must approach through original floor.");
                StepGameplayUpdates(12, Vector2.Up, batched: batch);
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batch);
                FailIf(_entities.Entities<TokayEyeballSlotRoomEntity>().Count != 0 || _entities.Entities<TokayEntranceEyeRoomEntity>().Count != 2 ||
                    _entities.PlayerMovementDisabled || _entities.PlayerMenusDisabled,
                    "Repeating A at the completed socket must not replay its program or reacquire input.");
            }
            finally { _entities.TextActiveSource = previousText; }
        }
        foreach (bool batch in new[] { false, true })
        foreach (int cancelAt in new[] { 0, 3, 64, 186, 247 })
        {
            ReinitializeGameplayForValidation(); _saveData.SetRoomFlag(1, 0xba, OracleSaveData.RoomFlag80, false);
            _inventory.GiveTreasure(TreasureId.TokayEyeball, 0); LoadValidationRoom(1, 0xba);
            _inventory.SetScriptedEquippedItems(0, 0);
            var parent = _entities.Entities<TokayEyeballSlotRoomEntity>().Single();
            _player.WarpTo(parent.Position + Vector2.Down * 24); _player.Face(Vector2I.Up);
            StepGameplayUpdates(12, Vector2.Up, batched: batch);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batch);
            StepGameplayUpdates(cancelAt, Vector2.Zero, batched: batch);
            LoadValidationRoom(4, 0xa1); StepGameplayUpdates(1, Vector2.Zero, batched: batch);
            FailIf(_entities.PlayerMovementDisabled || _runtimeState.ReadWramByte(WramAddress.wDisabledObjects) != 0 ||
                !_inventory.HasTreasure(TreasureId.TokayEyeball) || !_saveData.HasRoomFlag(1, 0xba, OracleSaveData.RoomFlag80),
                "Cancellation must release input, retain the committed flag and avoid premature item consumption.");
            LoadValidationRoom(1, 0xba); StepGameplayUpdates(1, Vector2.Zero, batched: batch);
            FailIf(_entities.Entities<TokayEyeballSlotRoomEntity>().Count != 0, "A committed insertion must not restart after cancellation/re-entry.");
        }
        ReinitializeGameplayForValidation();
    }
}
