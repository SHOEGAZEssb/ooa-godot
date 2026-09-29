using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private int _tasMovieFrame;
    private long _tasPresentationClock;

    private void AdvanceTasPresentation(JsonElement command)
    {
        int frame = command.GetProperty("movieFrame").GetInt32();
        int buttons = command.GetProperty("input").GetInt32();
        long clock = command.GetProperty("cpuClocks").GetInt64();
        if (frame != _tasMovieFrame || buttons is < 0 or > 255 || clock <= _tasPresentationClock)
            throw new InvalidOperationException("Invalid TAS presentation frame/input/clock sequence.");
        // Observation time and physical buttons only. Never copy a reference
        // page, palette, cursor, RAM value or loading completion into the port.
        if (clock >= _originalTiming!.Clocks)
        {
            var map = new (int Bit, string Action)[] { (1,"attack"), (2,"item"), (4,"map"),
                (8,"inventory"), (16,"move_right"), (32,"move_left"), (64,"move_up"), (128,"move_down") };
            Vector2 movement = new(((buttons & 16) != 0 ? 1 : 0) - ((buttons & 32) != 0 ? 1 : 0),
                ((buttons & 128) != 0 ? 1 : 0) - ((buttons & 64) != 0 ? 1 : 0));
            TasRootField<ApplicationInputBuffer>("_applicationInput")!.CaptureForValidation(
                map.Where(x => (buttons & x.Bit) != 0).Select(x => x.Action).ToArray(), [], movement);
            long previous = Math.Max(_tasPresentationClock, 1_171_344);
            AdvanceTimedApplication((clock - previous) / (double)OracleExecutionClock.CpuClocksPerSecond);
            AdvanceGameplayPreparation();
        }
        GD.Print("TAS_PRESENTATION " + JsonSerializer.Serialize(new {
            movieFrame = frame, input = buttons, cpuClocks = clock,
            state = CaptureTasPresentationState(this), diagnostics = new {
                actualCpuClocks = _originalTiming.Clocks,
                update = _originalTiming.CompletedUpdates,
                logicalMenu = _mainMenu?.CurrentPage.ToString(),
                displayedMenu = _mainMenuScreen?.CurrentPage.ToString(),
                busy = _originalTiming.Busy
            }
        }));
        _tasPresentationClock = clock;
        _tasMovieFrame++;
    }

    private static Dictionary<string, int> CaptureTasPresentationState(Node root)
    {
        var state = new Dictionary<string, int> { ["presentation.menu.visible"] = 0 };
        // Read the screen used by _Draw, never the controller's loading page.
        // The outgoing node can outlive its logical owner until publication.
        MainMenuScreen? screen = root.GetChildren().OfType<MainMenuScreen>()
            .LastOrDefault(node => node.IsVisibleInTree() && !node.IsQueuedForDeletion());
        if (screen is null || screen.DisplayBlank || screen.WhiteFadeOffset >= 28 || screen.CurrentPage == Page.Title)
            return state;
        int page = screen.CurrentPage switch {
            Page.FileSelect or Page.TextSpeed or Page.EraseSelect or Page.EraseConfirm => 1,
            Page.NewFileOptions => 2, Page.NameEntry => 3,
            Page.CopySource or Page.CopyDestination or Page.CopyConfirm => 4,
            Page.SecretEntry => 5,
            _ => throw new NotSupportedException($"Unmapped displayed file-menu page {screen.CurrentPage}.")
        };
        byte[] gba = OracleGraphicsData.ReadBytes("res://assets/oracle/metadata/gba_palette_components.bin", 32);
        state["presentation.menu.visible"] = 1;
        state["presentation.menu.screen"] = page;
        state["presentation.menu.overlay"] = screen.CurrentPage == Page.TextSpeed ? 1 : 0;
        state["presentation.menu.blackLevelRgb5"] = gba[screen.WhiteFadeOffset];
        if (page is 1 or 2 or 4)
        {
            var cursors = new List<Vector2I>();
            if (screen.CurrentPage is Page.CopyConfirm or Page.EraseConfirm)
            {
                cursors.Add(new(screen.CurrentPage == Page.CopyConfirm ? 80 : 8, 52 + screen.SelectedSlot * 24));
                cursors.Add(new(screen.Choice == 0 ? 34 : 90, 122));
            }
            else if (page == 2) cursors.Add(new(32, 56 + screen.Cursor * 24));
            else if (screen.CurrentPage == Page.TextSpeed) cursors.Add(new(8, 52 + screen.SelectedSlot * 24));
            else cursors.Add(screen.Cursor < 3
                ? new(screen.CurrentPage == Page.CopyDestination ? 80 : 8, 52 + screen.Cursor * 24)
                : new(screen.Choice == 0 ? 34 : 90, 122));
            state["presentation.cursor.acorn.count"] = cursors.Count;
            for (int i = 0; i < cursors.Count; i++)
            {
                state[$"presentation.cursor.acorn.{i}.x"] = cursors[i].X;
                state[$"presentation.cursor.acorn.{i}.y"] = cursors[i].Y;
            }
        }
        if (page == 1)
        {
            bool speed = screen.TextSpeedCursorVisible;
            state["presentation.cursor.speed.visible"] = speed ? 1 : 0;
            if (speed)
            {
                Vector2 point = MainMenuScreen.TextSpeedCursorPositionForValidation(screen.TextSpeed);
                state["presentation.cursor.speed.x"] = (int)point.X;
                state["presentation.cursor.speed.y"] = (int)point.Y;
            }
        }
        return state;
    }
}
