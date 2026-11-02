
namespace Oracles;

internal interface IBiggoronSwordCollisionRoomEntity
{
    bool ApplyBiggoronSwordCollision(Rect2 bounds,Vector2 origin,int damage,ICollection<RoomEntitySpawn> spawns);
}
