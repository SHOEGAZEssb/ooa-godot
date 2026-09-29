using Godot;
using System;
using System.Reflection;

namespace oracleofages;

/// <summary>Production frontend loop with isolated file slots and explicit host input.</summary>
internal partial class FrontendValidationRoot : GameRoot
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
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

    internal void Initialize()
    {
        _sound = new OracleSoundEngine(new OracleSoundData(), false) { ApplicationUpdateOwned = true };
        AddChild(_sound);
        _random = new OracleRandom();
        typeof(GameRoot).GetMethod("StartFrontend", Private)!.Invoke(this, [false]);
    }

    internal void Step(int updates, bool batched, params string[] pressed)
    {
        var buffer = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput", Private)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates", Private)!.GetValue(this)!;
        Action advance = typeof(GameRoot).GetMethod("AdvanceApplicationUpdate", Private)!.CreateDelegate<Action>(this);
        buffer.CaptureForValidation(pressed, pressed, Vector2.Zero);
        if (batched) scheduler.Advance(updates / 60.0, advance);
        else for (int update = 0; update < updates; update++) scheduler.Advance(1.0 / 60.0, advance);
    }

    internal void AdvanceHost(double delta) => base._Process(delta);
}
