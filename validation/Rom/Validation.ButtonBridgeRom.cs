using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareButtonBridgeRom()
    {
        static Vector2 Point(int packed) => new((packed&15)*16+8,(packed>>4)*16+8);
        foreach (int branch in new[] {0,1,2,3})
        foreach (bool batch in RomHostSchedules(branch))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0xad); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0); _player.WarpTo(new(72.25f,136.5f)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position),"Bridge approach must begin on original Crown$4:$ad floor.");
            // Original mainData.s has PART$09:$80 at$74, then INTERAC$90:$19.
            // Exclude the following enemy pointer while retaining both owners.
            var bridge = new ButtonBridgeRoomEntity(new CrownDungeonDatabase().ButtonBridge,_currentRoom,
                () => _entities.ActiveTriggers,(packed,tile) => {
                    _currentRoom.SetUnderlyingStorageMetatile(packed,tile); _rooms.TrySetTile(packed,tile);
                },_sound.PlaySound,_entities.TryCreateRockDebris);
            var mechanics = new DungeonMechanicDatabase();
            var button = new GroundButtonRoomEntity(mechanics.GetRoomRecords(4,0xad).Single(row=>row.Id == 9),
                _currentRoom,mechanics,_entities.SetTrigger,(packed,tile)=>_rooms.TrySetTile(packed,tile),_sound.PlaySound);
            _entities.AddEntity(button); _entities.AddEntity(bridge);
            FailIf(_entities.InteractionSlot(bridge) != 2 || button.PackedPosition != 0x74 || button.SubId != 0x80,
                "Bridge fixture must retain the original controller slot and reusable trigger-bit0 button.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,0,72,136) { HostilePartsEnabled = true };
            rom.Word(0xd00c,72*256+64); rom.Word(0xd00a,136*256+128);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom[0xcc39] = 5;
            rom[0xd240] = 1; rom[0xd241] = 0x90; rom[0xd242] = 0x19;
            rom[0xd0c0] = 1; rom[0xd0c1] = 9; rom[0xd0c2] = 0x80; rom[0xd0cb] = 120; rom[0xd0cd] = 72;
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1,int angle = 0xff) => StepGameplayUpdates(count,
                angle == 0xff ? Vector2.Zero : OracleObjectMath.StrictCardinalVector(angle),batched:batch,afterUpdate:() => {
                rom.UpdateGameplay(0,angle switch {0=>0x40,16=>0x80,_=>0},angle,_entities.FrameCounter);
                rom.AdvanceTileGraphics();
                string context = $"Bridge branch{branch}, batch={batch}, update{++update}";
                FailIf(bridge.State != rom[0xd244] || bridge.Counter != rom[0xd246] ||
                    button.Pressed != (rom[0xd0f0] != 0) || _entities.ActiveTriggers != rom[0xcca0] ||
                    _player.PrecisePosition != new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f) ||
                    !sounds.Requests.Where(cue=>cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                    context+$": controller={bridge.State}/{rom[0xd244]}, counter={bridge.Counter}/{rom[0xd246]}, trigger/Link/cues differ.");
                foreach (int packed in new[] {0x55,0x56,0x57,0x58,0x59,0x74})
                    FailIf(_currentRoom.Layout[packed] != rom[0xcf00+packed] ||
                        _currentRoom.GetUnderlyingStorageMetatile(packed) != rom.Underlying(packed) ||
                        _currentRoom.GetTerrainInfo(Point(packed)).Collision != rom[0xce00+packed],
                        context+$": native layout/collision/underlying bytes differ at${packed:x2}.");
                var debris = _entities.Entities<RockDebrisEffect>();
                int[] native = Enumerable.Range(0xd2,14).Select(page=>page*256+0x40)
                    .Where(slot=>rom[slot] != 0 && rom[slot+1] == 6).ToArray();
                FailIf(debris.Count != native.Length,"Bridge debris allocation/lifetime differs from the checked native pool.");
                for (int index = 0; index < debris.Count; index++)
                    FailIf(_entities.InteractionSlot(debris[index]) != (native[index]>>8)-0xd0 ||
                        debris[index].Position != new Vector2(rom[native[index]+0xd],rom[native[index]+0xb]) ||
                        debris[index].CurrentParameter != rom[native[index]+0x21],
                        "Bridge debris must retain the unpositioned controller origin and later-slot same-update initialization.");
                var random = _random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls,
                    context+": shared RNG order differs.");
            });
            _dialogue.ShowGameplayMessage("Pending bridge",120); rom[0xcba0] = 1;
            Step(3); _dialogue.Close(); rom[0xcba0] = 0;
            for (int repeat = 0; repeat < 2; repeat++)
            {
                for (int walk = 0; bridge.State != 2 && walk < 40; walk++) Step(angle:0);
                FailIf(bridge.State != 2 || bridge.Counter != 8,"Actual button pressure must install wait8 before extension.");
                if (branch == 0 && repeat == 0)
                {
                    Step(3); Step(angle:16);
                    FailIf(bridge.State != 4 || bridge.Counter != 5,"Early actual release must preserve the remaining bridge delay.");
                    Step(angle:0);
                    FailIf(bridge.State != 1 || bridge.Counter != 5,"Immediate actual repress must return to waiting without decrementing.");
                    Step();
                    FailIf(bridge.State != 2 || bridge.Counter != 8,"The following pressed update must restart wait8.");
                }
                Step(7);
                if (branch == 1 && repeat == 0) for (int queued = 0; queued < 31; queued++)
                {
                    byte tile = _currentRoom.Layout[0x11];
                    FailIf(!_rooms.TrySetTile(0x11,tile),"Bridge fixture must accept31 queued writes before wait zero.");
                    rom.SetTile(0x11,tile);
                }
                Step();
                FailIf(_currentRoom.GetUnderlyingStorageMetatile(0x55) != 0x6d ||
                    _currentRoom.Layout[0x55] != (branch == 1 && repeat == 0 ? 0xf4 : 0x6d),
                    "Native setTileInAllBuffers must publish underlying bridge even when the graphics queue rejects its live write.");
                _dialogue.ShowGameplayMessage("Bridge wait",120); rom[0xcba0] = 0x80;
                Step(3); _dialogue.Close(); rom[0xcba0] = 0;
                for (int wait = 0; bridge.State != 3 && wait < 56; wait++) Step();
                FailIf(bridge.State != 3 || Enumerable.Range(0x55,5).Any(p=>_currentRoom.Layout[p] != 0x6d),
                    "Original rescan must retry a rejected hole after8 updates and finish five tiles plus its empty scan.");
                if (branch >= 2)
                {
                    // Declare the movable-diamond producer's completed tile.
                    _currentRoom.SetUnderlyingStorageMetatile(0x59,0xdb);
                    FailIf(!_rooms.TrySetTile(0x59,0xdb),"Diamond handoff must fit the drained queue."); rom.SetTile(0x59,0xdb);
                    rom.CopyRoom(_currentRoom);
                    if (branch == 3) for (int slot = 3; slot < 16; slot++)
                    {
                        _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(200,120),SoundId.MusNone));
                        int address = (0xd0+slot)*256+0x40;
                        rom[address] = 1; rom[address+1] = 5; rom[address+2] = 0x80; rom[address+0xb] = 120; rom[address+0xd] = 200;
                    }
                }
                for (int walk = 0; bridge.State != 4 && walk < 40; walk++) Step(angle:16);
                FailIf(bridge.State != 4,"Leaving the actual button must retain wait8 for retraction.");
                Step(8);
                FailIf(_currentRoom.Layout[0x59] != 0xf4 ||
                    _entities.Entities<RockDebrisEffect>().Count != (branch == 2 ? 1 : 0),
                    "Right-first retraction must rewrite a diamond irrespective of checked debris capacity.");
                for (int wait = 0; bridge.State != 1 && wait < 48; wait++) Step();
                FailIf(bridge.State != 1 || Enumerable.Range(0x55,5).Any(p=>_currentRoom.Layout[p] != 0xf4),
                    "Five retractions and the empty scan must restore waiting without an allocation retry.");
            }
            LoadValidationRoom(0,0x60);
            FailIf(_entities.Entities<ButtonBridgeRoomEntity>().Count != 0,"Room replacement must retire its bridge controller.");
        }
    }
}
