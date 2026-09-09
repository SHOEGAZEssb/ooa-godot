using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoomTransitionSounds()
    {
        // link.s warpTransition2/3/4 request $6e only on the source side.
        foreach (int source in new[] { 0, 2, 3, 4 })
        foreach (int destination in new[] { 0, 1, 3, 4, 0x0e })
        {
            LoadValidationRoom(0, 0x11);
            _sound.ClearPlayRequestAudit();
            var warp = new Warp(0, 0x11, -1, 0, source, 0, 0x12, 0xff, 0, destination);
            _transitions.ApplyWarp(_player, warp);
            int expected = source == 0 ? 0 : 1;
            FailIf(_sound.PlayRequestsFor(OracleSoundEngine.SndEnterCave) != expected,
                $"Source transition ${source:x2} did not request SND_ENTERCAVE $6e on initialization.");
            _transitions.ApplyWarp(_player, warp);
            FailIf(_transitions.CheckTileWarp(_player), "An active warp accepted a second tile-warp trigger.");
            _transitions.UpdateWarp(180.0 / 60.0);
            FailIf(_transitions.IsTransitioning ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndEnterCave) != expected,
                $"Destination ${destination:x2} replayed source ${source:x2}'s sound or the warp did not finish.");
        }

        // Scripted wWarpTransition2 writes bypass Link's source handler.
        foreach (int mode in new[] { 0, 1, 2 })
        {
            LoadValidationRoom(0, 0x11);
            _sound.ClearPlayRequestAudit();
            var warp = new Warp(0, 0x11, -1, 0, 2, 0, 0x12, 0x44, 0, 0,
                DirectFadeOut: mode == 0);
            if (mode == 0) _transitions.ApplyWarp(_player, warp);
            else if (mode == 1) _transitions.ApplyWarpWithFadeOut(_player, warp);
            else _transitions.ApplyWarpWithDelayedFadeOut(_player, warp);
            _transitions.UpdateWarp(180.0 / 60.0);
            FailIf(_transitions.IsTransitioning || _sound.PlayRequestsFor(OracleSoundEngine.SndEnterCave) != 0,
                "A direct cutscene fade ran Link's audible source-transition handler.");
        }

        // Unlisted dungeon stairs own their sound in findWarpSourceAndDest.
        LoadValidationRoom(4, 0x35);
        _player.WarpTo(new Vector2(0x38, 0x28), recordSafe: false);
        _sound.ClearPlayRequestAudit();
        FailIf(!_transitions.CheckTileWarp(_player) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndEnterCave) != 1,
            "Room 4:35/$23 did not play its staircase sound at acceptance.");
        FailIf(_transitions.CheckTileWarp(_player), "Dungeon stairs retriggered during their fade.");
        _transitions.UpdateWarp(120.0 / 60.0);
        FailIf(_currentRoom.Id != 0x2d || _transitions.CheckTileWarp(_player) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndEnterCave) != 1,
            "Dungeon stairs doubled $6e or retriggered at the destination.");

        LoadValidationRoom(0, 0x11);
        _sound.ClearPlayRequestAudit();
        _transitions.BeginScroll(_player, Vector2I.Right, 0x12);
        FinishActiveScrollingTransitionForValidation();
        FailIf(_sound.PlayRequestsFor(OracleSoundEngine.SndEnterCave) != 0,
            "An ordinary room scroll acquired a cave-entry sound.");

        LoadValidationRoom(4, 0x34);
        _sound.ClearPlayRequestAudit();
        _transitions.ApplyDungeonHoleWarp(_player, 0x44);
        _transitions.UpdateWarp(180.0 / 60.0);
        FailIf(_sound.PlayRequestsFor(OracleSoundEngine.SndEnterCave) != 0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSplash) != 1,
            "initiateFallDownHoleWarp added $6e or lost the arrival collapse sound.");
    }
}
