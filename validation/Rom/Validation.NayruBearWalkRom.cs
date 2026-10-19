using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateNayruBearWalkRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool cancel in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); _saveData.SetGlobalFlag(GlobalFlag.IntroDone, false);
            _saveData.SetRoomFlag(0, 0x39, 0x80, false); LoadValidationRoom(0, 0x39);
            var owner = _roomEvents.Get<NayruIntroEvent>(); var bear = owner.ActorRegistry["Bear"];
            // Declare the four preceding conversations; approach the remaining
            // bear through this room's actual geometry and central A routing.
            F1Set(owner, "_nayruAudienceMask", 0x0f);
            _player.WarpTo(new(88, 96));
            _runtimeState.SetWramByte(WramAddress.wWarpsDisabled, 1);
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 88, 96)
                { CompanionDispatchEnabled = true };
            native.InitializeLinkGameplay(); native[0xcc6e] = 1; native[0xcfde] = 0x0f;
            int at = 0xd040 + _entities.InteractionSlot(bear) * 256;
            native[at] = 1; native[at + 1] = 0x5d; native[at + 0x0b] = 0x28; native[at + 0x0d] = 0x58;
            int visits = 0, moves = 0; bool held = false, cancelled = false;
            void Step(int count, Vector2 direction, int keys = 0, int angle = 0xff, bool press = false, bool compare = true)
                => StepGameplayUpdates(count, direction, pressed: press ? ["attack"] : [], batched: batched, afterUpdate: () =>
                {
                    int y = native.Word(at + 10);
                    native.UpdateGameplay(press ? 1 : 0, keys, angle, _entities.FrameCounter); visits++;
                    if (native[at + 4] != 0 && native.Word(at + 10) != y && native[at + 0x10] != 0) moves++;
                    if (!compare) return;
                    FailIf(_player.Position != new Vector2(native[0xd00d], native[0xd00b]) ||
                        SomariaPrivate<OracleObjectPosition>(owner, "_bearObjectPosition").PrecisePosition !=
                            new Vector2(native.Word(at + 12) / 256f, native.Word(at + 10) / 256f) ||
                        _dialogue.IsOpen != (native[0xcba0] != 0) ||
                        _runtimeState.ReadWramByte(WramAddress.wDisabledObjects) != native[0xcc8a] ||
                        _saveData.HasRoomFlag(0, 0x39, 0x80) != ((native[0xc739] & 0x80) != 0) ||
                        Enumerable.Range(0, 4).Any(i => _runtimeState.ReadWramByte(0xcec0 + i) != native[0xcec0 + i]),
                        $"Nayru bear walk visit{visits}: source approach/position/counter publication/text/control/room flag differs (moves{moves}, stage{owner.CurrentStage}, Link{_player.Position}/{native[0xd00d]},{native[0xd00b]}).");
                });
            Step(2, Vector2.Zero, compare: false); // original state0 then initcollisions/checkabutton.
            Step(28, Vector2.Up, 0x40, 0);
            Step(1, Vector2.Zero, 1, press: true);
            FailIf(owner.CurrentStage != 2 || native[at + 0x31] != 0 || (native[0xcfde] & 0x1f) != 0x1f,
                $"The reachable bear must consume the fifth audience conversation through actual A routing (stage{owner.CurrentStage}, nativeA${native[at + 0x31]:x2}, audience${native[0xcfde]:x2}, Link{_player.Position}).");
            while (!_dialogue.IsOpen && visits < 150)
            {
                if (cancel && moves == 3)
                {
                    OracleObjectPosition before = SomariaPrivate<OracleObjectPosition>(owner, "_bearObjectPosition");
                    owner.Cancel(); cancelled = true;
                    Step(2, Vector2.Zero, compare: false);
                    FailIf(owner.HasState || _player.CutsceneControlled ||
                        SomariaPrivate<CutsceneCommandRunner>(owner, "_bearRunner").Active ||
                        _runtimeState.ReadWramByte(WramAddress.wDisabledObjects) != 0 ||
                        SomariaPrivate<OracleObjectPosition>(owner, "_bearObjectPosition") != before ||
                        _saveData.HasRoomFlag(0, 0x39, 0x80),
                        "Cancelling the bear walk must retire its runner and own control mask without completing or moving it.");
                    break;
                }
                if (!held && moves == 3)
                {
                    held = true; _dialogue.ShowMessage("Bear movement text hold.", _player.Position.Y); native[0xcba0] = 1;
                    int before = moves; Step(3, Vector2.Zero);
                    FailIf(moves != before, "Ordinary bear movement must pause under text.");
                    _dialogue.Close(); native[0xcba0] = 0;
                    _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0x13); native[0xcc8a] = 0x13;
                    Step(3, Vector2.Zero);
                    FailIf(moves != before, "The bear's initialized script must retain its counter under interaction mask$02.");
                    _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0x11); native[0xcc8a] = 0x11;
                }
                Step(1, Vector2.Zero);
            }
            if (cancel)
            {
                FailIf(!cancelled, "The bear cancellation case must reach actual movement before cancellation.");
                _entities.Clear(); continue;
            }
            FailIf(!held || moves != 31 || !_dialogue.IsOpen || native[at + 0x10] != 0x14 ||
                SomariaPrivate<OracleObjectPosition>(owner, "_bearObjectPosition").YFixed != 0x2880,
                "The original bear conversation must perform31 half-pixel moves, retain$80 low Y, and openTX5703 after the source settle waits.");
            _dialogue.Close(); native[0xcba0] = 0; Step(2, Vector2.Zero);
            // Repeat the central interaction from the now-open path. No second
            // walk occurs once the source room flag is set.
            Step(18, Vector2.Up, 0x40, 0);
            Step(1, Vector2.Zero, 1, press: true);
            for (int limit = 0; !_dialogue.IsOpen && limit < 4; limit++) Step(1, Vector2.Zero);
            FailIf(!_dialogue.IsOpen || moves != 31, "Repeat bear conversation must remain reachable without repeating its walk.");
            owner.Cancel(); _dialogue.Close(); _entities.Clear();
        }
    }
}
