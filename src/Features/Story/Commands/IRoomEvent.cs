using Godot;
using System;

namespace oracleofages;

internal interface IRoomEvent
{
    bool HasState { get; }
    bool BlocksGameplay { get; }
    void UpdateFrame();
    void Cancel();
}
