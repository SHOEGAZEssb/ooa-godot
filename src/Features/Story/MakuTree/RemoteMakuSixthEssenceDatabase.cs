using System;

namespace oracleofages;

internal sealed class RemoteMakuSixthEssenceDatabase : RemoteMakuEventDatabase
{
    internal RemoteMakuSixthEssenceDatabase() : base("remote_maku_sixth_essence_event.tsv",
        "sixth-Essence remote Maku event","remote_maku_sixth_essence_commands.tsv")
    {
        if (Record is not {Group:3,Room:0x0f,InteractionId:InteractionId.RemoteMakuCutscene,
            SubId:1,Var03:8,EssenceMask:0x20,RequiredTreasure:0xff,RoomFlag:0x40,
            StandardTextId:0x05b8,LinkedTextId:0x05c8,StandardMapText:0xb8,LinkedMapText:0xc8,
            ConfettiKind:RemoteMakuConfettiKind.Past})
            throw new InvalidOperationException("INTERAC_REMOTE_MAKU_CUTSCENE $8a:$01/v$08: incomplete Mermaid's Cave exit mapping.");
    }
}
