using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

/// <summary>ITEM_DUST in reserved item F; two alternating clouds share one OAM object.</summary>
internal sealed partial class PegasusDustRoomEntity : TransitionOffsetNode2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime
{
    private readonly PegasusSeedState _pegasus;
    private readonly Image _source = EnemyVisualSource.LoadComposite(["spr_common_sprites"]);
    private readonly Dictionary<(string Oam, int Tile, int Flags), (Texture2D Texture, Vector2 Offset)> _textures = new();
    private readonly byte[] _clouds = new byte[8];
    private int _subid, _counter, _frame, _frameCounter, _z;
    private string _oam = "";
    internal int Substate { get; private set; }
    internal int OamFlags { get; private set; }
    internal int TileBase { get; private set; }
    internal int Subid => _subid;
    internal ReadOnlySpan<byte> Clouds => _clouds;
    public bool Finished { get; private set; }
    public Node2D Node => this;

    internal PegasusDustRoomEntity(PegasusSeedState pegasus)
    { _pegasus = pegasus; Name = "PegasusDust_ItemF"; Visible = false; ZIndex = NpcCharacter.FixedHighPriorityZIndex; }
    internal void Signal() => _subid = (_subid + 1) & 255;
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Substate == 0)
        {
            Substate = 1;
            Position = frame.Player.Position;
            _z = frame.Player.GaleZFixed >> 8;
            OamFlags = _pegasus.Data.DustFlags;
            TileBase = _pegasus.Data.DustTile;
            SetFrame(0, 0, applyTile: false);
            Visible = true;
        }
        else if (Substate == 1)
        {
            if (--_frameCounter == 0) SetFrame(0, _frame + 1, applyTile: false);
            var entry = OracleGraphicsCache.GetAnimationDefinition(_pegasus.Data.Animations[0]).Frames[_frame];
            TileBase = entry.Parameter & 0x7f;
            OamFlags = ((OamFlags + 1) & 0xfb) ^ 0x60;
            if ((entry.Parameter & 0x80) != 0)
            { OamFlags = 0x0b; _z = 0; Visible = false; Substate = 2; }
        }
        else
        {
            if (!_pegasus.Active) { Finished = true; Visible = false; return; }
            if ((_subid & 1) != 0)
            {
                _subid = 0;
                int free = (_clouds[0] & 0x80) == 0 ? 0 : (_clouds[4] & 0x80) == 0 ? 4 : -1;
                if (free >= 0)
                {
                    _clouds[free] = 0x80; _clouds[free + 1] = 0;
                    _clouds[free + 2] = (byte)((int)frame.Player.Position.Y + 5);
                    _clouds[free + 3] = (byte)frame.Player.Position.X;
                }
            }
            _counter = (_counter - 1) & 255;
            int slot = (_counter & 1) * 4;
            Visible = (_clouds[slot] & 0x80) != 0;
            if (Visible)
            {
                if (++_clouds[slot] >= 0x82)
                {
                    _clouds[slot] = 0x80;
                    if (++_clouds[slot + 1] >= 3)
                    { Array.Clear(_clouds, slot, 4); Visible = false; QueueRedraw(); return; }
                }
                Position = new Vector2(_clouds[slot + 3], _clouds[slot + 2]);
                SetFrame(_clouds[slot + 1] + 1, 0, applyTile: true);
            }
        }
        QueueRedraw();
    }

    private void SetFrame(int animation, int frame, bool applyTile)
    {
        var entry = OracleGraphicsCache.GetAnimationDefinition(_pegasus.Data.Animations[animation]).Frames[frame];
        _frame = frame; _frameCounter = entry.Duration; _oam = entry.EncodedOam;
        if (applyTile) TileBase = entry.Parameter & 0x7f;
    }

    public override void _Draw()
    {
        if (!Visible || _oam.Length == 0) return;
        var key = (_oam, TileBase, OamFlags);
        if (!_textures.TryGetValue(key, out var visual))
        {
            string oam = string.Join(';', _oam.Split(';').Select(block =>
            {
                var fields = block.Split(',');
                fields[3] = (int.Parse(fields[3]) ^ (OamFlags & 0x60)).ToString();
                return string.Join(',', fields);
            }));
            visual = NpcCharacter.BuildPositionedOamTexture(_source, oam, TileBase,
                OamFlags & 7, paletteOverride: null, sourceGrayscaleInverted: true);
            _textures.Add(key, visual);
        }
        DrawTexture(visual.Texture, visual.Offset + new Vector2(0, _z) + TransitionDrawOffset);
    }
}
