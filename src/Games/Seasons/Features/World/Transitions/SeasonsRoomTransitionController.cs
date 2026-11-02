
namespace Oracles;

internal sealed class SeasonsRoomTransitionController : RoomTransitionController
{
    private readonly SeasonsWorldData _data;

    internal SeasonsRoomTransitionController(RoomSession rooms, SeasonsWorldData data, GameplaySceneGraph scene,
        RoomEntityManager entities, DeathRespawnPointController checkpoints, OracleSoundEngine sound)
        : base(RequireSeasons(rooms),WarpDatabase.For(rooms.AssetRoot),scene.RoomView,scene.RoomLoadReveal,
            scene.Player,scene.RoomCamera,scene.WarpFade,scene.Hud,scene.Dialogue,entities,checkpoints,sound)
        => _data = data;

    private static RoomSession RequireSeasons(RoomSession rooms) => rooms.SaveData.Game == OracleGame.Seasons ? rooms :
        throw new ArgumentException("Seasons transition rules require Seasons room content.",nameof(rooms));

    private protected override void PrepareRoomPackTransition(int targetId)
    {
        // bank1.checkRoomPack commits the pack/season before CUTSCENE_05's fade.
        int pack = _data.RoomPack(targetId);
        _entities.RuntimeState.SetField("wLoadingRoomPack",(byte)pack);
        _entities.RuntimeState.SetField("wRoomPack",(byte)pack);
        bool spring = _rooms.SaveData.HasGlobalFlag(SeasonsGlobalFlag.SeasonAlwaysSpring);
        int season = pack == 0 ? spring ? 0 : _entities.Random.Next().Value & 3 :
            _data.DefaultSeason(spring && pack < 0xf0 ? pack & 15 : pack,_rooms.SaveData.ReadField("wAnimalCompanion"));
        _entities.RuntimeState.SetField("wRoomStateModifier",(byte)season);
    }

    private protected override OracleRoomData LoadDestinationRoom(Warp warp)
    {
        if (warp.DestinationGroup == 0)
        {
            int pack = _data.RoomPack(warp.DestinationRoom);
            _entities.RuntimeState.SetField("wLoadingRoomPack",(byte)pack);
            _entities.RuntimeState.SetField("wRoomPack",(byte)pack);
            // applyWarpDest_b04 checks the source group and source dungeon index.
            int sourceGroup = _rooms.ActiveGroup;
            if (!RoomPackTransitionActive && !warp.PreserveDestinationSeason &&
                (sourceGroup < 3 || sourceGroup > 3 && _rooms.CurrentDungeonIndex >= 0))
            {
                bool spring = _rooms.SaveData.HasGlobalFlag(SeasonsGlobalFlag.SeasonAlwaysSpring);
                if (spring || pack != 0)
                    _entities.RuntimeState.SetField("wRoomStateModifier",(byte)_data.DefaultSeason(
                        spring && pack < 0xf0 ? pack & 15 : pack,_rooms.SaveData.ReadField("wAnimalCompanion")));
            }
        }
        return base.LoadDestinationRoom(warp);
    }
}
