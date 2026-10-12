using System;

namespace oracleofages;

internal sealed class RemoteMakuSeventhEssenceDatabase : RemoteMakuEventDatabase
{
    internal RemoteMakuSeventhEssenceDatabase() : base("remote_maku_seventh_essence_event.tsv",
        "seventh-Essence remote Maku event", "remote_maku_seventh_essence_commands.tsv")
    {
        if (Record is not { Group: 2, Room: 0x90, InteractionId: InteractionId.RemoteMakuCutscene,
            SubId: 0, Var03: 0x09, EssenceMask: 0x40, RequiredTreasure: 0xff, RoomFlag: 0x40,
            StandardTextId: 0x05b9, LinkedTextId: 0x05c9, StandardMapText: 0xb9, LinkedMapText: 0xc9,
            ConfettiKind: RemoteMakuConfettiKind.Present })
            throw new InvalidOperationException("INTERAC_REMOTE_MAKU_CUTSCENE $8a:$00/v$09: incomplete post-D7 return mapping.");
    }
}
