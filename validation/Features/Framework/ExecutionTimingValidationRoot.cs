using Godot;
using System;
using System.Reflection;

namespace oracleofages;

/// <summary>Isolated frontend using the production application loop and input buffer.</summary>
internal partial class ExecutionTimingValidationRoot : GameRoot
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly byte[]?[] _slots = new byte[OracleSaveStore.SlotCount][];
    private ApplicationInputBuffer Buffer => (ApplicationInputBuffer)typeof(GameRoot)
        .GetField("_applicationInput", Private)!.GetValue(this)!;
    public override void _Ready() { }
    public override void _Process(double delta) { }
    public override void _ExitTree() => _originalTiming?.Dispose();
    protected override OracleSaveData? LoadFileSlot(int slot) => SavedSlot(slot);
    internal OracleSaveData? SavedSlot(int slot)
    {
        if (_slots[slot] is not { } bytes) return null;
        if (!OracleSaveData.TryDeserialize(bytes, out var save))
            throw new InvalidOperationException("Execution timing fixture could not reload its isolated save.");
        return save;
    }
    protected override SaveResult StoreFileSlot(int slot, OracleSaveData save)
    {
        _slots[slot] = save.Serialize();
        return SaveResult.Succeeded;
    }
    protected override void EraseFileSlot(int slot) => _slots[slot] = null;

    internal void Initialize()
    {
        _sound = new OracleSoundEngine(new OracleSoundData(), false) { ApplicationUpdateOwned = true };
        AddChild(_sound);
        _random = CreateRandom();
        typeof(GameRoot).GetMethod("StartFrontend", Private)!.Invoke(this, [false]);
    }

    internal void Sample(params string[] held) => Buffer.CaptureForValidation(held, held, Vector2.Zero);

    internal void InitializeAtFileSelect(int titleDelay = 0)
    {
        Initialize();
        Step(241);
        Step(1, "inventory");
        Step(9 + titleDelay);
        Step(1, "inventory");
        Step(32);
    }

    internal void Step(int count, params string[] held)
    {
        Sample(held);
        Action advance = typeof(GameRoot).GetMethod("AdvanceApplicationUpdate", Private)!.CreateDelegate<Action>(this);
        for (int i = 0; i < count; i++) advance();
    }
}
