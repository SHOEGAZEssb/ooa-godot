using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateMooshGhiniCircleRom()
    {
        // Clean-US scripts.s: enter each asm15 helper, then execute the real
        // wait1/scriptjump loop. State-zero admission and companion signals
        // are separate boundaries; no imported timing table supplies this oracle.
        var cases = new[] { (0, 0, 0x7592), (1, 0, 0x75b9), (1, 1, 0x75d3), (2, 0, 0x75f5) };
        foreach (var (subId, circle, entry) in cases)
        foreach (bool batched in new[] { false, true })
        foreach (int hold in new[] { 0, 5, 36 })
        {
            ReinitializeGameplayForValidation();
            _saveData.WriteWramByte(0xc6bf, 2); _saveData.SetRoomFlag(1, 0x79, 0x40, true);
            _saveData.WriteWramByte(0xc648, 0); _inventory.LoseTreasure(0x52);
            LoadValidationRoom(0, 0x6c);
            var owner = _roomEvents.Get<MooshRescueEvent>();
            string name = $"Ghini{subId}";
            var commands = subId == 0 ? owner.Database.Ghini0 : subId == 1 ? owner.Database.Ghini1 : owner.Database.Ghini2;
            int first = commands.Select((command, index) => (command, index)).Where(row => row.command is
                CutsceneNativeBlockingCommand { Handler: "CircleGhini" } || row.command is CutsceneNativeCommand native && native.Handler == $"Begin{name}Circle")
                .ElementAt(circle).index;
            var lanes = SomariaPrivate<CutsceneCommandLaneScheduler>(owner, "_lanes");
            lanes.Clear();
            // Retain the production callback when the native mover is independent
            // of script dispatch. Reflection only selects this declared lane.
            var mover = typeof(MooshRescueEvent).GetMethod("AdvanceGhiniCircle", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            lanes.StartLane(name, commands, firstCommand: first, beforeAdvance: () => mover?.Invoke(owner, [name]));
            var actor = SomariaPrivate<Dictionary<string, NpcCharacter>>(owner, "_actors")[name];
            var positions = SomariaPrivate<Dictionary<string, OracleObjectPosition>>(owner, "_positions");
            positions[name] = OracleObjectMovement.Shared.PositionFromPixels(new(88.25f, 48.5f));
            actor.SetStatePosition(positions[name].PixelPosition);
            _player.WarpTo(new(120, 112)); _player.BeginCutsceneControl(owner: owner);
            _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0x11);
            _runtimeState.SetWramByte(AgesWramAddress.wWarpsDisabled, 1);
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 120, 112);
            native.InitializeLinkGameplay(); native[0xcc8a] = 0x11; native[0xcc6e] = 1;
            int at = 0xd040 + _entities.InteractionSlot(actor) * 256;
            native[at] = 1; native[at + 1] = 0x73; native[at + 2] = (byte)subId;
            native.AdvanceInteractions();
            FailIf(native[at + 4] != 1, "Native Ghini source initializer did not admit the declared actor.");
            native.Word(at + 10, 0x3080); native.Word(at + 12, 0x5840); native.Word(at + 0x18, entry);
            int visits = 0, moves = 0; byte[] expected = new byte[4];
            void Seed() { for (int i = 0; i < 4; i++) { byte value = (byte)(0xa5 + i); native[0xcec0 + i] = value; _runtimeState.SetWramByte(0xcec0 + i, value); } }
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                int x = native.Word(at + 12), y = native.Word(at + 10);
                native.AdvanceInteractions(); visits++;
                for (int i = 0; i < 4; i++) expected[i] = native[0xcec0 + i];
                if (x != native.Word(at + 12) || y != native.Word(at + 10)) moves++;
                var actual = positions[name];
                FailIf(actual != OracleObjectMovement.Shared.PositionFromPixels(new(native.Word(at + 12) / 256f, native.Word(at + 10) / 256f)) ||
                    Enumerable.Range(0, 4).Any(i => _runtimeState.ReadWramByte(0xcec0 + i) != expected[i]) ||
                    _dialogue.IsOpen != (native[0xcba0] != 0),
                    $"Ghini $73:${subId:x2} circle{circle} visit{visits} hold{hold}: native position/publication/text boundary differs (moves{moves}, nativeText${native[0xcba0]:x2}).");
                Seed();
            });
            Seed(); Step(4);
            if (hold != 0)
            {
                _dialogue.ShowMessage("Ghini native movement continues during text.", _player.Position.Y); native[0xcba0] = 1;
                Step(hold); _dialogue.Close(); native[0xcba0] = 0;
            }
            for (int limit = 0; !_dialogue.IsOpen && limit < 50; limit++) Step(1);
            FailIf(moves != 32 || !_dialogue.IsOpen || native[at + 0x10] != 0,
                $"Ghini${subId:x2}/circle{circle}/hold{hold}: expected32 moves, text, speed0; actual {moves}/{_dialogue.IsOpen}/${native[at + 0x10]:x2} after{visits} visits.");
            Step(2); // A completed native circle cannot restart under its own text.
            owner.Cancel(); _dialogue.Close(); _entities.Clear();
        }
    }
}
