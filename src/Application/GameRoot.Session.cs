namespace Oracles;

public partial class GameRoot
{
    internal GameplaySceneGraph _scene = null!;
    internal Godot.Label _roomDebug => _scene.RoomDebug;
    private NewGameIntroController? _newGameIntro;
    private NewGameIntroScreen? _newGameIntroScreen;
    internal StatusBarController _statusBar => _gameplaySession!.StatusBar;
    internal DebugObjectSpawnerController _debugObjectSpawner => _gameplayMenus!.Spawner;
    public bool IsTransitioning => _gameplayMenus?.TransitionActive ?? false;
    public bool DialogueOpen => _gameplaySession is { Active: true } && _scene.Dialogue.IsOpen;

    private void BeginPregame(NewGameIntroScreen screen, System.Action complete)
    {
        _newGameIntroScreen = screen;
        screen.Dialogue.ApplicationUpdateOwned = true;
        screen.Dialogue.MessageSpeed = _saveData.TextSpeed;
        screen.Dialogue.SetLinkNameProvider(() => _saveData.LinkName);
        _newGameIntro = new(screen,() =>
        {
            complete();
            _newGameIntro = null; _newGameIntroScreen = null;
        },_sound,initializing:true,() => _saveData.ReadField("wPlaytimeCounter"));
    }

    private void BindGameplaySession(GameplaySession session, System.Action release, System.Action<OracleSaveData> restore,
        System.Func<bool> canCapture, System.Func<double> animationTicks, System.Action<DebugSavestateData> restoreDebug)
    {
        _gameplaySession?.Dispose();
        _gameplaySession = session;
        _canCaptureDebugState = canCapture; _debugAnimationTicks = animationTicks; _restoreDebugState = restoreDebug;
        _debugSavestateStatus = string.Empty; _debugSavestateStatusFrames = 0;
        BindGameplayMenus(session.Menus,release,restore);
    }

    private void AdvancePregameFrame()
    {
        if (_newGameIntro is { GraphicsLoadPending: true })
        {
            _newGameIntro.CompleteGraphicsLoad();
            return;
        }
        MainThreadClock.Advance(_saveData,_runtimeState);
        Stage previous = _newGameIntro!.CurrentStage;
        _newGameIntro.Update(ApplicationFixedUpdateScheduler.UpdateDelta);
        if (previous == Stage.PostVanish && _newGameIntro?.CurrentStage == Stage.RestartGame)
            _saveData.SetGlobalFlag(_newGameIntro.Record.LinkSummonedFlag);
        _newGameIntroScreen?.Dialogue.AdvanceApplicationUpdate();
    }
}
