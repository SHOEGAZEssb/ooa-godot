using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Native INTERAC_FOREST_FAIRY $49:$00 movement. Coordinates and velocities
/// retain the original unsigned high-byte checks and signed 8.8 word wrapping.
/// </summary>
internal sealed class ForestFairyFlight
{
    private readonly FairiesWoodsDatabase _database;
    private readonly FairiesWoodsEventRecord _event;
    private readonly OracleRuntimeState _runtime;
    private readonly OracleSoundEngine _sound;
    private readonly FairiesWoodsSparkleLayer _sparkles;
    private readonly Action<Vector2> _spawnPuff;
    private int _presetIndex;
    private int _yFixed;
    private int _xFixed;
    private int _targetY;
    private int _targetX;
    private int _angle;
    private int _direction;
    private int _counter1;
    private int _counter2;
    private int _sparkleCounter = 0x5a;
    private FairyFlightStage _stage = FairyFlightStage.FirstLeg;
    private bool _stateZeroPending = true;

    internal NpcCharacter Actor { get; }
    internal bool Active { get; private set; } = true;
    internal int PresetIndex => _presetIndex;
    internal int Angle => _angle;
    internal int Direction => _direction;
    internal int Counter1 => _counter1;
    internal int Counter2 => _counter2;
    internal int SparkleCounter => _sparkleCounter;
    internal int YFixed => unchecked((short)_yFixed);
    internal int XFixed => unchecked((short)_xFixed);
    internal int Stage => (int)_stage;

    internal ForestFairyFlight(
        FairiesWoodsDatabase database,
        OracleRuntimeState runtime,
        OracleSoundEngine sound,
        FairiesWoodsSparkleLayer sparkles,
        NpcCharacter actor,
        int presetIndex,
        Action<Vector2> spawnPuff)
    {
        _database = database;
        _event = database.Event;
        _runtime = runtime;
        _sound = sound;
        _sparkles = sparkles;
        _spawnPuff = spawnPuff;
        Actor = actor;
        _presetIndex = presetIndex;
        LoadPreset(setPosition: true);
    }

    internal void UpdateFrame(int globalFrame)
    {
        if (!Active)
            return;
        if (_stateZeroPending)
        {
            _stateZeroPending = false;
            return;
        }

        switch (_stage)
        {
            case FairyFlightStage.FirstLeg:
                if (UpdateFlight(globalFrame))
                {
                    SnapToTarget();
                    _stage = FairyFlightStage.WaitForSignal;
                }
                break;

            case FairyFlightStage.WaitForSignal:
                if (Signal != 0)
                {
                    Actor.AdvanceAnimationUpdates(1);
                    break;
                }
                if (_presetIndex >= 6)
                {
                    SpawnPuffAndDelete();
                    break;
                }
                _presetIndex += 6;
                _stage = FairyFlightStage.SecondLeg;
                LoadPreset(setPosition: true);
                break;

            case FairyFlightStage.SecondLeg:
                if (UpdateFlight(globalFrame))
                {
                    Delete();
                    break;
                }
                if (PositionY >= 0x80 || PositionX >= 0xa0)
                    Signal = unchecked((byte)(Signal + 1));
                break;

        }
    }

    internal void Deactivate()
    {
        if (!Active)
            return;
        Active = false;
        if (GodotObject.IsInstanceValid(Actor))
            Actor.SetActive(false);
    }

    private bool UpdateFlight(int globalFrame)
    {
        if (WithinFour(PositionX, _targetX) &&
            WithinFour(PositionY, _targetY))
        {
            Signal = unchecked((byte)(Signal + 1));
            return true;
        }

        _sparkleCounter = unchecked((byte)(_sparkleCounter - 1));
        int sparkleTest = _sparkleCounter;
        if (_sparkleCounter == 0)
        {
            _sparkleCounter = 0x5a;
            _counter2 = (_counter2 >> 1) + (_counter2 & 1);
        }
        if ((sparkleTest & 7) == 0)
            _sparkles.Spawn(Actor.Position);

        _counter1--;
        if (_counter1 == 0)
        {
            _counter1 = _counter2;
            int target = OracleObjectMovement.Shared.RelativeAngle(
                (byte)PositionY,
                (byte)PositionX,
                (byte)_targetY,
                (byte)_targetX);
            int difference = (_angle - target) & 0x1f;
            if (difference != 0)
            {
                _angle = difference < 0x10
                    ? (_angle - 1) & 0x1f
                    : (_angle + 1) & 0x1f;
            }
        }

        ApplySpeed();
        if ((globalFrame & 0x1f) == 0)
            _sound.PlaySound(_event.MagicSound);
        Actor.AdvanceAnimationUpdates(1);
        return false;
    }

    private void LoadPreset(bool setPosition)
    {
        if (_presetIndex < 0 || _presetIndex >= _database.Movements.Count)
        {
            throw new InvalidOperationException(
                $"Forest fairy requested movement preset ${_presetIndex:x2}.");
        }
        FairiesWoodsMovementRecord movement =
            _database.Movements[_presetIndex];
        if (setPosition)
        {
            _yFixed = (movement.InitialY << 8) | (_yFixed & 0xff);
            _xFixed = (movement.InitialX << 8) | (_xFixed & 0xff);
        }
        _targetY = movement.TargetY;
        _targetX = movement.TargetX;
        _angle = movement.Angle;
        _counter1 = movement.Counter;
        _counter2 = movement.Counter;
        _direction = movement.Direction;
        Actor.SetBasePalette(movement.Palette);
        Actor.SetScriptAnimation(
            movement.Direction == 0 ? _event.Animation0 : _event.Animation1);
        UpdateActorPosition();
    }

    private void ApplySpeed()
    {
        OracleObjectPosition position = OracleObjectMovement.Shared.ApplySpeed(
            new OracleObjectPosition(
                unchecked((ushort)_yFixed),
                unchecked((ushort)_xFixed)),
            0x50,
            _angle);
        _yFixed = position.YFixed;
        _xFixed = position.XFixed;
        UpdateActorPosition();
    }

    private void SnapToTarget()
    {
        _yFixed = (_targetY << 8) | (_yFixed & 0xff);
        _xFixed = (_targetX << 8) | (_xFixed & 0xff);
        UpdateActorPosition();
    }

    private void UpdateActorPosition()
    {
        Actor.Position = new Vector2(PositionX, PositionY);
        Actor.SetScriptDrawOffset(new Vector2(0, -4));
    }

    private void SpawnPuffAndDelete()
    {
        _spawnPuff(Actor.Position);
        Delete();
    }

    private void Delete()
    {
        Active = false;
        if (GodotObject.IsInstanceValid(Actor))
            Actor.SetActive(false);
    }

    private byte Signal
    {
        get => _runtime.ReadWramByte(_event.SignalAddress);
        set => _runtime.SetWramByte(_event.SignalAddress, value);
    }

    private int PositionY => (byte)(_yFixed >> 8);
    private int PositionX => (byte)(_xFixed >> 8);

    private static bool WithinFour(int current, int target) =>
        unchecked((byte)(current - target + 4)) < 9;

}

internal enum FairyFlightStage
{
    FirstLeg,
    WaitForSignal,
    SecondLeg
}
