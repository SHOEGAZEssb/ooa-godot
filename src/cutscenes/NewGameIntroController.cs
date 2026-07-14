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

    public NewGameIntroController(NewGameIntroScreen screen, Action complete)
    {
        _screen = screen;
        _record = new NewGameIntroDatabase().Record;
        _complete = complete;
        UpdateScreen();
    }

    public void Update(double delta)
    {
        if (CurrentStage == Stage.Complete)
            return;

        if (CurrentStage == Stage.Dialogue)
        {
            if (!_screen.Dialogue.IsOpen)
            {
                AdvanceDialogueClock(delta);
                BeginStage(Stage.Vanishing);
                return;
            }
            AdvanceDialogueClock(delta);
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
        _stageFrame++;
        switch (CurrentStage)
        {
            case Stage.WaitingForVoice:
                if (_stageFrame >= TotalVoiceWaitFrames)
                {
                    CurrentStage = Stage.Dialogue;
                    _stageFrame = 0;
                    _screen.ShowDialogue();
                }
                break;
            case Stage.Vanishing:
                if (_stageFrame >= TotalVanishFrames)
                    BeginStage(Stage.PostVanish);
                break;
            case Stage.PostVanish:
                if (_stageFrame >= _record.PostVanishWaitFrames)
                {
                    CurrentStage = Stage.Complete;
                    _complete();
                    return;
                }
                break;
        }
        UpdateScreen();
    }

    private void BeginStage(Stage stage)
    {
        CurrentStage = stage;
        _stageFrame = 0;
        UpdateScreen();
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
        UpdateScreen();
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
