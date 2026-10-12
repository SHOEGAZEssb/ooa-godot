using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePostD7Handoff()
    {
        foreach (bool linked in new[] { false, true })
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(linked);
            for (int i = 0; i < 7; i++) _inventory.GiveTreasure(TreasureId.Essence, i);
            _saveData.SetMakuTreeState(6);
            _saveData.SetRoomFlag(2, 0x90, 2);
            _saveData.SetGlobalFlag(GlobalFlag.GotPermissionToEnterJabu);
            LoadValidationRoom(2, 0x90);
            _player.WarpTo(new(72, 72), recordSafe: false);
            var owner = _roomEvents.Get<PostD7Event>();
            var remote = _roomEvents.Get<RemoteMakuSeventhEssenceEvent>();
            var texts = new List<int>();
            int hold = 0, updates = 0;
            void Observe()
            {
                updates++;
                if (_dialogue.IsOpen && hold == 0)
                { texts.Add(_dialogue.SourceTextId ?? -1); hold = 3; }
                if (hold > 0 && --hold == 0) _dialogue.Close();
            }
            for (int i = 0; i < 6000 && (owner.HasState || remote.HasState || i < 2); i += 2)
                StepGameplayUpdates(2, Vector2.Zero, batched: batch, afterUpdate: Observe);
            int[] expected = linked
                ? [0x2809, 0x280a, 0x130b, 0x130c, 0x130d, 0x05c9]
                : [0x2808, 0x130b, 0x130c, 0x130d, 0x05b9];
            FailIf(owner.HasState || remote.HasState || !texts.SequenceEqual(expected),
                $"Post-D7 linked={linked}, batch={batch}, update={updates}, stage={owner.Stage}, state={owner.State}, room={_rooms.ActiveGroup:x}:{_rooms.CurrentRoom.Id:x2}, signals={_entities.RuntimeState.ReadWramByte(0xcfc0):x2}/{_entities.RuntimeState.ReadWramByte(0xcfdf):x2}, texts={string.Join(',', texts.Select(x => x.ToString("x4")))}.");
            FailIf(_rooms.ActiveGroup != 2 || _rooms.CurrentRoom.Id != 0x90 ||
                !_saveData.HasRoomFlag(4, 0xf6, 0x40) || !_saveData.HasRoomFlag(2, 0x90, 0x40) ||
                _saveData.MakuTreeState != 7 || _saveData.MakuMapTextPresent != (linked ? 0xc9 : 0xb9) ||
                !_player.Visible || _hud.StatusBarHidden || _player.CutsceneControlled ||
                _entities.RuntimeState.ReadWramByte(WramAddress.wDisabledObjects) != 0 ||
                _entities.RuntimeState.ReadWramByte(WramAddress.wMenuDisabled) != 0,
                "Post-D7 must persist the tower and remote-Maku flags and return control to Link in $2:$90.");
            LoadValidationRoom(2, 0x90); StepGameplayUpdates(4, Vector2.Zero);
            FailIf(owner.HasState || remote.HasState, "Completed post-D7 handoff must not replay on room re-entry.");
        }
        ReinitializeGameplayForValidation();
        _inventory.GiveTreasure(TreasureId.Essence, 6); LoadValidationRoom(2, 0x90);
        StepGameplayUpdates(12, Vector2.Zero); LoadValidationRoom(1, 0x36); StepGameplayUpdates(2, Vector2.Zero);
        FailIf(_roomEvents.Get<PostD7Event>().HasState || !_player.Visible || _hud.StatusBarHidden ||
            _entities.RuntimeState.ReadWramByte(WramAddress.wDisabledObjects) != 0,
            "Cancelling post-D7 must release its palette, HUD and input ownership.");
    }
}
