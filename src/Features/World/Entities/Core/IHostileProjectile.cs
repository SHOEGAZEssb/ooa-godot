using Godot;

namespace oracleofages;

internal interface IHostileProjectile
{
    bool Finished { get; }
    Rect2 CollisionBounds { get; }
    void UpdateFrame(Player player);
    bool DeflectWithSword();
}
