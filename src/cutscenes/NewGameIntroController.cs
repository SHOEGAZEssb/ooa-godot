using System;
using System.Linq;

namespace oracleofages;

public sealed class NewGameIntroController
{
    // fadeinFromWhiteWithDelay(2) reaches the visible palette on update 63;
    // its palette thread reports completion on update 65.
    internal const int ArrivalFadeWaitFrames = 65;

    internal enum Stage
    {
        WaitingForVoice,
        Dialogue,
        Vanishing,
        PostVanish,
        Complete
    }

    private readonly NewGameIntroScreen _screen;
    private readonly NewGameIntroDatabase.NewGameIntroRecord _record;
    private readonly Action _complete;
    private readonly OracleSoundEngine _sound;
    private readonly RoomEventTimeline _timeline = new();
    private double _tickAccumulator;
    private int _stageFrame;
    private int _clock;
    private int _motionClock;

    internal Stage CurrentStage { get; private set; } = Stage.WaitingForVoice;
    internal int StageFrame => _stageFrame;
    internal int TotalVoiceWaitFrames => _record.InitialWaitFrames + _record.VoiceWaitFrames;
    // Animation $05 reaches its $ff terminal parameter after the three timed
    // frames, then linkCutsceneB and the cutscene handler each observe that
    // state on the following two updates.
    internal int TotalVanishFrames => _record.VanishDurations[..^1].Sum() + 2;
    internal NewGameIntroDatabase.NewGameIntroRecord Record => _record;

    public NewGameIntroController(
        NewGameIntroScreen screen,
        Action complete,
        OracleSoundEngine sound)
    {
        _screen = screen;
        _record = new NewGameIntroDatabase().Record;
        _complete = complete;
        _sound = sound;
        // Pregame state $0a starts this cue as it creates Link's blue-orb
        // descent presentation.
        _sound.PlaySound(OracleSoundEngine.MusEssenceRoom);
        BuildTimeline();
        UpdateScreen();
    }

    public void Update(double delta)
    {
        if (CurrentStage == Stage.Complete)
            return;

        if (CurrentStage == Stage.Dialogue)
        {
            AdvanceDialogueClock(delta);
            if (!_screen.Dialogue.IsOpen)
                _timeline.AdvanceFrame();
            UpdateScreen();
            return;
        }

        _tickAccumulator += delta * 60.0;
        while (_tickAccumulator >= 1.0 && CurrentStage != Stage.Complete)
        {
            _tickAccumulator -= 1.0;
            AdvanceOneFrame();
        }
    }

    private void AdvanceOneFrame()
    {
        _clock++;
        if (CurrentStage == Stage.WaitingForVoice)
            _motionClock++;
        _timeline.AdvanceFrame();
        if (CurrentStage == Stage.Complete)
            return;
        UpdateScreen();
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
            completed: () => SetStage(Stage.Vanishing));

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
                _sound.PlaySound(OracleSoundEngine.SndCtrlStopMusic);
                CurrentStage = Stage.Complete;
                _complete();
            });
    }

    private void SetStage(Stage stage)
    {
        CurrentStage = stage;
        _stageFrame = 0;
    }

    private void AdvanceDialogueClock(double delta)
    {
        _tickAccumulator += delta * 60.0;
        while (_tickAccumulator >= 1.0)
        {
            _tickAccumulator -= 1.0;
            _clock++;
            _motionClock++;
        }
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
        else if (CurrentStage is Stage.PostVanish or Stage.Complete)
        {
            visible = false;
        }
        _screen.SetAnimation(
            _clock, _motionClock, _stageFrame, vanishing, visible);
    }
}
