using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ComparePushBlockBraceletHintRom()
    {
        var image = ValidationRom.LoadCleanUs(); var data = new TileInfoTextDatabase();
        // Original bank06:$4315 showInfoTextForTile@data, independent of import.
        for (int index = 0; index < 10; index++)
        {
            int address = 6 * 0x4000 + 0x315 + index * 2;
            int text = 0x5100 | image.Span[address + 1];
            FailIf(data.Get(text).Mask != image.Span[address],$"Native tile hint TX_{text:x4} suppression mask differs.");
        }
        // Independent text.yaml: $5105 jumps to $5107; unterminated $5106
        // falls through with a trailing newline command. Neither may truncate.
        FailIf(data.Get(0x5105).Message != "This block has\ncracks in it.\nIt looks like it\ncould be broken!" ||
            data.Get(0x5106).Message != "This wall is\ncracked.\nIt looks like it\ncould be broken!",
            "Tile hint TX_5105 jump/TX_5106 fallthrough must preserve the full source-derived TX_5107 suffix.");
        int fixture = 0;
        foreach (int level in new[] { -1,0,1,2 })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            bool obtained = level >= 0;
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0xce); _entities.Clear();
            // Deliberately obtain the treasure with level0: native eligibility
            // reads its flag, while movement speed reads the separate level byte.
            if (obtained) _inventory.GiveTreasure(TreasureId.Bracelet,level);
            _player.WarpTo(new(120,24)); _player.Face(Vector2I.Down);
            FailIf(_currentRoom.IsSolid(_player.Position) || _currentRoom.GetMetatile(new(120,40)) != 0x10,
                "Pot contact must start on original room$4:$ce floor above tile$10 at$27.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,2,120,24);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1,int angle = 0xff) =>
                StepSomariaMotionRom(rom,count,batch,angle,afterUpdate:() =>
                {
                    string context = $"Pot Bracelet obtained={obtained}/level{level} batch={batch}, update={++update}";
                    FailIf(_pushBlocks.RemainingPushFrames != rom[0xcc6a] ||
                        _pushBlocks.Active != (rom[0xd140] != 0) ||
                        _rooms.InformativeTextsShown != rom[0xccd7] || _dialogue.IsOpen != (rom[0xcba0] != 0),
                        context + ": contact clock/allocation/hint modal/shared mask differs.");
                    if (_pushBlocks.Active)
                        FailIf(_pushBlocks.BlockTopLeft + new Vector2(8,6) !=
                            new Vector2(rom.Word(0xd14c) / 256f,rom.Word(0xd14a) / 256f) ||
                            _pushBlocks.ActiveMoveFrames - (int)SomariaPrivate<float>(_pushBlocks,"_moveFrame") != rom[0xd146],
                            context + ": full block position/movement clock differs.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                        random.Calls - seed.Calls != rom.RandomCalls ||
                        !sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                        context + ": gameplay cues/shared RNG differs (textbox printing is a separate fixture).");
                });
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Step();
                int limit = repeat == 1 && !obtained ? 24 : 80;
                for (int wait = 0; !_pushBlocks.Active && !_dialogue.IsOpen && wait < limit; wait++)
                {
                    Step(angle:16);
                    FailIf(_currentRoom.IsSolid(_player.Position),"Pot approach entered source solid geometry.");
                }
                if (obtained)
                {
                    int frames = level == 2 ? 21 : 32;
                    FailIf(!_pushBlocks.Active || _pushBlocks.ActiveMoveFrames != frames || rom.TextGeneration != 0,
                        "Obtained Bracelet must use native SPEED_80/$20 or L2 SPEED_c0/$15 without a hint.");
                    Step(frames - 1);
                    FailIf(_pushBlocks.Active || _currentRoom.GetMetatile(new(120,56 + repeat * 16)) != 0x10,
                        "Native pot must finish on its exact movement boundary at$37/$47.");
                }
                else if (repeat == 0)
                {
                    FailIf(!_dialogue.IsOpen || _rooms.InformativeTextsShown != 0x08 ||
                        rom.TextGeneration != 1 || rom[0xcba2] != 3 || rom[0xcba3] != 0x55 ||
                        _currentRoom.GetMetatile(new(120,40)) != 0x10,
                        $"Missing Bracelet must show native TX_5103 once, retain tile$10 and publish mask$08: open={_dialogue.IsOpen}, mask=${_rooms.InformativeTextsShown:x2}, generation={rom.TextGeneration}, text bytes=${rom[0xcba2]:x2}/${rom[0xcba3]:x2}, tile=${_currentRoom.GetMetatile(new(120,40)):x2}.");
                    Step(3,16); _dialogue.Close(); rom[0xcba0] = 0;
                }
                else FailIf(_dialogue.IsOpen || _pushBlocks.Active || rom.TextGeneration != 1,
                    "Held missing-Bracelet retry must retain the tile without reopening TX_5103.");
            }
            if (!obtained)
            {
                // Other hints share bits ($5100/$5102), while pot$08 is independent.
                FailIf(_rooms.PrepareTileInfoMessage(0x5100) is null ||
                    _rooms.PrepareTileInfoMessage(0x5102) is not null || _rooms.InformativeTextsShown != 0x0c,
                    "Key-door/key-block hint suppression must share bit$04 without overwriting pot bit$08.");
                LoadValidationRoom(4,0xce);
                FailIf(_rooms.InformativeTextsShown != 0 || _rooms.PrepareTileInfoMessage(0x5103) is null,
                    "Room re-entry must clear the shared hint byte and permit TX_5103 again.");
            }
        }
    }
}
