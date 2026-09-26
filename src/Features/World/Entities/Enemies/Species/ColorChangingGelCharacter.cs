using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal partial class ColorChangingGelCharacter : EnemyCharacter
{
    private readonly ColorChangingGelBehaviorProfile _behavior =
        EnemyBehaviorTables.Shared.ColorChangingGel;

    private OracleRandom _random = null!;
    private OracleRoomData _room = null!;
    private ColorChangingGelState _state;
    private int _counter;
    private int _colorCounter;
    private int _zFixed;
    private int _speedZ;
    private Vector2 _target;
    private byte _storedTile;
    private int _color;
    private Action<int> _sound = null!;
    private int _pendingAttack;

    internal ImportedEnemyDefinition Record { get; private set; }
    internal int Color => _color;
    internal int ZHigh => _zFixed >> 8;
    internal int CollisionMode { get; private set; }
    internal ColorChangingGelState State => _state;
    internal int Counter => _counter;
    internal int ColorCounter => _colorCounter;
    internal int StoredTile => _storedTile;
    internal Vector2 HopTarget => _target;
    internal bool NormalDeathDispatched { get; private set; }
    internal override Texture2D CurrentDrawTexture =>
        DrawsDamagePalette ? Animation.DamageTexture : Animation.CurrentTextureForPalette(_color);
    protected override Vector2 AnimationDrawOffset =>
        new(-16, -16 + ZHigh);

    internal void Initialize(
        ImportedEnemyDefinition record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random,
        IReadOnlyDictionary<int, Color[]> paletteOverrides,
        Action<int> sound)
    {
        Record = record;
        _room = room;
        _random = random;
        _sound = sound;
        _state = ColorChangingGelState.Uninitialized;
        _color = record.Palette;
        CollisionMode = _behavior.ImmuneCollisionMode;
        InitializeEnemy(
            position,
            EnemyCharacterConfiguration.FromImported(record),
            initialAnimation: 0,
            paletteOverrides: paletteOverrides,
            paletteVariants: [1, 6]);
        ConfigureHazards(room, zPosition: () => ZHigh);
        Visible = false;
    }

    internal void UpdateFrame()
    {
        if (IsDead) return;
        if (_state == ColorChangingGelState.Uninitialized)
        {
            // enemyStandardUpdate reloads properties and consumes var3d RNG
            // on every state0 dispatch, including color-delay retries.
            _random.Next();
            Health = Record.Health;
            _color = Record.Palette;
            CollisionMode = _behavior.ImmuneCollisionMode;
            RestartAnimation(0);
        }
        if (BeginFrame()) return;
        if (CheckHazards())
            return;
        if (_pendingAttack != 0)
        {
            int attack = _pendingAttack;
            _pendingAttack = 0;
            if (attack == 0x1a)
            {
                _color = _behavior.RandomColors[(_color & ~1) + (_random.Next().Value & 1)].Value;
                QueueRedraw();
            }
            else if (CollisionMode == _behavior.ImmuneCollisionMode && (attack == 0x0d || attack is >= 4 and <= 9))
            {
                InvincibilityCounter = -_behavior.DeflectionInvincibilityFrames;
                _sound(SoundId.SndDamageEnemy);
                // Native signed-counter advancement follows the handler.
                AdvanceInvincibilityCounter();
            }
        }
        else if (Health == 0) { NormalDeathDispatched = true; Finish(); return; }
        if (!UpdateColor()) return;

        switch (_state)
        {
            case ColorChangingGelState.Uninitialized:
                _state = ColorChangingGelState.Waiting;
                _counter = _behavior.WaitFrames;
                _colorCounter = 0;
                _storedTile = _room.GetMetatile(Position);
                CollisionMode = _behavior.ImmuneCollisionMode;
                RestartAnimation(3);
                Visible = true;
                return;

            case ColorChangingGelState.Waiting:
                if (--_counter != 0)
                    return;
                _counter = 1;
                int index = _random.Next().Value & 0x0e;
                _target = new Vector2(
                    ((int)Mathf.Floor(Position.X) + _behavior.HopOffsets[index + 1].Value) & 0xff,
                    ((int)Mathf.Floor(Position.Y) + _behavior.HopOffsets[index].Value) & 0xff);
                // getTileCollisionsAtPosition tests the entire collision byte.
                if (_room.GetTerrainInfo(_target).Collision != 0)
                {
                    _counter = 1;
                    return;
                }
                _state = ColorChangingGelState.Preparing;
                _counter = _behavior.HopDelayFrames;
                _speedZ = _behavior.InitialSpeedZ;
                SetAnimation(2);
                return;

            case ColorChangingGelState.Preparing:
                if (--_counter != 0)
                {
                    AdvanceAnimation();
                    return;
                }
                _state = ColorChangingGelState.Hopping;
                int angle = OracleObjectMovement.Shared.RelativeAngle(
                    Position, _target);
                SetAnimation((angle & 0x10) >> 4);
                return;

            case ColorChangingGelState.Hopping:
                bool landed = OracleObjectMath.UpdateSpeedZ(
                    ref _zFixed,
                    ref _speedZ,
                    _behavior.Gravity);
                QueueRedraw();
                if (!landed)
                {
                    Vector2 delta = Position.Floor() - _target;
                    if ((((int)delta.X + 1) & 0xff) < 3 && (((int)delta.Y + 1) & 0xff) < 3) return;
                    int angleToTarget =
                        OracleObjectMovement.Shared.RelativeAngle(
                            Position, _target);
                    // ecom_moveTowardPosition calls objectApplySpeed after
                    // state 8 already rejected a solid target tile.
                    Position += OracleObjectMovement.Shared.Delta(
                        _behavior.SpeedRaw,
                        angleToTarget);
                    Position = new Vector2((Position.X + 256) % 256, (Position.Y + 256) % 256);
                    QueueRedraw();
                    return;
                }
                Position = new Vector2(
                    Mathf.Floor(Position.X / 16.0f) * 16.0f + 8.0f,
                    Mathf.Floor(Position.Y / 16.0f) * 16.0f + 8.0f);
                _state = ColorChangingGelState.Waiting;
                _counter = _behavior.WaitFrames;
                SetAnimation(3);
                return;
        }
    }

    internal override bool TakeSwordHit(Vector2 _, int damage) => TakeAttack(4, damage);

    internal bool TakeSwitchHookHit(int damage) => TakeAttack(0x0d, damage);

    internal bool TakeMysterySeedHit() => TakeAttack(0x1a, 0);

    internal bool TakeSeedHit(int collisionType, int damage) => TakeAttack(collisionType, damage);

    private bool TakeAttack(int attack, int damage)
    {
        if (!CollisionEnabled || InvincibilityCounter != 0) return false;
        _pendingAttack = attack;
        if (attack != 0x1a && CollisionMode == _behavior.VulnerableCollisionMode)
        {
            // ENEMYDMG_$0c writes health, $20 invincibility and var2a.
            // JUST_HIT dispatch still precedes health-zero death next update.
            Health = Math.Max(0, Health - damage);
            InvincibilityCounter = 32;
            _sound(SoundId.SndDamageEnemy);
        }
        return true;
    }

    internal override bool TakeBurnHit(int _) => false;

    private bool UpdateColor()
    {
        if (_zFixed < 0)
            return true;
        if (_colorCounter > 0 && --_colorCounter == 1)
        {
            int color = FloorColor(_storedTile);
            if (_color != color)
            {
                _color = color;
                QueueRedraw();
            }
        }
        if (_colorCounter > 1)
        {
            UpdateImmunity();
            return false; // pop bc discards the state-dispatch return address.
        }
        if (UpdateImmunity()) return true;
        _storedTile = _room.GetMetatile(Position);
        _colorCounter = _behavior.ColorDelayFrames;
        return false;
    }

    private bool UpdateImmunity()
    {
        byte tile = _room.GetMetatile(Position);
        // Somaria returns Z without writing enemyCollisionMode.
        if (tile == _behavior.SomariaTile) return true;
        bool immune = FloorColor(tile) == _color;
        CollisionMode = immune ? _behavior.ImmuneCollisionMode : _behavior.VulnerableCollisionMode;
        return immune;
    }

    private int FloorColor(byte tile)
    {
        for (int i = 0; i < _behavior.FloorColors.Count; i += 2)
            if (_behavior.FloorColors[i].Value == tile) return _behavior.FloorColors[i + 1].Value;
        return _behavior.DefaultColor;
    }
}

internal enum ColorChangingGelState
{
    Uninitialized,
    Waiting = 8,
    Preparing,
    Hopping
}
