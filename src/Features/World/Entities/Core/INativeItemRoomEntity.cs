namespace Oracles;

internal interface INativeItemRoomEntity : IRoomEntity
{
    int NativeItemId { get; }
    bool NativeInitializationPending { get; }
    int? NativeReconstructionSlot => null;
    void BindNativeItemSlot(int page);
    void ReleaseNativeItemSlot();
    void UpdateItemPost();
}
