using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateDekuForestSoldierCutscene()
    {
        const int group = 1;
        const int room = 0x81;
        const int mysterySeeds = 0x24;
        DekuForestSoldierEvent roomEvent =
            _roomEvents.DekuForestSoldier;
        DekuForestSoldierEventRecord record =
            new DekuForestSoldierEventDatabase().Record;

        _saveData.SetRoomFlag(
            group, room, OracleSaveData.RoomFlag40, value: false);
        _inventory.GiveTreasure(mysterySeeds, 0);
        var trace = new ValidationCutsceneTrace();
        _roomEvents.CommandTraceSink = trace;
        bool LastTraceIs(
            int command,
            CutsceneCommandTracePhase phase,
            int counter)
        {
            if (trace.Entries.Count == 0)
                return false;
            CutsceneCommandTraceEntry entry = trace.Entries[^1];
            return entry.Source.CommandIndex == command &&
                entry.Phase == phase &&
                entry.Counter == counter;
        }
        int clinks = _sound.PlayRequestsFor(OracleSoundEngine.SndClink);
        LoadValidationRoom(group, room);

        NpcCharacter soldier = _entities.Entities<NpcCharacter>().Single(npc =>
            npc.Record.Id == record.InteractionId &&
            npc.Record.SubId == record.SubId);
        FailIf(
            record is not
            {
                Group: group,
                Room: room,
                TriggerTreasure: mysterySeeds,
                RoomFlag: OracleSaveData.RoomFlag40,
                TriggerY: 0x2a,
                InitialY: 0x68,
                InitialX: 0xf0,
                Palette: 2,
                InitialAnimation: 2,
                SlowSpeed: 0x1e,
                FastSpeed: 0x3c,
                EffectFrames: 0x28,
                DestinationGroup: 1,
                DestinationRoom: 0x46,
                DestinationPosition: 0x34,
                DestinationParameter: 0,
                SourceTransition: 0,
                DestinationTransition: 3,
                TextId: 0x590b
            } ||
            soldier.Record is not
            {
                Id: 0x40,
                SubId: 0x0a,
                Y: 0x68,
                X: 0xf0,
                SpriteName: "spr_soldier",
                Palette: 2,
                DefaultAnimation: 2,
                CanFace: false,
                Implementation: NpcImplementationClassification.EventOwned
            } ||
            soldier.Position != new Vector2(0xf0, 0x68) ||
            soldier.Visible ||
            soldier.AnimationRate != 0.0f ||
            !roomEvent.HasState ||
            roomEvent.BlocksGameplay ||
            _roomEvents.Active ||
            trace.Entries.Count != 0,
            "Room 1:81 did not allocate the hidden red soldier $40:$0a " +
            "at $68/$f0 from the Mystery Seeds room-specific predicate.");

        _player.WarpTo(new Vector2(0x58, 0x2b), recordSafe: false);
        StepRoomEventFrames(1);
        FailIf(
            !LastTraceIs(0, CutsceneCommandTracePhase.Updated, 0) ||
            soldier.Visible ||
            roomEvent.BlocksGameplay,
            "soldierSubid0aScript passed its exact w1Link.yh=$2a gate early.");

        _player.WarpTo(new Vector2(0x58, 0x2a), recordSafe: false);
        StepRoomEventFrames(1);
        FailIf(
            !LastTraceIs(0, CutsceneCommandTracePhase.Completed, 0) ||
            soldier.Visible ||
            roomEvent.BlocksGameplay,
            "The matching Link Y update did not yield after checkmemoryeq.");

        StepRoomEventFrames(1);
        FailIf(
            !LastTraceIs(5, CutsceneCommandTracePhase.Updated, 30) ||
            !soldier.Visible ||
            !roomEvent.BlocksGameplay ||
            !_roomEvents.Active ||
            !_roomEvents.MenusDisabled ||
            !_player.CutsceneControlled ||
            soldier.Position != new Vector2(0xf0, 0x68),
            "The soldier did not reveal, drop Link's held item, write " +
            "wDisabledObjects=$01, disable the menu, and install wait 30 " +
            "in one carry-through script update.");

        StepRoomEventFrames(29);
        FailIf(
            !LastTraceIs(5, CutsceneCommandTracePhase.Updated, 1) ||
            soldier.Position != new Vector2(0xf0, 0x68),
            "The initial soldier wait completed before its original 30 updates.");
        StepRoomEventFrames(1);
        FailIf(
            !LastTraceIs(6, CutsceneCommandTracePhase.Completed, 0) ||
            soldier.Position != new Vector2(0xf0, 0x68),
            "wait 30 did not carry into the yielding SPEED_0c0 command.");

        StepRoomEventFrames(1);
        FailIf(
            !LastTraceIs(7, CutsceneCommandTracePhase.Updated, 0x4b) ||
            soldier.CurrentScriptAnimationSource != record.Animation1 ||
            soldier.Position != new Vector2(0xf0, 0x68),
            "moveright $4b did not install its counter and animation $01 " +
            "without moving on the command update.");
        StepRoomEventFrames(74);
        FailIf(
            !LastTraceIs(7, CutsceneCommandTracePhase.Updated, 1) ||
            soldier.Position != new Vector2(0x27, 0x68),
            "SPEED_0c0 did not wrap the soldier's unsigned X word from " +
            "$f0 to high byte $27 over 74 objectApplySpeed calls.");
        StepRoomEventFrames(1);
        FailIf(
            !LastTraceIs(7, CutsceneCommandTracePhase.Completed, 0) ||
            soldier.Position != new Vector2(0x27, 0x68),
            "moveright $4b moved on its counter-zero yield update.");

        int effectGuard = 0;
        while (!LastTraceIs(
                12, CutsceneCommandTracePhase.Updated, 60) &&
            effectGuard++ < 80)
        {
            StepRoomEventFrames(1);
        }
        NpcCharacter? exclamation =
            _entities.Entities<NpcCharacter>().SingleOrDefault(npc =>
                npc.Record.Id == record.EffectId && npc.Active);
        FailIf(
            effectGuard >= 80 ||
            exclamation is null ||
            exclamation.Record is not
            {
                Id: 0x9f,
                SubId: 0,
                Y: 0x5b,
                X: 0x27,
                SpriteName: "spr_zz_bubble_exclamation_heart_kid",
                TileBase: 8,
                Palette: 5,
                Implementation: NpcImplementationClassification.EventOwned
            } ||
            exclamation.Position != new Vector2(0x27, 0x5b) ||
            exclamation.AnimationRate != 0.0f ||
            soldier.CurrentScriptAnimationSource != record.Animation0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndClink) != clinks + 1,
            "The soldier reaction did not select animation $00, create " +
            "INTERAC_EXCLAMATION_MARK at offset -13/0, play SND_CLINK, " +
            "and install wait 60.");

        StepRoomEventFrames(record.EffectFrames - 1);
        FailIf(
            !_entities.Entities<NpcCharacter>().Any(npc =>
                npc.Record.Id == record.EffectId && npc.Active),
            "The room 1:81 exclamation mark expired before 40 updates.");
        StepRoomEventFrames(1);
        FailIf(
            _entities.Entities<NpcCharacter>().Any(npc =>
                npc.Record.Id == record.EffectId && npc.Active) ||
            !LastTraceIs(12, CutsceneCommandTracePhase.Updated, 20),
            "The room 1:81 exclamation mark did not delete on its 40th " +
            "update while the enclosing wait 60 retained 20 updates.");

        StepRoomEventFrames(20);
        FailIf(
            !LastTraceIs(13, CutsceneCommandTracePhase.Completed, 0) ||
            soldier.Position != new Vector2(0x27, 0x68),
            "The reaction wait did not carry into SPEED_180 at its exact boundary.");
        StepRoomEventFrames(1);
        FailIf(
            !LastTraceIs(14, CutsceneCommandTracePhase.Updated, 0x1e) ||
            soldier.Position != new Vector2(0x27, 0x68),
            "moveup $1e did not install its counter without moving.");
        StepRoomEventFrames(29);
        FailIf(
            !LastTraceIs(14, CutsceneCommandTracePhase.Updated, 1) ||
            soldier.Position != new Vector2(0x27, 0x3c),
            "SPEED_180 did not move the soldier upward 29 times with " +
            "unsigned 8.8 precision.");
        StepRoomEventFrames(1);
        FailIf(
            !LastTraceIs(14, CutsceneCommandTracePhase.Completed, 0) ||
            soldier.Position != new Vector2(0x27, 0x3c),
            "moveup $1e moved on its counter-zero yield update.");

        StepRoomEventFrames(31);
        FailIf(
            !_dialogue.IsOpen ||
            !LastTraceIs(16, CutsceneCommandTracePhase.Completed, 0) ||
            !DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                "Queen Ambi", System.StringComparison.Ordinal) ||
            !DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                "Mystery Seeds", System.StringComparison.Ordinal),
            "The final wait did not open imported TX_590b on its exact update.");

        int dialogueAnimationFrame = soldier.CurrentAnimationFrame;
        int dialogueTraceEntries = trace.Entries.Count;
        StepRoomEventFrames(17);
        FailIf(
            !_dialogue.IsOpen ||
            trace.Entries.Count != dialogueTraceEntries ||
            soldier.CurrentAnimationFrame == dialogueAnimationFrame,
            "The enabled-bit-7 soldier did not keep animating while " +
            "interactionRunScript remained paused by wTextIsActive.");

        _dialogue.Close();
        StepRoomEventFrames(30);
        FailIf(
            _dialogue.IsOpen ||
            _saveData.HasRoomFlag(
                group, room, OracleSaveData.RoomFlag40) ||
            _rooms.CurrentRoom.Id != room ||
            !LastTraceIs(17, CutsceneCommandTracePhase.Updated, 1),
            "The soldier did not preserve the post-text wait 30 boundary " +
            "after TX_590b closed " +
            $"(dialogue={_dialogue.IsOpen}, " +
            $"flag={_saveData.HasRoomFlag(group, room, OracleSaveData.RoomFlag40)}, " +
            $"room={_rooms.ActiveGroup:x}:{_rooms.CurrentRoom.Id:x2}, " +
            $"trace={trace.Entries[^1]}).");
        StepRoomEventFrames(1);
        FailIf(
            !_saveData.HasRoomFlag(
                group, room, OracleSaveData.RoomFlag40) ||
            _rooms.CurrentRoom.Id != room ||
            !LastTraceIs(18, CutsceneCommandTracePhase.Completed, 0),
            "orroomflag $40 did not yield one update before scriptend.");
        StepRoomEventFrames(1);
        FailIf(
            _rooms.ActiveGroup != record.DestinationGroup ||
            _rooms.CurrentRoom.Id != record.DestinationRoom ||
            _player.Position != new Vector2(0x48, 0x38) ||
            _player.FacingVector != Vector2I.Up ||
            _dialogue.IsOpen ||
            roomEvent.HasState,
            "soldierSubid0a did not install its hardcoded transition " +
            "$00 to room 1:46 position $34 with transition $03.");

        List<CutsceneCommandTraceEntry> starts = trace.Entries
            .Where(entry =>
                entry.Phase == CutsceneCommandTracePhase.Started)
            .ToList();
        FailIf(
            starts.Count != 20 ||
            starts.Select(entry => entry.Source.CommandIndex)
                .SequenceEqual(Enumerable.Range(0, 20)) is false ||
            starts.Any(entry =>
                entry.Source.Script != "soldierSubid0aScript" ||
                entry.Source.SourceLine <= 0),
            "The room 1:81 typed command trace lost source lines or command order.");

        UpdateRoomWarpTransition(
            RoomTransitionController.WarpFadeFrames / 60.0);
        LoadValidationRoom(group, room);
        FailIf(
            _entities.Entities<NpcCharacter>().Any(npc =>
                npc.Record.Id == record.InteractionId &&
                npc.Record.SubId == record.SubId) ||
            roomEvent.HasState ||
            roomEvent.BlocksGameplay,
            "Room flag $40 did not suppress the dynamic soldier on re-entry.");

        _roomEvents.CommandTraceSink = null;
        GD.Print(
            "Validated room 1:81 Mystery Seeds soldier $40:$0a: room-specific " +
            "predicate, exact Link-Y gate, hidden red OAM, carried native " +
            "setup, 30/6/20/60/30/30 waits, wrapping SPEED_0c0 entrance, " +
            "40-update exclamation/SND_CLINK, doubled SPEED_180 animation " +
            "cadence, TX_590b, room flag $40, and hardcoded warp to 1:46.");
    }
}
