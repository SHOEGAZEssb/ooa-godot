using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed partial class SpiritsGraveCubeFlame : SpiritsGraveVisualEntity,
    IRoomEntity, IFixedRoomEntity
{
    private readonly SpiritsGravePuzzleState _puzzle;
    private readonly EnemyAnimationPlayer[] _palettes = new EnemyAnimationPlayer[3];
    private int _palette;

    public Node2D Node => this;
    internal int Palette => _palette;

    internal SpiritsGraveCubeFlame(
        ObjectRecord record,
        VisualRecord visual,
        SpiritsGravePuzzleState puzzle)
    {
        _puzzle = puzzle;
        Name = $"SpiritsGraveCubeFlame_{record.Order}";
        Position = record.Position;
        Image source = EnemyVisualSource.LoadComposite(visual.Sprites);
        int[] sourcePalettes = { 2, 3, 1 };
        for (int index = 0; index < _palettes.Length; index++)
        {
            _palettes[index] = new EnemyAnimationPlayer(this, 1);
            _palettes[index].Load(source, visual.Animations, visual.TileBase, sourcePalettes[index]);
            _palettes[index].SetAnimation(0);
        }
        ApplyPuzzleState(advanceAnimation: false);
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        ApplyPuzzleState(advanceAnimation: true);
    }

    private void ApplyPuzzleState(bool advanceAnimation)
    {
        Visible = (_puzzle.CubeColor & 0x80) != 0;
        if (!Visible)
            return;
        _palette = _puzzle.CubeColor & 0x7f;
        if (advanceAnimation)
            _palettes[_palette].Advance();
        QueueRedraw();
    }

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) =>
        SetTransitionDrawOffset(offset);

    public override void _Draw()
    {
        if (Visible && _palettes[_palette].HasFrames)
            DrawTexture(_palettes[_palette].CurrentTexture,
                new Vector2(-16, -16) + TransitionDrawOffset);
    }
}
