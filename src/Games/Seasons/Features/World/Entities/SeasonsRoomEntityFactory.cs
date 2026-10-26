using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed class SeasonsRoomEntityFactory(SeasonsOpeningSession session) : IRoomEntityFactory
{
    public string AssetRoot => SeasonsGameContent.AssetRoot;
    public int OpenShutterTile => 0xa0; // constants/common/tileIndices.s:TILEINDEX_STANDARD_FLOOR
    private readonly SeedTreeRefillData _refills = new(SeasonsGameContent.AssetRoot, 8);
    private readonly GeneratedTable _objects = GeneratedTable.Load(SeasonsGameContent.AssetRoot + "opening/room_objects.tsv",
        new("Seasons room object stream", GeneratedTableKeySemantics.Grouped,
            ["room", "order", "kind", "id", "subid", "y", "x", "target", "source"], ["room"], headerRequired: true));

    public IEnumerable<bool> PrepareResources() => [];
    public void BindEntity(IRoomEntity entity) { }
    public void RefreshState(IEnumerable<IRoomEntity> entities)
    {
        foreach (var entity in entities)
            if (entity is IRoomSaveStateEntity state) state.RefreshSaveState();
    }
    public IRoomEntity Create(RoomEntitySpawn spawn, OracleRoomData room) =>
        throw new NotSupportedException($"Seasons room $0:{room.Id:x2}: unbound spawn {spawn.GetType().Name}.");

    public IEnumerable<IRoomEntity> CreateRoomEntities(int group, OracleRoomData room, EnemyPlacementContext placement)
    {
        int order = 0;
        foreach (var row in _objects.Rows)
        {
            if (row.HexByte(0) != room.Id) continue;
            if (group != 0 || row.UnsignedDecimal(1) != order++) throw row.Invalid(1, "native room object order in group $00");
            string source = row.RequiredString(8);
            switch (row.RequiredString(2))
            {
                case "interaction":
                    if (!session.Entities.InteractionSlotAvailable) yield break;
                    yield return new SeasonsOpeningActor(session, row.HexByte(3), row.HexByte(4), new Vector2(row.HexByte(6), row.HexByte(5)), 0);
                    break;
                case "after-event":
                    if (session.Save.HasRoomFlag(group, room.Id, 0x80))
                        throw new NotSupportedException($"{source}: after-event stream {row.RequiredString(7)} is not implemented.");
                    break;
                default: throw row.Invalid(2, "an implemented object opcode");
            }
        }
        if (order == 0) throw new NotSupportedException($"objects/seasons/mainData.s: group{group:x1}Map{room.Id:x2}ObjectData is not imported.");
    }

    public IRoomEntity? CreateRoomLoadReward(int group, int room) => null;
    public void UpdateSeedTreeRefillState(int group, int room)
    {
        if (group == 0) _refills.UpdateRefillState(session.Memory, group, room);
    }
    public IEnumerable<IRoomEntity> ParseRandomEnemies(RoomObjectRecord source, OracleRoomData room,
        EnemyPlacementContext placement, EnemyPlacementReservations? reservations = null) =>
        throw new NotSupportedException($"{source.Source}: Seasons enemy placement is not implemented.");
}
