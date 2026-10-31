using Godot;
using System;

namespace Oracles;

public partial class GameRoot
{
    internal SeasonsOpeningSession? _seasonsOpening;
    private SeasonsOpeningSession? _preparedSeasonsOpening;
    private void StartSeasonsFromGameSelect()
    {
        GameSelectScreen screen = _gameSelect!;
        screen.AgesSelected -= StartAgesFromGameSelect;
        screen.SeasonsSelected -= StartSeasonsFromGameSelect;
        screen.End(); _gameSelect = null; _applicationInput.Clear();
        _sound.SelectContent(new OracleSoundData(SeasonsGameContent.AssetRoot));
        BeginBootLoading(GetNode<BootLoadingScreen>("%BootLoading"), OracleGame.Seasons);
    }
    private void OpenSeasonsMainMenu()
    {
        _mainMenu = new MainMenuController(_mainMenuScreen!, StartSelectedSeasonsFile,
            load: LoadFileSlot, save: StoreFileSlot, erase: EraseFileSlot,
            playSound: _sound.PlaySound, advanceTitleRandom: () => _random.Next());
    }
    private void StartSelectedSeasonsFile(int slot, OracleSaveData save)
    {
        _activeSaveSlot = slot; _saveData = save;
        save.ResetHealthIfDepleted(); save.AdvancePlaytime();
        _sound.RestartSound(); _applicationInput.Clear();
        _mainMenu = null; _mainMenuScreen?.QueueFree(); _mainMenuScreen = null;
        StartSeasonsGameplay(save);
    }

    private void StartSeasonsGameplay(OracleSaveData save, DebugSavestateData? debugSavestate = null)
    {
        _seasonsOpening = _preparedSeasonsOpening ?? GD.Load<PackedScene>("res://scenes/seasons/opening.tscn").Instantiate<SeasonsOpeningSession>();
        _preparedSeasonsOpening = null;
        SeasonsOpeningSession session = _seasonsOpening;
        AddChild(session);
        session.Start(_sound,_random,save,new GameplayMenuBindings(
            () => session.MenuOpeningAvailable, () => session.DebugOpeningAvailable,
            session.DebugTravel,
            SaveActiveFile, ReturnSeasonsToTitle, RestartGameplayAfterDeath,
            () => session.DebugTravel(session.Rooms.ActiveGroup,session.RoomId),
            () => session.DebugOpeningAvailable && session.MenuOpeningAvailable && session.Phase is
                (SeasonsOpeningPhase.Forest or SeasonsOpeningPhase.Troupe or SeasonsOpeningPhase.Aftermath or SeasonsOpeningPhase.Complete)),
            () => _presentationSettings.HudBottom,BeginPregame,debugSavestate);
        _runtimeState = session.Memory; _scene = session;
        BindGameplaySession(session.Engine!,ReleaseSeasonsGameplay,save => StartSeasonsGameplay(save),
            () => session.CanCaptureDebugState,() => session.AnimationTicks,RestoreSeasonsDebugSavestate);
    }

    private void RestoreSeasonsDebugSavestate(DebugSavestateData state)
    {
        OracleSaveData save = state.CreateSaveData();
        if (!GameplayInitialization.PregameComplete(save) || !save.HasGlobalFlag(GlobalFlag.IntroDone) ||
            state.AnimationTicks > int.MaxValue || state.AnimationTicks != Math.Truncate(state.AnimationTicks))
            throw new ArgumentException("Seasons debug states require a completed opening and a native room-animation counter.",nameof(state));
        ReleaseSeasonsGameplay();
        _sound.RestartSound();
        _random = new OracleRandom();
        _saveData = save;
        StartSeasonsGameplay(save,state);
    }

    private void ReleaseSeasonsGameplay()
    {
        _newGameIntro = null; _newGameIntroScreen = null;
        _gameplaySession?.Dispose();
        _gameplaySession = null;
        if (_seasonsOpening is not null)
        {
            _seasonsOpening.ReleaseRuntime();
            _seasonsOpening.QueueFree();
        }
        _seasonsOpening = null;
    }

    private void ReturnSeasonsToTitle()
    {
        ReleaseSeasonsGameplay();
        _sound.RestartSound();
        _mainMenuScreen = new MainMenuScreen { Name = "MainMenu", AssetRoot = SeasonsGameContent.AssetRoot,
            ZIndex = ScreenDrawPriority.FrontendZIndex };
        AddChild(_mainMenuScreen);
        OpenSeasonsMainMenu();
    }

    private void AllocateSeasonsBootHierarchy() =>
        _preparedSeasonsOpening = GD.Load<PackedScene>("res://scenes/seasons/opening.tscn").Instantiate<SeasonsOpeningSession>();

    private static PreparedBootData PrepareSeasonsBootData(string[] images) => new(images, static () => { });
}
