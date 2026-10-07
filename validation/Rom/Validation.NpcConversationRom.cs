using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateNpcConversationRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(GlobalFlag.IntroDone);
            LoadValidationRoom(3, 0xf7);
            _dialogue.MessageSpeed = 4;
            _saveData.SetTextSpeed(4);
            _inventory.GiveTreasure(TreasureId.Feather,1);
            _inventory.EquipA(0); _inventory.EquipB(TreasureId.Feather);
            _player.WarpTo(new(80, 112)); _player.Face(Vector2I.Down);
            var birds = _entities.Entities<KnowItAllBirdCharacter>().ToArray();
            var seed = _random.CaptureState();
            var rom = new DialogueRom(_saveData);
            rom.InitializeGameplay(_currentRoom, seed, 80, 112);
            rom[0xd008] = 2; rom[0xd009] = 0xff;
            foreach (var bird in birds)
                rom.AddBird(_entities.InteractionSlot(bird), bird.Record.SubId, (int)bird.Position.X, (int)bird.Position.Y);
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0;
            void Step(int count, Vector2 movement, int mask = 0)
            {
                string[] actions = new[] { "attack", "item", "map", "inventory", "move_right", "move_left", "move_up", "move_down" }
                    .Where((_, bit) => (mask & (1 << bit)) != 0).ToArray();
                int angle = movement == Vector2.Up ? 0 : movement == Vector2.Right ? 8 : movement == Vector2.Down ? 16 : movement == Vector2.Left ? 24 : 0xff;
                int edge = mask;
                StepGameplayUpdates(count, movement, actions, actions, batched, () =>
                {
                    rom.AdvanceGameplay(edge, mask, angle, _entities.FrameCounter); edge = 0; update++;
                    string context = $"NPC $3:$f7 $e3 batch={batched}, update={update}, text=${rom[0xcba1]:x2}/${rom.State:x2}";
                    FailIf(_player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256.0f, rom.Word(0xd00a) / 256.0f) ||
                        _dialogue.IsOpen != rom.Active || _entities.PlayerUpdatesFrozen != (rom[0xcc8a] == 0x91),
                        $"{context}: player position/text/object-disable handoff differs: runtime XY={_player.PrecisePosition}, native XY=${rom.Word(0xd00c):x4}/${rom.Word(0xd00a):x4}, disabled=${rom[0xcc8a]:x2}.");
                    FailIf(_player.TopDownAirborne != (rom[0xcc5c] != 0) ||
                        (_player.ItemCreationZFixed & 0xffff) != rom.Word(0xd00e) ||
                        (_player.TopDownAirSpeedZ & 0xffff) != rom.Word(0xd014),
                        context + ": Link air/Z/vertical speed differs.");
                    foreach (var bird in birds)
                    {
                        int address = 0xd040 + _entities.InteractionSlot(bird) * 0x100;
                        FailIf(bird.Talking != (rom[address + 5] == 1) || bird.TalkingSignal != (rom[address + 0x37] != 0) ||
                            bird.Direction != rom[address + 8] || bird.TurnCounter != rom[address + 0x36] ||
                            (ushort)bird.ZFixed != rom.Word(address + 0x0e) || (ushort)bird.SpeedZ != rom.Word(address + 0x14) ||
                            (bird.TextId & 255) != rom[address + 0x32],
                            $"{context}: subid=${bird.Record.SubId:x2}, native state/sub=${rom[address + 4]:x2}/${rom[address + 5]:x2}, " +
                            $"text=${rom.Word(address + 0x32):x4}, Z/speed=${rom.Word(address + 0x0e):x4}/${rom.Word(address + 0x14):x4}, " +
                            $"runtime talk/signal={bird.Talking}/{bird.TalkingSignal}, direction={bird.Direction}, counter={bird.TurnCounter}, text=${bird.TextId:x4}, Z/speed={bird.ZFixed}/{bird.SpeedZ}.");
                    }
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls - seed.Calls != rom.RandomCalls,
                        $"{context}: ordered shared RNG differs.");
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds), $"{context}: sound request order differs: [{string.Join(',', sounds.Requests)}] / [{string.Join(',', rom.Sounds)}].");
                });
            }
            void Walk(Vector2 destination)
            {
                for (int axis = 0; axis < 2; axis++)
                {
                    int limit = 220;
                    while (Mathf.Abs(axis == 0 ? _player.Position.X - destination.X : _player.Position.Y - destination.Y) > 1)
                    {
                        FailIf(--limit == 0, $"NPC ROM approach blocked {_player.Position} -> {destination}.");
                        Vector2 movement = axis == 0 ? new(Mathf.Sign(destination.X - _player.Position.X), 0) : new(0, Mathf.Sign(destination.Y - _player.Position.Y));
                        Step(1, movement);
                    }
                }
                Step(1, Vector2.Zero);
                FailIf(_currentRoom.IsSolid(_player.Position) || _entities.BlocksLink(_player.Position),
                    $"NPC ROM approach ended inside solid geometry at {_player.Position}.");
            }
            void FinishText()
            {
                int limit = update + 1600;
                while (rom.Active && update < limit)
                {
                    bool lineDone = rom.Text(0xd0d0) >= 16 || rom.Text(0xd400 + rom.Text(0xd0d0)) == 0;
                    bool waiting = rom.State is 5 or 0x0f || (rom.Text(0xd0c1) & 2) != 0 && lineDone;
                    Step(batched && !waiting ? 3 : 1, Vector2.Zero, waiting || update % 4 == 0 ? 1 : 0);
                }
                FailIf(rom.Active, "NPC ROM tutorial never completed.");
            }
            Step(3, Vector2.Zero);
            foreach (var bird in birds)
            {
                Walk(new(80, _player.Position.Y)); Walk(new(80, bird.Position.Y));
                Walk(new(bird.Position.X < 80 ? 40 : 120, bird.Position.Y));
                // Walk the last pixel toward the bird so both owners set facing.
                Step(1, bird.Position.X < 80 ? Vector2.Left : Vector2.Right);
                if (bird.Record.SubId == 0)
                {
                    Step(1,Vector2.Zero,2);
                    FailIf(!_player.TopDownAirborne,"Reachable bird air probe must launch through equipped Feather.");
                    Step(1,Vector2.Zero,1);
                    FailIf(_dialogue.IsOpen || birds.Any(actor => actor.Talking || actor.TalkingSignal),
                        "Fresh A during Feather air must bypass native A-sensitive bird selection.");
                    for (int wait = 0; _player.TopDownAirborne && wait < 48; wait++) Step(1,Vector2.Zero);
                    FailIf(_player.TopDownAirborne,"Bird conversation must wait for the actual Feather landing.");
                    Step(1,Vector2.Zero);
                }
                for (int choice = 1; choice >= 0; choice--)
                {
                    Step(1, Vector2.Zero, 1);
                    Step(2, Vector2.Zero);
                    FailIf(!_dialogue.IsOpen, $"$e3:${bird.Record.SubId:x2} reachable A-button conversation did not open.");
                    int limit = update + 900;
                    while (!(rom[0xcba1] == 1 && rom.State == 2) && update < limit)
                        Step(1, Vector2.Zero, rom[0xcba1] == 1 ? 0 : update % 3 == 0 ? 1 : 0);
                    FailIf(update >= limit, "$e3 prompt never reached option input.");
                    if (choice == 1) Step(1, Vector2.Zero, 2);
                    Step(1, Vector2.Zero);
                    Step(1, Vector2.Zero, 1);
                    Step(3, Vector2.Zero); // Native text remains active until its final update.
                    if (choice == 0)
                    {
                        Step(1, Vector2.Zero); // First script visit initializes wait 30.
                        FailIf(bird.Script.Counter != 30, "$e3 accepted choice did not initialize wait $1e.");
                        Step(29, Vector2.Zero);
                        FailIf(_dialogue.IsOpen || bird.Script.Counter != 1,
                            "$e3 tutorial opened before the source wait reached zero.");
                        Step(1, Vector2.Zero);
                        FailIf(!_dialogue.IsOpen || bird.TextId != 0x320a + bird.Record.SubId,
                            "$e3 accepted choice did not reach its source 30-update tutorial.");
                        FinishText();
                    }
                    Step(5, Vector2.Zero);
                    FailIf(bird.Talking || bird.TalkingSignal || bird.Script.ObjectsDisabled || bird.TextId != 0x3200 + bird.Record.SubId,
                        "$e3 completion did not restore idle/text/input before repeated conversation.");
                }
            }
        }
        GD.Print("Validated all ten clean-US Know-It-All Bird scripts through collision-reachable A-button approach, No/repeated Yes, native choices, 30-update tutorial, paging, text closure, object masks, hopping and ordered RNG in split/batched gameplay.");
    }
}
