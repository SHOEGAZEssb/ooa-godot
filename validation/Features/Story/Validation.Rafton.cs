using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRaft()
    {
        var database = new RaftDatabase();
        RaftBehavior behavior = database.Behavior;
        FailIf(
            database.GetPlacements(1, 0xa7).Single() is not
                { SubId: 1, Y: 0x58, X: 0x78 } ||
            database.GetPlacements(1, 0xa9).Single() is not
                { SubId: 0, Y: 0x38, X: 0x78 } ||
            behavior.DismountCollision != 0x18 ||
            behavior.ValidTiles is not [0xfc, 0xe0, 0xe1, 0xe2, 0xe3, 0xfa, 0xe9],
            "INTERAC_RAFT placement or valid-water order changed.");

        OracleRoomData oceanRoom = _world.LoadRoom(1, 0xa8);
        Vector2 deepWater = default;
        bool foundDeepWater = false;
        for (int y = 0; y < oceanRoom.HeightInTiles && !foundDeepWater; y++)
        for (int x = 0; x < oceanRoom.WidthInTiles; x++)
        {
            Vector2 candidate = new(x * 16 + 8, y * 16 + 8);
            if (oceanRoom.GetTerrainInfo(candidate).Collision != 0x10 ||
                oceanRoom.IsSolid(candidate))
                continue;
            deepWater = candidate;
            foundDeepWater = true;
            break;
        }
        FailIf(!foundDeepWater,
            "Room 1:a8 no longer exposes a non-solid collision-$10 ocean tile.");
        var oceanRaft = new RaftRoomEntity(
            new RaftSpawn(deepWater, 0, 1, 0xa8, Riding: true),
            oceanRoom, behavior, _entities.RuntimeState);
        FailIf(
            oceanRaft.CanDismountAt(deepWater),
            "SPECIALOBJECT_RAFT allowed a dismount onto collision-$10 ocean water.");
        oceanRaft.QueueFree();

        CompanionRuntimeState.Clear(
            _entities.RuntimeState, CompanionRuntimeState.RaftId);
        CompanionRuntimeState.ForgetRemembered(_entities.RuntimeState);
        LoadValidationRoom(1, 0xa8);
        FailIf(
            _entities.Entities<RaftRoomEntity>().Count != 0,
            "Room 1:a8 unexpectedly contained a raft during forced-walk validation.");
        _player.WarpTo(deepWater, recordSafe: false);
        _player.BeginForcedRoomEntryMovement(Vector2I.Up);
        _player.AdvanceApplicationUpdate();
        FailIf(
            _player.IsDrowning,
            "LINK_STATE_FORCE_MOVEMENT entered normal ocean drowning before " +
            "the raft could advance Link onto land.");
        _player.EndForcedRoomEntryMovement();

        _saveData.SetGlobalFlag(behavior.ChangedRoomsFlag, value: false);
        LoadValidationRoom(1, 0xa7);
        FailIf(
            _entities.Entities<RaftRoomEntity>().Count != 0,
            "Room 1:a7 spawned subid $01 before Rafton changed rooms.");

        _saveData.SetGlobalFlag(behavior.ChangedRoomsFlag);
        LoadValidationRoom(1, 0xa7);
        RaftRoomEntity raft = _entities.Entities<RaftRoomEntity>().Single();
        FailIf(
            raft.AnimationIndex != 0,
            "INTERAC_RAFT did not initialize direction&1 animation $00.");

        _player.WarpTo(new Vector2(0x78, 0x53), recordSafe: false);
        _entities.Update(1.0 / 60.0, _player);
        FailIf(
            !raft.LinkRiding || !_player.RaftRideActive ||
            !CompanionRuntimeState.IsActive(
                _entities.RuntimeState, CompanionRuntimeState.RaftId),
            "INTERAC_RAFT did not promote to SPECIALOBJECT_RAFT $13.");
        RememberedCompanion remembered =
            CompanionRuntimeState.ReadRemembered(_entities.RuntimeState);
        FailIf(
            remembered is not { Id: 0x13, Group: 1, Room: 0xa7, Y: 0x58, X: 0x78 },
            "SPECIALOBJECT_RAFT did not save its initial local respawn and " +
            "remembered companion position.");

        Vector2 expected = raft.PrecisePosition;
        OracleObjectMovement.Shared.ApplySpeed(
            ref expected, behavior.Speed, 0x18);
        Input.ActionPress("move_left");
        _entities.Update(1.0 / 60.0, _player);
        Input.ActionRelease("move_left");
        FailIf(
            !raft.PrecisePosition.IsEqualApprox(expected) ||
            raft.Direction != 3 || raft.AnimationIndex != 1,
            "SPECIALOBJECT_RAFT did not apply SPEED_e0 left movement and " +
            "select direction&1 animation $01.");

        // Destination object parsing occurs while the outgoing mounted raft
        // still owns w1Companion. Its active room byte therefore differs from
        // the destination, but both placed $e6 subids must still delete.
        CompanionRuntimeState.Update(
            _entities.RuntimeState, CompanionRuntimeState.RaftId, 0xa8,
            raft.PrecisePosition, raft.Direction);
        _entities.Clear();
        _player.EndRaftRide(_player.PrecisePosition, 0);
        LoadValidationRoom(1, 0xa7);
        FailIf(
            _entities.Entities<RaftRoomEntity>().Count != 0,
            "Room 1:a7 recreated its placed raft while an outgoing " +
            "SPECIALOBJECT_RAFT still owned w1Companion.");

        CompanionRuntimeState.Clear(
            _entities.RuntimeState, CompanionRuntimeState.RaftId);
        LoadValidationRoom(1, 0xa7);
        FailIf(
            _entities.Entities<RaftRoomEntity>().Single().LinkRiding,
            "Remembered raft incorrectly reloaded as an active mounted object.");

        _saveData.WriteWramByte(behavior.DimitriStateAddress, 0);
        LoadValidationRoom(1, 0xa9);
        FailIf(
            _entities.Entities<RaftRoomEntity>().Count != 0,
            "Room 1:a9 spawned subid $00 without wDimitriState bit 6.");
        _saveData.WriteWramByte(
            behavior.DimitriStateAddress, (byte)behavior.DimitriMask);
        LoadValidationRoom(1, 0xa9);
        FailIf(
            _entities.Entities<RaftRoomEntity>().Count != 1,
            "Room 1:a9 did not spawn subid $00 with wDimitriState bit 6.");
    }

    private void ValidateRooms21eAnd21fRafton()
    {
        const int group = 2;
        const int leftRoom = 0x1e;
        const int rightRoom = 0x1f;
        const int essencesAddress = 0xc6bf;
        const int tradeItemAddress = 0xc6c0;
        const int obtainedTreasureBase = 0xc69a;

        RaftonEvent raftonEvent = _roomEvents.Rafton;
        RaftonEventDatabase database = raftonEvent.Database;
        RaftonEventRecord record = database.Record;
        const string firstRightRoomDialogue =
            "Shove off from\nover there.\nThe raft awaits!\n" +
            "Climb on top and\npress \\col(0x84)\\item(0x00)\\col(0) to\nmove!";
        FailIf(
            database.DialogueText(0x2708) != firstRightRoomDialogue,
            "Rafton TX_2708 did not preserve its source fallthrough into TX_2709.");
        byte originalLeftFlags = _saveData.GetRoomFlags(group, leftRoom);
        byte originalRightFlags = _saveData.GetRoomFlags(group, rightRoom);
        bool originalGaveRope = _saveData.HasGlobalFlag(record.GaveRopeFlag);
        bool originalChangedRooms =
            _saveData.HasGlobalFlag(record.ChangedRoomsFlag);
        var inventorySnapshot = new byte[0x39];
        _saveData.ReadWramBytes(0xc688, inventorySnapshot);
        MethodInfo? reloadInventory = typeof(InventoryState).GetMethod(
            "LoadFromSaveData",
            BindingFlags.Instance | BindingFlags.NonPublic);
        FailIf(reloadInventory is null, "Could not reload Rafton validation inventory.");

        void SetTreasureFlag(int treasure, bool obtained)
        {
            int address = obtainedTreasureBase + (treasure >> 3);
            int mask = 1 << (treasure & 7);
            byte flags = _saveData.ReadWramByte(address);
            _saveData.WriteWramByte(address, obtained
                ? (byte)(flags | mask)
                : (byte)(flags & ~mask));
        }

        void Configure(
            bool d2 = false,
            bool d3 = false,
            bool chevalRope = false,
            bool islandChart = false,
            bool gaveRope = false,
            bool changedRooms = false,
            bool magicOar = false,
            bool traded = false,
            bool suppressLeft = false,
            bool suppressRight = false)
        {
            _saveData.WriteWramBytes(0xc688, inventorySnapshot);
            int essences = _saveData.ReadWramByte(essencesAddress) &
                ~(record.D2EssenceMask | record.D3EssenceMask);
            if (d2)
                essences |= record.D2EssenceMask;
            if (d3)
                essences |= record.D3EssenceMask;
            _saveData.WriteWramByte(essencesAddress, (byte)essences);
            SetTreasureFlag(record.ChevalRopeTreasure, chevalRope);
            SetTreasureFlag(record.IslandChartTreasure, islandChart);
            SetTreasureFlag(TreasureDatabase.TreasureTradeItem, magicOar);
            _saveData.WriteWramByte(
                tradeItemAddress,
                magicOar ? (byte)record.RequiredTradeItem : (byte)0);
            _saveData.CommitInventoryChange();
            reloadInventory.Invoke(_inventory, null);

            _saveData.SetGlobalFlag(record.GaveRopeFlag, gaveRope);
            _saveData.SetGlobalFlag(record.ChangedRoomsFlag, changedRooms);
            _saveData.SetRoomFlag(
                group, leftRoom, OracleSaveData.RoomFlagItem, value: false);
            _saveData.SetRoomFlag(
                group, rightRoom, OracleSaveData.RoomFlagItem, traded);
            _saveData.SetRoomFlag(
                group, leftRoom, OracleSaveData.RoomFlag80, suppressLeft);
            _saveData.SetRoomFlag(
                group, rightRoom, OracleSaveData.RoomFlag80, suppressRight);
        }

        void ExpectDialogue(int textId, string phase)
        {
            FailIf(
                !_dialogue.IsOpen ||
                _dialogue.CurrentMessage !=
                    DialogueBox.PlainText(database.DialogueText(textId)),
                $"Rafton {phase} did not show TX_{textId:x4}.");
        }

        RaftonCharacter Rafton() =>
            _entities.Entities<RaftonCharacter>().Single();

        void PositionForTalk(int x)
        {
            _player.WarpTo(new Vector2(x, 0x54));
            _player.Face(Vector2I.Up);
        }

        void Talk(int x, int frames, int textId, string phase)
        {
            PositionForTalk(x);
            FailIf(
                !_interactions.TryInteract(_player),
                $"Rafton {phase} was not reachable through the normal A-button path.");
            StepRoomEventFrames(frames);
            ExpectDialogue(textId, phase);
        }

        var trace = new ValidationCutsceneTrace();
        _roomEvents.CommandTraceSink = trace;

        OracleRoomData rightRoomData = _world.LoadRoom(group, rightRoom);
        Configure();
        LoadValidationRoom(group, leftRoom);
        _entities.BeginScreenTransition(
            group, rightRoomData, new Vector2(rightRoomData.Width, 0));
        RaftonCharacter hiddenIncoming = Rafton();
        FailIf(
            hiddenIncoming.Active || hiddenIncoming.Visible,
            "Rafton did not explicitly retain his flag-suppressed hidden " +
            "presentation while scrolling into room 2:1f.");
        _entities.FinishScreenTransition();

        Configure(changedRooms: true);
        LoadValidationRoom(group, leftRoom);
        _entities.BeginScreenTransition(
            group, rightRoomData, new Vector2(rightRoomData.Width, 0));
        RaftonCharacter visibleIncoming = Rafton();
        FailIf(
            !visibleIncoming.Active || !visibleIncoming.Visible,
            "Rafton did not explicitly retain his flag-enabled visible " +
            "presentation while scrolling into room 2:1f.");
        _entities.FinishScreenTransition();

        Configure();
        LoadValidationRoom(group, leftRoom);
        RaftonCharacter left = Rafton();
        FailIf(
            left.Record is not { Id: 0x69, SubId: 0x00 } ||
            left.Position != new Vector2(0x68, 0x48) ||
            left.Record.SpriteName != "spr_masksalesman_rafton" ||
            left.Record.TileBase != 0x10 || left.Record.Palette != 1 ||
            left.CurrentAnimationTextureSize != new Vector2I(32, 32) ||
            left.AnimationRate != 0.0f || !raftonEvent.HasState ||
            raftonEvent.Behaviour != 0 || raftonEvent.CurrentCommandIndex != 0 ||
            raftonEvent.ButtonSensitive,
            "Room 2:1e did not preserve INTERAC_RAFTON's placement, visual, " +
            "zero-update initialization, or pre-D2 behaviour " +
            $"(record={left.Record.Id:x2}:{left.Record.SubId:x2}, " +
            $"position={left.Position}, tile={left.Record.TileBase}, " +
            $"palette={left.Record.Palette}, size={left.CurrentAnimationTextureSize}, " +
            $"rate={left.AnimationRate}, state={raftonEvent.HasState}, " +
            $"behaviour={raftonEvent.Behaviour}, command={raftonEvent.CurrentCommandIndex}, " +
            $"button={raftonEvent.ButtonSensitive}).");
        PositionForTalk(0x68);
        FailIf(
            _entities.FindTalkTarget(_player) is not null,
            "Rafton became A-sensitive before initcollisions ran.");
        StepRoomEventFrames(1);
        FailIf(
            !raftonEvent.ButtonSensitive ||
            raftonEvent.CurrentCommandIndex != 3 ||
            raftonEvent.LoadedTextId != 0x2700 ||
            _entities.FindTalkTarget(_player) != left,
            "Pre-D2 Rafton did not dispatch var38=0 to his loaded-text loop.");
        Talk(0x68, 1, 0x2700, "pre-D2 greeting");
        _dialogue.Close();
        StepRoomEventFrames(2);
        FailIf(
            raftonEvent.CurrentCommandIndex != 3 ||
            raftonEvent.BlocksGameplay || _player.CutsceneControlled,
            "Pre-D2 Rafton did not restore input and return to checkabutton " +
            $"(command={raftonEvent.CurrentCommandIndex}, blocks=" +
            $"{raftonEvent.BlocksGameplay}, controlled={_player.CutsceneControlled}).");

        LoadValidationRoom(group, rightRoom);
        FailIf(
            _entities.Entities<RaftonCharacter>().Count != 1 ||
            _entities.Entities<RaftonCharacter>().Any(actor => actor.Active) ||
            raftonEvent.HasState,
            "Unset changed-rooms flag $26 did not suppress Rafton in room 2:1f.");

        Configure(d2: true);
        LoadValidationRoom(group, leftRoom);
        StepRoomEventFrames(1);
        FailIf(
            raftonEvent.Behaviour != 1 || raftonEvent.CurrentCommandIndex != 9,
            "D2 essence bit 1 did not select Rafton behaviour 1.");
        Talk(0x68, 4, 0x2700, "post-D2 introduction");
        _dialogue.Close();
        StepRoomEventFrames(20);
        FailIf(
            _dialogue.IsOpen || raftonEvent.Counter != 1,
            "Rafton's wait 20 ended before its exact counter boundary.");
        StepRoomEventFrames(1);
        ExpectDialogue(0x2701, "post-D2 repeat setup");
        _dialogue.Close();
        StepRoomEventFrames(1);
        Talk(0x68, 1, 0x2701, "post-D2 repeat");
        _dialogue.Close();
        StepRoomEventFrames(1);

        Configure(chevalRope: true);
        LoadValidationRoom(group, leftRoom);
        StepRoomEventFrames(1);
        FailIf(
            raftonEvent.Behaviour != 2 || raftonEvent.CurrentCommandIndex != 15,
            "Cheval Rope did not select Rafton behaviour 2.");
        PositionForTalk(0x68);
        FailIf(!_interactions.TryInteract(_player),
            "Cheval Rope Rafton was not talkable.");
        StepRoomEventFrames(4);
        FailIf(
            raftonEvent.CurrentCommandIndex != 17 || raftonEvent.Counter != 20,
            "Rafton's face-and-freeze subscript did not install wait 20.");
        int clinks = _sound.PlayRequestsFor(OracleSoundEngine.SndClink);
        StepRoomEventFrames(19);
        FailIf(
            raftonEvent.Counter != 1 ||
            _entities.Entities<NpcCharacter>().Any(npc =>
                npc.Record.Id == record.EffectId && npc.Active),
            "Rafton's exclamation appeared before wait 20 completed.");
        StepRoomEventFrames(1);
        NpcCharacter exclamation = _entities.Entities<NpcCharacter>().Single(npc =>
            npc.Record.Id == record.EffectId && npc.Active);
        FailIf(
            exclamation.Position != new Vector2(0x68, 0x3b) ||
            exclamation.Record.SpriteName !=
                "spr_zz_bubble_exclamation_heart_kid" ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndClink) != clinks + 1 ||
            raftonEvent.CurrentCommandIndex != 19 || raftonEvent.Counter != 30,
            "Rafton did not create INTERAC_EXCLAMATION_MARK at -13/0, play " +
            "SND_CLINK, and install wait 30 on the source boundary.");
        StepRoomEventFrames(29);
        FailIf(!exclamation.Active || raftonEvent.Counter != 1,
            "Rafton's exclamation or enclosing wait expired early.");
        StepRoomEventFrames(1);
        ExpectDialogue(0x2702, "rope recognition");
        FailIf(!exclamation.Active,
            "Rafton's 60-update exclamation expired after only the first wait 30.");
        _dialogue.Close();
        StepRoomEventFrames(31);
        ExpectDialogue(0x2703, "rope request");
        FailIf(!_dialogue.ChoiceActive || exclamation.Active,
            "TX_2703 did not expose Rafton's OK/Sorry options.");
        _dialogue.SubmitChoiceForValidation(1);
        StepRoomEventFrames(20);
        FailIf(_dialogue.IsOpen || raftonEvent.Counter != 1,
            "Declined rope wait 20 ended early.");
        StepRoomEventFrames(1);
        ExpectDialogue(0x2704, "declined rope response");
        FailIf(
            !_inventory.HasTreasure(record.ChevalRopeTreasure) ||
            _saveData.HasGlobalFlag(record.GaveRopeFlag),
            "Declining Rafton's request changed rope or global flag $15.");
        _dialogue.Close();
        StepRoomEventFrames(1);
        Talk(0x68, 4, 0x2703, "repeated rope request");
        _dialogue.SubmitChoiceForValidation(0);
        StepRoomEventFrames(1);
        FailIf(
            _inventory.HasTreasure(record.ChevalRopeTreasure) ||
            _saveData.HasGlobalFlag(record.GaveRopeFlag) ||
            raftonEvent.Counter != 20,
            "loseTreasure did not clear Cheval Rope before the following wait 20.");
        StepRoomEventFrames(20);
        ExpectDialogue(0x2705, "accepted rope response");
        FailIf(
            _dialogue.CurrentMessage.Contains("\\n", StringComparison.Ordinal) ||
            !_dialogue.CurrentMessage.Contains(
                "invitation to\ndisaster.\nI'll have the",
                StringComparison.Ordinal),
            "Rafton TX_2705 did not fall through into TX_2706 in the same " +
            "accepted-rope textbox.");
        _dialogue.Close();
        StepRoomEventFrames(20);
        FailIf(_saveData.HasGlobalFlag(record.GaveRopeFlag),
            "Rafton set global flag $15 before the final wait 20 boundary.");
        StepRoomEventFrames(1);
        FailIf(
            !_saveData.HasGlobalFlag(record.GaveRopeFlag) ||
            raftonEvent.BlocksGameplay || _player.CutsceneControlled ||
            raftonEvent.LoadedTextId != 0x2706 ||
            raftonEvent.CurrentCommandIndex != 3,
            "Accepted rope path did not set flag $15, restore input, and enter " +
            "behaviour-3 TX_2706 loop.");
        Talk(0x68, 1, 0x2706, "post-rope repeat greeting");
        _dialogue.Close();
        StepRoomEventFrames(1);

        Configure(islandChart: true);
        LoadValidationRoom(group, leftRoom);
        StepRoomEventFrames(1);
        left = Rafton();
        FailIf(
            raftonEvent.Behaviour != 4 ||
            raftonEvent.CurrentCommandIndex != 40 ||
            raftonEvent.Counter != 100 ||
            !raftonEvent.BlocksGameplay || !_player.CutsceneControlled,
            "Island Chart did not select Rafton's forced behaviour-4 wait 100.");
        StepRoomEventFrames(99);
        FailIf(
            raftonEvent.Counter != 1 || _dialogue.IsOpen ||
            _saveData.HasGlobalFlag(record.ChangedRoomsFlag),
            "Rafton's forced wait 100 ended before its exact boundary.");
        StepRoomEventFrames(1);
        FailIf(
            raftonEvent.CurrentCommandIndex != 42 ||
            raftonEvent.Counter != record.FreezeCounter,
            "Rafton's wait 100 did not write Interaction.animCounter=$7f.");
        StepRoomEventFrames(1);
        ExpectDialogue(0x2707, "completed raft announcement");
        _dialogue.Close();
        StepRoomEventFrames(31);
        FailIf(
            raftonEvent.CurrentCommandIndex != 45 || raftonEvent.Counter != 0,
            "Rafton's wait 30 did not yield after setspeed SPEED_100.");
        StepRoomEventFrames(1);
        FailIf(
            raftonEvent.CurrentCommandIndex != 45 ||
            raftonEvent.Counter != record.MoveCounter ||
            left.Position != new Vector2(0x68, 0x48),
            "Rafton did not install SPEED_100 moveright $40 without moving on setup.");
        StepRoomEventFrames(1);
        FailIf(
            left.Position != new Vector2(0x69, 0x48) ||
            raftonEvent.Counter != 0x3f ||
            left.FacingVector != Vector2I.Right ||
            left.Record.RightAnimation != record.Animation1 ||
            left.Record.RightAnimation == left.Record.DownAnimation,
            "Rafton's first SPEED_100 update did not move one pixel and select " +
            "the imported right-facing animation $01.");
        StepRoomEventFrames(62);
        FailIf(
            left.Position != new Vector2(0xa7, 0x48) ||
            raftonEvent.Counter != 1 ||
            _saveData.HasGlobalFlag(record.ChangedRoomsFlag),
            "moveright $40 did not preserve 63 nonzero movement updates.");
        StepRoomEventFrames(1);
        FailIf(
            left.Position != new Vector2(0xa7, 0x48) ||
            raftonEvent.CurrentCommandIndex != 46 ||
            _saveData.HasGlobalFlag(record.ChangedRoomsFlag),
            "moveright $40 moved on its zero update or set flag $26 early.");
        StepRoomEventFrames(1);
        FailIf(
            !_saveData.HasGlobalFlag(record.ChangedRoomsFlag) ||
            raftonEvent.HasState || left.Active ||
            raftonEvent.BlocksGameplay || _player.CutsceneControlled,
            "Rafton's departure did not set flag $26, restore input, end script, " +
            "and delete $69:$00 on the following update.");

        LoadValidationRoom(group, leftRoom);
        FailIf(
            _entities.Entities<RaftonCharacter>().Count != 1 ||
            _entities.Entities<RaftonCharacter>().Any(actor => actor.Active) ||
            raftonEvent.HasState,
            "Changed-rooms flag $26 did not suppress Rafton in room 2:1e " +
            $"(actors={_entities.Entities<RaftonCharacter>().Count}, " +
            $"state={raftonEvent.HasState}).");
        LoadValidationRoom(group, rightRoom);
        RaftonCharacter right = Rafton();
        FailIf(
            right.Record is not { Id: 0x69, SubId: 0x01 } ||
            right.Position != new Vector2(0x28, 0x48),
            "Changed-rooms flag $26 did not reveal Rafton at 2:1f $48/$28.");

        Configure(changedRooms: true);
        LoadValidationRoom(group, rightRoom);
        StepRoomEventFrames(1);
        FailIf(
            raftonEvent.CurrentCommandIndex != 3 ||
            raftonEvent.LoadedTextId != 0,
            "Missing D3 did not take jumpifmemoryset's yield-on-miss boundary.");
        StepRoomEventFrames(1);
        FailIf(
            raftonEvent.CurrentCommandIndex != 4 ||
            raftonEvent.LoadedTextId != 0x2708,
            "Pre-D3 Rafton did not load TX_2708 before checkabutton.");
        Talk(0x28, 1, 0x2708, "pre-D3 greeting");
        _dialogue.Close();
        StepRoomEventFrames(10);
        FailIf(_dialogue.IsOpen || raftonEvent.Counter != 1,
            "Pre-D3 Rafton wait 10 ended early.");
        StepRoomEventFrames(1);
        FailIf(raftonEvent.CurrentCommandIndex != 9,
            "Pre-D3 Rafton wait 10 did not yield after setanimation DIR_DOWN.");
        StepRoomEventFrames(1);
        FailIf(
            raftonEvent.CurrentCommandIndex != 4 ||
            raftonEvent.LoadedTextId != 0x270a,
            "Pre-D3 Rafton did not switch repeats to TX_270a.");
        Talk(0x28, 1, 0x270a, "pre-D3 repeat");
        _dialogue.Close();
        StepRoomEventFrames(11);

        Configure(d3: true, changedRooms: true);
        LoadValidationRoom(group, rightRoom);
        StepRoomEventFrames(1);
        FailIf(
            raftonEvent.CurrentCommandIndex != 11,
            "D3 essence bit 2 did not select Rafton's trade loop.");
        Talk(0x28, 1, 0x2710, "missing-oar dialogue");
        _dialogue.Close();
        StepRoomEventFrames(30);
        FailIf(raftonEvent.Counter != 1 || raftonEvent.BlocksGameplay == false,
            "Missing-oar wait 30 ended early or released input prematurely.");
        StepRoomEventFrames(1);
        FailIf(
            raftonEvent.CurrentCommandIndex != 11 ||
            raftonEvent.BlocksGameplay || _player.CutsceneControlled,
            "Missing Magic Oar did not restore input after wait 30.");

        Configure(d3: true, changedRooms: true, magicOar: true);
        LoadValidationRoom(group, rightRoom);
        StepRoomEventFrames(1);
        Talk(0x28, 1, 0x2710, "Magic Oar introduction");
        _dialogue.Close();
        StepRoomEventFrames(31);
        ExpectDialogue(0x2711, "Magic Oar prompt");
        FailIf(!_dialogue.ChoiceActive,
            "TX_2711 did not expose Rafton's Yes/No trade options.");
        _dialogue.SubmitChoiceForValidation(1);
        StepRoomEventFrames(31);
        ExpectDialogue(0x2713, "declined Magic Oar trade");
        FailIf(
            _inventory.TradeItem != record.RequiredTradeItem ||
            _saveData.HasRoomFlag(group, rightRoom, (byte)record.RoomFlag),
            "Declining the Magic Oar trade changed inventory or room bit $20.");
        _dialogue.Close();
        StepRoomEventFrames(1);

        Talk(0x28, 1, 0x2710, "repeated Magic Oar introduction");
        _dialogue.Close();
        StepRoomEventFrames(31);
        ExpectDialogue(0x2711, "repeated Magic Oar prompt");
        _dialogue.SubmitChoiceForValidation(0);
        StepRoomEventFrames(31);
        ExpectDialogue(0x2712, "accepted Magic Oar trade");
        _dialogue.Close();
        StepRoomEventFrames(31);
        GroundTreasurePickup reward =
            _entities.Entities<GroundTreasurePickup>().Single();
        TreasureObjectRecord rewardObject =
            _treasures.GetObject(record.RewardObject);
        FailIf(
            reward.Record.TreasureObject != record.RewardObject ||
            reward.Record.GrabMode != 2 || !reward.Held ||
            !_player.IsHoldingItemTwoHands ||
            _inventory.TradeItem != record.RewardParameter ||
            !_saveData.HasRoomFlag(group, rightRoom, (byte)record.RoomFlag) ||
            !_dialogue.IsOpen ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(rewardObject.Message),
            "Rafton's giveitem did not exchange Magic Oar for a two-hand Sea " +
            "Ukulele and set room bit $20.");
        _dialogue.Close();
        _interactions.Update(1.0 / 60.0, _player);
        _entities.Update(1.0 / 60.0, _player);
        StepRoomEventFrames(1);
        FailIf(
            _player.IsHoldingItemTwoHands ||
            _entities.Entities<GroundTreasurePickup>().Count != 0 ||
            raftonEvent.CurrentCommandIndex != 11 ||
            raftonEvent.BlocksGameplay || _player.CutsceneControlled,
            "Sea Ukulele cleanup did not restore Rafton's trade loop and input.");

        LoadValidationRoom(group, rightRoom);
        StepRoomEventFrames(1);
        Talk(0x28, 1, 0x2714, "completed-trade greeting");
        _dialogue.Close();
        StepRoomEventFrames(1);
        ValidateInteractiveInfiniteScriptCancellation(
            raftonEvent,
            Rafton(),
            "Rafton");

        Configure(changedRooms: true, suppressRight: true);
        LoadValidationRoom(group, rightRoom);
        FailIf(
            _entities.Entities<RaftonCharacter>().Count != 1 ||
            _entities.Entities<RaftonCharacter>().Any(actor => actor.Active) ||
            raftonEvent.HasState,
            "Room flag $80 did not suppress INTERAC_RAFTON $69:$01.");

        CutsceneCommandTraceEntry[] commandStarts = trace.Entries.Where(entry =>
            entry.Phase == CutsceneCommandTracePhase.Started &&
            entry.Source.Script is
                "rafton_subid00Script" or "rafton_subid01Script").ToArray();
        string[] requiredOpcodes =
        [
            "jumptablememory", "writememory", "showloadedtext", "callscript",
            "writeobjectbyte", "move", "setglobalflag",
            "jumpifmemoryeqyieldonmiss", "jumpiftradeitemeq",
            "jumpiftextoptioneq", "giveitem"
        ];
        FailIf(
            commandStarts.Any(entry => entry.Source.SourceLine <= 0) ||
            requiredOpcodes.Any(opcode =>
                !commandStarts.Any(entry => entry.Source.Opcode == opcode)),
            "Rafton's typed trace lost source lines or a required opcode.");

        _saveData.WriteWramBytes(0xc688, inventorySnapshot);
        _saveData.CommitInventoryChange();
        reloadInventory.Invoke(_inventory, null);
        _saveData.SetGlobalFlag(record.GaveRopeFlag, originalGaveRope);
        _saveData.SetGlobalFlag(record.ChangedRoomsFlag, originalChangedRooms);
        foreach (byte flag in new byte[] { 1, 2, 4, 8, 0x10, 0x20, 0x40, 0x80 })
        {
            _saveData.SetRoomFlag(
                group, leftRoom, flag, (originalLeftFlags & flag) != 0);
            _saveData.SetRoomFlag(
                group, rightRoom, flag, (originalRightFlags & flag) != 0);
        }
        _roomEvents.CommandTraceSink = null;

        GD.Print("Validated rooms 2:1e/2:1f Rafton $69:$00/$01: five imported " +
            "left-room behaviours, TX_2700-$2714 loaded/choice dialogue, exact " +
            "10/20/30/100 waits, 60-update exclamation/SND_CLINK, Cheval Rope " +
            "loss and flags $15/$26, SPEED_100 moveright $40, D3 Magic Oar " +
            "No/Yes trade, two-hand Sea Ukulele, room bits $20/$80, visibility, " +
            "typed traces, and cancellation.");
    }
}
