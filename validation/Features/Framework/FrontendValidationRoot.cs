using Godot;
using System;
using System.Reflection;

namespace oracleofages;

/// <summary>Production frontend loop with isolated file slots and explicit host input.</summary>
internal partial class FrontendValidationRoot : GameRoot
{
    private ApplicationValidationFixture? _application;
    private ApplicationValidationFixture Application => _application ??= new(this);
    private readonly byte[]?[] _slots = new byte[OracleSaveStore.SlotCount][];
    public override void _Ready() { }
    public override void _Process(double delta) { }

    protected override OracleSaveData? LoadFileSlot(int slot)
    {
        if (_slots[slot] is not { } bytes) return null;
        if (!OracleSaveData.TryDeserialize(bytes, out var save))
            throw new InvalidOperationException("Frontend fixture could not reload its isolated save.");
        return save;
    }

    protected override SaveResult StoreFileSlot(int slot, OracleSaveData save)
    {
        _slots[slot] = save.Serialize();
        return SaveResult.Succeeded;
    }

    protected override void EraseFileSlot(int slot) => _slots[slot] = null;

    internal byte[] FileSlotSnapshot(int slot) => (byte[])_slots[slot]!.Clone();
    internal bool FileSlotExists(int slot) => _slots[slot] is not null;

    internal void CompleteScenePreload()
    {
        // File-start scenarios stop before destination preparation. Drain the
        // owner's pending resource request before freeing this isolated root.
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        var resource = (GameplaySceneResource)typeof(GameRoot)
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
        _sound = new OracleSoundEngine(new OracleSoundData(), false) { ApplicationUpdateOwned = true };
        AddChild(_sound);
        _random = new OracleRandom();
        StartFrontend(startAtTitle: false);
    }

    internal void Step(int updates, bool batched, params string[] pressed) =>
        Application.Step(updates, Vector2.Zero, pressed, pressed, batched);

    internal void AdvanceHost(double delta) => base._Process(delta);
}
