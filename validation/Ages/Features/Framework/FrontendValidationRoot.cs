using Godot;
using System;
using System.Reflection;

namespace Oracles;

/// <summary>Production frontend loop with isolated file slots and explicit host input.</summary>
internal partial class FrontendValidationRoot : GameRoot
{
    private ApplicationValidationFixture? _application;
    private ApplicationValidationFixture Application => _application ??= new(this);
    private readonly byte[]?[] _slots = new byte[OracleSaveStore.SlotCount][];
    private readonly byte[]?[] _seasonsSlots = new byte[OracleSaveStore.SlotCount][];
    private readonly System.Collections.Generic.Dictionary<(OracleGame Game,int Slot),byte[]> _debugSlots = new();
    internal int DebugStateWrites { get; private set; }
    private byte[]?[] Slots(OracleGame game) => game == OracleGame.Seasons ? _seasonsSlots : _slots;
    internal string? NextSaveFailure { get; set; }
    public override void _Ready() { }
    public override void _Process(double delta) { }

    internal override SaveResult StoreDebugStateSlot(int slot, DebugSavestateData state)
    {
        _debugSlots[(state.Game,slot)] = state.Serialize();
        DebugStateWrites++;
        return SaveResult.Succeeded;
    }

    internal override DebugSavestateLoadResult LoadDebugStateSlot(int slot)
    {
        if (!_debugSlots.TryGetValue((_selectedGame,slot),out var bytes)) return DebugSavestateLoadResult.NotFound;
        return DebugSavestateData.TryDeserialize(bytes,_selectedGame,out var state)
            ? DebugSavestateLoadResult.Loaded(state!) : DebugSavestateLoadResult.Failed("Invalid isolated debug state.");
    }

    internal override OracleSaveData? LoadFileSlot(int slot, OracleGame game)
    {
        if (Slots(game)[slot] is not { } bytes) return null;
        if (!OracleSaveData.TryDeserialize(bytes, game, out var save))
            throw new InvalidOperationException("Frontend fixture could not reload its isolated save.");
        return save;
    }

    protected override SaveResult StoreFileSlot(int slot, OracleSaveData save)
    {
        if (NextSaveFailure is string error)
        {
            NextSaveFailure = null;
            return SaveResult.Failed(error);
        }
        Slots(save.Game)[slot] = save.Serialize();
        return SaveResult.Succeeded;
    }

    protected override void EraseFileSlot(int slot) => Slots(_selectedGame)[slot] = null;

    internal byte[] FileSlotSnapshot(int slot, OracleGame game) => (byte[])Slots(game)[slot]!.Clone();
    internal bool FileSlotExists(int slot, OracleGame game) => Slots(game)[slot] is not null;
    internal void SeedFileSlot(int slot, OracleSaveData save) => StoreFileSlot(slot,save);

    internal GameSelectScreen InitializeGameSelect(bool persistSaveData = false)
    {
        if (persistSaveData) typeof(GameRoot).GetField("_persistSaveData", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(this, true);
        _sound = new OracleSoundEngine(new OracleSoundData(AgesGameContent.AssetRoot), false) { ApplicationUpdateOwned = true };
        AddChild(_sound);
        _random = new OracleRandom();
        var boot = ResourceLoader.Load<PackedScene>("res://scenes/launcher/boot_loading.tscn").Instantiate<BootLoadingScreen>();
        AddChild(boot);
        boot.Owner = this;
        boot.UniqueNameInOwner = true;
        var screen = ResourceLoader.Load<PackedScene>("res://scenes/launcher/game_select.tscn").Instantiate<GameSelectScreen>();
        AddChild(screen);
        BeginGameSelect(screen);
        return screen;
    }

    internal void CompleteBootLoading()
    {
        var deadline = System.Diagnostics.Stopwatch.StartNew();
        while (_mainMenu is null && _frontendIntro is null && deadline.Elapsed.TotalSeconds < 30)
        {
            AdvanceHost(1.0 / 60.0);
            System.Threading.Thread.Sleep(1);
        }
        if (_mainMenu is null && _frontendIntro is null) throw new InvalidOperationException("Frontend boot did not complete.");
    }

    internal void CompleteScenePreload()
    {
        // File-start scenarios stop before destination preparation. Drain the
        // owner's pending resource request before freeing this isolated root.
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        var resource = (AgesGameplaySceneResource)typeof(GameRoot)
            .GetField("_gameplaySceneResource", fields)!.GetValue(this)!;
        _ = resource.Load();
    }

    internal void Initialize(OracleSaveData? slotOneSave = null, bool persistSaveData = false)
    {
        if (slotOneSave is not null)
        {
            StoreFileSlot(0, slotOneSave);
        }
        if (slotOneSave is not null || persistSaveData)
        {
            // Use the retail checkpoint path with the isolated store above.
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(GameRoot).GetField("_launchOptions", fields)!.SetValue(this, new LaunchOptions());
            typeof(GameRoot).GetField("_persistSaveData", fields)!.SetValue(this, true);
        }
        _sound = new OracleSoundEngine(new OracleSoundData(AgesGameContent.AssetRoot), false) { ApplicationUpdateOwned = true };
        AddChild(_sound);
        _random = new OracleRandom();
        StartFrontend(startAtTitle: false);
    }

    internal void Step(int updates, bool batched, params string[] pressed) =>
        Application.Step(updates, Vector2.Zero, pressed, pressed, batched);

    internal void AdvanceHost(double delta) => base._Process(delta);

    internal void CompleteSavedGameLoading(bool batched)
    {
        var loading = typeof(GameRoot).GetField("_loadingSavedGame",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        for (int host = 0; (bool)loading.GetValue(this)! && host < 512; host++)
        {
            Step(batched ? 4 : 1, batched);
            AdvanceGameplayPreparation();
        }
        if ((bool)loading.GetValue(this)!)
            throw new InvalidOperationException("Saved-file resource preparation did not complete.");
    }
}
