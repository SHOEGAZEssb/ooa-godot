using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateNayruGhostPublicationRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); _saveData.SetGlobalFlag(GlobalFlag.IntroDone, false);
            LoadValidationRoom(0, 0x39);
            var owner = _roomEvents.Get<NayruIntroEvent>();
            var actor = owner.ActorRegistry.Spawn("GhostVeran", "GhostVeran", new(88.25f, 48.5f));
            _player.WarpTo(new(120, 112)); _player.BeginCutsceneControl(owner: owner);
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 1);
            _runtimeState.SetWramByte(WramAddress.wWarpsDisabled, 1);
            _runtimeState.SetWramByte(0xcfd0, 0x17); _runtimeState.SetWramByte(0xcfd2, 0);
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 120, 112);
            native.InitializeLinkGameplay(); native[0xcc8a] = 1; native[0xcc6e] = 1;
            int at = 0xd040 + _entities.InteractionSlot(actor) * 256;
            native[at] = 0x81; native[at + 1] = 0x3e; native[at + 4] = 1; native[at + 5] = 7;
            native.Word(at + 10, 0x3080); native.Word(at + 12, 0x5840);
            native[0xcfd0] = 0x17; native[0xcfd2] = 0;
            // Declared substate7 boundary: the actual native handler installs
            // part2 and executes setcoords/wait30 in this same slot.
            owner.GhostScript.Start(actor); native.AdvanceInteractions();
            FailIf(owner.GhostScript.PrecisePosition != new Vector2(120.25f, 36.5f) || native[at + 5] != 8,
                "Ghost part2 entry must retain incoming low coordinate bytes and fall through substate8.");
            int visits = 0, upward = 0, downward = 0;
            bool heldUp = false, heldDown = false;
            void Seed() { for (int i = 0; i < 4; i++) { byte value = (byte)(0xa5 + i); native[0xcec0 + i] = value; _runtimeState.SetWramByte(0xcec0 + i, value); } }
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                int y = native.Word(at + 10); native.AdvanceInteractions(); visits++;
                if (native[at] != 0 && native.Word(at + 10) != y)
                { if (native[at + 9] == 0) upward++; else downward++; }
                FailIf(owner.GhostScript.Active != (native[at] != 0) || native[at] != 0 &&
                    owner.GhostScript.PrecisePosition != new Vector2(native.Word(at + 12) / 256f, native.Word(at + 10) / 256f) ||
                    Enumerable.Range(0, 4).Any(i => _runtimeState.ReadWramByte(0xcec0 + i) != native[0xcec0 + i]) ||
                    _runtimeState.ReadWramByte(0xcfd0) != native[0xcfd0],
                    $"Ghost Veran $3e:$00 part2 visit{visits}: native coordinates/movement publication/phase/deletion differ.");
                Seed();
            });
            Seed();
            while (owner.GhostScript.Active && visits < 260)
            {
                if (owner.GhostScript.Runner.CurrentCommand is CutsceneMemoryGateCommand)
                { _runtimeState.SetWramByte(0xcfd2, 0xff); native[0xcfd2] = 0xff; }
                bool hold = !heldUp && upward == 3 || !heldDown && downward == 3;
                if (hold)
                {
                    if (downward != 0) heldDown = true; else heldUp = true;
                    _dialogue.ShowMessage("Ghost movement waits for text.", _player.Position.Y); native[0xcba0] = 1;
                    int before = upward + downward; Step(3);
                    FailIf(upward + downward != before, "Ghost scripted movement must freeze during native active text.");
                    _dialogue.Close(); native[0xcba0] = 0;
                }
                Step(1);
            }
            FailIf(owner.GhostScript.Active || upward != 0x44 || downward != 0x22 || !heldUp || !heldDown ||
                _runtimeState.ReadWramByte(0xcfd0) != 0x1a || actor.Active,
                "Ghost part2 must complete68 quarter-speed up and34 half-speed down movements, both text holds, phase1a and deletion.");
            owner.Cancel(); _entities.Clear();
        }
    }
}
