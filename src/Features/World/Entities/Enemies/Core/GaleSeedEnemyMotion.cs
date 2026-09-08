using Godot;
using System;

namespace oracleofages;

// collisionEffect29 and ecom_galeSeedEffect. Completion policy belongs to
// the native enemy adapter (some linked actors also notify their spawner).
internal sealed class GaleSeedEnemyMotion(EnemyCharacter entity)
{
    private int _counter;
    private int _z;
    private int _speedZ;
    private int _angle;
    private Vector2 _position;
    internal bool Active { get; private set; }
    internal int Counter => _counter;
    internal int ZFixed => _z;

    internal void Begin(Vector2 seedPosition, int z, Func<byte> random)
    {
        Active = true;
        _counter = 0x1e;
        _z = (z < 0 ? z : -1) << 8;
        _speedZ = -0x600;
        _angle = random() & 0x18;
        _position = new Vector2((int)seedPosition.X + entity.Position.X % 1,
            (int)seedPosition.Y + entity.Position.Y % 1);
        entity.Position = OracleObjectMath.ToPixelPosition(_position);
        entity.GaleDrawZ = _z >> 8;
        entity.GaleCollisionDisabled = true;
        entity.ZIndex = 11;
        entity.QueueRedraw();
    }

    internal void Update(int cameraY)
    {
        if (_counter > 0) _counter--;
        if (_counter != 0)
        {
            int dx = (_counter & 3) is 0 or 3 ? -2 : 2;
            _position.X = (((int)_position.X + dx) & 0xff) + _position.X % 1;
            entity.Position = OracleObjectMath.ToPixelPosition(_position);
        }
        else
        {
            entity.Position = OracleObjectMovement.Shared.ApplySpeed(ref _position, 0x05, _angle);
            OracleObjectMath.UpdateSpeedZ(ref _z, ref _speedZ, 0x10);
            int z = (_z >> 8) & 0xff;
            if (z < 0x80 || (((int)entity.Position.Y + z - cameraY) & 0xff) >= 0xb0)
            {
                entity.FinishGale();
                if (!entity.IsDead)
                {
                    Active = false;
                    entity.GaleCollisionDisabled = false;
                    entity.GaleDrawZ = 0;
                    return;
                }
            }
        }
        entity.GaleDrawZ = _z >> 8;
        entity.QueueRedraw();
    }
}
