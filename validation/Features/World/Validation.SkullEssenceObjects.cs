using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullEssenceObjects()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var textSource = _entities.TextActiveSource;
        var slots = (Dictionary<IRoomEntity, int>)typeof(RoomEntityManager).GetField("_interactionSlots", flags)!.GetValue(_entities)!;
        try
        {
            foreach (bool batch in new[] {false, true})
            {
                bool text = true;
                _entities.TextActiveSource = () => text || textSource();
                void Step(int count = 1, Vector2 movement = default) =>
                    StepGameplayUpdates(count, movement, [], [], batched: batch);
                _saveData.SetRoomFlag(4, 0x69, OracleSaveData.RoomFlagItem, false);
                LoadValidationRoom(4, 0x69);
                _player.WarpTo(new(120,140));
                var essence = _entities.Entities<DungeonEssence>().Single();
                FailIf(essence.Initialized || essence.Visible || _entities.Entities<DungeonEssencePedestal>().Count != 0,
                    "Essence $7f:$00 must wait for state0 before allocating its pedestal or becoming visible.");
                Step();
                var pedestal = _entities.Entities<DungeonEssencePedestal>().Single();
                var glow = _entities.Entities<DungeonEssenceGlow>().Single();
                FailIf(slots[essence] != 2 || slots[pedestal] != 3 || slots.ContainsKey(glow),
                    "The Essence must use dynamic $d2, its pedestal $d3, and the glow reserved $d1 outside the dynamic allocator.");
                FailIf(!essence.Initialized || !essence.Visible || !pedestal.Initialized || !pedestal.Visible ||
                    glow.Initialized || glow.Visible || _currentRoom.GetTerrainInfo(new(120,40)).Collision != 15 ||
                    essence.ZIndex != 11 || pedestal.ZIndex != 8 || glow.ZIndex != 9,
                    "State0 must initialize the later pedestal, leave reserved glow for next update, and use source visible81/83/82 priorities.");
                Step();
                FailIf(!glow.Initialized || !glow.Visible || glow.AnimationFrame != 0 || glow.Z != 0 ||
                    glow.Position != new Vector2(120,40),
                    "Reserved glow state0 must only initialize graphics; its initial copy precedes parent zh=-$10.");
                // Source glow frames each last2 updates, parameters0/1/0/1.
                int[] frames = [0,1,1,2,2,3,3,0];
                bool[] visible = [true,false,false,false,false,true,true,true];
                for (int i = 0; i < frames.Length; i++)
                {
                    Step();
                    FailIf(glow.AnimationFrame != frames[i] || glow.Visible != visible[i] || glow.Z != -16 ||
                        essence.DrawZ != -16 || essence.Position != new Vector2(120,40),
                        $"Reserved glow under text lost animation/consumed parameter or froze with its parent on update{i+1}.");
                }
                text = false;
                bool moved = false;
                for (int i = 0; !_dialogue.IsOpen && i < 220; i++)
                {
                    Vector2 previous = essence.Position.Floor(); int previousZ = essence.DrawZ;
                    Step(movement: Vector2.Up);
                    FailIf(glow.Position != previous || glow.Z != previousZ,
                        "Reserved $d1 glow must take its dynamic parent's preceding high XYZ before the parent moves.");
                    moved |= essence.Position != previous;
                }
                FailIf(!moved || !_dialogue.IsOpen || !essence.ReadyForDialogue,
                    "The real pedestal approach must complete its movement and open the Essence dialogue.");
                bool changed = false;
                for (int i = 0; i < 8; i++)
                {
                    bool before = glow.Visible; Step(); changed |= before != glow.Visible;
                }
                FailIf(!changed || !essence.ReadyForDialogue,
                    "The reserved glow must keep flickering while the real get-Essence textbox holds the parent.");
                LoadValidationRoom(4, 0x91);
                FailIf(_entities.Entities<DungeonEssenceGlow>().Count != 0 || _entities.Entities<DungeonEssencePedestal>().Count != 0,
                    "Room replacement retained an Essence child or reserved glow.");
                LoadValidationRoom(4, 0x69); _player.WarpTo(new(120,140));
                Step();
                FailIf(_entities.Entities<DungeonEssenceGlow>().Count != 0 ||
                    _entities.Entities<DungeonEssence>().Single().Visible ||
                    _entities.Entities<DungeonEssencePedestal>().Single() is not {Visible:true, Initialized:true},
                    "Collected re-entry must initialize only the persistent pedestal, without an Essence/glow sprite.");
                FailIf(slots.ContainsKey(_entities.Entities<DungeonEssence>().Single()) ||
                    slots[_entities.Entities<DungeonEssencePedestal>().Single()] != 3,
                    "Collected state0 must free dynamic parent $d2 after creating the pedestal in $d3.");

                _saveData.SetRoomFlag(4, 0x69, OracleSaveData.RoomFlagItem, false);
                LoadValidationRoom(4, 0x69); _player.WarpTo(new(120,140));
                var reservations = new List<IRoomEntity>();
                try
                {
                    for (int slot = 3; slot < 16; slot++)
                    {
                        var reservation = new ArmosSlotReservation();
                        reservations.Add(reservation); slots.Add(reservation, slot);
                    }
                    Step();
                    FailIf(_entities.Entities<DungeonEssencePedestal>().Count != 0 ||
                        _entities.Entities<DungeonEssenceGlow>().Count != 1,
                        "With all fourteen dynamic slots occupied, pedestal allocation must fail while reserved glow creation still succeeds.");
                }
                finally
                { foreach (var reservation in reservations) { slots.Remove(reservation); reservation.Node.Free(); } }
            }
        }
        finally { _entities.TextActiveSource = textSource; }
    }
}
