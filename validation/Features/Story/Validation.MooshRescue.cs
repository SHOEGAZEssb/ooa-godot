using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom06cMooshRescue()
    {
        const int group = 0;
        const int room = 0x6c;
        const ulong linkLeft1ePixelHash = 0x86096bcb7a70c48fUL;
        const ulong linkRight1cPixelHash = 0x7b0717062915917fUL;
        const ulong linkRight20PixelHash = linkRight1cPixelHash;
        Vector2[] mooshCollisionSamples =
        [
            new(-3, -5), new(4, -5), new(-3, 8), new(4, 8),
            new(-5, -3), new(-5, 6), new(6, -3), new(6, 6)
        ];
        MooshRescueEvent rescue = _roomEvents.MooshRescue;
        MooshRescueEventDatabase database = rescue.Database;
        MooshRescueEventRecord record = database.Record;

        void Configure(bool essence, bool sourceFlag, int mooshState = 0)
        {
            byte essences = _saveData.ReadWramByte(record.EssenceAddress);
            _saveData.WriteWramByte(
                record.EssenceAddress,
                essence
                    ? (byte)(essences | record.EssenceMask)
                    : (byte)(essences & ~record.EssenceMask));
            _saveData.SetRoomFlag(
                record.FlagGroup,
                record.FlagRoom,
                (byte)record.FlagMask,
                sourceFlag);
            _saveData.WriteWramByte(record.MooshStateAddress, (byte)mooshState);
            _inventory.LoseTreasure(record.ChevalRopeTreasure);
        }

        List<NpcCharacter> PlacedGhinis() =>
            _entities.Entities<NpcCharacter>()
                .Where(npc => npc.Record is { Id: 0x73 } &&
                    npc.Record.Room == room)
                .OrderBy(npc => npc.Record.SubId)
                .ToList();

        string Text(int textId) =>
            new[] { database.Ghini0, database.Ghini1, database.Ghini2, database.Companion }
                .SelectMany(commands => commands)
                .OfType<CutsceneShowTextCommand>()
                .Single(command => command.TextId == textId)
                .Message;

        void ExpectDialogue(int textId, string phase)
        {
            FailIf(
                !_dialogue.IsOpen ||
                _dialogue.CurrentMessage != DialogueBox.PlainText(Text(textId)),
                $"Room 0:6c {phase} did not show TX_{textId:x4}.");
        }

        void WaitForDialogue(int textId, int maximumFrames, string phase)
        {
            for (int frame = 0; frame < maximumFrames && !_dialogue.IsOpen; frame++)
                StepRoomEventFrames(1);
            ExpectDialogue(textId, phase);
        }

        int LinkMooshManhattanDistance(MooshCompanionRoomEntity companion) =>
            Math.Abs(
                Mathf.FloorToInt(_player.PrecisePosition.X) -
                Mathf.FloorToInt(companion.Position.X)) +
            Math.Abs(
                Mathf.FloorToInt(_player.PrecisePosition.Y) -
                Mathf.FloorToInt(companion.Position.Y));

        void ExpectMountedLinkAttached(
            MooshCompanionRoomEntity companion,
            string phase)
        {
            Vector2 expectedDrawOffset = companion.LinkTextureOffset +
                new Vector2(0, companion.ZFixed >> 8);
            FailIf(
                !_player.CompanionRideActive ||
                _player.CompanionRideZFixed != companion.ZFixed ||
                _player.CompanionRideDrawOffset != expectedDrawOffset,
                $"Mounted Link did not copy Moosh's live Z during {phase} " +
                $"(mooshZ={companion.ZFixed}, " +
                $"linkZ={_player.CompanionRideZFixed}, " +
                $"expectedOffset={expectedDrawOffset}, " +
                $"actualOffset={_player.CompanionRideDrawOffset}).");
        }

        bool MooshCanOccupy(OracleRoomData data, Vector2 position) =>
            mooshCollisionSamples.All(offset =>
            {
                Vector2 sample = position + offset;
                return sample.X >= 0 && sample.X < data.Width &&
                    sample.Y >= 0 && sample.Y < data.Height &&
                    !data.IsSolid(sample);
            });


        bool TryFindMooshHole(
            out int holeGroup,
            out int holeRoom,
            out Vector2 holeCenter,
            out Vector2 safePosition)
        {
            for (int candidateGroup = 0; candidateGroup <= 5; candidateGroup++)
            for (int candidateRoom = 0; candidateRoom <= 0xff; candidateRoom++)
            {
                if (!_world.HasRoom(candidateGroup, candidateRoom))
                    continue;
                OracleRoomData data = _world.LoadRoom(
                    candidateGroup, candidateRoom);
                Vector2? safe = null;
                Vector2? hole = null;
                for (int tileY = 0; tileY < data.HeightInTiles; tileY++)
                for (int tileX = 0; tileX < data.WidthInTiles; tileX++)
                {
                    var center = new Vector2(
                        tileX * OracleRoomData.MetatileSize + 8,
                        tileY * OracleRoomData.MetatileSize + 8);
                    if (!MooshCanOccupy(data, center))
                        continue;
                    HazardType hazard = data.GetTerrainInfo(
                        center + new Vector2(0, 5)).Hazard;
                    if (hazard == HazardType.None && safe is null)
                        safe = center;
                    else if (hazard == HazardType.Hole && hole is null)
                        hole = center;
                }
                if (safe is not null && hole is not null)
                {
                    holeGroup = candidateGroup;
                    holeRoom = candidateRoom;
                    holeCenter = hole.Value;
                    safePosition = safe.Value;
                    return true;
                }
            }
            holeGroup = -1;
            holeRoom = -1;
            holeCenter = Vector2.Zero;
            safePosition = Vector2.Zero;
            return false;
        }

        bool TryFindMooshWaterApproach(
            out int waterGroup,
            out int waterRoom,
            out Vector2 start,
            out Vector2 waterCenter,
            out int direction,
            out string movementAction)
        {
            (Vector2I Offset, int Direction, string Action)[] approaches =
            [
                (Vector2I.Down, 0, "move_up"),
                (Vector2I.Left, 1, "move_right"),
                (Vector2I.Up, 2, "move_down"),
                (Vector2I.Right, 3, "move_left")
            ];
            for (int candidateGroup = 0; candidateGroup <= 5; candidateGroup++)
            for (int candidateRoom = 0; candidateRoom <= 0xff; candidateRoom++)
            {
                if (!_world.HasRoom(candidateGroup, candidateRoom))
                    continue;
                OracleRoomData data = _world.LoadRoom(
                    candidateGroup, candidateRoom);
                for (int tileY = 0; tileY < data.HeightInTiles; tileY++)
                for (int tileX = 0; tileX < data.WidthInTiles; tileX++)
                {
                    var center = new Vector2(
                        tileX * OracleRoomData.MetatileSize + 8,
                        tileY * OracleRoomData.MetatileSize + 8);
                    if (data.GetTerrainInfo(
                            center + new Vector2(0, 5)).Hazard !=
                            HazardType.Water ||
                        !MooshCanOccupy(data, center))
                    {
                        continue;
                    }
                    foreach ((Vector2I offset, int facing, string action) in
                        approaches)
                    {
                        Vector2 candidate = center +
                            (Vector2)offset * OracleRoomData.MetatileSize;
                        if (!MooshCanOccupy(data, candidate) ||
                            data.GetTerrainInfo(
                                candidate + new Vector2(0, 5)).Hazard !=
                                HazardType.None)
                        {
                            continue;
                        }
                        waterGroup = candidateGroup;
                        waterRoom = candidateRoom;
                        start = candidate;
                        waterCenter = center;
                        direction = facing;
                        movementAction = action;
                        return true;
                    }
                }
            }
            waterGroup = -1;
            waterRoom = -1;
            start = Vector2.Zero;
            waterCenter = Vector2.Zero;
            direction = -1;
            movementAction = string.Empty;
            return false;
        }

        Configure(essence: false, sourceFlag: true);
        LoadValidationRoom(group, room);
        FailIf(
            PlacedGhinis().Count != 3 || PlacedGhinis().Any(npc => npc.Active) ||
            _entities.Entities<NpcCharacter>().Any(npc => npc.Record.Id == record.MooshId) ||
            rescue.HasState,
            "Room 0:6c did not suppress all three `$73 Ghinis and `$67 Moosh " +
            "when the second Essence predicate was false.");

        Configure(essence: true, sourceFlag: false);
        LoadValidationRoom(group, room);
        FailIf(
            PlacedGhinis().Any(npc => npc.Active) || rescue.HasState,
            "Room 0:6c ignored wPastRoomFlags+$79 bit `$40 suppression.");

        Configure(essence: true, sourceFlag: true, mooshState: record.RescuedMask);
        LoadValidationRoom(group, room);
        FailIf(
            PlacedGhinis().Any(npc => npc.Active) || rescue.HasState ||
            rescue.ScreenTransitionsDisabled ||
            _entities.Entities<MooshCompanionRoomEntity>().Count != 1,
            "wMooshState bit `$20 did not suppress the rescue controllers while " +
            "retaining `$67:$00's ordinary Moosh spawn.");

        Configure(essence: true, sourceFlag: true);
        LoadValidationRoom(group, room);
        List<NpcCharacter> ghinis = PlacedGhinis();
        NpcCharacter? moosh = rescue.MooshActor;
        FailIf(
            ghinis.Count != 3 || ghinis.Any(npc => !npc.Active) ||
            !ghinis.Select(npc => npc.Record.SubId).SequenceEqual([0, 1, 2]) ||
            ghinis[0].Position != new Vector2(0x68, 0x18) ||
            ghinis[1].Position != new Vector2(0x48, 0x18) ||
            ghinis[2].Position != new Vector2(0x58, 0x38) ||
            ghinis.Any(npc => npc.Record is not
                {
                    SpriteName: "spr_moblin_ghini",
                    TileBase: 0x16,
                    Palette: 2,
                    Implementation: NpcImplementationClassification.EventOwned
                }) ||
            moosh is null || moosh.Position != new Vector2(0x58, 0x28) ||
            moosh.Record.SpriteName != "spr_moosh" ||
            moosh.CurrentAnimationTextureSize.X < 24 ||
            !rescue.HasState || !rescue.ScreenTransitionsDisabled ||
            rescue.Signal != 0,
            "Room 0:6c did not preserve its source-order Ghinis, `$67:$00 Moosh " +
            "preset, visuals, or controller initialization.");

        StepRoomEventFrames(1);
        FailIf(
            !rescue.BlocksGameplay || !_player.CutsceneControlled,
            "The first Ghini lane did not apply setdisabledobjectsto11 on update 1.");

        OracleObjectPosition expected =
            OracleObjectMovement.Shared.PositionFromPixels(new Vector2(0x68, 0x18));
        for (int update = 1; update <= record.GhiniFrames; update++)
        {
            int angle = (record.GhiniAngle + 1 - update) & 0x1f;
            expected = OracleObjectMovement.Shared.ApplySpeed(
                expected, record.GhiniSpeed, angle);
        }
        StepRoomEventFrames(record.GhiniFrames + 1);
        FailIf(
            ghinis[0].Position != expected.PixelPosition || _dialogue.IsOpen,
            "Ghini `$73:$00 did not apply exactly 32 SPEED_140/ANGLE_LEFT " +
            "fixed-point circular updates before its text command.");
        StepRoomEventFrames(1);
        ExpectDialogue(0x1204, "first Ghini taunt");

        _dialogue.Close();
        WaitForDialogue(0x1205, 40, "second Ghini taunt");
        _dialogue.Close();
        WaitForDialogue(0x2200, 70, "frightened Moosh plea");
        _dialogue.Close();
        WaitForDialogue(0x1206, 40, "third Ghini taunt");
        _dialogue.Close();
        WaitForDialogue(0x1207, 45, "fight-start Ghini taunt");
        _dialogue.Close();

        for (int frame = 0; frame < 12 && _entities.RoomEnemyCount != 3; frame++)
            StepRoomEventFrames(1);
        FailIf(
            _entities.RoomEnemyCount != 3 ||
            _entities.Entities<GhiniCharacter>().Count != 3 ||
            ghinis.Any(npc => npc.Active) ||
            (rescue.Signal & 0x10) == 0 || rescue.BlocksGameplay,
            "The three `$73 scripts did not replace themselves in source order " +
            "with ordinary killable `$17:$00 Ghinis and restore player input.");

        _entities.ApplySwordHit(
            new Rect2(-16, -16, 192, 160),
            new Vector2(0x50, 0x48),
            damage: 20);
        for (int frame = 0; frame < 90 && _entities.RoomEnemyCount != 0; frame++)
            StepRoomEventFrames(1);
        FailIf(
            _entities.RoomEnemyCount != 0,
            "The companion lane did not observe the normal room-enemy counter " +
            "after all three rescue Ghinis were defeated.");

        moosh = rescue.MooshActor;
        FailIf(moosh is null, "Moosh disappeared before his post-fight A-button gate.");
        _player.WarpTo(moosh!.Position + Vector2.Down * 10.0f);
        _player.Face(Vector2I.Up);
        for (int frame = 0; frame < 80 &&
             _entities.FindTalkTarget(_player) != moosh; frame++)
        {
            StepRoomEventFrames(1);
        }
        FailIf(
            _entities.FindTalkTarget(_player) != moosh ||
            !_interactions.TryInteract(_player),
            "Moosh did not become A-sensitive after the three SND_DING cues.");
        Vector2 fearLockPosition = _player.PrecisePosition;
        Input.ActionPress("move_down");
        for (int frame = 0; frame < 12; frame++)
        {
            _player._PhysicsProcess(1.0 / 60.0);
            StepRoomEventFrames(1);
        }
        Input.ActionRelease("move_down");
        FailIf(
            !rescue.BlocksGameplay || !_player.CutsceneControlled ||
            _player.PrecisePosition != fearLockPosition,
            "SPECIALOBJECT_MOOSH state `$0a did not retain the input/menu " +
            "lock from the post-fight A-button response through its fear shake.");
        WaitForDialogue(0x2201, 70, "post-combat fear response");
        ulong frightenedMooshPixelHash = moosh.CurrentAnimationPixelHash;
        _dialogue.Close();
        int exclamationClinks =
            _sound.PlayRequestsFor(OracleSoundEngine.SndClink);
        StepRoomEventFrames(1);
        FailIf(
            _entities.Entities<NpcCharacter>().Count(npc =>
                npc.Record.Id == record.ExclamationId && npc.Active) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndClink) !=
                exclamationClinks + 1 ||
            _sound.LastPlayRequestForValidation() !=
                OracleSoundEngine.SndClink ||
            !rescue.BlocksGameplay || !_player.CutsceneControlled,
            "Moosh did not create the 30-update exclamation with SND_CLINK " +
            "and reapply setdisabledobjectsto11 before turning toward Link.");

        StepRoomEventFrames(1);
        FailIf(
            moosh.FacingVector != Vector2I.Down ||
            moosh.CurrentAnimationPixelHash == frightenedMooshPixelHash ||
            moosh.CurrentAnimationTextureSize.X < 24,
            "companionScript_writeAngleTowardLinkToCompanionVar3f did not " +
            "retain Link's locked below-Moosh approach and switch Moosh from " +
            "animation `$00 to direction+1 sprite `$03 after the exclamation " +
            $"(hash={moosh.CurrentAnimationPixelHash}).");

        WaitForDialogue(0x2203, 70, "first Moosh meeting");
        _dialogue.Close();
        for (int frame = 0; frame < 120 && !_dialogue.IsOpen; frame++)
        {
            _player._PhysicsProcess(1.0 / 60.0);
            StepRoomEventFrames(1);
        }
        ExpectDialogue(0x2205, "mounted-companion tutorial");
        MooshCompanionRoomEntity? companion =
            _entities.Entities<MooshCompanionRoomEntity>().SingleOrDefault();
        FailIf(
            (_saveData.ReadWramByte(record.MooshStateAddress) &
                record.RescuedMask) == 0 ||
            companion is null || !companion.Mounted ||
            !_player.CompanionRideActive || !rescue.CompanionMounted ||
            !CompanionRuntimeState.IsActive(
                _runtimeState, CompanionRuntimeState.MooshId) ||
            companion.AnimationIndex != 0x16 ||
            companion.Direction != 3 ||
            companion.LinkAnimationParameter != 0x1e ||
            companion.LinkTexturePixelHash != linkLeft1ePixelHash ||
            _player.CompanionRideTexturePixelHash !=
                companion.LinkTexturePixelHash ||
            _player.CompanionRideTextureOffset !=
                companion.LinkTextureOffset ||
            _player.CompanionRideTextureSize != new Vector2I(16, 16) ||
            _player.Position != companion.Position + new Vector2(0, -16) ||
            rescue.BlocksGameplay || _player.CutsceneControlled,
            "The room-local rescue did not set wMooshState bit `$20 and " +
            "complete companionForceMount into SPECIALOBJECT_MOOSH `$0d " +
            "in the first-meeting left direction with " +
            "SPECIALOBJECT_LINK_RIDING_ANIMAL's source frame/offset " +
            $"(parameter=${companion.LinkAnimationParameter:x2}, " +
            $"hash={companion.LinkTexturePixelHash:x16}, " +
            $"offset={companion.LinkTextureOffset}).");

        int equippedA = _inventory.EquippedA;
        _inventory.EquipA(InventoryState.ItemFeather);
        int tutorialJumpSounds = _sound.PlayRequestsFor(record.JumpSound);
        int textAdvanceGuard = 0;
        while (_dialogue.HasNextMessage && textAdvanceGuard++ < 16)
        {
            _dialogue.RevealCurrentPageForValidation();
            _dialogue.AdvanceOrClose();
            _dialogue.AdvanceTextScrollForValidation(3.0 / 60.0);
        }
        _dialogue.RevealCurrentPageForValidation();

        // The close edge belongs to the textbox update. The following held
        // update has no wGameKeysJustPressed edge and must not become either
        // Moosh's jump or Link's equipped Roc's Feather action.
        StepMooshApplicationInput(
            pressed: ["attack"],
            justPressed: ["attack"],
            closeDialogue: true);
        StepMooshApplicationInput(
            pressed: ["attack"],
            justPressed: []);
        FailIf(
            _dialogue.IsOpen || !_dialogue.BlocksPlayerInput ||
            companion.Phase != MooshCompanionPhase.Riding ||
            _player.TopDownAirborne ||
            _sound.PlayRequestsFor(record.JumpSound) != tutorialJumpSounds,
            "The A press which closed TX_2205 leaked through the textbox " +
            "input lease into Moosh's wGameKeysJustPressed jump or Link's " +
            "equipped Roc's Feather action.");
        StepMooshApplicationInput(pressed: [], justPressed: []);
        FailIf(
            _dialogue.BlocksPlayerInput ||
            companion.Phase != MooshCompanionPhase.Riding,
            "TX_2205 did not release its closing-input lease after A was released.");

        StepMooshApplicationInput(
            pressed: ["attack"],
            justPressed: ["attack"]);
        StepMooshApplicationInput(pressed: [], justPressed: []);
        _inventory.EquipA(equippedA);
        FailIf(
            companion.Phase != MooshCompanionPhase.Airborne ||
            _player.TopDownAirborne ||
            !_player.CompanionRideActive ||
            _player.CompanionRideTexturePixelHash !=
                companion.LinkTexturePixelHash ||
            !rescue.CompanionMounted || rescue.HasState ||
            rescue.ScreenTransitionsDisabled,
            "A fresh post-TX_2205 A edge with Roc's Feather equipped did not " +
            "keep SPECIALOBJECT_LINK_RIDING_ANIMAL's imported frame " +
            "authoritative through Moosh's airborne state, or did not clear " +
            "the rescue script and wDisableScreenTransitions.");
        for (int frame = 0; frame < 90 &&
             companion.Phase != MooshCompanionPhase.Riding; frame++)
        {
            StepRoomEventFrames(1);
        }
        FailIf(
            companion.Phase != MooshCompanionPhase.Riding,
            "Moosh did not land after the TX_2205 airborne completion " +
            "regression.");

        Vector2 movementStart = companion!.Position;
        Input.ActionPress("move_right");
        StepRoomEventFrames(1);
        FailIf(
            companion.Position != movementStart ||
            companion.Direction != 1 || companion.AnimationIndex != 0x14 ||
            companion.LinkAnimationParameter != 0x1c ||
            companion.LinkTexturePixelHash != linkRight1cPixelHash ||
            _player.CompanionRideTexturePixelHash !=
                companion.LinkTexturePixelHash ||
            _player.Position != companion.Position + new Vector2(0, -16),
            "Mounted Moosh did not consume the first changed-angle update " +
            "selecting right animation `$14 and Link riding frame `$1c " +
            "without applying SPEED_100 movement " +
            $"(parameter=${companion.LinkAnimationParameter:x2}, " +
            $"hash={companion.LinkTexturePixelHash:x16}).");
        StepRoomEventFrames(7);
        FailIf(
            companion.Position.X <= movementStart.X ||
            companion.Direction != 1 || companion.AnimationIndex != 0x14 ||
            companion.LinkAnimationParameter != 0x1c ||
            !_player.CompanionRideActive,
            "Mounted Moosh did not preserve Link riding frame `$1c for the " +
            "first seven SPEED_100 animation updates.");
        StepRoomEventFrames(1);
        FailIf(
            companion.LinkAnimationParameter != 0x20 ||
            companion.LinkTexturePixelHash != linkRight20PixelHash ||
            companion.LinkTextureOffset != new Vector2(-8, -7) ||
            _player.CompanionRideTexturePixelHash !=
                companion.LinkTexturePixelHash,
            "Moosh animation `$14 did not select Link riding frame `$20 on " +
            "its exact eighth animation update " +
            $"(parameter=${companion.LinkAnimationParameter:x2}, " +
            $"hash={companion.LinkTexturePixelHash:x16}).");

        Input.ActionRelease("move_right");
        Input.ActionPress("move_up");
        StepRoomEventFrames(1);
        FailIf(
            companion.Direction != 0 || companion.AnimationIndex != 0x13 ||
            companion.LinkAnimationParameter != 0x1b,
            "A cardinal up-angle change did not select Moosh animation `$13 " +
            "and Link riding frame `$1b.");
        Vector2 diagonalStart = companion.Position;
        Input.ActionPress("move_right");
        StepRoomEventFrames(1);
        FailIf(
            companion.Position != diagonalStart || companion.Angle != 0x04 ||
            companion.Direction != 0 || companion.AnimationIndex != 0x13 ||
            companion.LinkAnimationParameter != 0x1b,
            "updateLinkDirectionFromAngle did not retain up-facing for an " +
            "up-right diagonal or consumed movement on the changed angle.");
        Input.ActionRelease("move_up");
        StepRoomEventFrames(1);
        FailIf(
            companion.Direction != 1 || companion.AnimationIndex != 0x14 ||
            companion.LinkAnimationParameter != 0x1c,
            "Returning from the sticky diagonal to right did not restore " +
            "Moosh animation `$14 and Link riding frame `$1c.");
        Input.ActionRelease("move_right");

        int jumpSounds = _sound.PlayRequestsFor(record.JumpSound);
        StepMooshApplicationInput(
            pressed: ["attack"],
            justPressed: ["attack"]);
        FailIf(
            companion.Phase != MooshCompanionPhase.Airborne ||
            companion.SpeedZ != 0 || companion.AnimationIndex != 0x14 ||
            _sound.PlayRequestsFor(record.JumpSound) != jumpSounds + 1,
            "Moosh's A press did not enter state `$08/substate `$00 with " +
            "SND_JUMP while retaining the riding frame for that update.");
        StepMooshApplicationInput(pressed: [], justPressed: []);
        FailIf(
            companion.SpeedZ != -0x140 || companion.AnimationIndex != 0x0a ||
            companion.LinkAnimationParameter != 0x24,
            "Moosh state `$08/substate `$00 did not initialize speedZ=-$0140 " +
            "and direction-indexed hover animation `$0a on the next update.");
        ExpectMountedLinkAttached(companion, "jump initialization");

        Input.ActionPress("move_right");
        StepRoomEventFrames(3);
        ExpectMountedLinkAttached(companion, "upward hover");
        FailIf(
            companion.LinkAnimationParameter != 0x24 ||
            _player.CompanionRideTexturePixelHash !=
                companion.LinkTexturePixelHash,
            "Airborne movement advanced Moosh/Link's shared four-update " +
            "hover frame more than once per update.");
        StepRoomEventFrames(1);
        Input.ActionRelease("move_right");
        ExpectMountedLinkAttached(companion, "hover-frame transition");
        FailIf(
            companion.LinkAnimationParameter != 0x28 ||
            _player.CompanionRideTexturePixelHash !=
                companion.LinkTexturePixelHash,
            "Moosh/Link's shared hover animation did not change from frame " +
            "`$24 to `$28 on its exact fourth update.");
        for (int frame = 0; frame < 90 &&
             companion.Phase != MooshCompanionPhase.Riding; frame++)
        {
            StepRoomEventFrames(1);
            ExpectMountedLinkAttached(companion, "jump descent");
        }
        FailIf(
            companion.Phase != MooshCompanionPhase.Riding,
            "An uncharged Moosh hover did not land back in state `$05 " +
            $"(phase={companion.Phase}, z={companion.ZFixed}, " +
            $"speedZ={companion.SpeedZ}, charge={companion.ChargeCounter}, " +
            $"flaps={companion.FlapCount}).");

        Vector2 dismountStart = companion.Position;
        RememberedCompanion rememberedBeforeDismount =
            CompanionRuntimeState.ReadRemembered(_runtimeState);
        Vector2 localRespawnBeforeDismount = _player.LocalRespawnPosition;
        int dismountJumpSounds =
            _sound.PlayRequestsFor(record.JumpSound);
        int dismountLandSounds =
            _sound.PlayRequestsFor(TopDownAirDatabase.Shared.Parameters.LandSound);
        FailIf(
            !companion.TryBeginDismount(_player),
            "Mounted Moosh rejected the B-button dismount action.");
        FailIf(
            companion.Phase != MooshCompanionPhase.Dismounting ||
            _player.CompanionJumpActive ||
            !_player.CompanionRideActive ||
            !CompanionRuntimeState.IsActive(
                _runtimeState, CompanionRuntimeState.MooshId) ||
            CompanionRuntimeState.ReadRemembered(_runtimeState) !=
                rememberedBeforeDismount ||
            _player.LocalRespawnPosition != localRespawnBeforeDismount,
            "The mounted B-button update did not exclusively enter Moosh " +
            "state $06/substate $00.");

        StepRoomEventFrames(1);
        Vector2I expectedRespawnFacing = companion.Direction switch
        {
            0 => Vector2I.Up,
            1 => Vector2I.Right,
            2 => Vector2I.Down,
            _ => Vector2I.Left
        };
        FailIf(
            companion.Phase != MooshCompanionPhase.Dismounting ||
            !_player.CompanionJumpActive ||
            _player.CompanionRideActive ||
            _player.PrecisePosition != dismountStart ||
            _player.TopDownAirZ != -8 ||
            _player.TopDownAirSpeedZ != -0x01c0 ||
            CompanionRuntimeState.IsActive(
                _runtimeState, CompanionRuntimeState.MooshId) ||
            !CompanionRuntimeState.TryGetRemembered(
                _runtimeState,
                CompanionRuntimeState.MooshId,
                group,
                room,
                out Vector2 remembered) ||
            remembered != companion.Position ||
            _player.LocalRespawnPosition != dismountStart ||
            _player.LocalRespawnFacingVector != expectedRespawnFacing ||
            _sound.PlayRequestsFor(record.JumpSound) != dismountJumpSounds,
            "Moosh state $06/substate $00 did not copy position, set zh=$f8/" +
            "speedZ=-$01c0, save both local anchors, or defer SND_JUMP.");

        _player._PhysicsProcess(1.0 / 60.0);
        FailIf(
            _player.PrecisePosition != dismountStart ||
            _player.TopDownAirZ != -10 ||
            _player.TopDownAirSpeedZ != -0x01a0 ||
            _sound.PlayRequestsFor(record.JumpSound) !=
                dismountJumpSounds + 1,
            "Link's first post-dismount air update did not remain at Moosh's " +
            "copied Y/X while applying gravity and SND_JUMP.");
        StepRoomEventFrames(1);
        for (int frame = 0; frame < 90 && _player.CompanionJumpActive; frame++)
        {
            _player._PhysicsProcess(1.0 / 60.0);
            StepRoomEventFrames(1);
        }
        FailIf(
            companion.Phase != MooshCompanionPhase.Dismounting ||
            _player.PrecisePosition != dismountStart ||
            LinkMooshManhattanDistance(companion) != 0 ||
            _sound.PlayRequestsFor(
                TopDownAirDatabase.Shared.Parameters.LandSound) !=
                    dismountLandSounds + 1,
            "companionDismount did not land vertically in place while Moosh " +
            "state $06/substate $01 retained its source-order landing delay.");

        _player._PhysicsProcess(1.0 / 60.0);
        StepRoomEventFrames(1);
        FailIf(
            companion.Phase != MooshCompanionPhase.AwaitingDistance,
            "Moosh state $06/substate $01 did not advance exactly one update " +
            "after Link cleared wLinkInAir.");
        _player._PhysicsProcess(1.0 / 60.0);
        StepRoomEventFrames(1);
        FailIf(
            companion.Phase != MooshCompanionPhase.AwaitingDistance,
            "Moosh skipped the state $06/substate $02 in-range hazard pass.");

        Input.ActionPress("move_left");
        for (int update = 0; update < 9; update++)
        {
            _player._PhysicsProcess(1.0 / 60.0);
            StepRoomEventFrames(1);
        }
        FailIf(
            companion.Phase != MooshCompanionPhase.AwaitingDistance ||
            LinkMooshManhattanDistance(companion) != 9,
            "Moosh observed Link crossing c=$09 after Link's object pass " +
            "instead of on the following companion-first update.");
        _player._PhysicsProcess(1.0 / 60.0);
        StepRoomEventFrames(1);
        Input.ActionRelease("move_left");
        FailIf(
            companion.Phase != MooshCompanionPhase.Waiting ||
            LinkMooshManhattanDistance(companion) != 10,
            "Moosh state $06/substate $02 did not reopen state $01 on the " +
            "first source-order update outside the strict c=$09 radius.");

        _deathRespawnPoints.RecordCurrentPoint();
        var restoredCompanionRuntime = new OracleRuntimeState();
        CompanionRuntimeState.RestoreRememberedFromDeathRespawn(
            restoredCompanionRuntime, _saveData);
        FailIf(
            _saveData.ReadWramByte(
                OracleSaveData.RespawnRememberedCompanionIdAddress) !=
                    CompanionRuntimeState.MooshId ||
            _saveData.ReadWramByte(
                OracleSaveData.RespawnRememberedCompanionGroupAddress) != group ||
            _saveData.ReadWramByte(
                OracleSaveData.RespawnRememberedCompanionRoomAddress) != room ||
            _saveData.ReadWramByte(
                OracleSaveData.RespawnLinkObjectIndexAddress) != 0xd0 ||
            _saveData.ReadWramByte(
                OracleSaveData.RespawnRememberedCompanionYAddress) !=
                    Mathf.FloorToInt(companion.Position.Y) ||
            _saveData.ReadWramByte(
                OracleSaveData.RespawnRememberedCompanionXAddress) !=
                    Mathf.FloorToInt(companion.Position.X) ||
            !CompanionRuntimeState.TryGetRemembered(
                restoredCompanionRuntime,
                CompanionRuntimeState.MooshId,
                group,
                room,
                out Vector2 restoredPosition) ||
            restoredPosition != companion.Position,
            "setDeathRespawnPoint did not copy and restore Moosh's remembered " +
            "ID/group/room/Y/X state with wLinkObjectIndex=$d0 after dismount.");

        Vector2 remountApproachStart = _player.PrecisePosition;
        Input.ActionPress("move_right");
        for (int frame = 0; frame < 60 &&
             companion.Phase == MooshCompanionPhase.Waiting; frame++)
        {
            _player._PhysicsProcess(1.0 / 60.0);
            StepRoomEventFrames(1);
        }
        Input.ActionRelease("move_right");
        FailIf(
            companion.Phase != MooshCompanionPhase.Mounting ||
            _player.PrecisePosition == remountApproachStart ||
            LinkMooshManhattanDistance(companion) >= 9,
            "Link could not physically walk back through Moosh's former " +
            "blocking body and trigger objectCheckLinkWithinDistance c=$09.");
        for (int frame = 0; frame < 120 && !companion.Mounted; frame++)
        {
            _player._PhysicsProcess(1.0 / 60.0);
            StepRoomEventFrames(1);
        }
        FailIf(
            !companion.Mounted || !_player.CompanionRideActive,
            "Remembered Moosh did not complete a second physical mount after " +
            "Link first moved beyond and then walked back inside the " +
            "nine-pixel Manhattan gate.");

        int chargeSounds = _sound.PlayRequestsFor(record.ChargeSound);
        int stompSounds = _sound.PlayRequestsFor(record.StompSound);
        StepMooshApplicationInput(
            pressed: ["attack"],
            justPressed: ["attack"]);
        for (int frame = 0; frame < 100 &&
             companion.Phase != MooshCompanionPhase.Charging; frame++)
        {
            StepMooshApplicationInput(
                pressed: ["attack"],
                justPressed: []);
        }
        FailIf(
            companion.Phase != MooshCompanionPhase.Charging ||
            companion.ChargeCounter != 10,
            "Holding A on descent did not enter Moosh's charge substate on " +
            "the exact ten-update threshold.");
        for (int frame = 0; frame < 40 && companion.ChargeCounter < 40; frame++)
        {
            StepMooshApplicationInput(
                pressed: ["attack"],
                justPressed: []);
        }
        FailIf(
            companion.ChargeCounter != 40 ||
            companion.ChargePaletteActive ||
            companion.MooshTexturePixelHash !=
                companion.NormalMooshTexturePixelHash ||
            companion.LinkTexturePixelHash !=
                companion.NormalLinkTexturePixelHash ||
            _sound.PlayRequestsFor(record.ChargeSound) != chargeSounds + 1,
            "Moosh's charged stomp did not emit SND_CHARGE_SWORD at 40 held " +
            "updates while deferring its palette flash until the following update.");
        bool sawChargePalette = false;
        bool sawRestoredPalette = false;
        for (int update = 0; update < 8; update++)
        {
            StepMooshApplicationInput(
                pressed: ["attack"],
                justPressed: []);
            bool expectedChargePalette = (_entities.FrameCounter & 0x04) == 0;
            sawChargePalette |= expectedChargePalette;
            sawRestoredPalette |= !expectedChargePalette;
            FailIf(
                companion.ChargePaletteActive != expectedChargePalette ||
                (companion.MooshTexturePixelHash !=
                    companion.NormalMooshTexturePixelHash) !=
                        expectedChargePalette ||
                (companion.LinkTexturePixelHash !=
                    companion.NormalLinkTexturePixelHash) !=
                        expectedChargePalette ||
                _player.CompanionRideTexturePixelHash !=
                    companion.LinkTexturePixelHash,
                "companionFlashFromChargingAnimation did not apply palette " +
                "`$02 to both Moosh and mounted Link in global-frame bit-2 bands.");
        }
        FailIf(
            !sawChargePalette || !sawRestoredPalette,
            "The eight-update Moosh charge sample did not cover both palette bands.");
        StepMooshApplicationInput(pressed: [], justPressed: []);
        FailIf(
            companion.ChargePaletteActive ||
            companion.MooshTexturePixelHash !=
                companion.NormalMooshTexturePixelHash ||
            companion.LinkTexturePixelHash !=
                companion.NormalLinkTexturePixelHash,
            "Releasing Moosh's charged stomp did not restore both original palettes.");
        for (int frame = 0; frame < 90 &&
             companion.Phase != MooshCompanionPhase.StompRecovery; frame++)
        {
            StepRoomEventFrames(1);
        }
        FailIf(
            companion.Phase != MooshCompanionPhase.StompRecovery ||
            _sound.PlayRequestsFor(record.StompSound) != stompSounds + 1 ||
            _entities.ScreenShakeCounter <= 0 ||
            _entities.Entities<MooshStompAttackRoomEntity>().Count != 1,
            "A charged Moosh landing did not stop SFX, play SND_SCENT_SEED, " +
            "shake for `$0f updates, and allocate ITEM_28.");
        for (int frame = 0; frame < 180 &&
             companion.Phase != MooshCompanionPhase.Riding; frame++)
        {
            StepRoomEventFrames(1);
        }
        FailIf(
            companion.Phase != MooshCompanionPhase.Riding,
            "Moosh's charged-stomp animation did not terminate on its imported " +
            "animation-parameter bit 7.");

        companion.SetScreenTransitionBoundaryCoordinate(
            horizontal: false,
            coordinate: record.RestrictY + 1,
            _player);
        Input.ActionPress("move_down");
        StepRoomEventFrames(1);
        Input.ActionRelease("move_down");
        FailIf(
            companion.Position.Y != record.RestrictY ||
            !_dialogue.IsOpen ||
            _dialogue.CurrentMessage !=
                DialogueBox.PlainText(record.RestrictText),
            "INTERAC_COMPANION_SCRIPTS `$71:$02 did not clamp mounted Moosh " +
            "at Y=$6d and show TX_2209.");
        _dialogue.Close();

        Vector2I scrollDirection = Vector2I.Right;
        if (!_rooms.TryGetNeighbor(scrollDirection, out int scrollTarget))
        {
            throw new InvalidOperationException(
                "Room 0:6c has no source-derived right neighbor for the " +
                "mounted-companion scrolling regression.");
        }
        _transitions.BeginScroll(_player, scrollDirection, scrollTarget);
        FailIf(
            _entities.OutgoingEntities<MooshCompanionRoomEntity>()
                .SingleOrDefault() != companion ||
            _entities.Entities<MooshCompanionRoomEntity>().Count != 0,
            "Mounted Moosh was duplicated or lost when the destination room " +
            "was parsed for ordinary scrolling.");
        for (int frame = 0; frame < 60 && _transitions.ScrollActive; frame++)
        {
            _transitions.UpdateScroll(1.0 / 60.0);
            _entities.Update(1.0 / 60.0, _player);
        }
        ActiveCompanion scrolledCompanion =
            CompanionRuntimeState.Read(_runtimeState);
        FailIf(
            _transitions.ScrollActive ||
            _rooms.CurrentRoom.Id != scrollTarget ||
            _entities.Entities<MooshCompanionRoomEntity>()
                .SingleOrDefault() != companion ||
            scrolledCompanion.Room != scrollTarget ||
            !_player.CompanionRideActive,
            "Mounted Moosh did not retain the single w1Companion owner and " +
            "mounted Link presentation across an ordinary screen scroll.");
        if (!_rooms.TryGetNeighbor(Vector2I.Left, out int returnTarget) ||
            returnTarget != room)
        {
            throw new InvalidOperationException(
                $"Room 0:{scrollTarget:x2} did not resolve room 0:6c as its " +
                "source-derived left neighbor.");
        }
        _transitions.BeginScroll(_player, Vector2I.Left, returnTarget);
        FailIf(
            _entities.OutgoingEntities<MooshCompanionRoomEntity>()
                .SingleOrDefault() != companion ||
            _entities.Entities<MooshCompanionRoomEntity>().Count != 0,
            "Preloading completed room 0:6c spawned a waiting duplicate while " +
            "the mounted w1Companion Moosh was retained in the outgoing set.");
        for (int frame = 0; frame < 60 && _transitions.ScrollActive; frame++)
        {
            _transitions.UpdateScroll(1.0 / 60.0);
            _entities.Update(1.0 / 60.0, _player);
        }
        FailIf(
            _transitions.ScrollActive || _rooms.CurrentRoom.Id != room ||
            CompanionRuntimeState.Read(_runtimeState).Room != room,
            "Mounted Moosh did not return through the source-derived ordinary " +
            "screen-scroll neighbor link.");

        Configure(
            essence: true,
            sourceFlag: true,
            mooshState: _saveData.ReadWramByte(record.MooshStateAddress));
        LoadValidationRoom(group, room);
        int reentryMooshCount =
            _entities.Entities<MooshCompanionRoomEntity>().Count;
        int reentryActiveGhiniCount = PlacedGhinis().Count(npc => npc.Active);
        FailIf(
            reentryActiveGhiniCount != 0 || reentryMooshCount != 1 ||
            rescue.HasState || rescue.ScreenTransitionsDisabled,
            "Completed room 0:6c re-entry did not suppress all rescue Ghinis " +
            "and retain the live SPECIALOBJECT_MOOSH companion " +
            $"(ghinis={reentryActiveGhiniCount}, moosh={reentryMooshCount}, " +
            $"state={rescue.HasState}, transitions={rescue.ScreenTransitionsDisabled}, " +
            $"wMooshState=${_saveData.ReadWramByte(record.MooshStateAddress):x2}, " +
            $"essences=${_saveData.ReadWramByte(record.EssenceAddress):x2}, " +
            $"flag={_saveData.HasRoomFlag(record.FlagGroup, record.FlagRoom, (byte)record.FlagMask)}, " +
            $"rope={_inventory.HasTreasure(record.ChevalRopeTreasure)}).");

        FailIf(
            !TryFindMooshHole(
                out int holeGroup,
                out int holeRoom,
                out Vector2 holeCenter,
                out Vector2 safePosition),
            "Could not find a canonical hole and companion-safe local respawn.");
        CompanionRuntimeState.Clear(
            _runtimeState, CompanionRuntimeState.MooshId);
        CompanionRuntimeState.Remember(
            _runtimeState, 0, 0, 0, Vector2.Zero);
        LoadValidationRoom(holeGroup, holeRoom);
        _player.WarpTo(safePosition);
        _player.Heal(_player.MaxHealthQuarters);
        var holeCompanion = _entities.Spawn<MooshCompanionRoomEntity>(
            new MooshCompanionSpawn(
                holeCenter,
                2,
                holeGroup,
                holeRoom,
                Riding: true));
        CompanionRuntimeState.Begin(
            _runtimeState,
            CompanionRuntimeState.MooshId,
            holeRoom,
            holeCenter,
            2);
        int splashSounds =
            _sound.PlayRequestsFor(OracleSoundEngine.SndSplash);
        int fallSounds =
            _sound.PlayRequestsFor(OracleSoundEngine.SndLinkFall);
        int healthBeforeHole = _player.HealthQuarters;
        StepRoomEventFrames(1);
        FailIf(
            holeCompanion.Phase != MooshCompanionPhase.HazardFalling ||
            holeCompanion.Hazard != HazardType.Hole ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSplash) !=
                splashSounds + 1,
            "Grounded Moosh did not enter state `$04 from the source y+$05 " +
            "hole probe and request SND_SPLASH.");
        StepRoomEventFrames(1);
        FailIf(
            holeCompanion.AnimationIndex != 0x0e ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndLinkFall) !=
                fallSounds + 1,
            "Centered Moosh did not select falling animation `$0e and play " +
            "SND_LINK_FALL.");
        for (int frame = 0; frame < 180 &&
            holeCompanion.Phase == MooshCompanionPhase.HazardFalling; frame++)
        {
            StepRoomEventFrames(1);
        }
        FailIf(
            holeCompanion.Phase != MooshCompanionPhase.Riding ||
            holeCompanion.Position != safePosition ||
            !_player.CompanionRideActive ||
            _player.HealthQuarters >= healthBeforeHole ||
            _player.InvincibilityFrames != 0x40,
            "Moosh's hole animation did not respawn the mounted pair at the " +
            "local safe point with companionRespawn's damage/invincibility state.");

        FailIf(
            !TryFindMooshWaterApproach(
                out int waterGroup,
                out int waterRoom,
                out Vector2 waterStart,
                out Vector2 waterCenter,
                out int waterDirection,
                out string waterMovementAction),
            "Could not find imported companion-safe floor adjacent to water.");
        CompanionRuntimeState.Clear(
            _runtimeState, CompanionRuntimeState.MooshId);
        CompanionRuntimeState.Remember(
            _runtimeState, 0, 0, 0, Vector2.Zero);
        LoadValidationRoom(waterGroup, waterRoom);
        _player.WarpTo(waterStart);
        var waterCompanion = _entities.Spawn<MooshCompanionRoomEntity>(
            new MooshCompanionSpawn(
                waterStart,
                waterDirection,
                waterGroup,
                waterRoom,
                Riding: true));
        CompanionRuntimeState.Begin(
            _runtimeState,
            CompanionRuntimeState.MooshId,
            waterRoom,
            waterStart,
            waterDirection);
        StepRoomEventFrames(1);
        int waterClinks =
            _sound.PlayRequestsFor(OracleSoundEngine.SndClink);
        StepMooshApplicationInput(
            pressed: ["attack"],
            justPressed: ["attack"]);
        Input.ActionPress(waterMovementAction);
        StepMooshApplicationInput(
            pressed: [waterMovementAction],
            justPressed: []);
        for (int frame = 0; frame < 60 &&
            waterCompanion.Phase !=
                MooshCompanionPhase.HoveringOverWater; frame++)
        {
            StepRoomEventFrames(1);
        }
        Input.ActionRelease(waterMovementAction);
        NpcCharacter? waterExclamation =
            _entities.Entities<NpcCharacter>().SingleOrDefault(npc =>
                npc.Name == "MooshHoverExclamation");
        FailIf(
            waterCompanion.Phase !=
                MooshCompanionPhase.HoveringOverWater ||
            waterCompanion.WaterHoverCounter != 60 ||
            waterCompanion.SpeedZ != 0 || waterCompanion.ZFixed >= 0 ||
            _rooms.CurrentRoom.GetTerrainInfo(
                waterCompanion.Position + new Vector2(0, 5)).Hazard !=
                HazardType.Water ||
            waterExclamation is null ||
            waterExclamation.Position != waterCompanion.Position +
                new Vector2(
                    0,
                    (waterCompanion.ZFixed >> 8) - 32) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndClink) !=
                waterClinks + 1,
            $"Moosh did not freeze above imported water and create the " +
            $"Z-$20/$3c-update SND_CLINK exclamation (room=" +
            $"{waterGroup:x1}:{waterRoom:x2}, start={waterStart}, " +
            $"water={waterCenter}, position={waterCompanion.Position}).");
        StepRoomEventFrames(59);
        FailIf(
            waterCompanion.WaterHoverCounter != 1 ||
            !_entities.Entities<NpcCharacter>().Any(npc =>
                npc.Name == "MooshHoverExclamation"),
            "Moosh's water hover or exclamation ended before `$3c updates.");
        StepRoomEventFrames(1);
        FailIf(
            waterCompanion.WaterHoverCounter != 0 ||
            waterCompanion.SpeedZ != 0x10 ||
            _entities.Entities<NpcCharacter>().Any(npc =>
                npc.Name == "MooshHoverExclamation"),
            "Moosh did not remove the exclamation and resume gravity on the " +
            "exact `$3c counter boundary.");
        int waterSplashes =
            _sound.PlayRequestsFor(OracleSoundEngine.SndSplash);
        for (int frame = 0; frame < 120 &&
            waterCompanion.Phase != MooshCompanionPhase.HazardFalling; frame++)
        {
            StepRoomEventFrames(1);
        }
        FailIf(
            waterCompanion.Phase != MooshCompanionPhase.HazardFalling ||
            waterCompanion.Hazard != HazardType.Water ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSplash) !=
                waterSplashes + 1,
            "Moosh's stationary post-exclamation descent did not enter the " +
            "ordinary water hazard state on landing.");

        GD.Print(
            "Validated room 0:6c source predicates, three ordered `$73 Ghini " +
            "lanes, exact 32-update circular motion, TX_1204/1205/2200/1206/1207 " +
            "taunt order, `$17:$00 combat replacement, room-enemy gate, three " +
            "SND_DING cues, SND_CLINK exclamation, Moosh A-button/shake sequence, " +
            "direction+1 rescue sprite, first-meeting left forced mount, " +
            "exact Link riding-frame " +
            "hashes, live XYZ jump attachment, sticky diagonal facing, " +
            "TX_2205 close-edge consumption, mounted item-pose suppression, " +
            "SPEED_100 movement, four-update hover cadence/landing, charged " +
            "ITEM_28 stomp, delayed vertical $ff-angle dismount, local/" +
            "death-buffer-backed dismount memory, source-ordered strict-" +
            "Manhattan walk-away/remount gate, charge palette " +
            "flash, source-probed hole fall/respawn, `$3c water-hover " +
            "exclamation/descent, locked post-fight fear shake, retained-set " +
            "scroll deduplication, " +
            "TX_2209 restriction, scrolling retention, and mounted completed " +
            "re-entry.");
    }

    private void StepMooshApplicationInput(
        IReadOnlyList<string> pressed,
        IReadOnlyList<string> justPressed,
        bool closeDialogue = false)
    {
        Vector2 movement = new(
            (pressed.Contains("move_right") ? 1.0f : 0.0f) -
                (pressed.Contains("move_left") ? 1.0f : 0.0f),
            (pressed.Contains("move_down") ? 1.0f : 0.0f) -
                (pressed.Contains("move_up") ? 1.0f : 0.0f));
        if (movement.LengthSquared() > 1.0f)
            movement = movement.Normalized();
        Input.BeginOriginalUpdate(new ApplicationInputSnapshot(
            pressed,
            justPressed,
            movement));
        try
        {
            _player.AdvanceApplicationUpdate();
            _entities.Update(1.0 / 60.0, _player);
            _roomEvents.Update(1.0 / 60.0);
            _interactions.Update(1.0 / 60.0, _player);
            if (closeDialogue)
                _dialogue.AdvanceOrClose();
            else
                _dialogue.AdvanceApplicationUpdate();
            _sound.Tick();
        }
        finally
        {
            Input.EndOriginalUpdate();
        }
    }
}
