using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePirateShipCourse()
    {
        var course = new PirateShipCourse();
        var save = OracleSaveData.CreateStandardGame();
        var runtime = new OracleRuntimeState();
        void Set(int room, int y, int x, int direction)
        {
            save.WriteWramByte(0xc6ec, (byte)room);
            save.WriteWramByte(0xc6ed, (byte)y);
            save.WriteWramByte(0xc6ee, (byte)x);
            save.WriteWramByte(0xc6ef, (byte)direction);
        }
        // Source shipDirectionsPast $b6:$34 turns down, but its linked table
        // only turns at $b6:$47. Text pauses displacement, not the turn signal.
        Set(0xb6, 0x38, 0x48, 0);
        course.Update(save, runtime, textActive: true, playingInstrument: false);
        FailIf(save.ReadWramByte(0xc6ef) != 2 || save.ReadWramByte(0xc6ed) != 0x38 ||
            runtime.ReadWramByte(0xcde1) != 0, "Past pirate route lost its paused turn/signal consumption.");
        save.WriteWramByte(WramAddress.wFileIsLinkedGame, 1);
        Set(0xb6, 0x38, 0x48, 0);
        course.Update(save, runtime, textActive: false, playingInstrument: true);
        FailIf(save.ReadWramByte(0xc6ef) != 0 || save.ReadWramByte(0xc6ed) != 0x38,
            "Linked pirate course used past turns or moved while playing an instrument.");
        Set(0xb6, 0x48, 0x78, 0);
        course.Update(save, runtime, textActive: false, playingInstrument: false);
        FailIf(save.ReadWramByte(0xc6ef) != 2 || save.ReadWramByte(0xc6ed) != 0x49,
            "Present pirate route did not turn before applying its speed.");
        // Source updatePirateShipRoom wraps each coordinate at its original
        // asymmetric offscreen boundary, preserving byte room arithmetic.
        foreach (var edge in new[] {
            (Dir:0,Y:0xf9,X:0x40,Room:0x05,NextY:0x80,NextX:0x40,NextRoom:0xf5),
            (Dir:1,Y:0x40,X:0x97,Room:0xff,NextY:0x40,NextX:0,NextRoom:0),
            (Dir:2,Y:0x87,X:0x40,Room:0xf5,NextY:0,NextX:0x40,NextRoom:0x05),
            (Dir:3,Y:0x40,X:0xf9,Room:0,NextY:0x40,NextX:0xa0,NextRoom:0xff) })
        {
            Set(edge.Room, edge.Y, edge.X, edge.Dir);
            course.Update(save, runtime, false, false);
            FailIf(save.ReadWramByte(0xc6ec) != edge.NextRoom || save.ReadWramByte(0xc6ed) != edge.NextY ||
                save.ReadWramByte(0xc6ee) != edge.NextX, $"Pirate ship direction ${edge.Dir:x2} lost its room boundary.");
        }
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadDebugRoom(0, 0x47);
            _saveData.WriteWramByte(0xc622, 0);
            StepGameplayUpdates(4, Vector2.Zero, batched: batched);
            FailIf(_saveData.ReadWramByte(0xc6ed) != 0x4a,
                "Offscreen ship did not move on even main-thread updates through the gameplay loop.");
            _dialogue.ShowMessage("Paused ship", _player.Position.Y);
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            FailIf(_saveData.ReadWramByte(0xc6ed) != 0x4a, "Dialogue failed to pause ship displacement.");
            _dialogue.Close();
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(_saveData.ReadWramByte(0xc6ed) != 0x4b, "Closing text lost the shared even-frame ship phase.");
            _saveData.SetGlobalFlag(GlobalFlag.PiratesGone);
            StepGameplayUpdates(4, Vector2.Zero, batched: batched);
            FailIf(_saveData.ReadWramByte(0xc6ed) != 0x4b, "GLOBALFLAG_PIRATES_GONE failed to stop the course.");
            _saveData.SetGlobalFlag(GlobalFlag.PiratesGone, false);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(_saveData.ReadWramByte(0xc6ed) != 0x4c, "Clearing the stop flag did not resume the live ship position.");
        }
        GD.Print("Validated offscreen pirate course, era turns, four room boundaries, text/instrument gates and batched updates.");
    }
}
