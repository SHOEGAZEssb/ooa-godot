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
    private readonly Dictionary<int, (int Frames, double WorkMs)> _profileStages = new();
    private long _profileLastFrame;
    private double _profileLongestFrame;
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
        var setup = Stopwatch.StartNew();
        BeginBootLoading(screen);
        GD.Print($"STARTUP_SETUP ms={setup.Elapsed.TotalMilliseconds:F2}");
        var tasks = (Queue<Func<IEnumerator<bool>?>>)typeof(GameRoot).GetField("_bootTasks", ProfileFields)!.GetValue(this)!;
        var originalTasks = tasks.ToArray();
        tasks.Clear();
        string[] names = ["Sources", "GameplayScene", "GameplayUI", "Frontend", "FileMenu", "NewGameIntro"];
        for (int index = 0; index < originalTasks.Length; index++)
        {
            var prepare = originalTasks[index];
            string name = names[index];
            tasks.Enqueue(() => ProfilePreparation(prepare()!, name).GetEnumerator());
        }
        _startupProfile = Stopwatch.StartNew();
        _profileLastFrame = Stopwatch.GetTimestamp();
    }

    private void AdvanceStartupProfile(double delta)
    {
        try
        {
            long now = Stopwatch.GetTimestamp();
            if (!_profileSelectedFile)
                _profileLongestFrame = Math.Max(_profileLongestFrame,
                    Stopwatch.GetElapsedTime(_profileLastFrame, now).TotalMilliseconds);
            _profileLastFrame = now;
            int stage = (int)typeof(GameRoot).GetField("_bootCompletedTasks", ProfileFields)!.GetValue(this)!;
            var step = Stopwatch.StartNew();
            base._Process(delta);
            double elapsed = step.Elapsed.TotalMilliseconds;
            if (_profileSelectedFile)
                _profileLongestFileStep = Math.Max(_profileLongestFileStep, elapsed);
            else
            {
                _profileLongestStep = Math.Max(_profileLongestStep, elapsed);
                var previous = _profileStages.GetValueOrDefault(stage);
                _profileStages[stage] = (previous.Frames + 1, previous.WorkMs + elapsed);
            }
            if (elapsed > 4)
                GD.Print($"STARTUP_STEP file={_profileSelectedFile} stage={typeof(GameRoot).GetField("_bootCompletedTasks", ProfileFields)!.GetValue(this)} ms={elapsed:F2}");
            if (!_profileSelectedFile && typeof(GameRoot).GetField("_bootLoading", ProfileFields)!.GetValue(this) is null)
            {
                GD.Print($"STARTUP_BOOT elapsed_ms={_startupProfile!.Elapsed.TotalMilliseconds:F2} max_step_ms={_profileLongestStep:F2}");
                GD.Print($"STARTUP_FRAME max_interval_ms={_profileLongestFrame:F2}");
                foreach (var entry in _profileStages)
                    GD.Print($"STARTUP_STAGE stage={entry.Key} frames={entry.Value.Frames} work_ms={entry.Value.WorkMs:F2}");
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
            double workMs = 0;
            while (true)
            {
                var timer = Stopwatch.StartNew();
                bool more = source.MoveNext();
                double elapsed = timer.Elapsed.TotalMilliseconds;
                workMs += elapsed;
                if (elapsed > 2)
                    GD.Print($"STARTUP_{phase}_STEP index={index} ms={elapsed:F2}");
                index++;
                if (!more)
                {
                    GD.Print($"STARTUP_RESOURCE name={phase} steps={index} work_ms={workMs:F2}");
                    yield break;
                }
                yield return source.Current;
            }
        }
    }
}
