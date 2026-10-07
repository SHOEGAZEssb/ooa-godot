using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private (SomariaRom Rom,PushBlockSynchronizerRoomEntity Synchronizer,OracleRandomState Seed)
        PrepareSynchronizedBlockRom(int room,int start,int facing)
    {
        ReinitializeGameplayForValidation(); LoadValidationRoom(4,room);
        FailIf(_entities.InteractionSlot(_entities.Entities<PushBlockSynchronizerRoomEntity>().Single()) != 3,
            "Original Crown parser must place $21 before synchronizer$bd in slot$d3.");
        // Isolate the actual shared $bd/$14 stream. Other room scripts and
        // enemy AI are outside this comparison; retain source room geometry.
        _entities.Clear(); _inventory.GiveTreasure(TreasureId.Bracelet,1);
        _inventory.EquipA(0); _inventory.EquipB(0);
        var synchronizer = new PushBlockSynchronizerRoomEntity(_currentRoom,new PushBlockSynchronizerDatabase(),
            () => _pushBlocks,_entities.TryCreateSynchronizedBlock);
        _entities.AddEntity(synchronizer);
        Vector2 point = new((start&15)*16+8.25f,(start>>4)*16+8.5f);
        _player.WarpTo(point); _player.Face((Vector2I)OracleObjectMath.StrictCardinalVector(facing*8));
        FailIf(_collision.Collides(point),"Synchronized-block comparison must start on unchanged source floor.");
        var seed = _random.CaptureState();
        var rom = new SomariaRom(_saveData,seed,_currentRoom,facing,(int)point.X,(int)point.Y);
        rom.Word(0xd00a,(int)(point.Y*256)); rom.Word(0xd00c,(int)(point.X*256));
        rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
        rom[0xd240] = 1; rom[0xd241] = 0xbd;
        return (rom,synchronizer,seed);
    }

    private void CompareSynchronizedPushBlocksRom(SomariaRom rom,
        PushBlockSynchronizerRoomEntity synchronizer,OracleRandomState seed,
        IReadOnlyList<int> sounds,string context)
    {
        FailIf(SomariaPrivate<int>(synchronizer,"_state") != rom[0xd244] ||
            _rooms.BlockPushAngle != rom[0xcca6] || _pushBlocks.RemainingPushFrames != rom[0xcc6a],
            context + $": $bd phase/shared direction/contact counter runtime={SomariaPrivate<int>(synchronizer,"_state")}/${_rooms.BlockPushAngle:x2}/{_pushBlocks.RemainingPushFrames}, ROM={rom[0xd244]}/${rom[0xcca6]:x2}/{rom[0xcc6a]}.");
        CompareNativePushBlockRom(rom,_pushBlocks,0xd140,context);
        var blocks = _entities.Entities<PushBlockController>()
            .OrderBy(_entities.InteractionSlot).ToArray();
        int[] native = Enumerable.Range(0xd2,14).Select(page => (page << 8) | 0x40)
            .Where(slot => rom[slot] != 0 && rom[slot + 1] == 0x14).ToArray();
        FailIf(blocks.Length != native.Length,context + ": synchronized child count differs.");
        for (int index = 0; index < native.Length; index++)
        {
            FailIf(_entities.InteractionSlot(blocks[index]) != ((native[index] >> 8) & 15),
                context + ": descending source scan/allocation order differs.");
            CompareNativePushBlockRom(rom,blocks[index],native[index],context);
        }
        var random = _random.CaptureState();
        FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
            random.Calls - seed.Calls != rom.RandomCalls || !sounds.SequenceEqual(rom.Sounds),
            context + ": ordered movement cues/shared RNG differs.");
    }

    private void CompareNativePushBlockRom(SomariaRom rom,PushBlockController block,int slot,string context)
    {
        bool active = rom[slot] != 0 && rom[slot + 1] == 0x14;
        FailIf(block.Active != active,context + $": INTERAC${slot >> 8:x2} allocation differs.");
        if (!active) return;
        FailIf(block.ActiveTile != rom[slot + 0x31] || block.PushAngle != (rom[slot + 9] & 31) ||
            block.BlockTopLeft + new Vector2(8,6) !=
                new Vector2(rom.Word(slot + 0xc) / 256f,rom.Word(slot + 0xa) / 256f) ||
            block.BlockZHigh != unchecked((sbyte)rom[slot + 0xf]) ||
            block.ActiveMoveFrames - (int)SomariaPrivate<float>(block,"_moveFrame") != rom[slot + 6],
            context + $": INTERAC${slot >> 8:x2} full XY/Z, tile, angle or movement clock differs.");
    }
}
