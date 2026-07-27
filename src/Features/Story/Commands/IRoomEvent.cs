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

/// <summary>
/// Reduced room-event counterpart to the original textbox update paths.
/// Event-owned actors otherwise receive no handler call while wTextIsActive
/// is nonzero; implementations expose only enabled-bit-7 objects or work
/// owned by a source handler outside the ordinary object dispatchers.
/// </summary>
internal interface IUpdatesDuringDialogueRoomEvent
{
    void UpdateDuringDialogueFrame();
}
