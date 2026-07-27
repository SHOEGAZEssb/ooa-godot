using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom083Interactions()
    {
        var npcs = new NpcDatabase();
        NpcRecord fairyRecord = npcs.GetRoomNpcs(0, 0x83).Single(npc =>
            npc.Id == 0xd5 && npc.SubId == 0x00);
        var visibility = new NpcVisibilityRuleDatabase();
        var runtime = new OracleRuntimeState();
        var predicateSave = OracleSaveData.CreateStandardGame();
        if (fairyRecord is not
            {
                Y: 0x28,
                X: 0x58,
                TextId: 0x4d1e,
                Palette: 3,
                Implementation:
                    NpcImplementationClassification.SpecializedNative
            } ||
            visibility.ShouldShow(fairyRecord, predicateSave, runtime))
        {
            throw new InvalidOperationException(
                "Room 0:83 lost the imported Temple Secret Great Fairy " +
                "placement or unlinked-file predicate.");
        }
        predicateSave.SetLinkedGame(true);
        if (visibility.ShouldShow(fairyRecord, predicateSave, runtime))
        {
            throw new InvalidOperationException(
                "The room 0:83 Great Fairy appeared before D2 was obtained.");
        }
        predicateSave.WriteWramByte(0xc6bf, 0x02);
        if (!visibility.ShouldShow(fairyRecord, predicateSave, runtime))
        {
            throw new InvalidOperationException(
                "The room 0:83 Great Fairy did not appear for linked + D2.");
        }

        var linkedNpcs = new LinkedGameNpcDatabase();
        LinkedGameNpcDatabaseRecord fairyData =
            linkedNpcs.Get(0, 0x83, 0xd5, 0x00);
        var secretSave = OracleSaveData.CreateStandardGame();
        secretSave.WriteWramByte(0xc600, 0x34);
        secretSave.WriteWramByte(0xc601, 0x12);
        byte[] secret =
            linkedNpcs.GenerateSecretValues(fairyData, secretSave);
        if (!secret.SequenceEqual(
                new byte[] { 0x03, 0x35, 0x27, 0x02, 0x16 }) ||
            secretSave.ReadWramByte(0xc6fb) != 0x26)
        {
            throw new InvalidOperationException(
                "The Temple secret lost its source bit packing, checksum, " +
                "or XOR cipher.");
        }

        ValidateRoom083GreatFairyAppearance(npcs);
        ValidateRoom083DialogueAndSign(fairyData);
        ValidateRoom083WingDungeonCollapse();

        GD.Print(
            "Validated room 0:83: linked+D2 Great Fairy visibility, " +
            "scroll-frozen $73/$98 spawn, 32-update reveal, Z bob, " +
            "Ancient Cave sign, Temple Secret dialogue/encoding, and the " +
            "$dc:$02 Bracelet-rock Wing Dungeon collapse with its " +
            "$8a:$00/v$01 TX_05b1 remote-Maku continuation.");
    }

    private void ValidateRoom083GreatFairyAppearance(NpcDatabase npcs)
    {
        var root = new Node { Name = "Room083GreatFairyValidation" };
        AddChild(root);
        var save = OracleSaveData.CreateStandardGame();
        save.SetLinkedGame(true);
        save.WriteWramByte(0xc6bf, 0x02);
        var sounds = new List<int>();
        var manager = new RoomEntityManager(
            root,
            npcs,
            new EnemyDatabase(),
            new ItemDropDatabase(),
            new TimePortalDatabase(),
            new OracleRandom(),
            save);
        manager.SoundRequested += sounds.Add;
        manager.LoadRoom(0, _world.LoadRoom(0, 0x82));
        manager.BeginScreenTransition(
            0, _world.LoadRoom(0, 0x83), new Vector2(160, 0));

        NpcCharacter fairy = manager.Entities<NpcCharacter>().Single(npc =>
            npc.Record.Id == 0xd5 && npc.Record.SubId == 0x00);
        _player.WarpTo(
            fairy.Position + Vector2.Down * 12.0f, recordSafe: false);
        _player.Face(Vector2I.Up);
        if (fairy.Visible || fairy.ScriptVisible ||
            !fairy.BlocksLinkCenter(fairy.Position))
        {
            throw new InvalidOperationException(
                "The room 0:83 Great Fairy was not hidden but solid during " +
                "its source spawn wait.");
        }

        for (int update = 0; update < 40; update++)
            manager.Update(1.0 / 60.0, _player);
        if (sounds.Count != 0 ||
            manager.Entities<PuzzlePuffEffect>().Count != 0 ||
            fairy.Visible)
        {
            throw new InvalidOperationException(
                "The Great Fairy advanced its always-update script while " +
                "the destination room was scrolling.");
        }

        manager.FinishScreenTransition();
        manager.Update(1.0 / 60.0, _player);
        PuzzlePuffEffect puff =
            manager.Entities<PuzzlePuffEffect>().Single();
        if (!sounds.SequenceEqual(
                new[]
                {
                    OracleSoundEngine.SndKillEnemy,
                    OracleSoundEngine.SndPoof
                }) ||
            puff.ElapsedUpdates != 1 ||
            fairy.Visible ||
            fairy.ScriptDrawOffset != new Vector2(0, -16) ||
            manager.FindTalkTarget(_player) is not null)
        {
            throw new InvalidOperationException(
                "The Great Fairy's first normal update did not create the " +
                "source kill-enemy/poof appearance while remaining untalkable.");
        }

        for (int update = 0; update < 31; update++)
            manager.Update(1.0 / 60.0, _player);
        if (fairy.Visible ||
            sounds.Contains(OracleSoundEngine.MusFairyFountain))
        {
            throw new InvalidOperationException(
                "The Great Fairy appeared before its 32-update source wait.");
        }

        manager.Update(1.0 / 60.0, _player);
        if (!fairy.Visible || !fairy.ScriptVisible ||
            sounds.Count(sound =>
                sound == OracleSoundEngine.MusFairyFountain) != 1 ||
            !ReferenceEquals(manager.FindTalkTarget(_player), fairy))
        {
            throw new InvalidOperationException(
                "The Great Fairy did not become visible and talkable with " +
                "MUS_FAIRY_FOUNTAIN after update 32.");
        }

        for (int update = 0; update < 7; update++)
            manager.Update(1.0 / 60.0, _player);
        if (manager.FrameCounter != 40 ||
            fairy.ScriptDrawOffset != new Vector2(0, -14))
        {
            throw new InvalidOperationException(
                "The Great Fairy lost the source frame-$28 +2 Z-height step.");
        }

        manager.TextActiveSource = static () => true;
        for (int update = 0; update < 8; update++)
            manager.Update(1.0 / 60.0, _player);
        if (manager.FrameCounter != 48 ||
            fairy.ScriptDrawOffset != new Vector2(0, -13))
        {
            throw new InvalidOperationException(
                "INTERAC_GREAT_FAIRY's enabled bit 7 did not preserve its " +
                "animation/Z update while wTextIsActive was set.");
        }

        manager.Clear();
        RemoveChild(root);
        root.Free();
    }

    private void ValidateRoom083DialogueAndSign(
        LinkedGameNpcDatabaseRecord fairyData)
    {
        bool linkedBefore = _saveData.IsLinkedGame;
        byte essencesBefore = _saveData.ReadWramByte(0xc6bf);
        byte gameIdLowBefore = _saveData.ReadWramByte(0xc600);
        byte gameIdHighBefore = _saveData.ReadWramByte(0xc601);
        byte shortSecretBefore = _saveData.ReadWramByte(0xc6fb);
        bool beganBefore = _saveData.HasGlobalFlag(fairyData.BeganFlag);

        try
        {
            _dialogue.Close();
            _saveData.SetLinkedGame(true);
            _saveData.WriteWramByte(
                0xc6bf, (byte)(essencesBefore | 0x02));
            _saveData.WriteWramByte(0xc600, 0x34);
            _saveData.WriteWramByte(0xc601, 0x12);
            _saveData.SetGlobalFlag(fairyData.BeganFlag, value: false);
            _saveData.CommitInventoryChange();

            LoadValidationRoom(0, 0x83);
            for (int update = 0; update < 33; update++)
                _entities.Update(1.0 / 60.0, _player);
            NpcCharacter fairy = _entities.Entities<NpcCharacter>().Single(
                npc => npc.Record.Id == 0xd5 && npc.Record.SubId == 0x00);
            if (!fairy.Visible)
            {
                throw new InvalidOperationException(
                    "The linked+D2 Great Fairy predicate did not survive " +
                    "actual room loading and its appearance wait.");
            }

            _player.WarpTo(new Vector2(0x28, 0x46), recordSafe: false);
            _player.Face(Vector2I.Up);
            if (!_interactions.TryInteract(_player) ||
                !_dialogue.CurrentMessage.Contains(
                    "Ancient Cave", StringComparison.Ordinal) ||
                !_dialogue.CurrentMessage.Contains(
                    "Crumbles", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Room 0:83's $dc:$02 sign did not open its imported text.");
            }
            _dialogue.Close();
            _interactions.Update(0.0, _player);

            _player.WarpTo(
                fairy.Position + Vector2.Down * 12.0f, recordSafe: false);
            _player.Face(Vector2I.Up);
            if (!_interactions.TryInteract(_player) ||
                !_dialogue.ChoiceActive ||
                !_dialogue.CurrentMessage.Contains(
                    "Labrynna", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The Great Fairy did not open TX_4d1e's Yes/No offer.");
            }
            _dialogue.SubmitChoiceForValidation(1);
            _interactions.Update(0.0, _player);
            if (_dialogue.ChoiceActive ||
                !_dialogue.CurrentMessage.Contains(
                    "Come back", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Choosing No did not follow linkedGameNpcScript to TX_4d1f.");
            }
            _dialogue.Close();
            _interactions.Update(0.0, _player);

            if (!_interactions.TryInteract(_player))
            {
                throw new InvalidOperationException(
                    "The Great Fairy offer loop could not be restarted.");
            }
            _dialogue.SubmitChoiceForValidation(0);
            _interactions.Update(0.0, _player);
            if (!_dialogue.ChoiceActive ||
                !_dialogue.CurrentMessage.Contains(
                    "sunken", StringComparison.Ordinal) ||
                !_dialogue.CurrentMessage.Contains(
                    "Temple", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Choosing Yes did not open TX_4d20's confirmation.");
            }
            _dialogue.SubmitChoiceForValidation(1);
            _interactions.Update(0.0, _player);
            if (!_dialogue.ChoiceActive || _dialogue.SelectedChoice != 1)
            {
                throw new InvalidOperationException(
                    "Choosing No did not repeat the Great Fairy confirmation.");
            }
            _dialogue.SubmitChoiceForValidation(0);
            _interactions.Update(0.0, _player);
            if (!_dialogue.ChoiceActive ||
                _dialogue.CurrentMessage.Contains(
                    "\\secret1", StringComparison.Ordinal) ||
                !_saveData.HasGlobalFlag(fairyData.BeganFlag) ||
                _saveData.ReadWramByte(0xc6fb) != 0x26)
            {
                throw new InvalidOperationException(
                    "The Great Fairy did not generate/substitute the Temple " +
                    "secret and set GLOBALFLAG_BEGAN_TEMPLE_SECRET.");
            }
            _dialogue.SubmitChoiceForValidation(1);
            _interactions.Update(0.0, _player);
            if (!_dialogue.ChoiceActive || _dialogue.SelectedChoice != 1)
            {
                throw new InvalidOperationException(
                    "Choosing No did not repeat TX_4d21's generated secret.");
            }
            _dialogue.SubmitChoiceForValidation(0);
            _interactions.Update(0.0, _player);
            if (_dialogue.ChoiceActive ||
                !_dialogue.CurrentMessage.Contains(
                    "Thank you", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Confirming the Temple secret did not show TX_4d22.");
            }
            _dialogue.Close();
            _interactions.Update(0.0, _player);
        }
        finally
        {
            _dialogue.Close();
            LoadValidationRoom(0, 0x11);
            _saveData.SetLinkedGame(linkedBefore);
            _saveData.WriteWramByte(0xc6bf, essencesBefore);
            _saveData.WriteWramByte(0xc600, gameIdLowBefore);
            _saveData.WriteWramByte(0xc601, gameIdHighBefore);
            _saveData.WriteWramByte(0xc6fb, shortSecretBefore);
            _saveData.SetGlobalFlag(fairyData.BeganFlag, beganBefore);
            _saveData.CommitInventoryChange();
        }
    }

    private void ValidateRoom083WingDungeonCollapse()
    {
        bool roomFlagBefore = _saveData.HasRoomFlag(
            0, 0x83, OracleSaveData.RoomFlag80);
        bool linkedRoomFlagBefore = _saveData.HasRoomFlag(
            0, 0x73, OracleSaveData.RoomFlag80);
        bool remoteRoomFlagBefore = _saveData.HasRoomFlag(
            0, 0x83, OracleSaveData.RoomFlag40);
        int makuStateBefore = _saveData.MakuTreeState;
        int makuMapTextBefore = _saveData.MakuMapTextPresent;
        bool linkedBefore = _saveData.IsLinkedGame;
        WingDungeonCollapseEvent collapse =
            _roomEvents.WingDungeonCollapse;
        RemoteMakuFirstEssenceEvent remoteMaku =
            _roomEvents.RemoteMakuFirstEssence;
        BraceletDatabaseRecord braceletData = new BraceletDatabase().Data;
        Vector2 rock = new(0x38, 0x48);

        try
        {
            _saveData.SetRoomFlag(
                0, 0x83, OracleSaveData.RoomFlag80, value: false);
            _saveData.SetRoomFlag(
                0, 0x73, OracleSaveData.RoomFlag80, value: false);
            _saveData.SetRoomFlag(
                0, 0x83, OracleSaveData.RoomFlag40, value: false);
            _saveData.SetLinkedGame(false);
            _saveData.SetMakuMapTextPresent(0);
            _inventory.EquipB(InventoryState.ItemBracelet);
            LoadValidationRoom(0, 0x83);
            byte[] facadeAttributes = ReadRoom083FacadeAttributes();
            if (_inventory.BraceletLevel < 1 ||
                _currentRoom.GetMetatile(rock) != 0xc3 ||
                collapse.Stage != WingDungeonCollapseStage.AwaitingRockLift)
            {
                throw new InvalidOperationException(
                    "Room 0:83 did not arm $dc:$02 over its source $c3 rock.");
            }

            _player.WarpTo(rock + Vector2.Left * 10, recordSafe: false);
            _player.Face(Vector2I.Right);
            _sound.ClearPlayRequestAudit();
            if (!_playerWorld.TryUseBracelet(
                    _player, primaryButton: false) ||
                _bracelet.State != BraceletState.GrabbingWall)
            {
                throw new InvalidOperationException(
                    "The Power Bracelet could not grab room 0:83's $c3 rock.");
            }
            for (int update = 0;
                 update < braceletData.GrabPullFrames - 1;
                 update++)
            {
                if (!_playerWorld.UpdateBracelet(
                        _player,
                        Vector2.Left,
                        primaryHeld: false,
                        secondaryHeld: true,
                        itemButtonJustPressed: false) ||
                    _currentRoom.GetMetatile(rock) != 0xc3)
                {
                    throw new InvalidOperationException(
                        "Room 0:83's rock moved before the Bracelet's " +
                        "11-update pull boundary.");
                }
            }
            if (!_playerWorld.UpdateBracelet(
                    _player,
                    Vector2.Left,
                    primaryHeld: false,
                    secondaryHeld: true,
                    itemButtonJustPressed: false) ||
                _bracelet.State != BraceletState.Lifting ||
                _currentRoom.GetMetatile(rock) != 0x1c ||
                collapse.Stage !=
                    WingDungeonCollapseStage.AwaitingPickup)
            {
                throw new InvalidOperationException(
                    "$dc:$02 did not replace Bracelet ground $3a with dug " +
                    "dirt $1c while waiting for Link grab state $83.");
            }

            int liftUpdates = braceletData.LiftLowFrames +
                braceletData.LiftMidFrames +
                braceletData.LiftHighFrames;
            for (int update = 0; update < liftUpdates - 1; update++)
            {
                if (!_playerWorld.UpdateBracelet(
                        _player,
                        Vector2.Zero,
                        primaryHeld: false,
                        secondaryHeld: false,
                        itemButtonJustPressed: false))
                {
                    throw new InvalidOperationException(
                        "Room 0:83's Bracelet lift ended before grab state $83.");
                }
            }
            if (_playerWorld.UpdateBracelet(
                    _player,
                    Vector2.Zero,
                    primaryHeld: false,
                    secondaryHeld: false,
                    itemButtonJustPressed: false) ||
                collapse.Stage != WingDungeonCollapseStage.PickupWait ||
                collapse.Counter != 30 ||
                !_player.CutsceneControlled ||
                !_player.IsCarryingObject ||
                _player.FacingVector != Vector2I.Right ||
                _sound.PlayRequestsFor(
                    OracleSoundEngine.SndCtrlStopMusic) != 1)
            {
                throw new InvalidOperationException(
                    "Completed rock pickup did not face Link right, stop " +
                    "music, and begin the source 30-update hold.");
            }

            AdvanceRoom083Collapse(collapse, 29);
            if (collapse.Stage != WingDungeonCollapseStage.PickupWait ||
                collapse.Counter != 1 ||
                !_player.IsCarryingObject)
            {
                throw new InvalidOperationException(
                    "The lifted rock or pickup wait ended before update 30.");
            }
            AdvanceRoom083Collapse(collapse, 1);
            NpcCharacter exclamation =
                _entities.Entities<NpcCharacter>().Single(npc =>
                    npc.Record.Id == 0x9f &&
                    npc.Record.SubId == 0x00 &&
                    npc.Active);
            if (collapse.Stage !=
                    WingDungeonCollapseStage.PreCollapseShake ||
                collapse.Counter != 60 ||
                exclamation.Position != new Vector2(0x38, 0x40) ||
                _player.IsCarryingObject ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndClink) != 1)
            {
                throw new InvalidOperationException(
                    "Update 30 did not create the 60-update exclamation and " +
                    "drop Link's held rock.");
            }

            AdvanceRoom083Collapse(collapse, 59);
            if (collapse.Counter != 1 ||
                collapse.Stage !=
                    WingDungeonCollapseStage.PreCollapseShake ||
                _entities.ScreenShakeCounter != 0x28)
            {
                throw new InvalidOperationException(
                    "The pre-collapse interaction lost its 60-update/$28 " +
                    "screen-shake boundary.");
            }
            AdvanceRoom083Collapse(collapse, 1);
            if (collapse.Stage != WingDungeonCollapseStage.CollapseStart ||
                _saveData.HasRoomFlag(
                    0, 0x83, OracleSaveData.RoomFlag80))
            {
                throw new InvalidOperationException(
                    "The room flag was written before CUTSCENE_D2_COLLAPSE " +
                    "began on the following update.");
            }
            AdvanceRoom083Collapse(collapse, 1);
            if (collapse.Stage != WingDungeonCollapseStage.InitialWait ||
                collapse.Counter != 60 ||
                !_saveData.HasRoomFlag(
                    0, 0x83, OracleSaveData.RoomFlag80) ||
                !_saveData.HasRoomFlag(
                    0, 0x73, OracleSaveData.RoomFlag80))
            {
                throw new InvalidOperationException(
                    "CUTSCENE_D2_COLLAPSE did not set room $83/$73 flag $80 " +
                    "and begin its 60-update tilemap hold.");
            }

            AdvanceRoom083Collapse(collapse, 59);
            if (collapse.Counter != 1 ||
                _sound.PlayRequestsFor(
                    OracleSoundEngine.SndDoorClose) != 0)
            {
                throw new InvalidOperationException(
                    "The first collapsing map appeared before the source " +
                    "60-update hold.");
            }
            AdvanceRoom083Collapse(collapse, 1);
            if (collapse.Stage != WingDungeonCollapseStage.FirstPhase ||
                collapse.DustCounter != 0x6a)
            {
                throw new InvalidOperationException(
                    "The collapse did not allocate INTERAC_97 at update 60.");
            }
            AdvanceRoom083Collapse(collapse, 1);
            AssertRoom083CollapseMap(
                collapse.Maps[0], facadeAttributes);
            if (collapse.Stage != WingDungeonCollapseStage.PhaseWait ||
                collapse.Counter != 30 ||
                collapse.Phase != 1 ||
                collapse.DustCounter != 0x69 ||
                _entities.Entities<PuzzlePuffEffect>().Count == 0 ||
                _sound.PlayRequestsFor(
                    OracleSoundEngine.SndDoorClose) != 1)
            {
                throw new InvalidOperationException(
                    "The first 6x6 collapse map, door-close sound, or " +
                    "INTERAC_97 puff boundary diverged.");
            }

            for (int phase = 1; phase < collapse.Maps.Count; phase++)
            {
                AdvanceRoom083Collapse(collapse, 29);
                if (collapse.Counter != 1)
                {
                    throw new InvalidOperationException(
                        $"Collapse phase {phase} ended before update 30.");
                }
                AdvanceRoom083Collapse(collapse, 1);
                AssertRoom083CollapseMap(
                    collapse.Maps[phase], facadeAttributes);
                if (_sound.PlayRequestsFor(
                        OracleSoundEngine.SndDoorClose) != phase + 1)
                {
                    throw new InvalidOperationException(
                        $"Collapse phase {phase} did not request " +
                        "SND_DOORCLOSE exactly once.");
                }
            }

            if (collapse.Stage != WingDungeonCollapseStage.FinalWait ||
                collapse.Counter != 60)
            {
                throw new InvalidOperationException(
                    "The collapsed entrance did not begin its final " +
                    "60-update hold.");
            }
            AssertRoom083FinalFacade();
            AdvanceRoom083Collapse(collapse, 59);
            if (collapse.Stage != WingDungeonCollapseStage.FinalWait ||
                collapse.Counter != 1)
            {
                throw new InvalidOperationException(
                    "The collapsed entrance released input before the final wait.");
            }
            AdvanceRoom083Collapse(collapse, 1);
            if (collapse.Stage != WingDungeonCollapseStage.Finish ||
                !_player.CutsceneControlled)
            {
                throw new InvalidOperationException(
                    "CUTSCENE_D2_COLLAPSE skipped its terminal state-4 update.");
            }
            AdvanceRoom083Collapse(collapse, 1);
            if (collapse.Stage != WingDungeonCollapseStage.Completed ||
                _player.CutsceneControlled ||
                _sound.ActiveMusic != _sound.Data.RoomMusic(0, 0x83) ||
                remoteMaku.Stage !=
                    RemoteMakuFirstEssenceEventStage.Running ||
                remoteMaku.ActiveRecord.Var03 != 1 ||
                remoteMaku.ActiveRecord.StandardTextId != 0x05b1)
            {
                throw new InvalidOperationException(
                    "Wing Dungeon collapse did not restore Link/room music " +
                    "and allocate objectData7e69's $8a:$00/v$01 warning.");
            }

            for (int update = 0;
                 update < 700 && !_dialogue.IsOpen;
                 update++)
            {
                StepRoomEventFrames(1);
            }
            if (!_dialogue.IsOpen ||
                !_dialogue.CurrentMessage.Contains(
                    "support", StringComparison.Ordinal) ||
                !_dialogue.CurrentMessage.Contains(
                    "Nayru's House", StringComparison.Ordinal) ||
                _saveData.MakuMapTextPresent != 0xb1)
            {
                throw new InvalidOperationException(
                    "The post-collapse remote Maku object did not show " +
                    "TX_05b1 and map-text byte $b1.");
            }
            _dialogue.Close();
            for (int update = 0;
                 update < 100 && remoteMaku.HasState;
                 update++)
            {
                StepRoomEventFrames(1);
            }
            if (remoteMaku.HasState ||
                _player.CutsceneControlled ||
                !_saveData.HasRoomFlag(
                    0, 0x83, OracleSaveData.RoomFlag40) ||
                _saveData.MakuTreeState != makuStateBefore + 1)
            {
                throw new InvalidOperationException(
                    "TX_05b1's shared remote-Maku lane did not restore input, " +
                    "set room flag $40, and increment wMakuTreeState.");
            }

            LoadValidationRoom(0, 0x11);
            LoadValidationRoom(0, 0x83);
            if (_currentRoom.GetMetatile(rock) != 0x1c ||
                collapse.HasState)
            {
                throw new InvalidOperationException(
                    "Room 0:83 did not restore its collapsed state or retired " +
                    "$dc:$02 after re-entry.");
            }
            AssertRoom083CollapseMap(
                collapse.Maps[^1], facadeAttributes);
            AssertRoom083FinalFacade();
        }
        finally
        {
            LoadValidationRoom(0, 0x11);
            _saveData.SetRoomFlag(
                0, 0x83, OracleSaveData.RoomFlag80, roomFlagBefore);
            _saveData.SetRoomFlag(
                0, 0x73, OracleSaveData.RoomFlag80, linkedRoomFlagBefore);
            _saveData.SetRoomFlag(
                0, 0x83, OracleSaveData.RoomFlag40, remoteRoomFlagBefore);
            _saveData.SetMakuTreeState(makuStateBefore);
            _saveData.SetMakuMapTextPresent(makuMapTextBefore);
            _saveData.SetLinkedGame(linkedBefore);
        }
    }

    private void AdvanceRoom083Collapse(
        WingDungeonCollapseEvent collapse,
        int updates)
    {
        for (int update = 0; update < updates; update++)
            collapse.UpdateFrame();
    }

    private void AssertRoom083CollapseMap(
        WingDungeonCollapseMapRecord map,
        IReadOnlyList<byte> expectedAttributes)
    {
        int index = 0;
        for (int y = 0; y < 6; y++)
        for (int x = 0; x < 6; x++)
        {
            byte actual =
                _currentRoom.GetBackgroundSubtileForValidation(8 + x, y);
            byte expected = map.TileIds[index++];
            byte attribute =
                _currentRoom.GetBackgroundAttributeForValidation(8 + x, y);
            if (actual != expected ||
                attribute != expectedAttributes[index - 1])
            {
                throw new InvalidOperationException(
                    $"Wing Dungeon collapse phase {map.Phase} BG tile " +
                    $"({x},{y}) was ${actual:x2}/attr ${attribute:x2}, " +
                    $"expected ${expected:x2}/attr " +
                    $"${expectedAttributes[index - 1]:x2}.");
            }
        }
    }

    private byte[] ReadRoom083FacadeAttributes()
    {
        var attributes = new byte[36];
        int index = 0;
        for (int y = 0; y < 6; y++)
        for (int x = 0; x < 6; x++)
        {
            attributes[index++] =
                _currentRoom.GetBackgroundAttributeForValidation(8 + x, y);
        }
        return attributes;
    }

    private void AssertRoom083FinalFacade()
    {
        WingDungeonCollapseRecord record =
            _roomEvents.WingDungeonCollapse.Record;
        int index = 0;
        for (int y = 0; y < record.FacadeHeight; y++)
        for (int x = 0; x < record.FacadeWidth; x++)
        {
            Vector2 center = new(
                (4 + x) * OracleRoomData.MetatileSize + 8,
                y * OracleRoomData.MetatileSize + 8);
            TerrainInfo terrain = _currentRoom.GetTerrainInfo(center);
            if (terrain.Tile != record.FinalTiles[index] ||
                terrain.Collision != record.FinalCollisions[index])
            {
                throw new InvalidOperationException(
                    $"Collapsed Wing Dungeon facade ({x},{y}) was " +
                    $"tile/collision ${terrain.Tile:x2}/" +
                    $"${terrain.Collision:x2}, expected " +
                    $"${record.FinalTiles[index]:x2}/" +
                    $"${record.FinalCollisions[index]:x2}.");
            }
            index++;
        }
    }
}
