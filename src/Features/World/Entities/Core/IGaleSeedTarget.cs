using Godot;
using System;

namespace Oracles;

internal interface IGaleSeedTarget
{
    bool GaleCaught { get; }
    bool TryCatchGale(Rect2 hitbox, int seedZ, Func<byte> random);
    void UpdateGale(int cameraY);
}
