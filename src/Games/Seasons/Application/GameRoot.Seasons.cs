using Godot;

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
        _mainMenu = null; _mainMenuScreen!.QueueFree(); _mainMenuScreen = null;
        _seasonsOpening = _preparedSeasonsOpening ?? GD.Load<PackedScene>("res://scenes/seasons/opening.tscn").Instantiate<SeasonsOpeningSession>();
        _preparedSeasonsOpening = null;
        AddChild(_seasonsOpening); _seasonsOpening.Start(_sound, _random, save);
        _runtimeState = _seasonsOpening.Memory;
    }

    private void AllocateSeasonsBootHierarchy() =>
        _preparedSeasonsOpening = GD.Load<PackedScene>("res://scenes/seasons/opening.tscn").Instantiate<SeasonsOpeningSession>();

    private static PreparedBootData PrepareSeasonsBootData(string[] images) => new(images, static () => { });
}
