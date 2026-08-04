using Godot;

namespace oracleofages;

internal interface ICompanionBarrierTarget
{
    int CompanionId { get; }
    bool BarrierMounted { get; }
    Vector2 BarrierPosition { get; }
    void ClampToLowerY(int y);
}
