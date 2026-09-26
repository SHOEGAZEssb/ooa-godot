namespace oracleofages;

// Source symbols retain their clean-US/Ages values. Keep IDs distinct from table indices,
// masks and counters; byte-oriented asset/state APIs intentionally continue to use integers.
[System.Flags]
public enum TilesetFlags
{
    // constants/common/tilesetFlags.s: TILESETFLAG_OUTDOORS
    Outdoors = 0x01,
    // constants/common/tilesetFlags.s: TILESETFLAG_MAKU
    Maku = 0x02,
    // constants/common/tilesetFlags.s: TILESETFLAG_DUNGEON
    Dungeon = 0x08,
    // constants/common/tilesetFlags.s: TILESETFLAG_LARGE_INDOORS
    LargeIndoors = 0x10,
    // constants/common/tilesetFlags.s: TILESETFLAG_SIDESCROLL
    Sidescroll = 0x20,
    // constants/common/tilesetFlags.s: TILESETFLAG_UNDERWATER
    Underwater = 0x40,
    // constants/common/tilesetFlags.s: TILESETFLAG_PAST
    Past = 0x80,
}
