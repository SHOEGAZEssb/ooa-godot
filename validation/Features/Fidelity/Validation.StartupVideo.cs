using Godot;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    // Rendered, wall-clock sampling complements update-boundary TAS state
    // comparisons. Never finish a pending load merely to take a screenshot.
    private async void CaptureStartupVideo()
    {
        try
        {
            string Argument(string name) => OS.GetCmdlineUserArgs()
                .Single(value => value.StartsWith(name + "=", StringComparison.Ordinal))[(name.Length + 1)..];
            string output = Path.GetFullPath(Argument("--video-output"));
            byte[] inputs = File.ReadAllBytes(Argument("--video-inputs"));
            using JsonDocument timeline = JsonDocument.Parse(File.ReadAllText(Argument("--video-timeline")));
            int first = int.Parse(Argument("--video-first"));
            int last = int.Parse(Argument("--video-last"));
            if (first < 0 || last < first || last >= inputs.Length / 2 ||
                last >= timeline.RootElement.GetArrayLength())
                throw new ArgumentException("Invalid startup video frame range.");
            Directory.CreateDirectory(output);
            using var metadata = new StreamWriter(Path.Combine(output, "frames.jsonl"));
            var buffer = TasRootField<ApplicationInputBuffer>("_applicationInput")!;
            var actions = new (int Bit, string Action)[] { (1,"attack"), (2,"item"), (4,"map"),
                (8,"inventory"), (16,"move_right"), (32,"move_left"), (64,"move_up"), (128,"move_down") };
            long previousClock = _originalTiming!.Clocks;
            for (int frame = 0; frame <= last; frame++)
            {
                JsonElement row = timeline.RootElement[frame];
                if (row.GetProperty("frame").GetInt32() != frame)
                    throw new InvalidDataException("Video timeline is not in consecutive movie-frame order.");
                // Both pictures are sampled at this recorded transport endpoint.
                // This sets observation time only: audio/CPU work and input
                // polling retain their production owners and can drift freely.
                long clock = row.GetProperty("fields").GetProperty("p_->cpu/cycleCounter_").GetInt64();
                if (clock < previousClock)
                {
                    if (_originalTiming.CompletedUpdates != 0)
                        throw new InvalidDataException("Video clock moved backwards.");
                    continue; // native boot before the supported first _mainLoop
                }
                if (inputs[frame * 2 + 1] != 0)
                    throw new InvalidDataException("Power input is unsupported in startup video capture.");
                int buttons = inputs[frame * 2];
                string[] held = actions.Where(x => (buttons & x.Bit) != 0).Select(x => x.Action).ToArray();
                Vector2 movement = new(((buttons & 16) != 0 ? 1 : 0) - ((buttons & 32) != 0 ? 1 : 0),
                    ((buttons & 128) != 0 ? 1 : 0) - ((buttons & 64) != 0 ? 1 : 0));
                buffer.CaptureForValidation(held, [], movement);
                AdvanceTimedApplication((clock - previousClock) / (double)OracleExecutionClock.CpuClocksPerSecond);
                previousClock = clock;
                AdvanceGameplayPreparation();
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                if (frame < first) continue;
                using Image image = GetViewport().GetTexture().GetImage();
                if (image.GetWidth() != 160 || image.GetHeight() != 144)
                    throw new InvalidOperationException($"Capture requires a 160x144 viewport, got {image.GetSize()}.");
                Error saved = image.SavePng(Path.Combine(output, $"{frame}.png"));
                if (saved != Error.Ok) throw new IOException($"PNG capture failed: {saved}.");
                metadata.WriteLine(JsonSerializer.Serialize(new
                {
                    frame, input = buttons, requestedClock = clock, actualClock = _originalTiming.Clocks,
                    update = _originalTiming.CompletedUpdates, busy = _originalTiming.Busy,
                    frontend = _frontendIntro?.Stage.ToString(), menu = _mainMenu?.CurrentPage.ToString(),
                    displayedMenu = _mainMenuScreen?.CurrentPage.ToString(),
                    whiteFade = _mainMenuScreen?.WhiteFadeOffset,
                    lcdEnabled = _mainMenuScreen?.OriginalLcdEnabled ?? _frontendIntroScreen?.OriginalLcdEnabled
                }));
            }
            GD.Print($"STARTUP_VIDEO_COMPLETE first={first} last={last} frames={last - first + 1}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"Startup video capture failed: {exception}");
            GetTree().Quit(2);
        }
    }
}
