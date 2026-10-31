using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSharedAirRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            LoadValidationRoom(0,0x45);
            Check(new ApplicationValidationFixture(this),_player,_rooms,_entities,_sound,_dialogue);
            var root = new FrontendValidationRoot();
            AddChild(root);
            try
            {
                var selector = root.InitializeGameSelect();
                var save = OracleSaveData.CreateStandardGame(OracleGame.Seasons);
                save.SetLinkName("SEAS"); save.SetGlobalFlag(0x21); save.SetGlobalFlag(0x0a);
                save.SetDeathRespawnPoint(0,0xb6,3,2,0x48,0x78); save.SetRoomFlag(0,0xb6,0x50);
                root.SeedFileSlot(0,save);
                selector.RequestPointerSelection(OracleGame.Seasons,true); root.Step(1,false); root.CompleteBootLoading();
                root.Step(1,false); root.Step(1,false,"inventory"); root.Step(32,false);
                root.Step(1,false,"attack"); root.Step(1,false); root.Step(1,false,"attack"); root.Step(32,false);
                var session = root._seasonsOpening ?? throw new InvalidOperationException("Seasons omitted shared air gameplay.");
                Check(new ApplicationValidationFixture(root),session.Link,session.Rooms,session.Entities,root._sound,session.Dialogue);
            }
            finally { root.Free(); }

            void Check(ApplicationValidationFixture app, Player player, RoomSession rooms,
                RoomEntityManager entities, OracleSoundEngine sound, DialogueBox dialogue)
            {
                entities.Clear();
                var memory = rooms.RuntimeState;
                var inventory = player.Inventory;
                bool seasons = memory.Game == OracleGame.Seasons;
                Vector2? approach = null;
                for (int y = 48; y <= 96 && approach is null; y += 16)
                for (int x = 32; x <= 128; x += 16)
                {
                    bool clear = true;
                    for (int step = 0; step <= 16; step++)
                    {
                        Vector2 point = new(x,y + step);
                        clear &= !rooms.CurrentRoom.IsSolid(point) &&
                            player.World.GetActiveTerrain(point).Terrain.Type == TerrainType.Normal;
                    }
                    if (clear) { approach = new(x,y + 16); break; }
                }
                FailIf(approach is null,$"{memory.Game}: Feather fixture needs a dry, reachable takeoff point.");
                player.WarpTo(approach!.Value);
                app.Step(16,Vector2.Up,batched:batched);
                app.Step(1,Vector2.Zero,batched:batched);
                Vector2 takeoff = player.PrecisePosition;
                FailIf(takeoff.DistanceTo(approach.Value) < 8 || rooms.CurrentRoom.IsSolid(takeoff),
                    "The air regression must reach takeoff through native room collision.");
                var audit = sound.AttachPlayRequestAudit();
                foreach (int level in seasons ? new[] { 1,2 } : new[] { 1 })
                foreach (bool primary in new[] { false,true })
                foreach (int repressAt in new[] { 0,-1,16,24,25 })
                {
                    inventory.GiveTreasure(TreasureId.Feather,level);
                    inventory.EquipA(primary ? TreasureId.Feather : TreasureId.None);
                    inventory.EquipB(primary ? TreasureId.None : TreasureId.Feather);
                    player.WarpTo(takeoff);
                    app.Step(1,Vector2.Zero,batched:batched);
                    audit.Clear();
                    string action = primary ? "attack" : "item";
                    var reference = seasons ? new SeasonsFeatherRom(rooms.CurrentRoom,takeoff,level,primary) : null;
                    bool eligible = seasons && level == 2, parent = eligible, gliding = false, airborne = true;
                    int z = 0, speed = -0x1e0, phase = 0, counter = 9, updates = 0;
                    // Independent source arithmetic: featherParent runs before
                    // linkUpdateInAir, which integrates old speed then gravity.
                    while (airborne && updates < 100)
                    {
                        int update = updates + 1;
                        bool held = repressAt == 0 || update < 8 || repressAt > 0 && update >= repressAt;
                        bool pressed = update == 1 || update == repressAt;
                        int count = batched ? 7 : 1;
                        foreach (int boundary in new[] { 7,15,repressAt - 1 })
                            if (updates < boundary) count = Math.Min(count,boundary - updates);
                        app.Step(count,Vector2.Zero,held:held ? [action] : [],pressed:pressed ? [action] : [],batched:batched,
                            afterUpdate:Compare);
                        if (updates == 15)
                        {
                            int retainedZ = player.SwitchHookZFixed, retainedSpeed = player.TopDownAirSpeedZ;
                            dialogue.ShowMessage("Air pause.",player.Position.Y);
                            if (reference is not null) reference[0xcba0] = 1;
                            app.Step(7,Vector2.Zero,held:held ? [action] : [],batched:batched,
                                afterUpdate:() => reference?.Update(held,false,primary));
                            FailIf(player.SwitchHookZFixed != retainedZ || player.TopDownAirSpeedZ != retainedSpeed,
                                $"{memory.Game}: text must freeze the air arc and Cape parent.");
                            FailIf(reference is not null && (reference.ZFixed != retainedZ || reference.SpeedZ != retainedSpeed),
                                "Native text gate must freeze the same Seasons air arc.");
                            dialogue.Close();
                            if (reference is not null) reference[0xcba0] = 0;
                        }

                        void Compare()
                        {
                            reference?.Update(held,pressed && updates == update - 1,primary);
                            if (reference is not null)
                            {
                                FailIf(player.SwitchHookZFixed != reference.ZFixed || player.TopDownAirSpeedZ != reference.SpeedZ ||
                                    player.CapeGliding != ((reference[0xcc77] & 0x20) != 0) ||
                                    player.CapeParentActive != (reference[0xd300] != 0 || reference[0xd400] != 0),
                                    $"Seasons ROM air update{updates+1}: Z/Vz={player.SwitchHookZFixed}/{player.TopDownAirSpeedZ}, ROM={reference.ZFixed}/{reference.SpeedZ}.");
                                if (player.TopDownAirborne)
                                {
                                    int bodyPhase = player.AirborneLinkBodyFrame;
                                    bool cape = player.CapeGliding;
                                    int graphic = cape ? (bodyPhase < 2 ? 0xe4 : 0x80) :
                                        bodyPhase < 3 ? 0xd8 + bodyPhase * 4 : 0x80;
                                    FailIf(reference[0xd030] != (cape ? 0x19 : 0x18) || reference[0xd031] != graphic ||
                                        player.LinkAnimationCounter != reference[0xd020],
                                        "Seasons Feather/Cape mode, graphic and animation counter must match executed native code.");
                                    foreach (int address in new[] { 0xd020, 0xd021, 0xd022, 0xd023, 0xd030, 0xd031 })
                                        FailIf(memory.ReadWramByte(address) != reference[address],
                                            $"Seasons Feather/Cape animation ${address:x4}: runtime${memory.ReadWramByte(address):x2}/ROM${reference[address]:x2}.");
                                }
                            }
                            if (!airborne)
                            {
                                FailIf(player.TopDownAirborne || player.CapeParentActive || player.CapeGliding,
                                    "A held Feather button restarted after landing inside a host batch.");
                                return;
                            }
                            int current = updates + 1;
                            if (current > 1)
                            {
                                if (current == repressAt && eligible) parent = true;
                                if (parent && (!held || gliding)) parent = false;
                                if (parent && speed >= 0x100)
                                { speed = -0x80; gliding = true; parent = false; phase = 0; counter = 6; }
                            }
                            z = unchecked((short)(z + speed));
                            if (z >= 0) { z = speed = 0; airborne = parent = gliding = false; }
                            else
                            {
                                speed += gliding ? 0x0a : 0x20;
                                int[] durations = gliding ? [6,40] : [9,9,6];
                                if (--counter == 0)
                                { phase++; counter = phase < durations.Length ? durations[phase] : 0x7f; }
                            }
                            updates++;
                            FailIf(player.TopDownAirborne != airborne || player.SwitchHookZFixed != z ||
                                player.TopDownAirSpeedZ != speed || player.CapeGliding != gliding || player.CapeParentActive != parent ||
                                airborne && (player.AirborneLinkBodyFrame != phase || player.LinkAnimationCounter != counter),
                                $"{memory.Game}: Feather L{level}, A={primary}, re-press={repressAt}, batch={batched}, update={updates}: " +
                                $"Z/Vz={player.SwitchHookZFixed}/{player.TopDownAirSpeedZ} expected={z}/{speed}, " +
                                $"glide/parent={player.CapeGliding}/{player.CapeParentActive} expected={gliding}/{parent}, " +
                                $"phase/counter={player.AirborneLinkBodyFrame}/{player.LinkAnimationCounter} expected={phase}/{counter}.");
                        }
                    }
                    bool shouldGlide = eligible && repressAt != -1;
                    FailIf(airborne || !shouldGlide && updates != 31 || audit.RequestsFor(SoundId.SndJump) != 1 ||
                        audit.RequestsFor(SoundId.SndLand) != 1 || audit.RequestsFor(SoundId.SndThrow) != (shouldGlide ? 1 : 0),
                        $"{memory.Game}: Feather/Cape must land once, clear its parent and emit only the native launch/glide/land cues.");
                    if (reference is not null)
                        FailIf(!audit.Requests.Where(id => id is SoundId.SndJump or SoundId.SndThrow or SoundId.SndLand)
                            .SequenceEqual(reference.Sounds.Where(id => id is SoundId.SndJump or SoundId.SndThrow or SoundId.SndLand)),
                            $"Seasons L{level} A={primary} re-press={repressAt}: cue order runtime=" +
                            string.Join(',',audit.Requests.Select(id => $"${id:x2}")) + " ROM=" +
                            string.Join(',',reference.Sounds.Select(id => $"${id:x2}")));
                    app.Step(2,Vector2.Zero,batched:batched);
                    // Re-entry and room replacement must clear the retained Cape bits.
                    int repeated = 0;
                    app.Step(27,Vector2.Zero,held:[action],pressed:[action],batched:batched,afterUpdate:() =>
                    {
                        reference?.Update(true,repeated++ == 0,primary);
                        FailIf(reference is not null && (player.SwitchHookZFixed != reference.ZFixed ||
                            player.TopDownAirSpeedZ != reference.SpeedZ),"Repeat Feather/Cape arc differs from native code.");
                    });
                    FailIf(!player.TopDownAirborne,"Feather must accept a fresh press after landing.");
                    FailIf(player.CapeGliding != eligible,"Repeat Cape input must reach the source glide threshold before cancellation.");
                    player.WarpTo(takeoff);
                    reference?.PutLinkOnGround();
                    FailIf(player.TopDownAirborne || player.CapeGliding || player.CapeParentActive,
                        "Room/coordinate replacement retained a cancelled Cape arc.");
                    FailIf(reference is not null && (reference.Airborne || reference[0xd300] != 0 || reference[0xd400] != 0),
                        "Native ground reset must cancel the retained Feather parents as well as the arc.");
                }
                if (seasons)
                {
                    inventory.GiveTreasure(TreasureId.Feather,2);
                    inventory.EquipA(TreasureId.Feather); inventory.EquipB(TreasureId.None);
                    player.WarpTo(takeoff);
                    app.Step(1,Vector2.Zero,batched:batched);
                    var reference = new SeasonsFeatherRom(rooms.CurrentRoom,takeoff,2,true);
                    // Inject repeated allocation edges to isolate native pool
                    // saturation, independently of the joypad edge producer.
                    for (int edge = 0; edge < 3; edge++)
                    {
                        memory.SetField("wGameKeysPressed", 0);
                        app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched);
                        reference.UpdateKeys(1,1);
                        int expected = edge == 0 ? 1 : 3;
                        FailIf(player.CapeParentSlotMask != expected || reference[0xd300] == 0 ||
                            edge > 0 && reference[0xd400] == 0,
                            "Cape allocation must fill both native free slots and reject a third parent.");
                    }
                    int update = 3;
                    app.Step(47,Vector2.Zero,held:["attack"],batched:batched,afterUpdate:() =>
                    {
                        reference.UpdateKeys(1,0); update++;
                        FailIf(player.SwitchHookZFixed != reference.ZFixed || player.TopDownAirSpeedZ != reference.SpeedZ ||
                            player.CapeGliding != ((reference[0xcc77] & 0x20) != 0) ||
                            player.CapeParentActive != (reference[0xd300] != 0 || reference[0xd400] != 0),
                            $"Seasons saturated Cape parent pool must match ROM at update{update}.");
                    });
                    player.WarpTo(takeoff);
                }
                inventory.EquipA(TreasureId.Feather); inventory.EquipB(TreasureId.None);
                memory.SetField("wMagnetGloveState",1);
                try
                {
                    app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched);
                    FailIf(player.TopDownAirborne,"Feather must reject the shared native Magnet state before launch.");
                    memory.SetField("wMagnetGloveState",0);
                    app.Step(3,Vector2.Zero,held:["attack"],batched:batched);
                    FailIf(player.TopDownAirborne,"A rejected Feather edge must not restart when its gate clears.");
                }
                finally { memory.SetField("wMagnetGloveState",0); }
                CheckSharedAirTreasure(app, player, rooms, entities, dialogue, batched);
            }
        }
    }
}
