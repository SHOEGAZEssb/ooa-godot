
namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSharedRickyInitialStateRom()
    {
        foreach (OracleGame game in new[] { OracleGame.Ages, OracleGame.Seasons })
        for (int flags = 0; flags < 0x100; flags++)
        {
            var native = new LinkCollisionRom(bankedStack: true, game: game);
            native[0xd100] = 1; native[0xd101] = 0x0b;
            native[0xd105] = game == OracleGame.Ages ? (byte)1 : (byte)0;
            native[0xd10b] = 0x40; native[0xd10d] = 0x50;
            native[game == OracleGame.Ages ? 0xc646 : 0xc643] = (byte)flags;
            native.Call(game == OracleGame.Ages ? 0x6d1e : 0x6c6a, objectPage: 0xd1);
            var actual = RickyScriptedCompanionRoomEntity.SelectInitialState(game, flags);
            FailIf(actual.State != native[0xd104] || actual.Animation != native[0xd130],
                $"{game} rickyState0 flags=${flags:x2}: source state/animation=${native[0xd104]:x2}/${native[0xd130]:x2}, runtime=${actual.State:x2}/${actual.Animation:x2}.");
        }
    }

    private void ValidateRickyScriptedCompanionRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool fractional in new[] { false, true })
        foreach (bool retry in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(0x15); _saveData.SetRoomFlag(0, 0x6a, 0x40);
            _saveData.WriteWramByte(0xc646, 0);
            _runtimeState.SetWramByte(0xd10a, fractional ? (byte)0x40 : (byte)0);
            _runtimeState.SetWramByte(0xd10c, fractional ? (byte)0xc0 : (byte)0);
            LoadValidationRoom(0, 0x6a);
            StepGameplayUpdates(1, Vector2.Zero); // The actual placed $67 birth leaves D1 state0 pending.
            _inventory.EquipA(0); _inventory.EquipB(0);
            _player.WarpTo(new(112, 88), recordSafe: false);
            FailIf(_collision.Collides(_player.Position), "Ricky D1 fixture requires original room $0:$6a floor.");
            var body = _entities.EntityAdapters<ScriptedCompanionRoomEntity>().Single();
            var npc = body.Npc;
            Vector2 position = fractional ? new(80.75f, 64.25f) : new(80, 64);
            // Declared post-$67:$02 birth boundary. Native state0, not a
            // fabricated initialized actor, runs alongside actual Link updates.
            var native = new CompanionRom(0x0b, position, 2, _currentRoom);
            native.InitializeRemembered(_player.Position, CompanionRuntimeState.ReadLastAnimalMountPosition(_saveData), false);
            native[0xc646] = 0; native[0xd124] = 0;
            int visits = 0, observedCounter = -1;
            var probe = new PartMovementScratchProbe(() => observedCounter = _runtimeState.ReadWramByte(0xd120));
            _entities.RegisterEnemySlot(probe, 15); _entities.AddEntity(probe);
            void Step(int count = 1)
            {
                StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
                {
                    native.Update(0xff); visits++;
                    foreach (int address in new[] { 0xd100, 0xd101, 0xd103, 0xd104, 0xd105, 0xd108, 0xd109,
                        0xd10a, 0xd10b, 0xd10c, 0xd10d, 0xd10e, 0xd10f, 0xd114, 0xd115, 0xd124 })
                        FailIf(_runtimeState.ReadWramByte(address) != native[address],
                            $"Ricky scripted D1 update{visits}, batch={batched}, fraction={fractional}: ${address:x4} native=${native[address]:x2}, runtime=${_runtimeState.ReadWramByte(address):x2}.");
                    if (native[0xd104] == 0) return;
                    if (_entities.IsSpecialObjectOwner(body))
                        FailIf(npc.ZIndex != ObjectDrawPriority.FromVisible(native[0xd11a]) ||
                            _runtimeState.ReadWramByte(0xd11a) != native[0xd11a],
                            $"Ricky scripted D1 update{visits}: source visible=${native[0xd11a]:x2}, runtime=${_runtimeState.ReadWramByte(0xd11a):x2}, priority={npc.ZIndex}.");
                    FailIf(_runtimeState.ReadWramByte(0xd120) != native[0xd120] ||
                        _runtimeState.ReadWramByte(0xd121) != native[0xd121] || observedCounter != native[0xd120],
                        $"Ricky scripted D1 animation must publish before enemies on update{visits}: counter={_runtimeState.ReadWramByte(0xd120)}/{native[0xd120]}, observed={observedCounter}.");
                });
            }
            Step(2);
            bool rejectedCapture = false;
            try { _entities.CaptureDebugState(); }
            catch (NotSupportedException error) when (error.Message.Contains("$0b")) { rejectedCapture = true; }
            FailIf(_entities.CanCaptureDebugState || !rejectedCapture,
                "A scripted native companion must not capture a snapshot that would lose its controller on reconstruction.");
            FailIf(!npc.Visible || !_entities.IsSpecialObjectOwner(body) || native[0xd104] != 0x0a,
                "Ricky must bind one D1 presentation through real two-visit initialization.");
            Step(35);
            _dialogue.ShowGameplayMessage("Native companion text freeze", 100); native[0xcba0] = 1;
            Step(8); _dialogue.Close(); native[0xcba0] = 0;
            var palette = _entities.PaletteFadeActiveSource;
            try
            {
                _entities.PaletteFadeActiveSource = () => true; native[0xc4ab] = 1;
                Step(6);
            }
            finally { _entities.PaletteFadeActiveSource = palette; native[0xc4ab] = 0; }
            _runtimeState.SetField("wDisabledObjects", 0x20); native[0xcc8a] = 0x20;
            Step(5); _runtimeState.SetField("wDisabledObjects", 0); native[0xcc8a] = 0;
            Step(65);
            // Declare the script's var03 request; native force-mount runs on
            // the following D1 visit and must retain animation and its air arc.
            _runtimeState.SetWramByte(0xd103, 1); native[0xd103] = 1;
            _runtimeState.SetField("wMenuDisabled", 0x80); native[0xcc02] = 0x80;
            if (retry)
            {
                _player.BeginCarriedObjectPose(); native[0xcc5a] = 1;
                Step(3);
                FailIf(!_entities.IsSpecialObjectOwner(body) || npc.ScriptButtonSensitive ||
                    _runtimeState.ReadWramByte(0xd104) != 0x0a || _runtimeState.ReadField("wMenuDisabled") != 0x80,
                    "A failed native force-mount must remove A sensitivity, retain the scripted body and restore menu $80 on every retry.");
                _player.EndCarriedObjectPose(); native[0xcc5a] = 0;
            }
            byte[] union = Enumerable.Range(0xd100, 0x40).Select(_runtimeState.ReadWramByte).ToArray();
            Step();
            union[4] = 3; // A1 writes state; the presentation change has no native birth.
            FailIf(_entities.IsSpecialObjectOwner(body) ||
                _entities.EntityAdapters<ScriptedCompanionRoomEntity>().Any() ||
                _entities.Entities<RickyCompanionRoomEntity>().Count != 1 ||
                _runtimeState.ReadField("wMenuDisabled") != 0x80 ||
                _entities.CanCaptureDebugState ||
                !union.SequenceEqual(Enumerable.Range(0xd100, 0x40).Select(_runtimeState.ReadWramByte)),
                "Force-mount must transfer the same native slot to its riding presentation and restore menu $80.");
            int transferredPriority = _entities.Entities<RickyCompanionRoomEntity>().Single().ZIndex;
            FailIf(transferredPriority != ObjectDrawPriority.FromVisible(native[0xd11a]),
                $"Ricky presentation transfer priority {transferredPriority}, native visible=${native[0xd11a]:x2}, batch={batched}, fraction={fractional}, retry={retry}.");
            _entities.RetireEntity(body);
            FailIf(!union.SequenceEqual(Enumerable.Range(0xd100, 0x40).Select(_runtimeState.ReadWramByte)),
                "Late retirement of the pre-mount presentation must not clear or mutate its same-ID successor.");
            Step(35);
            FailIf(!_player.CompanionRideActive || _runtimeState.ReadWramByte(0xd100) != 3 || !_entities.CanCaptureDebugState,
                "The native force-mount transfer did not complete through actual Link and companion updates.");
            union = Enumerable.Range(0xd100, 0x40).Select(_runtimeState.ReadWramByte).ToArray();
            _roomEvents.Get<RickyGlovesEvent>().Cancel(); native[0xcc02] = 0;
            FailIf(_runtimeState.ReadField("wMenuDisabled") != 0 ||
                !union.SequenceEqual(Enumerable.Range(0xd100, 0x40).Select(_runtimeState.ReadWramByte)),
                "Cancelling the old controller must release its menu write while preserving the transferred physical companion.");
            Step(4);
        }
    }
}
