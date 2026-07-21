using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateNewGameIntro()
    {
        var screen = new NewGameIntroScreen { Name = "NewGameIntroValidation" };
        AddChild(screen);
        int completionRequests = 0;
        var intro = new NewGameIntroController(
            screen, () => completionRequests++, _sound);
        NewGameIntroDatabase.NewGameIntroRecord record = intro.Record;
        var introDatabase = new NewGameIntroDatabase();
        NewGameIntroDatabase.IntroSpriteFrame[] linkSpin =
            introDatabase.SpriteFrames("link-spin");
        NewGameIntroDatabase.IntroSpriteFrame[] linkVanish =
            introDatabase.SpriteFrames("link-vanish");
        NewGameIntroDatabase.IntroSpriteFrame[] linkArrival =
            introDatabase.SpriteFrames("link-arrival");
        NewGameIntroDatabase.IntroSpriteFrame[] orbDescend =
            introDatabase.SpriteFrames("orb-descend");
        NewGameIntroDatabase.IntroSpriteFrame[] orbVanish =
            introDatabase.SpriteFrames("orb-vanish");
        int descendFrames = record.InitialWaitFrames + record.VoiceWaitFrames;
        int fairySoundRequests =
            _sound.PlayRequestsFor(OracleSoundEngine.SndFairyCutscene);
        OracleSoundData.ChannelStart fairySoundChannel =
            _sound.Data.ChannelsFor(OracleSoundEngine.SndFairyCutscene).Single();

        if (_sound.ActiveMusic != OracleSoundEngine.MusEssenceRoom)
            throw new InvalidOperationException(
                "Pregame state $0a did not start MUS_ESSENCE_ROOM for the blue-orb descent.");

        if (record.InitialWaitFrames != 300 || record.VoiceWaitFrames != 60 ||
            record.PostVanishWaitFrames != 60 || record.SummonFrames != 128 ||
            record.LinkX != 0x50 || record.LinkY != 0xd0 ||
            record.LinkSummonedFlag != OracleSaveData.GlobalFlagLinkSummoned ||
            record.PregameIntroDoneFlag != OracleSaveData.GlobalFlagPregameIntroDone ||
            record.TextId != 0x1213 || record.TextPosition != 2 ||
            record.SpinFrameDuration != 4 || record.SpinGraphics.Length != 8 ||
            record.VanishDurations.Length != 4 || record.VanishGraphics.Length != 4 ||
            record.DescendOscillation.Length != 8 ||
            record.DescendOscillation[6] != -1 ||
            record.HoverOscillation.Length != 8 ||
            !record.HoverOscillation.SequenceEqual(new[] { -1, -1, -1, 0, 1, 1, 1, 0 }) ||
            NewGameIntroScreen.FirstVisibleLinkFrameForValidation(
                record.LinkY,
                descendFrames,
                record.DescendOscillation,
                record.HoverOscillation) != 96 ||
            NewGameIntroScreen.LinkZForValidation(
                descendFrames,
                descendFrames,
                record.DescendOscillation,
                record.HoverOscillation) != 77 ||
            NewGameIntroScreen.LinkZForValidation(
                descendFrames + 64,
                descendFrames,
                record.DescendOscillation,
                record.HoverOscillation) != 77 ||
            CutsceneSpriteRenderer.SourcePixelForValidation(0x0900, 0x00, 0, 0) !=
                new Vector2I(64, 64) ||
            CutsceneSpriteRenderer.SourcePixelForValidation(0x1c00, 0x12, 7, 15) !=
                new Vector2I(79, 239) ||
            intro.TotalVanishFrames != 62 ||
            NewGameIntroController.ArrivalFadeWaitFrames != 65 ||
            Player.NewGameSlowFallInitialZ(0x48) != -0x50 ||
            Player.NewGameSlowFallInitialZ(0x90) != -0x80 ||
            Player.NewGameSlowFallZForValidation(0x48, 58) != -3 ||
            Player.NewGameSlowFallZForValidation(0x48, 59) != 0 ||
            RoomView.WaveOffsetForValidation(0xff, 0x1f) != 0xfe ||
            RoomView.WaveOffsetForValidation(0xff, 0x5f) != -0xfe)
        {
            throw new InvalidOperationException(
                "Imported CUTSCENE_PREGAME_INTRO data diverged from the disassembly.");
        }

        if (linkSpin.Length != 8 || linkVanish.Length != 4 || linkArrival.Length != 3 ||
            orbDescend.Length != 2 || orbVanish.Length != 4 ||
            linkSpin.Any(frame => frame.Duration != 4 || frame.BasePalette != 0) ||
            linkSpin[0].Parts.Length != 2 || linkVanish[1].Parts.Length != 1 ||
            !linkArrival.Select(frame => frame.Duration).SequenceEqual(new[] { 4, 4, 4 }) ||
            !linkArrival.Select(frame => frame.SourceOffset)
                .SequenceEqual(new[] { 0x0c40, 0x0c00, 0x0c20 }) ||
            !linkArrival.Select(frame => frame.Parts.Length)
                .SequenceEqual(new[] { 2, 2, 2 }) ||
            linkArrival.Any(frame => frame.BasePalette != 0) ||
            linkArrival
                .Any(frame => frame.Parts.Any(part => (part.Flags & 0x07) != 0)) ||
            orbDescend[0].SourceOffset != 0x1c00 ||
            orbDescend[0].Parts.Length != 18 || orbDescend[1].Parts.Length != 14 ||
            orbDescend.Any(frame => frame.BasePalette != 0 ||
                frame.Parts.Any(part => (part.Flags & 0x07) != 4)) ||
            !orbVanish.Select(frame => frame.Duration)
                .SequenceEqual(new[] { 30, 18, 1, 1 }) ||
            !orbVanish.Select(frame => frame.Parts.Length)
                .SequenceEqual(new[] { 6, 2, 2, 2 }) ||
            orbVanish.Any(frame => frame.SourceOffset != 0x1d40 ||
                frame.BasePalette != 4))
        {
            throw new InvalidOperationException(
                "Imported pregame Link/orb/slow-fall graphics, OAM, palettes, or animation frames diverged.");
        }

        for (int frame = 0; frame < intro.TotalVoiceWaitFrames - 1; frame++)
            intro.Update(1.0 / 60.0);
        if (intro.CurrentStage != NewGameIntroController.Stage.WaitingForVoice ||
            intro.StageFrame != intro.TotalVoiceWaitFrames - 1 ||
            screen.Dialogue.IsOpen)
        {
            throw new InvalidOperationException(
                "TX_1213 opened before the original 300+60 update wait.");
        }
        intro.Update(1.0 / 60.0);
        if (intro.CurrentStage != NewGameIntroController.Stage.Dialogue ||
            intro.StageFrame != 0 ||
            !screen.Dialogue.IsOpen ||
            screen.Dialogue.CurrentMessage != "Accept our\nquest, hero!" ||
            screen.Dialogue.Position.Y != 80 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndFairyCutscene) != fairySoundRequests)
        {
            throw new InvalidOperationException(
                "CUTSCENE_PREGAME_INTRO did not open TX_1213 at position 2 " +
                "without starting SND_FAIRYCUTSCENE early.");
        }

        screen.Dialogue.Close();
        intro.Update(1.0 / 60.0);
        if (intro.CurrentStage != NewGameIntroController.Stage.Vanishing ||
            intro.StageFrame != 0 ||
            _sound.LastPlayRequest != OracleSoundEngine.SndFairyCutscene ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndFairyCutscene) !=
                fairySoundRequests + 1 ||
            !_sound.Channel(fairySoundChannel.Channel).Active ||
            _sound.Channel(fairySoundChannel.Channel).Priority != fairySoundChannel.Priority ||
            _sound.Channel(fairySoundChannel.Channel).Bank != fairySoundChannel.Bank ||
            _sound.Channel(fairySoundChannel.Channel).Offset != fairySoundChannel.Offset)
        {
            throw new InvalidOperationException(
                "Closing TX_1213 did not start SND_FAIRYCUTSCENE and its original " +
                "channel program with the vanish timeline at frame zero.");
        }
        for (int frame = 0; frame < intro.TotalVanishFrames - 1; frame++)
            intro.Update(1.0 / 60.0);
        if (intro.CurrentStage != NewGameIntroController.Stage.Vanishing ||
            intro.StageFrame != intro.TotalVanishFrames - 1)
        {
            throw new InvalidOperationException("Link's original vanish animation ended early.");
        }
        intro.Update(1.0 / 60.0);
        if (intro.CurrentStage != NewGameIntroController.Stage.PostVanish ||
            intro.StageFrame != 0)
        {
            throw new InvalidOperationException(
                "Link's vanish did not enter the post-vanish timeline at frame zero.");
        }
        for (int frame = 0; frame < record.PostVanishWaitFrames; frame++)
            intro.Update(1.0 / 60.0);
        if (completionRequests != 1 ||
            intro.CurrentStage != NewGameIntroController.Stage.Complete ||
            intro.StageFrame != record.PostVanishWaitFrames ||
            _sound.ActiveMusic != 0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndFairyCutscene) !=
                fairySoundRequests + 1)
        {
            throw new InvalidOperationException(
                "The new-game intro did not stop music and hand off to the summon transition exactly once.");
        }

        _player.WarpTo(new Vector2(0x50, 0x48));
        _newGameArrivalTicks = 0.0;
        _newGameArrivalFadeFrames = NewGameIntroController.ArrivalFadeWaitFrames;
        _newGameArrivalFrames = record.SummonFrames;
        _newGameArrivalPhase = 0;
        _newGameArrivalLastFrame = 0;
        _player.Visible = false;
        _player.SetPhysicsProcess(false);
        _player.SetProcess(false);
        for (int frame = 1; frame <= NewGameIntroController.ArrivalFadeWaitFrames; frame++)
        {
            if (!UpdateNewGameArrival(1.0 / 60.0) || _player.Visible ||
                _player.IsNewGameSlowFalling)
            {
                throw new InvalidOperationException(
                    "The new-game room fade exposed Link before transition $0b.");
            }
        }
        for (int frame = 1; frame < record.SummonFrames / 2; frame++)
        {
            if (!UpdateNewGameArrival(1.0 / 60.0) || _player.Visible)
                throw new InvalidOperationException("Link appeared before summon-wave frame 64.");
        }
        if (!UpdateNewGameArrival(1.0 / 60.0) || !_player.Visible ||
            !_player.IsNewGameSlowFalling || _player.NewGameSlowFallFrame != 0 ||
            _player.NewGameSlowFallZ != -0x50)
        {
            throw new InvalidOperationException(
                "Transition $0b did not begin at wave frame 64 and Z -$50.");
        }
        for (int frame = 65; frame <= 67; frame++)
            UpdateNewGameArrival(1.0 / 60.0);
        if (_player.NewGameSlowFallFrame != 0)
            throw new InvalidOperationException("LINK_ANIM_MODE_FALL frame $e6 ended early.");
        UpdateNewGameArrival(1.0 / 60.0);
        if (_player.NewGameSlowFallFrame != 1)
            throw new InvalidOperationException("LINK_ANIM_MODE_FALL did not advance after four updates.");
        for (int frame = 69; frame <= 122; frame++)
            UpdateNewGameArrival(1.0 / 60.0);
        if (!_player.IsNewGameSlowFalling || _player.NewGameSlowFallFrame != 2 ||
            _player.NewGameSlowFallZ != -3)
        {
            throw new InvalidOperationException(
                "Transition $0b diverged before its 59th gravity update.");
        }
        if (!UpdateNewGameArrival(1.0 / 60.0) || _player.IsNewGameSlowFalling)
            throw new InvalidOperationException("Transition $0b did not land on wave frame 123.");
        for (int frame = 124; frame < record.SummonFrames; frame++)
            UpdateNewGameArrival(1.0 / 60.0);
        if (UpdateNewGameArrival(1.0 / 60.0) || !_player.Visible ||
            !_player.IsProcessing() || !_player.IsPhysicsProcessing())
        {
            throw new InvalidOperationException(
                "The summon wave did not restore Link control on frame 128.");
        }

        screen.QueueFree();
        GD.Print("Validated CUTSCENE_PREGAME_INTRO frame-96 top entrance, interleaved 8x16 OBJ cells, " +
            "hardware OAM priority, cumulative descend/hover Z tables, $0d/$06 blue-orb OAM and palette 4, " +
            "300/60 waits, TX_1213, 62-update vanish handoff, 60-update black hold, 65-update white-fade wait, " +
            "SND_FAIRYCUTSCENE, MUS_ESSENCE_ROOM/STOPMUSIC handoff, 128-update wave, and transition $0b's three-pose " +
            "4-update/59-gravity-update slow fall.");
    }

    private void ValidateTimePortals()
    {
        var database = new TimePortalDatabase();
        var effectDatabase = new TimeWarpEffectDatabase();
        _saveData.SetGlobalFlag(
            OracleSaveData.GlobalFlagEnterPastCutsceneDone,
            value: false);
        _enterPastCommandTrace = new ValidationCutsceneTrace();
        _roomEvents.CommandTraceSink = _enterPastCommandTrace;
        (int Even, int Odd)[] expectedMasks =
        {
            (0xdd, 0xff), (0xdd, 0xbb), (0x55, 0xbb), (0x55, 0xaa),
            (0x11, 0xaa), (0x11, 0x88), (0x00, 0x88), (0x00, 0x00)
        };
        if (effectDatabase.TimeWarpSprite != "spr_timeportal" ||
            effectDatabase.CommonSprite != "spr_common_sprites" ||
            effectDatabase.SparkleSprite !=
                "spr_triforce_sparkle_vineseed_bookofseals" ||
            effectDatabase.PrimaryTileBase != 0 || effectDatabase.PrimaryPalette != 0 ||
            effectDatabase.BeamPalette != 7 ||
            effectDatabase.TrailTileBase != 0x10 || effectDatabase.TrailPalette != 3 ||
            effectDatabase.ParticleTileBase != 0x1e || effectDatabase.ParticlePalette != 4 ||
            effectDatabase.SparkleTileBase != 0x0a || effectDatabase.SparklePalette != 2 ||
            effectDatabase.PrimaryPriority != 3 || effectDatabase.BeamPriority != 2 ||
            effectDatabase.TrailPriority != 1 || effectDatabase.ParticlePriority != 3 ||
            effectDatabase.SparklePriority != 1 ||
            effectDatabase.DissolveFrames != 48 ||
            effectDatabase.SourceEffectFrames != 120 ||
            effectDatabase.SourceTrailFrames != 60 ||
            effectDatabase.ArrivalWaitFrames != 30 ||
            effectDatabase.ArrivalEffectFrames != 16 ||
            effectDatabase.ArrivalFlickerFrames != 30 ||
            effectDatabase.Particles.Count != 8 ||
            effectDatabase.Particles[0] != new TimeWarpEffectDatabase.ParticleRecord(0x280, -4, 0) ||
            effectDatabase.Particles[7] != new TimeWarpEffectDatabase.ParticleRecord(0x240, 9, 3) ||
            effectDatabase.OutdoorBeamPalette.SequenceEqual(effectDatabase.IndoorBeamPalette) ||
            expectedMasks.Where((mask, index) =>
                RoomTransitionController.TimeWarpDissolveMaskForValidation(index) != mask).Any())
        {
            throw new InvalidOperationException(
                "Imported $dd/$2b/$84 time-warp graphics, priorities, timing, particles, palettes, or " +
                "$dd/$ff..$00/$00 dissolve masks changed.");
        }
        IReadOnlyList<TimePortalDatabase.PortalRecord> ordinaryRecords =
            database.GetRoomPortals(0, 0x3a);
        if (ordinaryRecords.Count != 1 || ordinaryRecords[0].SubId != 0x00 ||
            ordinaryRecords[0].X != 0x18 || ordinaryRecords[0].Y != 0x28)
        {
            throw new InvalidOperationException(
                "Room 0:3a did not preserve its ordinary `$e1:$00 portal at `$21.");
        }

        LoadValidationRoom(0, 0x3a);
        List<TimePortal> ordinaryPortals = _entities.Entities<TimePortal>();
        if (ordinaryPortals.Count != 1 || !ordinaryPortals[0].Active ||
            _currentRoom.GetMetatile(ordinaryPortals[0].Position) != 0xd7)
        {
            throw new InvalidOperationException(
                "The exposed `$d7 marker in room 0:3a did not create an active ordinary portal.");
        }

        IReadOnlyList<TimePortalDatabase.PortalRecord> records = database.GetRoomPortals(0, 0x39);
        if (records.Count != 1 || records[0].SubId != 0x01 ||
            records[0].X != 0x28 || records[0].Y != 0x28 || records[0].LoopStart != 3)
        {
            throw new InvalidOperationException(
                "Room 0:39 did not preserve its `$e1:$01 portal at `$22 with animation loop index 3.");
        }

        LoadValidationRoom(0, 0x39);
        bool sourceUsesIndoorBeamPalette =
            (_currentRoom.TilesetFlags & 0x80) != 0;
        bool destinationUsesIndoorBeamPalette =
            (_rooms.GetRoom(1, 0x39).TilesetFlags & 0x80) != 0;
        if (sourceUsesIndoorBeamPalette || !destinationUsesIndoorBeamPalette)
        {
            throw new InvalidOperationException(
                "Canonical room 0:39 -> 1:39 no longer crosses from the outdoor " +
                "PALH_c1 source classification to the indoor PALH_c2 destination classification.");
        }
        List<TimePortal> portals = _entities.Entities<TimePortal>();
        if (portals.Count != 1 || portals[0].Active ||
            _currentRoom.GetMetatile(portals[0].Position) != 0x3a)
        {
            throw new InvalidOperationException(
                $"Room 0:39 portal initial state was count={portals.Count}, " +
                $"active={(portals.Count == 1 && portals[0].Active)}, tile=" +
                $"`${(portals.Count == 1 ? _currentRoom.GetMetatile(portals[0].Position) : 0):x2}.");
        }

        TimePortal portal = portals[0];
        if (!_currentRoom.ReplaceMetatile(portal.Position, 0x3a, 0xd7, (long)_animationTicks))
            throw new InvalidOperationException("Could not reveal portal-spot metatile `$d7 for validation.");
        _roomView.QueueRedraw();
        _entities.Update(1.0 / 60.0, _player);
        if (!portal.Active)
            throw new InvalidOperationException("The `$e1 portal did not initialize after its `$d7 spot was revealed.");
        for (int frame = 0; frame < 6; frame++)
            _entities.Update(1.0 / 60.0, _player);
        if (portal.CurrentFrame != 3)
            throw new InvalidOperationException("The portal did not finish its three 2-update intro frames.");
        for (int frame = 0; frame < 6; frame++)
            _entities.Update(1.0 / 60.0, _player);
        if (portal.CurrentFrame != 3)
            throw new InvalidOperationException("The portal's three-frame animation loop restarted incorrectly.");

        _player.WarpTo(portal.Position, recordSafe: false);
        _player.Face(Vector2I.Left);
        // Enter from a composite non-neutral pose. The sword previously hid a
        // stale pushing flag which became visible as soon as portal entry
        // cancelled the sword.
        _player.SetCutscenePushing(true);
        _player.StartSwordAttackForValidation(Vector2.Left);
        _sound.ClearPlayRequestAudit();
        _entities.Update(1.0 / 60.0, _player);
        if (!IsTransitioning || portal.Visible || _player.Position != portal.Position ||
            _player.FacingVector != Vector2I.Down || _player.Walking || _player.IsPushing ||
            _player.IsAttacking || _player.IsHoldingItemOneHand || _sound.ActiveMusic != 0 ||
            _transitions.TimeWarpPhaseName != "TimeWarpInitialize")
        {
            throw new InvalidOperationException(
                "interactionBeginTimewarp did not delete the portal, center Link in a neutral " +
                "down-facing pose, restart sound, and trigger CUTSCENE_TIMEWARP.");
        }

        // A long rendered frame must still service only state 0. In
        // particular, first-use shader compilation cannot collapse the 48
        // graphics-buffer updates into one visible frame.
        _transitions.UpdateWarp(5.0 / 60.0);
        if (_transitions.TimeWarpPhaseName != "TimeWarpDissolve" ||
            _transitions.TimeWarpDissolveStep != 0 ||
            _transitions.TimeWarpDissolveBufferStep != -1 ||
            _transitions.TimeWarpAppliedDissolveStep != -1 || _hud.Visible ||
            !_player.Visible ||
            !Mathf.IsEqualApprox(
                _roomView.BackgroundFadeAlpha,
                1.0f / RoomTransitionController.FastPaletteFadeFrames))
        {
            throw new InvalidOperationException(
                "CUTSCENE_TIMEWARP state 0 did not hide the HUD, start the BG-only fast black " +
                "fade, preserve a long rendered frame, and prepare the still-unmasked " +
                "six-buffer dissolve pass.");
        }
        UpdateRoomWarpTransition(5.0 / 60.0);
        if (_transitions.TimeWarpDissolveStep != 0 ||
            _transitions.TimeWarpDissolveBufferStep != 4 ||
            _transitions.TimeWarpAppliedDissolveStep != -1 || !_player.Visible)
        {
            throw new InvalidOperationException(
                "Link was hidden by the first five non-Link object/common graphics buffer updates.");
        }
        UpdateRoomWarpTransition(1.0 / 60.0);
        if (_transitions.TimeWarpDissolveBufferStep != 5 ||
            _transitions.TimeWarpAppliedDissolveStep != 0 || !_player.Visible)
        {
            throw new InvalidOperationException(
                "The final bank-6 object/companion pass did not commit mask $dd/$ff " +
                "without masking Link.");
        }
        UpdateRoomWarpTransition(1.0 / 60.0);
        if (_transitions.TimeWarpDissolveStep != 1 ||
            _transitions.TimeWarpDissolveBufferStep != 0 ||
            _transitions.TimeWarpAppliedDissolveStep != 0 || !_player.Visible)
        {
            throw new InvalidOperationException(
                "The second $dd/$bb non-Link buffer cycle did not begin on update 7.");
        }
        UpdateRoomWarpTransition(41.0 / 60.0);
        if (_transitions.TimeWarpPhaseName != "TimeWarpSetup" ||
            _transitions.TimeWarpDissolveStep != 7 ||
            _transitions.TimeWarpDissolveBufferStep != 5 ||
            _transitions.TimeWarpAppliedDissolveStep != 7 || !_player.Visible ||
            _entities.Entities<TimePortal>().Count != 0 ||
            !Mathf.IsEqualApprox(_roomView.BackgroundFadeAlpha, 1.0f))
        {
            throw new InvalidOperationException(
                "The eight six-buffer object-graphics masks did not make non-Link objects " +
                "transparent over black, retain Link, and clear the source objects.");
        }

        UpdateRoomWarpTransition(1.0 / 60.0);
        if (_transitions.ActiveTimeWarpEffect is not null ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndTimewarpInitiated) != 0 ||
            !_player.Visible)
        {
            throw new InvalidOperationException("The tilemap reload substep created the source effect early.");
        }
        UpdateRoomWarpTransition(1.0 / 60.0);
        TimeWarpEffect sourceEffect = _transitions.ActiveTimeWarpEffect ??
            throw new InvalidOperationException("INTERAC_TIMEWARP $dd:$00 was not created.");
        if (_transitions.TimeWarpPhaseName != "TimeWarpSourceEffect" ||
            !sourceEffect.PrimaryVisible || !_player.Visible ||
            sourceEffect.BackgroundZIndex != NpcCharacter.BehindLinkZIndex ||
            sourceEffect.ForegroundZIndex != NpcCharacter.InFrontOfLinkZIndex ||
            sourceEffect.UsesIndoorBeamPalette != sourceUsesIndoorBeamPalette ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndTimewarpInitiated) != 1)
        {
            throw new InvalidOperationException(
                "The source effect did not begin with priority-3 ground below Link, its " +
                "priority-2 beam layer above Link, and SND_TIMEWARP_INITIATED $d1.");
        }
        UpdateRoomWarpTransition(119.0 / 60.0);
        if (_transitions.TimeWarpPhaseName != "TimeWarpSourceEffect" ||
            !sourceEffect.PrimaryVisible || !sourceEffect.BeamVisible || !_player.Visible ||
            sourceEffect.ParticleSpawnCount != 10)
        {
            throw new InvalidOperationException(
                "The source $dd:$00 effect or intact Link ended before the 120-count handoff.");
        }
        UpdateRoomWarpTransition(1.0 / 60.0);
        if (_transitions.TimeWarpPhaseName != "TimeWarpSourceTrail" ||
            !sourceEffect.PrimaryVisible || !sourceEffect.BeamVisible ||
            sourceEffect.BeamContracting || !sourceEffect.TrailVisible ||
            sourceEffect.SourceCounter != 24 ||
            sourceEffect.ParticleSpawnCount != 10 || _player.Visible)
        {
            throw new InvalidOperationException(
                "The cutscene's 120-count handoff did not delete w1Link, retain the " +
                "$dd:$00 interaction's final 24 counts and purple child, emit ten $2b " +
                "particles, and create $dd:$02.");
        }
        UpdateRoomWarpTransition(12.0 / 60.0);
        if (sourceEffect.SparkleSpawnCount != 1 || sourceEffect.ActiveSparkleCount != 1 ||
            sourceEffect.SourceCounter != 12 || sourceEffect.BeamContracting)
        {
            throw new InvalidOperationException(
                "The rising -$0400 trail did not create INTERAC_SPARKLE $84:$01 after six moves.");
        }
        UpdateRoomWarpTransition(11.0 / 60.0);
        if (sourceEffect.SourceCounter != 1 || sourceEffect.BeamContracting ||
            !sourceEffect.PrimaryVisible || !sourceEffect.BeamVisible)
        {
            throw new InvalidOperationException(
                "The source ground or purple child began contracting before $dd:$00 counter1 reached zero.");
        }
        UpdateRoomWarpTransition(1.0 / 60.0);
        if (sourceEffect.SourceCounter != 0 || !sourceEffect.BeamContracting ||
            sourceEffect.BeamFrameIndex != 0 || !sourceEffect.PrimaryVisible ||
            !sourceEffect.BeamVisible)
        {
            throw new InvalidOperationException(
                "$dd:$00 counter1 zero did not select ground animation $01 and the purple " +
                "child's horizontal-fold animation $04 on source-trail update 24.");
        }
        UpdateRoomWarpTransition(10.0 / 60.0);
        if (!sourceEffect.BeamVisible || sourceEffect.BeamFrameIndex != 10)
        {
            throw new InvalidOperationException(
                "The source purple child did not retain all 11 visible animation-$04 fold frames.");
        }
        UpdateRoomWarpTransition(1.0 / 60.0);
        if (sourceEffect.BeamVisible || !sourceEffect.PrimaryVisible)
        {
            throw new InvalidOperationException(
                "The source purple child did not delete immediately after its 11-update horizontal fold.");
        }
        UpdateRoomWarpTransition(13.0 / 60.0);
        if (sourceEffect.PrimaryVisible)
        {
            throw new InvalidOperationException(
                "The source ground did not finish its independent 24-update contraction.");
        }
        UpdateRoomWarpTransition(12.0 / 60.0);
        if (_transitions.TimeWarpPhaseName != "TimeWarpBlackFadeIn" ||
            sourceEffect.BeamVisible || sourceEffect.PrimaryVisible || _player.Visible)
        {
            throw new InvalidOperationException(
                "The source trail did not hold for exactly 60 updates after both portal " +
                "components had collapsed and vanished.");
        }
        UpdateRoomWarpTransition(RoomTransitionController.FastPaletteFadeFrames / 60.0);
        if (_transitions.TimeWarpPhaseName != "TimeWarpWhiteFadeOut" ||
            _transitions.TimeWarpPhaseFrame != 1 ||
            !Mathf.IsEqualApprox(_roomView.BackgroundFadeAlpha, 1.0f) ||
            !Mathf.IsEqualApprox(_warpFade.Color.A, 1.0f / WarpFadeFrames) ||
            _player.Visible)
        {
            throw new InvalidOperationException(
                "The source tilemap was not kept black-covered while the palette handoff " +
                "started the first fadeoutToWhite step.");
        }
        UpdateRoomWarpTransition((WarpFadeFrames - 2.0f) / 60.0);
        if (_transitions.TimeWarpPhaseName != "TimeWarpWhiteFadeOut" ||
            _transitions.TimeWarpPhaseFrame != WarpFadeFrames - 1 ||
            !Mathf.IsEqualApprox(_roomView.BackgroundFadeAlpha, 1.0f) ||
            !Mathf.IsEqualApprox(
                _warpFade.Color.A, (WarpFadeFrames - 1.0f) / WarpFadeFrames) ||
            _activeGroup != 0)
        {
            throw new InvalidOperationException(
                "The source tilemap became visible before the white overlay reached opacity.");
        }
        UpdateRoomWarpTransition(1.0 / 60.0);
        if (_activeGroup != 1 || _currentRoom.Id != 0x39 ||
            _currentRoom.GetPackedPosition(_player.Position) != 0x22 ||
            _player.Visible || !_hud.Visible ||
            _transitions.TimeWarpPhaseName != "TimeWarpArrivalFadeIn" ||
            _transitions.TimeWarpDissolveStep != -1)
        {
            throw new InvalidOperationException(
                $"Time portal 0:39/`$22 landed at {_activeGroup:x1}:{_currentRoom.Id:x2}/" +
                $"`${_currentRoom.GetPackedPosition(_player.Position):x2} instead of 1:39/`$22.");
        }

        UpdateRoomWarpTransition(WarpFadeFrames / 60.0);
        if (_transitions.TimeWarpPhaseName != "TimeWarpArrivalWait")
            throw new InvalidOperationException("The destination did not fade in from white for 32 updates.");
        UpdateRoomWarpTransition(30.0 / 60.0);
        TimeWarpEffect arrivalEffect = _transitions.ActiveTimeWarpEffect ??
            throw new InvalidOperationException("Destination INTERAC_TIMEWARP $dd:$01 was not created.");
        if (_transitions.TimeWarpPhaseName != "TimeWarpArrivalEffect" || _player.Visible ||
            !arrivalEffect.PrimaryVisible ||
            arrivalEffect.BackgroundZIndex != NpcCharacter.BehindLinkZIndex ||
            arrivalEffect.ForegroundZIndex != NpcCharacter.InFrontOfLinkZIndex ||
            arrivalEffect.UsesIndoorBeamPalette != sourceUsesIndoorBeamPalette ||
            ((_currentRoom.TilesetFlags & 0x80) != 0) !=
                destinationUsesIndoorBeamPalette)
        {
            throw new InvalidOperationException(
                "The hidden 30-update arrival wait did not create the destination effect " +
                "with the source-carried wcc50 beam palette variant.");
        }
        UpdateRoomWarpTransition(15.0 / 60.0);
        if (_player.Visible ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndTimewarpCompleted) != 0)
        {
            throw new InvalidOperationException("Link or SND_TIMEWARP_COMPLETED appeared before update 16.");
        }
        UpdateRoomWarpTransition(1.0 / 60.0);
        if (!_player.Visible ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndTimewarpCompleted) != 1 ||
            _transitions.TimeWarpPhaseName != "TimeWarpArrivalFlicker" ||
            !arrivalEffect.BeamVisible || arrivalEffect.BeamContracting)
        {
            throw new InvalidOperationException(
                "Destination update 16 did not reveal Link and play SND_TIMEWARP_COMPLETED $d4.");
        }

        int invisibleFlickerFrames = 0;
        for (int frame = 0; frame < 4; frame++)
        {
            UpdateRoomWarpTransition(1.0 / 60.0);
            if (!_player.Visible)
                invisibleFlickerFrames++;
        }
        if (invisibleFlickerFrames != 1)
        {
            throw new InvalidOperationException(
                "Destination objectFlickerVisibility b=$03 was not visible on three of four updates.");
        }
        UpdateRoomWarpTransition(4.0 / 60.0);
        if (!arrivalEffect.BeamContracting || !arrivalEffect.BeamVisible ||
            arrivalEffect.BeamFrameIndex != 0 || !arrivalEffect.PrimaryVisible)
        {
            throw new InvalidOperationException(
                "The completed $dd:$01 expansion did not start ground animation $01 and " +
                "purple-child animation $04 with their first contraction frames intact.");
        }
        UpdateRoomWarpTransition(11.0 / 60.0);
        if (arrivalEffect.BeamVisible || !arrivalEffect.PrimaryVisible)
        {
            throw new InvalidOperationException(
                "The purple $dd:$04 child did not collapse for 11 updates and delete itself " +
                "before the slower ground contraction.");
        }
        UpdateRoomWarpTransition(11.0 / 60.0);
        if (IsTransitioning || !_player.Visible ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndTimewarpCompleted) != 1 ||
            !_player.CutsceneControlled || !_roomEvents.EnterPast.HasState ||
            _roomEvents.EnterPast.Stage != EnterPastEvent.EventStage.PreJumpWait ||
            _roomEvents.EnterPast.Counter !=
                _roomEvents.EnterPast.Record.ExpectedArrivalCounter)
        {
            throw new InvalidOperationException(
                "The 30-update arrival flicker did not hand off to the partially elapsed " +
                "room 1:39 first-arrival script.");
        }

        GD.Print("Validated all 21 `$e1 portal records and the complete 0:39 -> 1:39 " +
            "CUTSCENE_TIMEWARP: centered Link, sound restart, 8x6 non-Link sprite dissolve, " +
            "intact-until-120 source Link, priority-3 ground below Link, priority-2/1 beam " +
            "and trail above Link, source update-24 horizontal beam fold, 11-update " +
            "source/arrival beam contraction, neutral down-facing Link on contact, " +
            "source-carried PALH_c1/c2 palette, hidden HUD, " +
            "$dd/$2b/$84 source effects, map-masked black-to-white fade, $d1/$d4 sounds, " +
            "and 30/16/30 arrival.");
    }

    private void ValidateEnterPastEvent()
    {
        EnterPastEvent enterPast = _roomEvents.EnterPast;
        EnterPastEventDatabase.EnterPastEventRecord record = enterPast.Record;
        NpcCharacter villager = _npcNodes.Find(npc =>
            npc.Record.Id == record.InteractionId && npc.Record.SubId == record.SubId) ??
            throw new InvalidOperationException(
                "Room 1:39 did not create INTERAC_MALE_VILLAGER $3a:$0d.");

        if (_activeGroup != record.Group || _currentRoom.Id != record.Room ||
            record.IntroWaitFrames != 100 || record.PreJumpWaitFrames != 40 ||
            record.PostJumpWaitFrames != 30 || record.PostTextWaitFrames != 30 ||
            record.JumpSpeedZ != -0x200 || record.JumpGravity != 0x30 ||
            record.FastSpeed != 0x28 || record.SlowSpeed != 0x14 ||
            record.FirstDownCounter != 0x11 || record.RightCounter != 0x11 ||
            record.SecondDownCounter != 0x09 || record.SlowDownCounter != 0x21 ||
            record.FinalDownCounter != 0x39 || record.TextId != 0x1622 ||
            record.JumpSound != OracleSoundEngine.SndJump ||
            record.GlobalFlag != OracleSaveData.GlobalFlagEnterPastCutsceneDone ||
            !enterPast.HasState || enterPast.Completed ||
            enterPast.Stage != EnterPastEvent.EventStage.PreJumpWait ||
            enterPast.Counter != record.ExpectedArrivalCounter ||
            villager.Position != new Vector2(0x18, 0x28) || !villager.Active ||
            !_player.CutsceneControlled)
        {
            throw new InvalidOperationException(
                "The portal handoff did not preserve the imported first-arrival actor, " +
                "script values, initial position, or wait overlap.");
        }

        _sound.ClearPlayRequestAudit();
        StepRoomEventFrames(record.ExpectedArrivalCounter - 1);
        if (enterPast.Counter != 1 || enterPast.Stage != EnterPastEvent.EventStage.PreJumpWait ||
            enterPast.ZFixed != 0 || _sound.PlayRequestsFor(record.JumpSound) != 0)
        {
            throw new InvalidOperationException(
                "The remaining pre-jump wait ended early after the time-warp arrival.");
        }
        StepRoomEventFrames(1);
        if (enterPast.Stage != EnterPastEvent.EventStage.BeginJump ||
            _sound.PlayRequestsFor(record.JumpSound) != 0)
        {
            throw new InvalidOperationException(
                "wait 40 did not return to jumpAndWaitUntilLanded on its zero update.");
        }
        StepRoomEventFrames(1);
        if (enterPast.Stage != EnterPastEvent.EventStage.Jump ||
            enterPast.ZFixed != -0x200 || villager.ScriptDrawOffset.Y != -2 ||
            _sound.PlayRequestsFor(record.JumpSound) != 1)
        {
            throw new InvalidOperationException(
                "beginJump did not apply speedZ -$0200 and SND_JUMP $53 on its own update.");
        }
        StepRoomEventFrames(21);
        if (enterPast.ZFixed != -0xb0 || villager.ScriptDrawOffset.Y != -1)
        {
            throw new InvalidOperationException(
                "The villager jump diverged before its 23rd $30-gravity update.");
        }
        StepRoomEventFrames(1);
        if (enterPast.Stage != EnterPastEvent.EventStage.InstallPostJumpWait ||
            enterPast.ZFixed != 0 || villager.ScriptDrawOffset != Vector2.Zero)
        {
            throw new InvalidOperationException(
                "The villager did not land on the 23rd gravity update.");
        }

        StepRoomEventFrames(1);
        StepRoomEventFrames(record.PostJumpWaitFrames - 1);
        if (enterPast.Counter != 1 || _dialogue.IsOpen)
            throw new InvalidOperationException("The post-jump wait ended early.");
        StepRoomEventFrames(1);
        const string expectedText =
            "Another one?!?\nFirst, that guy\nwith the weird\nhat appears,\nthen you...\n" +
            "Ever since that\ngirl Nayru came,\nthere's been all\nsorts o' weird\ngoings on!";
        if (!_dialogue.IsOpen || _dialogue.CurrentMessage != expectedText ||
            !record.Text.Contains("\\stop", StringComparison.Ordinal) ||
            !record.Text.Contains("\\col(3)Nayru\\col(0)", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "TX_1622 did not retain its stop command, blue Nayru span, and exact text.");
        }

        _dialogue.Close();
        StepRoomEventFrames(1);
        if (enterPast.Stage != EnterPastEvent.EventStage.PostTextWait ||
            enterPast.Counter != record.PostTextWaitFrames)
        {
            throw new InvalidOperationException(
                "The script did not install its post-text wait on the first update after closing TX_1622.");
        }
        StepRoomEventFrames(record.PostTextWaitFrames - 1);
        if (enterPast.Counter != 1 || villager.Position != new Vector2(0x18, 0x28))
            throw new InvalidOperationException("The post-text wait or stationary position ended early.");
        StepRoomEventFrames(1);
        if (enterPast.Stage != EnterPastEvent.EventStage.StartFirstDown)
            throw new InvalidOperationException("setspeed SPEED_100 lost its script-command update.");

        StepRoomEventFrames(1);
        if (enterPast.Stage != EnterPastEvent.EventStage.FirstDown ||
            enterPast.Counter != record.FirstDownCounter ||
            villager.CurrentScriptAnimationSource != record.DownAnimation)
        {
            throw new InvalidOperationException(
                "movedown $11 did not install its counter and down animation.");
        }
        StepRoomEventFrames(6);
        if (villager.Position != new Vector2(0x18, 0x2e) ||
            enterPast.Counter != 0x0b || villager.CurrentAnimationFrame != 0)
        {
            throw new InvalidOperationException(
                "The first SPEED_100 leg or doubled animation cadence diverged before update 7.");
        }
        StepRoomEventFrames(1);
        if (villager.Position != new Vector2(0x18, 0x2f) ||
            enterPast.Counter != 0x0a || villager.CurrentAnimationFrame != 1)
        {
            throw new InvalidOperationException(
                "interactionAnimateBasedOnSpeed did not advance twice at SPEED_100.");
        }
        StepRoomEventFrames(9);
        if (villager.Position != new Vector2(0x18, 0x38) || enterPast.Counter != 1)
            throw new InvalidOperationException("movedown $11 did not move exactly 16 pixels.");
        StepRoomEventFrames(1);
        if (enterPast.Stage != EnterPastEvent.EventStage.FirstDown ||
            enterPast.CurrentCommandIndex != 10 || enterPast.CurrentCommandUpdates != 0 ||
            enterPast.Counter != 0 ||
            villager.CurrentScriptAnimationSource != record.DownAnimation)
        {
            throw new InvalidOperationException(
                "movedown $11's counter2-zero update incorrectly dispatched moveright.");
        }
        StepRoomEventFrames(1);
        if (enterPast.Stage != EnterPastEvent.EventStage.Right ||
            enterPast.Counter != record.RightCounter ||
            villager.CurrentScriptAnimationSource != record.RightAnimation)
        {
            throw new InvalidOperationException(
                "moveright $11 did not start on the update after counter2 reached zero.");
        }
        StepRoomEventFrames(record.RightCounter - 1);
        if (villager.Position != new Vector2(0x28, 0x38) || enterPast.Counter != 1)
            throw new InvalidOperationException("moveright $11 did not move exactly 16 pixels.");
        StepRoomEventFrames(1);
        if (enterPast.Stage != EnterPastEvent.EventStage.Right ||
            enterPast.CurrentCommandIndex != 11 || enterPast.CurrentCommandUpdates != 0 ||
            enterPast.Counter != 0 ||
            villager.CurrentScriptAnimationSource != record.RightAnimation)
        {
            throw new InvalidOperationException(
                "moveright $11's counter2-zero update incorrectly dispatched movedown.");
        }
        StepRoomEventFrames(1);
        if (enterPast.Stage != EnterPastEvent.EventStage.SecondDown ||
            enterPast.Counter != record.SecondDownCounter ||
            villager.CurrentScriptAnimationSource != record.DownAnimation)
        {
            throw new InvalidOperationException(
                "movedown $09 did not start on the update after counter2 reached zero.");
        }
        StepRoomEventFrames(record.SecondDownCounter - 1);
        if (villager.Position != new Vector2(0x28, 0x40) || enterPast.Counter != 1)
            throw new InvalidOperationException("movedown $09 did not move exactly eight pixels.");
        StepRoomEventFrames(1);
        if (enterPast.Stage != EnterPastEvent.EventStage.StartSlowDown ||
            enterPast.CurrentCommandIndex != 12 || enterPast.CurrentCommandUpdates != 0 ||
            enterPast.Counter != 0)
        {
            throw new InvalidOperationException(
                "movedown $09's counter2-zero update incorrectly dispatched SPEED_080.");
        }
        StepRoomEventFrames(1);
        if (enterPast.Stage != EnterPastEvent.EventStage.StartSlowDown ||
            enterPast.CurrentCommandIndex != 13 || enterPast.CurrentCommandUpdates != 0)
        {
            throw new InvalidOperationException("setspeed SPEED_080 lost its script-command update.");
        }
        StepRoomEventFrames(1);
        if (enterPast.Stage != EnterPastEvent.EventStage.SlowDown ||
            enterPast.Counter != record.SlowDownCounter)
        {
            throw new InvalidOperationException("applyspeed $21 lost its script-command update.");
        }
        StepRoomEventFrames(record.SlowDownCounter - 1);
        if (villager.Position != new Vector2(0x28, 0x50) || enterPast.Counter != 1)
        {
            throw new InvalidOperationException(
                "SPEED_080 applyspeed $21 did not move exactly 16 pixels.");
        }
        StepRoomEventFrames(1);
        if (enterPast.Stage != EnterPastEvent.EventStage.StartFinalDown ||
            enterPast.CurrentCommandIndex != 14 || enterPast.CurrentCommandUpdates != 0 ||
            enterPast.Counter != 0)
        {
            throw new InvalidOperationException(
                "applyspeed $21's counter2-zero update incorrectly dispatched SPEED_100.");
        }
        StepRoomEventFrames(1);
        if (enterPast.Stage != EnterPastEvent.EventStage.StartFinalDown ||
            enterPast.CurrentCommandIndex != 15 || enterPast.CurrentCommandUpdates != 0)
        {
            throw new InvalidOperationException("The final SPEED_100 command lost its own update.");
        }
        StepRoomEventFrames(1);
        if (enterPast.Stage != EnterPastEvent.EventStage.FinalDown ||
            enterPast.Counter != record.FinalDownCounter)
        {
            throw new InvalidOperationException("The final applyspeed $39 lost its command update.");
        }
        StepRoomEventFrames(record.FinalDownCounter - 1);
        if (villager.Position != new Vector2(0x28, 0x88) || enterPast.Counter != 1 ||
            enterPast.Completed || !villager.Active)
        {
            throw new InvalidOperationException(
                "The final SPEED_100 applyspeed $39 path did not cover 56 pixels.");
        }
        StepRoomEventFrames(1);
        if (!enterPast.HasState || enterPast.Completed || !villager.Active ||
            enterPast.CurrentCommandIndex != 16 || enterPast.CurrentCommandUpdates != 0 ||
            enterPast.Counter != 0 || !_player.CutsceneControlled)
        {
            throw new InvalidOperationException(
                "applyspeed $39's counter2-zero update incorrectly completed the script.");
        }
        StepRoomEventFrames(1);
        if (enterPast.HasState || !enterPast.Completed || villager.Active ||
            _player.CutsceneControlled ||
            !_saveData.HasGlobalFlag(OracleSaveData.GlobalFlagEnterPastCutsceneDone) ||
            _sound.PlayRequestsFor(record.JumpSound) != 1)
        {
            throw new InvalidOperationException(
                "The first-past-arrival script did not set flag $41, delete the villager, and restore input.");
        }

        LoadValidationRoom(record.Group, record.Room);
        NpcCharacter? completedVillager = _npcNodes.Find(npc =>
            npc.Record.Id == record.InteractionId && npc.Record.SubId == record.SubId);
        if (completedVillager is null || completedVillager.Active || enterPast.HasState ||
            _player.CutsceneControlled)
        {
            throw new InvalidOperationException(
                "villagerSubid0dScript did not redirect to stubScript and delete on re-entry.");
        }

        ValidationCutsceneTrace commandTrace = _enterPastCommandTrace ??
            throw new InvalidOperationException(
                "The first-past-arrival command trace was not installed before time-warp arrival.");
        CutsceneCommandTraceEntry[] starts = commandTrace.Entries
            .Where(entry => entry.Phase == CutsceneCommandTracePhase.Started)
            .ToArray();
        string[] expectedOpcodes =
        {
            "setdisabledobjects", "wait", "disableinput", "wait", "jump",
            "wait", "showtext", "wait", "setspeed", "move", "move", "move",
            "setspeed", "applyspeed", "setspeed", "applyspeed",
            "setglobalflag", "enableinput", "scriptend"
        };
        if (starts.Length != expectedOpcodes.Length ||
            starts.Where((entry, index) =>
                entry.Source.Script != "villagerSubid0dScript" ||
                entry.Source.Label != "villagerSubid0dScript" ||
                entry.Source.CommandIndex != index ||
                entry.Source.Opcode != expectedOpcodes[index] ||
                entry.Source.SourceLine <= 0 ||
                (index > 0 && entry.Source.SourceLine <= starts[index - 1].Source.SourceLine))
            .Any())
        {
            throw new InvalidOperationException(
                "The imported first-past-arrival trace lost source lines, command order, " +
                "or typed opcodes.");
        }

        int CompletedUpdate(int commandIndex) => commandTrace.Entries.Single(entry =>
            entry.Source.CommandIndex == commandIndex &&
            entry.Phase == CutsceneCommandTracePhase.Completed).ScriptUpdate;
        if (starts[3].ScriptUpdate != starts[2].ScriptUpdate ||
            starts[6].ScriptUpdate != CompletedUpdate(5) ||
            starts[10].ScriptUpdate != CompletedUpdate(9) + 1 ||
            starts[11].ScriptUpdate != CompletedUpdate(10) + 1 ||
            starts[12].ScriptUpdate != CompletedUpdate(11) + 1 ||
            starts[13].ScriptUpdate != starts[12].ScriptUpdate + 1 ||
            starts[14].ScriptUpdate != CompletedUpdate(13) + 1 ||
            starts[15].ScriptUpdate != starts[14].ScriptUpdate + 1 ||
            starts[16].ScriptUpdate != CompletedUpdate(15) + 1 ||
            starts[17].ScriptUpdate != starts[16].ScriptUpdate ||
            starts[18].ScriptUpdate != starts[16].ScriptUpdate)
        {
            throw new InvalidOperationException(
                "villagerSubid0dScript did not preserve carry-through commands, " +
                "counter2 zero-update yields, or the final same-update completion chain.");
        }
        _roomEvents.CommandTraceSink = null;
        _enterPastCommandTrace = null;

        GD.Print("Validated room 1:39's first time-portal arrival: transition-overlapped " +
            "100/40 waits, -$0200/$30 jump and SND_JUMP, TX_1622 controls, 30/30 waits, " +
            "$11/$11/$09/$21/$39 movement counters, SPEED_100/SPEED_080 animation cadence, " +
            "counter2 zero-update command boundaries, exact $18,$28 -> $28,$88 path, " +
            "imported source trace, input gating, deletion, and persistent flag $41.");
    }

    private void ValidateImpaIntroEncounter()
    {
        ImpaIntroEvent impaEvent = _roomEvents.Impa;
        NayruIntroEvent nayruIntro = _roomEvents.Nayru;
        var encounterTrace = new ValidationCutsceneTrace();
        _roomEvents.CommandTraceSink = encounterTrace;
        _saveData.SetGlobalFlag(OracleSaveData.GlobalFlagPregameIntroDone);
        _saveData.SetGlobalFlag(OracleSaveData.GlobalFlagIntroDone, value: false);
        _sound.PlaySound(OracleSoundEngine.SndCtrlStopMusic);
        _sound.ClearPlayRequestAudit();
        _saveData.SetRoomFlag(0, 0x7a, OracleSaveData.RoomFlag40, value: false);
        _saveData.SetRoomFlag(0, 0x6a, OracleSaveData.RoomFlag40, value: false);
        LoadValidationRoom(0, 0x7a);
        if (_sound.ActiveMusic != 0)
            throw new InvalidOperationException(
                "The playable intro did not suppress ordinary room music before meeting Impa.");
        _player.WarpTo(new Vector2(0x38, 0x07));
        _player.Face(Vector2I.Up);
        if (!impaEvent.HelpWaitingAtEdge || _roomEvents.Active)
            throw new InvalidOperationException("Room 0:7a did not arm INTERAC_MISCELLANEOUS_1 $6b:$00.");
        impaEvent.UpdateHelpFrame(upPressed: true);
        if (_dialogue.IsOpen)
            throw new InvalidOperationException("Impa's help text triggered before Link's Y coordinate was below $07.");

        _player.WarpTo(new Vector2(0x38, 0x06));
        impaEvent.UpdateHelpFrame(upPressed: true);
        if (!_dialogue.IsOpen || _dialogue.CurrentMessage != "HELLLLP!!!" ||
            _dialogue.Position.Y != 96 || !_player.CutsceneControlled ||
            impaEvent.Counter != 30 ||
            _saveData.HasRoomFlag(0, 0x7a, OracleSaveData.RoomFlag40))
        {
            throw new InvalidOperationException(
                "Room 0:7a did not show fixed-bottom TX_0100 and install its 30-update counter.");
        }
        _dialogue.Close();
        StepRoomEventFrames(29);
        if (impaEvent.Counter != 1 ||
            _saveData.HasRoomFlag(0, 0x7a, OracleSaveData.RoomFlag40))
        {
            throw new InvalidOperationException(
                "INTERAC_MISCELLANEOUS_1 $6b:$00 ended its post-text counter early.");
        }
        StepRoomEventFrames(1);
        if (impaEvent.Counter != 8 ||
            !_saveData.HasRoomFlag(0, 0x7a, OracleSaveData.RoomFlag40))
        {
            throw new InvalidOperationException(
                "Room 0:7a did not set room flag $40 and install eight BTN_UP updates.");
        }
        StepRoomEventFrames(1);
        if (!_transitions.ScrollActive || _activeGroup != 0 || _currentRoom.Id != 0x6a)
            throw new InvalidOperationException("The simulated Up input did not begin the 0:7a -> 0:6a scroll.");
        int impaScrollFrames = FinishActiveScrollingTransitionWithRoomEventsForValidation();
        if (impaScrollFrames != 32)
            throw new InvalidOperationException(
                $"The 0:7a -> 0:6a vertical scroll took {impaScrollFrames} updates, expected 32.");

        NpcCharacter? impa = impaEvent.Actor;
        System.Collections.Generic.IReadOnlyList<NpcCharacter> octoroks =
            impaEvent.FakeOctoroks;
        Color possessedHighlight = new(0x12 / 31.0f, 0x1a / 31.0f, 0x1f / 31.0f);
        if (impa is null || !_roomEvents.Active || impa.Position != new Vector2(0x48, 0x38) ||
            octoroks.Count != 3 ||
            octoroks[0].Position != new Vector2(0x48, 0x18) ||
            octoroks[1].Position != new Vector2(0x38, 0x38) ||
            octoroks[2].Position != new Vector2(0x58, 0x38) ||
            !impa.CurrentAnimationUsesColor(possessedHighlight))
        {
            throw new InvalidOperationException(
                "Room 0:6a did not create possessed Impa and objectData.impaOctoroks " +
                $"with their original positions and PALH_97 palette 7 (impa={impa?.Position}, " +
                $"active={_roomEvents.Active}, octoroks={octoroks.Count}, " +
                $"positions={string.Join(',', octoroks.Select(actor => actor.Position))}, " +
                $"highlight={impa?.CurrentAnimationUsesColor(possessedHighlight)}).");
        }

        Vector2 linkStart = _player.Position;
        if (linkStart != new Vector2(0x38, 0x76))
            throw new InvalidOperationException($"0:7a -> 0:6a placed Link at {linkStart}, expected $76/$38.");
        if (!_player.CutsceneControlled || impaEvent.Counter != 120 ||
            _sound.ActiveMusic != OracleSoundEngine.MusFairyFountain ||
            _sound.MusicVolume != 3 ||
            _player.Position != linkStart || _player.FacingVector != Vector2I.Up)
        {
            throw new InvalidOperationException(
                "linkCutscene1 state 0 did not install its $78 counter, upward animation, " +
                "and MUS_FAIRY_FOUNTAIN volume-3 override.");
        }
        StepRoomEventFrames(119);
        if (impaEvent.Counter != 1 || _player.Position != linkStart)
            throw new InvalidOperationException("Link's initial 120-update wait ended early in room 0:6a.");
        StepRoomEventFrames(1);
        StepRoomEventFrames(15);
        if (_player.Position != new Vector2(0x47, 0x76) ||
            _player.FacingVector != Vector2I.Right)
        {
            throw new InvalidOperationException(
                "Link did not approach center X=$48 at one pixel per SPEED_100 update.");
        }
        StepRoomEventFrames(1);
        StepRoomEventFrames(1);
        if (impaEvent.Counter != 4 || _player.Position != new Vector2(0x48, 0x76))
            throw new InvalidOperationException("Link did not install the four-update center wait at X=$48.");
        StepRoomEventFrames(3);
        if (impaEvent.Counter != 1 || _player.Position.Y != 0x76)
            throw new InvalidOperationException("Link's four-update center wait ended early.");
        StepRoomEventFrames(1);
        StepRoomEventFrames(45);
        if (impaEvent.Counter != 1 || _player.Position != new Vector2(0x48, 0x49))
            throw new InvalidOperationException("Link's $2e-update upward approach ended early.");
        StepRoomEventFrames(1);
        if (_player.Position != new Vector2(0x48, 0x48) ||
            _sound.LastPlayRequest != OracleSoundEngine.SndClink ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndClink) != 1)
        {
            throw new InvalidOperationException(
                "Link did not finish exactly 46 pixels above his entry point and play SND_CLINK.");
        }

        StepRoomEventFrames(1);
        if (impaEvent.Counter != 0 || impaEvent.EncounterCommandIndex != 1)
            throw new InvalidOperationException(
                "impaScript0 did not preserve checkmemoryeq's successful one-update yield.");
        StepRoomEventFrames(1);
        if (impaEvent.Counter != 210 || impaEvent.EncounterCommandIndex != 1)
            throw new InvalidOperationException(
                "impaScript0 did not install its 210-update wait after the cfd0 gate.");
        Vector2[] fakeStarts =
        {
            new(0x48, 0x18), new(0x38, 0x38), new(0x58, 0x38)
        };
        StepRoomEventFrames(18);
        if (octoroks[0].Position != fakeStarts[0] ||
            octoroks[1].Position != fakeStarts[1] ||
            octoroks[2].Position != fakeStarts[2])
        {
            throw new InvalidOperationException("A fake Octorok left before its original $14 signal wait.");
        }
        StepRoomEventFrames(1);
        StepRoomEventFrames(59);
        if (octoroks[1].Position != fakeStarts[1])
            throw new InvalidOperationException("Fake Octorok var03=$01 moved during its $3c flee delay.");
        StepRoomEventFrames(1);
        if (octoroks[1].Position != fakeStarts[1] ||
            _sound.LastPlayRequest != OracleSoundEngine.SndThrow ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndThrow) != 1)
        {
            throw new InvalidOperationException(
                "Fake Octorok var03=$01 did not play SND_THROW on its stationary substate update.");
        }
        StepRoomEventFrames(1);
        if (octoroks[1].Position != fakeStarts[1] + Vector2.Left * 3)
        {
            throw new InvalidOperationException(
                "Fake Octorok var03=$01 did not flee left at SPEED_300 after $14+$3c updates.");
        }

        StepRoomEventFrames(129);
        if (_dialogue.IsOpen || impaEvent.Counter != 1)
            throw new InvalidOperationException("Impa's 210-update post-signal wait ended early.");
        StepRoomEventFrames(1);
        if (!_dialogue.IsOpen || _dialogue.Position.Y != 24 ||
            !_dialogue.CurrentMessage.StartsWith("That was\nfrightening!") ||
            !_dialogue.CurrentMessage.EndsWith("with you nearby.") ||
            octoroks[0].Active || octoroks[1].Active || octoroks[2].Active ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndThrow) != 3)
        {
            throw new InvalidOperationException(
                "TX_0102, automatic TX_0101 call, textbox placement, fake-Octorok cleanup, " +
                "or the three staggered SND_THROW calls diverged.");
        }

        _dialogue.Close();
        StepRoomEventFrames(1);
        StepRoomEventFrames(29);
        if (impaEvent.Counter != 1 || impa.Position != new Vector2(0x48, 0x38))
            throw new InvalidOperationException("Impa's 30-update post-text wait ended early.");
        StepRoomEventFrames(1);
        if (impaEvent.Counter != 0 || impaEvent.EncounterCommandIndex != 5)
            throw new InvalidOperationException(
                "Impa's post-text wait did not carry through setspeed on its completion update.");
        StepRoomEventFrames(1);
        if (impa.Position != new Vector2(0x48, 0x38) || impaEvent.Counter != 32)
        {
            throw new InvalidOperationException(
                "Impa moved during the setspeed/movedown script-command updates.");
        }
        StepRoomEventFrames(30);
        if (impaEvent.Counter != 2 || impa.Position != new Vector2(0x48, 0x47))
        {
            throw new InvalidOperationException(
                "Impa did not apply SPEED_080 through the high-coordinate byte for 30 updates.");
        }
        StepRoomEventFrames(1);
        if (impaEvent.Counter != 1 || impa.Position != new Vector2(0x48, 0x47))
            throw new InvalidOperationException("movedown $20 did not retain its final half-pixel fraction.");
        StepRoomEventFrames(1);
        if (_saveData.HasRoomFlag(0, 0x6a, OracleSaveData.RoomFlag40))
            throw new InvalidOperationException("Impa set room flag $40 before counter2 reached zero.");
        StepRoomEventFrames(1);
        if (!_roomEvents.Active || impaEvent.Following ||
            !_player.CutsceneControlled || impa.Position == _player.Position ||
            !_saveData.HasRoomFlag(0, 0x6a, OracleSaveData.RoomFlag40))
        {
            throw new InvalidOperationException(
                "scriptCmd_orRoomFlags did not set room flag $40 and yield before scriptend.");
        }
        StepRoomEventFrames(1);
        Vector2 followStart = _player.Position;
        if (_roomEvents.Active || !impaEvent.Following || _player.CutsceneControlled ||
            impa.Position != followStart || impa.FacingVector != Vector2I.Up ||
            !_saveData.HasRoomFlag(0, 0x6a, OracleSaveData.RoomFlag40))
        {
            throw new InvalidOperationException(
                "Impa did not set room flag $40, restore Link, and initialize the follower at Link's position.");
        }

        Vector2I[] impaDirections =
        {
            Vector2I.Up, Vector2I.Right, Vector2I.Down, Vector2I.Left
        };
        var impaAnimationHashes = new HashSet<ulong>();
        foreach (Vector2I direction in impaDirections)
        {
            impa.SetFacingDirection(direction);
            if (impa.CurrentAnimationOpaquePixels == 0 ||
                !impaAnimationHashes.Add(impa.CurrentAnimationPixelHash))
            {
                throw new InvalidOperationException(
                    $"Impa animation ${Array.IndexOf(impaDirections, direction):x2} " +
                    "was empty or reused another directional sprite.");
            }
        }
        impa.SetFacingDirection(Vector2I.Up);

        for (int update = 1; update <= 16; update++)
        {
            _player.WarpTo(followStart + Vector2.Right * update);
            _player.Face(Vector2I.Right);
            StepRoomEventFrames(1);
        }
        if (impa.Position != followStart)
            throw new InvalidOperationException("Impa advanced before the 16-entry Link path delay elapsed.");
        _player.WarpTo(followStart + Vector2.Right * 17);
        StepRoomEventFrames(1);
        if (impa.Position != followStart + Vector2.Right || impa.FacingVector != Vector2I.Right)
        {
            throw new InvalidOperationException(
                "checkUpdateFollowingLinkObject did not replay Link's first delayed position/direction.");
        }

        for (int update = 18; update <= 82; update++)
        {
            _player.WarpTo(followStart + Vector2.Right * update);
            StepRoomEventFrames(1);
        }
        if (_player.Position.X != 0x9a ||
            impa.Position != _player.Position + Vector2.Left * 16)
        {
            throw new InvalidOperationException(
                "Impa's path was not primed at the right screen edge before scrolling.");
        }

        _transitions.BeginScroll(_player, Vector2I.Right, 0x6b);
        NpcCharacter? incomingImpa = impaEvent.Actor;
        if (incomingImpa is null || incomingImpa == impa ||
            incomingImpa.Position != impa.Position + Vector2.Left * 160 ||
            impa.Active ||
            !impaEvent.Following)
        {
            throw new InvalidOperationException(
                "Following Impa was not transferred into room 0:6b with the original " +
                "screen offset and a retired outgoing rendering copy.");
        }
        for (int frame = 0; frame < 40; frame++)
        {
            UpdateScrollingTransition(1.0 / 60.0);
            _roomEvents.Update(1.0 / 60.0);
            Vector2 scrollingLink = _transitions.ScrollLinkPositionInDestination;
            Vector2 expectedImpa = new(
                Mathf.Floor(scrollingLink.X) - 16,
                Mathf.Floor(scrollingLink.Y));
            bool outgoingImpaVisible = _entities.OutgoingEntities<NpcCharacter>().Any(npc =>
                npc.Record.Id == 0x31 && npc.Record.SubId == 0x00 && npc.Active);
            if (_transitions.ScrollActive &&
                (incomingImpa.Position != expectedImpa || outgoingImpaVisible))
            {
                throw new InvalidOperationException(
                    $"Always-update Impa fell behind on right-scroll update {frame + 1}: " +
                    $"expected {expectedImpa}, got {incomingImpa.Position}, " +
                    $"outgoing visible={outgoingImpaVisible}.");
            }
        }
        if (IsTransitioning)
            throw new InvalidOperationException("The Impa right scroll did not finish in 40 updates.");
        if (incomingImpa.Position != _player.Position + Vector2.Left * 16)
        {
            throw new InvalidOperationException(
                "resetFollowingLinkObjectPosition did not place Impa 16 pixels behind " +
                $"Link after the right scroll (Link={_player.Position}, Impa={incomingImpa.Position}).");
        }
        StepRoomEventFrames(1);
        if (incomingImpa.Position != _player.Position + Vector2.Left * 16 ||
            incomingImpa.FacingVector != Vector2I.Right)
        {
            throw new InvalidOperationException(
                "The rebuilt path did not retain Impa at the left edge facing right on its first update.");
        }

        _player.Face(Vector2I.Left);
        for (int x = 0x16; x >= 0x06; x--)
        {
            _player.WarpTo(new Vector2(x, _player.Position.Y));
            StepRoomEventFrames(1);
        }
        if (incomingImpa.Position != _player.Position + Vector2.Right * 16 ||
            incomingImpa.FacingVector != Vector2I.Left)
        {
            throw new InvalidOperationException(
                "Impa's path was not primed at the left screen edge before scrolling.");
        }

        _transitions.BeginScroll(_player, Vector2I.Left, 0x6a);
        List<NpcCharacter> returningImpas = _npcNodes.Where(npc =>
            npc.Record.Id == 0x31 && npc.Record.SubId == 0x00).ToList();
        NpcCharacter? returningFollower = impaEvent.Actor;
        if (returningImpas.Count != 2 || returningFollower is null ||
            incomingImpa.Active ||
            !returningFollower.Active || returningImpas.Count(npc => npc.Active) != 1 ||
            !impaEvent.Following)
        {
            throw new InvalidOperationException(
                "Returning to room 0:6a retained both the completed placed Impa and her follower.");
        }
        for (int frame = 0; frame < 40; frame++)
        {
            UpdateScrollingTransition(1.0 / 60.0);
            _roomEvents.Update(1.0 / 60.0);
            Vector2 scrollingLink = _transitions.ScrollLinkPositionInDestination;
            Vector2 expectedImpa = new(
                Mathf.Floor(scrollingLink.X) + 16,
                Mathf.Floor(scrollingLink.Y));
            bool outgoingImpaVisible = _entities.OutgoingEntities<NpcCharacter>().Any(npc =>
                npc.Record.Id == 0x31 && npc.Record.SubId == 0x00 && npc.Active);
            if (_transitions.ScrollActive &&
                (returningFollower.Position != expectedImpa || outgoingImpaVisible))
            {
                throw new InvalidOperationException(
                    $"Always-update Impa fell behind on left-scroll update {frame + 1}: " +
                    $"expected {expectedImpa}, got {returningFollower.Position}, " +
                    $"outgoing visible={outgoingImpaVisible}.");
            }
        }
        if (IsTransitioning)
            throw new InvalidOperationException("The Impa left scroll did not finish in 40 updates.");
        if (returningFollower.Position != _player.Position + Vector2.Right * 16)
        {
            throw new InvalidOperationException(
                "resetFollowingLinkObjectPosition did not place Impa 16 pixels behind " +
                $"Link after the left scroll (Link={_player.Position}, Impa={returningFollower.Position}).");
        }
        StepRoomEventFrames(1);
        if (returningFollower.Position != _player.Position + Vector2.Right * 16 ||
            returningFollower.FacingVector != Vector2I.Left)
        {
            throw new InvalidOperationException(
                "The rebuilt path did not retain Impa at the right edge facing left on its first update.");
        }

        _saveData.SetGlobalFlag(OracleSaveData.GlobalFlagIntroDone, value: false);
        _saveData.SetRoomFlag(0, 0x39, OracleSaveData.RoomFlag40, value: false);
        _saveData.SetRoomFlag(0, 0x39, OracleSaveData.RoomFlag80, value: false);
        _transitions.BeginScroll(_player, Vector2I.Right, 0x39);
        NpcCharacter? gatheringFollower = impaEvent.Actor;
        if (gatheringFollower is null || !impaEvent.Following ||
            nayruIntro.ActorRegistry.Count != 7 || _roomEvents.Active)
        {
            throw new InvalidOperationException(
                "Room 0:39 did not create the seven intro gathering actors while Impa was following Link.");
        }
        FinishActiveScrollingTransitionForValidation();
        StepRoomEventFrames(1);
        if (gatheringFollower.Position != _player.Position + Vector2.Left * 16 ||
            nayruIntro.ActorRegistry.Values.Any(actor => !actor.Active))
        {
            throw new InvalidOperationException(
                "The complete Nayru gathering or following Impa did not survive the incoming room scroll.");
        }

        _transitions.BeginScroll(_player, Vector2I.Right, 0x3a);
        List<NpcCharacter> outgoingGathering = _entities.OutgoingEntities<NpcCharacter>()
            .Where(actor => actor.Name.ToString().StartsWith(
                "NayruIntro_", StringComparison.Ordinal))
            .ToList();
        if (nayruIntro.ActorRegistry.Count != 0 || !impaEvent.Following ||
            outgoingGathering.Count != 7 || outgoingGathering.Any(actor => !actor.Active))
        {
            throw new InvalidOperationException(
                "Leaving room 0:39 did not retain all seven dynamic audience actors in the outgoing scroll set.");
        }
        UpdateScrollingTransition(1.0 / 60.0);
        if (outgoingGathering.Any(actor => actor.TransitionDrawOffset != Vector2.Left * 4))
            throw new InvalidOperationException(
                "Room 0:39's dynamic audience did not move with the outgoing room texture.");
        FinishActiveScrollingTransitionForValidation();
        _transitions.BeginScroll(_player, Vector2I.Left, 0x39);
        NpcCharacter? returningGatheringFollower = impaEvent.Actor;
        if (returningGatheringFollower is null || !impaEvent.Following ||
            nayruIntro.ActorRegistry.Count != 7 ||
            nayruIntro.ActorRegistry.Values.Any(actor => !actor.Active))
        {
            throw new InvalidOperationException(
                "Re-entering pre-intro room 0:39 did not recreate all seven gathering actors.");
        }
        FinishActiveScrollingTransitionForValidation();

        ValidateImpaStoneEvent(impaEvent, encounterTrace);

        LoadValidationRoom(0, 0x6a);
        NpcCharacter? completedImpa = _npcNodes.Find(npc =>
            npc.Record.Id == 0x31 && npc.Record.SubId == 0x00);
        if (completedImpa is null || completedImpa.Active || _roomEvents.Active ||
            impaEvent.Following || impaEvent.FakeOctoroks.Count != 0)
        {
            throw new InvalidOperationException(
                "Room flag $40 did not suppress Impa and her fake Octoroks on room 0:6a re-entry.");
        }

        LoadValidationRoom(0, 0x7a);
        _player.WarpTo(new Vector2(0x38, 0x06));
        _player.Face(Vector2I.Up);
        impaEvent.UpdateHelpFrame(upPressed: true);
        if (impaEvent.HelpWaitingAtEdge || _roomEvents.Active || _dialogue.IsOpen)
        {
            throw new InvalidOperationException(
                "Room 0:7a flag $40 did not suppress TX_0100 on re-entry.");
        }

        CutsceneCommandTraceEntry[] helpStarts = encounterTrace.Entries
            .Where(entry =>
                entry.Source.Script == "interaction6b_subid00" &&
                entry.Phase == CutsceneCommandTracePhase.Started)
            .ToArray();
        string[] expectedHelpOpcodes =
        {
            "disablemenu", "setdisabledobjectscontinue", "setcounter", "showtext",
            "waitpreloadedcounter", "setdisabledobjectscontinue", "native",
            "orroomflagcontinue", "scriptend"
        };
        if (helpStarts.Length != expectedHelpOpcodes.Length ||
            helpStarts.Where((entry, index) =>
                entry.Source.Label != "interaction6b_subid00" ||
                entry.Source.CommandIndex != index ||
                entry.Source.Opcode != expectedHelpOpcodes[index] ||
                entry.Source.SourceLine <= 0 ||
                (index > 0 && entry.Source.SourceLine <=
                    helpStarts[index - 1].Source.SourceLine))
            .Any())
        {
            throw new InvalidOperationException(
                "The imported interaction6b_subid00 trace lost source lines, " +
                "native-operation order, or typed opcodes.");
        }

        int HelpCompletedUpdate(int commandIndex) => encounterTrace.Entries.Single(entry =>
            entry.Source.Script == "interaction6b_subid00" &&
            entry.Source.CommandIndex == commandIndex &&
            entry.Phase == CutsceneCommandTracePhase.Completed).ScriptUpdate;
        CutsceneCommandTraceEntry[] helpWaitUpdates = encounterTrace.Entries
            .Where(entry =>
                entry.Source.Script == "interaction6b_subid00" &&
                entry.Source.CommandIndex == 4 &&
                entry.Phase == CutsceneCommandTracePhase.Updated)
            .ToArray();
        if (helpStarts[0].ScriptUpdate != helpStarts[1].ScriptUpdate ||
            helpStarts[0].ScriptUpdate != helpStarts[2].ScriptUpdate ||
            helpStarts[0].ScriptUpdate != helpStarts[3].ScriptUpdate ||
            helpStarts[4].ScriptUpdate != HelpCompletedUpdate(3) + 1 ||
            helpWaitUpdates.Length != 29 ||
            helpWaitUpdates.Where((entry, index) => entry.Counter != 29 - index).Any() ||
            helpStarts[5].ScriptUpdate != HelpCompletedUpdate(4) ||
            helpStarts[6].ScriptUpdate != helpStarts[5].ScriptUpdate ||
            helpStarts[7].ScriptUpdate != helpStarts[5].ScriptUpdate ||
            helpStarts[8].ScriptUpdate != helpStarts[5].ScriptUpdate)
        {
            throw new InvalidOperationException(
                "interaction6b_subid00 did not preserve its same-update setup, " +
                "first-post-text decrement, 30-update counter, or completion handoff.");
        }

        CutsceneCommandTraceEntry[] starts = encounterTrace.Entries
            .Where(entry =>
                entry.Source.Script == "impaScript0" &&
                entry.Phase == CutsceneCommandTracePhase.Started)
            .ToArray();
        string[] expectedOpcodes =
        {
            "checkmemoryeq", "wait", "showtextdifferentforlinked", "wait",
            "setspeed", "move", "orroomflag", "scriptend"
        };
        if (starts.Length != expectedOpcodes.Length ||
            starts.Where((entry, index) =>
                entry.Source.Script != "impaScript0" ||
                entry.Source.Label != "impaScript0" ||
                entry.Source.CommandIndex != index ||
                entry.Source.Opcode != expectedOpcodes[index] ||
                entry.Source.SourceLine <= 0 ||
                (index > 0 && entry.Source.SourceLine <= starts[index - 1].Source.SourceLine))
            .Any())
        {
            throw new InvalidOperationException(
                "The imported impaScript0 trace lost source lines, command order, or typed opcodes.");
        }

        int CompletedUpdate(int commandIndex) => encounterTrace.Entries.Single(entry =>
            entry.Source.Script == "impaScript0" &&
            entry.Source.CommandIndex == commandIndex &&
            entry.Phase == CutsceneCommandTracePhase.Completed).ScriptUpdate;
        if (starts[1].ScriptUpdate != CompletedUpdate(0) + 1 ||
            starts[2].ScriptUpdate != CompletedUpdate(1) ||
            starts[3].ScriptUpdate != CompletedUpdate(2) + 1 ||
            starts[4].ScriptUpdate != CompletedUpdate(3) ||
            starts[5].ScriptUpdate != CompletedUpdate(4) + 1 ||
            starts[6].ScriptUpdate != CompletedUpdate(5) + 1 ||
            starts[7].ScriptUpdate != CompletedUpdate(6) + 1)
        {
            throw new InvalidOperationException(
                "impaScript0 did not preserve gate, text, wait, setspeed, counter2, " +
                "room-flag yield, or scriptend cadence.");
        }
        _roomEvents.CommandTraceSink = null;

        GD.Print("Validated room 0:7a $6b:$00 edge trigger, imported native-operation " +
            "source trace, fixed-bottom TX_0100, 30-update post-text wait, silent playable-intro " +
            "room music, room flag $40, and eight-Up handoff; " +
            "room 0:6a possessed Impa " +
            "$31:$00 PALH_97, three objectData fake Octoroks, linkCutscene1 $78/$04/$2e " +
            "cadence with SND_CLINK, staggered $14+$50/$3c/$5a escapes and three SND_THROW " +
            "calls, imported TX_0102/TX_0103 selection and source trace, 210/30 waits, " +
            "SPEED_080 movedown $20, MUS_FAIRY_FOUNTAIN volume 3, room flag $40, " +
            "animations $00-$03, single-copy " +
            "always-update scroll following, transition-end 16-entry follower-path rebuild, " +
            "room 0:39's seven-actor intro gathering during follow, clean leave/re-entry " +
            "recreation, and placed-Impa suppression when the follower returns.");
    }

    private void ValidateImpaStoneEvent(
        ImpaIntroEvent impaEvent,
        ValidationCutsceneTrace commandTrace)
    {
        const int group = 0;
        const int room = 0x59;
        ImpaIntroEventDatabase.ImpaStoneEventRecord record = impaEvent.Database.StoneRecord;
        ImpaIntroEventDatabase.ImpaStoneActorRecord stone = record.Actor;
        ImpaIntroEventDatabase.ImpaStoneTimingRecord timing = record.Timing;
        _saveData.SetRoomFlag(group, room, OracleSaveData.RoomFlag40, value: false);
        _saveData.SetRoomFlag(group, room, OracleSaveData.RoomFlag80, value: false);

        _transitions.BeginScroll(_player, Vector2I.Right, room);
        NpcCharacter? follower = impaEvent.Actor;
        NpcCharacter? stoneActor = impaEvent.StoneActor;
        Color stoneMidtone = new(0x0c / 31.0f, 0x12 / 31.0f, 0x11 / 31.0f);
        if (follower is null || stoneActor is null ||
            impaEvent.CurrentStoneStage != ImpaIntroEvent.StoneStage.WaitingForApproach ||
            stoneActor.Position != new Vector2(stone.InitialX, stone.InitialY) ||
            stone.SourceGrayscaleInverted ||
            stoneActor.CurrentAnimationTextureSize != new Vector2I(24, 16) ||
            stoneActor.CurrentAnimationOpaquePixels != 278 ||
            !stoneActor.CurrentAnimationUsesColor(stoneMidtone) ||
            stoneActor.ZIndex != NpcCharacter.FixedLowPriorityZIndex ||
            follower.ZIndex <= stoneActor.ZIndex)
        {
            throw new InvalidOperationException(
                "Room 0:59 did not transfer following Impa or instantiate the centered " +
                "INTERAC_TRIFORCE_STONE $34:$00 with its non-inverted 24x16 sprite, PALH_98, " +
                "and fixed priority 3 below follower Impa's relative priority 1/2.");
        }
        FinishActiveScrollingTransitionForValidation();

        int approachX = stone.ApproachX - 8;
        _player.WarpTo(
            new Vector2(approachX, stone.ApproachY + 0x20), recordSafe: false);
        for (int y = stone.ApproachY + 0x1f; y >= stone.ApproachY - 1; y--)
        {
            _player.SetScriptedPosition(new Vector2(approachX, y));
            StepRoomEventFrames(1);
        }
        int retainedStoneCutscenePriority = follower.ZIndex;
        if (stoneActor.ZIndex != NpcCharacter.FixedLowPriorityZIndex ||
            retainedStoneCutscenePriority != NpcCharacter.InFrontOfLinkZIndex)
        {
            throw new InvalidOperationException(
                "INTERAC_TRIFORCE_STONE priority 3 did not remain below follower Impa's " +
                "trigger-frame priority 2 after Link approached from below.");
        }
        if (!_player.CutsceneControlled || !_roomEvents.Active ||
            impaEvent.CurrentStoneStage != ImpaIntroEvent.StoneStage.SpotJumpHold ||
            impaEvent.Counter != timing.SpotHoldFrames)
        {
            throw new InvalidOperationException(
                "Impa did not clear following and begin the $1e-update first jump below $58/$78.");
        }

        StepRoomEventFrames(timing.SpotHoldFrames);
        int firstAirUpdates = 0;
        while (impaEvent.CurrentStoneStage == ImpaIntroEvent.StoneStage.SpotJumpAir &&
            firstAirUpdates++ < 60)
        {
            StepRoomEventFrames(1);
        }
        if (firstAirUpdates != 29 ||
            impaEvent.CurrentStoneStage != ImpaIntroEvent.StoneStage.FirstLandingWait ||
            follower.ScriptDrawOffset != Vector2.Zero)
        {
            throw new InvalidOperationException(
                $"Impa's -$1c0/$20 first jump did not land after 29 gravity updates ({firstAirUpdates}).");
        }
        StepRoomEventFrames(timing.FirstLandingWait);
        if (!_dialogue.IsOpen || _dialogue.CurrentMessage !=
            DialogueBox.PlainText(record.Texts.First.Message) ||
            impaEvent.Counter != timing.FirstTextPostFrames)
        {
            throw new InvalidOperationException("Impa did not show TX_0104 after the first landing wait.");
        }
        _dialogue.Close();
        StepRoomEventFrames(1 + timing.FirstTextPostFrames);
        int approachUpdates = 0;
        while (impaEvent.CurrentStoneStage == ImpaIntroEvent.StoneStage.ApproachStone &&
            approachUpdates++ < 80)
        {
            StepRoomEventFrames(1);
        }
        if (impaEvent.CurrentStoneStage != ImpaIntroEvent.StoneStage.AtStoneWait ||
            follower.Position != new Vector2(stone.TargetX, stone.TargetY) ||
            !Mathf.IsEqualApprox(follower.AnimationRate, 1.0f) ||
            follower.ZIndex != retainedStoneCutscenePriority)
        {
            throw new InvalidOperationException(
                "Impa did not reach $38/$38 at SPEED_300 with the original close-radius " +
                "snap and retained trigger-frame priority.");
        }

        StepRoomEventFrames(timing.StoneWaitFrames + timing.SecondHoldFrames);
        int secondAirUpdates = 0;
        while (impaEvent.CurrentStoneStage == ImpaIntroEvent.StoneStage.SecondJumpAir &&
            secondAirUpdates++ < 60)
        {
            StepRoomEventFrames(1);
        }
        if (secondAirUpdates != 25 ||
            impaEvent.CurrentStoneStage != ImpaIntroEvent.StoneStage.SecondLandingWait)
        {
            throw new InvalidOperationException(
                $"Impa's -$180/$20 second jump did not land after 25 gravity updates ({secondAirUpdates}).");
        }
        StepRoomEventFrames(timing.SecondLandingWait);
        if (!_dialogue.IsOpen || _dialogue.CurrentMessage !=
            DialogueBox.PlainText(record.Texts.Sign.Message) ||
            !_dialogue.CurrentMessage.Contains('▲'))
        {
            throw new InvalidOperationException("Impa did not show expanded TX_0105 with the Triforce glyph.");
        }
        _dialogue.Close();
        StepRoomEventFrames(1 + timing.SignTextPostFrames);

        int linkUpdates = 0;
        while (!_dialogue.IsOpen && linkUpdates++ < 240)
        {
            StepRoomEventFrames(1);
        }
        if (!_dialogue.IsOpen || _dialogue.CurrentMessage !=
            DialogueBox.PlainText(record.Texts.Request.Message) ||
            _player.Position != new Vector2(stone.LinkTargetX, stone.LinkTargetY) ||
            follower.ZIndex != retainedStoneCutscenePriority)
        {
            throw new InvalidOperationException(
                "linkCutscene2 did not route Link through $38/$48 while Impa retained " +
                "priority 2 through its 8/60/16 waits before TX_0106.");
        }
        _dialogue.Close();
        int firstRetreatUpdates = 0;
        while (!_dialogue.IsOpen && firstRetreatUpdates++ < 120)
            StepRoomEventFrames(1);
        if (firstRetreatUpdates != 98 || !_dialogue.IsOpen || _dialogue.CurrentMessage !=
            DialogueBox.PlainText(record.Texts.Hesitation.Message) ||
            follower.Position.X != stone.TargetX - 16)
        {
            throw new InvalidOperationException(
                "Imported impaScript_moveAwayFromRock did not reach TX_0107 after " +
                "the exact 98-update first retreat path.");
        }
        _dialogue.Close();
        int secondRetreatUpdates = 0;
        while (!_dialogue.IsOpen && secondRetreatUpdates++ < 120)
            StepRoomEventFrames(1);
        if (secondRetreatUpdates != 95 || !_dialogue.IsOpen || _dialogue.CurrentMessage !=
            DialogueBox.PlainText(record.Texts.Failure.Message) ||
            follower.Position.X != stone.TargetX - 32)
        {
            throw new InvalidOperationException(
                "Imported impaScript_moveAwayFromRock did not reach TX_0108 after " +
                "the exact 95-update second retreat path.");
        }
        _dialogue.Close();
        int finishRetreatUpdates = 0;
        while (impaEvent.CurrentStoneStage != ImpaIntroEvent.StoneStage.WaitingForPush &&
            finishRetreatUpdates++ < 60)
        {
            StepRoomEventFrames(1);
        }
        if (finishRetreatUpdates != 31 ||
            impaEvent.CurrentStoneStage != ImpaIntroEvent.StoneStage.WaitingForPush ||
            _roomEvents.Active || _player.CutsceneControlled ||
            impaEvent.WaitingNpcInitialized || follower.TextId != 0 ||
            follower.Record.CanFace ||
            follower.CurrentScriptAnimationSource != impaEvent.Database.Record.RightAnimation ||
            _entities.BlocksLink(follower.Position))
        {
            throw new InvalidOperationException(
                "Impa did not retain animation $01 for the one update between installing " +
                "impaScript_waitForRockToBeMoved and running rungenericnpc TX_010b.");
        }

        CutsceneCommandTraceEntry[] prePushStarts = commandTrace.Entries
            .Where(entry =>
                entry.Source.Script == "impaScript_moveAwayFromRock" &&
                entry.Phase == CutsceneCommandTracePhase.Started)
            .ToArray();
        string[] expectedPrePushOpcodes =
        {
            "checkmemoryeq", "setanimation", "wait", "showtext", "wait",
            "setanimation", "setangle", "setspeed", "applyspeed", "wait",
            "showtext", "wait", "applyspeed", "wait", "showtext", "wait",
            "writememory", "scriptend"
        };
        if (prePushStarts.Length != expectedPrePushOpcodes.Length ||
            prePushStarts.Where((entry, index) =>
                entry.Source.Label != "impaScript_moveAwayFromRock" ||
                entry.Source.CommandIndex != index ||
                entry.Source.Opcode != expectedPrePushOpcodes[index] ||
                entry.Source.SourceLine <= 0 ||
                (index > 0 && entry.Source.SourceLine <=
                    prePushStarts[index - 1].Source.SourceLine))
            .Any())
        {
            throw new InvalidOperationException(
                "The imported impaScript_moveAwayFromRock trace lost source lines, " +
                "command order, or typed opcodes.");
        }

        int PrePushCompletedUpdate(int commandIndex) => commandTrace.Entries.Single(entry =>
            entry.Source.Script == "impaScript_moveAwayFromRock" &&
            entry.Source.CommandIndex == commandIndex &&
            entry.Phase == CutsceneCommandTracePhase.Completed).ScriptUpdate;
        int PrePushDuration(int commandIndex) =>
            PrePushCompletedUpdate(commandIndex) - prePushStarts[commandIndex].ScriptUpdate;
        int gateUpdates = commandTrace.Entries.Count(entry =>
            entry.Source.Script == "impaScript_moveAwayFromRock" &&
            entry.Source.CommandIndex == 0 &&
            entry.Phase == CutsceneCommandTracePhase.Updated);
        if (gateUpdates == 0 ||
            PrePushDuration(2) != timing.RequestLeadFrames ||
            PrePushDuration(4) != timing.RequestPostFrames ||
            PrePushDuration(8) != timing.FirstBackAwayFrames ||
            PrePushDuration(9) != timing.BetweenFirstBackAwayFrames ||
            PrePushDuration(11) != timing.HesitationPostFrames ||
            PrePushDuration(12) != timing.SecondBackAwayFrames ||
            PrePushDuration(13) != timing.BetweenSecondBackAwayFrames ||
            PrePushDuration(15) != timing.FailurePostFrames ||
            prePushStarts[1].ScriptUpdate != PrePushCompletedUpdate(0) + 1 ||
            prePushStarts[2].ScriptUpdate != PrePushCompletedUpdate(1) + 1 ||
            prePushStarts[3].ScriptUpdate != PrePushCompletedUpdate(2) ||
            prePushStarts[4].ScriptUpdate != PrePushCompletedUpdate(3) + 1 ||
            prePushStarts[5].ScriptUpdate != PrePushCompletedUpdate(4) ||
            prePushStarts[6].ScriptUpdate != PrePushCompletedUpdate(5) + 1 ||
            prePushStarts[7].ScriptUpdate != PrePushCompletedUpdate(6) + 1 ||
            prePushStarts[8].ScriptUpdate != PrePushCompletedUpdate(7) + 1 ||
            prePushStarts[9].ScriptUpdate != PrePushCompletedUpdate(8) + 1 ||
            prePushStarts[10].ScriptUpdate != PrePushCompletedUpdate(9) ||
            prePushStarts[11].ScriptUpdate != PrePushCompletedUpdate(10) + 1 ||
            prePushStarts[12].ScriptUpdate != PrePushCompletedUpdate(11) ||
            prePushStarts[13].ScriptUpdate != PrePushCompletedUpdate(12) + 1 ||
            prePushStarts[14].ScriptUpdate != PrePushCompletedUpdate(13) ||
            prePushStarts[15].ScriptUpdate != PrePushCompletedUpdate(14) + 1 ||
            prePushStarts[16].ScriptUpdate != PrePushCompletedUpdate(15) ||
            prePushStarts[17].ScriptUpdate != prePushStarts[16].ScriptUpdate)
        {
            throw new InvalidOperationException(
                "impaScript_moveAwayFromRock did not preserve the native $02/$03 gate, " +
                "yield boundaries, waits, counter2 movement, or same-update $04 completion.");
        }

        StepRoomEventFrames(1);
        if (!impaEvent.WaitingNpcInitialized ||
            !_entities.BlocksLink(follower.Position) ||
            !_collision.Collides(follower.Position) || follower.Record.CanFace ||
            follower.CurrentScriptAnimationSource != impaEvent.Database.Record.RightAnimation ||
            follower.ZIndex != NpcCharacter.BehindLinkZIndex)
        {
            throw new InvalidOperationException(
                "genericNpcScript did not install Impa's $06/$06 collision and TX_010b " +
                "or resume relative priority without changing her animation or enabling " +
                "automatic Link-facing.");
        }

        _player.WarpTo(follower.Position);
        impaEvent.UpdateStoneFrame(pushing: false);
        if (_player.Position != follower.Position + Vector2.Left * 12)
        {
            throw new InvalidOperationException(
                "interactionAnimateAsNpc did not resolve an exact Impa/Link overlap " +
                "horizontally by the combined $06+$06 collision radii.");
        }

        _player.WarpTo(follower.Position + Vector2.Right * 16);
        _player.Face(Vector2I.Left);
        if (!TryInteract(_player) || !_dialogue.IsOpen ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(record.Texts.Talk.Message) ||
            follower.Record.CanFace ||
            follower.CurrentScriptAnimationSource != impaEvent.Database.Record.RightAnimation)
        {
            throw new InvalidOperationException(
                "Waiting Impa did not expose rungenericnpc TX_010b while holding animation $01.");
        }
        _dialogue.Close();
        _player.WarpTo(new Vector2(0x50, stone.LeaveY + 2));
        impaEvent.UpdateStoneFrame(pushing: false, downPressed: true);
        if (!_dialogue.IsOpen || _player.Position.Y != stone.LeaveY ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(record.Texts.Leave.Message) ||
            !_dialogue.CurrentMessage.EndsWith("move this!"))
        {
            throw new InvalidOperationException(
                "The room $59 boundary guard did not clamp Y=$76 and expand TX_010a -> TX_010c.");
        }
        _dialogue.Close();

        // Approach from the stone's left through the normal movement path. The
        // original interaction accepts either horizontal side; this verifies
        // that entity collision leaves Link at the centered pushing distance
        // and that the room event starts counting rightward input there.
        _player.WarpTo(new Vector2(stone.InitialX - 23.5f, stone.InitialY));
        _player.Face(Vector2I.Right);
        Input.ActionPress("move_right");
        int leftApproachGuard = 16;
        while (_player.Position.X < stone.InitialX - 16 && leftApproachGuard-- > 0)
        {
            _player._PhysicsProcess(1.0 / 60.0);
            StepRoomEventFrames(1);
        }
        Input.ActionRelease("move_right");
        if (_player.Position != new Vector2(stone.InitialX - 16, stone.InitialY) ||
            impaEvent.StonePushCounter != timing.PushHoldFrames - 1 ||
            !_player.IsPushing)
        {
            throw new InvalidOperationException(
                "The Triforce stone could not be pushed from its left side through normal movement.");
        }
        StepRoomEventFrames(1);
        if (impaEvent.StonePushCounter != timing.PushHoldFrames || _player.IsPushing)
        {
            throw new InvalidOperationException(
                "Releasing the left-side stone push did not reset its hold counter.");
        }

        _player.Face(Vector2I.Right);
        impaEvent.UpdateStoneFrame(pushing: false);
        if (impaEvent.StonePushCounter != timing.PushHoldFrames)
            throw new InvalidOperationException("The stone push counter did not reset to $14.");

        // Dynamic actors are not room-tile walls, so Link's generic wall-push
        // detector is deliberately false here. Drive the actual room-event
        // input path to ensure the interaction observes the held direction.
        _player.UpdatePushingState(Vector2.Right);
        if (_player.IsPushing)
            throw new InvalidOperationException(
                "The Triforce stone was incorrectly treated as static room-tile collision.");
        Input.ActionPress("move_right");
        Input.ActionPress("attack");
        StepRoomEventFrames(1);
        Input.ActionRelease("attack");
        if (impaEvent.StonePushCounter != timing.PushHoldFrames || _player.IsPushing)
        {
            throw new InvalidOperationException(
                "objectCheckLinkPushingAgainstCenter counted a push update while A was held.");
        }
        StepRoomEventFrames(timing.PushHoldFrames - 1);
        if (impaEvent.CurrentStoneStage != ImpaIntroEvent.StoneStage.WaitingForPush ||
            impaEvent.StonePushCounter != 1 || !_player.IsPushing)
        {
            throw new InvalidOperationException("The Triforce stone moved before 20 centered push updates.");
        }
        StepRoomEventFrames(1);
        Input.ActionRelease("move_right");
        if (impaEvent.CurrentStoneStage != ImpaIntroEvent.StoneStage.PushStarted ||
            !_player.CutsceneControlled || !_player.IsPushing ||
            impaEvent.StoneMoveCounter != timing.StoneMoveFrames)
        {
            throw new InvalidOperationException(
                "INTERAC_TRIFORCE_STONE did not start linkCutscene6 and its $40-update movement.");
        }

        for (int update = 1; update < timing.StoneMoveFrames; update++)
        {
            StepRoomEventFrames(1);
            if (Mathf.Abs(stoneActor.Position.X - _player.Position.X) <
                    stone.CollisionRadiusX + NpcCharacter.LinkCollisionRadius &&
                Mathf.Abs(stoneActor.Position.Y - _player.Position.Y) <
                    stone.CollisionRadiusY + NpcCharacter.LinkCollisionRadius)
            {
                throw new InvalidOperationException(
                    $"objectPreventLinkFromPassing allowed Link inside the moving stone " +
                    $"on update {update} (Link={_player.Position}, stone={stoneActor.Position}).");
            }
        }
        if (_saveData.HasRoomFlag(group, room, OracleSaveData.RoomFlag80) ||
            impaEvent.StoneMoveCounter != 1)
        {
            throw new InvalidOperationException("The stone set room flag $80 before counter1 reached zero.");
        }
        StepRoomEventFrames(1);
        if (_saveData.HasRoomFlag(group, room, OracleSaveData.RoomFlag40) ||
            !_saveData.HasRoomFlag(group, room, OracleSaveData.RoomFlag80) ||
            stoneActor.Position != new Vector2(stone.RightX, stone.InitialY) ||
            _player.Position != new Vector2(0x37, stone.InitialY) ||
            _collision.Collides(_player.Position) ||
            _currentRoom.GetMetatile(new Vector2(stone.RightX, stone.MovedY)) !=
                stone.FinalLayoutTile ||
            !_currentRoom.IsSolid(new Vector2(stone.RightX, stone.MovedY)))
        {
            throw new InvalidOperationException(
                "The right-pushed stone did not snap to X=$48, leave Link outside at X=$37, " +
                $"set room flag $80, and install collision $0f (flags=" +
                $"{_saveData.GetRoomFlags(group, room):x2}, stone={stoneActor.Position}, " +
                $"Link={_player.Position}, LinkCollision={_collision.Collides(_player.Position)}, " +
                $"tile={_currentRoom.GetMetatile(new Vector2(stone.RightX, stone.MovedY)):x2}, " +
                $"solid={_currentRoom.IsSolid(new Vector2(stone.RightX, stone.MovedY))}).");
        }

        int responseUpdates = 0;
        bool sawRightFacingReunion = false;
        while (!_dialogue.IsOpen && responseUpdates++ < 400)
        {
            StepRoomEventFrames(1);
            if (!sawRightFacingReunion && commandTrace.Entries.Any(entry =>
                entry.Source.Script == "impaScript_rockJustMoved" &&
                entry.Source.CommandIndex == 10 &&
                entry.Phase == CutsceneCommandTracePhase.Started))
            {
                sawRightFacingReunion = true;
                if (follower.CurrentScriptAnimationSource !=
                    impaEvent.Database.Record.RightAnimation)
                {
                    throw new InvalidOperationException(
                        "Impa did not retain animation $01 while moving right to rejoin Link.");
                }
            }
        }
        if (!_dialogue.IsOpen || _dialogue.CurrentMessage !=
            DialogueBox.PlainText(record.Texts.Thanks.Message) ||
            responseUpdates >= 400 || !sawRightFacingReunion ||
            _player.FacingVector != Vector2I.Down || _player.IsPushing)
        {
            throw new InvalidOperationException(
                "Impa's right-push 4+65+120 response, SPEED_100 move, " +
                "cfd0=$07 Link pose reset, or TX_0109 timing stalled.");
        }
        _dialogue.Close();
        int finishUpdates = 0;
        bool sawUpFacingFinalMove = false;
        while (impaEvent.CurrentStoneStage != ImpaIntroEvent.StoneStage.Moved &&
            finishUpdates++ < 100)
        {
            StepRoomEventFrames(1);
            if (!sawUpFacingFinalMove && commandTrace.Entries.Any(entry =>
                entry.Source.Script == "impaScript_rockJustMoved" &&
                entry.Source.CommandIndex == 21 &&
                entry.Phase == CutsceneCommandTracePhase.Started))
            {
                sawUpFacingFinalMove = true;
                if (follower.FacingVector != Vector2I.Up)
                {
                    throw new InvalidOperationException(
                        "moveup $20 did not select Impa's up-facing animation before reunion movement.");
                }
            }
        }
        if (!sawUpFacingFinalMove || !impaEvent.Following ||
            _player.CutsceneControlled || _player.FacingVector != Vector2I.Down ||
            follower.FacingVector != Vector2I.Down || follower.Position != _player.Position)
        {
            throw new InvalidOperationException(
                "Impa did not face up for moveup $20, finish TX_0109, face down with Link, " +
                "and rebuild following.");
        }

        CutsceneCommandTraceEntry[] rightPostPushStarts = commandTrace.Entries
            .Where(entry =>
                entry.Source.Script == "impaScript_rockJustMoved" &&
                entry.Phase == CutsceneCommandTracePhase.Started)
            .ToArray();
        int[] expectedRightPostPushCommands =
        {
            0, 1, 6, 7, 8, 9, 10, 11, 12, 15, 16, 17, 18, 19, 20, 21, 22
        };
        if (rightPostPushStarts.Length != expectedRightPostPushCommands.Length ||
            rightPostPushStarts.Where((entry, pathIndex) =>
                entry.Source.CommandIndex != expectedRightPostPushCommands[pathIndex] ||
                entry.Source.SourceLine <= 0 ||
                (pathIndex > 0 && entry.Source.SourceLine <=
                    rightPostPushStarts[pathIndex - 1].Source.SourceLine))
            .Any())
        {
            throw new InvalidOperationException(
                "The right-push impaScript_rockJustMoved trace did not follow the " +
                "imported branch and source order.");
        }

        CutsceneCommandTraceEntry PrimaryRightCompleted(int commandIndex) =>
            commandTrace.Entries.Single(entry =>
                entry.Source.Script == "impaScript_rockJustMoved" &&
                entry.Source.CommandIndex == commandIndex &&
                entry.Phase == CutsceneCommandTracePhase.Completed);
        int PrimaryRightStartedUpdate(int commandIndex) => rightPostPushStarts.Single(entry =>
            entry.Source.CommandIndex == commandIndex).ScriptUpdate;
        int PrimaryRightDuration(int commandIndex) =>
            PrimaryRightCompleted(commandIndex).ScriptUpdate -
            PrimaryRightStartedUpdate(commandIndex);
        if (PrimaryRightCompleted(1).NextCommandIndex != 6 ||
            PrimaryRightCompleted(12).NextCommandIndex != 15 ||
            PrimaryRightDuration(0) != timing.ReactionLeadFrames ||
            PrimaryRightDuration(6) != timing.RightBranchWaitFrames ||
            PrimaryRightDuration(7) != timing.CommonWaitFrames ||
            PrimaryRightDuration(10) != timing.ResponseRightFrames ||
            PrimaryRightDuration(11) != timing.ResponseWait1Frames ||
            PrimaryRightDuration(17) != timing.PoseWaitFrames ||
            PrimaryRightDuration(19) != timing.ThanksPostFrames ||
            PrimaryRightDuration(21) != timing.FinalMoveFrames ||
            PrimaryRightStartedUpdate(1) != PrimaryRightCompleted(0).ScriptUpdate ||
            PrimaryRightStartedUpdate(6) != PrimaryRightCompleted(0).ScriptUpdate ||
            PrimaryRightStartedUpdate(12) != PrimaryRightCompleted(11).ScriptUpdate ||
            PrimaryRightStartedUpdate(15) != PrimaryRightStartedUpdate(12) ||
            PrimaryRightStartedUpdate(16) != PrimaryRightStartedUpdate(15) ||
            PrimaryRightStartedUpdate(18) != PrimaryRightCompleted(17).ScriptUpdate ||
            PrimaryRightStartedUpdate(20) != PrimaryRightCompleted(19).ScriptUpdate ||
            PrimaryRightStartedUpdate(22) != PrimaryRightCompleted(21).ScriptUpdate + 1)
        {
            throw new InvalidOperationException(
                "The right-push post-stone script lost a branch decision, counter duration, " +
                "yield boundary, or same-update continuation.");
        }

        // Execute the imported left-push branch independently of room state;
        // the right path above covers the complete native stone/Link integration.
        // This second lane proves both jump targets and all skipped commands.
        var leftPostPushHost = new ValidationImpaPostPushHost(linkAngle: 0x18);
        var leftPostPushRunner = new CutsceneCommandRunner(leftPostPushHost);
        leftPostPushRunner.Start(impaEvent.Database.StonePostPushCommands);
        int leftGuard = 0;
        while (leftPostPushRunner.Active && leftGuard++ < 500)
        {
            leftPostPushHost.AdvanceValidationFrame();
            leftPostPushRunner.AdvanceFrame();
            if (leftPostPushHost.DialogueOpen)
                leftPostPushHost.CloseDialogue();
        }
        int[] expectedLeftPostPushCommands =
        {
            0, 1, 2, 3, 4, 5, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17,
            18, 19, 20, 21, 22
        };
        CutsceneCommandTraceEntry[] leftPostPushStarts = leftPostPushHost.Trace.Entries
            .Where(entry => entry.Phase == CutsceneCommandTracePhase.Started)
            .ToArray();
        CutsceneCommandTraceEntry LeftCompleted(int commandIndex) =>
            leftPostPushHost.Trace.Entries.Single(entry =>
                entry.Source.CommandIndex == commandIndex &&
                entry.Phase == CutsceneCommandTracePhase.Completed);
        if (leftPostPushRunner.Active || !leftPostPushHost.Ended ||
            leftPostPushHost.Signal != 0x07 ||
            leftPostPushHost.TextIds.ToArray() is not [0x0109] ||
            leftPostPushStarts.Length != expectedLeftPostPushCommands.Length ||
            leftPostPushStarts.Where((entry, pathIndex) =>
                entry.Source.CommandIndex != expectedLeftPostPushCommands[pathIndex]).Any() ||
            LeftCompleted(1).NextCommandIndex != 2 ||
            LeftCompleted(5).NextCommandIndex != 7 ||
            LeftCompleted(12).NextCommandIndex != 13 ||
            leftPostPushHost.Facing != Vector2I.Up)
        {
            throw new InvalidOperationException(
                "The imported left-push branch did not run both corrections, signal $07, " +
                "or complete the reunion path.");
        }

        LoadValidationRoom(group, room);
        NpcCharacter? movedStone = impaEvent.StoneActor;
        if (movedStone is null || movedStone.Position != new Vector2(stone.RightX, stone.MovedY) ||
            _roomEvents.Active || impaEvent.Following ||
            !_currentRoom.IsSolid(new Vector2(stone.RightX, stone.MovedY)))
        {
            throw new InvalidOperationException(
                "PART_TRIFORCE_STONE $5a:$5a did not restore the right-side solid stone on re-entry.");
        }

        _saveData.SetRoomFlag(group, room, OracleSaveData.RoomFlag80, value: false);
        _saveData.SetRoomFlag(group, room, OracleSaveData.RoomFlag40);
        LoadValidationRoom(group, room);
        movedStone = impaEvent.StoneActor;
        if (movedStone is null || movedStone.Position != new Vector2(stone.LeftX, stone.MovedY) ||
            !_currentRoom.IsSolid(new Vector2(stone.LeftX, stone.MovedY)) ||
            _currentRoom.IsSolid(new Vector2(stone.RightX, stone.MovedY)))
        {
            throw new InvalidOperationException(
                "PART_TRIFORCE_STONE $5a:$5a did not select the left-side $40 position on re-entry.");
        }

        GD.Print("Validated room 0:59 Impa/Triforce-stone event: PALH_98 interaction/part " +
            "forms, fixed priority 3 below follower Impa, retained trigger-frame priority 2 " +
            "while Link approaches, two fixed-point jumps, " +
            "TX_0104-$010b, native linkCutscene2 targeting, imported " +
            "impaScript_moveAwayFromRock source trace and $02/$03/$04 handshake, " +
            "two SPEED_080 retreats, exact rungenericnpc wait animation/collision/talk loop, " +
            "A/B-safe 20-update push, 64-update SPEED_40 movement with per-update " +
            "objectPreventLinkFromPassing, linkCutscene6, direction flags $40/$80, imported " +
            "impaScript_rockJustMoved left/right branch traces, response waits, moveup $11/$20 " +
            "facing resets, $07, TX_0109 follower restore, final collision, sounds, and " +
            "completed re-entry.");
    }

    private void ValidateMakuTreeDisappearanceCutscene()
    {
        MakuTreeDisappearanceEvent makuEvent = _roomEvents.MakuTree;
        MakuTreeCutsceneDatabase makuDatabase = makuEvent.Database;
        MakuTreeCutsceneDatabase.MakuTreeCutsceneRecord makuRecord = makuDatabase.Record;
        var commandTrace = new ValidationCutsceneTrace();
        _roomEvents.CommandTraceSink = commandTrace;
        _sound.ClearPlayRequestAudit();
        LoadValidationRoom(0, 0x38);
        // The event is entered through room position $52 (the open $dc tile),
        // before its simulated right/up approach takes over.
        _player.WarpTo(new Vector2(0x28, 0x58));
        NpcCharacter? makuTree = _npcNodes.Find(npc =>
            npc.Record.Id == 0x87 && npc.Record.SubId == 0x00);
        if (makuTree is null || !_roomEvents.Active || !makuTree.Active ||
            makuTree.Position != new Vector2(0x50, 0x40))
        {
            throw new InvalidOperationException(
                "Room 0:38 did not start the $87:$01 Maku Tree entry event at $40/$50.");
        }
        bool retainedPalettes = true;
        for (int header = 0; header < MakuTreeCutsceneDatabase.PaletteCount; header++)
        for (int palette = 4;
            palette < MakuTreeCutsceneDatabase.BackgroundPalettesPerHeader;
            palette++)
        for (int shade = 0; shade < MakuTreeCutsceneDatabase.ColorsPerPalette; shade++)
        {
            retainedPalettes &= makuDatabase.BackgroundPalettes[header, palette, shade]
                .IsEqualApprox(makuDatabase.BackgroundPalettes[
                    makuRecord.InitialPaletteHeader, palette, shade]);
        }
        // Metatile $8f at room tile 3,7 selects BG palette 7. Pixel $31,$71
        // uses its first color, making it a focused check for the gate shown
        // during the unswapped-layout lead-in.
        OracleRoomData unswappedRoom = _currentRoom;
        Vector2I gatePixelPosition = new(0x31, 0x71);
        Color gatePixel = unswappedRoom.GetRenderedPixelForValidation(gatePixelPosition);
        Color expectedGate = makuDatabase.BackgroundPalettes[
            makuRecord.InitialPaletteHeader, 5, 0];
        bool GateMatchesExpected(Color color) =>
            Mathf.RoundToInt(color.R * 31) == Mathf.RoundToInt(expectedGate.R * 31) &&
            Mathf.RoundToInt(color.G * 31) == Mathf.RoundToInt(expectedGate.G * 31) &&
            Mathf.RoundToInt(color.B * 31) == Mathf.RoundToInt(expectedGate.B * 31);
        if (makuRecord.InitialPaletteHeader != 2 ||
            makuDatabase.BackgroundPalettes.GetLength(0) != 4 ||
            makuDatabase.BackgroundPalettes.GetLength(1) != 6 ||
            makuDatabase.BackgroundPalettes.GetLength(2) != 4 ||
            !retainedPalettes || makuEvent.PaletteHeader != makuRecord.InitialPaletteHeader ||
            Mathf.RoundToInt(expectedGate.R * 31) != 0x19 ||
            Mathf.RoundToInt(expectedGate.G * 31) != 0x15 ||
            Mathf.RoundToInt(expectedGate.B * 31) != 0x02 ||
            !GateMatchesExpected(gatePixel))
        {
            throw new InvalidOperationException(
                $"The unswapped Maku Tree room did not apply PALH_8f to BG palettes 2-7 " +
                $"before simulated input (header={makuEvent.PaletteHeader}, " +
                $"gate={gatePixel}, expected={expectedGate}, retained={retainedPalettes}).");
        }
        if (makuTree.CurrentAnimationTextureSize.X <= 32 ||
            makuTree.CurrentAnimationTextureSize.Y <= 32 ||
            makuTree.CurrentAnimationOffset.Y >= -16)
        {
            throw new InvalidOperationException(
                $"The Maku Tree face OAM was clipped to an ordinary NPC canvas " +
                $"(size={makuTree.CurrentAnimationTextureSize}, offset={makuTree.CurrentAnimationOffset}).");
        }

        Vector2 inputStart = _player.Position;
        StepRoomEventFrames(60);
        if (_player.Position != inputStart)
            throw new InvalidOperationException("Maku Tree simulated input did not begin with 60 idle updates.");
        StepRoomEventFrames(48);
        if (makuEvent.InputFrame != 108 || _player.FacingVector != Vector2I.Right ||
            _player.Position.X <= inputStart.X)
        {
            throw new InvalidOperationException(
                $"Maku Tree simulated input did not hold BTN_RIGHT for exactly 48 updates " +
                $"(input={makuEvent.InputFrame}, facing={_player.FacingVector}, " +
                $"start={inputStart}, current={_player.Position}).");
        }
        StepRoomEventFrames(4);
        Vector2 beforeUp = _player.Position;
        StepRoomEventFrames(14);
        if (makuEvent.InputFrame != 126 || _player.FacingVector != Vector2I.Up ||
            _player.Position.Y >= beforeUp.Y)
        {
            throw new InvalidOperationException(
                "Maku Tree simulated input did not hold BTN_UP for exactly 14 updates.");
        }
        StepRoomEventFrames(84);
        if (_dialogue.IsOpen || makuEvent.CurrentCommandIndex != 5 ||
            makuEvent.Counter != 3)
        {
            throw new InvalidOperationException(
                "The Maku Tree script preamble did not retain its collision/gate update boundaries.");
        }
        StepRoomEventFrames(3);
        if (!_dialogue.IsOpen || _dialogue.Position.Y != 96 ||
            !_dialogue.CurrentMessage.StartsWith("Pleased to meet\nyou, young hero."))
        {
            throw new InvalidOperationException(
                "TX_0564 did not open after the script preamble and original 210-update wait.");
        }

        _dialogue.Close();
        StepRoomEventFrames(61);
        if (_sound.LastPlayRequest != OracleSoundEngine.SndCtrlStopMusic ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndCtrlStopMusic) != 1)
        {
            throw new InvalidOperationException(
                "The Maku Tree did not stop its music after the 60-update post-text wait.");
        }
        StepRoomEventFrames(1);
        if (makuTree.CurrentAnimationFrame != 0 ||
            makuTree.CurrentAnimationOpaquePixels == 0)
        {
            throw new InvalidOperationException(
                "The Maku Tree animation command did not reset the frown to visible frame zero.");
        }
        StepRoomEventFrames(3);
        if (makuTree.CurrentAnimationFrame != 1)
            throw new InvalidOperationException(
                "INTERAC_MAKU_TREE animation 4 did not use its original four-update first frame.");
        StepRoomEventFrames(57);
        if (_sound.LastPlayRequest != OracleSoundEngine.SndMakuDisappear ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndMakuDisappear) != 1)
        {
            throw new InvalidOperationException(
                "The first SND_MAKUDISAPPEAR did not start with the palette-cycling disappearance.");
        }

        StepRoomEventFrames(1);
        int paletteBefore = makuEvent.PaletteHeader;
        StepRoomEventFrames(8);
        if (makuEvent.PaletteHeader == paletteBefore)
            throw new InvalidOperationException(
                "The $9a/$c4/$8f/$c5 Maku Tree palettes did not cycle within eight updates.");
        StepRoomEventFrames(202);
        if (!_dialogue.IsOpen || _dialogue.Position.Y != 96 || _dialogue.CurrentMessage != "Ahh...")
            throw new InvalidOperationException("TX_0540 did not open after 210 disappearance updates.");

        _dialogue.Close();
        StepRoomEventFrames(1);
        if (_sound.LastPlayRequest != OracleSoundEngine.SndMakuDisappear ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndMakuDisappear) != 2)
        {
            throw new InvalidOperationException(
                "TX_0540 did not replay SND_MAKUDISAPPEAR when its textbox closed.");
        }
        StepRoomEventFrames(211);
        if (!_dialogue.IsOpen || _dialogue.Position.Y != 96 ||
            !_dialogue.CurrentMessage.StartsWith("I feel so weird.\nI'm vanishing!"))
            throw new InvalidOperationException("TX_0541 did not follow the original 210-update pause.");

        _dialogue.Close();
        StepRoomEventFrames(1);
        if (_sound.LastPlayRequest != OracleSoundEngine.SndMakuDisappear ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndMakuDisappear) != 3)
        {
            throw new InvalidOperationException(
                "TX_0541 did not replay SND_MAKUDISAPPEAR when its textbox closed.");
        }
        StepRoomEventFrames(151);
        if (!makuEvent.HasState || makuEvent.Completed || IsTransitioning ||
            _saveData.MakuTreeState != 1 ||
            _saveData.HasGlobalFlag(OracleSaveData.GlobalFlagMakuTreeDisappeared))
        {
            throw new InvalidOperationException(
                "The Maku Tree script did not increment wMakuTreeState and defer its native handler by one update.");
        }
        StepRoomEventFrames(1);
        if (!makuEvent.Completed || _roomEvents.Active || !IsTransitioning ||
            _activeGroup != 0 || _currentRoom.Id != 0x38 ||
            !_saveData.HasGlobalFlag(OracleSaveData.GlobalFlagMakuTreeDisappeared) ||
            !_saveData.HasRoomFlag(0, 0x38, OracleSaveData.RoomFlagLayoutSwap) ||
            _saveData.MakuTreeState != 1 ||
            _sound.LastPlayRequest != OracleSoundEngine.SndFadeOut ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndFadeOut) != 1)
        {
            throw new InvalidOperationException(
                "The Maku Tree event did not persist GLOBALFLAG_0c, wMakuTreeState, room bit 0, " +
                "and initiate its hardcoded same-room warp after 150 updates.");
        }
        Color fadeStartGate = unswappedRoom.GetRenderedPixelForValidation(gatePixelPosition);
        if (_currentRoom.TilesetId != 0x22 || !makuTree.Active || _warpFade.Color.A != 0.0f ||
            !GateMatchesExpected(fadeStartGate))
        {
            throw new InvalidOperationException(
                $"The delayed $83 fade replaced room 0:38 or restored its corrupt base palette " +
                $"before beginning its transition to white (gate={fadeStartGate}).");
        }

        for (int frame = 0; frame < RoomTransitionController.DelayedWarpFadeFrames - 1; frame++)
            UpdateRoomWarpTransition(1.0 / 60.0);
        Color nearWhiteGate = unswappedRoom.GetRenderedPixelForValidation(gatePixelPosition);
        if (_currentRoom.TilesetId != 0x22 || _warpFade.Color.A <= 0.9f ||
            _warpFade.Color.A >= 1.0f || !GateMatchesExpected(nearWhiteGate))
        {
            throw new InvalidOperationException(
                $"The $83 delayed fade did not retain the old layout and cutscene palette " +
                $"for 124 updates (tileset={_currentRoom.TilesetId:x2}, " +
                $"alpha={_warpFade.Color.A}, gate={nearWhiteGate}).");
        }
        UpdateRoomWarpTransition(1.0 / 60.0);
        NpcCharacter? reloadedTree = _npcNodes.Find(npc =>
            npc.Record.Id == 0x87 && npc.Record.SubId == 0x00);
        Color retiredGate = unswappedRoom.GetRenderedPixelForValidation(gatePixelPosition);
        if (reloadedTree is null || reloadedTree.Active || GateMatchesExpected(retiredGate) ||
            !_rooms.IsLayoutSwapped(0, 0x38) || _currentRoom.TilesetId != 0x23 ||
            _currentRoom.GetMetatile(new Vector2(0x48, 0x28)) != 0xf9)
        {
            throw new InvalidOperationException(
                $"Room flag bit 0 did not load group 2's tree-less room 0:38 layout and suppress $87 " +
                $"(tree={reloadedTree is not null}/{reloadedTree?.Active}, " +
                $"swap={_rooms.IsLayoutSwapped(0, 0x38)}, retiredGate={retiredGate}, " +
                $"tileset={_currentRoom.TilesetId:x2}, " +
                $"tile24={_currentRoom.GetMetatile(new Vector2(0x48, 0x28)):x2}).");
        }

        for (int frame = 0; frame < WarpFadeFrames; frame++)
            UpdateRoomWarpTransition(1.0 / 60.0);
        if (IsTransitioning || _player.Position != new Vector2(0x58, 0x48))
        {
            throw new InvalidOperationException(
                $"The room $38/$45 hardcoded warp did not remain at $48/$58; got {_player.Position}.");
        }
        _player._PhysicsProcess(1.0 / 60.0);
        if (_activeGroup != 0 || _currentRoom.Id != 0x38 || IsTransitioning)
            throw new InvalidOperationException(
                "The $45 re-entry incorrectly walked Link into room 0:38's $ee/$ef warp to 5:cf.");

        LoadValidationRoom(0, 0x38);
        reloadedTree = _npcNodes.Find(npc => npc.Record.Id == 0x87 && npc.Record.SubId == 0x00);
        if (_roomEvents.Active || reloadedTree is null || reloadedTree.Active)
            throw new InvalidOperationException("The completed Maku Tree entry event retriggered on room reload.");

        CutsceneCommandTraceEntry[] starts = commandTrace.Entries
            .Where(entry => entry.Phase == CutsceneCommandTracePhase.Started)
            .ToArray();
        string[] expectedOpcodes =
        {
            "disablemenu", "setanimationcontinue", "setcollisionradii",
            "makeabuttonsensitive", "gate", "wait", "showtext", "wait",
            "playsound", "setanimationcontinue", "wait", "playsound",
            "writememory", "wait", "showtext", "playsound", "wait",
            "showtext", "playsound", "wait", "writememory", "native",
            "scriptend"
        };
        if (starts.Length != expectedOpcodes.Length ||
            starts.Where((entry, index) =>
                entry.Source.Script != "makuTree_subid01Script_body" ||
                entry.Source.Label != "makuTree_subid01Script_body" ||
                entry.Source.CommandIndex != index ||
                entry.Source.Opcode != expectedOpcodes[index] ||
                entry.Source.SourceLine <= 0 ||
                (index > 0 && entry.Source.SourceLine <= starts[index - 1].Source.SourceLine))
            .Any())
        {
            throw new InvalidOperationException(
                "The imported Maku Tree command trace lost source lines, command order, or typed opcodes.");
        }

        int CompletedUpdate(int commandIndex) => commandTrace.Entries.Single(entry =>
            entry.Source.CommandIndex == commandIndex &&
            entry.Phase == CutsceneCommandTracePhase.Completed).ScriptUpdate;
        if (starts[1].ScriptUpdate != starts[0].ScriptUpdate ||
            starts[2].ScriptUpdate != starts[0].ScriptUpdate ||
            starts[3].ScriptUpdate != CompletedUpdate(2) + 1 ||
            starts[4].ScriptUpdate != starts[3].ScriptUpdate ||
            starts[5].ScriptUpdate != CompletedUpdate(4) + 1 ||
            starts[6].ScriptUpdate != CompletedUpdate(5) ||
            starts[7].ScriptUpdate != CompletedUpdate(6) + 1 ||
            starts[8].ScriptUpdate != CompletedUpdate(7) ||
            starts[9].ScriptUpdate != CompletedUpdate(8) + 1 ||
            starts[10].ScriptUpdate != starts[9].ScriptUpdate ||
            starts[11].ScriptUpdate != CompletedUpdate(10) ||
            starts[12].ScriptUpdate != CompletedUpdate(11) + 1 ||
            starts[13].ScriptUpdate != starts[12].ScriptUpdate ||
            starts[14].ScriptUpdate != CompletedUpdate(13) ||
            starts[15].ScriptUpdate != CompletedUpdate(14) + 1 ||
            starts[16].ScriptUpdate != CompletedUpdate(15) + 1 ||
            starts[17].ScriptUpdate != CompletedUpdate(16) ||
            starts[18].ScriptUpdate != CompletedUpdate(17) + 1 ||
            starts[19].ScriptUpdate != CompletedUpdate(18) + 1 ||
            starts[20].ScriptUpdate != CompletedUpdate(19) ||
            starts[21].ScriptUpdate != starts[20].ScriptUpdate ||
            starts[22].ScriptUpdate != starts[20].ScriptUpdate)
        {
            throw new InvalidOperationException(
                "makuTree_subid01Script_body did not preserve carry-through, yield, wait, " +
                "dialogue, or final same-update command cadence.");
        }
        _roomEvents.CommandTraceSink = null;

        GD.Print("Validated room 0:38 Maku Tree $87:$01 simulated input, two-sheet unclipped face OAM, " +
            "initial six-palette PALH_8f gate/ground colors, fixed-bottom \\pos(2) dialogue, " +
            "imported typed script/source trace, exact command yields, 210/60/60/210/210/150 waits, four-header " +
            "palette cycle, STOPMUSIC/three SND_MAKUDISAPPEAR/SND_FADEOUT cue chain, " +
            "cutscene palette retained through the delayed 125-update white fade, " +
            "and one-shot $45 re-entry warp.");
    }

    private void StepRoomEventFrames(int frames)
    {
        for (int frame = 0; frame < frames; frame++)
        {
            _entities.Update(1.0 / 60.0, _player);
            _roomEvents.Update(1.0 / 60.0);
            _sound.Tick();
        }
    }

    private void ValidateMakuSproutRescueCutscene()
    {
        const int group = 1;
        const int roomId = 0x38;
        MakuSproutRescueEvent rescue = _roomEvents.MakuSproutRescue;
        MakuSproutRescueDatabase database = rescue.Database;
        MakuSproutRescueDatabase.EventRecord record = database.Record;
        int originalState = _saveData.MakuTreeState;
        int originalMapText = _saveData.MakuMapTextPast;
        bool originalSaved = _saveData.HasGlobalFlag(record.SavedFlag);
        bool originalAdvice = _saveData.HasGlobalFlag(record.AdviceFlag);
        bool originalRoom80 = _saveData.HasRoomFlag(group, roomId, (byte)record.RoomFlag);
        bool originalPresentSwap = _saveData.HasRoomFlag(
            0, 0x38, OracleSaveData.RoomFlagLayoutSwap);
        bool originalPastSwap = _saveData.HasRoomFlag(
            1, 0x48, OracleSaveData.RoomFlagLayoutSwap);

        _saveData.SetGlobalFlag(record.SavedFlag, false);
        _saveData.SetGlobalFlag(record.AdviceFlag, false);
        _saveData.SetRoomFlag(group, roomId, (byte)record.RoomFlag, false);
        _saveData.SetMakuTreeState(0);
        LoadValidationRoom(group, roomId);
        if (rescue.HasState || _roomEvents.Active)
            throw new InvalidOperationException(
                "Room 1:38 rescue ignored its wMakuTreeState $01/$02 predicate.");

        _saveData.SetMakuTreeState(1);
        var trace = new ValidationCutsceneTrace();
        _roomEvents.CommandTraceSink = trace;
        _sound.ClearPlayRequestAudit();
        LoadValidationRoom(group, roomId);
        if (!rescue.HasState || rescue.Stage != MakuSproutRescueEvent.EventStage.Running)
            throw new InvalidOperationException(
                "Room 1:38 did not start the unsaved Maku Sprout rescue at state $01.");

        MakuSproutRescueDatabase.ActorRecord sproutActor = database.Actors["Sprout"];
        MakuSproutRescueDatabase.ActorRecord leftActor = database.Actors["MoblinLeft"];
        MakuSproutRescueDatabase.ActorRecord rightActor = database.Actors["MoblinRight"];
        NpcCharacter? initialSprout = _entities.Entities<NpcCharacter>().SingleOrDefault(
            npc => npc.Record.Id == sproutActor.Id && npc.Record.SubId == sproutActor.SubId);
        NpcCharacter? initialLeft = _entities.Entities<NpcCharacter>().SingleOrDefault(
            npc => npc.Record.Id == leftActor.Id && npc.Record.SubId == leftActor.SubId);
        NpcCharacter? initialRight = _entities.Entities<NpcCharacter>().SingleOrDefault(
            npc => npc.Record.Id == rightActor.Id && npc.Record.SubId == rightActor.SubId);
        if (initialSprout is null || initialLeft is null || initialRight is null ||
            database.FearfulSproutAnimation == sproutActor.DownAnimation ||
            initialSprout.CurrentScriptAnimationSource != database.FearfulSproutAnimation ||
            initialLeft.CurrentScriptAnimationSource != leftActor.LeftAnimation ||
            initialRight.CurrentScriptAnimationSource != rightActor.RightAnimation ||
            !_player.CutsceneControlled || !rescue.ScreenTransitionsDisabled ||
            rescue.CutsceneState != 0 ||
            _entities.Entities<MaskedMoblinCharacter>().Count != 0)
        {
            throw new InvalidOperationException(
                "Room 1:38 exposed its ordinary sprout frame before the rescue's " +
                "state-0 sprout/controller/Moblin initialization completed.");
        }

        bool movedToTrigger = false;
        bool killedFirst = false;
        bool killedSecond = false;
        bool sawOneEnemyText = false;
        bool movedToEdge = false;
        bool sawUpFacingMakuDialogue = false;
        bool sawBottomExitDialogue = false;
        var textIds = new HashSet<int>();
        for (int frame = 0; frame < 5000 && rescue.HasState; frame++)
        {
            _entities.Update(1.0 / 60.0, _player);
            _roomEvents.Update(1.0 / 60.0);
            _sound.Tick();

            if (!movedToTrigger && rescue.CutsceneState == 5)
            {
                _player.WarpTo(new Vector2(record.ControllerX, record.ControllerY));
                movedToTrigger = true;
            }

            List<MaskedMoblinCharacter> moblins =
                _entities.Entities<MaskedMoblinCharacter>();
            if (!killedFirst && moblins.Count == 2)
            {
                MaskedMoblinCharacter moblin = moblins[0];
                _entities.ApplySwordHit(moblin.CollisionBounds.Grow(1), moblin.Position);
                killedFirst = true;
            }
            if (sawOneEnemyText && !killedSecond && moblins.Count == 1)
            {
                MaskedMoblinCharacter moblin = moblins[0];
                _entities.ApplySwordHit(moblin.CollisionBounds.Grow(1), moblin.Position);
                killedSecond = true;
            }

            if (_dialogue.IsOpen)
            {
                CutsceneCommandTraceEntry? textStart = trace.Entries.LastOrDefault(
                    entry => entry.Phase == CutsceneCommandTracePhase.Started &&
                        entry.Source.Opcode == "showtext");
                if (textStart.HasValue)
                {
                    int command = textStart.Value.Source.CommandIndex;
                    int textId = command switch
                    {
                        11 => 0x1202,
                        16 => 0x05d0,
                        27 => 0x1203,
                        36 => 0x05d1,
                        39 => 0x05d2,
                        54 => 0x05d3,
                        60 => 0x05d6,
                        68 => 0x05d4,
                        _ => 0
                    };
                    if (textId != 0)
                    {
                        textIds.Add(textId);
                        sawOneEnemyText |= textId == 0x05d1;
                        if (textId == 0x05d3)
                        {
                            sawUpFacingMakuDialogue =
                                _player.FacingVector == Vector2I.Up;
                        }
                        if (textId == 0x05d4)
                        {
                            sawBottomExitDialogue =
                                _dialogue.Position.Y == 96;
                        }
                    }
                }
                _dialogue.Close();
            }

            if (!movedToEdge && _saveData.HasGlobalFlag(record.SavedFlag))
            {
                _player.WarpTo(new Vector2(0x50, 0x7a));
                movedToEdge = true;
            }
        }

        int[] requiredTextIds =
        [
            0x1202, 0x05d0, 0x1203, 0x05d1, 0x05d2,
            0x05d3, 0x05d6, 0x05d4
        ];
        Vector2 GatePoint(int packed) => new(
            (packed & 0x0f) * OracleRoomData.MetatileSize + 8,
            (packed >> 4) * OracleRoomData.MetatileSize + 8);
        if (rescue.HasState || rescue.Stage != MakuSproutRescueEvent.EventStage.Completed ||
            !movedToTrigger || !killedFirst || !killedSecond || !movedToEdge ||
            !sawUpFacingMakuDialogue || !sawBottomExitDialogue ||
            requiredTextIds.Any(id => !textIds.Contains(id)) ||
            !_saveData.HasGlobalFlag(record.SavedFlag) ||
            !_saveData.HasGlobalFlag(record.AdviceFlag) ||
            _saveData.MakuTreeState != 2 || _saveData.MakuMapTextPast != record.MapTextLow ||
            !_saveData.HasRoomFlag(group, roomId, (byte)record.RoomFlag) ||
            _saveData.HasRoomFlag(0, 0x38, OracleSaveData.RoomFlagLayoutSwap) ||
            !_saveData.HasRoomFlag(1, 0x48, OracleSaveData.RoomFlagLayoutSwap) ||
            _currentRoom.GetMetatile(GatePoint(record.GateLeft)) != record.ClearTile ||
            _currentRoom.GetMetatile(GatePoint(record.GateInnerLeft)) != record.ClearTile ||
            _currentRoom.GetMetatile(GatePoint(record.GateInnerRight)) != record.ClearTile ||
            _currentRoom.GetMetatile(GatePoint(record.GateRight)) != record.ClearTile ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDing) != 4 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose) != 4 ||
            _roomCamera.Offset != Vector2.Zero)
        {
            throw new InvalidOperationException(
                "Room 1:38 did not complete its Moblin fight, dialogue, four gate phases, " +
                "up-facing Link handoff, lower exit textbox, music/flag/layout mutations, " +
                "or final screen-edge release.");
        }

        CheckRoomExit(_player);
        if (!IsTransitioning || _transitions.ScrollActive ||
            _activeGroup != group || _currentRoom.Id != roomId)
        {
            throw new InvalidOperationException(
                "Room 1:38's unlocked bottom edge did not start its $03 exit warp.");
        }
        UpdateRoomWarpTransition(WarpLeaveFrames / 60.0);
        if (_activeGroup != group || _currentRoom.Id != 0x48)
            throw new InvalidOperationException(
                "Room 1:38's bottom exit did not load past room 1:48.");
        UpdateRoomWarpTransition(WarpFadeFrames / 60.0);
        _entities.Update(1.0 / 60.0, _player);
        TimePortal? returnPortal = _entities.Entities<TimePortal>().SingleOrDefault();
        if (IsTransitioning || _activeGroup != group || _currentRoom.Id != 0x48 ||
            returnPortal is null || returnPortal.Record.SubId != 0x02 ||
            returnPortal.Position != new Vector2(0x58, 0x48) ||
            !returnPortal.Active ||
            _currentRoom.GetMetatile(returnPortal.Position) != 0xd7 ||
            _saveData.HasTreasure(TreasureDatabase.TreasureSeedSatchel))
        {
            throw new InvalidOperationException(
                "The post-rescue layout swap did not expose and activate room 1:48's " +
                "$e1:$02 portal for a Seed-Satchel-less Link.");
        }

        string[] scripts = trace.Entries
            .Where(entry => entry.Phase == CutsceneCommandTracePhase.Started)
            .Select(entry => entry.Source.Script)
            .Distinct()
            .ToArray();
        string[] requiredScripts =
        [
            "makuSprout_subid01Script", "interaction6b_subid04Script",
            "moblin_subid00Script", "moblin_subid01Script"
        ];
        if (requiredScripts.Any(script => !scripts.Contains(script)) ||
            trace.Entries.Any(entry => entry.Source.SourceLine <= 0))
        {
            throw new InvalidOperationException(
                "Room 1:38 importer-generated command trace lost source ownership.");
        }

        LoadValidationRoom(group, roomId);
        NpcCharacter? savedSprout = _npcNodes.Find(npc =>
            npc.Record.Id == record.SproutId && npc.Record.SubId == record.SproutSubId);
        if (rescue.HasState || _roomEvents.Active || savedSprout is null ||
            savedSprout.TextId != record.PostTextId ||
            savedSprout.Message != record.PostText ||
            _currentRoom.GetMetatile(GatePoint(record.GateLeft)) != record.ClearTile)
        {
            throw new InvalidOperationException(
                "Room 1:38 saved re-entry retriggered or lost TX_05d5/the cleared gate.");
        }

        var enemyDatabase = new EnemyDatabase();
        EnemyDatabase.MaskedMoblinRecord masked = enemyDatabase.MaskedMoblin;
        EnemyDatabase.EnemyArrowRecord arrowRecord = enemyDatabase.EnemyArrow;
        if (masked is not
            { Id: 0x20, SubId: 0, CollisionRadiusY: 6, CollisionRadiusX: 6,
              DamageQuarters: 2, Health: 2, SpeedRaw: 0x14,
              MoveCounterBase: 0x30, MoveCounterMask: 0x3f, TurnWait: 8 } ||
            arrowRecord is not
            { DamageQuarters: 2, SpeedRaw: 0x50 })
        {
            throw new InvalidOperationException(
                "The dynamically-created masked Moblin/enemy-arrow records diverged from source.");
        }
        var deflectedArrow = new EnemyArrowProjectile();
        deflectedArrow.Initialize(
            arrowRecord, _currentRoom, new Vector2(0x50, 0x40), 0x08);
        if (!deflectedArrow.DeflectWithSword() ||
            deflectedArrow.State != EnemyArrowProjectile.ArrowState.Bouncing ||
            deflectedArrow.Counter != 0x20)
        {
            throw new InvalidOperationException(
                "PART_ENEMY_ARROW did not enter its shared 32-update sword-bounce state.");
        }
        for (int frame = 0; frame < 31; frame++)
            deflectedArrow.UpdateFrame(_player);
        if (deflectedArrow.Finished || deflectedArrow.Counter != 1 ||
            deflectedArrow.ZFixed == 0)
        {
            throw new InvalidOperationException(
                "PART_ENEMY_ARROW ended before its $20 SPEED_40 -$00e0/$0e bounce completed.");
        }
        deflectedArrow.UpdateFrame(_player);
        if (!deflectedArrow.Finished)
            throw new InvalidOperationException(
                "PART_ENEMY_ARROW did not delete on bounce counter zero.");
        deflectedArrow.Free();

        _saveData.SetMakuTreeState(originalState);
        _saveData.SetMakuMapTextPast(originalMapText);
        _saveData.SetGlobalFlag(record.SavedFlag, originalSaved);
        _saveData.SetGlobalFlag(record.AdviceFlag, originalAdvice);
        _saveData.SetRoomFlag(group, roomId, (byte)record.RoomFlag, originalRoom80);
        _saveData.SetRoomFlag(
            0, 0x38, OracleSaveData.RoomFlagLayoutSwap, originalPresentSwap);
        _saveData.SetRoomFlag(
            1, 0x48, OracleSaveData.RoomFlagLayoutSwap, originalPastSwap);
        _roomEvents.CommandTraceSink = null;

        GD.Print("Validated room 1:38 Maku Sprout rescue: exact state/flag predicate, " +
            "pre-display state-0 actor initialization, four typed script owners, " +
            "synchronized jumping Moblins, dynamic masked-Moblin " +
            "combat and one-enemy branch, Link approach/reposition, four interleaved gate " +
            "bursts with shake/sounds, advice/saved/map-text/layout persistence, room music " +
            "restore, final DIR_UP waypoint, lower TX_05d4, screen-edge transition lock, " +
            "bottom exit to the active 1:48 $e1:$02 portal, TX_05d5, and completed re-entry.");
    }

    private void ValidateMakuTreeSavedCutscene()
    {
        const int group = 0;
        const int room = 0x38;
        MakuTreeSavedEvent savedEvent = _roomEvents.MakuTreeSaved;
        MakuTreeSavedDatabase database = savedEvent.Database;
        MakuTreeSavedDatabase.SavedEventRecord record = database.Record;
        int originalState = _saveData.MakuTreeState;
        int originalMapText = _saveData.MakuMapTextPresent;
        int originalSatchelX = _saveData.MakuTreeSeedSatchelXPosition;
        byte originalRoomFlags = _saveData.GetRoomFlags(group, room);
        bool originalAdvice = _saveData.HasGlobalFlag(record.AdviceFlag);
        var inventorySnapshot = new byte[0x36];
        _saveData.ReadWramBytes(0xc688, inventorySnapshot);

        _saveData.SetMakuTreeState(1);
        _saveData.SetRoomFlag(group, room, OracleSaveData.RoomFlagLayoutSwap, false);
        _saveData.SetRoomFlag(group, room, OracleSaveData.RoomFlagItem, false);
        _saveData.SetRoomFlag(group, room, OracleSaveData.RoomFlag80, false);
        _saveData.SetGlobalFlag(record.AdviceFlag, false);
        _saveData.SetMakuMapTextPresent(0);
        LoadValidationRoom(group, room);
        if (savedEvent.HasState)
        {
            throw new InvalidOperationException(
                "Room 0:38 adult Maku Tree event ignored its wMakuTreeState=$02 predicate.");
        }

        _saveData.SetMakuTreeState(2);
        var trace = new ValidationCutsceneTrace();
        _roomEvents.CommandTraceSink = trace;
        _sound.ClearPlayRequestAudit();
        LoadValidationRoom(group, room);
        _player.WarpTo(new Vector2(0x50, 0x4d));
        _player.Face(Vector2I.Up);
        NpcCharacter tree = _entities.Entities<NpcCharacter>().Single(npc =>
            npc.Record.Id == record.InteractionId && npc.Record.SubId == record.SubId);
        if (!savedEvent.HasState || savedEvent.BlocksGameplay || !tree.Active ||
            tree.Position != new Vector2(0x50, 0x40) ||
            tree.CurrentAnimationTextureSize.X <= 32 ||
            tree.CurrentAnimationTextureSize.Y <= 32)
        {
            throw new InvalidOperationException(
                "Room 0:38 did not initialize the state-$02 adult tree with its full OAM.");
        }

        StepRoomEventFrames(3);
        if (!savedEvent.ButtonSensitive || savedEvent.CurrentCommandIndex != 6 ||
            savedEvent.BlocksGameplay || _sound.ActiveMusic != record.Music)
        {
            throw new InvalidOperationException(
                "makuTree_subid02Script_body did not reach its initial A-button wait with Maku music.");
        }
        if (!_interactions.TryInteract(_player))
            throw new InvalidOperationException(
                "The normal A-button target path could not reach the adult Maku Tree.");
        // Exercise the helper's lower Link-X band after the real centered
        // interaction point has routed A to pressedAButton.
        _player.WarpTo(new Vector2(0x45, 0x4d));

        var textIds = new List<int>();
        int handledTextStarts = 0;
        int choiceCount = 0;
        for (int frame = 0; frame < 5000; frame++)
        {
            StepRoomEventFrames(1);
            CutsceneCommandTraceEntry[] textStarts = trace.Entries.Where(entry =>
                entry.Phase == CutsceneCommandTracePhase.Started &&
                entry.Source.Opcode == "showtext").ToArray();
            if (_dialogue.IsOpen && textStarts.Length > handledTextStarts)
            {
                CutsceneCommandTraceEntry started = textStarts[handledTextStarts++];
                if (database.Commands[started.Source.CommandIndex]
                    is not CutsceneShowTextCommand text)
                {
                    throw new InvalidOperationException(
                        "Saved Maku Tree showtext trace did not resolve to typed text metadata.");
                }
                textIds.Add(text.TextId);
                if (_dialogue.Position.Y != 96)
                {
                    throw new InvalidOperationException(
                        $"Saved Maku Tree TX_{text.TextId:x4} ignored its \\pos(2) textbox.");
                }
                if (text.TextId == 0x054a)
                {
                    _dialogue.SubmitChoiceForValidation(choiceCount++ == 0 ? 0 : 1);
                }
                else
                {
                    _dialogue.Close();
                }
            }

            GroundTreasurePickup? falling =
                _entities.Entities<GroundTreasurePickup>().SingleOrDefault();
            if (savedEvent.CurrentCommandIndex == 60 &&
                !savedEvent.BlocksGameplay && falling?.State ==
                    GroundTreasurePickup.PickupState.Waiting)
            {
                break;
            }
        }

        int[] expectedTexts =
        [
            0x0542, 0x0543, 0x0544, 0x0545, 0x0546, 0x0547,
            0x0548, 0x0549, 0x054a,
            0x0548, 0x0549, 0x054a,
            0x054b, 0x054c, 0x054d, 0x054e, 0x054f, 0x0550, 0x0561
        ];
        GroundTreasurePickup dropped =
            _entities.Entities<GroundTreasurePickup>().Single();
        if (!textIds.SequenceEqual(expectedTexts) || choiceCount != 2 ||
            savedEvent.CurrentCommandIndex != 60 || savedEvent.BlocksGameplay ||
            _player.CutsceneControlled ||
            !_saveData.HasGlobalFlag(record.AdviceFlag) ||
            _saveData.MakuMapTextPresent != record.MapTextLow ||
            !_saveData.HasRoomFlag(group, room, OracleSaveData.RoomFlag80) ||
            _saveData.HasRoomFlag(group, room, OracleSaveData.RoomFlagItem) ||
            _saveData.MakuTreeSeedSatchelXPosition != record.LowerBandX ||
            dropped.Record.TreasureObject != record.FallingTreasureObject ||
            dropped.Position != new Vector2(record.LowerBandX, record.DropY) ||
            dropped.Record.SpawnMode != 2 || dropped.Record.GrabMode != 1 ||
            dropped.State != GroundTreasurePickup.PickupState.Waiting ||
            dropped.ZFixed != 0 ||
            _sound.PlayRequestsFor(record.SpawnSound) != 1 ||
            _sound.PlayRequestsFor(record.LandingSound) != record.BounceCount)
        {
            throw new InvalidOperationException(
                "Room 0:38 did not preserve the explanation repeat, advice state, " +
                "dynamic Satchel X selection, 40-update fall, or two landing cues.");
        }

        _player.WarpTo(new Vector2(0x50, 0x4d));
        _player.Face(Vector2I.Up);
        if (!_interactions.TryInteract(_player))
            throw new InvalidOperationException(
                "The normal A-button target path could not reach the adult Maku Tree NPC loop.");
        StepRoomEventFrames(1);
        if (!_dialogue.IsOpen || _dialogue.Position.Y != 96 ||
            database.Commands[savedEvent.CurrentCommandIndex - 1]
                is not CutsceneShowTextCommand { TextId: 0x054f })
        {
            throw new InvalidOperationException(
                "The completed adult Maku Tree NPC loop did not repeat bottom TX_054f.");
        }
        _dialogue.Close();
        StepRoomEventFrames(31);

        // Leave before collecting. Room bit $80 and wMakuTreeSeedSatchelXPosition
        // must recreate var03=$03 instantly at $58 on the next entry.
        LoadValidationRoom(0, 0x39);
        LoadValidationRoom(group, room);
        StepRoomEventFrames(1);
        GroundTreasurePickup respawned =
            _entities.Entities<GroundTreasurePickup>().Single();
        if (respawned.Record.TreasureObject != record.RespawnTreasureObject ||
            respawned.Position != new Vector2(record.LowerBandX, record.RespawnY) ||
            respawned.Record.SpawnMode != 0 || respawned.Record.GrabMode != 1)
        {
            throw new InvalidOperationException(
                "The uncollected Seed Satchel did not respawn from persisted room bit $80/X data.");
        }
        StepRoomEventFrames(2);
        if (savedEvent.CurrentCommandIndex != 60 ||
            respawned.State != GroundTreasurePickup.PickupState.Waiting ||
            _dialogue.IsOpen)
        {
            throw new InvalidOperationException(
                "Satchel re-entry retriggered the full adult-tree conversation or delayed var03=$03.");
        }

        int emberSeedsBefore = _inventory.EmberSeeds;
        _sound.ClearPlayRequestAudit();
        _player.WarpTo(respawned.Position);
        _entities.Update(1.0 / 60.0, _player);
        if (_interactions.GroundTreasureForValidation != respawned ||
            !_saveData.HasRoomFlag(group, room, OracleSaveData.RoomFlagItem) ||
            _inventory.SeedSatchelLevel != 1 ||
            _inventory.EmberSeeds != Math.Max(emberSeedsBefore, 0x20) ||
            !_dialogue.IsOpen ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndGetItem) != 1)
        {
            throw new InvalidOperationException(
                "Touching the respawned Satchel did not grant its level, 20 Ember Seeds, " +
                $"$20, text, and sound (tracked={ReferenceEquals(_interactions.GroundTreasureForValidation, respawned)}, " +
                $"flag={_saveData.HasRoomFlag(group, room, OracleSaveData.RoomFlagItem)}, " +
                $"level={_inventory.SeedSatchelLevel}, seeds={_inventory.EmberSeeds}/" +
                $"{emberSeedsBefore}, dialogue={_dialogue.IsOpen}, " +
                $"sounds={_sound.PlayRequestsFor(OracleSoundEngine.SndGetItem)})." );
        }
        _interactions.Update(1.0 / 60.0, _player);
        _entities.Update(1.0 / 60.0, _player);
        if (!respawned.Held || !_player.IsHoldingItemOneHand ||
            respawned.Position != _player.Position + new Vector2(-4, -14) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndGetItem) != 2)
        {
            throw new InvalidOperationException(
                "Seed Satchel collection did not use grab mode $01 and its -4/-14 offset.");
        }
        _dialogue.Close();
        _interactions.Update(1.0 / 60.0, _player);
        _entities.Update(1.0 / 60.0, _player);
        if (_player.IsHoldingItemOneHand ||
            _entities.Entities<GroundTreasurePickup>().Count != 0)
        {
            throw new InvalidOperationException(
                "Closing the Seed Satchel text did not release Link and delete INTERAC_TREASURE.");
        }
        LoadValidationRoom(group, room);
        StepRoomEventFrames(3);
        if (_entities.Entities<GroundTreasurePickup>().Count != 0 ||
            savedEvent.CurrentCommandIndex != 60 || _dialogue.IsOpen)
        {
            throw new InvalidOperationException(
                "Collected room-$20 Satchel respawned or retriggered the first conversation.");
        }

        CutsceneCommandTraceEntry[] commandStarts = trace.Entries.Where(entry =>
            entry.Phase == CutsceneCommandTracePhase.Started &&
            entry.Source.Script == "makuTree_subid02Script_body").ToArray();
        if (commandStarts.Any(entry => entry.Source.SourceLine <= 0) ||
            !commandStarts.Any(entry => entry.Source.Opcode == "jumpifroomflagset") ||
            !commandStarts.Any(entry => entry.Source.Opcode == "jumpiftextoptioneq") ||
            !commandStarts.Any(entry => entry.Source.Opcode == "checkabutton") ||
            !commandStarts.Any(entry => entry.Source.Opcode == "setmusic"))
        {
            throw new InvalidOperationException(
                "Saved Maku Tree typed trace lost its source lines or new branch/input/music opcodes.");
        }
        LoadValidationRoom(0, 0x39);
        _saveData.WriteWramBytes(0xc688, inventorySnapshot);
        _saveData.CommitInventoryChange();
        System.Reflection.MethodInfo? reloadInventory = typeof(InventoryState).GetMethod(
            "LoadFromSaveData",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);
        if (reloadInventory is null)
            throw new InvalidOperationException("Could not restore validation inventory state.");
        reloadInventory.Invoke(_inventory, null);
        _saveData.SetMakuTreeState(originalState);
        _saveData.SetMakuMapTextPresent(originalMapText);
        _saveData.SetMakuTreeSeedSatchelXPosition(originalSatchelX);
        _saveData.SetGlobalFlag(record.AdviceFlag, originalAdvice);
        _saveData.SetRoomFlag(
            group, room, OracleSaveData.RoomFlagLayoutSwap,
            (originalRoomFlags & OracleSaveData.RoomFlagLayoutSwap) != 0);
        _saveData.SetRoomFlag(
            group, room, OracleSaveData.RoomFlagItem,
            (originalRoomFlags & OracleSaveData.RoomFlagItem) != 0);
        _saveData.SetRoomFlag(
            group, room, OracleSaveData.RoomFlag80,
            (originalRoomFlags & OracleSaveData.RoomFlag80) != 0);
        _roomEvents.CommandTraceSink = null;

        GD.Print("Validated room 0:38 Maku Tree $87:$02: exact state predicate, " +
            "68-command typed NPC loop, fixed-bottom TX_0542-$0550/$0561 sequence, " +
            "Yes repeat/No continuation, five face animations, present-map advice state, " +
            "Link-relative Satchel X, persistent $80/X respawn, 40-update falling bounce, " +
            "SND_SOLVEPUZZLE/two SND_DROPESSENCE cues, room-$20 suppression, and one-hand collection.");
    }

    private void ValidateNayruIntroCutscene()
    {
        NayruIntroEvent nayruIntro = _roomEvents.Nayru;
        var nayruTrace = new ValidationCutsceneTrace();
        _roomEvents.CommandTraceSink = nayruTrace;
        const int group = 0;
        const int roomId = 0x39;
        Vector2 portalPoint = new(0x28, 0x28);
        OracleRoomData sourceRoom = _rooms.World.LoadRoom(group, roomId);
        byte sourcePortalTile = sourceRoom.GetMetatile(portalPoint);
        if (sourcePortalTile != 0x3a)
            sourceRoom.ReplaceMetatile(portalPoint, sourcePortalTile, 0x3a, (long)_animationTicks);
        _saveData.SetGlobalFlag(OracleSaveData.GlobalFlagPregameIntroDone);
        _saveData.SetGlobalFlag(OracleSaveData.GlobalFlagIntroDone, value: false);
        _saveData.SetRoomFlag(group, roomId, OracleSaveData.RoomFlag40, value: false);
        _saveData.SetRoomFlag(group, roomId, OracleSaveData.RoomFlag80, value: false);
        _sound.PlayMusicIfChanged(OracleSoundEngine.MusFairyFountain);
        _sound.SetMusicVolume(3);
        LoadValidationRoom(group, 0x59);
        _transitions.BeginScroll(_player, Vector2I.Up, 0x49);
        if (_sound.ActiveMusic != OracleSoundEngine.MusFairyFountain ||
            _sound.MusicVolume != 3)
        {
            throw new InvalidOperationException(
                "INTERAC_PLAY_NAYRU_MUSIC $2f ran before the 0:59 -> 0:49 scroll completed.");
        }
        FinishActiveScrollingTransitionForValidation();
        if (_sound.ActiveMusic != OracleSoundEngine.MusNayru ||
            _sound.MusicVolume != 2)
        {
            throw new InvalidOperationException(
                "Room 0:49 did not start MUS_NAYRU with volume 2 after its incoming scroll.");
        }
        _transitions.BeginScroll(_player, Vector2I.Up, roomId);
        if (_sound.ActiveMusic != OracleSoundEngine.MusNayru ||
            _sound.MusicVolume != 2)
        {
            throw new InvalidOperationException(
                "The 0:49 -> 0:39 scroll changed Nayru's volume before her interaction resumed.");
        }
        FinishActiveScrollingTransitionForValidation();
        if (_sound.ActiveMusic != OracleSoundEngine.MusNayru ||
            _sound.MusicVolume != 2)
        {
            throw new InvalidOperationException(
                "Nayru's destination interaction changed music during the 0:39 scroll.");
        }

        if (_inventory.HasTreasure(TreasureDatabase.TreasureSword) ||
            _inventory.SwordLevel != 0 || _inventoryMenu.CanOpenForValidation ||
            _mapMenu.CanOpenNormalForValidation)
        {
            throw new InvalidOperationException(
                "The pre-intro save retained the development sword or allowed Start/Select " +
                "before GLOBALFLAG_INTRO_DONE $0a.");
        }

        int errorRequests = _sound.PlayRequestsFor(OracleSoundEngine.SndError);
        int openMenuRequests = _sound.PlayRequestsFor(OracleSoundEngine.SndOpenMenu);
        foreach (string[] actions in new[]
        {
            new[] { "inventory" },
            new[] { "map" },
            new[] { "inventory", "map" }
        })
        {
            foreach (string action in actions)
                Input.ActionPress(action);
            _inventoryMenu.Update(1.0 / 60.0);
            foreach (string action in actions)
                Input.ActionRelease(action);
        }
        if (_sound.LastPlayRequest != OracleSoundEngine.SndError ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndError) != errorRequests + 3 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndOpenMenu) != openMenuRequests ||
            _inventoryMenu.IsActive || _mapMenu.IsActive ||
            _inventoryScreen.Visible || _mapScreen.Visible)
        {
            throw new InvalidOperationException(
                "Pre-GLOBALFLAG_INTRO_DONE $0a Start, Select, and Start+Select did not " +
                "each reject the blocked menu request with exactly one SND_ERROR $5a.");
        }

        NayruActorRegistry actors = nayruIntro.ActorRegistry;
        (string Name, Vector2 Position)[] expectedActors =
        {
            ("Nayru", new Vector2(0x78, 0x18)),
            ("Ralph", new Vector2(0x88, 0x30)),
            ("Bear", new Vector2(0x58, 0x38)),
            ("Monkey", new Vector2(0x78, 0x50)),
            ("Rabbit", new Vector2(0x88, 0x50)),
            ("Boy", new Vector2(0x68, 0x48)),
            ("Bird", new Vector2(0x48, 0x2c))
        };
        if (actors.Count != expectedActors.Length || nayruIntro.CurrentStage != 1 ||
            _roomEvents.Active || _player.CutsceneControlled ||
            _currentRoom.GetMetatile(portalPoint) != 0x3a)
        {
            throw new InvalidOperationException(
                "Room 0:39 did not create the pre-intro $6b:$01 audience while retaining Link control.");
        }
        var nayruDatabase = new NayruIntroEventDatabase();
        NayruIntroEventDatabase.EventRecord nayruEvent = nayruDatabase.Event;
        if (nayruEvent.NpcJumpSpeedZ != -0x200 || nayruEvent.NpcJumpGravity != 0x30 ||
            nayruEvent.DarkFadeFrames != 0x20 || nayruEvent.WhiteFadeOutFrames != 0x20 ||
            nayruEvent.PossessionFadeHoldFrames != 0x3c ||
            nayruEvent.WhiteFadeInFrames != 97 || nayruEvent.NayruAscentSpeedZ != -0x400 ||
            nayruEvent.NayruTransferZ != -0x8000 || nayruEvent.NayruLandingDelay != 0x1e ||
            nayruEvent.NayruFallSpeedZ != 0x40 || nayruEvent.NayruFallGravity != 0x20 ||
            nayruDatabase.DarkBackgroundPalettes.GetLength(0) != 6 ||
            nayruDatabase.Actor("RalphSword").Id != 0x5e ||
            nayruDatabase.Flee("Monkey").WaitJumpSpeedZ != -0x120 ||
            nayruDatabase.Flee("Rabbit").EscapeJumpSpeedZ != -0x200 ||
            !nayruDatabase.Flee("Boy").WaitForLanding ||
            nayruDatabase.Flee("Bird").EscapeGravity != 0 ||
            nayruDatabase.Effect("MusicNote").VelocityXFixed != 53 ||
            nayruDatabase.Effect("MusicNote").VelocityYFixed != -79 ||
            nayruDatabase.Vignette(0) != new NayruIntroEventDatabase.VignetteRecord(0, 0, 0x98, 937) ||
            nayruDatabase.Vignette(1) != new NayruIntroEventDatabase.VignetteRecord(1, 0, 0x5a, 600) ||
            nayruDatabase.Vignette(2) != new NayruIntroEventDatabase.VignetteRecord(2, 2, 0x0e, 645) ||
            nayruDatabase.VignetteMonkeys.Count != 10 ||
            nayruDatabase.VignetteMonkeys[8] !=
                new NayruIntroEventDatabase.VignetteMonkeyRecord(8, 0x50, 0x46, 180, 2) ||
            nayruDatabase.Actor("VignetteGuy").InitialAnimation != 3 ||
            nayruDatabase.Actor("VignetteGirl").InitialAnimation != 1 ||
            nayruDatabase.Actor("VignetteBoy").InitialAnimation != 1 ||
            nayruDatabase.Actor("Exclamation").Id != 0x9f ||
            nayruDatabase.PossessedSpritePalette.Length != 4 ||
            nayruDatabase.StoneSpritePalette.Length != 4 ||
            !nayruDatabase.StoneSpritePalette[2].IsEqualApprox(
                new Color(17.0f / 31.0f, 17.0f / 31.0f, 25.0f / 31.0f, 1.0f)) ||
            !nayruDatabase.PossessedSpritePalette[2].IsEqualApprox(
                new Color(3.0f / 31.0f, 13.0f / 31.0f, 27.0f / 31.0f, 1.0f)))
        {
            throw new InvalidOperationException(
                "The imported Ralph jump, PALH $99, audience escape, linked sword, white flash, " +
                "or Nayru portal-flight records changed.");
        }
        foreach ((string name, Vector2 position) in expectedActors)
        {
            NayruIntroEventDatabase.ActorRecord record = nayruDatabase.Actor(name);
            if (!actors.TryGetValue(name, out NpcCharacter? actor) || !actor.Active ||
                actors.NameOf(actor) != name ||
                actor.Position != position || actor.CurrentAnimationOpaquePixels == 0 ||
                actors.AnimationSource(name, record.InitialAnimation) !=
                    record.Animation(record.InitialAnimation) ||
                actor.CurrentScriptAnimationSource != record.Animation(record.InitialAnimation))
            {
                throw new InvalidOperationException(
                    $"objectData.nayruAndAnimalsInIntro actor {name} was missing, blank, " +
                    $"at {actor?.Position} instead of {position}, or not using initial " +
                    $"animation ${record.InitialAnimation:x2}.");
            }
        }
        StepRoomEventFrames(1);
        if (_sound.ActiveMusic != OracleSoundEngine.MusNayru ||
            _sound.MusicVolume != 3)
        {
            throw new InvalidOperationException(
                "INTERAC_NAYRU $36:$00 did not restore MUS_NAYRU to volume 3 on its first update.");
        }
        if (actors["Nayru"].SourceGraphicsWidth != 256)
        {
            throw new InvalidOperationException(
                "Nayru's short first sheet did not retain its full 128-pixel VRAM slot before spr_nayru_2.");
        }
        foreach ((string name, Vector2 position) in expectedActors)
        {
            if (!_entities.BlocksLink(position))
                throw new InvalidOperationException(
                    $"Dynamically generated Nayru gathering actor {name} has no Link collision.");
        }
        // The second note is created on phase 45 after effect movement runs. Give it
        // 18 movement updates so its rightward SPEED_60 path always exceeds the
        // global-frame sway, regardless of the frame phase established by earlier
        // validation scenarios. The first note remains inside its 70-update life.
        StepRoomEventFrames(63);
        List<NpcCharacter> singingNotes = _entities.Entities<NpcCharacter>()
            .Where(actor => actor.Name.ToString().StartsWith(
                "NayruIntroEffect_MusicNote", StringComparison.Ordinal))
            .ToList();
        NpcCharacter leftNote = singingNotes.SingleOrDefault(note =>
            note.Name.ToString().EndsWith("MusicNote0", StringComparison.Ordinal))!;
        NpcCharacter rightNote = singingNotes.SingleOrDefault(note =>
            note.Name.ToString().EndsWith("MusicNote1", StringComparison.Ordinal))!;
        if (nayruDatabase.Effect("MusicNote").SpriteName != "spr_common_sprites" ||
            nayruDatabase.Effect("MusicNote").TileBase != 0x44 ||
            !nayruTrace.Saw("NoteSpawn", value: 2) || singingNotes.Count != 2 ||
            singingNotes.Any(note => note.Record.SpriteName != "spr_common_sprites" ||
                note.Record.TileBase != 0x44) ||
            singingNotes.Any(note => !note.Active || note.CurrentAnimationOpaquePixels == 0) ||
            leftNote is null || rightNote is null ||
            leftNote.Position.X >= 0x78 - 6 || leftNote.Position.Y >= 0x18 - 4 ||
            rightNote.Position.X <= 0x78 + 8 || rightNote.Position.Y >= 0x18 - 4 ||
            !nayruTrace.Saw("NoteMotion", value: 0x01) ||
            !nayruTrace.Saw("NoteMotion", value: 0x02))
        {
            throw new InvalidOperationException(
                "Nayru's animation $04 did not create both visible music notes from " +
                "fixed bank-1 VRAM tile $44 in spr_common_sprites, with the original opposing " +
                "SPEED_60 paths and global-frame sway distinct from the snore Z at $40. " +
                $"count={singingNotes.Count}, names/positions=" +
                string.Join(", ", singingNotes.Select(note =>
                    $"{note.Name}:{note.Position}:tile{note.Record.TileBase}:" +
                    $"active={note.Active}:pixels={note.CurrentAnimationOpaquePixels}")) +
                $", motion={nayruTrace.OrValues("NoteMotion"):x2}.");
        }

        NpcCharacter bird = actors["Bird"];
        _player.WarpTo(bird.Position + Vector2.Left * 16, recordSafe: false);
        _player.Face(Vector2I.Right);
        if (!TryInteract(_player) || !_dialogue.IsOpen ||
            _dialogue.CurrentMessage != "No! I have to\nhear Nayru's\nsong!" ||
            nayruTrace.LastValue("AudienceMask") != 0x01 ||
            bird.CurrentScriptAnimationSource != nayruDatabase.Actor("Bird").Animation(2))
        {
            throw new InvalidOperationException(
                "The intro bird did not route TX_3214 through normal NPC interaction or select " +
                "its cplinkx+$02 left-facing talk animation.");
        }
        StepRoomEventFrames(1);
        if (bird.ScriptDrawOffset.Y >= 0)
            throw new InvalidOperationException(
                "The intro bird did not begin its repeating -$00c0/$0020 talk hop.");
        _dialogue.Close();
        StepRoomEventFrames(1);
        if (bird.ScriptDrawOffset != Vector2.Zero ||
            bird.CurrentScriptAnimationSource != nayruDatabase.Actor("Bird").Animation(2))
        {
            throw new InvalidOperationException(
                "The intro bird did not stop hopping and hold its talk pose for the post-text wait.");
        }
        StepRoomEventFrames(10);
        if (bird.CurrentScriptAnimationSource != nayruDatabase.Actor("Bird").Animation(1))
            throw new InvalidOperationException(
                "The intro bird did not restore animation $01 after its 10-update post-text wait.");

        NpcCharacter rabbit = actors["Rabbit"];
        _player.WarpTo(rabbit.Position + Vector2.Right * 16, recordSafe: false);
        if (!nayruIntro.TryInteractNpc(rabbit) ||
            !_dialogue.CurrentMessage.StartsWith("♪La la li li la♪", StringComparison.Ordinal) ||
            nayruTrace.LastValue("AudienceMask") != 0x03 ||
            rabbit.CurrentScriptAnimationSource != nayruDatabase.Actor("Rabbit").Animation(1))
        {
            throw new InvalidOperationException(
                "The rabbit did not face Link through turnToFaceLink, set audience bit $02, " +
                "or decode TX_5705's music symbols.");
        }
        _dialogue.Close();
        StepRoomEventFrames(11);

        NpcCharacter boy = actors["Boy"];
        _player.WarpTo(boy.Position + Vector2.Down * 16, recordSafe: false);
        if (!nayruIntro.TryInteractNpc(boy) ||
            boy.CurrentScriptAnimationSource != nayruDatabase.Actor("Boy").Animation(2))
        {
            throw new InvalidOperationException(
                "The intro boy did not use turnToFaceLink's down-facing animation.");
        }
        _dialogue.Close();
        StepRoomEventFrames(11);

        NpcCharacter monkey = actors["Monkey"];
        _player.WarpTo(monkey.Position + Vector2.Right * 16, recordSafe: false);
        if (!nayruIntro.TryInteractNpc(monkey) ||
            monkey.CurrentScriptAnimationSource != nayruDatabase.Actor("Monkey").Animation(1))
        {
            throw new InvalidOperationException(
                "The intro monkey did not use cplinkx's right-facing animation.");
        }
        _dialogue.Close();
        StepRoomEventFrames(21);
        if (nayruTrace.LastValue("AudienceMask") != 0x0f ||
            !nayruIntro.TryInteractNpc(actors["Bear"]) ||
            nayruTrace.LastValue("AudienceMask") != 0x1f ||
            nayruIntro.CurrentStage != 2 || !_roomEvents.Active ||
            !_player.CutsceneControlled)
        {
            throw new InvalidOperationException(
                "The $01/$02/$04/$08 audience bits did not unlock the bear's $10 lead-in.");
        }

        StepRoomEventFrames(20 + 16);
        if (actors["Bear"].Position != new Vector2(0x58, 0x30) ||
            actors["Bear"].CurrentScriptAnimationSource !=
                nayruDatabase.Actor("Bear").Animation(1))
        {
            throw new InvalidOperationException(
                "The bear's raw angle-$00 movement did not preserve its explicit right-facing animation $01.");
        }
        StepRoomEventFrames(16 + 50);
        if (!_dialogue.IsOpen || _dialogue.CurrentMessage !=
            "Sit here and\nlisten. How\ncharming..." ||
            actors["Bear"].Position != new Vector2(0x58, 0x28) ||
            !_saveData.HasRoomFlag(group, roomId, OracleSaveData.RoomFlag80))
        {
            throw new InvalidOperationException(
                "The bear did not wait 20, move upward for 32, settle for 50, and set room flag $80.");
        }
        _dialogue.Close();
        StepRoomEventFrames(1);
        if (_roomEvents.Active || _player.CutsceneControlled ||
            nayruIntro.CurrentStage != 1)
        {
            throw new InvalidOperationException("The bear lead-in did not restore Link control.");
        }

        _sound.ClearPlayRequestAudit();
        _player.WarpTo(new Vector2(0x60, 0x3d), recordSafe: false);
        StepRoomEventFrames(1);
        if (nayruIntro.CurrentStage != 6 || nayruIntro.Counter != 120 ||
            !_player.CutsceneControlled)
        {
            throw new InvalidOperationException(
                "The x>=$60/y<$3e initial Nayru cutscene boundary did not install the 120-update wait.");
        }
        StepRoomEventFrames(120);
        if (!_dialogue.IsOpen || _dialogue.CurrentMessage != "Isn't it \nenchanting?")
            throw new InvalidOperationException("TX_5706 did not follow the bear's 120-update wait.");
        _dialogue.Close();
        StepRoomEventFrames(1);
        StepRoomEventFrames(30);
        if (nayruIntro.CurrentStage != 9 || nayruIntro.Counter != 11)
            throw new InvalidOperationException("The post-TX_5706 30-update delay did not begin the fast fade.");
        StepRoomEventFrames(11);
        NayruSingingScreen? singing =
            _scene.InterfaceLayer.GetNodeOrNull<NayruSingingScreen>("NayruSingingScreen");
        if (nayruIntro.CurrentStage != 10 || singing is null || singing.ScrollX != 0 ||
            _hud.Visible || _warpFade.Position != Vector2.Zero ||
            _warpFade.Size != new Vector2(
                OracleRoomData.ViewportWidth, OracleRoomData.ScreenHeight) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndCloseMenu) != 1)
        {
            throw new InvalidOperationException(
                "GFXH_NAYRU_SINGING_CUTSCENE did not play SND_CLOSEMENU and replace the " +
                "room/HUD after 11 updates.");
        }
        StepRoomEventFrames(320);
        if (singing.ScrollX != 40 || nayruIntro.CurrentStage != 10)
        {
            throw new InvalidOperationException(
                "The singing still did not perform 40 one-pixel scrolls at eight-update intervals.");
        }
        StepRoomEventFrames(280);
        StepRoomEventFrames(11);
        if (nayruIntro.CurrentStage != 12 || !_hud.Visible ||
            _warpFade.Position !=
                new Vector2(0, OracleRoomData.GameplayScreenTop) ||
            _warpFade.Size != new Vector2(
                OracleRoomData.ViewportWidth, OracleRoomData.ViewportHeight) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndCloseMenu) != 2)
        {
            throw new InvalidOperationException(
                "The 600-update singing screen did not replay SND_CLOSEMENU and enter the room script.");
        }

        int scriptFrames = 0;
        int observedVignettes = 0;
        int ralphFallFrames = 0;
        bool sawStaticFallenRalph = false;
        bool sawVisibleLightning = false;
        bool sawVisibleSwordGift = false;
        bool sawSwordPickupPose = false;
        bool sawHudDuringVignetteSequence = false;
        bool hudHiddenDuringVignetteSequence = false;
        bool sawLinkFaceNayru = false;
        bool sawLinkFaceNayruSecond = false;
        bool sawLinkFaceRalph = false;
        bool sawLinkFaceImpaAfterReveal = false;
        bool sawNayruStopSingingForRalph = false;
        bool sawRalphAirborne = false;
        bool sawDarkPalette = false;
        bool sawAudienceAirborne = false;
        bool sawBoyShockDoubleCadence = false;
        bool sawBoyEscapeNormalCadence = false;
        bool sawVeranReactionMovement = false;
        bool sawPossessionFlash = false;
        bool sawRalphSword = false;
        bool sawNayruAscent = false;
        bool sawNayruDescent = false;
        bool sawNeutralLinkDuringVeranSpeech = false;
        bool sawSideviewMusic = false;
        bool sawRoomOfRitesMusic = false;
        bool sawVignetteRestartSilence = false;
        bool sawDisasterMusic = false;
        bool sawSadnessMusic = false;
        string swordMessage = DialogueBox.PlainText(nayruDatabase.Text(0x001c).Message);
        string nayruGreeting = DialogueBox.PlainText(nayruDatabase.Text(0x1d00).Message);
        string nayruSecondGreeting = DialogueBox.PlainText(nayruDatabase.Text(0x1d22).Message);
        string ralphIntroduction = DialogueBox.PlainText(nayruDatabase.Text(0x2a00).Message);
        string ralphReply = DialogueBox.PlainText(nayruDatabase.Text(0x2a22).Message);
        string veranAgeSpeech = DialogueBox.PlainText(nayruDatabase.Text(0x5605).Message);
        string nayruDownAnimation = nayruDatabase.Actor("Nayru").Animation(2);
        string impaRevealAnimation = nayruDatabase.Actor("AftermathImpa").Animation(4);
        string ralphFallAnimation = nayruDatabase.Actor("AftermathRalph").Animation(8);
        while (!_saveData.HasGlobalFlag(OracleSaveData.GlobalFlagIntroDone) &&
            scriptFrames < 20000)
        {
            NayruActorRegistry currentActors = nayruIntro.ActorRegistry;
            int visitedVignettes = nayruTrace.OrValues("VignetteVisited");
            sawSideviewMusic |= _sound.ActiveMusic == OracleSoundEngine.MusLadxSideview;
            sawRoomOfRitesMusic |= _sound.ActiveMusic == OracleSoundEngine.MusRoomOfRites;
            sawVignetteRestartSilence |= nayruIntro.CurrentVignetteIndex == 0 &&
                nayruIntro.VignetteElapsed is >= 1 and <= 120 && _sound.ActiveMusic == 0;
            sawDisasterMusic |= visitedVignettes != 0 &&
                !currentActors.ContainsKey("AftermathRalph") &&
                _sound.ActiveMusic == OracleSoundEngine.MusDisaster;
            sawSadnessMusic |= currentActors.ContainsKey("AftermathRalph") &&
                _sound.ActiveMusic == OracleSoundEngine.MusSadness;
            if (visitedVignettes != 0 && _roomEvents.Active)
            {
                sawHudDuringVignetteSequence |= _hud.Visible;
                hudHiddenDuringVignetteSequence |= !_hud.Visible;
            }
            if (_dialogue.IsOpen && _dialogue.CurrentMessage == nayruGreeting &&
                _player.FacingVector == Vector2I.Up)
                sawLinkFaceNayru = true;
            if (_dialogue.IsOpen && _dialogue.CurrentMessage == nayruSecondGreeting &&
                _player.FacingVector == Vector2I.Up)
                sawLinkFaceNayruSecond = true;
            if (_dialogue.IsOpen && _dialogue.CurrentMessage == ralphReply &&
                _player.FacingVector == Vector2I.Right)
                sawLinkFaceRalph = true;
            if (_dialogue.IsOpen && _dialogue.CurrentMessage == veranAgeSpeech &&
                !_player.Walking)
            {
                sawNeutralLinkDuringVeranSpeech = true;
            }
            if (_dialogue.IsOpen && _dialogue.CurrentMessage == ralphIntroduction &&
                currentActors.TryGetValue("Nayru", out NpcCharacter? listeningNayru) &&
                listeningNayru.CurrentScriptAnimationSource == nayruDownAnimation)
            {
                sawNayruStopSingingForRalph = true;
            }
            if (currentActors.TryGetValue("Impa", out NpcCharacter? revealingImpa) &&
                revealingImpa.Active &&
                revealingImpa.CurrentScriptAnimationSource == impaRevealAnimation &&
                _player.FacingVector == Vector2I.Down)
            {
                sawLinkFaceImpaAfterReveal = true;
            }
            if (currentActors.TryGetValue("Ralph", out NpcCharacter? introRalph) &&
                introRalph.ScriptDrawOffset.Y < 0)
                sawRalphAirborne = true;
            sawDarkPalette |= _currentRoom.TemporaryBackgroundPaletteBlend >= 1.0f;
            foreach (string audienceName in new[] { "Monkey", "Rabbit", "Boy", "Bird" })
            {
                if (currentActors.TryGetValue(audienceName, out NpcCharacter? audience) &&
                    audience.ScriptDrawOffset.Y < 0)
                    sawAudienceAirborne = true;
            }
            if (currentActors.TryGetValue("Boy", out NpcCharacter? shockedBoy))
            {
                if (shockedBoy.CurrentScriptAnimationSource ==
                        nayruDatabase.Actor("Boy").Animation(2) &&
                    Mathf.IsEqualApprox(shockedBoy.AnimationRate, 2.0f))
                {
                    sawBoyShockDoubleCadence = true;
                }
                if (sawBoyShockDoubleCadence && shockedBoy.ScriptDrawOffset.Y < 0 &&
                    Mathf.IsEqualApprox(shockedBoy.AnimationRate, 1.0f))
                {
                    sawBoyEscapeNormalCadence = true;
                }
            }
            if (currentActors.ContainsKey("GhostVeran") && _scene.WarpFade.Color.A >= 0.99f)
                sawPossessionFlash = true;
            if (currentActors.TryGetValue("RalphSword", out NpcCharacter? ralphSword) &&
                ralphSword.Active && ralphSword.CurrentAnimationOpaquePixels > 0)
                sawRalphSword = true;
            if (currentActors.TryGetValue("Nayru", out NpcCharacter? flyingNayru) &&
                flyingNayru.ScriptDrawOffset.Y < 0)
            {
                if (flyingNayru.Position.X == 0x78)
                    sawNayruAscent = true;
                if (flyingNayru.Position == new Vector2(0x28, 0x38))
                    sawNayruDescent = true;
            }
            foreach (NpcCharacter effect in _entities.Entities<NpcCharacter>())
            {
                if (effect.Name.ToString().StartsWith(
                        "NayruIntroEffect_Lightning", StringComparison.Ordinal) &&
                    effect.Active && effect.CurrentAnimationOpaquePixels > 0)
                {
                    sawVisibleLightning = true;
                }
            }
            ChestTreasureEffect? swordGift =
                _scene.WorldRoot.GetNodeOrNull<ChestTreasureEffect>("NayruSwordGift");
            if (_dialogue.IsOpen && _dialogue.CurrentMessage == swordMessage &&
                swordGift is not null)
            {
                sawVisibleSwordGift = true;
                sawSwordPickupPose |= _player.IsHoldingItemOneHand &&
                    swordGift.Position == _player.Position + new Vector2(-4, -14);
            }
            if (_dialogue.IsOpen)
                _dialogue.Close();
            StepRoomEventFrames(1);
            scriptFrames++;

            int newVignettes = visitedVignettes & ~observedVignettes;
            if (newVignettes != 0)
            {
                (int Group, int Room) expected = newVignettes switch
                {
                    1 => (0, 0x98),
                    2 => (0, 0x5a),
                    4 => (2, 0x0e),
                    _ => throw new InvalidOperationException(
                        $"Multiple Nayru vignettes advanced on one update (${newVignettes:x2}).")
                };
                if (_rooms.ActiveGroup != expected.Group ||
                    _rooms.CurrentRoom.Id != expected.Room)
                {
                    throw new InvalidOperationException(
                        $"Nayru vignette ${newVignettes:x2} showed " +
                        $"{_rooms.ActiveGroup:x1}:{_rooms.CurrentRoom.Id:x2} instead of " +
                        $"{expected.Group:x1}:{expected.Room:x2}.");
                }
                observedVignettes |= newVignettes;
            }

            if (nayruIntro.ActorRegistry.TryGetValue(
                    "AftermathRalph", out NpcCharacter? aftermathRalph) &&
                aftermathRalph.Active &&
                aftermathRalph.CurrentScriptAnimationSource == ralphFallAnimation)
            {
                ralphFallFrames++;
                if (ralphFallFrames > 60)
                {
                    sawStaticFallenRalph = true;
                    if (aftermathRalph.CurrentAnimationFrame != 9)
                        throw new InvalidOperationException(
                            "Ralph's animation $08 restarted its falling frames during TX_2a03.");
                }
            }
        }
        CutsceneCommandTraceEntry[] nayruStarts = nayruTrace.Entries
            .Where(entry => entry.Phase == CutsceneCommandTracePhase.Started)
            .ToArray();
        int importedTranslateCount = nayruDatabase.Commands
            .Count(command => command is CutsceneTranslateCommand or
                CutsceneParallelTranslateCommand);
        int startedTranslateCount = nayruStarts.Count(entry =>
            entry.Source.Opcode is "translate" or "paralleltranslate");
        int linkVeranFacingMask = nayruTrace.OrValues("LinkVeranFacing");
        int ralphVeranFacingMask = nayruTrace.OrValues("RalphVeranFacing");
        int ghostTrackingPhases = nayruTrace.OrValues("GhostTrackingPhase");
        sawVeranReactionMovement =
            nayruTrace.SawPosition(
                "ActorPosition", "Player", new Vector2(0x57, 0x30)) &&
            nayruTrace.SawPosition(
                "ActorPosition", "Ralph", new Vector2(0x88, 0x51));
        bool movementFacingShown =
            startedTranslateCount == importedTranslateCount &&
            nayruTrace.Saw("VignetteMovement", "VignetteGirl", 0) &&
            nayruTrace.Saw("VignetteMovement", "VignetteBoy", 1) &&
            nayruTrace.Saw("VignetteMovement", "VignetteBoy", 3) &&
            nayruTrace.Saw("VignetteMovement", "VignetteLady", 2) &&
            nayruTrace.Saw("VignetteMovement", "VignetteLady", 3);
        bool vignetteDetailShown =
            nayruTrace.Saw("VignetteGirlJump") &&
            nayruTrace.Saw("VignetteMonkeyHop") &&
            nayruTrace.Saw("VignetteMonkeyPacing") &&
            nayruTrace.Saw("VignetteMonkeyStone") &&
            nayruTrace.Saw("VignetteMonkeyFlicker") &&
            nayruTrace.Saw("VignetteBoyPalette") &&
            nayruTrace.Saw("VignetteLadyCadence") &&
            nayruTrace.Count("VignetteExclamation") == 3;
        bool completeCommandTrace = nayruStarts.Length == nayruDatabase.Commands.Count &&
            nayruStarts.Select(entry => entry.Source.CommandIndex)
                .SequenceEqual(Enumerable.Range(0, nayruDatabase.Commands.Count));
        int rumbleRequests = _sound.PlayRequestsFor(OracleSoundEngine.SndRumble2);
        bool exactCutsceneSounds =
            _sound.PlayRequestsFor(OracleSoundEngine.SndCloseMenu) == 2 &&
            _sound.PlayRequestsFor(OracleSoundEngine.SndJump) == 4 &&
            _sound.PlayRequestsFor(OracleSoundEngine.SndBossDead) == 1 &&
            _sound.PlayRequestsFor(OracleSoundEngine.SndUnknown5) == 4 &&
            _sound.PlayRequestsFor(OracleSoundEngine.SndSwordSpin) == 8 &&
            _sound.PlayRequestsFor(OracleSoundEngine.SndTeleport) == 2 &&
            _sound.PlayRequestsFor(OracleSoundEngine.SndSwordObtained) == 2 &&
            rumbleRequests is >= 13 and <= 15 &&
            _sound.PlayRequestsFor(OracleSoundEngine.SndKillEnemy) == 1 &&
            _sound.PlayRequestsFor(OracleSoundEngine.SndSwordSlash) == 1 &&
            _sound.PlayRequestsFor(OracleSoundEngine.SndLightning) == 6 &&
            _sound.PlayRequestsFor(OracleSoundEngine.SndSlash) == 1 &&
            _sound.PlayRequestsFor(OracleSoundEngine.SndWarpStart) == 1 &&
            _sound.PlayRequestsFor(OracleSoundEngine.SndClink) == 10 &&
            _sound.PlayRequestsFor(OracleSoundEngine.SndBoomerang) == 1 &&
            _sound.PlayRequestsFor(OracleSoundEngine.SndGetItem) == 1;
        if (!_saveData.HasGlobalFlag(OracleSaveData.GlobalFlagIntroDone) ||
            !_saveData.HasRoomFlag(group, roomId, OracleSaveData.RoomFlag40) ||
            _currentRoom.GetMetatile(portalPoint) != 0xd7 ||
            nayruIntro.CurrentStage != 0 || nayruIntro.ActorRegistry.Count != 0 ||
            _roomEvents.Active || _player.CutsceneControlled || !_hud.Visible ||
            _rooms.ActiveGroup != group || _rooms.CurrentRoom.Id != roomId ||
            observedVignettes != 0x07 || nayruTrace.Count("LightningSpawn") != 6 ||
            !sawVisibleLightning || !nayruTrace.Saw("CollapsedImpaRendered") ||
            !nayruTrace.SawPosition(
                "ActorPosition", "Nayru", new Vector2(0x78, 0x20)) ||
            !nayruTrace.Saw("SwordGift") || !sawVisibleSwordGift ||
            !sawSwordPickupPose || _player.IsHoldingItemOneHand ||
            !sawHudDuringVignetteSequence || hudHiddenDuringVignetteSequence ||
            !sawStaticFallenRalph || !sawLinkFaceNayru || !sawLinkFaceNayruSecond ||
            !sawLinkFaceRalph || !sawLinkFaceImpaAfterReveal ||
            !sawNeutralLinkDuringVeranSpeech ||
            !sawNayruStopSingingForRalph ||
            !sawRalphAirborne || nayruTrace.Count("RalphJump") != 2 ||
            !sawDarkPalette || !nayruTrace.Saw("DarkPalette") ||
            !sawAudienceAirborne || !nayruTrace.Saw("AudienceAirborne") ||
            !sawBoyShockDoubleCadence || !sawBoyEscapeNormalCadence ||
            !nayruTrace.Saw("BoyEscapeStarted") || !nayruTrace.Saw("BoyEscaped") ||
            ghostTrackingPhases != 0x3c ||
            linkVeranFacingMask != 0x0f || ralphVeranFacingMask != 0x0d ||
            !sawVeranReactionMovement || !sawPossessionFlash ||
            !sawRalphSword || !nayruTrace.Saw("RalphSwordVisible") ||
            !nayruTrace.SawPosition(
                "ActorPosition", "Nayru", new Vector2(0x78, 0x18)) ||
            !nayruTrace.Saw("GhostHiddenAfterPossession") ||
            !nayruTrace.Saw("PostChargeFacing") ||
            !nayruTrace.Saw("PossessionSway") ||
            !nayruTrace.Saw("PossessionBlink") ||
            !nayruTrace.Saw("PossessionMovementSync") ||
            !nayruTrace.Saw("GhostEmergence") ||
            !nayruTrace.Saw("RalphSwordSpacing") ||
            !nayruTrace.Saw("AftermathLinkWalk") ||
            !movementFacingShown || !vignetteDetailShown ||
            (nayruTrace.OrValues("AftermathRalphFacing") & 0x07) != 0x07 ||
            !sawNayruAscent || !sawNayruDescent ||
            !nayruTrace.Saw("PortalFlight") || !completeCommandTrace ||
            !sawSideviewMusic || !sawRoomOfRitesMusic ||
            !sawVignetteRestartSilence || !sawDisasterMusic || !sawSadnessMusic ||
            !exactCutsceneSounds ||
            _sound.ActiveMusic != OracleSoundEngine.MusOverworld ||
            _sound.MusicVolume != 3 ||
            !_inventory.HasTreasure(TreasureDatabase.TreasureSword) ||
            _inventory.SwordLevel != 1 || !_inventoryMenu.CanOpenForValidation ||
            !_mapMenu.CanOpenNormalForValidation ||
            scriptFrames >= 20000)
        {
            throw new InvalidOperationException(
                $"The Nayru possession/portal/vignette/aftermath sequence did not complete " +
                $"(frames={scriptFrames}, stage={nayruIntro.CurrentStage}, " +
                $"faces={sawLinkFaceNayru}/{sawLinkFaceNayruSecond}/" +
                $"{sawLinkFaceRalph}/{sawLinkFaceImpaAfterReveal}, " +
                $"neutralLink={sawNeutralLinkDuringVeranSpeech}, " +
                $"boy={sawBoyShockDoubleCadence}/{sawBoyEscapeNormalCadence}/" +
                $"{nayruTrace.Saw("BoyEscapeStarted")}/{nayruTrace.Saw("BoyEscaped")}" +
                $", track={ghostTrackingPhases:x2}:" +
                $"{linkVeranFacingMask:x2}/{ralphVeranFacingMask:x2}, " +
                $"ghostHidden={nayruTrace.Saw("GhostHiddenAfterPossession")}, " +
                $"listen/down={sawNayruStopSingingForRalph}/" +
                $"{nayruTrace.Saw("PostChargeFacing")}, " +
                $"possession={nayruTrace.Saw("PossessionSway")}/" +
                $"{nayruTrace.Saw("PossessionBlink")}/" +
                $"{nayruTrace.Saw("PossessionMovementSync")}/" +
                $"{nayruTrace.Saw("GhostEmergence")}, " +
                $"swordSpace={nayruTrace.Saw("RalphSwordSpacing")}, " +
                $"moveFacing={movementFacingShown}, vignette={vignetteDetailShown}, " +
                $"hud={sawHudDuringVignetteSequence}/{hudHiddenDuringVignetteSequence}, " +
                $"ralphTracking={nayruTrace.OrValues("AftermathRalphFacing"):x2}, " +
                $"linkWalk={nayruTrace.Saw("AftermathLinkWalk")}, " +
                $"reaction={sawVeranReactionMovement}, flash={sawPossessionFlash}, " +
                $"sword={sawRalphSword}/{nayruTrace.Saw("RalphSwordVisible")}/" +
                $"{sawSwordPickupPose}/{_player.IsHoldingItemOneHand}, " +
                $"flight={sawNayruAscent}/{sawNayruDescent}/" +
                $"{nayruTrace.Saw("PortalFlight")}, trace={nayruStarts.Length}/" +
                $"{nayruDatabase.Commands.Count}, " +
                $"sfx={exactCutsceneSounds}:jump" +
                $"{_sound.PlayRequestsFor(OracleSoundEngine.SndJump)}/" +
                $"spin{_sound.PlayRequestsFor(OracleSoundEngine.SndSwordSpin)}/" +
                $"clink{_sound.PlayRequestsFor(OracleSoundEngine.SndClink)}/" +
                $"lightning{_sound.PlayRequestsFor(OracleSoundEngine.SndLightning)}/" +
                $"rumble{rumbleRequests}, " +
                $"music={sawSideviewMusic}/{sawRoomOfRitesMusic}/" +
                $"{sawVignetteRestartSilence}/{sawDisasterMusic}/{sawSadnessMusic}/" +
                $"{_sound.ActiveMusic:x2}:{_sound.MusicVolume}).");
        }
        _entities.Update(1.0 / 60.0, _player);
        TimePortal? portal = _entities.Entities<TimePortal>().SingleOrDefault();
        if (portal is null || !portal.Active)
            throw new InvalidOperationException("Lightning tile $22=$d7 did not activate portal $e1:$01.");

        _currentRoom.ReplaceMetatile(portalPoint, 0xd7, 0x3a, (long)_animationTicks);
        LoadValidationRoom(group, roomId);
        _entities.Update(1.0 / 60.0, _player);
        portal = _entities.Entities<TimePortal>().SingleOrDefault();
        if (_currentRoom.GetMetatile(portalPoint) != 0xd7 || portal is null || !portal.Active ||
            nayruIntro.CurrentStage != 0)
        {
            throw new InvalidOperationException(
                "Room flag $40 did not restore the opened portal without retriggering the intro.");
        }

        // Leave the cached room in its imported state for the independent portal validation.
        _currentRoom.ReplaceMetatile(portalPoint, 0xd7, 0x3a, (long)_animationTicks);
        _saveData.SetRoomFlag(group, roomId, OracleSaveData.RoomFlag40, value: false);
        _saveData.SetRoomFlag(group, roomId, OracleSaveData.RoomFlag80, value: false);
        _roomEvents.CommandTraceSink = null;
        GD.Print("Validated room 0:39's pre-GLOBALFLAG_INTRO_DONE $6b:$01 audience, " +
            "$01/$02/$04/$08/$10 talk mask, cplinkx/turnToFaceLink talk facings, bird " +
            "$02/$03 hop and exact pose resets, bear room flag $80 movement, $60/$3e trigger, " +
            "solid dynamic actors and outgoing scrolling, visible singing notes, 120/30/600 " +
            "timing, imported singing OAM and 40-pixel scroll, opposing SPEED_60 notes, all " +
            "Link/Nayru/Ralph/Impa dialogue, neutral Link holds after translated movement, " +
            "and cfd5/cfd6 ghost-facing handoffs with Link's " +
            "8-update and Ralph's 16-update cadence, Nayru's held $02, cached collision target, " +
            "clockwise diagonal rounding and cfd2 left turn, opcode-driven movement " +
            "facings with preserved raw-angle backwalk poses, aftermath Ralph tracking, two Ralph jumps, PALH $99 " +
            "darkening, the boy's $0e-$10 double animation cadence and normal-speed jumping escape, " +
            "boy-inclusive audience escapes, Link/Ralph reaction movement, " +
            "Nayru's stopped singing/down-facing backstep, possession white flash, exact " +
            "palette blink/sway and 150/220-start offset, hand-raised ghost emergence, spaced " +
            "linked Ralph sword, animated aftermath Link walking, Nayru's ascent/landing, fainted Impa, " +
            "portal/vignette lightning, exact $98/$5a/2:$0e room swaps and 937/600/645-update " +
            "actor scripts with jumps, pacing, stone palettes, flicker, and exclamation marks, one-shot Ralph fall, " +
            "visible sword handoff, Fairy Fountain/Nayru/sideview/Room of Rites/" +
            "vignette-stop/Disaster/Sadness/room-music cue chain, " +
            "all actor/part/treasure SFX calls and repeated global-frame cues, " +
            "$22=$d7/flag $40, aftermath, and persistent completion.");
    }

    private void ValidateRalphPortalDepartureEvent()
    {
        RalphPortalEvent ralphEvent = _roomEvents.Ralph;
        var commandTrace = new ValidationCutsceneTrace();
        _roomEvents.CommandTraceSink = commandTrace;
        _sound.ClearPlayRequestAudit();
        // @initSubid0d deletes the object on a direct room load because
        // wScreenTransitionDirection is not DIR_RIGHT ($01).
        LoadValidationRoom(0, 0x39);
        NpcCharacter? directRalph = _npcNodes.Find(npc =>
            npc.Record.Id == 0x37 && npc.Record.SubId == 0x0d);
        if (directRalph is null || directRalph.Active || _roomEvents.Active)
        {
            throw new InvalidOperationException(
                "INTERAC_RALPH $37:$0d ignored its DIR_RIGHT room-entry guard.");
        }

        LoadValidationRoom(0, 0x38);
        _transitions.BeginScroll(_player, Vector2I.Right, 0x39);
        NpcCharacter? ralph = _npcNodes.Find(npc =>
            npc.Record.Id == 0x37 && npc.Record.SubId == 0x0d);
        if (ralph is null || !ralph.Active || !_roomEvents.Active ||
            !ralphEvent.WaitingForScroll || !_entities.ScreenTransitionActive ||
            ralph.Position != new Vector2(0x18, 0x28))
        {
            throw new InvalidOperationException(
                "Room 0:39 did not retain Ralph at $28/$18 while entering from the left.");
        }

        int ralphScrollFrames = FinishActiveScrollingTransitionWithRoomEventsForValidation();
        if (ralphScrollFrames != 40)
            throw new InvalidOperationException(
                $"The 0:38 -> 0:39 horizontal scroll took {ralphScrollFrames} updates, expected 40.");
        if (!_player.CutsceneControlled || ralphEvent.Counter != 40)
            throw new InvalidOperationException(
                "Ralph's destination event fast-forwarded instead of installing its full " +
                "40-update wait after scrolling.");
        StepRoomEventFrames(39);
        if (_dialogue.IsOpen || ralphEvent.Counter != 1)
            throw new InvalidOperationException("Ralph's introductory wait ended early.");
        StepRoomEventFrames(1);
        if (!_dialogue.IsOpen || _dialogue.CurrentMessage !=
            "The Maku Tree?\nThis is more\nof Veran's work!\nLink! You made\nit! Veran just\nleapt through\nthis Time\nPortal! If we go\nback in time, we\nshould be able\nto save Nayru\nand the Maku\nTree! I'm\ncoming, Nayru!")
        {
            throw new InvalidOperationException(
                "TX_2a1e did not open after Ralph's original 40-update wait.");
        }

        _dialogue.Close();
        StepRoomEventFrames(1);
        StepRoomEventFrames(29);
        if (ralphEvent.Counter != 1 || ralph.Position != new Vector2(0x18, 0x28))
            throw new InvalidOperationException("Ralph's post-text 30-update wait ended early.");
        StepRoomEventFrames(1);
        if (ralph.CurrentAnimationFrame != 0)
            throw new InvalidOperationException(
                "Ralph did not select animation $01 after the post-text wait.");
        StepRoomEventFrames(2);
        if (ralph.Position != new Vector2(0x18, 0x28))
            throw new InvalidOperationException(
                "Ralph moved during the setspeed/setangle script-command updates.");
        StepRoomEventFrames(1);
        if (ralphEvent.Counter != 17 || ralph.Position != new Vector2(0x18, 0x28))
            throw new InvalidOperationException(
                "Ralph did not install applyspeed counter $11 on its own script update.");

        StepRoomEventFrames(12);
        if (ralph.Position != new Vector2(0x24, 0x28) ||
            ralph.CurrentAnimationFrame != 0 || ralphEvent.Counter != 5)
        {
            throw new InvalidOperationException(
                "Ralph's SPEED_100 movement or animation $01 first-frame duration diverged.");
        }
        StepRoomEventFrames(1);
        if (ralph.Position != new Vector2(0x25, 0x28) ||
            ralph.CurrentAnimationFrame != 1 || ralphEvent.Counter != 4)
        {
            throw new InvalidOperationException(
                "Ralph's animation $01 did not change after its original 16 updates.");
        }
        StepRoomEventFrames(2);
        if (ralph.Position != new Vector2(0x27, 0x28) || ralphEvent.Counter != 2)
            throw new InvalidOperationException("Ralph's SPEED_100 movement skipped an update.");
        StepRoomEventFrames(1);
        if (ralph.Position != new Vector2(0x28, 0x28) ||
            ralphEvent.Counter != 1)
        {
            throw new InvalidOperationException(
                "applyspeed $11 did not move Ralph exactly 16 pixels to the portal.");
        }
        StepRoomEventFrames(1);
        if (ralphEvent.Counter != 0 || ralphEvent.Flickering ||
            ralph.Position != new Vector2(0x28, 0x28))
        {
            throw new InvalidOperationException(
                "Ralph's counter2 path did not pause for one update after reaching zero.");
        }
        StepRoomEventFrames(1);
        if (ralph.CurrentAnimationFrame != 0 || ralphEvent.Flickering)
            throw new InvalidOperationException("Ralph did not select portal animation $09.");
        StepRoomEventFrames(2);
        if (ralphEvent.Counter != 45 || ralphEvent.Flickering ||
            _sound.LastPlayRequest != OracleSoundEngine.SndMysterySeed ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndMysterySeed) != 1)
        {
            throw new InvalidOperationException(
                "Ralph's var3f=$2d and SND_MYSTERY_SEED commands lost their script updates.");
        }
        StepRoomEventFrames(1);
        bool firstFlickerVisibility = (_entities.FrameCounter & 1) != 0;
        if (!ralphEvent.Flickering || ralphEvent.Counter != 44 ||
            ralph.CurrentAnimationFrame != 0 || ralph.Visible != firstFlickerVisibility ||
            ralphEvent.Completed)
        {
            throw new InvalidOperationException(
                "Ralph did not select animation $09 and begin the $2d-frame parity flicker.");
        }
        StepRoomEventFrames(1);
        if (ralph.Visible == firstFlickerVisibility || ralphEvent.Counter != 43)
            throw new InvalidOperationException(
                "Ralph's objectFlickerVisibility b=$01 did not alternate every update.");
        StepRoomEventFrames(42);
        if (!_roomEvents.Active || ralphEvent.Counter != 1 ||
            ralphEvent.Completed || !ralph.Active)
        {
            throw new InvalidOperationException(
                "Ralph's $2d-frame portal flicker completed one update early.");
        }
        StepRoomEventFrames(1);
        if (_roomEvents.Active || !ralphEvent.Completed || ralph.Active ||
            _player.CutsceneControlled ||
            _sound.ActiveMusic != OracleSoundEngine.MusOverworld ||
            !_saveData.HasGlobalFlag(OracleSaveData.GlobalFlagRalphEnteredPortal))
        {
            throw new InvalidOperationException(
                "Ralph's departure did not set GLOBALFLAG_RALPH_ENTERED_PORTAL $40 and restore input.");
        }

        LoadValidationRoom(0, 0x39);
        NpcCharacter? completedRalph = _npcNodes.Find(npc =>
            npc.Record.Id == 0x37 && npc.Record.SubId == 0x0d);
        if (completedRalph is null || completedRalph.Active || _roomEvents.Active)
            throw new InvalidOperationException("Ralph's one-shot portal event retriggered after flag $40.");

        CutsceneCommandTraceEntry[] starts = commandTrace.Entries
            .Where(entry => entry.Phase == CutsceneCommandTracePhase.Started)
            .ToArray();
        string[] expectedOpcodes =
        {
            "disableinput", "wait", "showtext", "wait", "setanimation",
            "setspeed", "setangle", "applyspeed", "setanimation",
            "writeobjectbyte", "playsound", "flicker", "setglobalflag",
            "native", "enableinput", "scriptend"
        };
        if (starts.Length != expectedOpcodes.Length ||
            starts.Where((entry, index) =>
                entry.Source.Script != "ralphSubid0dScript" ||
                entry.Source.CommandIndex != index ||
                entry.Source.Opcode != expectedOpcodes[index] ||
                entry.Source.SourceLine <= 0).Any())
        {
            throw new InvalidOperationException(
                "The importer-generated Ralph command trace lost script labels, " +
                "source lines, command order, or typed opcodes.");
        }
        int flickerCompletionUpdate = commandTrace.Entries.Single(entry =>
            entry.Source.CommandIndex == 11 &&
            entry.Phase == CutsceneCommandTracePhase.Completed).ScriptUpdate;
        if (starts[12].ScriptUpdate != flickerCompletionUpdate ||
            starts[13].ScriptUpdate != flickerCompletionUpdate ||
            starts[14].ScriptUpdate != flickerCompletionUpdate ||
            starts[15].ScriptUpdate != flickerCompletionUpdate)
        {
            throw new InvalidOperationException(
                "Ralph's completion flag, native music restore, enableinput, and " +
                "scriptend did not continue on the final flicker update.");
        }
        _roomEvents.CommandTraceSink = null;

        GD.Print("Validated room 0:39 Ralph $37:$0d DIR_RIGHT guard, TX_2a1e, " +
            "40/30 waits, per-command script cadence, animation $01, 16-pixel SPEED_100 " +
            "movement, animation $09, SND_MYSTERY_SEED, $2d-frame flicker, " +
            "same-update completion chain, imported source trace, MUS_OVERWORLD restore, " +
            "and persistent GLOBALFLAG $40.");
    }
}
