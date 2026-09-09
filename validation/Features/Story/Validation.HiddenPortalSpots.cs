using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateHiddenPortalSpots()
    {
        var database = new TimePortalDatabase();
        var records = database.GetRoomReveals(0, 0x13);
        FailIf(records.Count != 2 ||
            records[0] is not { Order: 0, SubId: 0x03, X: 0x28, Y: 0x48, RoomFlag: 0x02 } ||
            records[1] is not { Order: 1, SubId: 0x04, X: 0x78, Y: 0x48, RoomFlag: 0x04 },
            "Room 0:13 lost its ordered $dc:$03/$04 portal reveal placements.");
        _saveData.SetRoomFlag(0, 0x13, 0xff, value: false);
        _inventory.GiveTreasure(_treasures.GetObject("TREASURE_OBJECT_SWORD_00"));
        Vector2 left = new(0x28, 0x48);
        Vector2 right = new(0x78, 0x48);
        LoadValidationRoom(0, 0x13);
        FailIf(_currentRoom.GetMetatile(left) != 0xc5 ||
            _currentRoom.GetMetatile(right) != 0xc5 ||
            _entities.Entities<TimePortal>().Count != 2 ||
            _entities.Entities<TimePortal>().Any(portal => portal.Active || portal.Visible),
            "Room 0:13 must initially hide both dormant portal spots beneath $c5 bushes.");

        void Cut(Vector2 point)
        {
            _player.WarpTo(point + new Vector2(0, 14), recordSafe: false);
            _player.Face(Vector2I.Up);
            FailIf(!_combat.ApplySwordTileHit(_player, direction: 0, swordPoke: false) ||
                _currentRoom.GetMetatile(point) != 0x3a,
                $"Room 0:13 sword cut at {point} did not expose standard ground $3a.");
        }
        void Step() => _entities.Update(1.0 / 60.0, _player);

        _sound.ClearPlayRequestAudit();
        Cut(left);
        Step();
        FailIf(_currentRoom.GetMetatile(left) != 0x3a ||
            (_saveData.GetRoomFlags(0, 0x13) & 0x06) != 0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 0,
            "$dc:$03 state 0 revealed the 0:13/$42 marker before its separate state-1 update.");
        Step();
        FailIf(_currentRoom.GetMetatile(left) != 0xd7 || _currentRoom.IsSolid(left) ||
            _currentRoom.GetMetatile(right) != 0xc5 ||
            (_saveData.GetRoomFlags(0, 0x13) & 0x06) != 0x02 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 1 ||
            _entities.EntityAdapters<PortalRevealRoomEntity>().Count() != 1 ||
            _entities.Entities<TimePortal>().Any(portal => portal.Active || portal.Visible),
            "$dc:$03 did not reveal only 0:13/$42, set flag $02, play one solve sound, and delete.");

        // Re-entry applies singleTileChanges before interactions initialize.
        LoadValidationRoom(0, 0x14);
        LoadValidationRoom(0, 0x13);
        _sound.ClearPlayRequestAudit();
        Step();
        FailIf(_currentRoom.GetMetatile(left) != 0xd7 ||
            _currentRoom.GetMetatile(right) != 0xc5 ||
            _entities.EntityAdapters<PortalRevealRoomEntity>().Count() != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 0,
            "Room 0:13 re-entry did not persist only the left marker and silently delete $dc:$03.");

        // The shared handler compares specifically against $3a, not merely
        // against the former bush. Dug dirt must leave the right flag clear.
        _currentRoom.ReplaceMetatile(right, 0xc5, 0xd2, (long)_animationTicks);
        Step();
        FailIf(_currentRoom.GetMetatile(right) != 0xd2 ||
            (_saveData.GetRoomFlags(0, 0x13) & 0x04) != 0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 0,
            "$dc:$04 incorrectly accepted tile $d2 instead of standard ground $3a.");
        _currentRoom.ReplaceMetatile(right, 0xd2, 0xc5, (long)_animationTicks);
        Cut(right);
        Step();
        FailIf(_currentRoom.GetMetatile(right) != 0xd7 ||
            (_saveData.GetRoomFlags(0, 0x13) & 0x06) != 0x06 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 1 ||
            _entities.EntityAdapters<PortalRevealRoomEntity>().Any(),
            "$dc:$04 did not reveal 0:13/$47, preserve flag $02, set $04, sound once, and delete.");

        EnsureHarpAndSongs();
        _inventory.SelectHarpSong(1);
        PlaySelectedHarpSong(validateNoteSides: false);
        FailIf(_entities.Entities<TimePortal>().Any(portal => !portal.Awakening),
            "The revealed 0:13 portals did not both observe Tune of Echoes after the reveal handlers.");
        _harp.BeginObjectUpdate(); // Resume the next Link/item/interaction update.
        Step();
        FailIf(_entities.Entities<TimePortal>().Any(portal => !portal.Active),
            "Room 0:13's revealed portal markers could not activate after Tune of Echoes.");

        LoadValidationRoom(0, 0x14);
        LoadValidationRoom(0, 0x13);
        _sound.ClearPlayRequestAudit();
        Step();
        Step();
        FailIf(_currentRoom.GetMetatile(left) != 0xd7 ||
            _currentRoom.GetMetatile(right) != 0xd7 ||
            _entities.EntityAdapters<PortalRevealRoomEntity>().Any() ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 0 ||
            _entities.Entities<TimePortal>().Any(portal => portal.Active || portal.Visible),
            "Room 0:13 re-entry must preserve both markers, leave portals dormant, and not replay reveal sounds.");
        GD.Print("Validated room 0:13 hidden portal markers: bush cuts, separate initialization, " +
            "exact ground predicate, independent flags, Echoes activation, and persistent re-entry.");
    }
}
