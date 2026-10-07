using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownKeyAllocation()
    {
        int fixture = 0;
        foreach (var spec in new (bool Block,int Free,bool QueueFull)[] {
            (false,0,false),(false,1,false),(false,2,false),
            (true,0,false),(true,1,false),(true,2,false),(true,2,true),(false,2,true) })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation();
            int room = spec.Block ? 0xa8 : 0xb3;
            byte flag = spec.Block ? (byte)0x80 : (byte)2;
            LoadValidationRoom(4,room); _entities.Clear();
            _inventory.GiveTreasure(_treasures.GetObject("TREASURE_OBJECT_SMALL_KEY_03"));
            _inventory.EquipA(0); _inventory.EquipB(0);
            Vector2 center = spec.Block ? new(136,40) : new(232,136);
            int direction = spec.Block ? 24 : 8;
            Vector2 start = center-OracleObjectMath.StrictCardinalVector(direction)*28+new Vector2(0.25f,0.5f);
            _player.WarpTo(start); _player.Face((Vector2I)OracleObjectMath.StrictCardinalVector(direction));
            FailIf(_collision.Collides(start) || _rooms.CurrentDungeonIndex != 5 ||
                _currentRoom.GetMetatile(center) != (spec.Block ? 0x1e : 0x71),
                "Crown key capacity probe must approach its unchanged original lock through real floor.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,direction/8,(int)start.X,(int)start.Y);
            rom.Word(0xd00a,(int)(start.Y*256)); rom.Word(0xd00c,(int)(start.X*256));
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom.CreateMenuView().LoadDungeon(5);
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1,int angle = 0xff) => StepSomariaMotionRom(rom,count,batch,angle,
                afterUpdate:() => {
                    rom.AdvanceTileGraphics();
                    string context = $"Crown key capacity block={spec.Block}, free={spec.Free}, fullQueue={spec.QueueFull}, batch={batch}, update={++update}";
                    CompareKeyDoorGameplayRom(rom,seed,sounds.Requests,context,excludeText:true);
                    FailIf(_rooms.TilePushCounter != rom[0xcc6a] ||
                        _rooms.PendingTileGraphics != ((rom[0xcce0]-rom[0xccdf]) & 31),
                        context+": shared contact/queue count differs.");
                    for (int id = 0; id < 256; id++)
                        FailIf(_saveData.GetRoomFlags(4,id) != rom[0xc900+id],context+$": room flags$4:${id:x2} differ.");
                    var puffs = _entities.Entities<PuzzlePuffEffect>().OrderBy(_entities.InteractionSlot).ToArray();
                    int[] slots = Enumerable.Range(0xd2,14).Select(page => page*256+0x40)
                        .Where(slot => rom[slot] != 0 && rom[slot+1] == 5).ToArray();
                    FailIf(puffs.Length != slots.Length,context+": physical puff count differs.");
                    for (int i = 0; i < slots.Length; i++)
                    {
                        var puff = puffs[i]; int slot = slots[i];
                        FailIf(_entities.InteractionSlot(puff) != (slot>>8)-0xd0 ||
                            puff.Position != new Vector2(rom[slot+0xd],rom[slot+0xb]) ||
                            puff.Initialized != (rom[slot+4] != 0) || puff.CurrentParameter != rom[slot+0x21] ||
                            puff.Visible != ((rom[slot+0x1a] & 0x80) != 0),context+": puff slot/XY/state/animation/visibility differs.");
                    }
                },contextPrefix:$"Crown key capacity block={spec.Block}, free={spec.Free}, fullQueue={spec.QueueFull}");
            Step();
            int boundary = spec.Block ? 1 : 2;
            for (int wait = 0; rom[0xcc6a] != boundary && wait < 64; wait++) Step(angle:direction);
            FailIf(rom[0xcc6a] != boundary || _currentRoom.IsSolid(_player.Position),
                $"Capacity contention must follow the real lock approach: block={spec.Block}, free={spec.Free}, fullQueue={spec.QueueFull}, counter={rom[0xcc6a]}/{boundary}, Link={_player.PrecisePosition}, collision={_collision.Collides(_player.Position)}, keys={_inventory.GetDungeonSmallKeys(5)}.");
            for (int i = 0; i < 14-spec.Free; i++)
            {
                var puff = _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(24,24),SoundId.MusNone));
                int slot = 0xd000+_entities.InteractionSlot(puff)*256+0x40;
                rom[slot] = 1; rom[slot+1] = 5; rom[slot+2] = 0x80;
                rom[slot+0xb] = rom[slot+0xd] = 24;
            }
            void FillQueue()
            {
                for (int i = 0; i < 31; i++)
                {
                    FailIf(!_rooms.TrySetTile(0x11,0xa0),"Full-queue lock fixture must accept31 writes.");
                    rom.SetTile(0x11,0xa0);
                }
            }
            if (spec.Block && spec.QueueFull) FillQueue();
            Step(angle:direction);
            var keys = _entities.Entities<DungeonKeyUseEffect>();
            FailIf(_inventory.GetDungeonSmallKeys(5) != 0 || !_saveData.HasRoomFlag(4,room,flag) ||
                keys.Count != (spec.Free > 0 ? 1 : 0) ||
                keys.Count != 0 && _entities.InteractionSlot(keys.Single()) != 16-spec.Free ||
                _entities.Entities<PuzzlePuffEffect>().Count != 14-spec.Free+(spec.Block && spec.Free == 2 ? 1 : 0) ||
                _currentRoom.GetMetatile(center) != (spec.Block && !spec.QueueFull ? 0xa0 : spec.Block ? 0x1e : 0x71),
                "Native lock must debit/set flags despite capacity failure, allocate key before puff, and preserve a rejected tile write.");
            if (!spec.Block && spec.QueueFull)
            {
                for (int wait = 0; rom[0xd046] != 1 && wait < 12; wait++) Step();
                FailIf(rom[0xd046] != 1 || !_keyDoors.Opening,
                    "Door queue contention must occur before the final interleave update.");
                FillQueue(); Step();
                FailIf(_keyDoors.Opening || _currentRoom.GetMetatile(center) != 0xa0 ||
                    !_currentRoom.IsSolid(center) || _inventory.GetDungeonSmallKeys(5) != 0,
                    "Full-queue door completion must retain closed collision without refunding the key or retrying its tile write.");
            }
            Step(36);
            FailIf(_keyDoors.Opening || _entities.Entities<DungeonKeyUseEffect>().Count != 0 ||
                _entities.Entities<PuzzlePuffEffect>().Count != 0 || !_entities.InteractionSlotAvailable ||
                sounds.Requests.Count(cue => cue == SoundId.SndGetSeed) != (spec.Free > 0 ? 1 : 0),
                "Retired native effects must free capacity without retrying allocations or debiting another key.");
            Step(4,direction);
            LoadValidationRoom(4,room);
            FailIf(_currentRoom.GetMetatile(center) != 0xa0 || _currentRoom.IsSolid(center),
                "Persistent lock flags must reconstruct floor on re-entry, including rejected live tile writes.");
        }
    }
}
