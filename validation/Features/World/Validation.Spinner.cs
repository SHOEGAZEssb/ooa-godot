using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateDungeonSpinner()
    {
        const double update = 1.0 / OracleSoundEngine.UpdatesPerSecond;
        const int crystalsFlag = 0x0f;
        static void Step(RoomEntityManager entities, Player player, int count = 1)
        {
            for (int updateIndex = 0; updateIndex < count; updateIndex++)
                entities.Update(update, player);
        }

        var placements = new DungeonSpinnerDatabase();
        DungeonInteractionVisual visual =
            new DungeonInteractionVisualDatabase().Visual("spinner");
        AnimationDefinition blueTurn =
            OracleGraphicsCache.GetAnimationDefinition(visual.Animations[0]);
        AnimationDefinition redTurn =
            OracleGraphicsCache.GetAnimationDefinition(visual.Animations[1]);
        AnimationDefinition blueArrow =
            OracleGraphicsCache.GetAnimationDefinition(visual.Animations[2]);
        AnimationDefinition redArrow =
            OracleGraphicsCache.GetAnimationDefinition(visual.Animations[3]);
        FailIf(
            placements.RecordCount != 2 ||
            blueTurn.Frames.Select(frame => (frame.Duration, frame.Parameter))
                .ToArray() is not
                [(15, 0), (4, 1), (4, 2), (4, 3), (4, 4), (127, 255)] ||
            redTurn.Frames.Select(frame => (frame.Duration, frame.Parameter))
                .ToArray() is not
                [(15, 0), (4, 15), (4, 14), (4, 13), (4, 12), (127, 255)] ||
            blueArrow.Frames.Select(frame => frame.Duration).ToArray() is not
                [12, 12, 12, 12] ||
            redArrow.Frames.Select(frame => frame.Duration).ToArray() is not
                [12, 12, 12, 12],
            "INTERAC_SPINNER lost its imported 15/4/4/4/4 turn signals, " +
            "$ff terminal frame, or 12-update arrow animation.");

        _saveData.SetRoomFlag(
            4, 0x60, OracleSaveData.RoomFlagItem, value: false);
        _saveData.SetGlobalFlag(crystalsFlag, value: false);
        LoadValidationRoom(4, 0x52);
        FailIf(
            _entities.Entities<DungeonSpinnerRoomEntity>().Count != 0,
            "Room 4:52 created the migrated spinner before " +
            "GLOBALFLAG_D3_CRYSTALS $0f was set.");

        _saveData.SetGlobalFlag(crystalsFlag);
        LoadValidationRoom(4, 0x60);
        Vector2 spinnerCenter = new(0x78, 0x58);
        FailIf(
            _entities.Entities<DungeonSpinnerRoomEntity>().Count != 0 ||
            _currentRoom.GetMetatile(spinnerCenter) != 0xf1,
            "Room 4:60 did not remove its spinner and reveal the closed " +
            "Gasha Seed chest after the D3 crystals broke.");
        LoadValidationRoom(4, 0x52);
        FailIf(
            _entities.Entities<DungeonSpinnerRoomEntity>() is not
                [{ Position: var migratedPosition }] ||
            migratedPosition != spinnerCenter,
            "Room 4:52 did not receive room 4:60's mask-$01 spinner at $57 " +
            "after the D3 crystals broke.");

        _saveData.SetGlobalFlag(crystalsFlag, value: false);
        _runtimeState.SetWramByte(OracleRuntimeState.SpinnerStateAddress, 0xa0);
        LoadValidationRoom(4, 0x60);
        _player.EndNewGameSlowFall();
        DungeonSpinnerRoomEntity spinner =
            _entities.Entities<DungeonSpinnerRoomEntity>().Single();
        ulong blueSpinnerHash = OracleGraphicsCache.PixelHash(
            spinner.SpinnerTexture.GetImage());
        ulong blueArrowHash = OracleGraphicsCache.PixelHash(
            spinner.ArrowTexture.GetImage());
        FailIf(
            spinner.Position != spinnerCenter || spinner.Red ||
            spinner.SpinnerAnimationIndex != 0 ||
            spinner.ArrowAnimationIndex != 2 ||
            blueSpinnerHash == 0 || blueArrowHash == 0,
            "Room 4:60 did not initialize a visible blue/counterclockwise " +
            "spinner and related arrow from clear mask $01.");

        _player.WarpTo(spinnerCenter + new Vector2(0, -15), recordSafe: false);
        _sound.ClearPlayRequestAudit();
        Step(_entities, _player, 31);
        FailIf(
            spinner.Phase != SpinnerPhase.Waiting || spinner.WaitCounter != 1 ||
            _entities.PlayerMovementDisabled,
            "INTERAC_SPINNER left its state-0 plus source wait-30 " +
            "initialization window early.");
        Step(_entities, _player);
        FailIf(
            spinner.Phase != SpinnerPhase.Waiting || spinner.WaitCounter != 0,
            "INTERAC_SPINNER accepted Link at the strict 9+6 collision " +
            "boundary instead of requiring a smaller coordinate delta.");
        _player.StartSwordAttackForValidation(Vector2.Zero);
        _player.SetScriptedPosition(spinnerCenter + new Vector2(0, -14));
        Step(_entities, _player);
        FailIf(
            spinner.Phase != SpinnerPhase.Touched ||
            !_entities.PlayerSwordDisabled ||
            !_entities.PlayerItemUsageDisabled ||
            !_entities.PlayerMovementDisabled ||
            !_entities.PlayerMenusDisabled ||
            !_player.IsAttacking ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndOpenChest) != 0,
            "The strict 9+6 collision did not enter the one-update spinner " +
            "touch handoff before beginning the turn.");
        Step(_entities, _player);
        FailIf(
            spinner.Phase != SpinnerPhase.Turning ||
            spinner.ExitDirection != 3 ||
            spinner.LinkOffset != new Vector2(0, -12) ||
            _player.PrecisePosition != spinnerCenter + new Vector2(0, -12) ||
            _player.FacingVector != Vector2I.Left ||
            _player.IsAttacking ||
            _entities.ScreenShakeCounter != 3 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndOpenChest) != 1,
            "The blue spinner did not snap an above-entry Link to -12 Y, " +
            "face him left, shake for four source updates, and play " +
            "SND_OPENCHEST once.");

        Step(_entities, _player, 15);
        FailIf(
            spinner.SpinnerAnimationFrame != 1 ||
            spinner.LinkOffset != new Vector2(0, -12) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose) != 0,
            "The blue spinner consumed parameter $01 on the transition into " +
            "its frame instead of the following interaction update.");
        Step(_entities, _player);
        FailIf(
            spinner.LinkOffset != new Vector2(-2, -10) ||
            _player.PrecisePosition != spinnerCenter + new Vector2(-2, -10) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose) != 1,
            "Blue spinner parameter $01 did not move Link to circular point " +
            "$09 and request SND_DOORCLOSE exactly once.");

        int blueTurnUpdates = 16;
        while (spinner.Phase == SpinnerPhase.Turning && blueTurnUpdates < 40)
        {
            Step(_entities, _player);
            blueTurnUpdates++;
        }
        FailIf(
            blueTurnUpdates != 32 ||
            spinner.Phase != SpinnerPhase.Exiting ||
            spinner.ExitCounter != 0x10 ||
            spinner.LinkOffset != new Vector2(-12, 0) ||
            _player.PrecisePosition != spinnerCenter + new Vector2(-12, 0) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose) != 4,
            "The blue spinner did not finish after 32 turning updates at " +
            "circular point $0c with four one-shot animation signals.");

        Step(_entities, _player, 15);
        FailIf(
            spinner.ExitCounter != 1 ||
            _player.PrecisePosition != spinnerCenter + new Vector2(-27, 0) ||
            _runtimeState.ReadWramByte(
                OracleRuntimeState.SpinnerStateAddress) != 0xa0,
            "LINK_STATE_FORCE_MOVEMENT did not retain mask state through its " +
            "first 15 standard-speed exit updates.");
        Step(_entities, _player);
        ulong redSpinnerHash = OracleGraphicsCache.PixelHash(
            spinner.SpinnerTexture.GetImage());
        ulong redArrowHash = OracleGraphicsCache.PixelHash(
            spinner.ArrowTexture.GetImage());
        FailIf(
            spinner.Phase != SpinnerPhase.Waiting || !spinner.Red ||
            spinner.ArrowAnimationIndex != 3 ||
            _player.PrecisePosition != spinnerCenter + new Vector2(-28, 0) ||
            _runtimeState.ReadWramByte(
                OracleRuntimeState.SpinnerStateAddress) != 0xa1 ||
            redSpinnerHash == blueSpinnerHash || redArrowHash == blueArrowHash ||
            _entities.PlayerMovementDisabled,
            "The 16th exit update did not XOR only spinner mask $01, switch " +
            "both visuals to red/clockwise, and release Link.");

        // The post-use script repeats wait 30 before accepting another touch.
        // A red above-entry turns clockwise and therefore exits right while
        // consuming parameters $0f,$0e,$0d,$0c.
        _player.WarpTo(spinnerCenter + new Vector2(0, -14), recordSafe: false);
        Step(_entities, _player, 30);
        FailIf(
            spinner.Phase != SpinnerPhase.Waiting || spinner.WaitCounter != 1,
            "spinnerScript_waitForLinkAfterDelay did not retain its newly " +
            "installed counter through 30 post-use interaction updates.");
        Step(_entities, _player);
        FailIf(
            spinner.Phase != SpinnerPhase.Touched,
            "spinnerScript_waitForLinkAfterDelay did not accept Link when its " +
            "source wait-30 counter reached zero.");
        Step(_entities, _player);
        FailIf(
            spinner.Phase != SpinnerPhase.Turning ||
            spinner.ExitDirection != 1 ||
            _player.FacingVector != Vector2I.Right ||
            spinner.SpinnerAnimationIndex != 1,
            "The red spinner did not select its clockwise animation and " +
            "rightward exit for an above entry.");
        Step(_entities, _player, 15);
        Step(_entities, _player);
        FailIf(
            spinner.LinkOffset != new Vector2(2, -10) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose) != 5,
            "Red spinner parameter $0f did not select clockwise circular " +
            "point $07 exactly once.");
        int redTurnUpdates = 16;
        while (spinner.Phase == SpinnerPhase.Turning && redTurnUpdates < 40)
        {
            Step(_entities, _player);
            redTurnUpdates++;
        }
        Step(_entities, _player, 16);
        FailIf(
            redTurnUpdates != 32 || spinner.Phase != SpinnerPhase.Waiting ||
            spinner.Red ||
            _player.PrecisePosition != spinnerCenter + new Vector2(28, 0) ||
            _runtimeState.ReadWramByte(
                OracleRuntimeState.SpinnerStateAddress) != 0xa0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndOpenChest) != 2 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose) != 8,
            "The red clockwise pass did not mirror the full source turn, " +
            "exit, sound, and shared-state sequence.");

        GD.Print("Validated room 4:60 INTERAC_SPINNER $7d: D3-crystal " +
            "migration to 4:52, imported parent/arrow visuals, strict collision, " +
            "state-0 and repeat wait-30 timing, blue/red circular animation " +
            "signals, four-update shake, 16-update forced exits, sounds, and " +
            "mask-preserving wSpinnerState toggles.");
    }
}
