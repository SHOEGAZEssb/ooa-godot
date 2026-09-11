using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateNayruGhostImport(NayruIntroEventDatabase database)
    {
        FailIf(database.Commands.Single(command => command.Source.Script == "runVeranGhostSubid0")
                is not CutsceneNativeBlockingCommand { Handler: "GhostInitialRise", Frames: 90, Payload: "0a" },
            "Ghost Veran native substate 1 lost counter1=$5a or SPEED_40=$0a.");
        // scripts/ages/scripts.s:ghostVeranSubid1Script_part2 and the unchanged
        // applyspeed operands; bank0.s decrements counter2 before moving.
        FailIf(database.GhostCommands is not [
            CutsceneSetCoordinatesCommand { Y: 0x24, X: 0x78 },
            CutsceneWaitCommand { Frames: 30 },
            CutsceneSetAngleCommand { Angle: 0 },
            CutsceneSetSpeedCommand { Speed: 0x0a },
            CutsceneApplySpeedCommand { Counter: 0x45 },
            CutsceneMemoryGateCommand { Binding: "NayruPortalSignal", Value: 0xff },
            CutsceneWaitCommand { Frames: 60 },
            CutsceneSetAngleCommand { Angle: 0x10 },
            CutsceneSetSpeedCommand { Speed: 0x14 },
            CutsceneApplySpeedCommand { Counter: 0x23 },
            CutsceneWaitCommand { Frames: 10 },
            CutsceneWriteMemoryCommand { Binding: "NayruPhase", Value: 0x1a },
            CutsceneEndCommand] || database.GhostCommands.Any(command =>
                command.Source.Label != "ghostVeranSubid1Script_part2" ||
                command.Source.SourceLine < 3153),
            "Ghost Veran $3e:$00 substate 8 lost its source operands or instruction locations.");
    }

    private void CheckNayruGhostUpdate(NayruGhostScriptHost ghost,
        CutsceneCommand? command, int counter, int commandUpdates, Vector2 position,
        ref int upwardMoves, ref int downwardMoves)
    {
        if (command is null)
            return; // Native substate 7 may install the lane during this update.
        Vector2 expected = position;
        if (command is CutsceneApplySpeedCommand && commandUpdates > 0 && counter > 1)
        {
            if (command.Source.CommandIndex == 4)
            {
                expected += Vector2.Up * 0.25f;
                upwardMoves++;
            }
            else
            {
                expected += Vector2.Down * 0.5f;
                downwardMoves++;
            }
        }
        FailIf(ghost.PrecisePosition != expected,
            $"Ghost Veran {command.Source} moved {position} -> {ghost.PrecisePosition}; " +
            $"expected {expected} with counter ${counter:x2}.");
    }
}
