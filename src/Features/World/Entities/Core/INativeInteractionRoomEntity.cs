namespace Oracles;

internal interface INativeInteractionRoomEntity : IRoomEntity
{
    bool NativeInitializationPending => false;
    int? NativeReconstructionSlot => null;
    void BindNativeInteractionSlot(int slot) { }
    void ReleaseNativeInteractionSlot() { }
}
