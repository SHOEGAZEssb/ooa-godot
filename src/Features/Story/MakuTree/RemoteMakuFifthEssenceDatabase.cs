using System;
namespace oracleofages;
internal sealed class RemoteMakuFifthEssenceDatabase : RemoteMakuEventDatabase
{
    internal RemoteMakuFifthEssenceDatabase():base("remote_maku_fifth_essence_event.tsv",
        "fifth-Essence remote Maku event","remote_maku_fifth_essence_commands.tsv")
    {
        if(Record is not {Group:0,Room:0x0a,InteractionId:0x8a,SubId:0,Var03:7,
            EssenceMask:0x10,RoomFlag:0x40,StandardTextId:0x05b7,LinkedTextId:0x05c7})
            throw new InvalidOperationException("INTERAC_REMOTE_MAKU_CUTSCENE $8a/v$07: incomplete Crown Dungeon exit mapping.");
    }
}
