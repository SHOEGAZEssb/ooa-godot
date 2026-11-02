
namespace Oracles;

public sealed partial class RoomSession
{
    public AgesWorldData World => (_content as AgesRoomContent)?.World ??
        throw new NotSupportedException($"{AssetRoot}: Ages world content is not bound.");

    public void SetLayoutSwapped(int group, int room)
    {
        if (group is not (0 or 1))
            throw new ArgumentOutOfRangeException(
                nameof(group), "ROOMFLAG_LAYOUTSWAP only redirects overworld groups 0 and 1.");
        _saveData.SetRoomFlag(group, room, OracleSaveData.RoomFlagLayoutSwap);
    }
}
