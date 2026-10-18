using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private TingleRoomEntity PrepareTingleAliasHit(bool batched)
    {
        ReinitializeGameplayForValidation(); LoadValidationRoom(0, 0x79); _entities.Clear();
        var actor = new TingleRoomEntity(new NpcDatabase().GetRoomNpcs(0, 0x79).Single(row => row.Id == 0xc8), new TingleDatabase(), _inventory);
        _entities.AddEntity(actor);
        _inventory.GiveTreasure(TreasureId.Feather, 1); _inventory.GiveTreasure(TreasureId.Sword, 1);
        _inventory.SetScriptedEquippedItems(equippedB: TreasureId.Feather, equippedA: TreasureId.Sword);
        _player.WarpTo(new(55, 41)); _player.Face(Vector2I.Down);
        FailIf(_collision.Collides(_player.Position) || _currentRoom.GetTerrainInfo(_player.Position).Hazard != HazardType.None,
            "Tingle alias regression must approach from the actual open floor above the balloon.");
        StepGameplayUpdates(1, Vector2.Zero, ["item"], ["item"], batched);
        StepGameplayUpdates(5, Vector2.Zero, batched: batched);
        FailIf(!_player.TopDownAirborne || actor.State != 1, "Tingle's accepted hit must follow real Feather input at compatible height.");
        return actor;
    }

    private void CheckSoundSweepAliasReader(SomariaRom native, int channel, byte sweep)
    {
        // Restore the measured pre-audio alias boundary: later queued sounds
        // may legitimately replace live channel bytes during request drain.
        var soundRam = SomariaPrivate<byte[]>(_sound.Driver, "_ram");
        soundRam[0xc03f + channel] = sweep; native[0xc03f + channel] = sweep;
        foreach (int address in new[] { 0xc01a, 0xc045 + channel, 0xc051 + channel, 0xfff2 + channel * 2, 0xfff3 + channel * 2 })
        {
            byte value = address == 0xc01a ? (byte)channel : address == 0xc051 + channel ? (byte)8 :
                address == 0xfff3 + channel * 2 ? (byte)4 : (byte)0;
            native[address] = value; soundRam[address] = value;
        }
        SomariaPrivate<FrontendRom>(native, "_rom").Call(0x41f3, 0x39);
        typeof(OracleSoundDriver).GetMethod("Call", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_sound.Driver, [0x41f3, 0]);
        int expected = 0x400 + unchecked((sbyte)sweep);
        FailIf(native.Word(0xfff2 + channel * 2) != expected || _sound.Driver.ReadStateWord(0xfff2 + channel * 2) != expected,
            $"Allocation alias must reach original sweep reader channel={channel}, sweep=${sweep:x2}, expected frequency=${expected:x4}.");
    }

    private void ValidateUncheckedEffectSoundAliasesRom()
    {
        foreach (bool moosh in new[] { true, false })
        foreach (bool batched in new[] { false, true })
        foreach (int free in new[] { 0, 1 })
        {
            MooshCompanionRoomEntity? companion = null; TingleRoomEntity? tingle = null;
            if (moosh)
            {
                companion = (MooshCompanionRoomEntity)PrepareMountedCompanionRom(0x0d).Actor;
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
                FailIf(companion.Phase != MooshCompanionPhase.Airborne, "Moosh must enter airborne state through mounted jump input.");
                // Declared water/capacity stress boundary, not a retail flight route.
                _currentRoom.SetPositionTileAndCollision(new(72, 69), 0xfe, 0, 0);
            }
            else tingle = PrepareTingleAliasHit(batched);
            Vector2 position = moosh ? CompanionField<Vector2>(companion!, "_precisePosition") : tingle!.Npc.Position;
            int z = moosh ? companion!.ZFixed : tingle!.ZFixed;
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, (int)_player.Position.X, (int)_player.Position.Y);
            if (moosh)
            {
                native[0xd100] = 1; native[0xd101] = 0x0d; native[0xd104] = 8; native[0xd105] = 1;
                native.Word(0xd10c, (int)(position.X * 256)); native.Word(0xd10a, (int)(position.Y * 256)); native.Word(0xd10e, z);
                native[0xffae] = 0; native[0xffaf] = 0xd1;
            }
            else
            {
                native[0xd240] = 1; native[0xd241] = 0xc8; native[0xd244] = 1;
                native[0xd0c0] = 1; native[0xd0c1] = 0x44; native.Word(0xd0d6, 0xd240);
                native[0xd0cb] = (byte)position.Y; native[0xd0cd] = (byte)position.X;
                native[0xffae] = 0xc0; native[0xffaf] = 0xd0;
            }
            FillInteractionAdmissionPool(native, free);
            int[] aliases = moosh ? [0xc03f, 0xc046] : [0xc040, 0xc043, 0xc04b, 0xc04d, 0xc04f];
            _sound.Driver.Stop();
            var snapshot = aliases.ToDictionary(address => address, _ => (byte)0);
            foreach (int address in aliases) { native[address] = 0; _sound.WriteNativeAllocationAlias(address, 0); }
            byte initialSweep = moosh && batched ? (byte)0x0e : (byte)0;
            if (moosh) { native[0xc03f] = initialSweep; _sound.WriteNativeAllocationAlias(0xc03f, initialSweep); snapshot[0xc03f] = initialSweep; }
            int writes = 0;
            void Observe(int address, byte value)
            {
                writes++;
                FailIf(_sound.Driver.ReadState(address) != value, $"Echo alias ${address:x4} must change the actual sound driver before request drain.");
                snapshot[address] = (byte)_sound.Driver.ReadState(address);
            }
            _entities.NativeSoundAllocationAliasWritten += Observe;
            try
            {
                if (moosh) StepGameplayUpdates(1, Vector2.Zero, batched: batched);
                else
                {
                    StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
                    for (int update = 0; update < 12 && tingle!.BalloonActive; update++)
                        StepGameplayUpdates(1, Vector2.Zero, ["attack"], [], batched);
                }
            }
            finally { _entities.NativeSoundAllocationAliasWritten -= Observe; }
            if (!moosh) native[0xd0cf] = unchecked((byte)tingle!.CollisionZ);
            byte[] caller = [0x16, moosh ? (byte)0xd1 : (byte)0xd0, 0xcd,
                moosh ? (byte)0xc7 : (byte)0x18, moosh ? (byte)0x79 : (byte)0x75, 0xc9];
            for (int index = 0; index < caller.Length; index++) native[0xc100 + index] = caller[index];
            SomariaPrivate<FrontendRom>(native, "_rom").Call(0xc100, moosh ? 5 : 0x11);
            int[] expected = free == 1 ? aliases.Select(address => address == 0xc03f ? initialSweep : 0).ToArray() : moosh
                ? [unchecked((byte)(initialSweep - 0x20)), 0x3c]
                : [0x56, 1, ((int)position.Y - 16) & 255, (int)position.X, tingle!.CollisionZ & 255];
            FailIf(aliases.Where((address, index) => native[address] != expected[index] || snapshot[address] != expected[index]).Any() ||
                writes != (free == 0 ? aliases.Length : 0),
                $"Unchecked effect moosh={moosh}, free={free}: failed writes must match original echo bytes and reach their authoritative audio consumer.");
            int children = moosh ? _entities.EntityAdapters<MooshHoverExclamationRoomEntity>().Count() : _entities.Entities<InteractionExplosionEffect>().Count;
            FailIf(children != free || NativeAdmissionChildren(native, moosh ? 0x9f : 0x56, 0).Length != free ||
                (moosh ? companion!.Phase != MooshCompanionPhase.HoveringOverWater : tingle!.BalloonActive || tingle.State != 3),
                "Failed unchecked creation must construct no node while completing the original Moosh/Tingle parent continuation.");
            CheckSoundSweepAliasReader(native, moosh ? 0 : 1, snapshot[moosh ? 0xc03f : 0xc040]);
            native.AdvanceInteractions();
            if (!moosh) FailIf(native[0xd244] != 3 || native[0xd246] != 15, "Original Tingle must continue into state 3/wait 15 after failed balloon explosion.");
            if (moosh && free == 1)
            {
                var child = _entities.EntityAdapters<MooshHoverExclamationRoomEntity>().Single();
                int at = NativeAdmissionChildren(native, 0x9f, 0).Single();
                FailIf(child.Counter != 60 || native[at + 6] != 60,
                    "INTERAC $9f initialization must return without decrementing Moosh's $3c counter.");
                _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 2); native[0xcc8a] = 2;
                int age = 0;
                StepGameplayUpdates(59, Vector2.Zero, batched: batched, afterUpdate: () =>
                {
                    age++; native.AdvanceInteractions(_entities.FrameCounter);
                    FailIf(child.Counter != 60 - age || native[at + 6] != 60 - age || child.Finished || native[at] == 0,
                        "Moosh's enabled-bit-7 exclamation must retain every countdown update under INTERACTION $02.");
                });
                StepGameplayUpdates(1, Vector2.Zero, batched: batched); native.AdvanceInteractions(_entities.FrameCounter);
                FailIf(!child.Finished || native[at] != 0 || _entities.EntityAdapters<MooshHoverExclamationRoomEntity>().Any(),
                    "Moosh's exclamation must delete on update60 after its setup-only visit, including under $02.");
                _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0); native[0xcc8a] = 0;
            }
            StepGameplayUpdates(80, Vector2.Zero, batched: batched);
            FailIf(moosh ? _entities.EntityAdapters<MooshHoverExclamationRoomEntity>().Any() : !tingle!.Grounded || _entities.Entities<InteractionExplosionEffect>().Count != 0,
                "Optional effects must expire without retry and Tingle must finish the fall after either allocation result.");
            if (!moosh)
            {
                _inventory.SetScriptedEquippedItems(equippedB: TreasureId.Sword, equippedA: 0);
                StepGameplayUpdates(1, Vector2.Zero, ["item"], ["item"], batched);
                StepGameplayUpdates(12, Vector2.Zero, batched: batched);
                FailIf(tingle!.BalloonActive || _entities.Entities<InteractionExplosionEffect>().Count != 0,
                    "Repeated grounded Sword action must not recreate the popped balloon or explosion.");
            }
            _entities.Clear();
        }
        ReinitializeGameplayForValidation();
        GD.Print("Compared Tingle/Moosh failed interaction pointers, parent continuation and actual production sweep-reader frequencies against original code.");
    }
}
