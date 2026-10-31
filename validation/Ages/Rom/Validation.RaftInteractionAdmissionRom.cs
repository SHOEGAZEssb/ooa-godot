using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateRaftInteractionAdmissionRom()
    {
        int cases = 0;
        foreach (int subid in new[] { 0, 1, 2 })
        foreach (bool changedRooms in new[] { false, true })
        foreach (byte dimitri in new byte[] { 0, 0x40 })
        foreach (byte companionId in new byte[] { 0, 0x13 })
        foreach (int reserved in new[] { -1, 0, 1 })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(0x26, changedRooms);
            _saveData.WriteWramByte(0xc647, dimitri);
            LoadValidationRoom(1, 0xa7); _entities.Clear();
            _player.WarpTo(new(80, 56));
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 0, 80, 56);
            rom.InitializeLinkGameplay();
            _runtimeState.SetWramByte(0xd101, companionId); rom[0xd101] = companionId;
            if (reserved >= 0)
            {
                int at = 0xd041 + reserved * 0x100;
                _runtimeState.SetWramByte(at, 0xe6); rom[at] = 0xe6;
            }
            var actor = new RaftInteractionRoomEntity(new RaftInteractionSpawn(new(80, 88), 1, 0xa7, subid),
                _currentRoom, new RaftDatabase().Behavior, _saveData, _runtimeState, _entities);
            _entities.AddEntity(actor);
            const int slot = 0xd240;
            FailIf(_entities.NativeInteractionSlot(actor) != 2 || !actor.NativeInitializationPending || actor.Visible,
                "Placed/remembered raft construction must allocate first-free $d2 and leave source state0 pending.");
            rom[slot] = 1; rom[slot + 1] = 0xe6; rom[slot + 2] = (byte)subid;
            rom[slot + 0x0b] = 88; rom[slot + 0x0d] = 80;
            byte[] companion = Enumerable.Range(0xd100, 0x40).Select(_runtimeState.ReadWramByte).ToArray();
            _entities.UpdateNativeInteractionPass(_player, _entities.FrameCounter);
            rom.AdvanceInteractions(_entities.FrameCounter);
            bool survives = subid == 2 || (subid == 1 || dimitri == 0x40) &&
                changedRooms && companionId != 0x13 && reserved < 0;
            string context = $"INTERAC $e6:${subid:x2}, flag26={changedRooms}, Dimitri=${dimitri:x2}, D1.id=${companionId:x2}/enabled00, reserved={reserved}";
            FailIf(actor.Finished == survives || (rom[slot] != 0) != survives ||
                _entities.Entities<RaftInteractionRoomEntity>().Count != (survives ? 1 : 0),
                $"{context}: source state0 admission/deletion differs.");
            if (survives)
            {
                foreach (int offset in new[] { 0, 1, 2, 4, 8, 0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f,
                    0x1a, 0x1e, 0x1f, 0x20, 0x21, 0x22, 0x23 })
                    FailIf(_runtimeState.ReadWramByte(slot + offset) != rom[slot + offset],
                        $"{context}: initialized source byte${slot + offset:x4} differs.");
                FailIf(actor.NativeInitializationPending || !actor.Visible || actor.State != 1,
                    $"{context}: source state0 must resolve graphics and visibility without running state1.");
            }
            else
                for (int offset = 0; offset < 0x40; offset++)
                    FailIf(_runtimeState.ReadWramByte(slot + offset) != 0 || rom[slot + offset] != 0,
                        $"{context}: interactionDelete must clear all64 bytes.");
            FailIf(!companion.SequenceEqual(Enumerable.Range(0xd100, 0x40).Select(_runtimeState.ReadWramByte)) ||
                !companion.SequenceEqual(Enumerable.Range(0xd100, 0x40).Select(address => rom[address])) ||
                RandomDiffers(seed, rom[0xff94], rom[0xff95], rom.RandomCalls),
                $"{context}: initialization changed native D1 or RNG.");
            cases++;
        }
        GD.Print($"Validated {cases} original raft state0 cases: subids00/01/02, flag26/Dimitri gates, disabled D1.id13, and reserved disabled interaction IDs in first-match order.");
    }
}
