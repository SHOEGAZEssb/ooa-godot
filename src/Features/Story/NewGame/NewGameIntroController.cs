using System;
using System.Linq;

namespace oracleofages;

public sealed class NewGameIntroController
{
    // fadeinFromWhiteWithDelay(2) reaches the visible palette on update 63;
    // its palette thread reports completion on update 65.
    internal const int ArrivalFadeWaitFrames = 65;

    private readonly NewGameIntroScreen _screen;
    private readonly NewGameIntroRecord _record;
    private readonly Action _complete;
    private readonly OracleSoundEngine _sound;
    private readonly Func<int>? _frameCounter;
    private readonly RoomEventTimeline _timeline = new();
    private double _tickAccumulator;
    private int _stageFrame;
    private int _clock;
    private int _linkZ;
    private int _spinClock;
    private int _orbClock;

    internal Stage CurrentStage { get; private set; } = Stage.WaitingForVoice;
    internal int StageFrame => _stageFrame;
    internal bool GraphicsLoadPending { get; private set; }
    internal int TotalVoiceWaitFrames => _record.InitialWaitFrames + _record.VoiceWaitFrames;
    // Animation $05 reaches its $ff terminal parameter after the three timed
    // frames, then linkCutsceneB and the cutscene handler each observe that
    // state on the following two updates.
    internal int TotalVanishFrames => _record.VanishDurations[..^1].Sum() + 2;
    internal NewGameIntroRecord Record => _record;

    public NewGameIntroController(
        NewGameIntroScreen screen,
        Action complete,
        OracleSoundEngine sound,
        bool initializing = false,
        Func<int>? frameCounter = null)
    {
        _screen = screen;
        _record = screen.Record;
        _complete = complete;
        _sound = sound;
        _frameCounter = frameCounter;
        _screen.Dialogue.SetSoundPlayer(_sound.PlaySound);
        // Pregame state $0a starts this cue as it creates Link's blue-orb
        // descent presentation.
        _sound.PlaySound(SoundId.MusEssenceRoom);
        BuildTimeline();
        if (initializing)
        {
            // linkCutsceneB falls through state 0 into substate 0 once before
            // the sparkle's loadObjectGfx suspends the main thread.
            _timeline.AdvanceFrame();
            // initializeGame clears wFrameCounter before linkCutsceneB's
            // state-0 fallthrough. Its first oscillator/animation call runs
            // at $00 even when the saved playtime has a different phase.
            AdvanceMotion();
            _spinClock = 1;
            GraphicsLoadPending = true;
        }
        UpdateScreen();
    }

    internal void CompleteGraphicsLoad() => GraphicsLoadPending = false;

    public void Update(double delta)
    {
        if (CurrentStage == Stage.Complete)
            return;

        _tickAccumulator += delta * 60.0;
        while (_tickAccumulator >= 1.0 && CurrentStage != Stage.Complete)
        {
            _tickAccumulator -= 1.0;
            AdvanceOneFrame();
        }
    }

    private void AdvanceOneFrame()
    {
        if (CurrentStage == Stage.RestartGame)
        {
            CurrentStage = Stage.LoadingArrival;
            return;
        }
        if (CurrentStage == Stage.LoadingArrival)
        {
            CurrentStage = Stage.Complete;
            _complete();
            return;
        }
        _clock = _frameCounter?.Invoke() ?? _clock + 1;
        if (CurrentStage is Stage.WaitingForVoice or Stage.Dialogue)
        {
            AdvanceMotion();
            _spinClock++;
            _orbClock++;
        }
        _timeline.AdvanceFrame();
        if (CurrentStage == Stage.Complete)
            return;
        UpdateScreen();
    }

    private void AdvanceMotion()
    {
        // linkCutscene_oscillateZ uses wFrameCounter's byte phase, selecting
        // the descent table until text opens and the hover table afterward.
        if ((_clock & 7) != 0) return;
        int[] deltas = CurrentStage == Stage.WaitingForVoice
            ? _record.DescendOscillation : _record.HoverOscillation;
        _linkZ = (_linkZ + deltas[(_clock & 0x38) >> 3]) & 0xff;
    }

    private void BuildTimeline()
    {
        int voiceWaitFrames = TotalVoiceWaitFrames;
        _timeline.Wait(
            voiceWaitFrames,
            counterChanged: remaining =>
                _stageFrame = voiceWaitFrames - remaining,
            elapsed: () =>
            {
                SetStage(Stage.Dialogue);
                _screen.ShowDialogue();
            });
        _timeline.WaitUntil(
            () => !_screen.Dialogue.IsOpen,
            completed: BeginVanishing);

        int vanishFrames = TotalVanishFrames;
        _timeline.Wait(
            vanishFrames,
            counterChanged: remaining =>
                _stageFrame = vanishFrames - remaining,
            elapsed: () => SetStage(Stage.PostVanish));
        _timeline.Wait(
            _record.PostVanishWaitFrames,
            counterChanged: remaining =>
                _stageFrame = _record.PostVanishWaitFrames - remaining,
            elapsed: () =>
            {
                // State $0c stops the cue after the post-vanish $3c hold,
                // before handing off to the silent playable arrival.
                _sound.PlaySound(SoundId.SndCtrlStopMusic);
                CurrentStage = Stage.RestartGame;
            });
    }

    private void SetStage(Stage stage)
    {
        CurrentStage = stage;
        _stageFrame = 0;
    }

    private void BeginVanishing()
    {
        // linkCutsceneB substate 2 requests SND_FAIRYCUTSCENE on the update
        // TX_1213 closes, immediately before creating the glowing orb.
        _sound.PlaySound(SoundId.SndFairyCutscene);
        SetStage(Stage.Vanishing);
    }

    private void UpdateScreen()
    {
        bool vanishing = CurrentStage == Stage.Vanishing;
        bool visible = true;
        if (CurrentStage == Stage.Vanishing)
        {
            int terminalFrame = _record.VanishDurations[..^1].Sum();
            visible = _stageFrame == 0 ||
                (((_stageFrame > terminalFrame ? _clock - 1 : _clock) & 1) != 0);
        }
        else if (CurrentStage is Stage.PostVanish or Stage.RestartGame or Stage.LoadingArrival or Stage.Complete)
        {
            visible = false;
        }
        _screen.SetAnimation(
            _clock, _linkZ, _spinClock, _orbClock, _stageFrame, vanishing, visible);
    }
}

internal enum Stage
{
    WaitingForVoice,
    Dialogue,
    Vanishing,
    PostVanish,
    RestartGame,
    LoadingArrival,
    Complete
}
