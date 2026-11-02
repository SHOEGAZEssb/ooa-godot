
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedMooshBody(FrontendValidationRoot root, bool batched)
    {
        var rooms = root._gameplaySession!.Rooms;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var memory = rooms.RuntimeState; var save = rooms.SaveData; var game = save.Game;
        var player = root._scene.Player; var room = rooms.CurrentRoom; var inventory = player.Inventory;
        var app = new ApplicationValidationFixture(root);
        Vector2 position = (from y in Enumerable.Range(1, room.HeightInTiles - 3)
            from x in Enumerable.Range(1, room.WidthInTiles - 2)
            let p = new Vector2(x * 16 + 8, y * 16 + 8)
            where Enumerable.Range(-12, 49).All(d => Enumerable.Range(-6, 13).All(dx =>
                !room.IsSolid(p + new Vector2(dx, d)) && room.GetTerrainInfo(p + new Vector2(dx, d)).Hazard == HazardType.None &&
                room.GetTerrainInfo(p + new Vector2(dx, d)).Type == TerrainType.Normal))
            select p).First();
        int oldAnimal = inventory.AnimalCompanion; byte oldFlags = save.ReadField("wMooshState");
        try
        {
            entities.Clear(); player.ClearNativeItemParents();
            for (int i = 0; i < 64; i++) memory.SetWramByte(0xd100 + i, 0);
            memory.SetField("wLinkObjectIndex", 0xd0); memory.SetField("wDisabledObjects", 0);
            memory.SetField("wMenuDisabled", 0); memory.SetField("wDisableScreenTransitions", 0);
            memory.SetField("wWarpsDisabled", 0); memory.SetField("wDisableWarpTiles", 0);
            CompanionRuntimeState.ForgetRemembered(memory);
            save.WriteField("wMooshState", 0x80); inventory.AssignAnimalCompanion(0x0d);
            inventory.EquipA(0); inventory.EquipB(0);
            player.WarpTo(position + new Vector2(0, 24)); player.Face(Vector2I.Up); player.ResetEnemyInvincibility();
            entities.Spawn(new MooshCompanionSpawn(position, 2, rooms.ActiveGroup, room.Id));
            var native = new BraceletRom(room, player.Position, 0, -1, game: game); native.LoadSave(save);
            native[0xcc2d] = (byte)rooms.ActiveGroup; native.InitializeWalkingAnimation();
            for (int i = 0; i < 64; i++) native[0xd100 + i] = memory.ReadWramByte(0xd100 + i);
            var sounds = root._sound.AttachPlayRequestAudit(); int previousKeys = 0, updates = 0;
            void Write(int address, byte value) { memory.SetWramByte(address, value); native[address] = value; }
            void Step(int count = 1, int keys = 0) => app.Step(count,
                keys == 0x40 ? Vector2.Up : keys == 0x80 ? Vector2.Down : Vector2.Zero,
                MenuRomActions(keys), MenuRomActions(keys & ~previousKeys), batched: batched, afterUpdate: () =>
                {
                    native.UpdateLinkItems(entities.FrameCounter, keys); previousKeys = keys; updates++;
                    string context = $"{game}/Moosh/batch{batched}/update{updates}";
                    foreach (int address in new[] { 0xd100, 0xd103, 0xd104, 0xd105, 0xd106, 0xd107, 0xd108, 0xd109,
                        0xd110, 0xd10e, 0xd10f, 0xd114, 0xd115, 0xd11a, 0xd120, 0xd121, 0xd130, 0xd139, 0xd13a, 0xd13b })
                        FailIf(memory.ReadWramByte(address) != native[address],
                            $"{context}: native byte${address:x4} runtime=${memory.ReadWramByte(address):x2}/native=${native[address]:x2}, state${native[0xd104]:x2}:${native[0xd105]:x2}.");
                    FailIf(CompanionRuntimeState.Read(memory).Position != new Vector2(native.Word(0xd10c) / 256f, native.Word(0xd10a) / 256f),
                        context + ": body XY/fractions differ.");
                    FailIf(player.CompanionRideActive != (native[0xcc2c] == 0xd1) ||
                        player.Position != new Vector2(native[0xd00d], native[0xd00b]), context + ": Link ride/position differs.");
                    int attacks = Enumerable.Range(0xd6, 10).Count(page => native[page << 8] != 0 && native[(page << 8) + 1] == 0x28);
                    FailIf(entities.Entities<MooshStompAttackRoomEntity>().Count != attacks, context + ": native stomp allocation/lifetime differs.");
                    FailIf(!sounds.Requests.SequenceEqual(native.Sounds),
                        context + $": cue order runtime=[{string.Join(',', sounds.Requests)}]/native=[{string.Join(',', native.Sounds)}].");
                });
            Step(3);
            var body = entities.Entities<MooshCompanionRoomEntity>().Single();
            for (int i = 0; i < 80 && (!player.CompanionRideActive || player.CompanionJumpActive); i++) Step(1, 0x40);
            FailIf(!player.CompanionRideActive || player.CompanionJumpActive, $"{game}: Moosh normal mounting did not finish.");
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Step(1, 1); Step(1, 1);
                if (repeat == 0)
                {
                    Write(0xd139, 2); Write(0xd13a, 15);
                    // Freeze admission must preserve the source phase and counters.
                    memory.SetField("wDisabledObjects", 0x20); native[0xcc8a] = 0x20; Step(3, 1);
                    memory.SetField("wDisabledObjects", 0); native[0xcc8a] = 0;
                }
                else
                {
                    Write(0xd105, 2); Write(0xd13b, 0xff);
                    Step(2, 1); // Native byte overflow returns to $00/$01; it does not force an early release.
                }
                Step(90, 1); Step(65);
                FailIf(body.Phase != MooshCompanionPhase.Riding, $"{game}: Moosh did not recover from charged stomp.");
            }
            // A raw native substate/counter write must select the next handler immediately.
            Write(0xd104, 8); Write(0xd105, 5); Write(0xd106, 2);
            Step(3);
            foreach (byte state in new byte[] { 2, 9, 0x0b }) { Write(0xd104, state); Step(2); }
            Write(0xd104, 5); Write(0xd105, 0);
            Step(1, 2); Step(70); Step(18, 0x80); Step(4);
            FailIf(memory.ReadWramByte(0xd104) != 1, $"{game}: Moosh dismount did not return to ordinary waiting.");
        }
        finally
        {
            entities.Clear(); CompanionRuntimeState.Clear(memory, 0x0d); CompanionRuntimeState.ForgetRemembered(memory);
            memory.SetField("wLinkObjectIndex", 0xd0); memory.SetField("wDisabledObjects", 0);
            memory.SetField("wMenuDisabled", 0); memory.SetField("wDisableScreenTransitions", 0);
            memory.SetField("wWarpsDisabled", 0); memory.SetField("wDisableWarpTiles", 0);
            save.WriteField("wMooshState", oldFlags); save.WriteField("wAnimalCompanion", (byte)oldAnimal);
            typeof(InventoryState).GetProperty(nameof(InventoryState.AnimalCompanion))!.SetValue(inventory, oldAnimal);
            player.WarpTo(position + new Vector2(0, 24));
        }
    }
}
