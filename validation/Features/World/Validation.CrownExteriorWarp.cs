using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownExteriorWarp()
    {
        // group4WarpSources:$bb mask04 -> group0 destination04:$0a/$17.
        // warpSource76aa's doorway fallback -> group4 destination06:$bb/$ff.
        _saveData.SetRoomFlag(0, 0x0a, 0x80); // Entrance already unlocked with Crown Key.
        for (int repeat = 0; repeat < 2; repeat++)
        {
            _runtimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress, 1);
            LoadValidationRoom(4, 0xbb);
            _entities.Clear();
            _player.WarpTo(new(120, 152));
            FailIf(_collision.Collides(_player.Position), "Crown exterior-exit approach must start on real entrance-room floor.");
            for (int i = 0; !_transitions.IsTransitioning && i < 40; i++) StepGameplayUpdates(1, Vector2.Down);
            FailIf(!_transitions.IsTransitioning, "Walking south through Crown's entry must trigger its exterior edge warp.");
            FailIf(_runtimeState.ReadWramByte(WramAddress.wTmpcec0) != 0,
                "The matched 4:bb edge source must clear checkScreenEdgeWarps scratch before destination loading.");
            for (int i = 0; _rooms.ActiveGroup == 4 && i < 160; i++) StepGameplayUpdates(1, Vector2.Zero);
            FailIf(_rooms.ActiveGroup != 0 || _rooms.CurrentRoom.Id != 0x0a || _player.Position != new Vector2(120, 24),
                "Crown's exterior exit must load0:0a/$17.");
            for (int i = 0; _transitions.IsTransitioning && i < 160; i++) StepGameplayUpdates(1, Vector2.Zero);
            FailIf(_transitions.IsTransitioning, "Crown exterior arrival must finish.");
            _entities.Clear();
            // Leave the arrival tile before approaching the unlocked doorway
            // again; wEnteredWarpPosition suppresses an immediate bounce.
            StepGameplayUpdates(16, Vector2.Down);
            FailIf(_transitions.IsTransitioning || _collision.Collides(_player.Position),
                "Leaving the exterior doorway must remain on actual open geometry.");
            for (int i = 0; !_transitions.IsTransitioning && i < 40; i++) StepGameplayUpdates(1, Vector2.Up);
            FailIf(!_transitions.IsTransitioning, "The unlocked Crown doorway must accept a repeated normal approach.");
            for (int i = 0; _rooms.ActiveGroup == 0 && i < 160; i++) StepGameplayUpdates(1, Vector2.Zero);
            FailIf(_rooms.ActiveGroup != 4 || _rooms.CurrentRoom.Id != 0xbb || _player.Position.X != 120,
                "Crown's doorway fallback must enter room4:bb at the source center X=$78.");
            for (int i = 0; _transitions.IsTransitioning && i < 160; i++) StepGameplayUpdates(1, Vector2.Zero);
            FailIf(_transitions.IsTransitioning || !_saveData.HasRoomFlag(0, 0x0a, 0x80),
                "Crown re-entry must finish while retaining the unlocked exterior entrance.");
            FailIf(_runtimeState.ReadWramByte(OracleRuntimeState.ToggleBlocksStateAddress) != 0 ||
                _runtimeState.ReadWramByte(WramAddress.wLastToggleBlocksState) != 0 ||
                _entities.FloorToggle!.Active,
                "Dungeon initialization must synchronize its reset toggle byte before live orb checks resume.");
            _dialogue.Close();
            int sounds = _sound.PlayRequestsFor(SoundId.SndDoorClose);
            StepGameplayUpdates(10, Vector2.Zero);
            FailIf(_entities.FloorToggle.Active || _sound.PlayRequestsFor(SoundId.SndDoorClose) != sounds,
                "Crown re-entry must not turn its initialization reset into a delayed floor-toggle cutscene.");
            LoadValidationRoom(0, 0x60);
        }
    }
}
