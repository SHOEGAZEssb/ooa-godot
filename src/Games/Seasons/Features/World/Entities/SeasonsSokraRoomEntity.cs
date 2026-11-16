namespace Oracles;

// object_code/seasons/interactions/sokra.s: interactionCode27.
internal sealed class SeasonsSokraRoomEntity : NativeNpcRoomEntity
{
    private readonly SeasonsOpeningSession _session;
    private readonly SeasonsTalkNpcDatabase _data;
    private readonly NativeObjectInstruction _source;
    private readonly OracleRoomData _room;

    internal SeasonsSokraRoomEntity(SeasonsOpeningSession session, SeasonsTalkNpcDatabase data,
        NativeObjectInstruction source, OracleRoomData room)
        : base(data.Create(source, room, session.Data.World.SpritePalettes), session.Memory, session.Link,
            index => data.Animation(source.Id, index), data.Text)
    {
        _session = session;
        _data = data;
        _source = source;
        _room = room;
    }

    protected override bool InitializeNpc(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (_source.SubId == 2 && !_session.Save.HasGlobalFlag(SeasonsGlobalFlag.FinishedGame))
            _session.Save.SetRoomFlag(_room.Group, _room.Id, 0x40, (_session.Save.ReadField("wObtainedSeasons") & 2) == 0);
        if (_source.SubId == 1 && !_session.Save.HasGlobalFlag(SeasonsGlobalFlag.FinishedGame) &&
            ((_session.Save.ReadField("wObtainedSeasons") & 8) == 0 || _session.Save.HasRoomFlag(_room.Group, _room.Id, 0x40)))
        {
            _session.Entities.ParseNativeObjectStream("objectData7e4a", EnemyPlacementContext.Unrestricted);
            Delete();
            return false;
        }
        var selected = _data.Select(Entity.Record, _session.Save, _session.Memory);
        if (!selected.Visible) { Delete(); return false; }
        WriteByte(0x44, 1);
        Entity.SetScriptVisible(true);
        Entity.ZIndex = ObjectDrawPriority.BehindLinkZIndex;
        Script = new SeasonsSokraScriptHost(this, _session, _data, selected.Script);
        WriteByte(0x73, 0x52);
        Script.AdvanceObjectUpdate(frame.Player);
        Script.AdvanceObjectUpdate(frame.Player);
        return false;
    }

    protected override bool UpdateNativeNpc(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        var player = frame.Player;
        // State $01 preserves a different script/animation order for each subid.
        if (_source.SubId == 0)
        {
            Script!.AdvanceObjectUpdate(player);
            Entity.AnimateAsNpcOneUpdate(player);
            if (!_session.Save.HasTreasure(TreasureId.SeedSatchel)) return true;
            if (_session.Save.HasRoomFlag(_room.Group, _room.Id, 0x40))
            {
                if (ReadByte(0x43) != 0 && (_session.FrameCounter & 0x3f) == 0)
                    FloatingMusicNoteRoomEntity.Spawn(_session.Entities, Entity.Position + new Vector2(-6, -12), true);
            }
            else if ((int)player.Position.X >= 0x78 && (int)player.Position.Y is >= 0x3c and < 0x60)
                WriteByte(0x77, 1);
            return true;
        }
        if (_source.SubId == 2 && (_session.DisabledObjects & 1) == 0) SpawnSnore();
        Entity.AnimateAsNpcOneUpdate(player);
        Script!.AdvanceObjectUpdate(player);
        if (_source.SubId != 1 || Finished || ReadByte(0x45) != 0 || (int)player.Position.X < 0x18) return true;
        WriteByte(0x45, 1);
        WriteByte(0x77, 1);
        WriteByte(0x54, 0x40);
        WriteByte(0x55, 0xff);
        return true;
    }

    internal void SpawnSnore()
    {
        if ((_session.FrameCounter & 0x3f) == 0)
            FloatingMusicNoteRoomEntity.Spawn(_session.Entities, Entity.Position + new Vector2(-4, -6), true, "SokraSnore", subid: 0);
    }

    internal void AdvanceJump()
    {
        Move(ReadByte(0x50), ReadByte(0x49));
        int z = unchecked((short)(ReadByte(0x4e) | (ReadByte(0x4f) << 8)));
        int speed = unchecked((short)(ReadByte(0x54) | (ReadByte(0x55) << 8)));
        OracleObjectMath.UpdateSpeedZ(ref z, ref speed, 0x10);
        WriteByte(0x4e, z);
        WriteByte(0x4f, z >> 8);
        WriteByte(0x54, speed);
        WriteByte(0x55, speed >> 8);
        Entity.SetScriptDrawOffset(new(0, unchecked((sbyte)(z >> 8))));
    }
}
