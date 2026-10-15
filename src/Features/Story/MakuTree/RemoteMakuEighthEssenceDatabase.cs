using System;

namespace oracleofages;

internal sealed class RemoteMakuEighthEssenceDatabase : RemoteMakuEventDatabase
{
    internal RemoteMakuEighthEssenceDatabase() : base("remote_maku_eighth_essence_event.tsv",
        "eighth-Essence remote Maku event", "remote_maku_eighth_essence_commands.tsv")
    {
        if (Record is not { Group: 1, Room: 0x5c, InteractionId: InteractionId.RemoteMakuCutscene,
            SubId: 1, Var03: 0x0a, EssenceMask: 0x80, RequiredTreasure: 0xff, RoomFlag: 0x40,
            StandardTextId: 0x05ba, LinkedTextId: 0x05ca, StandardMapText: 0xba, LinkedMapText: 0xca,
            ConfettiKind: RemoteMakuConfettiKind.Past })
            throw new InvalidOperationException("remoteMakuCutscene.s:@val0a: incomplete Ancient Tomb return $1:$5c mapping.");
    }
}
