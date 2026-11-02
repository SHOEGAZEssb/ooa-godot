
namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateMooshScriptedCompanionRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool fractional in new[] { false, true })
        foreach (bool retry in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.WriteWramByte(0xc6bf, 2); _saveData.SetRoomFlag(1, 0x79, 0x40);
            _saveData.WriteWramByte(0xc648, 0); _saveData.SetGlobalFlag(0x24, false);
            _inventory.LoseTreasure(0x52);
            _runtimeState.SetWramByte(0xd10a, fractional ? (byte)0x40 : (byte)0);
            _runtimeState.SetWramByte(0xd10c, fractional ? (byte)0xc0 : (byte)0);
            LoadValidationRoom(0, 0x6c);
            var owner = _roomEvents.Get<MooshRescueEvent>();
            var lanes = SomariaPrivate<CutsceneCommandLaneScheduler>(owner, "_lanes");
            Vector2 link = (from y in Enumerable.Range(1, 6)
                            from x in Enumerable.Range(1, 8)
                            let point = new Vector2(x * 16 + 8, y * 16 + 8)
                            where !_collision.Collides(point) && point.DistanceSquaredTo(new(88, 40)) > 1024
                            select point).First();
            _player.WarpTo(link, recordSafe: false);
            FailIf(_collision.Collides(_player.Position), "Moosh D1 fixture must use original room $0:$6c floor.");
            StepGameplayUpdates(1, Vector2.Zero, batched: batched); // Actual $67:$00 birth follows the earlier special-object phase.
            foreach (var ghini in _entities.EntityAdapters<MooshRescueGhiniRoomEntity>().ToArray()) _entities.RetireEntity(ghini);
            lanes.Clear(); lanes.StartLane("Moosh", owner.Database.Companion);
            var body = _entities.EntityAdapters<ScriptedCompanionRoomEntity>().Single();
            var npc = body.Npc;
            Vector2 position = fractional ? new(88.75f, 40.25f) : new(88, 40);
            // Declared post-$67:$00 birth, with $71/Ghini scripts held at the
            // initial signal gate. Original bank5 Moosh dispatch executes normally.
            var native = new CompanionRom(0x0d, position, 2, _currentRoom);
            native.InitializeRemembered(_player.Position, CompanionRuntimeState.ReadLastAnimalMountPosition(_saveData), false);
            for (int address = 0xc600; address < 0xcb00; address++) native[address] = _saveData.ReadWramByte(address);
            // CompanionRom normally stages an initialized rider. This fixture
            // begins at a fresh spawner birth, whose remaining union is zero.
            for (int offset = 0; offset < 0x40; offset++) native[0xd100 + offset] = 0;
            native[0xd100] = 1; native[0xd101] = 0x0d;
            native.Word(0xd10a, (int)(position.Y * 256)); native.Word(0xd10c, (int)(position.X * 256));
            int visits = 0;
            void Step(int count = 1)
            {
                StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
                {
                    native.Update(0xff); visits++;
                    foreach (int address in new[] { 0xd100, 0xd101, 0xd103, 0xd104, 0xd105, 0xd108, 0xd109,
                        0xd10a, 0xd10b, 0xd10c, 0xd10d, 0xd10e, 0xd10f, 0xd114, 0xd115,
                        0xd11a, 0xd120, 0xd121, 0xd124, 0xd130, 0xd13c, 0xd13d, 0xd13f })
                        FailIf(_runtimeState.ReadWramByte(address) != native[address],
                            $"Moosh rescue D1 update{visits}, batch={batched}, fraction={fractional}, retry={retry}: ${address:x4} native=${native[address]:x2}, runtime=${_runtimeState.ReadWramByte(address):x2}.");
                    FailIf(_runtimeState.ReadField("wDisabledObjects") != native[0xcc8a] ||
                        _runtimeState.ReadField("wMenuDisabled") != native[0xcc02],
                        "Moosh native actor control writes must retain source $01/$01 and their actual eligibility phase.");
                });
            }
            Step(2); Step(8);
            _dialogue.ShowGameplayMessage("Moosh companion text freeze", 100); native[0xcba0] = 1;
            Step(5); _dialogue.Close(); native[0xcba0] = 0;
            var palette = _entities.PaletteFadeActiveSource;
            try { _entities.PaletteFadeActiveSource = () => true; native[0xc4ab] = 1; Step(4); }
            finally { _entities.PaletteFadeActiveSource = palette; native[0xc4ab] = 0; }
            _runtimeState.SetField("wDisabledObjects", 0x20); native[0xcc8a] = 0x20;
            Step(3); _runtimeState.SetField("wDisabledObjects", 0); native[0xcc8a] = 0;
            owner.WriteMemory("MooshVar03", 2); native[0xd103] = 2;
            Step();
            FailIf(!npc.ScriptButtonSensitive || native[0xd103] != 1 || _entities.CanCaptureDebugState,
                "Moosh A2 must add button sensitivity and return to A1 on its next D1 visit.");
            CompanionRuntimeState.WriteScriptButton(_runtimeState, 1); native[0xd13d] = 1;
            Step(3);
            FailIf(_runtimeState.ReadField("wDisabledObjects") != 1 || _runtimeState.ReadField("wMenuDisabled") != 1,
                "Ages Moosh A1 must write exactly $01/$01 after the staged native A-button publication.");
            owner.WriteMemory("MooshVar03", 3); native[0xd103] = 3;
            _runtimeState.SetWramByte(0xd13f, 3); native[0xd13f] = 3;
            owner.SetDisabledObjects(0x11); native[0xcc8a] = 0x11;
            Step(60);
            _saveData.WriteWramByte(0xc648, 0x20); native[0xc648] = 0x20;
            owner.SetDisabledObjects(0); native[0xcc8a] = 0;
            if (retry)
            {
                _player.BeginCarriedObjectPose(); native[0xcc5a] = 1;
                Step(3);
                FailIf(!_entities.IsSpecialObjectOwner(body) || npc.ScriptButtonSensitive,
                    "Rejected Moosh force-mount must retain the physical scripted body and remove A sensitivity.");
                _player.EndCarriedObjectPose(); native[0xcc5a] = 0;
            }
            Step();
            FailIf(_entities.IsSpecialObjectOwner(body) || _entities.Entities<MooshCompanionRoomEntity>().Count != 1,
                "Moosh force-mount must transfer one physical D1 presentation without another allocation.");
            _entities.RetireEntity(body);
            Step(35);
            var mounted = _entities.Entities<MooshCompanionRoomEntity>().Single();
            FailIf(!_player.CompanionRideActive || mounted.Direction != native[0xd108] ||
                mounted.AnimationIndex != native[0xd130] || !_entities.CanCaptureDebugState,
                "Moosh's mounted direction/animation must continue native state, including the first-meeting downward direction.");
            owner.Cancel(); native[0xcc02] = 0;
            Step(3);
        }
    }
}
