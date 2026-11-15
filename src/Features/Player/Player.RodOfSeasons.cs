namespace Oracles;

public partial class Player
{
    private bool AdmitItemOverRod(int id)
    {
        if (!IsUsingRodOfSeasons || id is <= 0 or >= 0x20) return true;
        var usage = _parentItemUsage.Item(id);
        if (usage.Selector == 4) return false;
        if (usage.Selector != 3) return true;
        if (usage.Enabled < _inventory.RuntimeState.ReadWramByte(0xd200)) return false;
        _world.ClearRodOfSeasonsParent();
        return true;
    }

    private void StartRodOfSeasonsAction(Vector2 input, bool primaryButton)
    {
        if (_inventory.RuntimeState.Game != OracleGame.Seasons || !_inventory.HasTreasure(TreasureId.RodOfSeasons) ||
            IsUsingHarp || IsCarryingObject) return;
        Span<ParentItemSlotState> slots = stackalloc ParentItemSlotState[4];
        slots.Clear();
        CollectFreeItemParents(slots);
        int current = _inventory.RuntimeState.ReadWramByte(0xd201);
        if (current != 0 && (IsUsingItem || _world.BombParentActive || _world.BraceletParentActive))
            slots[0] = new((byte)(IsUsingRodOfSeasons ? _inventory.RuntimeState.ReadWramByte(0xd200) :
                current is TreasureId.Punch or TreasureId.BiggoronSword ? 0xff : _parentItemUsage.Item(current).Enabled), (byte)current);
        int primary = _inventory.EquippedA;
        if (!primaryButton && primary is > 0 and < 0x20 && Input.IsActionJustPressed("attack"))
        {
            var usage = _parentItemUsage.Item(primary);
            if (usage.Selector == 3 && usage.Enabled > slots[0].Enabled)
                slots[0] = new((byte)usage.Enabled, (byte)primary);
        }
        if (_parentItemUsage.ChooseSlot(TreasureId.RodOfSeasons, slots) != 2) return;
        if (_world.SwordDisabled || _world.SwordJinxed) { _world.PlaySound(SoundId.SndError); return; }
        CancelSwordAttack(); CancelShovelAction(); CancelPunchAction();
        _world.ClearRodOfSeasonsParent();
        if (input.LengthSquared() > 0.01f) UpdateFacing(input);
        _world.BeginRodOfSeasons(this);
        ParentItemButtons.SetAction(_inventory.RuntimeState, 2, primaryButton ? "attack" : "item");
        _walking = false;
        QueueRedraw();
    }
}
