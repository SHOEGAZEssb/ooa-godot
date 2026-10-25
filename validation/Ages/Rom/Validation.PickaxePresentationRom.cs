using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidatePickaxePresentationRom()
    {
        foreach (int subid in new[] { 0, 3 })
        foreach (bool above in new[] { false, true })
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(subid == 0 ? 1 : 4, subid == 0 ? 0x48 : 0xe1);
            // The upper Tower worker has no walkable floor above him; the
            // source var03=$01 placement supports both priority observations.
            var worker = _entities.Entities<NpcCharacter>().First(npc => npc.Record.Id == 0x57 && npc.Record.SubId == subid &&
                (subid == 0 || npc.Record.Var03 == 1));
            IRoomEntity parent = _entities.EntityAdapters<IRoomEntity>().Single(a => ReferenceEquals(a.Node, worker));
            foreach (var other in _entities.EntityAdapters<IRoomEntity>().Where(a => !ReferenceEquals(a, parent)).ToArray())
                _entities.RetireEntity(other);
            // Declare the parent in the low source slot so children receive
            // state zero later in this same interaction walk.
            InteractionAdmissionPool[parent] = 2;
            _inventory.SetScriptedEquippedItems(0, 0);
            var floors = (from y in Enumerable.Range(0, _currentRoom.HeightInTiles)
                from x in Enumerable.Range(0, _currentRoom.WidthInTiles)
                let point = new Vector2(x * 16 + 8, y * 16 + 8)
                where !_collision.Collides(point) && _currentRoom.GetTerrainInfo(point).Hazard == HazardType.None
                select point).ToArray();
            var eligibleFloors = floors.Where(point => above ? point.Y < worker.Position.Y - 12 : point.Y > worker.Position.Y + 12).ToArray();
            FailIf(eligibleFloors.Length == 0,
                $"Pickaxe fixture $57:${subid:x2}, above={above}, worker={worker.Position}: no observer floor among {string.Join(';', floors)}.");
            Vector2 floor = eligibleFloors[0];
            _player.WarpTo(floor); _player.BeginCutsceneControl();
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, (int)floor.X, (int)floor.Y);
            var data = new Room148PickaxeDatabase().Record;
            const int atParent = 0xd240;
            int updates = 0, strikes = 0;
            var publication = new InteractionMovementPublication(_entities, _runtimeState);
            StepGameplayUpdates(131, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                updates++;
                int parameter = worker.CurrentAnimationParameter;
                if (parameter != 0)
                {
                    strikes++;
                    native[atParent] = 1; native[atParent + 1] = 0x57;
                    native[atParent + 2] = (byte)subid; native[atParent + 0x21] = (byte)parameter;
                    native[atParent + 11] = (byte)worker.Position.Y; native[atParent + 13] = (byte)worker.Position.X;
                    native[atParent + 0x1a] = above ? (byte)0x81 : (byte)0x82;
                    // Original shared worker helper; A=$03 is independent
                    // of the strike parameter, which only chooses the origin.
                    native[0xffae] = 0x40; native[0xffaf] = 0xd2;
                    byte[] caller = [0x16, 0xd2, 0x3e, 3, 0xcd, 0xa4, 0x6a, 0xc9];
                    for (int i = 0; i < caller.Length; i++) native[0xc100 + i] = caller[i];
                    SomariaPrivate<FrontendRom>(native, "_rom").Call(0xc100, 9);
                    native[atParent] = 0; // Parent animation is a declared input, not replayed here.
                }
                InteractionMovementPublication.Seed(native);
                native.AdvanceInteractions(_entities.FrameCounter);
                publication.Compare(native, "Pickaxe debris $92:$06");
                var chips = _entities.Entities<Room148PickaxeDebris>().OrderBy(_entities.InteractionSlot).ToArray();
                int[] source = NativeAdmissionChildren(native, 0x92, 6);
                string context = $"Pickaxe $57:${subid:x2} above={above}, batch={batched}, update={updates}";
                FailIf(chips.Length != source.Length, context + ": source chip lifetime differs.");
                for (int i = 0; i < chips.Length; i++)
                {
                    int slot = source[i]; var chip = chips[i];
                    int expectedZ = above ? 12 : 11;
                    FailIf(native[slot + 3] != 3 || native[slot + 0x1c] != 0x0b ||
                        (native[slot + 0x1a] & 3) != (above ? 0 : 1) || chip.Palette != 3 || chip.ZIndex != expectedZ,
                        $"{context}: palette/priority={chip.Palette}/{chip.ZIndex}, original flags=${native[slot + 0x1c]:x2}, visible=${native[slot + 0x1a]:x2}; expected palette3/Z{expectedZ}.");
                    FailIf(chip.PrecisePosition != new Vector2(native.Word(slot + 12)/256f, native.Word(slot + 10)/256f) ||
                        chip.ZFixed != unchecked((short)native.Word(slot + 14)), context + ": presentation fix changed source motion.");
                    if (parameter == 0) continue;
                    var actual = SomariaPrivate<Texture2D>(chip, "_texture");
                    var frame = OracleGraphicsCache.GetAnimationDefinition(data.DebrisAnimation).Frames[0];
                    using var expected = OracleOamCompositor.BuildOamTextureUncachedForValidation(
                        OracleGraphicsCache.LoadImage(AgesGameContent.AssetRoot + $"gfx/{data.DebrisSpriteName}.png"), frame.EncodedOam, 2, 3);
                    using var actualImage = actual.GetImage(); using var expectedImage = expected.GetImage();
                    FailIf(!actualImage.GetData().SequenceEqual(expectedImage.GetData()), context + ": chip texture must use source bank-1 tile $02 and palette3.");
                }
            });
            FailIf(strikes != 3, "Both worker producers must retain three natural strikes through update130, covering both horizontal origins.");
            _entities.Clear(); StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(_entities.Entities<Room148PickaxeDebris>().Count != 0, "Cancelling a live third strike must retire its presentation owner.");
        }
        ReinitializeGameplayForValidation();
    }
}
