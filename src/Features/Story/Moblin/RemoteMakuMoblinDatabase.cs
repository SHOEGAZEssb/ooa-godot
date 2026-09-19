namespace oracleofages;

internal sealed class RemoteMakuMoblinDatabase : RemoteMakuEventDatabase
{
    internal RemoteMakuMoblinDatabase():base("remote_maku_moblin_event.tsv","Moblin remote Maku","remote_maku_moblin_commands.tsv")
    {
        if(Record is not {Group:0,Room:9,Var03:6,StandardTextId:0x05b6,LinkedTextId:0x05c6})
            throw new System.InvalidOperationException("INTERAC $8a/v$06 identity changed.");
    }
}
