using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareBlockPatternKeyHandoffRom()
    {
        int fixture = 0;
        foreach (var test in new[] { (Source:0x4b,Goal:0x3b,Angle:0),(Source:0x5a,Goal:0x59,Angle:24),(Source:0x5c,Goal:0x5d,Angle:8) })
        foreach (bool batched in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0x64);
            _inventory.EquipA(0); _inventory.EquipB(0);
            var parent = _entities.Entities<DungeonTilePatternFallingKeyRoomEntity>().Single();
            var direction = OracleObjectMath.StrictCardinalVector(test.Angle);
            Vector2 source = new((test.Source&15)*16+8,(test.Source>>4)*16+8);
            Vector2 start = source-direction*16+new Vector2(0.25f,-0.5f);
            _player.WarpTo(start); _player.Face((Vector2I)direction);
            FailIf(_collision.Collides(_player.Position) || _currentRoom.GetTerrainInfo(_player.Position+Vector2.Down*5).Hazard != HazardType.None,
                $"Block-key approach requires original room$04:$64 geometry beside ${test.Source:x2}.");
            // Declare only the other two completed goals. This fixture isolates
            // one real block-to-key handoff, without a room progression test.
            foreach (int goal in new[] { 0x3b,0x59,0x5d })
                if (goal != test.Goal) _currentRoom.SetPositionTileAndCollision(new((goal&15)*16+8,(goal>>4)*16+8),0x1d,null,0);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,test.Angle/8,(int)start.X,(int)start.Y);
            rom.Word(0xd00a,(int)(start.Y*256)); rom.Word(0xd00c,(int)(start.X*256));
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom[0xcc39] = 3;
            int address = (0xd0+_entities.InteractionSlot(parent))*256+0x40;
            rom[address] = 1; rom[address+1] = 0x21; rom[address+2] = 9; rom[address+0xb] = 0x68; rom[address+0xd] = 0xb8;
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0; bool sawMotion = false, sawHandoff = false;
            void Step(int count = 1,int angle = 0xff) => StepGameplayUpdates(count,
                angle == 0xff ? Vector2.Zero : OracleObjectMath.StrictCardinalVector(angle),batched:batched,afterUpdate:() =>
            {
                // Camera origin is a declared presentation input; original
                // block motion, interaction dispatch and Link XY stay live.
                Vector2 screenOrigin = _entities.ToScreen(Vector2.Zero);
                rom[0xffaa] = unchecked((byte)-(int)screenOrigin.Y); rom[0xffac] = unchecked((byte)-(int)screenOrigin.X);
                int held = angle switch { 0 => 0x40,8 => 0x10,16 => 0x80,24 => 0x20,_ => 0 };
                rom.UpdateGameplay(0,held,angle,_entities.FrameCounter);
                CompareSomariaMotionRom(rom,$"Block-key ${test.Source:x2}, update{update+1}, batch={batched}");
                rom.AdvanceTileGraphics(); update++;
                bool moving = rom[0xd140] != 0 && rom[0xd141] == 0x14;
                sawMotion |= moving;
                int[] native = Enumerable.Range(0xd2,14).Select(page => page*256+0x40)
                    .Where(slot => rom[slot] != 0 && rom[slot+1] == 0x60).ToArray();
                var keys = _entities.Entities<GroundTreasurePickup>();
                FailIf(_pushBlocks.Active != moving || _pushBlocks.RemainingPushFrames != rom[0xcc6a] ||
                    parent.Finished != (rom[address] == 0) || keys.Count != native.Length ||
                    !sounds.Requests.SequenceEqual(rom.Sounds),
                    $"Block-key ${test.Source:x2}->${test.Goal:x2}, update{update}: push clock/parent/physical key/cues differ.");
                if (keys.Count != 0)
                {
                    var key = keys.Single(); int slot = native.Single(); sawHandoff = true;
                    FailIf(_entities.InteractionSlot(key) != (slot>>8)-0xd0 || key.Position != new Vector2(184,104) ||
                        (int)key.State != rom[slot+4] || key.SpawnSubstate != rom[slot+5] ||
                        key.SpawnSubstate == 1 && key.SpawnCounter != rom[slot+6] ||
                        unchecked((ushort)key.ZFixed) != rom.Word(slot+0xe) || key.SpeedZ != unchecked((short)rom.Word(slot+0x14)) ||
                        key.Visible != ((rom[slot+0x1a]&0x80) != 0),
                        $"Block-key ${test.Source:x2}, update{update}: key slot={_entities.InteractionSlot(key)}/{(slot>>8)-0xd0}, XY={key.Position}, state={key.State}:{key.SpawnSubstate}:{key.SpawnCounter}/{rom[slot+4]}:{rom[slot+5]}:{rom[slot+6]}, z/vz={key.ZFixed}/{key.SpeedZ}:{rom.Word(slot+0xe)}/{unchecked((short)rom.Word(slot+0x14))}, visible={key.Visible}/{rom[slot+0x1a]:x2}.");
                }
                var random = _random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls,
                    "Actual block-key handoff must preserve shared RNG.");
            });
            Step(); Step(80,test.Angle);
            FailIf(!sawMotion || !sawHandoff || !parent.Finished || _pushBlocks.Active || _currentRoom.Layout[test.Source] != 0xa0 ||
                _currentRoom.Layout[test.Goal] != 0x1d,"Original directional push must complete its single goal and falling-key handoff.");
            Step(20,test.Angle); Step(2);
            FailIf(_entities.Entities<GroundTreasurePickup>().Count != 1 || sounds.Requests.Count(cue => cue == SoundId.SndMoveBlock) != 1,
                "Repeating the finished directional push must preserve the one falling key without another block allocation.");
        }
    }
}
