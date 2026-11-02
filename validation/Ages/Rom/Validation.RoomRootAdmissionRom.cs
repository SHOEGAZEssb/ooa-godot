
namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateRoomRootAdmissionRom()
    {
        ReinitializeGameplayForValidation();
        _saveData.SetGlobalFlag(GlobalFlag.IntroDone);
        _saveData.SetMapleKillCounter(0);
        CompanionRuntimeState.ForgetRemembered(_runtimeState);
        foreach (bool staticCart in new[] { false, true })
        foreach (int occupied in staticCart ? new[] { 0, 11, 14 } : new[] { 0 })
        foreach (int flags in staticCart ? new[] { 0, 1, 2, 3, 4, 8, 0x80, 0xff } : Enumerable.Range(0, 256))
        {
            _entities.Clear();
            for (int address = 0xd100; address < 0xd140; address++) _runtimeState.SetWramByte(address, 0);
            _runtimeState.SetField("wcc05", (byte)flags);
            _runtimeState.SetField("wcc85", 0);
            var room = _rooms.Load(staticCart ? 4 : 0, staticCart ? 0x78 : 1);
            ResetMinecartsForValidation(_runtimeState, staticCart
                ? [new MinecartStaticRecord(0, 0x78, 0x88, 0x78, "data/ages/staticDungeonObjects.s:dungeon4StaticObjects retained-buffer fixture")]
                : []);
            _runtimeState.SetField("wTmpcfc0", 0x5a);
            for (int address = 0xcec0; address < 0xcee0; address++)
                _runtimeState.SetWramByte(address, (byte)(0x81 + (address & 0x1f)));
            var seed = _random.CaptureState();
            var native = new SomariaRom(_saveData, seed, room, 2, 80, 80);
            native[0xcc2e] = (byte)Math.Max(0, _rooms.GetDungeonIndex(room.Group, room.Id));
            native[0xcc05] = (byte)flags; native[0xcc85] = 0; native[0xcfc0] = 0x5a;
            native[0xcc4a] = 0xf0;
            for (int address = 0xcec0; address < 0xcee0; address++) native[address] = _runtimeState.ReadWramByte(address);
            for (int index = 0; index < 256; index++) native.SetFloorPermutation(index, seed.PlacementBuffer[index]);
            for (int address = 0xcd80; address < 0xcdc0; address++) native[address] = _runtimeState.ReadWramByte(address);
            void ReserveInteractions(int group, OracleRoomData destination)
            {
                for (int slot = 2; slot < occupied + 2; slot++)
                {
                    if (!_entities.InteractionSlotAvailable) break;
                    var puff = new StationaryDebrisEffect();
                    puff.Initialize(destination.AssetRoot, new(24, 24), SoundId.MusNone);
                    _entities.AddEntity(new InteractionSlotValidationEntity(puff, _ => { }));
                }
            }
            _entities.RoomInitializing += ReserveInteractions;
            try
            {
                // Retained allocations and layout controllers both precede
                // initializeRoom. Only their total occupancy matters here;
                // the separate shutter regression checks tile-handler ordering.
                for (int slot = 2; slot < occupied + 2; slot++) native[0xd040 + slot * 256] = 1;
                native.ApplyRoomTileSubstitutions();
                native.InitializeRoomInteractions();
                _entities.LoadRoom(room.Group, room, EnemyPlacementContext.FromWarpDestination(0xf0));
            }
            finally { _entities.RoomInitializing -= ReserveInteractions; }
            string context = $"initializeRoom flags${flags:x2}/static={staticCart}/occupied${occupied:x2}";
            var parsed = _random.CaptureState();
            int expectedCalls = (flags & 1) != 0 ? 256 : 0;
            FailIf(parsed.Calls - seed.Calls != expectedCalls || native.RandomCalls != expectedCalls ||
                parsed.Rng1 != native[0xff94] || parsed.Rng2 != native[0xff95] ||
                !Enumerable.Range(0, 256).All(index => parsed.PlacementBuffer[index] == native.FloorPermutation(index)),
                context + ": skipped roots must retain the exact placement permutation/RNG; admitted roots rebuild it once.");
            for (int address = 0xcec0; address < 0xcee0; address++)
                FailIf(_runtimeState.ReadWramByte(address) != native[address], context + $": scratch byte${address:x4} differs: port=${_runtimeState.ReadWramByte(address):x2}, native=${native[address]:x2}, modifier=${_runtimeState.ReadField("wRoomStateModifier"):x2}.");
            FailIf(_runtimeState.ReadField("wTmpcfc0") != native[0xcfc0], context + ": skipped parse must preserve $cfc0.");
            if (!staticCart) continue;
            int nativeCartSlot = Enumerable.Range(2, 14).FirstOrDefault(slot =>
                native[0xd040 + slot * 256] != 0 && native[0xd041 + slot * 256] == 0x16, -1);
            var carts = _entities.Entities<MinecartInteractionRoomEntity>();
            FailIf(carts.Count != (nativeCartSlot >= 0 ? 1 : 0) ||
                carts.Count == 1 && _entities.InteractionSlot(carts[0]) != nativeCartSlot,
                context + $": static cart must allocate after every ordinary interaction, including skipped roots/full-pool failure; native=${nativeCartSlot:x2}, port=[{string.Join(",", carts.Select(cart => _entities.InteractionSlot(cart).ToString("x2")))}], actors=[{string.Join(",", _entities.EntityAdapters<IRoomEntity>().Select(actor => actor.GetType().Name))}].");
            for (int address = 0xcd80; address < 0xcdc0; address++)
                FailIf(_runtimeState.ReadWramByte(address) != native[address], context + $": static-buffer byte${address:x4} changed.");
        }
        GD.Print("Validated native initializeRoom for all $100 root masks, retained/rebuilt placement scratch and RNG, and static interaction allocation after ordinary roots under pool pressure.");
    }
}
