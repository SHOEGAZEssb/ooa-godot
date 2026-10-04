using Godot;
using System;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCompanionMountRom()
    {
        int hostCase2 = 0;
        foreach (int id in new[] { 0x0b, 0x0c, 0x0d })
        foreach (int cancel in new[] { 0, 1, 2 })
        foreach (bool batched in RomHostSchedules(hostCase2++))
        {
            ReinitializeGameplayForValidation(); PrepareCompanionFidelityRoom(); _entities.Clear();
            Vector2 position = new(72, 64);
            var actor = SpawnFidelityCompanion(id, position);
            _player.WarpTo(position + new Vector2(0, 8));
            var rom = new CompanionRom(id, position, 2, _currentRoom);
            rom.WaitForMount(_player.PrecisePosition);
            int update = 0;
            void Compare(Vector2 movement, byte buttons)
            {
                rom.Update(CompanionMovement.AngleForInput(movement), buttons); update++;
                bool riding = rom[0xcc2c] == 0xd1;
                FailIf(_player.CompanionRideActive != riding || _player.CompanionJumpActive != (rom[0xcc5c] != 0) ||
                    _player.Position != rom.LinkPixels,
                    $"ROM companion ${id:x2} mount update {update}: native ride={riding}, air=${rom[0xcc5c]:x2}, pos={rom.LinkPixels}; runtime ride={_player.CompanionRideActive}, air={_player.CompanionJumpActive}, pos={_player.Position}.");
            }
            void Step(int count, Vector2 movement = default, bool dismount = false, bool blocked = false)
            {
                int frame = 0;
                _runtimeState.SetWramByte(0xcc98, blocked ? (byte)1 : (byte)0);
                StepGameplayUpdates(count, movement, dismount ? ["item"] : [], dismount ? ["item"] : [], batched,
                    () =>
                    {
                        rom[0xcc98] = blocked ? (byte)1 : (byte)0;
                        Compare(movement, dismount && frame++ == 0 ? (byte)2 : (byte)0);
                        FailIf(_runtimeState.ReadWramByte(0xcc98) != 0, "Mount signal must clear after the companion pass.");
                        if (blocked) _runtimeState.SetWramByte(0xcc98, 1);
                    });
            }
            Step(3, blocked: true);
            if (cancel != 0)
            {
                Step(5);
                if (cancel == 2) { _player.BeginCarriedObjectPose(); rom[0xcc5a] = 1; }
                Step(35, blocked: cancel == 1);
                FailIf(((IPlayerRideableRoomEntity)actor).LinkRiding || rom[0xd104] != 1,
                    "A mount lock during the jump must cancel the companion's mount, allowing Link's existing arc to finish.");
                if (cancel == 2) { _player.EndCarriedObjectPose(); rom[0xcc5a] = 0; }
            }
            // wDisableWarps is a different signal and must not prohibit mounting.
            _runtimeState.SetWramByte(0xccb2, 1); rom[0xccb2] = 1;
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Step(35);
                FailIf(!_player.CompanionRideActive, "ROM mount fixture did not complete mounting.");
                Step(35, dismount: true);
                FailIf(_player.CompanionRideActive || _player.CompanionJumpActive, "ROM dismount fixture did not land.");
                Step(10, Vector2.Down);
                Step(10, Vector2.Up);
            }
        }
        GD.Print("Validated native mounting/dismounting ownership, position and airborne gates through completion, walking away and repeated mounting in individual/batched gameplay.");
    }

    private void ValidateCompanionMovementGameplayRom()
    {
        int hostCase1 = 0;
        foreach (int id in new[] { 0x0b, 0x0c, 0x0d })
        foreach (bool wall in new[] { false, true })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation(); PrepareCompanionFidelityRoom(); _entities.Clear();
            if (wall)
                for (int y = 8; y < 128; y += 16)
                    _currentRoom.SetPositionTileAndCollision(new(88, y), 0, 15, 0);
            Vector2 initial = new(72.75f, 64.25f);
            IRoomEntity actor = id switch
            {
                0x0b => _entities.Spawn<RickyCompanionRoomEntity>(new RickyCompanionSpawn(initial, 1, 0, 0x2a, Riding: true)),
                0x0c => _entities.Spawn<DimitriCompanionRoomEntity>(new DimitriCompanionSpawn(initial, 1, 0, 0x2a, Riding: true)),
                _ => _entities.Spawn<MooshCompanionRoomEntity>(new MooshCompanionSpawn(initial, 1, 0, 0x2a, Riding: true))
            };
            CompanionRuntimeState.Begin(_runtimeState, id, 0x2a, initial, 1);
            var rom = new CompanionRom(id, initial, 1, _currentRoom);
            int update = 0;
            Vector2 Position() => actor switch
            { RickyCompanionRoomEntity r => r.PrecisePosition, DimitriCompanionRoomEntity d => d.PrecisePosition, MooshCompanionRoomEntity m => m.ScreenTransitionPosition, _ => throw new InvalidOperationException() };
            int Direction() => actor switch
            { RickyCompanionRoomEntity r => r.Direction, DimitriCompanionRoomEntity d => d.Direction, MooshCompanionRoomEntity m => m.Direction, _ => throw new InvalidOperationException() };
            void Step(int count, Vector2 movement)
            {
                StepGameplayUpdates(count, movement, batched: batched, afterUpdate: () =>
                {
                    rom.Update(CompanionMovement.AngleForInput(movement)); update++;
                    FailIf(Position() != rom.Position || Direction() != rom[0xd108] || _player.Position != rom.LinkPixels ||
                        CarriedObjectMotion.DirectionIndex(_player.FacingVector) != rom[0xd008],
                        $"ROM companion ${id:x2} gameplay update {update}, wall={wall}, batch={batched}: native={rom.Position}/{rom.LinkPixels}/dir{rom[0xd108]}, runtime={Position()}/{_player.Position}/dir{Direction()}.");
                    FailIf(!_player.CompanionRideActive || !((IPlayerRideableRoomEntity)actor).LinkRiding,
                        "Ground movement lost mounted Link ownership.");
                });
            }
            Step(1, Vector2.Zero);
            foreach (Vector2 movement in new[] { Vector2.Right, new Vector2(1, -1), Vector2.Up, new Vector2(-1, -1),
                Vector2.Left, new Vector2(-1, 1), Vector2.Down, new Vector2(1, 1) })
            {
                Step(5, movement);
                Step(1, Vector2.Zero); // Reset Ricky's hop countdown; jumps are a separate contract.
            }
            _dialogue.ShowGameplayMessage("Companion ROM gate", 100); rom[0xcba0] = 1;
            Step(4, Vector2.Right);
            _dialogue.Close(); rom[0xcba0] = 0;
            Step(3, Vector2.Right);
        }
        GD.Print("Validated all companion ground movement, diagonal turn timing, wall blocking, mounted Link anchors/facing and dialogue freeze/release through individual and batched gameplay updates against native species handlers.");
    }
}
