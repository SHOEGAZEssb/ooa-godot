using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateVireEntranceSlotsRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool complete in new[] { false, true })
        {
            InitializeCompletedIntroRoom(0, 0x11);
            _saveData.SetRoomFlag(4, 0xe6, 0x40, complete);
            LoadValidationRoom(4, 0xe6);
            var vire = _entities.EntityAdapters<VireEntranceRoomEntity>().Single();
            FailIf(_entities.NativeInteractionSlot(vire) != 2 || vire.State != 0 || vire.PresentationNode.Visible,
                "mainData.s:$4:$e6 must place uninitialized $b8:$00 before $71:$0c at $d2/$d3.");
            F1Retain(vire); _player.WarpTo(new(120, 152));
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 120, 152);
            native.InitializeLinkGameplay(); native[0xd240] = 1; native[0xd241] = 0xb8;
            native[0xd24b] = 120; native[0xd24d] = 120;
            native[0xcc35] = (byte)_sound.NativeActiveMusic; native[0xcc46] = (byte)_sound.NativeRoomMusic;
            int update = 0, generation = 0, hold = 0;
            void Compare()
            {
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter); update++;
                CompareFairyInteractionSlotsRom(native, $"Vire entrance update{update}, complete={complete}, batch={batched}");
                FailIf(vire.Finished != (native[0xd240] == 0) || !vire.Finished &&
                    (vire.State != native[0xd244] || vire.Counter != native[0xd246] ||
                    vire.PresentationNode.Position != new Vector2(native[0xd24d], native[0xd24b]) ||
                    vire.PresentationNode.ZIndex != ObjectDrawPriority.FromVisible(native[0xd25a])),
                    "Vire source state, counter, position or fixed visible$c2 priority differs.");
                FailIf(_runtimeState.ReadField("wDisabledObjects") != native[0xcc8a] ||
                    _runtimeState.ReadField("wMenuDisabled") != native[0xcc02],
                    $"Vire update{update} input mask ${_runtimeState.ReadField("wDisabledObjects"):x2}/${_runtimeState.ReadField("wMenuDisabled"):x2} differs from ${native[0xcc8a]:x2}/${native[0xcc02]:x2}; air={_runtimeState.ReadField("wLinkInAir")}/{native[0xcc5c]}, vulnerable={_player.NativePuzzleResetVulnerable}, native var2a/inv/knock=${native[0xd02a]:x2}/${native[0xd02b]:x2}/${native[0xd02e]:x2}.");
                if (generation != native.TextGeneration)
                {
                    generation = native.TextGeneration; hold = 3;
                    FailIf(!_dialogue.IsOpen || !_dialogue.CurrentMessage.Contains("A turtle works"), "Vire must show source TX_2f27 through its native slot.");
                }
                if (hold != 0 && --hold == 0) { _dialogue.Close(); native[0xcba0] = 0; }
            }
            StepGameplayUpdates(1, Vector2.Zero, batched: batched, afterUpdate: Compare);
            while (!vire.Finished && update < 120) StepGameplayUpdates(1, Vector2.Zero, batched: batched, afterUpdate: Compare);
            FailIf(!vire.Finished || _saveData.HasRoomFlag(4, 0xe6, 0x40) != true || generation != (complete ? 0 : 1),
                "Vire must finish its one text/movement/wait script, set room bit$40 and retire; completed reentry deletes in state0.");
        }
    }
}
