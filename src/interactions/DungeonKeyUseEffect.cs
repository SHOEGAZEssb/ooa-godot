using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class DungeonKeyUseRoomEntity(DungeonKeyUseEffect effect)
    : RoomEntityAdapter<DungeonKeyUseEffect>(effect, effect.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime
{
    public bool Finished => Entity.Finished;

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame();

    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
}

/// <summary>
/// INTERAC_DUNGEON_KEY_SPRITE $17, small-key subid $00. The key stays four
/// pixels above the door for eight updates, then eight pixels above it for 20.
/// </summary>
internal partial class DungeonKeyUseEffect : TransitionOffsetNode2D
{
    private Texture2D _texture = null!;
    private Vector2 _textureOffset;
    private int _phase;
    private int _counter;
    private int _z;

    internal bool Finished { get; private set; }
    internal int Phase => _phase;
    internal int Counter => _counter;
    internal int Z => _z;

    internal void Initialize(
        Vector2 position,
        TreasureDatabase.TreasureObjectVisualRecord visual)
    {
        Position = position;
        Image source = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{visual.Sprite}.png");
        OracleGraphicsCache.AnimationDefinition definition =
            OracleGraphicsCache.GetAnimationDefinition(visual.Animation);
        if (visual.Graphic != 0x42 || visual.TileBase != 0x0c ||
            visual.Palette != 5 || definition.Frames.Length == 0)
        {
            throw new System.InvalidOperationException(
                "INTERAC_DUNGEON_KEY_SPRITE small-key visual no longer matches treasure graphic $42.");
        }
        (_texture, _textureOffset) = NpcCharacter.BuildPositionedOamTexture(
            source,
            definition.Frames[0].EncodedOam,
            visual.TileBase,
            visual.Palette,
            paletteOverride: null,
            sourceGrayscaleInverted: true);
        _phase = 0;
        _counter = 8;
        _z = -4;
        QueueRedraw();
    }

    internal void UpdateFrame()
    {
        if (Finished)
            return;
        _counter--;
        if (_counter != 0)
            return;
        if (_phase == 0)
        {
            _phase = 1;
            _counter = 20;
            _z = -8;
            QueueRedraw();
            return;
        }
        Finished = true;
        Visible = false;
    }

    public override void _Draw()
    {
        if (!Finished)
            DrawTexture(
                _texture,
                _textureOffset + new Vector2(0, _z) + TransitionDrawOffset);
    }
}
