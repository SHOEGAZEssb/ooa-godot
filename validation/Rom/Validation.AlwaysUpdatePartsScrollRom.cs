using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateAlwaysUpdatePartsScrollRom()
    {
        int publicationChecks=0;
        foreach (bool batched in new[] { false, true })
        foreach (bool outward in new[] { false, true })
        foreach (string ending in new[] { "expiry", "complete", "delete", "cancel" })
        for (int repeat = 0; repeat < 2; repeat++)
        {
            if (repeat == 0 || ending == "cancel") ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xa1); _entities.Clear();
            _inventory.SetScriptedEquippedItems(0, 0); _player.WarpTo(new(120, 40));
            FailIf(_collision.Collides(_player.Position), "PART scroll fixture requires original room $4:$a1 floor.");
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 120, 40);
            byte duration = ending == "expiry" ? (byte)20 : (byte)0xff;
            // Declared effects isolate their eligibility during a real room
            // transition; this is not a natural swirl/Jabu coexistence claim.
            for (int slot = 0; slot < 8; slot++)
            {
                _entities.Spawn<BlueEnergyBeadRoomEntity>(new BlueEnergyBeadSpawn(7 - slot, new(120, 72), duration, outward));
                int at = 0xd0c0 + slot * 256;
                native[at] = 1; native[at + 1] = 0x53; native[at + 3] = (byte)(7 - slot);
                native[at + 6] = duration; native[at + 0x30] = 72; native[at + 0x31] = 120;
                native[at + 0x32] = outward ? (byte)1 : (byte)0;
            }
            for (int index = 0; index < 2; index++)
            {
                Vector2 point = new(index == 0 ? 64 : 96, index == 0 ? 1 : 136);
                _entities.AddEntity(new JabuBubbleRoomEntity(point, index, new JabuExteriorDatabase(), _random, _runtimeState));
                int at = 0xd8c0 + index * 256;
                native[at] = 1; native[at + 1] = 0x16; native[at + 2] = (byte)index;
                native[at + 11] = (byte)point.Y; native[at + 13] = (byte)point.X;
            }
            var beam = _entities.Spawn<BeamosBeamPart>(new BeamosBeamSpawn(new(80, 72), 8, 0));
            native[0xdac0] = 1; native[0xdac1] = 0x29; native[0xdac9] = 8;
            native[0xdacb] = 72; native[0xdacd] = 80;
            var slots = SomariaPrivate<Dictionary<IRoomEntity, int>>(_entities, "_partSlots");
            var originalSlots = new Dictionary<IRoomEntity, int>(slots);
            var originals = originalSlots.Keys.ToArray();
            void Compare()
            {
                foreach (var entity in originals)
                {
                    bool exists = slots.TryGetValue(entity, out int slot);
                    int at = 0xd0c0 + originalSlots[entity] * 256;
                    FailIf(exists != (native[at] != 0), $"PART scroll {ending}, repeat{repeat}: slot ${at:x4} lifetime differs.");
                    if (!exists) continue;
                    FailIf(slot != (at - 0xd0c0) / 256, "Outgoing PARTs must retain their original physical slots.");
                    if (entity is BlueEnergyBeadRoomEntity bead)
                        FailIf(bead.Duration != native[at + 6] || bead.Delay != native[at + 7] ||
                            bead.Initialized != (native[at + 4] != 0) || bead.Visible != ((native[at + 26] & 128) != 0) ||
                            bead.PrecisePosition != new Vector2(native.Word(at + 12) / 256f, native.Word(at + 10) / 256f),
                            $"PART $53 scroll {ending}, index{bead.Index}: duration={bead.Duration}/{native[at + 6]}, delay={bead.Delay}/{native[at + 7]}, position={bead.PrecisePosition}.");
                    else if (entity is JabuBubbleRoomEntity bubble)
                        FailIf(SomariaPrivate<int>(bubble, "_state") != native[at + 4] ||
                            SomariaPrivate<int>(bubble, "_counter") != native[at + 6] ||
                            SomariaPrivate<int>(bubble, "_counter2") != native[at + 7] ||
                            SomariaPrivate<Vector2>(bubble, "_position") != new Vector2(native.Word(at + 12) / 256f, native.Word(at + 10) / 256f) ||
                            bubble.Visible != ((native[at + 26] & 128) != 0),
                            $"PART $16 scroll {ending}: state/counters/position/visibility differ at ${at:x4}.");
                    else FailIf(beam.Counter != native[at + 6], "Ordinary PART $29 must remain frozen during scrolling.");
                }
            }
            StepGameplayUpdates(1, Vector2.Zero, batched: batched); native.AdvanceParts(_entities.FrameCounter); Compare();
            FailIf(beam.Counter != 2, "The ordinary control must initialize before scrolling.");
            FailIf(!_rooms.TryGetNeighbor(Vector2I.Left, out int target) || target != 0xa0,
                "PART scroll must use imported dungeon adjacency $4:$a1 -> $4:$a0.");
            _transitions.BeginScroll(_player, Vector2I.Left, target);
            native.SetOutgoingParts(); native[0xcd00] = 8;
            FailIf(native[0xd0c0] != 0x82 || native[0xd8c0] != 0x82 || native[0xdac0] != 2,
                "setPartsEnabledTo2 must retain enabled bit7 only on its source owners.");
            // Incoming room parsing has separate coverage. Start the isolated
            // effect RNG comparison after the real destination preload.
            var seed = _random.CaptureState(); int sourceCalls = native.RandomCalls;
            native[0xff94] = seed.Rng1; native[0xff95] = seed.Rng2;
            int update = 0;
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                bool movingBubble=native[0xd9c0]!=0&&native[0xd9c4]>=2;
                update++; native.AdvanceParts(_entities.FrameCounter);
                if (!_transitions.ScrollActive) { native.ClearOutgoingParts(); native[0xcd00] = 4; }
                // Observe the last moving bubble, before its retirement leaves
                // a different source movement owner as the final scratch writer.
                if (_transitions.ScrollActive&&movingBubble)
                {
                    publicationChecks++;
                    FailIf(Enumerable.Range(0,4).Any(i=>_runtimeState.ReadWramByte(0xcec0+i)!=native[0xcec0+i]),
                        "Scrolling PART movement must publish native scratch in original slot order.");
                }
                Compare();
                FailIf(RandomDiffers(seed, native[0xff94], native[0xff95], native.RandomCalls - sourceCalls),
                    "Scrolling PART dispatch must retain ordered shared RNG draws.");
            });
            Step(3);
            FailIf(!_transitions.ScrollActive || ending == "expiry" && originals.OfType<BlueEnergyBeadRoomEntity>().Any(bead => bead.Duration != 17),
                "Three active scroll visits must decrement duration20 to17.");
            if (ending == "cancel")
            {
                LoadValidationRoom(4, 0xa1); native.ClearOutgoingParts();
                // Debug room replacement clears the entity owner, not the
                // independent transition clock. Check the relevant lifetime.
                FailIf(originals.Any(slots.ContainsKey), "Room replacement during scrolling must clear outgoing PART ownership.");
                Compare(); StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            }
            else
            {
                if (ending == "delete")
                {
                    _runtimeState.SetWramByte(WramAddress.wDeleteEnergyBeads, 1); native[WramAddress.wDeleteEnergyBeads] = 1;
                    Step(1);
                    FailIf(originals.OfType<BlueEnergyBeadRoomEntity>().Any(slots.ContainsKey), "The shared delete signal must retire beads during scrolling.");
                }
                if (ending == "expiry")
                {
                    Step(16);
                    FailIf(originals.OfType<BlueEnergyBeadRoomEntity>().Any(bead => bead.Duration != 1 || !slots.ContainsKey(bead)),
                        "Finite beads must remain alive through scroll update19.");
                    Step(1);
                    FailIf(originals.OfType<BlueEnergyBeadRoomEntity>().Any(slots.ContainsKey), "Finite beads must expire on scroll update20.");
                }
                Step(_transitions.ScrollTotalFrames - update);
                FailIf(_transitions.ScrollActive || originals.Any(slots.ContainsKey), "The finishing scroll update must release all outgoing slots.");
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
                FailIf(originals.Any(slots.ContainsKey), "Destination updates must not revive retired PARTs.");
            }
        }
        FailIf(publicationChecks==0,"Real scroll comparison must observe positive Jabu bubble publication.");
        ReinitializeGameplayForValidation();
        GD.Print("Compared $53/$16 always-update PARTs through 32 real scroll lifecycles: both swirls/schedules, expiry, shared delete, completion, cancellation and repeat; ordinary $29 freeze control retained.");
    }
}
