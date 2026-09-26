using System;

namespace oracleofages;

internal sealed class RemoteMakuFourthEssenceDatabase : RemoteMakuEventDatabase
{
    internal RemoteMakuFourthEssenceDatabase() : base("remote_maku_fourth_essence_event.tsv",
        "fourth-Essence remote Maku event", "remote_maku_fourth_essence_commands.tsv")
    {
        if (Record is not { Group: 0, Room: 3, InteractionId: InteractionId.RemoteMakuCutscene, SubId: 0, Var03: 5,
            EssenceMask: 8, RequiredTreasure: 0xff, RoomFlag: 0x40,
            StandardTextId: 0x05b5, LinkedTextId: 0x05c5, StandardMapText: 0xb5,
            LinkedMapText: 0xc5, ConfettiKind: RemoteMakuConfettiKind.Present })
            throw new InvalidOperationException("INTERAC_REMOTE_MAKU_CUTSCENE $8a:$00/v$05: incomplete fourth-Essence source mapping.");
    }
}
