using System;

namespace oracleofages;

/// <summary>Session-owned wPegasusSeedCounter and the Satchel's parent-only $22 branch.</summary>
public sealed class PegasusSeedState
{
    private readonly InventoryState _inventory;
    private readonly RoomEntityManager _entities;
    internal PegasusSeedRecord Data { get; } = PegasusSeedRecord.Load();
    internal PegasusSeedState(InventoryState inventory, RoomEntityManager entities)
    { _inventory = inventory; _entities = entities; }

    internal int RawCounter
    {
        get => _entities.RuntimeState.ReadWramByte(WramAddress.wPegasusSeedCounter) |
            (_entities.RuntimeState.ReadWramByte(WramAddress.wPegasusSeedCounter + 1) << 8);
        private set
        {
            _entities.RuntimeState.SetWramByte(WramAddress.wPegasusSeedCounter, (byte)value);
            _entities.RuntimeState.SetWramByte(WramAddress.wPegasusSeedCounter + 1, (byte)(value >> 8));
        }
    }
    public bool Active => RawCounter != 0;
    public int NormalSpeed => Data.NormalSpeed;
    public int GrassSpeed => Data.GrassSpeed;
    public int StairsSpeed => Data.StairsSpeed;

    internal void TryUse()
    {
        if (Active || !_inventory.HasSelectedSatchelSeed()) return;
        if (!_inventory.TryConsumeSelectedSatchelSeed(out int consumed) || consumed != 0x22)
            throw new InvalidOperationException("Pegasus Satchel parent consumed a seed other than ITEM $22.");
        RawCounter = Data.Counter;
        _entities.RequestPegasusDust(this, signal: false);
    }

    public void Clear() => RawCounter = 0;

    public void AdvanceCounter()
    {
        int counter = RawCounter & 0x7fff;
        int decrement = RingEffects.PegasusSeedTimerDecrement(_inventory);
        int mask = decrement == 1 ? 7 : 15, signal = 0;
        for (int i = 0; i < decrement; i++)
        {
            counter = Math.Max(0, counter - 1);
            if (counter == 0) { RawCounter = 0; return; }
            if ((counter & mask) == 0) signal = 0x8000;
        }
        RawCounter = counter | signal;
    }

    public void AnimateWalking()
    {
        // The high bit is not consumed here. Each native animation call can
        // increment ITEM_DUST.subid; its low bit gates allocation in state2.
        if ((RawCounter & 0x8000) == 0) return;
        _entities.RequestPegasusDust(this, signal: true);
        _entities.RequestSound(Data.StepSound);
    }
}

internal sealed record PegasusSeedRecord(int Counter, int NormalSpeed, int GrassSpeed,
    int StairsSpeed, int DustTile, int DustFlags, int StepSound, string[] Animations)
{
    internal static PegasusSeedRecord Load()
    {
        var table = GeneratedTable.Load("res://assets/oracle/metadata/pegasus_seed.tsv",
            new GeneratedTableSchema("Pegasus seed", GeneratedTableKeySemantics.Unique,
                ["item", "counter", "normal-speed", "grass-speed", "stairs-speed", "dust-tile",
                 "dust-flags", "step-sound", "initial-animation", "cloud1", "cloud2", "cloud3", "source"],
                ["item"], headerRequired: true));
        if (table.Rows.Count != 1 || table.Rows[0].HexByte(0) != 0x22)
            throw new InvalidOperationException("Expected one Pegasus Satchel ITEM $22 record.");
        var row = table.Rows[0];
        return new(row.HexWord(1), row.HexByte(2), row.HexByte(3), row.HexByte(4),
            row.HexByte(5), row.HexByte(6), row.HexByte(7),
            [row.RequiredString(8), row.RequiredString(9), row.RequiredString(10), row.RequiredString(11)]);
    }
}
