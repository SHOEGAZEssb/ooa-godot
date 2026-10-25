using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void CheckSoundVibratoAliasReader(SomariaRom native, int channel, byte active)
    {
        var ram = SomariaPrivate<byte[]>(_sound.Driver, "_ram");
        native[0xc045 + channel] = active; ram[0xc045 + channel] = active;
        foreach (int address in new[] { 0xc01a, 0xc03f + channel, 0xc04b + channel, 0xc051 + channel, 0xfff2 + channel * 2, 0xfff3 + channel * 2 })
        {
            byte value = address == 0xc01a ? (byte)channel : address == 0xc04b + channel ? (byte)3 :
                address == 0xc051 + channel ? (byte)2 : address == 0xfff3 + channel * 2 ? (byte)4 : (byte)0;
            native[address] = value; ram[address] = value;
        }
        SomariaPrivate<FrontendRom>(native, "_rom").Call(0x41f3, 0x39);
        typeof(OracleSoundDriver).GetMethod("Call", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_sound.Driver, [0x41f3, 0]);
        // Source table entry[2]=2 times intensity3: active bit4 skips wait.
        int frequency = (active & 0x10) != 0 ? 0x406 : 0x400;
        int counter = (active & 0x10) != 0 ? 3 : 1;
        FailIf(native.Word(0xc01f) != frequency || _sound.Driver.ReadStateWord(0xc01f) != frequency ||
            native[0xc051 + channel] != counter || ram[0xc051 + channel] != counter,
            $"Failed allocation vibrato alias channel={channel}, active=${active:x2} must reach original reader frequency=${frequency:x4}, phase/wait={counter}.");
    }

    private void ValidateSparkleSoundAliasesRom()
    {
        foreach (bool queenCase in new[] { false, true })
        foreach (bool batched in new[] { false, true })
        foreach (int free in queenCase ? new[] { 0, 1, 8, 9 } : new[] { 0, 1, 2, 3 })
        {
            TingleRoomEntity? tingle = null;
            if (queenCase)
            {
                ReinitializeGameplayForValidation(); LoadValidationRoom(3, 0xef); _roomEvents.Get<SeaFairyEvent>().Cancel(); _entities.Clear();
                _inventory.SetScriptedEquippedItems(0, 0); _player.WarpTo(new(80, 104));
                FailIf(_collision.Collides(_player.Position), "Sea Fairy alias observer must stand on the room's actual floor.");
                _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0x11);
                _entities.Spawn(new CleansedFairySpawn(new SeaFairyDatabase(), new(80, 40), () => { }, () => { }));
            }
            else
            {
                tingle = PrepareTingleAliasHit(batched);
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
                for (int update = 0; update < 12 && tingle.BalloonActive; update++) StepGameplayUpdates(1, Vector2.Zero, ["attack"], [], batched);
                StepGameplayUpdates(80, Vector2.Zero, batched: batched);
                FailIf(!tingle.Grounded, "Tingle must finish the actual popped-balloon fall before Kooloo.");
                _inventory.SetScriptedEquippedItems(0, 0); _player.WarpTo(new(24, 112));
                tingle.StartKooloo(); StepGameplayUpdates(59, Vector2.Zero, batched: batched);
            }
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, (int)_player.Position.X, (int)_player.Position.Y);
            Vector2 position = queenCase ? new(80, 40) : tingle!.Npc.Position;
            native[0xd240] = queenCase ? (byte)1 : (byte)0x81; native[0xd241] = queenCase ? (byte)0xd5 : (byte)0xc8;
            native[0xd242] = queenCase ? (byte)1 : (byte)0; native[0xd244] = queenCase ? (byte)0 : (byte)4;
            native[0xd24b] = (byte)position.Y; native[0xd24d] = (byte)position.X;
            if (queenCase) native[0xcc8a] = 0x11;
            FillInteractionAdmissionPool(native, free);
            _sound.RestartSound();
            foreach (int address in new[] { 0xc046, 0xc049 }) { native[address] = 0; _sound.WriteNativeAllocationAlias(address, 0); }
            byte active1 = 0, active4 = 0; int writes = 0;
            void Observe(int address, byte value)
            {
                writes++;
                FailIf(_sound.Driver.ReadState(address) != value, "Sparkle failure must mutate the actual audio byte before queued sounds drain.");
                if (address == 0xc046) active1 = value;
                if (address == 0xc049) active4 = value;
            }
            void SourceChildren()
            {
                native[0xcba0] = (byte)(_dialogue.IsOpen ? 1 : 0);
                if (!queenCase) native[0xd240] = 0;
                native.AdvanceInteractions(_entities.FrameCounter);
                if (!queenCase) native[0xd240] = 0x81;
            }
            void SourceBurst()
            {
                native[0xffae] = 0x40; native[0xffaf] = 0xd2;
                native.Word(0xd24e, 0); native.Word(0xd254, -0x200);
                byte[] caller = [0x16, 0xd2, 0xcd, 0xd9, 0x75, 0xc9];
                for (int index = 0; index < caller.Length; index++) native[0xc100 + index] = caller[index];
                SomariaPrivate<FrontendRom>(native, "_rom").Call(0xc100, 0x0b);
            }
            _entities.NativeSoundAllocationAliasWritten += Observe;
            try
            {
                StepGameplayUpdates(1, Vector2.Zero, batched: batched);
                if (!queenCase) SourceBurst();
                SourceChildren();
            }
            finally { _entities.NativeSoundAllocationAliasWritten -= Observe; }
            int expected1 = queenCase && free == 0 ? 0x78 : 0;
            int expected4 = free < (queenCase ? 9 : 3) ? (queenCase ? 0x1c : 0x10) : 0;
            int children = queenCase ? _entities.EntityAdapters<SeaFairySparkleRoomEntity>().Count() : _entities.Entities<InteractionSparkleEffect>().Count;
            int sourceChildren = NativeAdmissionChildren(native, 0x84, queenCase ? 4 : 0).Length +
                (queenCase ? NativeAdmissionChildren(native, 0x84, 0x0a).Length : 0);
            FailIf(children != free || sourceChildren != free || writes != (queenCase ? 9 : 3) - free ||
                active1 != expected1 || active4 != expected4 || native[0xc046] != expected1 || native[0xc049] != expected4,
                $"Sparkle aliases queen={queenCase}, free={free}: ordered partial creation and last failed $e046/$e049 writes must match original code and sound ownership.");
            CheckSoundVibratoAliasReader(native, 4, active4);
            if (queenCase) CheckSoundVibratoAliasReader(native, 1, active1);
            if (queenCase)
            {
                var queen = _entities.EntityAdapters<CleansedFairyRoomEntity>().Single();
                int age = 0;
                var priorText = _entities.TextActiveSource;
                bool text = false;
                _entities.TextActiveSource = () => text;
                try
                {
                    _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0x13); native[0xcc8a] = 0x13;
                    StepGameplayUpdates(120, Vector2.Zero, batched: batched, afterUpdate: () =>
                    {
                        age++; SourceChildren();
                        FailIf(queen.Counter != 180 - age || native[0xd246] != 180 - age,
                            "The Sea Fairy queen must count through text/$02 using its native always-update bit.");
                        foreach (var effect in _entities.EntityAdapters<SeaFairySparkleRoomEntity>())
                        {
                            int at = 0xd040 + _entities.InteractionSlot(effect.Node) * 256;
                            FailIf(native[at] == 0 || CompanionField<Vector2>(effect, "_position") != new Vector2(native.Word(at + 12) / 256f, native.Word(at + 10) / 256f),
                                $"Sea Fairy effect age={age}, slot=${at:x4}, role={CompanionField<string>(effect, "_role")}: port={effect.Node.Position}, native=({native.Word(at + 12) / 256f},{native.Word(at + 10) / 256f}), enabled=${native[at]:x2}; positions/deletion must match during and after restricted passes.");
                        }
                        int sourceCount = NativeAdmissionChildren(native, 0x84, 4).Length + NativeAdmissionChildren(native, 0x84, 0x0a).Length;
                        FailIf(_entities.EntityAdapters<SeaFairySparkleRoomEntity>().Count() != sourceCount ||
                            free > 0 && (age < 120) != NativeAdmissionChildren(native, 0x84, 4).Any(),
                            "The Sea Fairy flash must retain update119 and delete at update120 despite earlier text/$02 restrictions.");
                        if (age == 2) { text = true; native[0xcba0] = 1; }
                        if (age == 4) { text = false; native[0xcba0] = 0; _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0x11); native[0xcc8a] = 0x11; }
                    });
                }
                finally { _entities.TextActiveSource = priorText; }
            }
            if (!queenCase)
            {
                StepGameplayUpdates(35, Vector2.Zero, batched: batched, afterUpdate: SourceChildren);
                FailIf(_entities.Entities<InteractionSparkleEffect>().Count != free || NativeAdmissionChildren(native, 0x84, 0).Length != free,
                    "Admitted Kooloo children must retain their terminal animation through update 36.");
                StepGameplayUpdates(1, Vector2.Zero, batched: batched, afterUpdate: SourceChildren);
                FailIf(_entities.Entities<InteractionSparkleEffect>().Count != 0 || NativeAdmissionChildren(native, 0x84, 0).Length != 0,
                    "Both original and runtime Kooloo children must delete on update 37.");
                for (int update = 0; update < 180 && !tingle!.KoolooComplete; update++) StepGameplayUpdates(1, Vector2.Zero, batched: batched);
                FailIf(!tingle!.KoolooComplete, "Kooloo must complete even if every sparkle failed to allocate.");
                tingle.StartKooloo(); StepGameplayUpdates(60, Vector2.Zero, batched: batched);
                FailIf(_entities.Entities<InteractionSparkleEffect>().Count != 3, "Repeating Kooloo after capacity clears must admit all three fresh children.");
            }
            LoadValidationRoom(4, 0xa1);
            FailIf(_entities.Entities<InteractionSparkleEffect>().Count != 0 || _entities.EntityAdapters<SeaFairySparkleRoomEntity>().Any(),
                "Room cancellation must retire all admitted effects without retaining a rejected request.");
        }
        ReinitializeGameplayForValidation();
        GD.Print("Compared Tingle/Sea Fairy partial admission aliases and production vibrato-reader frequency/phase with original code, plus expiry, repeat and cancellation.");
    }
}
