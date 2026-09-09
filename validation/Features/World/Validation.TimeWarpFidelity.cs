using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateTimeWarpLandingFidelity()
    {
        var landing = new TimeWarpLandingDatabase();
        int[] forceRooms = [0x5b, 0x5c, 0x5d, 0x6b, 0x6c, 0x6d, 0x75, 0x76,
            0x7b, 0x7c, 0x7d, 0x85, 0x86, 0x8c];
        for (int room = 0; room < 256; room++)
            FailIf(landing.SentBackByStrangeForce(room) != forceRooms.Contains(room),
                $"CUTSCENE_TIMEWARP strange-force dbrev room ${room:x2} changed.");

        LoadValidationRoom(0, 0x06);
        Vector2 point = _player.Position;
        foreach (byte tile in new byte[] { 0xf3, 0xfe, 0xff, 0xe4, 0xe5, 0xe6, 0xe7, 0xe8, 0xe9, 0xfc })
        {
            _currentRoom.ReplaceMetatile(point, _currentRoom.GetMetatile(point), tile, 0);
            FailIf(landing.CanStandOnTile(_currentRoom, point, false),
                $"checkLinkCanStandOnTile accepted invalid tile ${tile:x2} without Mermaid Suit.");
            if (tile != 0xfc)
                FailIf(landing.CanStandOnTile(_currentRoom, point, true),
                    $"Mermaid Suit incorrectly bypassed timewarp tile ${tile:x2}.");
        }
        FailIf(!landing.CanStandOnTile(_currentRoom, point, true),
            "TREASURE_MERMAID_SUIT $4a did not permit deep-water tile $fc.");
        _currentRoom.ReplaceMetatile(point, 0xfc, 0x3a, 0);
        Vector2 dugPoint = new(0x18, 0x18);
        _currentRoom.ReplaceMetatile(dugPoint, _currentRoom.GetMetatile(dugPoint), 0xd2, 0);

        // The destination tile is changed only after its actual full-room load,
        // before Link's 30-update arrival timer checks it.
        _saveData.ClearTimePortalLocation();
        _saveData.SetRoomFlag(1, 0x06, 0x10, false);
        _transitions.ApplyHarpTimeWarp(_player, point);
        AdvanceTimeWarpToPhase("TimeWarpArrivalFadeIn");
        _currentRoom.ReplaceMetatile(_player.Position, _currentRoom.GetMetatile(_player.Position), 0xf3, 0);
        AdvanceTimeWarpToPhase("TimeWarpArrivalEffect");
        for (int frame = 0; frame < 15; frame++) UpdateRoomWarpTransition(HarpFrame);
        FailIf(_player.Visible, "Rejected timewarp revealed Link before counter $10 reached zero.");
        UpdateRoomWarpTransition(HarpFrame);
        FailIf(!_player.Visible || _transitions.TimeWarpPhaseName != "TimeWarpRejectedFlicker",
            "Rejected timewarp did not enter substate 5 after $10 updates.");
        for (int frame = 0; frame < 119; frame++) UpdateRoomWarpTransition(HarpFrame);
        FailIf(_transitions.TimeWarpPhaseName != "TimeWarpRejectedFlicker",
            "Rejected timewarp skipped part of its $78-update flicker.");
        UpdateRoomWarpTransition(HarpFrame);
        FailIf(_transitions.TimeWarpPhaseName != "TimeWarpRejectedEffect",
            "Rejected timewarp failed to spawn its second $dd:$01 effect on update $78.");
        for (int frame = 0; frame < 16; frame++) UpdateRoomWarpTransition(HarpFrame);
        FailIf(_player.Visible || _transitions.TimeWarpPhaseName != "TimeWarpRejectedWait",
            "Rejected timewarp did not hide Link after its second $10-update effect.");
        for (int frame = 0; frame < 19; frame++) UpdateRoomWarpTransition(HarpFrame);
        FailIf(_transitions.TimeWarpPhaseName != "TimeWarpRejectedWait",
            "Rejected timewarp shortened its $14-update hidden wait.");
        UpdateRoomWarpTransition(HarpFrame);
        FailIf(_transitions.TimeWarpPhaseName != "TimeWarpReturnFadeOut" ||
            _saveData.HasRoomFlag(1, 0x06, 0x10),
            "Rejected timewarp did not restore unvisited room 1:06 before its direct return fade.");
        FinishTimeWarp();
        FailIf(_activeGroup != 0 || _currentRoom.Id != 0x06 || _saveData.TimePortalGroup != 0xff ||
            _dialogue.IsOpen || _player.InvincibilityFrames != -0x78 ||
            _entities.RuntimeState.ReadWramByte(0xcddc) != 0 || _entities.RuntimeState.ReadWramByte(0xcde0) != 0,
            "Obstructed timewarp did not return silently, suppress a new portal, clear warp state, and grant $88 invincibility.");
        FailIf(_currentRoom.GetMetatile(dugPoint) != 0xd2,
            "Failed timewarp discarded the source room's unsaved dug tile $d2 instead of restoring bank-2 layout.");

        // The room restriction applies to both eras and shows TX_5112 only
        // after the return arrival, unlike ordinary terrain rejection.
        LoadValidationRoom(0, 0x5b);
        int randomBeforeRestrictedWarp = _entities.RandomCalls;
        _sound.ClearPlayRequestAudit();
        _transitions.ApplyHarpTimeWarp(_player, _player.Position);
        AdvanceTimeWarpToPhase("TimeWarpArrivalEffect");
        FailIf(_entities.RandomCalls != randomBeforeRestrictedWarp ||
            _entities.Entities<NpcCharacter>().Count != 0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndWarpStart) != 1,
            "Strange-force initializeRoom must skip object parsing/RNG and create only $7c distortion with SND_WARP_START.");
        FailIf(_entities.RuntimeState.ReadWramByte(OracleRuntimeState.SentBackByStrangeForceAddress) != 1,
            "Room $5b did not publish wSentBackByStrangeForce=$01 before arrival.");
        AdvanceTimeWarpToPhase("TimeWarpReturnFadeOut");
        FailIf(_entities.RuntimeState.ReadWramByte(OracleRuntimeState.SentBackByStrangeForceAddress) != 2,
            "Strange-force return did not increment $01 to $02.");
        AdvanceTimeWarpToPhase("TimeWarpArrivalFadeIn");
        _currentRoom.ReplaceMetatile(_player.Position, _currentRoom.GetMetatile(_player.Position), 0x3a, 0);
        FinishTimeWarp();
        FailIf(_activeGroup != 0 || _currentRoom.Id != 0x5b || !_dialogue.IsOpen ||
            _entities.RuntimeState.ReadWramByte(OracleRuntimeState.SentBackByStrangeForceAddress) != 0,
            "Strange-force return did not finish in room 0:5b with TX_5112 and cleared state.");
        _dialogue.Close();

        _saveData.WriteWramByte(0xc6bf, 0x40); // getGameProgress_1=$03 after d7
        LoadValidationRoom(0, 0x45);
        NpcCharacter boy = _entities.Entities<NpcCharacter>().Single(npc => npc.Record.Id == 0x3f);
        Vector2 npcPoint = boy.Position;
        int npcPacked = _currentRoom.GetPackedPosition(npcPoint);
        FailIf(!_entities.TimeWarpPositionOccupied(landing, npcPacked),
            "Room 0:45's $3f:$01 did not reserve its original short-position tile.");
        _saveData.SetRoomFlag(0, 0x45, 0x10);
        LoadValidationRoom(1, 0x45);
        _player.WarpTo(npcPoint, recordSafe: false);
        _currentRoom.ReplaceMetatile(npcPoint, _currentRoom.GetMetatile(npcPoint), 0x3a, 0);
        _transitions.ApplyHarpTimeWarp(_player, npcPoint);
        AdvanceTimeWarpToPhase("TimeWarpRejectedFlicker");
        FailIf(_player.Position != npcPoint,
            "wLinkCanPassNpcs did not prevent the occupied arrival tile from pushing Link.");
        FinishTimeWarp();
        FailIf(_activeGroup != 1 || !_saveData.HasRoomFlag(0, 0x45, 0x10),
            "NPC obstruction failed to return to 1:45 or cleared the already visited 0:45 flag.");
        foreach (bool directHarp in new[] { false, true })
        {
            LoadValidationRoom(0, 0x74);
            OctorokCharacter octorok = _entities.Entities<OctorokCharacter>().First();
            Vector2 enemyStart = new(80, 64);
            octorok.Position = enemyStart;
            for (int y = 48; y <= 80; y += 16)
            for (int x = 64; x <= 96; x += 16)
            {
                Vector2 tilePoint = new(x, y);
                _currentRoom.ReplaceMetatile(tilePoint, _currentRoom.GetMetatile(tilePoint), 0x3a, 0);
            }
            if (directHarp)
                _transitions.ApplyHarpTimeWarp(_player, _player.Position);
            else
                _transitions.ApplyTimePortalWarp(_player, _player.Position);
            for (int frame = 0; frame < 11; frame++) UpdateRoomWarpTransition(HarpFrame);
            FailIf(octorok.Position != enemyStart,
                "Timewarp updated an enemy during the initial fast palette fade.");
            UpdateRoomWarpTransition(HarpFrame);
            FailIf((octorok.Position != enemyStart) != directHarp,
                "Timewarp must resume enemies after the Harp $5b fade, while portal $81 keeps them frozen.");
            FinishTimeWarp();
        }
        GD.Print("Validated timewarp invalid tiles, Mermaid Suit, all 14 strange-force rooms, rejection counters, return fade, visit restoration, portal suppression, source update masks, and $88 invincibility.");
    }

    private void ValidateTimePortalContactFidelity()
    {
        LoadValidationRoom(0, 0x13);
        var database = new TimePortalDatabase();
        PortalRecord record = database.GetRoomPortals(0, 0x13)[0] with { SubId = 0xc0 };
        Vector2 center = new(record.X, record.Y);
        _currentRoom.ReplaceMetatile(center, _currentRoom.GetMetatile(center), 0xd7, 0);
        for (int y = -9; y <= 9; y++)
        for (int x = -9; x <= 9; x++)
        {
            var portal = new TimePortal();
            portal.InitializePlaced(record, _currentRoom, true, _saveData, () => 0, _ => { });
            portal.UpdateFrame(1);
            bool expected = x >= -8 && x < 8 && y >= -8 && y < 8;
            bool contact = portal.CheckLinkContact(center + new Vector2(x, y));
            FailIf(contact != expected, $"$e1 collision at ({x},{y}) must use unsigned summed-radius window [-8,7].");
            portal.Free();
        }
        FailIf(!_saveData.HasRoomFlag(0, 0x13, 0x02),
            "$e1 sub-ID bit 6 did not set room flag $02 on contact.");

        var temporary = new TimePortal();
        _saveData.SetTimePortalLocation(0, 0x13, 0x42);
        temporary.InitializeTemporary(database.TemporaryVisual, _currentRoom, center, _saveData);
        _player.WarpTo(center);
        temporary.UpdateFrame(2, _player);
        FailIf(temporary.CheckLinkContact(center) || temporary.CurrentPalette != 1,
            "$de state 0 accepted arrival contact or advanced its palette before initialization finished.");
        temporary.UpdateFrame(4, _player);
        FailIf(temporary.CheckLinkContact(center) || temporary.CurrentPalette != 2,
            "$de state 1 must cycle its palette while waiting for Link to leave.");
        _player.WarpTo(center + new Vector2(-9, 0));
        temporary.UpdateFrame(5, _player);
        FailIf(temporary.CheckLinkContact(_player.Position),
            "$de negative collision edge -9 must remain inside the summed-radius window.");
        _player.WarpTo(center + new Vector2(9, 0));
        temporary.UpdateFrame(6, _player);
        _saveData.SetTimePortalLocation(0, 0x13, 0x47);
        temporary.UpdateFrame(7, _player);
        FailIf(!temporary.Expired || temporary.Visible,
            "$de state 2 did not delete a stale portal after wPortalPos changed from $42 to $47.");
        temporary.Free();
        GD.Print("Validated $e1/$de asymmetric collision boundaries, bit-6 entry flag, return-portal initialization, leave gate, palette cadence, and stale-position deletion.");
    }

    private void AdvanceTimeWarpToPhase(string phase)
    {
        for (int frame = 0; frame < 1000 && _transitions.TimeWarpPhaseName != phase && IsTransitioning; frame++)
            UpdateRoomWarpTransition(HarpFrame);
        FailIf(_transitions.TimeWarpPhaseName != phase,
            $"Timewarp expected {phase}, reached {_transitions.TimeWarpPhaseName} in {_activeGroup:x1}:{_currentRoom.Id:x2}.");
    }
}
