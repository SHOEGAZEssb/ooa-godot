using Godot;

namespace oracleofages;

/// <summary>
/// Original 8.8 enemy Z position and speed integration with a fixed gravity.
/// </summary>
internal sealed class EnemyVerticalMotion(Node2D entity, int gravity)
{
    private int _zFixed;
    private int _speedZ;

    public int ZFixed
    {
        get => _zFixed;
        set => _zFixed = value;
    }

    public int SpeedZ
    {
        get => _speedZ;
        set => _speedZ = value;
    }

    public void Reset()
    {
        ZFixed = 0;
        SpeedZ = 0;
    }

    public bool Update()
    {
        bool landed = OracleObjectMath.UpdateSpeedZ(
            ref _zFixed, ref _speedZ, gravity);
        if (landed)
            SpeedZ = 0;
        entity.QueueRedraw();
        return landed;
    }
}
