using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom025Carpenters()
    {
        CarpenterEvent quest = _roomEvents.Carpenters;
        CarpenterDatabase data = quest.Database;
        _saveData.SetLinkedGame(false);
        foreach (string flag in new[] { "bridge-flag", "flute-flag", "talked-flag", "zelda-flag" })
            _saveData.SetGlobalFlag(data.Constant(flag), false);
        _runtimeState.SetWramByte(data.Constant("state-address"), 0);
        _runtimeState.SetWramByte(data.Constant("found-address"), 0);

        void Enter()
        {
            LoadValidationRoom(0, 0x25);
            _player.WarpTo(new Vector2(0x78, 0x68), recordSafe: false);
        }
        NpcCharacter Actor(int subid) => _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x9a && n.Record.SubId == subid);
        string Text(int id) => DialogueBox.PlainText(data.Commands.OfType<CutsceneShowTextCommand>().First(c => c.TextId == id).Message);
        void AwaitText(int id)
        {
            for (int i = 0; i < 1200 && !_dialogue.IsOpen; i++) StepRoomEventFrames(1);
            FailIf(!_dialogue.IsOpen || _dialogue.CurrentMessage != Text(id),
                $"Room $0:$25 expected TX_{id:x4}, got '{_dialogue.CurrentMessage}'.");
        }
        void Talk(int id)
        {
            StepRoomEventFrames(2);
            FailIf(!quest.TryInteractNpc(Actor(0)), "Head carpenter did not accept the registered A-button interaction.");
            AwaitText(id);
        }

        Enter();
        FailIf(!_entities.EntityAdapters<CarpenterRoomEntity>().Select(n => n.Npc.Record.SubId).SequenceEqual(new[] { 0, 1, 2, 3, 4 }) ||
            !Actor(0).Active || !Actor(1).Active || Actor(2).Active || Actor(3).Active || Actor(4).Active ||
            Actor(0).Record.Palette != 3 || Actor(1).Record.Palette != 0 ||
            Actor(0).CurrentScriptAnimationSource != data.Script(0).Animation ||
            Actor(1).CurrentScriptAnimationSource != data.Script(1).Animation ||
            _rooms.CurrentRoom.GetMetatile(new Vector2(0x58, 0x58)) != 0 ||
            _rooms.CurrentRoom.GetTerrainInfo(new Vector2(0x58, 0x58)).Collision != 0x0f,
            "Room $0:$25 did not retain source-ordered carpenter slots, palettes, animations, found-bit suppression, and blocker WRAM tile.");
        Talk(0x2301);
        StepRoomEventFrames(16);
        FailIf(quest.SearchState != 0, "Head carpenter's script advanced during dialogue.");
        FailIf(_saveData.HasGlobalFlag(data.Constant("talked-flag")), "Head carpenter wrote TALKED_TO_HEAD_CARPENTER before TX_2301 closed.");
        _dialogue.Close();
        StepRoomEventFrames(1);
        FailIf(!_saveData.HasGlobalFlag(data.Constant("talked-flag")) || quest.SearchState != 0,
            "Head carpenter's pre-flute talk did not write its flag while retaining search state $00.");

        _saveData.SetGlobalFlag(data.Constant("flute-flag"));
        Talk(0x2302);
        FailIf(!_dialogue.ChoiceActive, "TX_2302 is not a choice.");
        _dialogue.SubmitChoiceForValidation(1);
        AwaitText(0x2303);
        _dialogue.Close();
        StepRoomEventFrames(2);
        FailIf(quest.SearchState != 0, "Refusing the carpenter search started it.");
        Talk(0x2302);
        _dialogue.SubmitChoiceForValidation(0);
        AwaitText(0x2304);
        FailIf(!_dialogue.ChoiceActive || !Text(0x2304).Contains(Text(0x2305), StringComparison.Ordinal),
            "Unterminated TX_2304 did not fall through into TX_2305 and its explanation choice.");
        // TX_2304's explicit newline is the sole separator at the physical
        // fallthrough: the fourth rendered line must begin TX_2305 directly.
        FailIf(_dialogue.GlyphCodeForValidation(0, 2, 0) != 'b' ||
            _dialogue.GlyphCodeForValidation(0, 3, 0) != 'I' ||
            _dialogue.GlyphCodeForValidation(0, 4, 0) != 'a',
            "TX_2304 -> TX_2305 inserted an empty line between 'be off, then!' and 'If you can get'.");
        _dialogue.SubmitChoiceForValidation(1);
        AwaitText(0x2305);
        _dialogue.SubmitChoiceForValidation(1);
        AwaitText(0x2305);
        _dialogue.SubmitChoiceForValidation(0);
        StepRoomEventFrames(2);
        FailIf(quest.SearchState != 1 || _player.CutsceneControlled,
            "Finishing the repeatable explanation did not set transient search state $01 and return control.");
        Talk(0x2306);
        _dialogue.Close();

        // Every proper subset of the returned bits shows only those carpenters.
        foreach (int mask in new[] { 0x04, 0x08, 0x10, 0x0c, 0x14, 0x18 })
        {
            _runtimeState.SetWramByte(data.Constant("found-address"), (byte)mask);
            Enter();
            for (int subid = 2; subid <= 4; subid++)
                FailIf(Actor(subid).Active != ((mask & (1 << subid)) != 0),
                    $"Room $0:$25 returned carpenter ${subid:x2} mismatched found mask ${mask:x2}.");
            StepRoomEventFrames(2);
            int returned = Enumerable.Range(2, 3).First(s => (mask & (1 << s)) != 0);
            FailIf(!quest.TryInteractNpc(Actor(returned)), "Returned carpenter was not A-sensitive.");
            AwaitText(0x2310);
            int talkFrame = Actor(returned).CurrentAnimationFrame;
            StepRoomEventFrames(16);
            FailIf(Actor(returned).CurrentAnimationFrame == talkFrame || quest.SearchState != 1,
                "Carpenter enabled bit 7 did not animate during dialogue while its script remained frozen.");
            _dialogue.Close();
            StepRoomEventFrames(2);
            FailIf(Actor(returned).CurrentScriptAnimationSource != data.Script(returned).Animation || quest.BlocksGameplay,
                "Returned carpenter did not restore animation $02 after TX_2310.");
        }

        _saveData.SetLinkedGame(true);
        Enter();
        FailIf(_entities.Entities<NpcCharacter>().Any(n => n.Active) || quest.HasState ||
            _rooms.CurrentRoom.GetMetatile(new Vector2(0x58, 0x58)) == 0,
            "Linked-game carpenters or their tile blocker initialized before Zelda's ring flag.");
        _saveData.SetGlobalFlag(data.Constant("zelda-flag"));
        Enter();
        FailIf(!Actor(0).Active || !Actor(1).Active, "Zelda's ring flag did not admit linked-game carpenters.");
        _saveData.SetLinkedGame(false);

        _runtimeState.SetWramByte(data.Constant("found-address"), 0x1c);
        Enter();
        FailIf(Actor(0).Position != new Vector2(0x58, 0x68) ||
            !_entities.EntityAdapters<CarpenterRoomEntity>().Select(n => n.ScriptSubid).SequenceEqual(new[] { 5, 1, 6, 7, 8 }),
            "Finding all three carpenters did not select native bridge-build subids $05/$01/$06/$07/$08.");
        var trace = new ValidationCutsceneTrace();
        int bridgeSounds = _sound.PlayRequestsFor(data.Constant("bridge-sound"));
        _roomEvents.CommandTraceSink = trace;
        StepRoomEventFrames(1);
        FailIf(!quest.BlocksGameplay || !quest.MenusDisabled || !_player.CutsceneControlled || Actor(0).ScriptDrawOffset.Y != 0 ||
            !_entities.EntityAdapters<CarpenterRoomEntity>().Any(e => e.FreezesRoomEntities),
            "Bridge build did not disable input at callscript while deferring the jump until the next update.");
        StepRoomEventFrames(1);
        FailIf(Actor(0).ScriptDrawOffset.Y != 0, "setzspeed moved the carpenter before the next native gravity update.");
        StepRoomEventFrames(1);
        FailIf(Actor(0).ScriptDrawOffset.Y != -2, "Carpenter jump did not apply source -$0200 Z speed on its first native update.");
        AwaitText(0x230a);
        FailIf(quest.SearchState != 1, "Bridge signal $02 was written before TX_230a closed.");
        _dialogue.Close();
        AwaitText(0x2311);
        _dialogue.Close();
        var columns = new List<int>();
        int previousState = quest.SearchState;
        for (int frame = 0; frame < 1600 && quest.HasState; frame++)
        {
            if (_dialogue.IsOpen) _dialogue.Close();
            StepRoomEventFrames(1);
            int state = quest.SearchState;
            if (state != previousState && state is >= 4 and <= 6)
            {
                int column = 6 - state;
                columns.Add(column);
                FailIf(_rooms.CurrentRoom.GetMetatile(new Vector2(column * 16 + 8, 0x58)) != 0x1d ||
                    _rooms.CurrentRoom.GetMetatile(new Vector2(column * 16 + 8, 0x68)) != 0x1e,
                    $"Carpenter bridge column ${column:x2} did not atomically install top/bottom tiles at signal ${state:x2}.");
            }
            previousState = state;
        }
        FailIf(quest.HasState || quest.BlocksGameplay || quest.MenusDisabled || _player.CutsceneControlled ||
            !_saveData.HasGlobalFlag(data.Constant("bridge-flag")) || quest.SearchState != 0x0b ||
            !columns.SequenceEqual(new[] { 2, 1, 0 }) || _entities.Entities<NpcCharacter>().Any(n => n.Active) ||
            _rooms.CurrentRoom.GetMetatile(new Vector2(0x58, 0x58)) != 0x3a,
            "Carpenter sequence stalled or lost bridge-column order, blocker removal, departure, persistent completion, or input release.");
        FailIf(!trace.Observations.Where(o => o.Observation == "Dialogue").Select(o => o.Value)
                .SequenceEqual(new[] { 0x230a, 0x2311, 0x2312, 0x230b, 0x2311 }),
            "Room $0:$25 bridge-build dialogue did not preserve the independently scheduled actor order.");
        int[] buildFrames = trace.Entries.Where(e => e.Phase == CutsceneCommandTracePhase.Started &&
                data.Commands[e.Source.CommandIndex] is CutsceneNativeCommand native &&
                native.Handler.StartsWith("BuildColumn:", StringComparison.Ordinal))
            .Select(e => e.ScriptUpdate).ToArray();
        FailIf(buildFrames.Length != 3 || buildFrames[1] - buildFrames[0] != 107 ||
            buildFrames[2] - buildFrames[1] != 107 ||
            _sound.PlayRequestsFor(data.Constant("bridge-sound")) - bridgeSounds != 3,
            "Carpenter columns lost the source $10 movement counter, 90-update waits, asm15 carry, or one sound per column: " +
            $"updates=[{string.Join(",", buildFrames)}], sounds={_sound.PlayRequestsFor(data.Constant("bridge-sound")) - bridgeSounds}.");
        _roomEvents.CommandTraceSink = null;
        Enter();
        FailIf(quest.HasState || _entities.Entities<NpcCharacter>().Any(n => n.Active),
            "Completed bridge carpenters respawned on room re-entry.");
        for (int x = 0; x < 3; x++)
            FailIf(_rooms.CurrentRoom.GetMetatile(new Vector2(x * 16 + 8, 0x58)) != 0x1d ||
                _rooms.CurrentRoom.GetMetatile(new Vector2(x * 16 + 8, 0x68)) != 0x1e,
                "GLOBALFLAG_SYMMETRY_BRIDGE_BUILT did not reconstruct the completed bridge.");

        // Cancelling a partially played cutscene releases control, without saving completion.
        _saveData.SetGlobalFlag(data.Constant("bridge-flag"), false);
        Enter();
        StepRoomEventFrames(3);
        FailIf(!quest.BlocksGameplay, "Carpenter cancellation fixture did not acquire input.");
        LoadValidationRoom(0, 0x24);
        FailIf(quest.HasState || _player.CutsceneControlled || _saveData.HasGlobalFlag(data.Constant("bridge-flag")),
            "Leaving the carpenter cutscene leaked control or persisted an incomplete bridge.");
        _transitions.BeginScroll(_player, Vector2I.Right, 0x25);
        NpcCharacter incoming = Actor(0);
        Vector2 incomingPosition = incoming.Position;
        int incomingFrame = incoming.CurrentAnimationFrame;
        StepRoomEventFrames(60);
        FailIf(quest.BlocksGameplay || incoming.Position != incomingPosition || incoming.CurrentAnimationFrame != incomingFrame,
            "Room $0:$25 destination carpenter event advanced during scroll preload.");
        _transitions.UpdateScroll(1.0);
        StepRoomEventFrames(1);
        FailIf(!quest.BlocksGameplay, "Room $0:$25 carpenter event did not start after the scroll completed.");
        LoadValidationRoom(0, 0x24);
        GD.Print("Validated room 0:25 carpenter search choices, linked gating, all returned masks, source-ordered bridge scripts/jumps/columns/departures, dialogue animation, completion, re-entry, scrolling, and cancellation.");
    }
}
