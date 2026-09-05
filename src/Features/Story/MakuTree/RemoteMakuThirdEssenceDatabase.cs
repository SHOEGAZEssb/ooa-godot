using System;

namespace oracleofages;

/// <summary>
/// Imported INTERAC_REMOTE_MAKU_CUTSCENE $8a:$00/v$04 data dynamically
/// created in room 0:ba after the post-D3 Black Tower explanation.
/// </summary>
internal sealed class RemoteMakuThirdEssenceDatabase : RemoteMakuEventDatabase
{
    internal RemoteMakuThirdEssenceDatabase()
        : base(
            "remote_maku_third_essence_event.tsv",
            "third-Essence remote Maku event",
            "remote_maku_third_essence_commands.tsv")
    {
        if (Record is not
            {
                Group: 0, Room: 0xba, InteractionId: 0x8a,
                SubId: 0, Var03: 4, EssenceMask: 0x04,
                RequiredTreasure: 0xff, RoomFlag: 0x40,
                StandardTextId: 0x05b4, LinkedTextId: 0x05c4,
                StandardMapText: 0xb4, LinkedMapText: 0xc4
            })
        {
            throw new InvalidOperationException(
                "Imported room 0:ba third-Essence remote Maku identity or " +
                "predicate is incomplete.");
        }
    }
}
