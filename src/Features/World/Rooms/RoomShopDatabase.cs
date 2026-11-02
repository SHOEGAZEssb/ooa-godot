
namespace Oracles;

// roomGfxChanges.s:applyRoomSpecificTileChangesAfterGfxLoad and handler$04.
// Source graphics initialization sets wInShop independently of NPC presence.
internal sealed class RoomShopDatabase
{
    public string AssetRoot { get; }

    private readonly byte[] _flags ;

    internal RoomShopDatabase(string assetRoot)
    {
        AssetRoot = assetRoot;
        _flags = OracleAssetCache.ReadBytes(AssetRoot + "metadata/roomShopFlags.bin");

        if (_flags.Length != 8 * 256)
            throw new InvalidOperationException("roomGfxChanges.s: roomShopFlags.bin requires eight 256-room groups.");
        int shops = 0;
        for (int index = 0; index < _flags.Length; index++)
        {
            if (_flags[index] == 2) shops++;
            else if (_flags[index] != 0)
                throw new InvalidOperationException($"roomGfxChanges.s: invalid initial shop flags${_flags[index]:x2} in room${index >> 8:x}:${index & 255:x2}.");
        }
        int expected = assetRoot == SeasonsGameContent.AssetRoot ? 12 : 4;
        if (shops != expected)
            throw new InvalidOperationException($"roomGfxChanges.s: expected{expected} handler$04 shop initializations.");
    }

    internal byte InitialFlags(int group,int room)
    {
        if (group is < 0 or > 7 || room is < 0 or > 255)
            throw new ArgumentOutOfRangeException(nameof(group),$"Invalid shop initialization room${group:x}:${room:x2}.");
        return _flags[group * 256 + room];
    }
}
