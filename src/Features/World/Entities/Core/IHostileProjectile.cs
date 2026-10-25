using Godot;

namespace Oracles;

internal interface IHostileProjectile
{
    bool Finished { get; }
    bool InitializationPending { get; }
    Rect2 CollisionBounds { get; }
    void UpdateFrame(Player player);
    bool DeflectWithSword();
}
