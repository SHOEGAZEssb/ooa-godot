using System;

namespace Oracles;

public partial class Player
{
    private bool InstrumentParentActive => _inventory is not null && _inventory.RuntimeState.ReadWramByte(0xd500) != 0 &&
        _inventory.RuntimeState.ReadWramByte(0xd501) is TreasureId.Harp or TreasureId.Flute;
    internal int ShieldParentButton => ShieldParentActive ? ParentItemButtons.Read(_inventory.RuntimeState, 5) : 0;
    internal bool ShieldParentInitialized
    {
        get => _inventory.RuntimeState.ReadWramByte(0xd504) != 0;
        private set => _inventory.RuntimeState.SetWramByte(0xd504, value ? (byte)1 : (byte)0);
    }
    private bool ShieldParentActive => _inventory.RuntimeState.ReadWramByte(0xd500) != 0 &&
        _inventory.RuntimeState.ReadWramByte(0xd501) == TreasureId.Shield;

    private void AdmitParentItem5(bool primarySuppressed)
    {
        if (_itemAllocationBlockedThisUpdate || _world.ItemUsageDisabled || _activeTransformation != 0 ||
            TopDownSwimming || IsUsingHarp) return;
        if (!primarySuppressed) CheckButton(_inventory.EquippedA, 1, "attack");
        if (!SecondaryItemAllocationSuppressed) CheckButton(_inventory.EquippedB, 2, "item");

        void CheckButton(int id, int button, string action)
        {
            if (id is <= 0 or >= 0x20) return;
            var usage = _parentItemUsage.Item(id);
            if (usage.Selector == 5 && (usage.JustPressed ? Input.IsActionJustPressed(action) : Input.IsActionPressed(action)))
                TryAllocateParentItem5(id, button);
        }
    }

    private bool TryAllocateParentItem5(int id, int button)
    {
        var memory = _inventory.RuntimeState;
        if (memory.ReadWramByte(0xd500) != 0 || _parentItemUsage.Item(id).Selector != 5) return false;
        memory.SetWramByte(0xd500, (byte)_parentItemUsage.Item(id).Enabled);
        memory.SetWramByte(0xd501, (byte)id);
        ParentItemButtons.Set(memory, 5, button);
        return true;
    }

    private void ClearParentItem5(int id)
    {
        var memory = _inventory.RuntimeState;
        if (memory.ReadWramByte(0xd501) != id) return;
        for (int offset = 0; offset < 0x40; offset++) memory.SetWramByte(0xd500 + offset, 0);
    }

    private void ClearCapeParents()
    {
        for (int index = 0; index < _capeParents.Length; index++)
            if (_capeParents[index].HasValue) ParentItemButtons.Set(_inventory.RuntimeState, index + 3, 0);
        Array.Clear(_capeParents);
    }

    internal void ReassignNativeParentButtons()
    {
        Span<ParentItemSlotState> parents = stackalloc ParentItemSlotState[4];
        CollectFreeItemParents(parents);
        var memory = _inventory.RuntimeState;
        bool fixedParent = IsAttacking || _forcedSwordParentInitializing || IsUsingShovel || IsUsingPunch ||
            IsUsingSeedShooter || IsUsingSwitchHook || IsUsingSomaria || IsUsingBiggoron ||
            _world.BombParentActive || _world.BraceletParentActive;
        int id = memory.ReadWramByte(0xd201);
        if (fixedParent && id != 0) parents[0] = new(1, (byte)id);
        parents[3] = new(memory.ReadWramByte(0xd500), memory.ReadWramByte(0xd501));
        ParentItemButtons.Reassign(memory, parents, _inventory.EquippedA, _inventory.EquippedB);
    }
}
