using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMoonlitInitializationGatesRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (string gate in new[] { "text", "mask02" })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4, 0x56);
            _inventory.SetScriptedEquippedItems(0, 0); _player.WarpTo(new(88, 136));
            FailIf(_collision.Collides(_player.Position), "Moonlit initialization observer must use the actual room floor.");
            var parent = _entities.Entities<MoonlitGrottoArmosEventRoomEntity>().Single();
            var priorText = _entities.TextActiveSource; bool held = true;
            _entities.TextActiveSource = () => held && gate == "text";
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 88, 136);
            native[0xd240] = 1; native[0xd241] = 0x21; native[0xd242] = 0x0a;
            native[0xcdd2] = 0x10; _runtimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress, 0x10);
            native[0xcc8a] = gate == "mask02" ? (byte)6 : (byte)4;
            native[0xcba0] = gate == "text" ? (byte)1 : (byte)0;
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects, native[0xcc8a]);
            try
            {
                StepGameplayUpdates(2, Vector2.Zero, batched: batched, afterUpdate: () =>
                {
                    native.AdvanceParts(_entities.FrameCounter); native.AdvanceInteractions(_entities.FrameCounter);
                    FailIf(!parent.Initialized || parent.Finished || native[0xd244] != 1 ||
                        _runtimeState.ReadWramByte(OracleRuntimeState.ToggleBlocksStateAddress) != 0 || native[0xcdd2] != 0 ||
                        _entities.Entities<DungeonOrbRoomEntity>().Count != 1,
                        "Moonlit $21:$0a must initialize and clear bit $10 once under text/$02, with an admitted orb.");
                });
                held = false; native[0xcba0] = 0; native[0xcc8a] = 4; _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 4);
                StepGameplayUpdates(1, Vector2.Zero, batched: batched); native.AdvanceParts(_entities.FrameCounter); native.AdvanceInteractions(_entities.FrameCounter);
                FailIf(!parent.Initialized || parent.Finished || !_entities.Entities<DungeonOrbRoomEntity>().Single().Visible,
                    "Releasing the gate must retain the original initialization instead of replaying its orb allocation.");
            }
            finally { _entities.TextActiveSource = priorText; _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0); }

            // Native subid $0c never increments state. Its real first pressure
            // visit must consume the PART publication in the same object pass.
            ReinitializeGameplayForValidation(); LoadValidationRoom(4, 0x5e);
            _inventory.SetScriptedEquippedItems(0, 0); _player.WarpTo(new(152, 32)); _player.Face(Vector2I.Up);
            parent = _entities.Entities<MoonlitGrottoArmosEventRoomEntity>().Single();
            native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 152, 32);
            native[0xd240] = 1; native[0xd241] = 0x21; native[0xd242] = 0x0c;
            native[0xd0c0] = 1; native[0xd0c1] = 9; native[0xd0cb] = 24; native[0xd0cd] = 152;
            native[0xcc8a] = 6; _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 6);
            StepGameplayUpdates(1, Vector2.Up, ["move_up"], [], batched);
            native[0xd00b] = (byte)_player.Position.Y; native.AdvanceParts(_entities.FrameCounter); native.AdvanceInteractions(_entities.FrameCounter);
            FailIf(_entities.ActiveTriggers != 1 || native[0xcca0] != 1 || !parent.Finished || native[0xd240] != 0 ||
                _runtimeState.ReadWramByte(OracleRuntimeState.ArmosTriggerAddress) != 1 || native[0xcca2] != 1,
                "The first real Moonlit pressure action must consume native state-zero $0c immediately, including under $02.");

            // Also sample an already-visited $0c controller under text/$02.
            ReinitializeGameplayForValidation(); LoadValidationRoom(4, 0x5e); _player.WarpTo(new(152, 40));
            _inventory.SetScriptedEquippedItems(0, 0); StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            parent = _entities.Entities<MoonlitGrottoArmosEventRoomEntity>().Single();
            priorText = _entities.TextActiveSource; _entities.TextActiveSource = () => gate == "text";
            _entities.SetTrigger(0, true); _runtimeState.SetWramByte(WramAddress.wDisabledObjects, gate == "mask02" ? (byte)6 : (byte)4);
            native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 152, 40);
            native[0xd240] = 1; native[0xd241] = 0x21; native[0xd242] = 0x0c; native[0xcca0] = 1;
            native[0xcc8a] = gate == "mask02" ? (byte)6 : (byte)4; native[0xcba0] = gate == "text" ? (byte)1 : (byte)0;
            try
            {
                StepGameplayUpdates(1, Vector2.Zero, batched: batched); native.AdvanceInteractions(_entities.FrameCounter);
                FailIf(!parent.Finished || native[0xd240] != 0 || _runtimeState.ReadWramByte(OracleRuntimeState.ArmosTriggerAddress) != native[0xcca2],
                    "Visited $0c retains native state zero and must remain eligible for a later declared shared trigger.");
            }
            finally { _entities.TextActiveSource = priorText; _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0); }
        }
        ReinitializeGameplayForValidation();
    }

    private void ValidateVolcanoInitializationGatesRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (string gate in new[] { "text", "mask02" })
        {
            ReinitializeGameplayForValidation(); _saveData.SetGlobalFlag(GlobalFlag.TuniNutPlaced, false); LoadValidationRoom(1, 3);
            _inventory.SetScriptedEquippedItems(0, 0); _player.BeginCutsceneControl();
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 80, 72);
            native[0xd240] = 1; native[0xd241] = 0xdc; native[0xd242] = 0x13;
            native[0xd340] = 1; native[0xd341] = 0xdc; native[0xd342] = 6; native[0xd34b] = 24; native[0xd34d] = 80;
            var priorText = _entities.TextActiveSource; bool held = true;
            _entities.TextActiveSource = () => held && gate == "text";
            void Gates()
            {
                native[0xcc8a] = held && gate == "mask02" ? (byte)6 : (byte)4;
                native[0xcba0] = held && gate == "text" ? (byte)1 : (byte)0;
                _runtimeState.SetWramByte(WramAddress.wDisabledObjects, native[0xcc8a]);
            }
            try
            {
                Gates(); int age = 0;
                StepGameplayUpdates(3, Vector2.Zero, batched: batched, afterUpdate: () =>
                {
                    age++; native.AdvanceInteractions(_entities.FrameCounter);
                    var child = _entities.EntityAdapters<VolcanoRoomEntity>().Single(entity => entity.Node.Name.ToString().Contains("b2_"));
                    int at = NativeAdmissionChildren(native, 0xb2, 1).Single();
                    FailIf(child.State != (age == 1 ? 0 : 1) || native[at + 4] != child.State || child.Counter != 0 || native[at + 6] != 0,
                        "Volcano's checked $06 child must initialize on its next physical visit under text/$02, then freeze normally.");
                });
                held = false; Gates(); StepGameplayUpdates(1, Vector2.Zero, batched: batched); native.AdvanceInteractions(_entities.FrameCounter);
                var resumed = _entities.EntityAdapters<VolcanoRoomEntity>().Single();
                int source = NativeAdmissionChildren(native, 0xb2, 1).Single(), counter = resumed.Counter;
                FailIf(counter != native[source + 6], "Volcano child must resume the same source script/counter update.");
                held = true; Gates(); StepGameplayUpdates(2, Vector2.Zero, batched: batched, afterUpdate: () => native.AdvanceInteractions(_entities.FrameCounter));
                FailIf(resumed.Counter != counter || native[source + 6] != counter, "Reapplying the interaction gate must freeze the initialized Volcano child.");
            }
            finally { _entities.TextActiveSource = priorText; _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0); }
            _entities.Clear(); FailIf(InteractionAdmissionPool.Count != 0, "Volcano cancellation must release parent and child slots.");
        }
        ReinitializeGameplayForValidation();
    }
}
