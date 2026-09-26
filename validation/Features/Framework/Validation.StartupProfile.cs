using Godot;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Collections.Generic;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private Stopwatch? _startupProfile;
    private bool _profileSelectedFile;
    private double _profileLongestStep;
    private double _profileLongestFileStep;
    private IEnumerator<bool>? _profileBootIterator;
    private const BindingFlags ProfileFields = BindingFlags.NonPublic | BindingFlags.Instance;

    // Cold-start measurement: deliberately bypass the suite's fully initialized
    // gameplay fixture, which would warm the very resources being measured.
    private void BeginStartupProfile()
    {
        typeof(GameRoot).GetField("_launchOptions", ProfileFields)!.SetValue(this, new LaunchOptions());
        _sound = GetNode<OracleSoundEngine>("SoundEngine");
        _sound.ApplicationUpdateOwned = true;
        _random = new OracleRandom();
        var screen = ResourceLoader.Load<PackedScene>("res://scenes/boot_loading.tscn").Instantiate<BootLoadingScreen>();
        AddChild(screen);
        BeginBootLoading(screen);
        _startupProfile = Stopwatch.StartNew();
    }

    private void AdvanceStartupProfile(double delta)
    {
        try
        {
            FieldInfo bootSteps = typeof(GameRoot).GetField("_bootResourceSteps", ProfileFields)!;
            if (bootSteps.GetValue(this) is IEnumerator<bool> steps && steps != _profileBootIterator)
            {
                _profileBootIterator = ProfilePreparation(steps, "BOOT").GetEnumerator();
                bootSteps.SetValue(this, _profileBootIterator);
            }
            var step = Stopwatch.StartNew();
            base._Process(delta);
            double elapsed = step.Elapsed.TotalMilliseconds;
            if (_profileSelectedFile)
                _profileLongestFileStep = Math.Max(_profileLongestFileStep, elapsed);
            else
                _profileLongestStep = Math.Max(_profileLongestStep, elapsed);
            if (elapsed > 4)
                GD.Print($"STARTUP_STEP file={_profileSelectedFile} stage={typeof(GameRoot).GetField("_bootCompletedTasks", ProfileFields)!.GetValue(this)} ms={elapsed:F2}");
            if (!_profileSelectedFile && typeof(GameRoot).GetField("_bootLoading", ProfileFields)!.GetValue(this) is null)
            {
                GD.Print($"STARTUP_BOOT elapsed_ms={_startupProfile!.Elapsed.TotalMilliseconds:F2} max_step_ms={_profileLongestStep:F2}");
                typeof(GameRoot).GetMethod("OpenFileSelectFromFrontend", ProfileFields)!.Invoke(this, null);
                step.Restart();
                typeof(GameRoot).GetMethod("StartSelectedFile", ProfileFields)!.Invoke(this, [0, OracleSaveData.CreateStandardGame()]);
                GD.Print($"STARTUP_FILE_SELECT ms={step.Elapsed.TotalMilliseconds:F2}");
                FieldInfo preparation = typeof(GameRoot).GetField("_gameplayPreparation", ProfileFields)!;
                preparation.SetValue(this, ProfilePreparation((IEnumerator<bool>)preparation.GetValue(this)!).GetEnumerator());
                _profileSelectedFile = true;
            }
            if (_profileSelectedFile && GameplayPrepared)
            {
                GD.Print($"STARTUP_PROFILE_COMPLETE max_file_step_ms={_profileLongestFileStep:F2}");
                SetProcess(false);
                GetTree().Quit(0);
            }
            if (_startupProfile!.Elapsed.TotalSeconds > 60)
                throw new InvalidOperationException("Startup profile did not finish within 60 seconds.");
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            SetProcess(false);
            GetTree().Quit(1);
        }
    }

    private static IEnumerable<bool> ProfilePreparation(IEnumerator<bool> source, string phase = "GAMEPLAY")
    {
        using (source)
        {
            int index = 0;
            while (true)
            {
                var timer = Stopwatch.StartNew();
                bool more = source.MoveNext();
                if (timer.Elapsed.TotalMilliseconds > 2)
                    GD.Print($"STARTUP_{phase}_STEP index={index} ms={timer.Elapsed.TotalMilliseconds:F2}");
                index++;
                if (!more) yield break;
                yield return source.Current;
            }
        }
    }
}
