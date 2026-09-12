using Godot;
using System;

namespace oracleofages;

/// <summary>PART_FALLING_BOULDER_SPAWNER $45, shared by the climb to Patch.</summary>
internal sealed partial class FallingBoulder : TransitionOffsetNode2D
{
    private readonly FallingBoulderDatabase _data;
    private readonly OracleRandom _random;
    private readonly Action<int> _sound;
    private readonly EnemyAnimationPlayer _animation;
    private readonly OracleRoomData _room;
    private readonly Func<Vector2, Vector2> _worldToScreen;
    private readonly TerrainShadowDefinition _shadow = TerrainShadow.Load();
    private int _frameCounter;
    private OracleObjectPosition _position;
    private Vector2 _origin;
    private int _zFixed;
    private int _speedZ;

    internal int SubId { get; }
    internal int State { get; private set; }
    internal int Counter { get; private set; }
    internal int Angle { get; private set; }
    internal int Z => _zFixed >> 8;
    internal int ZFixed => _zFixed;
    internal int SpeedZ => _speedZ;
    internal OracleObjectPosition FixedPosition => _position;
    internal Texture2D CurrentTexture => _animation.CurrentTexture;
    internal int PartSlot { get; private set; }
    internal bool ShadowDrawn => Visible && Z < 0 && ((_frameCounter ^ PartSlot) & 1) != 0 &&
        (_room.TilesetFlags & 0x20) == 0 && 16 - _worldToScreen(Vector2.Zero).Y < 0x97;

    internal FallingBoulder(int subId, Vector2 position, FallingBoulderDatabase data,
        OracleRandom random, Action<int> sound, OracleRoomData room, Func<Vector2, Vector2> worldToScreen)
    {
        if ((uint)subId >= data.Delays.Length)
            throw new InvalidOperationException($"fallingBoulderSpawner.s: unsupported PART $45:${subId:x2}.");
        SubId = subId;
        _position = OracleObjectPosition.FromPixels(position);
        Position = position;
        _data = data;
        _random = random;
        _sound = sound;
        _room = room;
        _worldToScreen = worldToScreen;
        Name = $"FallingBoulder_{subId:x2}";
        Visible = false;
        ZIndex = 10;
        _animation = new(this, 1);
        _animation.Load(OracleGraphicsCache.LoadImage($"res://assets/oracle/gfx/{data.Sprite}.png"),
            [data.Animation], data.TileBase, data.Palette,
            sourceGrayscaleInverted: data.SourceGrayscaleInverted);
        _animation.SetAnimation(0);
    }

    internal void UpdateFrame(int frameCounter)
    {
        _frameCounter = frameCounter;
        switch (State)
        {
            case 0:
                int y = unchecked((byte)((_position.YFixed >> 8) - 8));
                if (y != 0) y = unchecked((byte)(y + 4));
                _origin = new(_position.XFixed >> 8, y);
                RestoreHighBytes();
                State = 1;
                Counter = _data.Delays[SubId];
                break;
            case 1:
                if (Counter != 0) Counter--;
                if (Counter != 0) break;
                State = 2;
                Bounce();
                break;
            case 2:
                if (OracleObjectMath.UpdateSpeedZ(ref _zFixed, ref _speedZ, 0x20)) Bounce();
                _position = OracleObjectMovement.Shared.ApplySpeed(_position, 0x32, Angle);
                Position = _position.PixelPosition;
                if (Position.Y < 0x88) _animation.Advance();
                else
                {
                    State = 1;
                    Counter = 0xb4;
                    // The source restores only yh/xh. Fractions, Z and animation survive.
                    RestoreHighBytes();
                    Visible = false;
                }
                break;
            default:
                throw new InvalidOperationException($"PART $45:${SubId:x2} unsupported state ${State:x2}.");
        }
        QueueRedraw();
    }

    internal void HandleLinkContact(Player player)
    {
        var bounds = new Rect2(Position - Vector2.One * _data.Radius, Vector2.One * _data.Radius * 2);
        if (State == 2 && player.AcceptsRoomEntityContact &&
            RoomEntityManager.ObjectCollisionZOverlaps(Z, player.EnemyContactZ, 7) &&
            Player.EnemyCollisionOverlaps(player.EnemyContactPosition, bounds))
            player.ApplyEnemyContactDamage(Position, _data.Damage);
    }

    private void Bounce()
    {
        _speedZ = -0x1a0;
        int value;
        do { value = _random.Next().Value & 7; } while (value == 7);
        Angle = value + 0x0d;
        Visible = true;
        _sound(0xb3); // SND_RUMBLE, fallingBoulderSpawner.s:@bounceRandomlyDownwards.
    }

    private void RestoreHighBytes()
    {
        _position = new((ushort)(((int)_origin.Y << 8) | (_position.YFixed & 255)),
            (ushort)(((int)_origin.X << 8) | (_position.XFixed & 255)));
        Position = _position.PixelPosition;
    }

    internal void SetPartSlot(int slot)
    {
        if ((uint)slot >= 16)
            throw new InvalidOperationException("PART $45: no free part slot in $d0..$df.");
        PartSlot = slot;
    }

    public override void _Draw()
    {
        if (ShadowDrawn) DrawTexture(_shadow.Texture, _shadow.Offset + SourceOamDrawOffset);
        DrawTexture(CurrentTexture, new Vector2(-16, -16 + Z) + SourceOamDrawOffset);
    }
}
