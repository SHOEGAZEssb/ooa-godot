using Godot;
using System;

namespace oracleofages;

/// <summary>
/// PART_SHADOW ($07), attached to an airborne boss object. The original part
/// alternates visibility each update and chooses its size from the parent's
/// signed Z high byte.
/// </summary>
public partial class BossShadowEffect : TransitionOffsetNode2D
{

    private static BossShadowEffectDefinition? _definition;
    private Func<Vector2> _parentPosition = null!;
    private Func<int> _parentZ = null!;
    private Func<bool> _parentExists = null!;
    private Texture2D[] _textures = null!;
    private int _size;
    private int _yOffset;
    private bool _initialized;

    public bool Finished { get; private set; }
    internal int AnimationIndex { get; private set; }
    internal int Size => _size;
    internal int YOffset => _yOffset;

    internal void Initialize(
        Func<Vector2> parentPosition,
        Func<int> parentZ,
        Func<bool> parentExists,
        int size,
        int yOffset)
    {
        if (size is < 0 or > 2)
            throw new ArgumentOutOfRangeException(nameof(size));

        BossShadowEffectDefinition definition = _definition ??= LoadDefinition();
        _parentPosition = parentPosition;
        _parentZ = parentZ;
        _parentExists = parentExists;
        _textures = definition.Textures;
        _size = size;
        _yOffset = yOffset;
        Position = parentPosition() + Vector2.Down * yOffset;
        QueueRedraw();
    }

    internal void UpdateFrame()
    {
        if (Finished)
            return;
        if (!_initialized)
        {
            _initialized = true;
            Visible = true;
        }
        if (!_parentExists())
        {
            Finished = true;
            Visible = false;
            return;
        }

        Position = _parentPosition() + Vector2.Down * _yOffset;
        int z = _parentZ();
        if (z == 0)
        {
            Visible = false;
            return;
        }

        // partCode07 works on the parent's raw Z high byte. The four entries
        // correspond to >=-$20, >=-$40, >=-$60, and higher than -$60.
        AnimationIndex = AnimationIndexFor(_size, z);
        Visible = !Visible;
        QueueRedraw();
    }

    internal static int AnimationIndexFor(int size, int z)
    {
        if (size is < 0 or > 2)
            throw new ArgumentOutOfRangeException(nameof(size));
        int rawZ = z & 0xff;
        int heightBand = rawZ >= 0xe0 ? 0
            : rawZ >= 0xc0 ? 1
            : rawZ >= 0xa0 ? 2
            : 3;
        ReadOnlySpan<int> animationIndices =
        [1, 1, 0, 0, 2, 1, 1, 0, 3, 2, 1, 0];
        return animationIndices[size * 4 + heightBand];
    }

    public override void _Draw()
    {
        if (!Finished)
        {
            DrawTexture(
                _textures[AnimationIndex],
                new Vector2(-16, -16) + TransitionDrawOffset);
        }
    }

    private static BossShadowEffectDefinition LoadDefinition()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/effects/boss_shadow.tsv",
            new GeneratedTableSchema(
                "boss shadow",
                GeneratedTableKeySemantics.Ordered,
                ["sprite", "tile-base", "palette", "animation-0",
                 "animation-1", "animation-2", "animation-3"],
                headerRequired: true));
        if (table.Rows.Count != 1)
        {
            throw new InvalidOperationException(
                $"PART_SHADOW should have one row, got {table.Rows.Count}.");
        }

        GeneratedTableRow row = table.Rows[0];
        string sprite = row.RequiredString(0);
        int tileBase = row.UnsignedDecimal(1);
        int palette = row.UnsignedDecimal(2);
        Image source = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{sprite}.png");
        var textures = new Texture2D[4];
        for (int index = 0; index < textures.Length; index++)
        {
            textures[index] = NpcCharacter.BuildOamTexture(
                source, row.RequiredString(3 + index), tileBase, palette);
        }
        return new BossShadowEffectDefinition(sprite, tileBase, palette, textures);
    }
}
