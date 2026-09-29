using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    // The pinned core starts after the boot ROM. Its clean-US cold-start
    // CPU/sample-clock origin is $ffbc, including the initial speed switch.
    // Independently observed endpoints: frame 0 = 205916, frame 1 = 346364;
    // later native instructions may overrun a transport endpoint by a few
    // clocks. Do not feed those overruns or ROM update times into Godot.
    private const long TasMovieClockOrigin = 65_468;
    private readonly List<object> _tasMovieCompleted = [];
    private bool _tasMovieStarted;
    private int _tasMovieSampledButtons;
    private int _tasMovieSampledEdges;
    private long _tasMovieDeadline;
    private static readonly (int Bit, string Action)[] TasMovieButtons =
    [
        (1, "attack"), (2, "item"), (4, "map"), (8, "inventory"),
        (16, "move_right"), (32, "move_left"), (64, "move_up"), (128, "move_down")
    ];

    private void AdvanceTasMovie(JsonElement command)
    {
        int frame = command.GetProperty("movieFrame").GetInt32();
        int buttons = command.GetProperty("input").GetInt32();
        if (frame != _tasMovieFrame || buttons is < 0 or > 255 ||
            command.EnumerateObject().Count() != 2)
            throw new InvalidOperationException("Movie playback accepts consecutive frames and physical buttons only.");
        _tasMovieCompleted.Clear();
        long deadline = TasMovieClockOrigin + ((long)frame + 1) * OracleExecutionClock.LcdFrameClocks;
        ApplicationInputBuffer buffer = TasRootField<ApplicationInputBuffer>("_applicationInput")!;
        buffer.CaptureForValidation(TasMovieButtons.Where(x => (buttons & x.Bit) != 0).Select(x => x.Action), [],
            new Vector2(((buttons & 16) != 0 ? 1 : 0) - ((buttons & 32) != 0 ? 1 : 0),
                ((buttons & 128) != 0 ? 1 : 0) - ((buttons & 64) != 0 ? 1 : 0)));
        if (!_tasMovieStarted && deadline >= _originalTiming!.Clocks)
        {
            _tasMovieStarted = true;
            _tasMovieDeadline = _originalTiming.Clocks;
            CaptureCompletedTasMovieUpdate(); // snapshot 0 precedes the first pollInput
        }
        if (_tasMovieStarted)
        {
            AdvanceGameplayPreparation();
            AdvanceTimedApplication((deadline - _tasMovieDeadline) / (double)OracleExecutionClock.CpuClocksPerSecond);
            AdvanceGameplayPreparation();
            _tasMovieDeadline = deadline;
        }
        GD.Print("TAS_MOVIE " + JsonSerializer.Serialize(new {
            movieFrame = frame, input = buttons, updates = _tasMovieCompleted,
            state = CaptureTasPresentationState(this), diagnostics = new {
                deadline, actualCpuClocks = _originalTiming!.Clocks,
                completedUpdates = _tasMovieStarted ? _originalTiming.CompletedUpdates : -1,
                busy = _originalTiming.Busy
            }
        }));
        _tasMovieFrame++;
    }

    private void CaptureCompletedTasMovieUpdate()
    {
        _tasMovieCompleted.Add(new {
            update = _originalTiming!.CompletedUpdates, input = _tasMovieSampledButtons, pressed = _tasMovieSampledEdges,
            state = CaptureTasSharedState(), diagnostics = new {
                movieFrame = _tasMovieFrame, cpuClocks = _originalTiming.Clocks,
                timerTicks = _originalTiming.TimerTicks,
                foregroundWork = TasRootField<long>("_timedCpuWork")
            }
        });
    }

    private void CaptureTasMoviePoll(ApplicationInputSnapshot snapshot)
    {
        // Observe the actual snapshot delivered to gameplay. Reconstructing
        // pressed edges here would hide a bug in the application's poller.
        _tasMovieSampledButtons = TasMovieButtons.Where(x => snapshot.IsPressed(x.Action)).Sum(x => x.Bit);
        _tasMovieSampledEdges = TasMovieButtons.Where(x => snapshot.IsJustPressed(x.Action)).Sum(x => x.Bit);
    }
}
