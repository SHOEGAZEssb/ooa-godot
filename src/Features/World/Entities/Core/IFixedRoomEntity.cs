
namespace Oracles;

internal interface IFixedRoomEntity
{
    void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns);
}
