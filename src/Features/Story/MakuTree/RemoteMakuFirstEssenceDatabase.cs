using System;

namespace oracleofages;

/// <summary>
/// Imported INTERAC_REMOTE_MAKU_CUTSCENE $8a:$00/v$00 data for room 0:8d's
/// first-Essence interaction.
/// </summary>
internal sealed class RemoteMakuFirstEssenceDatabase : RemoteMakuEventDatabase
{
    internal RemoteMakuFirstEssenceDatabase()
        : base(
            "remote_maku_first_essence_event.tsv",
            "first-Essence remote Maku event",
            "remote_maku_first_essence_commands.tsv")
    {
        if (Record is not
            {
                Group: 0, Room: 0x8d, InteractionId: 0x8a,
                SubId: 0, Var03: 0, EssenceMask: 0x01,
                RequiredTreasure: 0xff, RoomFlag: 0x40,
                StandardTextId: 0x05b0, LinkedTextId: 0x05c0,
                StandardMapText: 0xb0, LinkedMapText: 0xc0
            })
        {
            throw new InvalidOperationException(
                "Imported room 0:8d first-Essence remote Maku identity or " +
                "predicate is incomplete.");
        }
    }
}
