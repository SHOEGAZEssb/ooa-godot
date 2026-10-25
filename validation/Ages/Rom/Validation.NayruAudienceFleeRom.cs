using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateNayruMonkeyEscapeAnimationRom()
    {
        ReinitializeGameplayForValidation(); _saveData.SetGlobalFlag(GlobalFlag.IntroDone, false); LoadValidationRoom(0, 0x39);
        var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 40, 112);
        native[0xd140] = 1; native[0xd141] = 0x39; native[0xd144] = 1;
        native[0xd145] = 2; native[0xd146] = 1;
        native.AdvanceInteractions();
        var animation = new ObjectAnimationRom(0, 0x39); animation.Set(0x0b);
        FailIf(native.Word(0xd162) != animation.Pointer || native[0xd145] != 3 || native.Word(0xd154) != 0xff00,
            $"Monkey zero-delay entry must retain rst target-low A=$0b for its first animation (pointer${native.Word(0xd162):x4}/${animation.Pointer:x4}).");
        var owner = _roomEvents.Get<NayruIntroEvent>(); var actor = owner.ActorRegistry["Monkey"];
        string encoded = new NayruIntroEventDatabase().Flee("Monkey").InitialEscapeAnimation;
        FailIf(encoded.Length == 0, "Monkey source animation$0b must be explicitly imported.");
        actor.SetScriptAnimation(encoded);
        var definition = OracleGraphicsCache.GetAnimationDefinition(encoded);
        for (int tick = 0; tick < 96; tick++)
        {
            var frame = definition.Frames[actor.CurrentAnimationFrame];
            int counter = frame.Duration - (int)SomariaPrivate<double>(actor, "_animationTicks");
            FailIf(frame.EncodedOam != animation.Oam || counter != animation.Counter || actor.CurrentAnimationParameter != animation.Parameter,
                $"Monkey initial escape animation$0b tick{tick}: actual native OAM/counter/parameter differ.");
            actor.AdvanceAnimationUpdates(1); animation.Advance();
        }
    }

    private void ValidateNayruAudienceFleeRom()
    {
        foreach (string selected in new[] { "Bear", "Monkey", "Rabbit", "Boy", "Bird", "All" })
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); _saveData.SetGlobalFlag(GlobalFlag.IntroDone, false);
            _saveData.SetRoomFlag(0, 0x39, 0x80, true); LoadValidationRoom(0, 0x39);
            var owner = _roomEvents.Get<NayruIntroEvent>();
            string[] names = selected == "All" ? ["Bear", "Monkey", "Rabbit", "Boy", "Bird"] : [selected];
            foreach (string name in names) owner.ActorRegistry[name].Position += new Vector2(0.25f, 0.5f);
            _player.WarpTo(new(40, 112)); _player.BeginCutsceneControl(owner: owner);
            _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 1); _runtimeState.SetWramByte(AgesWramAddress.wWarpsDisabled, 1);
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 40, 112);
            native.InitializeLinkGameplay(); native[0xcc8a] = 1; native[0xcc6e] = 1;
            var addresses = names.ToDictionary(name => name, name => 0xd040 + _entities.InteractionSlot(owner.ActorRegistry[name]) * 256);
            foreach (string name in names)
            {
                int at = addresses[name]; var actor = owner.ActorRegistry[name];
                native[at] = 1; native[at + 1] = (byte)actor.Record.Id;
            }
            native.AdvanceInteractions(); // Real state-zero graphics/script admission.
            foreach (string name in names)
            {
                int at = addresses[name]; var point = OracleObjectPosition.FromPixels(owner.ActorRegistry[name].Position);
                native.Word(at + 10, point.YFixed); native.Word(at + 12, point.XFixed);
                native[at + 5] = (byte)(name == "Bear" ? 2 : 1);
            }
            native[0xcfd0] = 0x10; _runtimeState.SetWramByte(0xcfd0, 0x10);
            native.AdvanceInteractions(); // Source signal observation/setup only.
            ((ICutsceneCommandHost)owner).RunNativeHandler("BeginNayruAudienceEscape");
            var fleeing = SomariaPrivate<List<FleeingAudience>>(owner, "_nayruFleeingAudience");
            fleeing.RemoveAll(actor => !names.Contains(actor.Record.Actor));
            int visits = 0, moves = 0, releaseVisits = 0;
            bool held = false, releaseDuringPass = false, openTextDuringPass = false;
            _entities.AddEntity(new PartMovementScratchProbe(() =>
            {
                if (releaseDuringPass)
                {
                    releaseVisits++;
                    _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 1);
                }
                if (openTextDuringPass && !_dialogue.IsOpen)
                    _dialogue.ShowMessage("Later interaction opens text.", _player.Position.Y);
            }));
            void Seed() { for (int i = 0; i < 4; i++) { byte value = (byte)(0xa5 + i); native[0xcec0 + i] = value; _runtimeState.SetWramByte(0xcec0 + i, value); } }
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                int[] before = names.Select(name => native.Word(addresses[name] + 10)).ToArray();
                native[0xcc00] = (byte)_entities.FrameCounter; native.AdvanceInteractions(); visits++;
                // Declared later-writer boundary: clearing a mask after the
                // native walk must not retroactively admit the frozen actors.
                if (releaseDuringPass) native[0xcc8a] = 1;
                if (openTextDuringPass) native[0xcba0] = 1;
                for (int index = 0; index < names.Length; index++)
                {
                    string name = names[index]; int at = addresses[name]; var actor = fleeing.Single(row => row.Record.Actor == name);
                    bool live = native[at] != 0;
                    if (live && native.Word(at + 10) != before[index]) moves++;
                    FailIf(actor.Actor.Active != live || live && (actor.Position.YFixed != native.Word(at + 10) ||
                        actor.Position.XFixed != native.Word(at + 12) || actor.ZFixed != (short)native.Word(at + 14) ||
                        actor.SpeedZ != (short)native.Word(at + 0x14) || actor.Delay != native[at + 6] ||
                        actor.Actor.ScriptDrawOffset.Y != ((short)native.Word(at + 14) >> 8)),
                        $"Audience {name}/{selected} visit{visits}: native delay/XY/Z/landing/lifetime differs; runtime {actor.Position.PrecisePosition},Z{actor.ZFixed}/{actor.SpeedZ},delay{actor.Delay}; native ${native.Word(at + 12):x4},${native.Word(at + 10):x4},Z{(short)native.Word(at + 14)}/{(short)native.Word(at + 0x14)},delay{native[at + 6]}.");
                }
                FailIf(Enumerable.Range(0, 4).Any(i => _runtimeState.ReadWramByte(0xcec0 + i) != native[0xcec0 + i]),
                    $"Audience {selected} visit{visits}: native phase movement publication differs.");
                Seed();
            });
            Seed(); Step(3);
            _dialogue.ShowMessage("Audience ordinary text hold.", _player.Position.Y); native[0xcba0] = 1;
            int beforeHold = moves; Step(3); held = true;
            FailIf(moves != beforeHold, "Ordinary audience must freeze under text.");
            _dialogue.Close(); native[0xcba0] = 0;
            _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 3); native[0xcc8a] = 3;
            Step(3);
            _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 1); native[0xcc8a] = 1;
            _runtimeState.SetWramByte(AgesWramAddress.wScrollMode, 8); native[0xcd00] = 8;
            Step(3);
            _runtimeState.SetWramByte(AgesWramAddress.wScrollMode, 1); native[0xcd00] = 1;
            _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 3); native[0xcc8a] = 3;
            releaseDuringPass = true; Step(1);
            FailIf(releaseVisits == 0 || _entities.InitializedInteractionUpdatesAllowed,
                "Later interaction mask release must retain this pass's sampled ordinary admission result.");
            releaseDuringPass = false; Step(1);
            openTextDuringPass = true; Step(1);
            openTextDuringPass = false; Step(2);
            _dialogue.Close(); native[0xcba0] = 0;
            for (int limit = 0; fleeing.Any(actor => actor.Actor.Active) && limit < 220; limit += 2) Step(2);
            Step(2);
            if (names.Contains("Monkey"))
                FailIf(_runtimeState.ReadWramByte(AgesWramAddress.wLoadedTreeGfxIndex) != native[0xcc18] || native[0xcc18] != 1,
                    "The native fleeing Monkey must restore loaded-tree graphics index1 on deletion.");
            FailIf(!held || moves < 10 || fleeing.Any(actor => actor.Actor.Active) || names.Any(name => native[addresses[name]] != 0),
                "Audience must execute nonzero source flight, retire at the original boundary and stay retired.");
            owner.Cancel(); _entities.Clear();
        }
    }
}
