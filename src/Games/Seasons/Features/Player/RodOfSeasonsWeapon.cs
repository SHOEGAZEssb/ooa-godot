namespace Oracles;

internal sealed partial class RodOfSeasonsWeapon : TransitionOffsetNode2D
{
    private readonly SeasonsOpeningSession _session;
    private readonly EnemyAnimationPlayer _visual;
    private readonly RodOfSeasonsDatabase _data;
    private OracleRuntimeState Memory => _session.Memory;
    internal bool Finished { get; private set; }
    internal bool Initializing => Memory.ReadWramByte(0xd604) == 0;
    internal int ZHigh => unchecked((sbyte)Memory.ReadWramByte(0xd60f));
    internal Rect2 Bounds => new(Position - new Vector2(Read(0x27), Read(0x26)), new Vector2(Read(0x27), Read(0x26)) * 2);
    internal RodOfSeasonsWeapon(SeasonsOpeningSession session, RodOfSeasonsDatabase data)
    {
        _session = session; _data = data; AssetRoot = SeasonsGameContent.AssetRoot;
        for (int offset = 0; offset < 0x40; offset++) Write(offset, 0);
        Write(0, 1); Write(1, ItemId.RodOfSeasons); Write(0x17, 0xd2);
        for (int offset = 8; offset <= 15; offset++) Write(offset, Memory.ReadWramByte(0xd000 + offset));
        _visual = new(this, 8);
        // OBJ tile $52 is the first tile of this weapon-only sheet.
        _visual.Load(OracleGraphicsCache.LoadTwoBitSpriteSheet(AssetRoot + "rod/weapon.2bpp", 160), data.WeaponPoses,
            0, data.Flags & 7, positionedOam: true);
        _visual.SetAnimation(0); ZIndex = ObjectDrawPriority.BehindLinkZIndex; Visible = false;
        Write(0x24, data.Collision); Write(0x28, -data.Damage); Write(0x3a, -data.Damage); Write(0x29, data.Health);
        Write(0x1b, data.Flags); Write(0x1c, data.Flags); Write(0x1d, data.TileBase);
    }
    internal void UpdateFrame()
    {
        if (Initializing)
        {
            Write(4, 1); Write(0, 3); Write(6, 0x10); Write(0x1a, 0x82);
            Visible = true; _session.Sound.PlaySound(SoundId.SndSwordSlash); return;
        }
        Write(6, Read(6) - 1);
        if (Read(6) == 0 && Memory.ReadField("wActiveTileType") == (int)TerrainType.Stump)
            _session.Entities.TryAllocateInteraction(() => new SeasonsUsedRodRoomEntity(_session, Position, Read(9)));
    }
    internal void UpdatePost(bool parentActive)
    {
        if (!parentActive)
        {
            Finished = true; Visible = false;
            if (Read(1) == ItemId.RodOfSeasons)
                for (int offset = 0; offset < 0x40; offset++) Write(offset, 0);
            return;
        }
        // postUpdate.s:updateSwingableItemAnimation; Rod never breaks terrain.
        int parameter = Memory.ReadWramByte(0xd221) & 0x1f;
        int direction = CarriedObjectMotion.DirectionIndex(_session.Link.FacingVector);
        int index = parameter < 0x10 ? direction * 4 + (parameter >> 1) : parameter;
        ReadOnlySpan<byte> poses = _data.Table("swing");
        if ((uint)index >= poses.Length) throw new NotSupportedException($"ITEM$07 unsupported native swing parameter ${parameter:x2}.");
        int pose = poses[index], arcIndex = (pose >> 4) + (parameter < 0x10 ? 0 : 0x10);
        _visual.SetAnimation(pose & 7); Write(0x30, arcIndex);
        SwordArc arc = LinkItemDatabase.For(AssetRoot).SwordArc(arcIndex);
        Write(0x26, arc.RadiusY); Write(0x27, arc.RadiusX);
        Write(0x0b, (int)_session.Link.Position.Y + arc.OffsetY); Write(0x0d, (int)_session.Link.Position.X + arc.OffsetX);
        Write(0x0f, _session.Link.EnemyContactZ - 2);
        Position = new(Read(0x0d), Read(0x0b)); QueueRedraw();
    }
    public override void _Draw() => DrawTexture(_visual.CurrentTexture, SourceOamDrawOffset + _visual.CurrentOffset + Vector2.Down * ZHigh);
    private int Read(int offset) => Memory.ReadWramByte(0xd600 + offset);
    private void Write(int offset, int value) => Memory.SetWramByte(0xd600 + offset, unchecked((byte)value));
}
