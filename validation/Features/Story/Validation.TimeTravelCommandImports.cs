using System;

namespace oracleofages;

public partial class ValidationRoot
{
    private static void ValidateTimeTravelCommandImports()
    {
        // scripts/ages/scripts.s: villagerSubid0dScript, ralphSubid0dScript;
        // jumpAndWaitUntilLanded and beginJump supply the native jump operands.
        // These expectations come from the source, not a second generated table.
        if (new EnterPastEventDatabase().Commands is not
            [CutsceneSetDisabledObjectsCommand { Value: 0x11 },
             CutsceneWaitCommand { Frames: 100 }, CutsceneDisableInputCommand,
             CutsceneWaitCommand { Frames: 40 },
             CutsceneJumpCommand { Actor: "Villager", InitialSpeedZ: -0x200, Gravity: 0x30, Sound: 0x53 },
             CutsceneWaitCommand { Frames: 30 }, CutsceneShowTextCommand { TextId: 0x1622 },
             CutsceneWaitCommand { Frames: 30 }, CutsceneSetSpeedCommand { Speed: 0x28 },
             CutsceneMoveCommand { Angle: 0x10, Counter: 0x11 },
             CutsceneMoveCommand { Angle: 0x08, Counter: 0x11 },
             CutsceneMoveCommand { Angle: 0x10, Counter: 0x09 },
             CutsceneSetSpeedCommand { Speed: 0x14 }, CutsceneApplySpeedCommand { Counter: 0x21 },
             CutsceneSetSpeedCommand { Speed: 0x28 }, CutsceneApplySpeedCommand { Counter: 0x39 },
             CutsceneSetGlobalFlagCommand { Flag: 0x41 }, CutsceneEnableInputCommand, CutsceneEndCommand])
            throw new InvalidOperationException("villagerSubid0dScript differs from its source-derived command boundaries and operands.");

        if (new RalphPortalEventDatabase().Commands is not
            [CutsceneDisableInputCommand, CutsceneWaitCommand { Frames: 40 },
             CutsceneShowTextCommand { TextId: 0x2a1e }, CutsceneWaitCommand { Frames: 30 },
             CutsceneSetAnimationCommand { Actor: "Ralph", Animation: 0x01 },
             CutsceneSetSpeedCommand { Actor: "Ralph", Speed: 0x28 },
             CutsceneSetAngleCommand { Actor: "Ralph", Angle: 0x08 },
             CutsceneApplySpeedCommand { Actor: "Ralph", Counter: 0x11 },
             CutsceneSetAnimationCommand { Actor: "Ralph", Animation: 0x09 },
             CutsceneWriteObjectByteCommand { Actor: "Ralph", Address: 0x3f, Value: 0x2d },
             CutscenePlaySoundCommand { Sound: 0x7b },
             CutsceneFlickerCommand { Actor: "Ralph", CounterAddress: 0x3f, FrameMask: 0x01 },
             CutsceneSetGlobalFlagCommand { Flag: 0x40 },
             CutsceneNativeCommand { Handler: "ralph_restoreMusic" },
             CutsceneEnableInputCommand, CutsceneEndCommand])
            throw new InvalidOperationException("ralphSubid0dScript differs from its source-derived command boundaries and operands.");
    }
}
