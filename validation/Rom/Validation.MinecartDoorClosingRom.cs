using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareMinecartDoorClosingRom()
    {
        int fixture = 0;
        foreach (int competingTile in new[] { 0xda,0x7b,0x5e })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0x9d); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            _player.WarpTo(new(120.25f,136.5f)); _player.Face(Vector2I.Down);
            FailIf(_collision.Collides(_player.Position),"Minecart shutter closure must retain original safe room$4:$9d floor.");
            // Declared open layout shutter and competing tile-owner boundary.
            // Actual cart creation/riding and Cane placement have separate ROM
            // fixtures; this executes their common INTERAC$1e closing consumer.
            _currentRoom.SetPositionTileAndCollision(new(120,168),0x5e,0,0);
            var door = new MinecartShutterRoomEntity(0xa7,0x7e,false,_currentRoom,
                new DungeonMechanicDatabase(),p => p-new Vector2(80,48),() => _entities.FrameCounter,
                _sound.PlaySound,_rooms.TrySetTile,_entities.UpdateBossShutterSignal,
                () => _entities.DoorPaletteFadeActive,_entities.IsScriptTextActive);
            _entities.AddEntity(door);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,2,120,136);
            rom.Word(0xd00a,136*256+128); rom.Word(0xd00c,120*256+64);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom[0xffaa] = 48; rom[0xffac] = 80;
            rom[0xd240] = 1; rom[0xd241] = 0x1e; rom[0xd242] = 0x0e; rom[0xd24b] = 0xa7;
            var sounds = _sound.AttachPlayRequestAudit();
            var previous = _entities.PaletteFadeActiveSource;
            void Step(int count = 1) => StepSomariaMotionRom(rom,count,batch,afterUpdate:() => {
                rom.AdvanceTileGraphics();
                var random = _random.CaptureState();
                FailIf(door.Finished != (rom[0xd240] == 0) ||
                    SomariaPrivate<int>(door,"_counter") != rom[0xd246] ||
                    _entities.BossEntrySignal != rom[0xcc93] ||
                    _rooms.PendingTileGraphics != ((rom[0xcce0]-rom[0xccdf])&31) ||
                    random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                    random.Calls-seed.Calls != rom.RandomCalls || !sounds.Requests.SequenceEqual(rom.Sounds),
                    $"Minecart closing tile=${competingTile:x2}, batch={batch}: runtime state={door.State}/finished={door.Finished}/counter={SomariaPrivate<int>(door,"_counter")}/signal=${_entities.BossEntrySignal:x2}; native state=${rom[0xd244]:x2}:${rom[0xd245]:x2}/alive={rom[0xd240]}/counter={rom[0xd246]}/signal=${rom[0xcc93]:x2}.");
            });
            try
            {
                for (int wait = 0; rom[0xd244] != 3 && wait < 16; wait++) Step();
                FailIf(door.State != MinecartShutterState.ReadyToClose || rom[0xd244] != 3 || rom[0xd245] != 0,
                    "The open layout minecart script must select closing before the next interaction update.");
                FailIf(!_rooms.TrySetTile(0xa7,(byte)competingTile),"Competing closing input must fit the tile queue.");
                rom.SetTile(0xa7,(byte)competingTile);
                if (competingTile == 0xda)
                {
                    // ITEM$18 explicitly writes collision$0f after setTile;
                    // the metatile's ordinary collision-table byte is zero.
                    _currentRoom.SetPositionTileAndCollision(door.Position,0xda,0x0f,0);
                    rom[0xcea7] = 0x0f;
                }
                // Palette mode gates only opening, including when closing
                // skips an unrelated solid tile or displaces Somaria$da.
                _entities.PaletteFadeActiveSource = () => true; rom[0xc4ab] = 1;
                Step();
                if (competingTile == 0x7b)
                {
                    Step(3);
                    FailIf(!door.Finished || _currentRoom.Layout[0xa7] != 0x7b ||
                        !_currentRoom.IsSolid(door.Position) || sounds.Requests.Count != 0 || _entities.BossEntrySignal != 1,
                        "Unrelated solid tiles must skip closing and preserve the layout shutter signal without a cue.");
                    continue;
                }
                FailIf(door.State != MinecartShutterState.ClosingInterleaved || rom[0xd246] != 6 ||
                    _currentRoom.Layout[0xa7] != 0x5e || _currentRoom.IsSolid(door.Position) != (competingTile == 0xda),
                    $"Closing tile=${competingTile:x2} must interleave over Somaria despite its collision: runtime={door.State}/counter={SomariaPrivate<int>(door,"_counter")}/tile=${_currentRoom.Layout[0xa7]:x2}/collision={_currentRoom.IsSolid(door.Position)}; native=${rom[0xd244]:x2}:${rom[0xd245]:x2}/counter={rom[0xd246]}/tile=${rom[0xcfa7]:x2}/collision=${rom[0xcea7]:x2}.");
                Step(5);
                FailIf(rom[0xd246] != 1,"Closing must retain its sixth-update publication boundary.");
                if (competingTile == 0x5e)
                    for (int write = 0; write < 31; write++)
                    {
                        FailIf(!_rooms.TrySetTile(0x11,0xa0),"Closing rejection input must accept all31 declared writes.");
                        rom.SetTile(0x11,0xa0);
                    }
                Step(); Step(8);
                FailIf(!door.Finished || _entities.BossEntrySignal != 0 ||
                    _currentRoom.Layout[0xa7] != (competingTile == 0xda ? 0x7e : 0x5e) ||
                    _currentRoom.IsSolid(door.Position) != (competingTile == 0xda) || sounds.Requests.Count != 2,
                    "Closing must retire and clear the shutter signal even when canonical tile publication is rejected, without a later retry.");
            }
            finally { _entities.PaletteFadeActiveSource = previous; }
        }
    }
}
