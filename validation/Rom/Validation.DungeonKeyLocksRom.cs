using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareDungeonKeyLocksRom(bool crown = false)
    {
        var cases = crown ? new[] {
            (0x9a,5,false,false,new Vector2(8,88),24),
            (0xa8,5,true,false,new Vector2(136,40),24),
            (0xac,5,true,false,new Vector2(88,40),0),
            (0xb3,5,false,false,new Vector2(232,136),8),
            (0xbd,5,false,false,new Vector2(8,88),24),
            (0xbe,5,false,true,new Vector2(232,88),8)
        } : new[] {
            (0x0a,13,false,false,new Vector2(8,88),24),
            (0x35,2,true,false,new Vector2(120,40),24),
            (0x12,1,false,true,new Vector2(232,120),8)
        };
        int fixture = 0;
        foreach (var (room,dungeon,block,boss,center,direction) in cases)
        foreach (bool owned in crown ? new[] { false } : new[] { false,true })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,room); _entities.Clear();
            if (owned) _inventory.GiveTreasure(_treasures.GetObject(boss ?
                "TREASURE_OBJECT_BOSS_KEY_03" : "TREASURE_OBJECT_SMALL_KEY_03"));
            Vector2I facing = (Vector2I)OracleObjectMath.StrictCardinalVector(direction);
            Vector2 start = center - (Vector2)facing * (block ? 16 : 28) + new Vector2(0.25f,0.5f);
            _player.WarpTo(start); _player.Face(facing);
            byte closed = block ? (byte)0x1e : boss ? (byte)0x75 : (byte)(0x70+direction/8);
            FailIf(_rooms.CurrentDungeonIndex != dungeon || _currentRoom.GetMetatile(center) != closed ||
                _collision.Collides(start),$"Native lock$4:${room:x2} must use original tile${closed:x2} and actual floor approach.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,direction / 8,(int)start.X,(int)start.Y);
            rom.Word(0xd00a,(int)(start.Y * 256)); rom.Word(0xd00c,(int)(start.X * 256));
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom.CreateMenuView().LoadDungeon(dungeon);
            Vector2 camera = boss ? new(80,48) : Vector2.Zero;
            var oldScreen = _entities.WorldToScreen; _entities.WorldToScreen = point => point - camera;
            rom[0xffaa] = (byte)camera.Y; rom[0xffac] = (byte)camera.X;
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1,int angle = 0xff) =>
                StepSomariaMotionRom(rom,count,batch,angle,afterUpdate:() =>
                {
                    string context = $"Dungeon lock$4:${room:x2} owned={owned}, batch={batch}, update={++update}";
                    CompareKeyDoorGameplayRom(rom,seed,sounds.Requests,context,excludeText:true,dungeon:dungeon);
                    FailIf(_keyDoors.RemainingPushFrames != rom[0xcc6a] ||
                        _rooms.InformativeTextsShown != rom[0xccd7] || _dialogue.IsOpen != (rom[0xcba0] != 0),
                        context + ": shared contact clock/hint/modal state differs.");
                    for (int index = 0; index < 16; index++)
                        FailIf(_inventory.GetDungeonSmallKeys(index) != rom[0xc672 + index] ||
                            _inventory.HasDungeonBossKey(index) != ((rom[0xc682 + index / 8] & (1 << (index & 7))) != 0),
                            context + $": dungeon${index:x2} small/boss-key bytes differ.");
                    for (int index = 0; index < 256; index++)
                        FailIf(_saveData.GetRoomFlags(4,index) != rom[0xc900 + index],context + $": room flags$4:${index:x2} differ.");
                    var puffs = _entities.Entities<PuzzlePuffEffect>().OrderBy(_entities.InteractionSlot).ToArray();
                    int[] nativePuffs = Enumerable.Range(0xd2,14).Select(page => (page << 8) | 0x40)
                        .Where(slot => rom[slot] != 0 && rom[slot + 1] == 5).ToArray();
                    FailIf(puffs.Length != nativePuffs.Length,context + ": INTERAC$05 count differs.");
                    for (int index = 0; index < puffs.Length; index++)
                    {
                        var puff = puffs[index]; int slot = nativePuffs[index];
                        FailIf(_entities.InteractionSlot(puff) != ((slot >> 8) & 15) ||
                            puff.Position != new Vector2(rom[slot + 0xd],rom[slot + 0xb]) ||
                            puff.Initialized != (rom[slot + 4] != 0) || puff.CurrentParameter != rom[slot + 0x21] ||
                            puff.Visible != ((rom[slot + 0x1a] & 0x80) != 0),context + ": puff slot/XY/state/animation/visibility differs.");
                    }
                });
            try
            {
                Step();
                for (int wait = 0; !_dialogue.IsOpen && !_keyDoors.Opening &&
                    _currentRoom.GetMetatile(center) == closed && wait < 60; wait++)
                {
                    Step(angle:direction);
                    FailIf(_currentRoom.IsSolid(_player.Position),"Key-lock approach entered original solid geometry.");
                }
                if (!owned)
                {
                    int text = block ? 0x5502 : boss ? 0x5501 : 0x5500;
                    int mask = boss ? 2 : 4;
                    FailIf(!_dialogue.IsOpen || rom.TextGeneration != 1 || rom.Word(0xcba2) != text ||
                        _rooms.InformativeTextsShown != mask || _currentRoom.GetMetatile(center) != closed,
                        "Missing key must show the independently traced hint once without changing the lock.");
                    if (crown)
                        FailIf(_dialogue.CurrentMessage != (block ? "Huh? This block\nhas a keyhole." :
                            boss ? "This keyhole\nis different!" : "You need a key\nfor this door!"),
                            "Original Crown lock must retain its source-derived missing-key text content.");
                    Step(3,direction); _dialogue.Close(); rom[0xcba0] = 0;
                    Step(44,direction);
                    FailIf(_dialogue.IsOpen || rom.TextGeneration != 1 || _keyDoors.Opening ||
                        _entities.Entities<DungeonKeyUseEffect>().Count != 0 || _entities.Entities<PuzzlePuffEffect>().Count != 0,
                        "Repeated missing-key contact must not reopen the hint or allocate key/puff effects.");
                    if (!crown) continue;
                    // Declare an external treasure grant after the failed
                    // contact, without resetting its shared hint/countdown.
                    // Execute giveTreasure in the native fixture as well.
                    _inventory.GiveTreasure(_treasures.GetObject(boss ?
                        "TREASURE_OBJECT_BOSS_KEY_03" : "TREASURE_OBJECT_SMALL_KEY_03"));
                    byte[] caller = [0x3e,boss ? (byte)0x31 : (byte)0x30,
                        0x0e,boss ? (byte)0 : (byte)1,0xcd,0x1c,0x17,0xc9];
                    for (int index = 0; index < caller.Length; index++) rom[0xc100+index] = caller[index];
                    SomariaPrivate<FrontendRom>(rom,"_rom").Call(0xc100,1);
                    for (int wait = 0; !_keyDoors.Opening && _currentRoom.GetMetatile(center) == closed && wait < 45; wait++)
                    {
                        Step(angle:direction);
                        FailIf(_currentRoom.IsSolid(_player.Position),"Renewed key contact must stay on the original approach floor.");
                    }
                }
                byte flag = block ? (byte)0x80 : (byte)(1<<(direction/8));
                FailIf(!_saveData.HasRoomFlag(4,room,flag) ||
                    _inventory.GetDungeonSmallKeys(dungeon) != 0 || boss && !_inventory.HasDungeonBossKey(dungeon),
                    "Lock must spend one small key or retain its Boss Key and mark source-derived flags.");
                if (!block)
                {
                    bool neighbor = _rooms.TryGetNeighbor(facing,out int target);
                    byte opposite = (byte)(1<<((direction/8+2)&3));
                    FailIf(!neighbor || !_saveData.HasRoomFlag(4,target,opposite),
                        "Directional lock must mark its opposite flag in the imported dungeon neighbor.");
                }
                FailIf(block && (_keyDoors.Opening || _currentRoom.GetMetatile(center) != 0xa0 ||
                    _entities.Entities<PuzzlePuffEffect>().Count != 1),
                    "Key block must immediately become floor and allocate exactly one native puff.");
                _dialogue.ShowMessage("Key-lock pause.",120); rom[0xcba0] = 1;
                Step(3); _dialogue.Close(); rom[0xcba0] = 0;
                Step(32); Step(4,direction);
                FailIf(_keyDoors.Opening || _currentRoom.IsSolid(center) ||
                    _entities.Entities<DungeonKeyUseEffect>().Count != 0 || _entities.Entities<PuzzlePuffEffect>().Count != 0,
                    "Completed/repeated key-lock input must leave passable floor and retire key/puff effects.");
                LoadValidationRoom(0,0x60); LoadValidationRoom(4,room);
                FailIf(_currentRoom.GetMetatile(center) != 0xa0 || _currentRoom.IsSolid(center),
                    "Source persistent key-lock substitution must reconstruct passable floor on re-entry.");
            }
            finally { _entities.WorldToScreen = oldScreen; _dialogue.Close(); }
        }
    }
}
