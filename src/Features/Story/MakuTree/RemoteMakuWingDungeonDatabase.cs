using System;

namespace oracleofages;

/// <summary>
/// Imported INTERAC_REMOTE_MAKU_CUTSCENE $8a:$00/v$01 data allocated after
/// room 0:83's Wing Dungeon collapse.
/// </summary>
internal sealed class RemoteMakuWingDungeonDatabase : RemoteMakuEventDatabase
{
    internal RemoteMakuWingDungeonDatabase()
        : base(
            "remote_maku_wing_dungeon_event.tsv",
            "Wing Dungeon remote Maku event",
            "remote_maku_wing_dungeon_commands.tsv")
    {
        if (Record is not
            {
                Group: 0, Room: 0x83, InteractionId: 0x8a,
                SubId: 0, Var03: 1, EssenceMask: 0,
                RequiredTreasure: 0xff, RoomFlag: 0x40,
                StandardTextId: 0x05b1, LinkedTextId: 0x05c1,
                StandardMapText: 0xb1, LinkedMapText: 0xc1
            })
        {
            throw new InvalidOperationException(
                "Imported room 0:83 Wing Dungeon remote Maku identity is " +
                "incomplete.");
        }
    }
}
