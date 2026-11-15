namespace Oracles;

// otherSwordsParent.s owns ParentItem2; its physical child occupies reserved ITEM $d6.
internal sealed class RodOfSeasonsController(SeasonsOpeningSession session)
{
    private RodOfSeasonsDatabase? _data;
    private readonly ParentItemUsageDatabase _parents = new(SeasonsGameContent.AssetRoot);
    private RodOfSeasonsWeapon? _weapon;
    private OracleRuntimeState Memory => session.Memory;
    internal bool Active => Memory.ReadWramByte(0xd200) != 0 && Memory.ReadWramByte(0xd201) == ItemId.RodOfSeasons;
    internal bool WeaponActive => _weapon is { Finished: false };
    internal void Begin(Player player)
    {
        Cancel();
        Memory.SetWramByte(0xd200, Memory.ReadField("wActiveTileType") == (int)TerrainType.Stump ? (byte)_parents.Item(ItemId.RodOfSeasons).Enabled : (byte)0xff);
        Memory.SetWramByte(0xd204, 1);
        Memory.SetWramByte(0xd218, 0);
        Memory.SetWramByte(0xd219, 0xd6);
        player.NotifyParentItemAnimationStarted(ItemId.RodOfSeasons);
        Memory.SetField("wLinkTurningDisabled", (byte)(Memory.ReadField("wLinkTurningDisabled") | 1));
        LinkNativeAnimationDatabase.For(SeasonsGameContent.AssetRoot).Set(Memory,
            player.CompanionRideActive || player.MinecartRideActive ? 0x26 : 0x22, 0xd2);
        _weapon = new(session, _data ??= new());
        session.WorldRoot.AddChild(_weapon);
    }
    internal void UpdateParent()
    {
        if (!Active) return;
        if (session.Link.World.SwordDisabled || session.Link.World.SwordJinxed ||
            (Memory.ReadField("wcc95") & 0x80) != 0 || Memory.ReadField("wLinkClimbingVine") == 0xff ||
            (Memory.ReadWramByte(0xd221) & 0x80) != 0) { ClearParent(); return; }
        LinkNativeAnimationDatabase.For(SeasonsGameContent.AssetRoot).Animate(Memory, 0xd2);
    }
    internal void UpdateItem(bool frozen)
    {
        if (Memory.ReadWramByte(0xd601) == ItemId.RodOfSeasons &&
            _weapon is { Finished: false } weapon && (!frozen || weapon.Initializing)) weapon.UpdateFrame();
    }
    internal void UpdatePost()
    {
        if (_weapon is null) return;
        _weapon.UpdatePost(Active);
        if (_weapon.Finished) { _weapon.Free(); _weapon = null; return; }
        var weapon = _weapon;
        session.Entities.ApplySwordHit(weapon.Bounds, session.Link.Position,
            RingEffects.SwordDamageFromBase(session.Inventory, (_data ??= new()).Damage), EnemyKnockbackStrength.Normal,
            collectItemDrops: true, itemZ: weapon.ZHigh, meleeActive: () => Active && _weapon == weapon,
            itemCollisionType: 0x0c, deferAll: true);
    }
    internal void ClearParent()
    {
        if (!Active) return;
        ParentItemAnimationState.Clear(Memory, 2, ItemId.RodOfSeasons);
        for (int offset = 0; offset < 0x40; offset++) Memory.SetWramByte(0xd200 + offset, 0);
    }
    internal void Cancel()
    {
        ClearParent();
        if (_weapon is not null) { _weapon.Free(); _weapon = null; }
        if (Memory.ReadWramByte(0xd601) == ItemId.RodOfSeasons)
            for (int offset = 0; offset < 0x40; offset++) Memory.SetWramByte(0xd600 + offset, 0);
    }
}
