
namespace Oracles;

internal interface IRoomEntityLifetime
{
    bool Finished { get; }
    void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
}
