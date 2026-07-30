using System;

namespace oracleofages;

/// <summary>
/// Imported INTERAC_REMOTE_MAKU_CUTSCENE $8a:$01/v$03 data for room 1:83's
/// second-Essence interaction.
/// </summary>
internal sealed class RemoteMakuSecondEssenceDatabase :
    RemoteMakuEventDatabase
{
    internal RemoteMakuSecondEssenceDatabase()
        : base(
            "remote_maku_second_essence_event.tsv",
            "second-Essence remote Maku event",
            "remote_maku_second_essence_commands.tsv")
    {
        if (Record is not
            {
                Group: 1, Room: 0x83, InteractionId: 0x8a,
                SubId: 1, Var03: 3, EssenceMask: 0x02,
                RequiredTreasure: 0xff, RoomFlag: 0x40,
                StandardTextId: 0x05b3, LinkedTextId: 0x05c3,
                StandardMapText: 0xb3, LinkedMapText: 0xc3,
                ConfettiKind: RemoteMakuConfettiKind.Past
            })
        {
            throw new InvalidOperationException(
                "Imported room 1:83 second-Essence remote Maku identity or " +
                "predicate is incomplete.");
        }
    }
}
