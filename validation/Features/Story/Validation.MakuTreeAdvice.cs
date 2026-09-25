using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMakuTreeAdviceAndLayout()
    {
        var advice = _roomEvents.Get<MakuTreeAdviceEvent>();
        var database = new MakuTreeAdviceDatabase();
        bool batched = false;
        void Step(int count = 1, Vector2 movement = default, bool press = false) =>
            StepGameplayUpdates(count, movement, press ? ["attack"] : [], press ? ["attack"] : [], batched: batched);
        void Approach()
        {
            _player.WarpTo(new Vector2(0x50, 0x68));
            FailIf(_currentRoom.IsSolid(_player.Position), "Maku approach begins inside solid geometry.");
            Step(40, Vector2.Up);
            FailIf(_player.Position.Y >= 0x58 || _currentRoom.IsSolid(_player.Position),
                $"Maku approach failed through room $0:38 geometry: {_player.Position}.");
        }

        // Independent makuTree.s state dispatch, including linked endgame mode.
        int[] states = [3,4,5,6,7,8,9,10,11,12,13,15,16];
        int[] modes = [2,0,0,0,4,4,2,0,5,4,0,0,1];
        int[] texts = [0x00,0x03,0x05,0x07,0x09,0x0b,0x0d,0x10,0x12,0x14,0x16,0x18,0x1c];
        foreach (bool linked in new[] { false, true })
        for (int i = 0; i < states.Length; i++)
        {
            FailIf(!database.TryGet(states[i], linked, out MakuTreeAdviceRecord record) ||
                record.Mode != (linked && states[i] == 16 ? 0 : modes[i]) ||
                record.TextId != 0x500 + (linked && states[i] == 16 ? 0x1a : texts[i]) + (linked ? 0x20 : 0),
                $"Maku state ${states[i]:x2} linked={linked} lost its source mode/text dispatch.");
        }
        database.TryGet(7, false, out MakuTreeAdviceRecord state7);
        database.TryGet(7, true, out MakuTreeAdviceRecord linkedState7);
        FailIf(!state7.Text.Contains("Black Tower", StringComparison.Ordinal) ||
            !state7.Text.Contains("peak northwest", StringComparison.Ordinal) ||
            !state7.Text.Contains("\\pos(2)", StringComparison.Ordinal) || linkedState7.Text != state7.Text,
            "TX_0509 must include the unterminated fallthrough into TX_050a, with formatting and bottom position.");

        _saveData.SetMakuTreeState(7);
        _saveData.SetGlobalFlag(OracleSaveData.GlobalFlagMakuTreeDisappeared);
        _saveData.SetGlobalFlag(OracleSaveData.GlobalFlagMakuTreeSaved);
        _saveData.SetRoomFlag(0, 0x38, OracleSaveData.RoomFlagLayoutSwap, false);
        _saveData.SetRoomFlag(1, 0x48, OracleSaveData.RoomFlagLayoutSwap, false);
        LoadValidationRoom(0, 0x38);
        FailIf(_currentRoom.GetMetatile(new Vector2(152, 40)) != 0x21,
            "Before past $1:48 bit $01, Maku room must retain its original top-right $21 patch.");
        _saveData.SetRoomFlag(1, 0x48, OracleSaveData.RoomFlagLayoutSwap);
        LoadValidationRoom(0, 0x38);
        FailIf(_currentRoom.GetMetatile(new Vector2(136, 40)) != 0x7e ||
            _currentRoom.GetMetatile(new Vector2(152, 40)) != 0x11 ||
            _currentRoom.GetMetatile(new Vector2(136, 56)) != 0x7f ||
            _currentRoom.GetMetatile(new Vector2(152, 56)) != 0x14,
            "Saved Maku layout must use group $03 top-right tiles $7e/$11/$7f/$14 (clean-ROM tileset $24).");
        using (Image room = _currentRoom.Texture.GetImage())
        {
            // Clean ROM reference: this patch uses tree-brown BG palette 2,
            // not the pink-flower palette 7 selected by the old $21 metatile.
            Color brown = Color.Color8(173, 148, 49);
            FailIf(room.GetPixel(151, 40) != brown,
                $"Saved Maku patch pixel (151,40) is {room.GetPixel(151, 40)}, expected source RGB5 $15/$12/$06.");
        }
        foreach (bool batch in new[] { false, true })
        foreach (bool linked in new[] { false, true })
        foreach (int state in new[] { 7, 3, 11, 16 })
        {
            batched = batch;
            _saveData.SetLinkedGame(linked);
            _saveData.SetMakuTreeState(state);
            _saveData.SetMakuMapTextPresent(0xb5);
            LoadValidationRoom(0, 0x38);
            Approach();
            NpcCharacter tree = _entities.Entities<NpcCharacter>().Single(npc => npc.Record.Id == 0x87);
            NpcCharacter flower = _entities.Entities<NpcCharacter>().Single(npc => npc.Record.Id == 0x86);
            FailIf(!advice.HasState || !advice.ButtonSensitive || !tree.Active || !flower.Active ||
                tree.CurrentAnimationOpaquePixels < 100 || tree.CurrentAnimationTextureSize.X <= 32,
                $"Maku state ${state:x2} must retain its visible full face and attached flower.");
            database.TryGet(state, linked, out MakuTreeAdviceRecord record);
            string initialAnimation = database.Graphics.Animation(state == 7 ? 4 : 0);
            FailIf(tree.CurrentScriptAnimationSource != initialAnimation,
                $"Maku state ${state:x2} has the wrong idle expression.");
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Step(press: true);
                Step(2);
                FailIf(!_dialogue.IsOpen || _dialogue.Position.Y != 96 ||
                    _saveData.MakuMapTextPresent != (record.TextId & 0xff),
                    $"Maku state ${state:x2} repeat {repeat} did not talk/update map advice through A input: pos={_player.Position}, facing={_player.FacingVector}, talk={tree.CanTalkTo(_player)}, open={_dialogue.IsOpen}, box={_dialogue.Position}, map=${_saveData.MakuMapTextPresent:x2}, command={advice.CurrentCommandIndex}.");
                bool pair = state is 3 or 11;
                FailIf(advice.BlocksGameplay != pair,
                    "Maku two-text modes alone must hold input across their dialogue gap.");
                int command = advice.CurrentCommandIndex;
                Step(8, Vector2.Down);
                FailIf(advice.CurrentCommandIndex != command,
                    "Maku script advanced while its dialogue was open.");
                _dialogue.Close();
                if (pair)
                {
                    Step(29);
                    FailIf(_dialogue.IsOpen || !advice.BlocksGameplay || advice.Counter != 1,
                        "Maku's 30-update dialogue gap ended before counter $01.");
                    Step();
                    FailIf(!_dialogue.IsOpen || _saveData.MakuMapTextPresent !=
                        ((state == 11 ? record.SecondTextId : record.TextId) & 0xff),
                        "Maku second text lost its exact wait boundary or mode-specific map write.");
                    _dialogue.Close();
                }
                Step(3);
                FailIf(advice.BlocksGameplay || _player.CutsceneControlled ||
                    tree.CurrentScriptAnimationSource != initialAnimation,
                    "Maku advice did not release input and restore its repeatable idle expression.");
            }
            if (state == 3)
            {
                Step(press: true); Step(2);
                _dialogue.Close(); Step(5);
                LoadValidationRoom(0, 0x11);
                FailIf(advice.HasState || _player.CutsceneControlled || flower.Active,
                    "Canceling Maku advice must release input and its related flower.");
            }
        }
        _saveData.SetRoomFlag(1, 0x48, OracleSaveData.RoomFlagLayoutSwap, false);
        LoadValidationRoom(0, 0x38);
        FailIf(_currentRoom.GetMetatile(new Vector2(152, 40)) != 0x21,
            "Maku layout cache retained the override after its source flag cleared.");
        GD.Print("Validated Maku Tree ordinary advice, source text fallthrough, face/flower, saved layout, repeat/cancel, and batched gameplay updates.");
    }
}
