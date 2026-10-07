using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateDungeonSpinner()
    {
        const int crystalsFlag = 0x0f;
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
        _saveData.SetGlobalFlag(crystalsFlag);
        LoadValidationRoom(4, 0x60);
        Vector2 spinnerCenter = new(0x78, 0x58);
        FailIf(
            _currentRoom.GetMetatile(spinnerCenter) != 0xf1,
            "Room 4:60 did not remove its spinner and reveal the closed " +
            "Gasha Seed chest after the D3 crystals broke.");
        CompareSpinnerGameplayRom();
        CompareSpinnerInitializationRom();
        ReinitializeGameplayForValidation();
    }
}
