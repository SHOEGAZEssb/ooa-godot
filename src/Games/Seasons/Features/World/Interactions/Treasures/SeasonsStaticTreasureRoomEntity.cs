namespace Oracles;

// miscellaneous1.s:sidescrollingStaticSeedSatchel deletes after its checked allocation, including failure.
internal sealed partial class SeasonsStaticTreasureRoomEntity : Node2D,
    INativeInteractionRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private readonly RoomEntityManager _world;
    private readonly GroundTreasureGrantRequest _request;
    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => true;
    public bool UpdatesDuringRoomEntityFreeze => true;

    internal SeasonsStaticTreasureRoomEntity(NativeObjectInstruction source,OracleRoomData room,RoomEntityManager world)
    {
        _world=world;Position=new(source.X,source.Y);Visible=false;
        var row=GeneratedTable.Load(world.AssetRoot+"objects/static_seed_satchel.tsv",
            new("Seasons static Seed Satchel",GeneratedTableKeySemantics.Unique,
                ["subid","treasure","treasure-subid","source"],["subid"],headerRequired:true)).SingleRow();
        if(source.SubId!=row.HexByte(0))throw row.Invalid(0,$"INTERAC$6b:${source.SubId:x2} producer");
        var treasure=world.Treasures.GetObject(row.HexByte(1),row.HexByte(2));
        _request=new(room.Group,room.Id,source.Address,source.Y,source.X,treasure.Name,row.RequiredString(3))
        {
            SpawnMode=(treasure.NativeFlags>>4)&7,GrabMode=treasure.NativeFlags&7,
            RoomFlagTiming=(treasure.NativeFlags&8)!=0?GroundTreasureRoomFlagTiming.OnActivation:GroundTreasureRoomFlagTiming.Never
        };
    }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(Finished)return;
        if(_world.SaveData?.HasRoomFlag(_request.Group,_request.Room,OracleSaveData.RoomFlagItem)!=true)
            _world.TryCreateGroundTreasure(_request);
        Finished=true;
    }
    public void SetTransitionDrawOffset(Vector2 offset) { }
}
