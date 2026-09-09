using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom38fTokkey()
    {
        var tokkey = _roomEvents.Get<TokkeyEvent>();
        var trace = new ValidationCutsceneTrace();
        _roomEvents.CommandTraceSink = trace;
        _saveData.SetRoomFlag(3, 0x8f, 0x40, false);
        _inventory.GiveTreasure(TreasureDatabase.TreasureHarp, 0);
        _inventory.GiveTreasure(TreasureDatabase.TreasureTuneOfEchoes, 0);
        _inventory.SelectHarpSong(1);
        LoadValidationRoom(3, 0x8f);
        NpcCharacter Actor() => _entities.Entities<NpcCharacter>().Single(npc => npc.Record.Id == 0x9d);
        byte Signal() => _entities.RuntimeState.ReadWramByte(0xcfc0);
        void StepRoomEventFrames(int frames)
        {
            for (int frame = 0; frame < frames; frame++)
            {
                _harp.BeginObjectUpdate();
                this.StepRoomEventFrames(1);
            }
        }
        void ExpectText(int id)
        {
            string text = id == 0x2c05 ? tokkey.Database.WrongPositionText :
                tokkey.Database.Commands.OfType<CutsceneShowTextCommand>().First(c => c.TextId == id).Message;
            FailIf(!_dialogue.IsOpen || _dialogue.CurrentMessage != DialogueBox.PlainText(text),
                $"Room 3:8f expected TX_{id:x4} at command {tokkey.CommandIndex}.");
            if (id != 0x2c03)
                FailIf(_dialogue.Position.Y != 96,
                    $"Tokkey TX_{id:x4} lost source position $02 (screen Y=96): {_dialogue.Position}.");
        }
        void PressA()
        {
            Input.ActionPress("attack");
            try { base._Process(1.0 / 60.0); }
            finally { Input.ActionRelease("attack"); }
        }
        void FinishDialogue()
        {
            for (int press = 0; press < 200 && _dialogue.IsOpen; press++)
            {
                base._Process(30.0 / 60.0);
                PressA();
            }
            FailIf(_dialogue.IsOpen, "Tokkey dialogue did not finish through ordinary A-button input.");
        }
        void Talk(int text, bool approach = true)
        {
            if (approach)
            {
                _player.WarpTo(Actor().Position + new Vector2(0, 48));
                Input.ActionPress("move_up");
                try { base._Process(40.0 / 60.0); }
                finally { Input.ActionRelease("move_up"); }
            }
            base._Process(1.0 / 60.0);
            FailIf(!Actor().CanTalkTo(_player),
                $"Room 3:8f $9d:$00 cannot be talked to from reachable Link position {_player.Position} for TX_{text:x4}.");
            PressA();
            base._Process(1.0 / 60.0);
            ExpectText(text);
        }
        void Play(Vector2 position, bool batched = false)
        {
            _player.WarpTo(position);
            _player.StartHarpActionForValidation();
            FailIf(!_player.IsUsingHarp || _harp.PlayingSong != 1,
                "Room 3:8f failed to start the ordinary Tune of Echoes item.");
            int command = tokkey.CommandIndex;
            int animation = Actor().CurrentAnimationFrame;
            int notes = _harp.NoteSpawnCount;
            void ExpectStillPlaying()
            {
                FailIf(!_player.IsUsingHarp || !_player.HarpPoseActive || tokkey.State != 1 ||
                    tokkey.CommandIndex != command || Actor().CurrentAnimationFrame != animation ||
                    _dialogue.IsOpen || tokkey.BlocksGameplay ||
                    _entities.Entities<NpcCharacter>().Any(npc => npc.Record.Id == 0x9f && npc.Active),
                    "Tokkey interrupted the harp, advanced his script/animation, or reacted before ITEM_HARP's 260th update.");
            }
            // Exercise the actual application scheduler, including Link/item
            // completion before the interaction pass, and batched host frames.
            if (batched)
            {
                base._Process(259.0 / 60.0);
                ExpectStillPlaying();
            }
            else
            {
                for (int update = 0; update < 259; update++)
                {
                    base._Process(1.0 / 60.0);
                    ExpectStillPlaying();
                }
            }
            base._Process(1.0 / 60.0);
            FailIf(_player.IsUsingHarp || _player.HarpPoseActive || _harp.IsPlaying ||
                _entities.PlayingInstrumentSource() != 1 || _harp.NoteSpawnCount != notes + 8,
                "ITEM_HARP failed to finish naturally with eight notes and preserve $01 for Tokkey's completion-update collision check.");
        }
        void StopPlaying()
        {
            _player.WarpTo(_player.Position);
            _dialogue.Close();
        }

        FailIf(Actor().Position != new Vector2(0x28, 0x18) ||
            !Actor().Record.CanFace || Actor().CurrentAnimationOpaquePixels == 0 ||
            tokkey.CommandIndex != 0 || tokkey.State != 1,
            "Room 3:8f lost Tokkey's source placement, facing graphics, or state-0 script boundary.");
        // scriptCmd_initNpcHitbox tests Y alone, then either replaces both
        // radii or leaves both unchanged (including a zero X radius).
        Actor().SetCollisionRadii(0, 9);
        tokkey.InitializeActorCollisionRadii("Tokkey");
        FailIf(Actor().ObjectCollisionBounds.Size != new Vector2(12, 12),
            "Ages $eb must initialize both radii to $06 when Y is zero.");
        Actor().SetCollisionRadii(0x14, 0);
        tokkey.InitializeActorCollisionRadii("Tokkey");
        FailIf(Actor().ObjectCollisionBounds.Size != new Vector2(0, 40),
            "Ages $eb must preserve both radii when Y is nonzero, even if X is zero.");
        Actor().SetCollisionRadii(6, 6);
        StepRoomEventFrames(2);
        FailIf(Actor().ObjectCollisionBounds.Size != new Vector2(12, 40),
            "tokkeyScript did not initialize collision radii $14/$06.");
        Play(new Vector2(0x28, 0x38));
        FailIf(tokkey.State != 1 || _dialogue.IsOpen || Signal() != 0,
            "Tokkey heard the harp before Link completed his first conversation.");
        StopPlaying();
        Talk(0x2c00);
        string introduction = _dialogue.CurrentMessage;
        FailIf(!introduction.Contains("a roadblock.\n\"Echoes produce\nwaves...\"", StringComparison.Ordinal) ||
            !introduction.EndsWith("What could it\nmean?", StringComparison.Ordinal),
            "Unterminated TX_2c00 failed to continue directly into the full TX_2c01 hint.");
        FailIf(Signal() != 0 || !tokkey.BlocksGameplay,
            "TX_2c00 must block Link without setting $cfc0 bit 0 until dialogue closes.");
        int pausedCommand = tokkey.CommandIndex;
        StepRoomEventFrames(5);
        FailIf(tokkey.CommandIndex != pausedCommand, "Tokkey advanced his script during TX_2c00.");
        FinishDialogue();
        StepRoomEventFrames(1);
        FailIf(Signal() != 1 || !tokkey.BlocksGameplay,
            "Tokkey's xorcfc0bit 0 must yield before releasing input.");
        StepRoomEventFrames(2);
        FailIf(tokkey.BlocksGameplay,
            "Tokkey's genericNpcScript did not release input.");
        Talk(0x2c01, approach: false);
        FailIf(Actor().ObjectCollisionBounds.Size != new Vector2(12, 40),
            "Ages scriptCmd_initNpcHitbox must preserve Tokkey's nonzero $14/$06 radii in genericNpcScript.");
        FailIf(_dialogue.CurrentMessage.Contains("research time", StringComparison.Ordinal),
            "Repeat TX_2c01 incorrectly replayed Tokkey's introduction.");
        FinishDialogue();
        base._Process(1.0 / 60.0);
        Play(new Vector2(0x38, 0x38));
        ExpectText(0x2c05);
        FailIf(tokkey.State != 1 || _saveData.HasRoomFlag(3, 0x8f, 0x40),
            "Tokkey accepted the wrong harp tile or committed his reward early.");
        StopPlaying();
        Play(new Vector2(0x2b, 0x3d), batched: true); // Any point on source tile $32 qualifies.
        FailIf(tokkey.State != 2 || tokkey.BlocksGameplay || tokkey.ZFixed != 0 ||
            !_entities.Entities<NpcCharacter>().Any(npc => npc.Record.Id == 0x9f),
            "Tokkey recognition must create the 60-update exclamation and install, but not run, his script.");
        StepRoomEventFrames(1);
        FailIf(!tokkey.BlocksGameplay || tokkey.Counter != 60 || tokkey.ZFixed != -416 ||
            tokkey.SpeedZ != -384,
            "Tokkey's loadscript/jump must run with wait 60 and gravity $20 on the next update.");
        StepRoomEventFrames(59);
        FailIf(tokkey.Counter != 1 || _dialogue.IsOpen,
            "Tokkey's opening wait 60 completed before its zero update.");
        FailIf(_entities.Entities<NpcCharacter>().Any(npc => npc.Record.Id == 0x9f && npc.Active),
            "Tokkey's $9f exclamation outlived its 60 native state-1 updates.");
        StepRoomEventFrames(1);
        ExpectText(0x2c02);
        _dialogue.Close();

        var states = new HashSet<int>();
        bool reachedLesson = false;
        int songUpdates = 0;
        bool wasSong = false;
        for (int update = 0; update < 4000 && !_inventory.HasTreasure(TreasureDatabase.TreasureTuneOfCurrents); update++)
        {
            states.Add(tokkey.State);
            int sourceState = tokkey.State;
            byte sourceSignal = Signal();
            int randomCalls = _entities.RandomCalls;
            StepRoomEventFrames(1);
            int expectedRandomCalls = sourceState is 2 or 3 && (sourceSignal & 2) != 0 &&
                (_entities.FrameCounter & 0x0f) == 0 ? 1 : 0;
            FailIf(_entities.RandomCalls != randomCalls + expectedRandomCalls,
                $"Tokkey state ${sourceState:x2} consumed the wrong shared RNG count on frame ${_entities.FrameCounter:x2}.");
            if (_player.HarpPoseActive && !_player.IsUsingHarp) { songUpdates++; wasSong = true; }
            if (_inventory.HasTreasure(TreasureDatabase.TreasureTuneOfCurrents)) break;
            if (!_dialogue.IsOpen) continue;
            ExpectText(0x2c03);
            FailIf(!_dialogue.CurrentMessage.EndsWith("you'll return to\nthe past.", StringComparison.Ordinal) ||
                !tokkey.Database.Commands.OfType<CutsceneShowTextCommand>()
                    .Single(command => command.TextId == 0x2c03).Message.Contains("\\stop", StringComparison.Ordinal),
                "Tokkey's Tune of Currents explanation lost its page stop or final return-to-the-past paragraph.");
            FailIf(Actor().Position != new Vector2(0x37, 0x38) ||
                _player.Position != new Vector2(0x28, 0x38) || _player.FacingVector != Vector2I.Right,
                $"Tokkey dance movement or centerLinkOnTile diverged: actor={Actor().Position}, Link={_player.Position}.");
            reachedLesson = true;
            _dialogue.Close();
        }
        FailIf(!reachedLesson || !states.SetEquals(new[] { 2, 3, 4 }) || !wasSong || songUpdates != 209 ||
            !_inventory.HasTreasure(TreasureDatabase.TreasureTuneOfCurrents) || (Signal() & 0x80) == 0,
            $"Tokkey failed the full dance/response/reward: states={string.Join(',', states)}, song={songUpdates}, command={tokkey.CommandIndex}.");
        FailIf(_saveData.HasRoomFlag(3, 0x8f, 0x40),
            "Tokkey committed room flag $40 before the Tune of Currents reward dialogue completed.");
        _dialogue.Close();
        _interactions.Update(1.0 / 60.0, _player);
        StepRoomEventFrames(8);
        FailIf(tokkey.State != 1 || tokkey.BlocksGameplay || (Signal() & 3) != 0 ||
            !_saveData.HasRoomFlag(3, 0x8f, 0x40), "Tokkey failed to finish his persistent lesson and restore input.");
        Talk(0x2c04);
        _dialogue.Close();
        FailIf(!OracleSaveData.TryDeserialize(_saveData.Serialize(), out var restored) || restored is null ||
            !restored.HasRoomFlag(3, 0x8f, 0x40) ||
            !new InventoryState(_treasures, restored).HasTreasure(TreasureDatabase.TreasureTuneOfCurrents),
            "Tokkey's Tune of Currents and completion flag did not survive save serialization.");
        LoadValidationRoom(0, 0x56);
        LoadValidationRoom(3, 0x8f);
        StepRoomEventFrames(3);
        FailIf(Actor().Position != new Vector2(0x28, 0x18) || Signal() != 0,
            "Tokkey re-entry retained dance coordinates or temporary signals.");
        Talk(0x2c04);
        _dialogue.Close();
        Play(new Vector2(0x28, 0x38));
        FailIf(tokkey.State != 1, "Tokkey repeated the lesson after completion.");
        StopPlaying();

        _saveData.SetRoomFlag(3, 0x8f, 0x40, false);
        LoadValidationRoom(0, 0x56);
        LoadValidationRoom(3, 0x8f);
        StepRoomEventFrames(2);
        Talk(0x2c00);
        _dialogue.Close();
        StepRoomEventFrames(3);
        _player.WarpTo(new Vector2(0x28, 0x38));
        _player.StartHarpActionForValidation();
        base._Process(120.0 / 60.0);
        StopPlaying(); // Cancelling an item must not publish natural completion.
        base._Process(1.0 / 60.0);
        FailIf(tokkey.State != 1 || _entities.PlayingInstrumentSource() != 0 || _dialogue.IsOpen,
            "Cancelling Echoes midway through playback falsely triggered Tokkey's lesson.");
        Play(new Vector2(0x28, 0x38));
        StepRoomEventFrames(2);
        LoadValidationRoom(0, 0x56);
        FailIf(tokkey.HasState || _player.CutsceneControlled || _player.HarpPoseActive ||
            _saveData.HasRoomFlag(3, 0x8f, 0x40), "Cancelling Tokkey leaked input, effects, or completion state.");
        LoadValidationRoom(3, 0x8f);
        StepRoomEventFrames(2);
        Talk(0x2c00);
        _dialogue.Close();
        GD.Print("Validated room 3:8f Tokkey: repeated A-button dialogue across the table, preserved Ages hitbox, complete positioned dialogue, full 260-update harp playback before recognition in single/batched frames, native dance, Currents reward, persistence, and cancellation.");
    }
}
