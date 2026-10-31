using Godot;
using System;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedAirTreasure(ApplicationValidationFixture app, Player player,
        RoomSession rooms, RoomEntityManager entities, DialogueBox dialogue, bool batched)
    {
        var memory = rooms.RuntimeState;
        var inventory = player.Inventory;
        var game = memory.Game;
        entities.Clear(); player.ClearNativeItemParents();
        player.WarpTo(player.PrecisePosition); player.Face(Vector2I.Right);
        inventory.GiveTreasure(TreasureId.Feather, 1);
        inventory.EquipA(TreasureId.Feather); inventory.EquipB(0); inventory.SetScriptedActiveRing(0xff);
        memory.SetField("wDisabledObjects", 0); memory.SetField("wLinkForceState", 0);
        app.Step(1, Vector2.Zero, batched: batched);
        FailIf(rooms.CurrentRoom.IsSolid(player.Position), "Air treasure must use the approached room's dry floor.");
        var native = new BraceletRom(rooms.CurrentRoom, player.PrecisePosition, 1, 0xff, game: game);
        native.LoadSave(rooms.SaveData); native.InitializeWalkingAnimation();
        native[0xcd00] = memory.ReadField("wScrollMode");
        foreach (string field in new[] { "wcc50", "wcc52", "wInstrumentsDisabledCounter" })
            native.SetNativeByte(memory.FieldAddress(field), memory.ReadField(field));
        var seed = entities.Random.CaptureState(); native[0xff94] = seed.Rng1; native[0xff95] = seed.Rng2;
        int updates = 0;
        GroundTreasurePickup? pickup = null;
        int actor = 0;
        void Step(int count = 1, bool jump = false, bool held = false) => app.Step(count, Vector2.Zero,
            held: jump || held ? ["attack"] : [], pressed: jump ? ["attack"] : [], batched: batched,
            afterUpdate: () =>
            {
                native.UpdateLinkItems(entities.FrameCounter, jump || held ? 1 : 0, interactions: true);
                string context = $"{game}/air treasure/batch{batched}/update{++updates}";
                FailIf(player.ItemCreationZFixed != unchecked((short)native.Word(0xd00e)) ||
                    player.PrecisePosition != new Vector2(native.Word(0xd00c) / 256f, native.Word(0xd00a) / 256f),
                    $"{context}: position/Z runtime{player.PrecisePosition}/${player.ItemCreationZFixed & 0xffff:x4}, ROM({native.Word(0xd00c) / 256f},{native.Word(0xd00a) / 256f})/${native.Word(0xd00e):x4}, native state${native[0xd004]:x2}.");
                foreach (int address in new[] { 0xd020, 0xd021, 0xd022, 0xd023, 0xd030, 0xd031 })
                    FailIf(memory.ReadWramByte(address) != native[address],
                        $"{context}: native animation ${address:x4} runtime${memory.ReadWramByte(address):x2}/ROM${native[address]:x2}.");
                foreach (string field in new[] { "wLinkForceState", "wcc50", "wcc52", "wDisabledObjects", "wInstrumentsDisabledCounter" })
                    FailIf(memory.ReadField(field) != native.ReadNativeByte(memory.FieldAddress(field)),
                        $"{context}: {field} runtime${memory.ReadField(field):x2}/ROM${native.ReadNativeByte(memory.FieldAddress(field)):x2}.");
                if (pickup is not null && GodotObject.IsInstanceValid(pickup))
                    FailIf((pickup.State == PickupState.Collected) != (native[actor] != 0 && native[actor + 4] == 3) && !pickup.Finished,
                        $"{context}: treasure collection runtime{pickup.State}/native{native[actor + 4]:x2}, LinkZ{player.ItemCreationZFixed >> 8}, objectZ{unchecked((sbyte)native[actor + 0x0f])} must run at its source interaction visit.");
                var rng = entities.Random.CaptureState();
                FailIf(rng.Rng1 != native[0xff94] || rng.Rng2 != native[0xff95] || rng.Calls - seed.Calls != native.RandomCalls,
                    $"{context}: shared RNG differs.");
            });
        try
        {
            foreach (int airMode in game == OracleGame.Seasons ? new[] { 0x18, 0x19 } : new[] { 0x18 })
            for (int repeat = 0; repeat < 2; repeat++)
            {
                bool gliding = airMode == 0x19;
                inventory.GiveTreasure(TreasureId.Feather, gliding ? 2 : 1);
                // Native mode$08 keeps an existing Cape upgrade. Waiting for
                // a sword treasure must preserve whichever level was staged.
                int featherLevel = inventory.FeatherLevel;
                native.LoadSave(rooms.SaveData);
                if (gliding)
                {
                    Step(25, jump: true);
                    FailIf(!player.CapeGliding || native[0xd030] != 0x19,
                        "The Cape handoff must reach native glide through actual held Feather input.");
                }
                var request = new GroundTreasureGrantRequest(rooms.ActiveGroup, rooms.CurrentRoom.Id, 0,
                    (int)player.Position.Y, (int)player.Position.X, "TREASURE_OBJECT_SWORD_01",
                    "treasure.s:@checkLinkTouched signed Z and linkState04 airborne restore") { GrabMode = 1 };
                // objectCreateInteraction copies the producer's Z high byte. Keep
                // the ordinary treasure at -20 so grounded Link cannot collect
                // it; the Cape producer uses Link's current airborne Z high.
                byte birthZ = unchecked((byte)(gliding ? player.ItemCreationZFixed >> 8 : -20));
                pickup = entities.Spawn<GroundTreasurePickup>(new GroundTreasureRecordSpawn(
                    request.Resolve(TreasureDatabase.For(rooms.AssetRoot)), birthZ));
                actor = 0xd040 + entities.InteractionSlot(pickup) * 0x100;
                native[actor] = 1; native[actor + 1] = 0x60; native[actor + 2] = 0x05; native[actor + 3] = 1;
                native[actor + 0x0b] = (byte)player.Position.Y; native[actor + 0x0d] = (byte)player.Position.X;
                native[actor + 0x0f] = birthZ;
                Step(3);
                FailIf(!gliding && (pickup.State != PickupState.Waiting || inventory.FeatherLevel != featherLevel),
                    $"{game}/repeat{repeat}: signed Z must keep treasure waiting on real floor, state{pickup.State}, Z{pickup.ZFixed >> 8}, Feather{inventory.FeatherLevel}/{featherLevel}.");
                if (!gliding) Step(jump: true);
                for (int wait = 0; wait < 40 && (native[0xd004] != 4 || native[0xd005] != 1); wait++) Step();
                FailIf(!player.TopDownAirborne || memory.ReadField("wcc52") != airMode || native[0xd004] != 4,
                    $"{game}/mode${airMode:x2}/repeat{repeat}: airborne treasure must enter state04 and save the native air mode without grounding Link.");
                int heldZ = player.ItemCreationZFixed;
                Step(4);
                FailIf(player.ItemCreationZFixed != heldZ || !dialogue.IsOpen,
                    "State04 must retain the airborne height while treasure text owns the gate.");
                dialogue.Close(); native[0xcba0] = 0; Step();
                FailIf(memory.ReadWramByte(0xd030) != airMode || player.ScriptedLinkAnimationMode is not null ||
                    memory.ReadField("wDisabledObjects") != 0 || !pickup.Finished,
                    "Text closure must restore the native air owner, then retire treasure and clear only its mask at its slot.");
                for (int wait = 0; wait < 48 && player.TopDownAirborne; wait++) Step();
                FailIf(player.TopDownAirborne || native[0xd004] != 1 || memory.ReadWramByte(0xd030) != 0x10,
                    "The restored air stream must resume, land, and return to ordinary WALK.");
                pickup = null; Step(2);
            }
            inventory.GiveTreasure(TreasureDatabase.For(rooms.AssetRoot).GetObject("TREASURE_OBJECT_FLUTE_00"));
            inventory.EquipA(TreasureId.Flute);
            native.LoadSave(rooms.SaveData);
            memory.SetField("wInstrumentsDisabledCounter", 1);
            native.SetNativeByte(memory.FieldAddress("wInstrumentsDisabledCounter"), 1);
            Step(jump: true);
            FailIf(player.IsUsingHarp || native[0xd500] != 0 || memory.ReadField("wInstrumentsDisabledCounter") != 0,
                "An instrument edge must read counter1 before the post-Link decrement to zero.");
            Step(2, held: true);
            FailIf(player.IsUsingHarp || native[0xd500] != 0,
                "A rejected instrument edge must not restart when its lock expires under a held button.");
            memory.SetField("wInstrumentsDisabledCounter", 2);
            native.SetNativeByte(memory.FieldAddress("wInstrumentsDisabledCounter"), 2);
            memory.SetField("wDisabledObjects", 1); native[0xcc8a] = 1;
            Step(2);
            FailIf(memory.ReadField("wInstrumentsDisabledCounter") != 0,
                "The shared special-object tail must tick the lock even when Link's state returns on its disable mask.");
            memory.SetField("wDisabledObjects", 0); native[0xcc8a] = 0;
        }
        finally
        {
            dialogue.Close(); entities.Clear(); player.WarpTo(player.PrecisePosition);
            memory.SetField("wDisabledObjects", 0); memory.SetField("wLinkForceState", 0);
        }
    }
}
