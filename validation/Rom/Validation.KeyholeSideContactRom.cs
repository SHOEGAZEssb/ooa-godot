using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareLibrarySideKeyholeRom()
    {
        foreach (bool batch in RomHostSchedules(0))
        {
            ReinitializeGameplayForValidation(); _saveData.SetRoomFlag(1,0xa5,0x80,false);
            _inventory.GiveTreasure(TreasureId.LibraryKey,1);
            _inventory.EquipA(0); _inventory.EquipB(0); LoadValidationRoom(1,0xa5);
            RetainKeyholeControllerRom(); _player.WarpTo(new(56,56)); _player.Face(Vector2I.Right);
            FailIf(_collision.Collides(_player.Position) || _currentRoom.GetMetatile(new(72,56)) != 0xec,
                "Side contact requires original Library floor$33:$f8 beside keyhole$34:$ec.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,1,56,56);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom[0xd240] = 1; rom[0xd241] = 0x90; rom[0xd242] = 0x13;
            rom[0xcc35] = rom[0xcc46] = (byte)_sound.ActiveMusic;
            var sounds = _sound.AttachPlayRequestAudit();
            void Step(int count = 1,int angle = 0xff) => StepSomariaMotionRom(rom,count,batch,angle,afterUpdate:() =>
            {
                var random = _random.CaptureState();
                FailIf(_rooms.TilePushCounter != rom[0xcc6a] || _rooms.InformativeTextsShown != rom[0xccd7] ||
                    _dialogue.IsOpen || _saveData.HasRoomFlag(1,0xa5,0x80) || rom[0xcfc0] != 0 ||
                    !sounds.Requests.SequenceEqual(rom.Sounds) || random.Rng1 != rom[0xff94] ||
                    random.Rng2 != rom[0xff95] || random.Calls - seed.Calls != rom.RandomCalls,
                    "Library side contact must preserve the native counter without text, key use or RNG/cue changes.");
            },contextPrefix:"Library side keyhole contact");
            Step(); Step(8,8);
            FailIf(_playerWorld.TilePushingDirection != 1 || rom[0xcc65] != 1,
                "Library side contact must reach its solid keyhole through actual collision geometry.");
            _rooms.TilePushCounter = rom[0xcc6a] = 7;
            Step(24,8);
            FailIf(_rooms.TilePushCounter != 7,"Wrong-side keyhole contact retains the shared clock instead of resetting it.");
            Step();
            FailIf(_rooms.TilePushCounter != 20,"Releasing side contact fails the pushing gate and restores20.");
            Step(2,8); _rooms.TilePushCounter = rom[0xcc6a] = 5;
            Step(12,8);
            FailIf(_rooms.TilePushCounter != 5,"Repeated wrong-side contact must retain the newly inherited countdown.");
        }
    }
}
