using System;

namespace oracleofages;

/// <summary>
/// Imported INTERAC_REMOTE_MAKU_CUTSCENE $8a:$00/v$02 data for room 0:3a's
/// post-Harp interaction.
/// </summary>
internal sealed class RemoteMakuHarpDatabase : RemoteMakuEventDatabase
{
    internal RemoteMakuHarpDatabase()
        : base(
            "remote_maku_harp_event.tsv",
            "post-Harp remote Maku event",
            "remote_maku_harp_commands.tsv")
    {
        if (Record is not
            {
                Group: 0, Room: 0x3a, InteractionId: 0x8a,
                SubId: 0, Var03: 2, EssenceMask: 0,
                RequiredTreasure: TreasureDatabase.TreasureHarp,
                RoomFlag: 0x40,
                StandardTextId: 0x05b2, LinkedTextId: 0x05c2,
                StandardMapText: 0xb2, LinkedMapText: 0xc2
            })
        {
            throw new InvalidOperationException(
                "Imported room 0:3a post-Harp remote Maku identity or " +
                "predicate is incomplete.");
        }
    }
}
