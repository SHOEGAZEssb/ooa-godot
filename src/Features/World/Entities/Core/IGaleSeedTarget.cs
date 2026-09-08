using Godot;
using System;

namespace oracleofages;

internal interface IGaleSeedTarget
{
    bool GaleCaught { get; }
    bool TryCatchGale(Rect2 hitbox, int seedZ, Func<byte> random);
    void UpdateGale(int cameraY);
}
