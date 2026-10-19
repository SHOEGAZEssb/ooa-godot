using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private ImpaIntroEvent PrepareImpaMovementRoom()
    {
        ReinitializeGameplayForValidation(); _saveData.SetRoomFlag(0, 0x6a, 0x40, false); LoadValidationRoom(0, 0x6a);
        var owner = _roomEvents.Get<ImpaIntroEvent>(); var record = owner.Actor!.BaseRecord;
        owner.Cancel(); _saveData.SetRoomFlag(0, 0x59, 0xc0, false); LoadValidationRoom(0, 0x59);
        owner.Cancel(); _entities.Clear();
        owner.Actor = _entities.Spawn<NpcCharacter>(new CutsceneNpcSpawn(record with { Room = 0x59 }, "ImpaPublication"));
        F1Set(owner, "_stage", ImpaIntroEventStage.Following); F1Set(owner, "_followRoom", _currentRoom);
        owner.StartStoneRoom(); _player.WarpTo(new(40, 112)); _player.BeginCutsceneControl(owner: owner);
        _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 1); _runtimeState.SetWramByte(WramAddress.wWarpsDisabled, 1);
        return owner;
    }

    private void ValidateImpaApproachPublicationRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (Vector2 initial in new[] { new Vector2(80.25f, 80.5f), new Vector2(31.75f, 64.25f) })
        {
            var owner = PrepareImpaMovementRoom(); var actor = owner.Actor!;
            actor.SetStatePosition(OracleObjectMath.ToPixelPosition(initial)); F1Set(owner, "_precisePosition", initial);
            F1Set(owner, "_stoneStage", StoneStage.FirstTextPost); F1Set(owner, "_counter", 1);
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 40, 112) { CompanionDispatchEnabled = true };
            native.InitializeLinkGameplay(); native[0xcc8a] = 1; native[0xcc6e] = 1;
            int at = 0xd040 + _entities.InteractionSlot(actor) * 256;
            native[at] = 1; native[at + 1] = 0x31; native[at + 4] = 1; native[at + 5] = 4; native[at + 6] = 1;
            native[at + 0x10] = 0x78; native[at + 0x20] = 0x7f;
            native.Word(at + 10, (int)(initial.Y * 256)); native.Word(at + 12, (int)(initial.X * 256));
            var angle = new ObjectMotionRom(); angle.Word(0xd14a, (int)(initial.Y * 256)); angle.Word(0xd14c, (int)(initial.X * 256));
            angle.Call(ObjectMotionRom.RelativeAngle, targetY: 0x38, targetX: 0x38, slot: 0x40); native[at + 9] = (byte)angle.Result;
            byte[] expected = new byte[4]; int update = 0, movements = 0;
            void Seed() { for (int i = 0; i < 4; i++) { byte value = (byte)(0xa5 + i); native[0xcec0 + i] = value; _runtimeState.SetWramByte(0xcec0 + i, value); } }
            void Step(int count, bool text = false) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                bool moving = native[at + 5] == 5 && !text;
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter, afterInteractions: () =>
                { for (int i = 0; i < 4; i++) expected[i] = native[0xcec0 + i]; }); update++;
                if (moving) movements++;
                FailIf(SomariaPrivate<Vector2>(owner, "_precisePosition") != new Vector2(native.Word(at + 12) / 256f, native.Word(at + 10) / 256f) ||
                    actor.Position != new Vector2(native[at + 13], native[at + 11]) ||
                    Enumerable.Range(0, 4).Any(i => _runtimeState.ReadWramByte(0xcec0 + i) != expected[i]) ||
                    (owner.CurrentStoneStage == StoneStage.ApproachStone) != (native[at + 5] == 5),
                    $"Impa$31:$00 approach visit{update}: source movement/fractional snap/publication differs.");
                Seed();
            });
            Seed(); Step(2); _dialogue.ShowMessage("Impa approach hold.", _player.Position.Y); native[0xcba0] = 1;
            Step(3, true); _dialogue.Close(); native[0xcba0] = 0;
            for (int limit = 0; native[at + 5] == 5 && limit < 40; limit++) Step(1);
            Step(2); FailIf(movements < 4 || owner.CurrentStoneStage != StoneStage.AtStoneWait,
                "Impa approach must reach native high-byte target while retaining source low bytes.");
            owner.Cancel(); _entities.Clear();
        }
    }

    private void ValidateImpaScriptPublicationRom()
    {
        // Original scriptHelper.s commands and incoming speed/angle bytes.
        foreach (var (stream, line, speed, angle) in new[] { (0, 955, 0x14, 0x10),
            (1, 968, 0x14, 0x18), (1, 972, 0x14, 0x18), (2, 798, 0x0a, 0x10),
            (2, 807, 0x28, 8), (2, 812, 0x28, 0), (2, 821, 0x14, 0) })
        foreach (bool batched in new[] { false, true })
        {
            var owner = PrepareImpaMovementRoom();
            var commands = stream == 0 ? owner.Database.EncounterCommands : stream == 1 ? owner.Database.StonePrePushCommands : owner.Database.StonePostPushCommands;
            var original = commands.Single(command => command.Source.SourceLine == line);
            var command = original with { Source = original.Source with { CommandIndex = 0 } };
            int frames = original is CutsceneMoveCommand move ? move.Counter : ((CutsceneApplySpeedCommand)original).Counter;
            var runner = SomariaPrivate<CutsceneCommandRunner>(owner, stream == 0 ? "_encounterRunner" : stream == 1 ? "_stonePrePushRunner" : "_stonePostPushRunner");
            F1Set(owner, "_stage", stream == 0 ? ImpaIntroEventStage.WaitingForScript : ImpaIntroEventStage.Following);
            F1Set(owner, "_stoneStage", stream == 1 ? StoneStage.PrePushScript : StoneStage.PostPushScript);
            owner.Actor!.SetStatePosition(new(8, 8)); F1Set(owner, "_precisePosition", new Vector2(8.25f, 8.5f));
            runner.Start([command, new CutsceneWaitCommand(command.Source with { CommandIndex = 1, Opcode = "wait" }, 0xff),
                new CutsceneEndCommand(command.Source with { CommandIndex = 2, Opcode = "scriptend" })]);
            runner.SetInitialMotionRegisters("Impa", speed, angle);
            var trace = new ScriptMovementPublicationTrace(_runtimeState, source => source == command.Source
                ? (speed, angle, SomariaPrivate<Vector2>(owner, "_precisePosition")) : null);
            _roomEvents.CommandTraceSink = trace;
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            int before = trace.MotionVisits; _dialogue.ShowMessage("Impa script hold.", _player.Position.Y);
            StepGameplayUpdates(3, Vector2.Zero, batched: batched); _dialogue.Close();
            FailIf(trace.MotionVisits != before, "Impa native script text gate must retain its motion counter.");
            StepGameplayUpdates(frames + 2, Vector2.Zero, batched: batched);
            FailIf(trace.Visited.Count != 1 || trace.MotionVisits != frames - 1,
                $"Impa line{line}: every positive source counter visit must publish native motion.");
            _roomEvents.CommandTraceSink = null; owner.Cancel(); _entities.Clear();
        }
    }

    private void ValidateFakeOctorokPublicationRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            var owner = PrepareImpaMovementRoom(); owner.LeaveStoneRoom();
            F1Set(owner, "_stage", ImpaIntroEventStage.WaitingForScript);
            var states = SomariaPrivate<List<FakeOctorokState>>(owner, "_fakeOctoroks");
            foreach (var record in owner.Database.Octoroks)
            {
                var actor = _entities.Spawn<NpcCharacter>(new CutsceneNpcSpawn(record.ToNpcRecord(0, 0x59), $"FakePublication{record.Index}"));
                states.Add(new(record, actor) { Stage = FakeOctorokStage.Moving,
                    Position = OracleObjectPosition.FromPixels(actor.Position + new Vector2(0.25f, 0.5f)) });
            }
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 40, 112) { CompanionDispatchEnabled = true };
            native.InitializeLinkGameplay(); native[0xcc8a] = 1; native[0xcc6e] = 1;
            int[] addresses = states.Select(state => 0xd040 + _entities.InteractionSlot(state.Actor) * 256).ToArray();
            for (int i = 0; i < states.Count; i++)
            {
                int at = addresses[i]; native[at] = 1; native[at + 1] = 0x32; native[at + 3] = (byte)i;
                native[at + 11] = (byte)states[i].Record.Y; native[at + 13] = (byte)states[i].Record.X;
            }
            native.AdvanceInteractions();
            for (int i = 0; i < states.Count; i++)
            {
                int at = addresses[i]; native[at + 5] = 3; native[at + 9] = i == 1 ? (byte)0x18 : (byte)0; native[at + 0x10] = 0x78;
                native.Word(at + 10, states[i].Position.YFixed); native.Word(at + 12, states[i].Position.XFixed);
            }
            int update = 0, movements = 0; byte[] expected = new byte[4];
            void Seed() { for (int i = 0; i < 4; i++) { byte value = (byte)(0xa5 + i); native[0xcec0 + i] = value; _runtimeState.SetWramByte(0xcec0 + i, value); } }
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter); update++;
                for (int i = 0; i < 4; i++) expected[i] = native[0xcec0 + i];
                for (int i = 0; i < states.Count; i++)
                {
                    int at = addresses[i]; bool live = native[at] != 0;
                    FailIf(states[i].Actor.Active != live || live &&
                        states[i].Position.PrecisePosition != new Vector2(native.Word(at + 12) / 256f, native.Word(at + 10) / 256f),
                        $"Fake Octorok $32:$00/v${i:x2} visit{update}: source byte wrapping/screen retirement differs.");
                    if (live && !_dialogue.IsOpen) movements++;
                }
                FailIf(Enumerable.Range(0, 4).Any(i => _runtimeState.ReadWramByte(0xcec0 + i) != expected[i]),
                    $"Fake Octorok visit{update}: source ordered movement publication differs.");
                Seed();
            });
            Seed(); Step(4); _dialogue.ShowMessage("Fake Octorok hold.", _player.Position.Y); native[0xcba0] = 1;
            Step(3); _dialogue.Close(); native[0xcba0] = 0;
            Step(30); Step(2);
            FailIf(states.Any(state => state.Actor.Active) || movements < 35, "All three native fake Octoroks must cross their wrapped screen edges and retire.");
            owner.Cancel(); _entities.Clear();
        }
    }

    private void ValidateImpaLinkPositioningRom()
    {
        foreach (int speed in new[] { 0x14, 0x1e, 0x28 })
        foreach (Vector2 initial in new[] { new Vector2(80.25f, 88.5f), new Vector2(80.25f, 64.5f), new Vector2(80.25f, 72.5f) })
        foreach (bool batched in new[] { false, true })
        {
            var owner = PrepareImpaMovementRoom();
            // Declared linkCutscene2 state1/substate0 after Impa published02;
            // her waiting script is outside this bounded Link comparison.
            F1Set(owner, "_stoneStage", StoneStage.LinkSelect); F1Set(owner, "_stoneSignal", 2); F1Set(owner, "_counter", 0);
            _player.WarpTo(initial); F1Set(_player, "_topDownMovementSpeedRaw", speed);
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 80, (int)initial.Y) { CompanionDispatchEnabled = true };
            native.InitializeLinkGameplay(); native[0xcc8a] = 1; native[0xcc6e] = 1;
            native[0xd001] = 8; native[0xd002] = 2; native[0xd004] = 1; native[0xd005] = 0; native[0xd010] = (byte)speed;
            native[0xcfd0] = 2; native.Word(0xd00a, (int)(initial.Y * 256)); native.Word(0xd00c, (int)(initial.X * 256));
            byte[] observed = new byte[4], expected = new byte[4]; int probes = 0, updates = 0, motions = 0;
            var probe = new PartMovementScratchProbe(() =>
            { probes++; for (int i = 0; i < 4; i++) observed[i] = _runtimeState.ReadWramByte(0xcec0 + i); });
            _entities.RegisterEnemySlot(probe, 15); _entities.AddEntity(probe);
            void Seed() { for (int i = 0; i < 4; i++) { byte value = (byte)(0xa5 + i); native[0xcec0 + i] = value; _runtimeState.SetWramByte(0xcec0 + i, value); } }
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                int beforeX = native.Word(0xd00c), beforeY = native.Word(0xd00a);
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter, afterLink: () =>
                { for (int i = 0; i < 4; i++) expected[i] = native[0xcec0 + i]; }); updates++;
                if (beforeX != native.Word(0xd00c) || beforeY != native.Word(0xd00a)) motions++;
                StoneStage stage = native[0xd005] switch
                {
                    0 => StoneStage.LinkSelect, 1 or 4 => StoneStage.LinkFirstAxis, 2 or 5 => StoneStage.LinkAxisWait,
                    3 or 6 => StoneStage.LinkSecondAxis, 7 => StoneStage.LinkTargetWait, 8 => StoneStage.PrePushScript,
                    _ => throw new System.InvalidOperationException("Unexpected native linkCutscene2 substate.")
                };
                FailIf(_player.PrecisePosition != new Vector2(native.Word(0xd00c) / 256f, native.Word(0xd00a) / 256f) ||
                    owner.CurrentStoneStage != stage || owner.Counter != native[0xd006] ||
                    SomariaPrivate<int>(owner, "_stoneSignal") != native[0xcfd0] || probes != 1 || !observed.SequenceEqual(expected),
                    $"Impa Link positioning speed${speed:x2}/start={initial}/visit{updates}: source axis/counter/signal/movement phase/publication differs; stage={owner.CurrentStoneStage}/{stage}, counter={owner.Counter}/{native[0xd006]}, XY={_player.PrecisePosition}/{native.Word(0xd00c):x4},{native.Word(0xd00a):x4}.");
                probes = 0; Seed();
            });
            Seed(); Step(4); _dialogue.ShowMessage("Native Link cutscene continues during text.", _player.Position.Y); native[0xcba0] = 1;
            Step(3); _dialogue.Close(); native[0xcba0] = 0;
            for (int limit = 0; native[0xd005] != 8 && limit < 300; limit += 2) Step(2);
            Step(2);
            FailIf(motions < 20 || native[0xd005] != 8 || _player.Position != new Vector2(56, 72),
                "Link's retained native speed must reach both source high-byte targets and publish03 at the60-update wait boundary.");
            owner.Cancel(); _entities.Clear();
        }
    }

    private void ValidateImpaStonePushPublicationRom()
    {
        foreach (bool right in new[] { false, true })
        foreach (bool batched in new[] { false, true })
        {
            var owner = PrepareImpaMovementRoom(); var stone = owner.StoneActor!; var impa = owner.Actor!;
            F1Set(owner, "_stoneStage", StoneStage.PushStarted); F1Set(owner, "_pushedRight", right);
            F1Set(owner, "_stoneMoveCounter", 0x40); F1Set(owner, "_stoneSignal", 6);
            F1Set(owner, "_stonePrecisePosition", new Vector2(56, 38)); stone.SetStatePosition(new(56, 38));
            F1Set(owner, "_precisePosition", new Vector2(24, 56)); impa.SetStatePosition(new(24, 56));
            Vector2 initial = new(right ? 40.25f : 72.25f, 38.5f);
            _player.WarpTo(initial); _player.BeginCutsceneControl(owner: owner); _player.Face(right ? Vector2I.Right : Vector2I.Left);
            int angle = right ? 8 : 0x18;
            F1Set(_player, "_topDownMovementSpeedRaw", 0x14); F1Set(_player, "_topDownMovementAngle", angle);
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, right ? 1 : 3, (int)initial.X, 38) { CompanionDispatchEnabled = true };
            native.InitializeLinkGameplay(); native[0xcc8a] = 1; native[0xcc6e] = 1; native[0xcfd0] = 6;
            native[0xd001] = 8; native[0xd002] = 6; native[0xd004] = 0; native[0xd005] = 0; native[0xd009] = (byte)angle;
            native.Word(0xd00a, (int)(initial.Y * 256)); native.Word(0xd00c, (int)(initial.X * 256));
            int stoneAt = 0xd040 + _entities.InteractionSlot(stone) * 256;
            int impaAt = 0xd040 + _entities.InteractionSlot(impa) * 256;
            native[stoneAt] = 1; native[stoneAt + 1] = 0x34; native[stoneAt + 4] = 1; native[stoneAt + 5] = 1;
            native[stoneAt + 6] = 0x40; native[stoneAt + 9] = (byte)angle; native[stoneAt + 0x10] = 0x0a;
            native[stoneAt + 0x26] = 3; native[stoneAt + 0x27] = 10;
            native.Word(stoneAt + 10, 38 * 256); native.Word(stoneAt + 12, 56 * 256);
            native[impaAt] = 1; native[impaAt + 1] = 0x31; native[impaAt + 4] = 1; native[impaAt + 5] = 0x0b;
            native[impaAt + 0x20] = 0x7f; native[impaAt + 0x26] = native[impaAt + 0x27] = 6;
            native.Word(impaAt + 10, 56 * 256); native.Word(impaAt + 12, 24 * 256);
            native.Word(impaAt + 0x18, 0x51f1); // bank$0c impaScript_waitForRockToBeMoved; native B installs the response wrapper.
            byte[] observed = new byte[4], linkExpected = new byte[4], finalExpected = new byte[4];
            int probes = 0, updates = 0, stoneMotions = 0;
            var probe = new PartMovementScratchProbe(() =>
            { probes++; for (int i = 0; i < 4; i++) observed[i] = _runtimeState.ReadWramByte(0xcec0 + i); });
            _entities.RegisterEnemySlot(probe, 15); _entities.AddEntity(probe);
            void Seed() { for (int i = 0; i < 4; i++) { byte value = (byte)(0xa5 + i); native[0xcec0 + i] = value; _runtimeState.SetWramByte(0xcec0 + i, value); } }
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                int before = native.Word(stoneAt + 12);
                native.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter,
                    afterLink: () => { for (int i = 0; i < 4; i++) linkExpected[i] = native[0xcec0 + i]; },
                    afterInteractions: () => { for (int i = 0; i < 4; i++) finalExpected[i] = native[0xcec0 + i]; }); updates++;
                if (native[stoneAt + 6] != 0 && native.Word(stoneAt + 12) != before) stoneMotions++;
                FailIf(probes != 1 || !observed.SequenceEqual(linkExpected) ||
                    _player.PrecisePosition != new Vector2(native.Word(0xd00c) / 256f, native.Word(0xd00a) / 256f) ||
                    SomariaPrivate<Vector2>(owner, "_stonePrecisePosition") != new Vector2(native.Word(stoneAt + 12) / 256f, native.Word(stoneAt + 10) / 256f) ||
                    SomariaPrivate<Vector2>(owner, "_precisePosition") != new Vector2(native.Word(impaAt + 12) / 256f, native.Word(impaAt + 10) / 256f) ||
                    SomariaPrivate<int>(owner, "_stoneMoveCounter") != native[stoneAt + 6] ||
                    (_saveData.GetRoomFlags(0, 0x59) & 0xc0) != (native[0xc759] & 0xc0) ||
                    Enumerable.Range(0, 4).Any(i => _runtimeState.ReadWramByte(0xcec0 + i) != finalExpected[i]),
                    $"Impa/Link/stone push right={right} visit{updates}: native early Link movement, clamp, ordered script/stone publication or completion differs.");
                probes = 0; Seed();
            });
            Seed(); Step(4); _dialogue.ShowMessage("Link keeps pushing while ordinary interactions hold.", _player.Position.Y); native[0xcba0] = 1;
            Step(3); _dialogue.Close(); native[0xcba0] = 0;
            for (int limit = 0; native[stoneAt + 6] != 0 && limit < 80; limit += 2) Step(2);
            Step(3);
            FailIf(stoneMotions != 63 || native[stoneAt + 5] != 2,
                "Stone counter$40 must execute63 quarter-pixel steps then preserve its final fraction while setting the direction's room flag.");
            owner.Cancel(); _entities.Clear();
        }
    }
}
