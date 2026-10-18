using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePartInitializationGatesRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (string gate in new[] { "text", "mask08", "mask80" })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4, 0xa1); _entities.Clear();
            _inventory.SetScriptedEquippedItems(0, 0); _player.WarpTo(new(120, 40)); _player.BeginCutsceneControl();
            var fire = _entities.Spawn<ZoraFireProjectile>(new ZoraFireSpawn(new(64, 72), 0x19));
            var beam = _entities.Spawn<BeamosBeamPart>(new BeamosBeamSpawn(new(80, 72), 8, 0));
            var bone = _entities.Spawn<StalfosBoneProjectile>(new StalfosBoneSpawn(new(88, 72)));
            var boulder = new FallingBoulder(0, new(96, 72), new FallingBoulderDatabase(), _random, _sound.PlaySound, _currentRoom, _entities.ToScreen);
            _entities.AddEntity(new FallingBoulderRoomEntity(boulder));
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 120, 40);
            int[] ids = [0x19, 0x29, 0x1c, 0x45]; int[] xs = [64, 80, 88, 96];
            for (int index = 0; index < ids.Length; index++)
            {
                int at = 0xd0c0 + index * 256;
                native[at] = 1; native[at + 1] = (byte)ids[index];
                native[at + 11] = 72; native[at + 13] = (byte)xs[index]; native[at + 9] = 8;
            }
            var previousText = _entities.TextActiveSource; bool held = true;
            _entities.TextActiveSource = () => held && gate == "text";
            void Gates()
            {
                native[0xcc8a] = held && gate == "mask08" ? (byte)8 : held && gate == "mask80" ? (byte)0x80 : (byte)0;
                native[0xcba0] = held && gate == "text" ? (byte)1 : (byte)0;
                _runtimeState.SetWramByte(WramAddress.wDisabledObjects, native[0xcc8a]);
            }
            try
            {
                Gates();
                StepGameplayUpdates(3, Vector2.Zero, batched: batched, afterUpdate: () =>
                {
                    native.AdvanceParts(_entities.FrameCounter);
                    FailIf(fire.State != 1 || beam.State != 1 || bone.State != HostileProjectileState.Flying || boulder.State != 1 ||
                        native[0xd0c4] != 1 || native[0xd1c4] != 1 || native[0xd2c4] != 1 || native[0xd3c4] != 1 ||
                        fire.Counter != 8 || native[0xd0c6] != 8 || beam.Counter != 2 || native[0xd1c6] != 2 ||
                        boulder.Counter != 0x2d || native[0xd3c6] != 0x2d,
                        $"PART $19/$29/$1c/$45 must initialize once, then freeze state/counters under {gate}.");
                });
                held = false; Gates();
                StepGameplayUpdates(1, Vector2.Zero, batched: batched); native.AdvanceParts(_entities.FrameCounter);
                FailIf(fire.Counter != 7 || native[0xd0c6] != 7 || beam.Counter != 1 || native[0xd1c6] != 1 ||
                    boulder.Counter != 0x2c || native[0xd3c6] != 0x2c,
                    "The first unrestricted PART pass must resume each source counter exactly once.");
                held = true; Gates();
                StepGameplayUpdates(2, Vector2.Zero, batched: batched, afterUpdate: () => native.AdvanceParts(_entities.FrameCounter));
                FailIf(fire.Counter != 7 || beam.Counter != 1 || boulder.Counter != 0x2c,
                    "Reapplying a restricted PART gate must preserve initialized counters.");
            }
            finally { _entities.TextActiveSource = previousText; _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0); }
            _entities.Clear();
        }
        ReinitializeGameplayForValidation();
    }
}
