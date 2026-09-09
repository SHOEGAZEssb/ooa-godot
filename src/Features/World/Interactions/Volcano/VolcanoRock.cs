using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed partial class VolcanoRock : TransitionOffsetNode2D
{
    private readonly VolcanoDatabase _data;
    private readonly OracleRoomData _room;
    private readonly OracleRandom _random;
    private readonly Action<int> _sound;
    private readonly Func<Vector2, Vector2> _worldToScreen;
    private readonly EnemyAnimationPlayer _animation;
    private readonly EnemyAnimationPlayer _impact;
    private int _zFixed;
    private int _speedZ;
    private int _radiusY;
    private int _radiusX;
    private readonly TerrainShadowDefinition _shadow = TerrainShadow.Load();
    private int _frameCounter;
    internal int PartSlot { get; private set; }
    internal bool ShadowDrawn => State == 3 && Z < 0 &&
        ((_frameCounter ^ PartSlot) & 1) != 0 &&
        (_room.TilesetFlags & 0x20) == 0 &&
        16 - _worldToScreen(Vector2.Zero).Y < 0x97;
    internal int State { get; private set; }
    internal int Counter { get; private set; }
    internal int Z => _zFixed >> 8;
    internal int SpeedZ => _speedZ;
    internal int Angle { get; private set; }
    internal bool Finished { get; private set; }
    internal Texture2D CurrentTexture => ActiveAnimation.CurrentTexture;
    private EnemyAnimationPlayer ActiveAnimation => State == 5 ? _impact : _animation;
    internal Rect2 CollisionBounds => new(Position - new Vector2(_radiusX, _radiusY), new(_radiusX * 2, _radiusY * 2));

    internal VolcanoRock(Vector2 position, VolcanoDatabase data, OracleRoomData room,
        OracleRandom random, Action<int> sound, Func<Vector2, Vector2> worldToScreen)
    {
        Position = position; _data = data; _room = room; _random = random;
        _sound = sound; _worldToScreen = worldToScreen;
        Name = "VolcanoRock"; Visible = false; ZIndex = 20;
        _radiusX = _radiusY = data.Radius;
        var image = OracleGraphicsCache.LoadImage($"res://assets/oracle/gfx/{data.Sprite}.png");
        _animation = new(this, 4);
        _animation.Load(image, data.Animations, data.TileBase, data.Palette);
        _impact = new(this, 1);
        _impact.Load(image, [data.Animations[3]], 0x26, data.Palette);
    }

    internal void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        _frameCounter = frame.Counter;
        switch (State)
        {
            case 0:
                State = 1; _speedZ = -0x400;
                Angle = _random.Next().Value & 0x1f;
                _animation.SetAnimation(1); Visible = true;
                break;
            case 1:
                // Byte test occurs BEFORE integration. Launch only changes Z;
                // the random angle is consumed even though X/Y stay stationary.
                if (((((int)Position.Y + Z + 8) & 0xff)) < 0xf8)
                    OracleObjectMath.UpdateSpeedZ(ref _zFixed, ref _speedZ, 0x10);
                else
                {
                    State = 2; Counter = 30; Visible = false;
                    int value = _random.Next().Value;
                    Vector2 camera = new Vector2(0, OracleRoomData.StatusBarHeight) - _worldToScreen(Vector2.Zero);
                    int y = ((value & 0x70) + 8 + (int)camera.Y) & 0xff;
                    int x = (((value & 7) + 1) * 16 + 8 + (int)camera.X) & 0xff;
                    Position = new(x, y);
                    _zFixed = unchecked((short)((((-y) & 0xfe) << 8) | (_zFixed & 0xff)));
                    _animation.SetAnimation(2);
                }
                break;
            case 2:
                if (--Counter == 0) { Counter = 0x10; State = 3; Visible = true; ZIndex = 20; }
                break;
            case 3:
                _animation.Advance();
                _zFixed = unchecked((short)(_zFixed + 0x200));
                if (Z != 0) break;
                HazardType hazard = _room.GetTerrainInfo(Position).Hazard;
                if (hazard is HazardType.Water or HazardType.Lava or HazardType.Hole)
                {
                    spawns.Add(hazard == HazardType.Hole
                        ? new FallingDownHoleSpawn(Position)
                        : new EnemySplashSpawn(Position, hazard));
                    Finish();
                    break;
                }
                State = 4; _speedZ = 0; ZIndex = 10;
                break;
            case 4:
                _animation.Advance();
                if (OracleObjectMath.UpdateSpeedZ(ref _zFixed, ref _speedZ, 0x16))
                {
                    State = 5; _impact.SetAnimation(0); _sound(_data.Constant("impact"));
                }
                else Position += OracleObjectMovement.Shared.Delta(0x05, Angle);
                break;
            case 5:
                int parameter = _impact.CurrentParameter;
                if (parameter == 0xff) { Finish(); break; }
                if (!_data.ImpactRadii.TryGetValue(parameter, out var radii))
                    throw new InvalidOperationException($"volcanoRock.s: collision parameter ${parameter:x2} is unsupported.");
                (_radiusY, _radiusX) = radii;
                _impact.Advance();
                break;
            default: throw new InvalidOperationException($"PART_VOLCANO_ROCK $11:$01 state ${State:x2}.");
        }
        QueueRedraw();
    }

    internal void HandleLinkContact(Player player)
    {
        if (!Finished && State != 0 &&
            RoomEntityManager.ObjectCollisionZOverlaps(Z, player.TopDownAirZ, 7) &&
            player.OverlapsEnemyCollision(CollisionBounds, Z))
            player.ApplyEnemyContactDamage(Position, _data.Damage);
    }

    private void Finish() { Finished = true; Visible = false; }

    internal void SetPartSlot(int slot)
    {
        if ((uint)slot >= 16)
            throw new InvalidOperationException("PART_VOLCANO_ROCK $11: no free part slot in $d0..$df.");
        PartSlot = slot;
    }

    public override void _Draw()
    {
        if (!Finished)
        {
            // _drawObjectTerrainEffects uses world XY before the rock's Z.
            if (ShadowDrawn) DrawTexture(_shadow.Texture, _shadow.Offset + TransitionDrawOffset);
            DrawTexture(CurrentTexture, new Vector2(-16, -16 + Z) + TransitionDrawOffset);
        }
    }
}
