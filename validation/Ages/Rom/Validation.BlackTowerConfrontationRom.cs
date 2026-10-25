using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateBlackTowerConfrontationRom()
    {
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xfe); _inventory.EquipA(0); _inventory.EquipB(0);
            _player.WarpTo(new(0x78, 0x88)); _player.Face(Vector2I.Up); _transitions.ResetCamera();
            FailIf(_collision.Collides(_player.Position), "Ralph $37:$0c scene requires reachable floor in $4:fe.");
            var rom = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 0x78, 0x88)
                { CompanionDispatchEnabled = true };
            rom.InitializeLinkGameplay(); rom[0xcd16] = 0; rom[0xcd17] = 0xd0; rom[0xffaa] = 48; rom[0xffac] = 40;
            rom[0xd240] = 1; rom[0xd241] = 0x37; rom[0xd242] = 0x0c; rom[0xd24b] = 0x58; rom[0xd24d] = 0x78;
            int update = 0, generation = 0, hold = 0;
            var texts = new List<int>();
            var sounds = _sound.AttachPlayRequestAudit();
            bool sawSword = false, sawJump = false, sawAmbi = false;
            void Compare()
            {
                rom.AdvanceWarpPalette(); rom.UpdateGameplay(0, 0, 0xff, ++update); rom.AdvanceCameraPosition();
                var slots = SomariaPrivate<Dictionary<IRoomEntity, int>>(_entities, "_interactionSlots");
                foreach (var actor in _entities.EntityAdapters<BlackTowerConfrontationActor>())
                {
                    int address = 0xd040 + slots[actor] * 256;
                    FailIf(rom[address] == 0 || actor.Id != rom[address + 1] || actor.State != rom[address + 4] ||
                        actor.PrecisePosition != new Vector2(rom.Word(address + 12) / 256f, rom.Word(address + 10) / 256f) ||
                        actor.ZFixed != unchecked((short)rom.Word(address + 14)) || actor.Node.Visible != ((rom[address + 0x1a] & 0x80) != 0),
                        $"Black Tower INTERAC ${actor.Id:x2} update {update}: port={actor.State}/{actor.PrecisePosition}/Z{actor.ZFixed}/{actor.Node.Visible}, " +
                        $"ROM ${address:x4}={rom[address + 4]}/{rom.Word(address + 12) / 256f},{rom.Word(address + 10) / 256f}/Z{unchecked((short)rom.Word(address + 14))}/${rom[address + 0x1a]:x2}.");
                    sawJump |= actor.Id == 0x37 && actor.ZFixed != 0;
                    sawAmbi |= actor.Id == 0x4d;
                }
                foreach (var sword in _entities.EntityAdapters<LinkedSwordRoomEntity>())
                {
                    int address = 0xd040 + slots[sword] * 256;
                    FailIf(sword.Node.Visible != ((rom[address + 0x1a] & 0x80) != 0), $"Ralph linked sword $5e visibility differs update {update}.");
                    sawSword |= sword.Node.Visible;
                }
                foreach (int address in new[] { 0xcfc0, 0xcfde, 0xcfdf, AgesWramAddress.wDisabledObjects, AgesWramAddress.wMenuDisabled })
                    FailIf(_entities.RuntimeState.ReadWramByte(address) != rom[address],
                        $"Ralph/Ambi shared byte ${address:x4} update {update}: port=${_entities.RuntimeState.ReadWramByte(address):x2}, ROM=${rom[address]:x2}.");
                FailIf(_player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f) ||
                    _player.InvincibilityFrames != unchecked((sbyte)rom[0xd02b]) ||
                    _transitions.NativePaletteFadeActive != (rom[0xc4ab] != 0),
                    $"Ralph disabled-input state update {update}: Link={_player.PrecisePosition}/" +
                    $"{rom.Word(0xd00c) / 256f},{rom.Word(0xd00a) / 256f}, invincibility={_player.InvincibilityFrames}/" +
                    $"{unchecked((sbyte)rom[0xd02b])}, palette={_transitions.NativePaletteFadeActive}/${rom[0xc4ab]:x2}.");
                FailIf(_saveData.HasRoomFlag(4, 0xfe, 0x40) != ((rom[0xc9fe] & 0x40) != 0),
                    $"Ralph room $4:fe completion flag differs update {update}.");
                FailIf(!sounds.Requests.Where(sound => sound >= 0x4c).SequenceEqual(rom.Sounds.Where(sound => sound >= 0x4c)),
                    $"Ralph/Ambi ordered sound requests differ update {update}.");
                if (rom.TextGeneration != generation)
                {
                    int[] expected = [0x1713, 0x2e1b, 0x1714];
                    FailIf(texts.Count >= expected.Length || rom.Word(0xcba2) != expected[texts.Count] || !_dialogue.IsOpen,
                        $"Ralph confrontation text {texts.Count} differs: native=${rom.Word(0xcba2):x4}.");
                    if (texts.Count == 0)
                        FailIf(!_dialogue.CurrentMessage.Replace('\n', ' ').Contains("If you slay me, you will vanish from this world!", System.StringComparison.Ordinal),
                            "TX_1313 must retain Veran's full ancestor warning through the imported page boundaries.");
                    texts.Add(rom.Word(0xcba2)); generation = rom.TextGeneration; hold = 3;
                }
                if (hold > 0 && --hold == 0) { _dialogue.Close(); rom[0xcba0] = 0; }
            }
            for (int bound = 0; bound < 1600 && (texts.Count < 3 || _entities.PlayerMenusDisabled); bound += 2)
                StepGameplayUpdates(2, Vector2.Zero, batched: batch, afterUpdate: Compare);
            FailIf(texts.Count != 3 || !sawSword || !sawAmbi || !sawJump || !_saveData.HasRoomFlag(4, 0xfe, 0x40) || _entities.PlayerMenusDisabled,
                "Ralph's confrontation must show three source texts, animate his sword/jump, remove Ambi and release control in $4:fe.");
            var ralph = _entities.EntityAdapters<BlackTowerConfrontationActor>().Single(actor => actor.Id == 0x37);
            FailIf(ralph.Node.Position != new Vector2(0x60, 0x58), "Defeated Ralph must remain at source $58/$60.");
            FailIf(((NpcCharacter)ralph.Node).TimeWarpSolidPosition != new Vector2(0x78, 0x58),
                "Ralph's initial objectMarkSolidPosition reservation must survive script movement through the confrontation.");
            _player.WarpTo(new(0x60, 0x78));
            FailIf(_collision.Collides(_player.Position), "Ralph repeat conversation must approach through $4:fe's actual floor.");
            StepGameplayUpdates(24, Vector2.Up, batched: batch);
            foreach (int id in new[] { 0x2a1c, 0x2a1d })
            {
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
                for (int wait = 0; !_dialogue.IsOpen && wait < 4; wait++) StepGameplayUpdates(1, Vector2.Zero);
                FailIf(!_dialogue.IsOpen || _dialogue.SourceTextId != id,
                    $"Collapsed Ralph must offer reachable source text TX_{id:x4} after the confrontation.");
                _dialogue.Close(); StepGameplayUpdates(34, Vector2.Zero, batched: batch);
            }
            LoadValidationRoom(4, 0xfd); LoadValidationRoom(4, 0xfe); StepGameplayUpdates(4, Vector2.Zero);
            FailIf(_entities.EntityAdapters<BlackTowerConfrontationActor>().Any(actor => actor.Id == 0x4d) || _entities.PlayerMenusDisabled ||
                _entities.EntityAdapters<BlackTowerConfrontationActor>().Single().Node.Position != new Vector2(0x60, 0x58),
                "Completed $4:fe must retain collapsed Ralph and skip Ambi's confrontation on re-entry.");
            FailIf(((NpcCharacter)_entities.EntityAdapters<BlackTowerConfrontationActor>().Single().Node).TimeWarpSolidPosition != new Vector2(0x60, 0x58),
                "Re-entering completed $4:fe must mark Ralph's already-selected collapsed position during state zero.");
        }
    }

    private void ValidateBlackTowerConfrontationCancellation()
    {
        foreach (int cancelAt in new[] { 1, 120, 260, 500 })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4, 0xfe); _player.WarpTo(new(0x78, 0x88));
            for (int update = 0; update < cancelAt; update++)
            { StepGameplayUpdates(1, Vector2.Zero); if (_dialogue.IsOpen) _dialogue.Close(); }
            _roomEvents.Get<BlackTowerConfrontationEvent>().Cancel();
            FailIf(_entities.EntityAdapters<BlackTowerConfrontationActor>().Any() || _entities.EntityAdapters<LinkedSwordRoomEntity>().Any() ||
                _entities.PlayerMenusDisabled || _player.CutsceneControlled || _transitions.NativePaletteFadeActive,
                $"Canceling Ralph $37:$0c update {cancelAt} retained a script actor, mask, or fade.");
        }
        ReinitializeGameplayForValidation(); _saveData.SetRoomFlag(4, 0xfc, 0x80); LoadValidationRoom(4, 0xfe);
        StepGameplayUpdates(2, Vector2.Zero);
        FailIf(_entities.EntityAdapters<BlackTowerConfrontationActor>().Any() || _entities.EntityAdapters<LinkedSwordRoomEntity>().Any(),
            "Veran's room $4:fc flag $80 must suppress Ralph $37:$0c before allocating a sword or starting its script.");
    }
}
