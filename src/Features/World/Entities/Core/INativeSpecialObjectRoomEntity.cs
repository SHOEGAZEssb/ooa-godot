using Godot;

namespace Oracles;

// A presentation bound to the one physical w1Companion slot. A vehicle's
// allocating interaction retains its own independent lifetime.
internal interface INativeSpecialObjectRoomEntity : IRoomEntity
{
    int NativeSpecialObjectId { get; }
    bool UsesSpecialObjectSlot => true;
    bool SupportsDebugReconstruction => false;
    Vector2 NativeSpecialObjectPosition { get; set; }
    int NativeSpecialObjectDirection => 0;
    bool MatchesNativeSpecialObjectId(int id) => id == NativeSpecialObjectId;
    void OnNativeSpecialObjectBound() { }
    void RestoreNativePresentation() { }
}
