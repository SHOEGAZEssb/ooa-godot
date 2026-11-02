
namespace Oracles;

public sealed partial class RoomEntityManager
{
    internal void UpdateNativeItemsPost()
    {
        // bank0.updateAllObjects: after bank1.func_4000, rider/held-object
        // copies and any yielded interactions, independently of main-item gates.
        foreach (object owner in _dynamicItems.LiveOwners())
            if (owner is INativeItemRoomEntity item) item.UpdateItemPost();
    }

    private void UpdateNativeItemsDuringScroll(RoomEntityFrame frame)
    {
        bool frozen = TextActiveSource() || _runtimeState.ReadField("wTextIsActive") != 0 || PaletteFadeActiveSource() ||
            _runtimeState.ReadField("wPaletteThread_mode") != 0 || _runtimeState.ReadField("wScrollMode") == 8 ||
            (NativeObjectMask & 0x90) != 0;
        foreach (object owner in _dynamicItems.LiveOwners())
            if (owner is INativeItemRoomEntity item && owner is IFixedRoomEntity handler &&
                (!frozen || item.NativeInitializationPending)) handler.UpdateFrame(frame, _pendingSpawns);
    }

    private void ValidateDebugNativeItems(OracleRuntimeState memory)
    {
        for (int page = 0xd7; page <= 0xdb; page++)
            if (memory.ReadWramByte(page << 8) != 0 && memory.ReadWramByte((page << 8) + 1) == ItemId.MinecartCollision &&
                !MinecartCollisionRoomEntity.CanReconstruct(memory, page, MinecartData.For(AssetRoot)))
                throw new NotSupportedException($"ITEM $1d slot${page:x2}: unsupported native debug state.");
    }

    private void RestoreDebugNativeItems()
    {
        if (!ReconstructingDebugObjects) return;
        ValidateDebugNativeItems(_runtimeState);
        for (int page = 0xd7; page <= 0xdb; page++)
            if (_runtimeState.ReadWramByte(page << 8) != 0 && _runtimeState.ReadWramByte((page << 8) + 1) == ItemId.MinecartCollision)
                AddEntity(new MinecartCollisionRoomEntity(_runtimeState, MinecartData.For(AssetRoot), page));
    }
}
