using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom06aRickyGloves()
    {
        RickyGlovesEvent rickyEvent = _roomEvents.RickyGloves;
        RickyGlovesEventDatabase database = rickyEvent.Database;
        RickyGlovesEventRecord record = database.Record;
        bool originalImpaCompleted = _saveData.HasRoomFlag(
            0, 0x6a, OracleSaveData.RoomFlag40);

        void ClearCompanionSlot()
        {
            if (CompanionRuntimeState.AnyActive(_runtimeState))
            {
                ActiveCompanion active =
                    CompanionRuntimeState.Read(_runtimeState);
                CompanionRuntimeState.Clear(_runtimeState, active.Id);
            }
            CompanionRuntimeState.ForgetRemembered(_runtimeState);
        }

        void Configure(bool prerequisite, int state, bool gloves)
        {
            _dialogue.Close();
            ClearCompanionSlot();
            _saveData.SetGlobalFlag(
                record.PrerequisiteGlobalFlag,
                prerequisite);
            _saveData.WriteWramByte(
                record.RickyStateAddress,
                checked((byte)state));
            _inventory.LoseTreasure(record.GlovesTreasure);
            if (gloves)
                _inventory.GiveTreasure(record.GlovesTreasure, 0);
        }

        void PositionForTalk()
        {
            _player.WarpTo(new Vector2(record.RickyX, record.RickyY + 10));
            _player.Face(Vector2I.Up);
        }

        void ExpectDialogue(int commandIndex, int textId, string phase)
        {
            var command = (CutsceneShowTextCommand)database.Commands[commandIndex];
            FailIf(
                !_dialogue.IsOpen ||
                _dialogue.CurrentMessage != DialogueBox.PlainText(command.Message),
                $"Ricky {phase} did not display imported TX_{textId:x4}.");
        }

        Configure(prerequisite: false, state: 0, gloves: false);
        LoadValidationRoom(record.Group, record.Room);
        FailIf(
            rickyEvent.HasState || rickyEvent.RickyActor is not null ||
            _entities.Entities<RickyCompanionRoomEntity>().Any(),
            "Room 0:6a spawned $67:$02 Ricky before " +
            "GLOBALFLAG_GAVE_ROPE_TO_RAFTON `$15.");

        Configure(
            prerequisite: true,
            state: record.CompleteMask,
            gloves: false);
        LoadValidationRoom(record.Group, record.Room);
        FailIf(
            rickyEvent.HasState || rickyEvent.RickyActor is not null ||
            _entities.Entities<RickyCompanionRoomEntity>().Any(),
            "wRickyState bit `$20 did not suppress room 0:6a Ricky.");

        Configure(
            prerequisite: true,
            state: record.LeftMask,
            gloves: false);
        LoadValidationRoom(record.Group, record.Room);
        FailIf(
            rickyEvent.HasState || rickyEvent.RickyActor is not null ||
            _entities.Entities<RickyCompanionRoomEntity>().Any(),
            "wRickyState bit `$40 did not suppress room 0:6a Ricky.");

        Configure(prerequisite: true, state: 0, gloves: false);
        CompanionRuntimeState.Begin(
            _runtimeState,
            CompanionRuntimeState.MooshId,
            record.Room,
            new Vector2(0x30, 0x30),
            direction: 2);
        LoadValidationRoom(record.Group, record.Room);
        FailIf(
            rickyEvent.HasState || rickyEvent.RickyActor is not null ||
            _entities.Entities<RickyCompanionRoomEntity>().Any(),
            "Room 0:6a overwrote an already-enabled w1Companion slot.");

        Configure(prerequisite: true, state: 0, gloves: false);
        _saveData.SetRoomFlag(0, 0x6a, OracleSaveData.RoomFlag40);
        const int leftNeighbor = 0x69;
        LoadValidationRoom(record.Group, leftNeighbor);
        if (!_rooms.TryGetNeighbor(Vector2I.Right, out int scrollTarget) ||
            scrollTarget != record.Room)
        {
            throw new System.InvalidOperationException(
                "Room 0:69 did not resolve room 0:6a as its source-derived " +
                "right neighbor.");
        }
        _transitions.BeginScroll(_player, Vector2I.Right, scrollTarget);
        NpcCharacter? incomingRicky = rickyEvent.RickyActor;
        FailIf(
            incomingRicky is null || !incomingRicky.Active ||
            !incomingRicky.Visible || !rickyEvent.ButtonSensitive ||
            rickyEvent.CurrentCommandIndex != 0 ||
            incomingRicky.TransitionDrawOffset != new Vector2(160, 0),
            "Room 0:6a destination preload did not resolve Ricky's two " +
            "state-0 updates into a visible incoming-scroll presentation.");
        ulong preloadedFrameHash = incomingRicky!.CurrentAnimationPixelHash;
        for (int update = 0; update < 10; update++)
        {
            UpdateScrollingTransition(1.0 / 60.0);
            _entities.Update(1.0 / 60.0, _player);
            _roomEvents.Update(1.0 / 60.0);
        }
        FailIf(
            !incomingRicky.Active || !incomingRicky.Visible ||
            incomingRicky.CurrentAnimationFrame != 0 ||
            incomingRicky.CurrentAnimationPixelHash != preloadedFrameHash ||
            rickyEvent.CurrentCommandIndex != 0,
            "Preloaded Ricky disappeared, animated, or advanced his script " +
            "while destination objects were frozen during scrolling.");
        FinishActiveScrollingTransitionWithRoomEventsForValidation();
        FailIf(
            !incomingRicky.Active || !incomingRicky.Visible ||
            incomingRicky.TransitionDrawOffset != Vector2.Zero ||
            rickyEvent.CurrentCommandIndex != 0,
            "Ricky did not retain his preloaded visible presentation at the " +
            "room 0:6a scroll handoff.");

        Configure(prerequisite: true, state: 0, gloves: false);
        // This later story state is only reachable after the possessed-Impa
        // encounter. Ricky's higher-priority room controller must not bypass
        // suppression of the completed placed $31:$00 actor.
        _saveData.SetRoomFlag(0, 0x6a, OracleSaveData.RoomFlag40);
        FailIf(
            _inventory.AnimalCompanion != 0,
            "Ricky's first-meeting validation did not begin with " +
            "wAnimalCompanion `$00.");
        CompanionRuntimeState.Remember(
            _runtimeState,
            CompanionRuntimeState.RickyId,
            record.Group,
            record.Room,
            new Vector2(0x22, 0x33));
        LoadValidationRoom(record.Group, record.Room);

        NpcCharacter? stagedRicky = rickyEvent.RickyActor;
        FailIf(
            stagedRicky is null,
            "Room 0:6a did not allocate the fixed Ricky special object.");
        NpcCharacter ricky = stagedRicky!;
        RememberedCompanion remembered =
            CompanionRuntimeState.ReadRemembered(_runtimeState);
        FailIf(
            ricky.Position != new Vector2(record.RickyX, record.RickyY) ||
            ricky.Record.SpriteName != database.Visual.Sprite ||
            ricky.Record.Palette != database.Visual.Palette ||
            !rickyEvent.HasState || rickyEvent.CurrentCommandIndex != 0 ||
            rickyEvent.ButtonSensitive || ricky.Active || ricky.Visible ||
            _entities.Entities<RickyCompanionRoomEntity>().Any() ||
            remembered.Id != 0 || remembered.Group != record.Group ||
            remembered.Room != record.Room || remembered.Y != 0x33 ||
            remembered.X != 0x22 ||
            _entities.Entities<NpcCharacter>().Any(npc =>
                npc.Record.Id == 0x31 && npc.Record.SubId == 0x00 &&
                npc.Active),
            "Room 0:6a did not preserve $71:$03/$67:$02 source ownership, " +
            "Ricky's fixed `$40,$50 preset, initial special-object visual, " +
            "the spawner's ID-only remembered-companion clear, or completed " +
            "Impa suppression.");

        StepRoomEventFrames(1);
        FailIf(
            ricky.Active || ricky.Visible || rickyEvent.ButtonSensitive ||
            rickyEvent.CurrentCommandIndex != 0,
            "SPECIALOBJECT_RICKY skipped companionCheckCanSpawn's first " +
            "state-0/substate-0 return.");
        StepRoomEventFrames(1);
        FailIf(
            !ricky.Active || !ricky.Visible || !rickyEvent.ButtonSensitive ||
            rickyEvent.CurrentCommandIndex != 0 ||
            ricky.Record.Palette != 3 ||
            ricky.CurrentAnimationTextureSize != new Vector2I(24, 32) ||
            ricky.CurrentAnimationOffset != new Vector2(-12, -24),
            "SPECIALOBJECT_RICKY did not become visible and A-sensitive on " +
            "the second source initialization update with source palette 3 " +
            "and the 24x32 five-cell idle OAM frame " +
            $"(active={ricky.Active}, visible={ricky.Visible}, " +
            $"sensitive={rickyEvent.ButtonSensitive}, " +
            $"command={rickyEvent.CurrentCommandIndex}, " +
            $"palette={ricky.Record.Palette}, " +
            $"size={ricky.CurrentAnimationTextureSize}, " +
            $"offset={ricky.CurrentAnimationOffset}, " +
            $"hash=${ricky.CurrentAnimationPixelHash:x16}).");

        ulong firstIdleFrame = ricky.CurrentAnimationPixelHash;
        StepRoomEventFrames(19);
        FailIf(
            ricky.CurrentAnimationFrame != 0 ||
            ricky.CurrentAnimationPixelHash != firstIdleFrame,
            "Ricky's initial 20-update idle frame ended early.");
        StepRoomEventFrames(1);
        FailIf(
            ricky.CurrentAnimationFrame != 1 ||
            ricky.CurrentAnimationPixelHash == firstIdleFrame,
            "Ricky's initial 20-update idle frame did not advance to the " +
            "imported second OAM pose.");

        PositionForTalk();
        FailIf(
            _entities.FindTalkTarget(_player) != ricky ||
            !_interactions.TryInteract(_player),
            "Room 0:6a Ricky was not reachable through normal A-button routing.");
        StepRoomEventFrames(1);
        ExpectDialogue(5, 0x2000, "first meeting");
        FailIf(
            (_saveData.ReadWramByte(record.RickyStateAddress) &
                record.TalkedMask) == 0 ||
            !rickyEvent.BlocksGameplay || !_player.CutsceneControlled ||
            rickyEvent.CurrentCommandIndex != 6 ||
            _dialogue.CurrentMessage.Contains(
                "\\jump", System.StringComparison.Ordinal),
            "Ricky's first talk did not set wRickyState bit `$01, retain " +
            "the input lease, or expand TX_2000's TX_2002 jump " +
            $"(state=${_saveData.ReadWramByte(record.RickyStateAddress):x2}, " +
            $"blocks={rickyEvent.BlocksGameplay}, " +
            $"cutscene={_player.CutsceneControlled}, " +
            $"command={rickyEvent.CurrentCommandIndex}, " +
            $"raw-jump={_dialogue.CurrentMessage.Contains("\\jump", System.StringComparison.Ordinal)}).");

        _dialogue.Close();
        StepRoomEventFrames(1);
        ExpectDialogue(9, 0x2003, "missing-gloves explanation");
        _dialogue.Close();
        StepRoomEventFrames(1);
        FailIf(
            rickyEvent.CurrentCommandIndex != 0 ||
            !rickyEvent.ButtonSensitive || rickyEvent.BlocksGameplay ||
            _player.CutsceneControlled || !ricky.Active,
            "Ricky's no-gloves path did not reset var3d, enable input, and " +
            "return to checkabutton.");

        PositionForTalk();
        FailIf(!_interactions.TryInteract(_player),
            "Ricky rejected his repeat no-gloves interaction.");
        StepRoomEventFrames(1);
        ExpectDialogue(9, 0x2003, "repeat missing-gloves explanation");
        _dialogue.Close();
        StepRoomEventFrames(1);

        _inventory.GiveTreasure(record.GlovesTreasure, 0);
        PositionForTalk();
        FailIf(!_interactions.TryInteract(_player),
            "Ricky rejected the retrieved-gloves interaction.");
        StepRoomEventFrames(1);
        ExpectDialogue(13, 0x2004, "glove return");
        FailIf(
            !_inventory.HasTreasure(record.GlovesTreasure) ||
            !rickyEvent.BlocksGameplay,
            "Ricky removed treasure `$48 before TX_2004 closed.");

        _dialogue.Close();
        StepRoomEventFrames(1);
        RickyCompanionRoomEntity companion =
            _entities.Entities<RickyCompanionRoomEntity>().Single();
        FailIf(
            _inventory.HasTreasure(record.GlovesTreasure) || ricky.Active ||
            companion.Phase != RickyCompanionPhase.Mounting ||
            rickyEvent.CurrentCommandIndex != 17 ||
            rickyEvent.BlocksGameplay || _player.CutsceneControlled ||
            !rickyEvent.MenusDisabled || !_roomEvents.MenusDisabled,
            "Closing TX_2004 did not lose treasure `$48, transfer the fixed " +
            "actor to SPECIALOBJECT_RICKY, clear wDisabledObjects, retain " +
            "wMenuDisabled, and wait for Link's mount.");

        for (int frame = 0; frame < 120 && !_dialogue.IsOpen; frame++)
        {
            _player._PhysicsProcess(1.0 / 60.0);
            StepRoomEventFrames(1);
        }
        ExpectDialogue(18, 0x2005, "mounted tutorial");
        FailIf(
            companion.Phase != RickyCompanionPhase.Riding ||
            !companion.LinkRiding || !_player.CompanionRideActive ||
            !CompanionRuntimeState.IsActive(
                _runtimeState, CompanionRuntimeState.RickyId) ||
            companion.AnimationIndex != 0x22 ||
            rickyEvent.CurrentCommandIndex != 19 ||
            !rickyEvent.MenusDisabled || !_roomEvents.MenusDisabled ||
            (_saveData.ReadWramByte(record.RickyStateAddress) &
                record.CompleteMask) != 0,
            "Ricky's force-mount gate did not finish state `$03 into the " +
            "down-facing `$22 ride pose before TX_2005, or set completion early.");

        _dialogue.Close();
        StepRoomEventFrames(1);
        FailIf(
            (_saveData.ReadWramByte(record.RickyStateAddress) &
                record.CompleteMask) == 0 ||
            rickyEvent.HasState || rickyEvent.MenusDisabled ||
            _roomEvents.MenusDisabled ||
            !CompanionRuntimeState.IsActive(
                _runtimeState, CompanionRuntimeState.RickyId),
            "Closing TX_2005 did not set wRickyState bit `$20, enable menus, " +
            "end $71:$03, and retain live Ricky companion ownership.");

        ClearCompanionSlot();
        LoadValidationRoom(record.Group, record.Room);
        FailIf(
            rickyEvent.HasState || rickyEvent.RickyActor is not null ||
            _entities.Entities<RickyCompanionRoomEntity>().Any(),
            "Room 0:6a replayed Ricky's glove interaction after persistent " +
            "wRickyState bit `$20.");

        const int fluteTreasure = 0x0e;
        Configure(prerequisite: true, state: 0, gloves: false);
        _inventory.GiveTreasure(
            fluteTreasure,
            record.AnimalCompanionId);
        LoadValidationRoom(record.Group, record.Room);
        StepRoomEventFrames(record.InitialSpecialObjectUpdates);
        NpcCharacter? returningRicky = rickyEvent.RickyActor;
        FailIf(
            returningRicky is null || !returningRicky.Active ||
            _inventory.AnimalCompanion != record.AnimalCompanionId,
            "Ricky's returning-companion setup did not bind wAnimalCompanion `$0b.");
        PositionForTalk();
        FailIf(
            _entities.FindTalkTarget(_player) != returningRicky ||
            !_interactions.TryInteract(_player),
            "Returning-companion Ricky was not A-sensitive.");
        StepRoomEventFrames(1);
        ExpectDialogue(7, 0x2001, "returning-companion greeting");
        FailIf(
            (_saveData.ReadWramByte(record.RickyStateAddress) &
                record.TalkedMask) == 0 ||
            rickyEvent.CurrentCommandIndex != 8,
            "wAnimalCompanion `$0b did not select @notFirstMeeting after " +
            "setting wRickyState bit `$01.");
        _dialogue.Close();
        StepRoomEventFrames(1);
        ExpectDialogue(9, 0x2003, "returning-companion missing-gloves explanation");
        _dialogue.Close();
        StepRoomEventFrames(1);

        _saveData.SetRoomFlag(
            0, 0x6a, OracleSaveData.RoomFlag40, originalImpaCompleted);

        GD.Print(
            "Validated room 0:6a $71:$03/$67:$02 Ricky interaction: global/" +
            "state/occupied-slot predicates, fixed preset, ID-only remembered " +
            "clear, visible frozen destination preload, two-update direct " +
            "initialization, first and repeat no-gloves " +
            "branches, TX_2000 jump expansion, " +
            "treasure `$48 removal, force mount, TX_2005 ordering, live Ricky " +
            "ownership, menu release, persistent bit `$20 suppression, " +
            "source palette/OAM geometry, and completed-Impa suppression.");
    }
}
