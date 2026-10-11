using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom3ceRom()
    {
        foreach (bool batch in RomHostSchedules(0))
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(3, 0xce);
            _inventory.EquipA(0); _inventory.EquipB(0);
            _dialogue.MessageSpeed = 4; _saveData.SetTextSpeed(4);
            var npc = _entities.Entities<NpcCharacter>().Single();
            const string text = "My family's been\nfishing this\nisland since my\ngrandpa's day.\\stop\nThe island is\ndrifting to the\nwest, so long\nago it was\nfarther east.";
            FailIf(npc.Record is not { Id: 0x3b, SubId: 8, Var03: 0, TextId: 0x0f03,
                X: 0x50, Y: 0x40, CanFace: true, Implementation: NpcImplementationClassification.OrdinaryGeneric } ||
                npc.Message != text || !npc.Visible,
                "Room3:ce must expose femaleVillager$3b:$08 at $4050 with complete TX_0f03 and its stop boundary.");
            _player.WarpTo(new(80, 104)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position) || _entities.BlocksLink(_player.Position),
                "Room3:ce conversation must start in actual open house geometry.");
            var seed = _random.CaptureState();
            var rom = new DialogueRom(_saveData);
            rom.InitializeGameplay(_currentRoom, seed, 80, 104);
            rom[0xd008] = 0; rom[0xd009] = 0;
            int slot = _entities.InteractionSlot(npc);
            int address = 0xd040 + slot * 0x100;
            rom.AddNpc(slot, 0x3b, 8, 80, 64);
            // Match the port's room-entry initialization with the original
            // state-zero dispatch; subsequent checks use the full gameplay loop.
            rom.InitializeNpcInteractions();
            FailIf(rom.Word(address + 0x32) != 0x0f03 || rom[address + 4] != 1,
                "Clean-US femaleVillager$08 initializer must select TX_0f03 and state$01.");
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0, lastHeld = 0;
            bool sawStop = false;
            void Step(int count = 1, int held = 0)
            {
                int edge = held & ~lastHeld; lastHeld = held;
                Vector2 move = (held & 0x40) != 0 ? Vector2.Up : Vector2.Zero;
                StepGameplayUpdates(count, move, MenuRomActions(held), MenuRomActions(edge), batch, () =>
                {
                    rom.AdvanceGameplay(edge, held, move == Vector2.Up ? 0 : 0xff, _entities.FrameCounter);
                    edge = 0;
                    string context = $"Room3:ce femaleVillager$3b:$08 batch={batch}, update{++update}";
                    FailIf(_player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f) ||
                        _dialogue.IsOpen != rom.Active || npc.FacingVector != OracleObjectMath.StrictCardinalVector(rom[address + 9]) ||
                        SomariaPrivate<double>(npc, "_faceCooldownFrames") != rom[address + 0x2b],
                        context + $": XY={_player.PrecisePosition}/{rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}; " +
                        $"text={_dialogue.IsOpen}/{rom.Active}, facing={npc.FacingVector}/${rom[address+9]:x2}, " +
                        $"turn counter={SomariaPrivate<double>(npc, "_faceCooldownFrames")}/{rom[address+0x2b]}.");
                    if (rom.Active && rom.State != 0x10) CompareDialogueLineRom(rom, context);
                    sawStop |= rom.Active && (rom.Text(0xd0c1) & 2) != 0;
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds), context + ": text sound order differs.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls - seed.Calls != rom.RandomCalls,
                        context + ": NPC/text shared RNG differs.");
                });
            }
            Step(3);
            for (int wait = 0; _player.Position.Y > 78 && wait < 40; wait++) Step(held: 0x40);
            Step();
            FailIf(_player.Position.Y > 78 || _currentRoom.IsSolid(_player.Position) || _entities.BlocksLink(_player.Position),
                "Room3:ce villager must be reachable from the actual house entrance without occupying her hitbox.");
            for (int repeat = 0; repeat < 2; repeat++)
            {
                sawStop = false;
                Step(held: 1); Step(2);
                FailIf(!_dialogue.IsOpen,
                    "Reachable A-button contact must open TX_0f03 through the generic NPC script.");
                int limit = update + 600;
                while (rom.Active && update < limit)
                {
                    bool lineDone = rom.Text(0xd0d0) >= 16 || rom.Text(0xd400 + rom.Text(0xd0d0)) == 0;
                    bool waiting = rom.State is 5 or 0x0f || (rom.Text(0xd0c1) & 2) != 0 && lineDone;
                    // Release A between page advances so each press is a real
                    // input edge in both the port and the native gameplay loop.
                    bool pressA = waiting ? (lastHeld & 1) == 0 : update % 4 == 0;
                    Step(batch && !waiting ? 3 : 1, pressA ? 1 : 0);
                }
                FailIf(rom.Active || !sawStop,
                    $"TX_0f03 must preserve its stop/page boundary and finish the native text thread: active={rom.Active}, stop={sawStop}, state=${rom.State:x2}, flags=${rom.Text(0xd0c1):x2}, update={update}, repeat={repeat}.");
                Step(3);
                FailIf(_dialogue.BlocksPlayerInput,
                    "TX_0f03 must release its closing input before repeated conversation.");
            }
            // Room replacement cancels an open ordinary conversation and its
            // target. Re-entry must create the same source actor and text.
            Step(held: 1); Step(2);
            FailIf(!_dialogue.IsOpen, "Room3:ce cancellation fixture must have an active repeated conversation.");
            LoadValidationRoom(3, 0xcf);
            FailIf(_dialogue.IsOpen || _entities.Entities<NpcCharacter>().Contains(npc),
                "Leaving room3:ce must close text and retire its talk target.");
            LoadValidationRoom(3, 0xce);
            var returned = _entities.Entities<NpcCharacter>().Single();
            FailIf(returned.TextId != 0x0f03 || returned.Message != text || returned.Position != new Vector2(80, 64),
                "Room3:ce re-entry must restore its ordinary source conversation.");
        }
        GD.Print("Validated room3:ce femaleVillager$3b:$08 initialization, reachable/repeated conversation, facing, complete TX_0f03 paging/sound/RNG, cancellation and re-entry with clean-US gameplay in split/batched updates.");
    }
}
