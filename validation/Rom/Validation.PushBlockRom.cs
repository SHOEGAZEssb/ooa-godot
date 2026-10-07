using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ComparePushBlockDataRom()
    {
        ReadOnlyMemory<byte> image = ValidationRom.LoadCleanUs();
        byte Native(int bank, int address) => image.Span[bank * 0x4000 + address - 0x4000];
        var imported = new PushableTileDatabase();
        var dispatch = new InteractableTileDatabase();
        int dispatchRecords = 0;
        int records = 0;
        for (int mode = 0; mode < 6; mode++)
        for (int tile = 0; tile < 256; tile++)
        {
            // Clean US bank06 interactableTilesTable and bank08 dbrel
            // pushableTilePropertiesTable are independent of generated data.
            int pointer = 0x43b2 + mode * 2;
            int cursor = Native(6, pointer) | Native(6, pointer + 1) << 8;
            int parameter = -1;
            while (Native(6, cursor) != 0)
            {
                if (Native(6, cursor) == tile) { parameter = Native(6, cursor + 1); break; }
                cursor += 2;
            }
            byte dispatchParameter = dispatch.Parameter(mode,(byte)tile);
            FailIf(dispatchParameter != unchecked((byte)parameter),
                $"Native interactable lookup mode${mode:x2}/tile${tile:x2}: imported=${dispatchParameter:x2}, ROM=${parameter & 255:x2}.");
            if (parameter >= 0) dispatchRecords++;
            if (tile == 0xda)
                FailIf(!imported.TryGetSomaria(mode,(byte)tile,out byte somariaParameter) ||
                    somariaParameter != dispatchParameter || somariaParameter != 0x80,
                    $"Native Somaria push dispatch mode${mode:x2}/tile$da must use parameter$80.");
            bool expected = parameter >= 0 && (parameter & 15) == 0 && tile != 0xda;
            bool found = imported.TryGet(mode, (byte)tile, out var row);
            FailIf(found != expected, $"Native push dispatch mode${mode:x2}/tile${tile:x2}: imported={found}, native parameter=${parameter & 0xff:x2}.");
            if (!expected) continue;
            records++;
            pointer = 0x452d + mode;
            cursor = pointer + Native(8, pointer);
            while (Native(8, cursor) != 0 && Native(8, cursor) != tile) cursor += 4;
            // A native lookup miss retains the freshly allocated var32..34
            // zero bytes; it still dispatches INTERAC$14 and moves the block.
            var expectedRow = Native(8, cursor) == 0 ? new PushableTileRecord((byte)parameter, 0, 0, 0) :
                new PushableTileRecord((byte)parameter, Native(8, cursor + 1), Native(8, cursor + 2), Native(8, cursor + 3));
            FailIf(row != expectedRow, $"Native push properties mode${mode:x2}/tile${tile:x2}: imported={row}, ROM={expectedRow}.");
        }
        FailIf(records != 51, $"Clean-US ordinary push dispatch requires51 mode/tile rows, got{records}.");
        FailIf(dispatchRecords != 115,
            $"Clean-US interactable lookup requires115 entries across all six alias/fallthrough profiles, got{dispatchRecords}.");
    }

    private void ComparePushBlockGameplayRom()
    {
        int fixture = 0;
        foreach (bool wrongDirection in new[] { false, true })
        foreach (int level in wrongDirection ? new[] { 0 } : new[] { 0, 2 })
        foreach (bool batched in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4, 0x09); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            if (level != 0) _inventory.GiveTreasure(TreasureId.Bracelet, level);
            Vector2 start = wrongDirection ? new(216.25f, 56.5f) : new(184.25f, 56.5f);
            _player.WarpTo(start); _player.Face(wrongDirection ? Vector2I.Left : Vector2I.Right);
            FailIf(_collision.Collides(start), "Pushblock approach must start on original room$4:$09 floor.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, wrongDirection ? 3 : 1, (int)start.X, (int)start.Y);
            rom.Word(0xd00a, (int)(start.Y * 256)); rom.Word(0xd00c, (int)(start.X * 256));
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1, int angle = 0xff) =>
                StepSomariaMotionRom(rom, count, batched, angle, afterUpdate: () =>
                {
                    string context = $"Pushblock$4:$09 wrong-direction={wrongDirection}, Bracelet L{level}, batch={batched}, update={++update}";
                    bool active = rom[0xd140] != 0 && rom[0xd141] == 0x14;
                    FailIf(_pushBlocks.Active != active || _pushBlocks.RemainingPushFrames != rom[0xcc6a],
                        context + $": allocation/push clock runtime={_pushBlocks.Active}/{_pushBlocks.RemainingPushFrames}, native={active}/{rom[0xcc6a]}.");
                    if (active)
                        FailIf(_pushBlocks.ActiveTile != rom[0xd171] || _pushBlocks.PushAngle != (rom[0xd149] & 31) ||
                            _pushBlocks.BlockTopLeft + new Vector2(8, 6) !=
                                new Vector2(rom.Word(0xd14c) / 256f, rom.Word(0xd14a) / 256f) ||
                            _pushBlocks.BlockZHigh != unchecked((sbyte)rom[0xd14f]) ||
                            _pushBlocks.ActiveMoveFrames - (int)SomariaPrivate<float>(_pushBlocks, "_moveFrame") != rom[0xd146],
                            context + ": INTERAC$14 full position/properties/Z/movement counter differs.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls - seed.Calls != rom.RandomCalls ||
                        !sounds.Requests.SequenceEqual(rom.Sounds), context + ": ordered movement cues/shared RNG differ.");
                });
            int direction = wrongDirection ? 24 : 8;
            Step(); Step(12, direction);
            _dialogue.ShowMessage("Pushblock pause.", _player.Position.Y); rom[0xcba0] = 1;
            Step(3, direction); _dialogue.Close(); rom[0xcba0] = 0;
            Step(3); Step(72, direction);
            FailIf(_pushBlocks.Active || rom[0xd140] != 0 || wrongDirection &&
                _currentRoom.GetMetatile(new(200, 56)) != 0x19,
                "Rejected one-way input must retain its tile; accepted pushes must complete within the bounded approach.");
            FailIf(sounds.Requests.Count(cue => cue == SoundId.SndMoveBlock) != (wrongDirection ? 0 : 1) ||
                !wrongDirection && (_currentRoom.GetMetatile(new(200, 56)) != 0xa0 ||
                    _currentRoom.GetMetatile(new(216, 56)) != 0x1d),
                "Native$19 right push must replace source$3c with floor$a0 and destination$3d with stationary$1d exactly once.");
            Step(3); Step(24, direction);
            FailIf(sounds.Requests.Count(cue => cue == SoundId.SndMoveBlock) != (wrongDirection ? 0 : 1),
                "Repeated rejected/finished one-way push must not allocate another moving interaction.");
        }
    }
}
