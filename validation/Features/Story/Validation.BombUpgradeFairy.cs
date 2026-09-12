using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom050BombUpgradeFairy()
    {
        var fairy = _roomEvents.Get<BombUpgradeFairyEvent>();
        var data = fairy.Database;
        byte Signal() => _entities.RuntimeState.ReadWramByte(0xcfc0);
        NpcCharacter[] Effects(int id, int subid) => _entities.Entities<NpcCharacter>()
            .Where(n => n.Active && n.Record.Id == id && n.Record.SubId == subid).ToArray();
        // These expectations come from scriptHelper.s and text/ages/text.yaml,
        // independently of the imported rows consumed by the event.
        string[] expectedTexts =
        [
            "Did you drop a\n\\col(1)Golden Bomb\\col(0)? Or\na \\col(1)Silver Bomb\\col(0)?\\stop\n \\opt()Golden \\opt()Silver\n \\opt()A regular one",
            "...Really?\nA \\col(1)Golden Bomb\\col(0)?\n  \\opt()Yes \\opt()No",
            "Really and truly\na \\col(1)Golden Bomb\\col(0)???\n  \\opt()Yes \\opt()No",
            "Liar! Greed\nbegets sorrow!",
            "I hope you've\nlearned not to\nlie.\\heart",
            "...Really?\nA \\col(1)Silver Bomb\\col(0)?\n  \\opt()Yes \\opt()No",
            "Liar! I'm\nconfiscating\nyour \\col(1)Bombs\\col(0)!",
            "You are an\nhonest person.\\stop\nI'll increase\nthe number of\n\\col(1)Bombs\\col(0) you can\ncarry to reward\nyou.\\stop\nHee-yah!",
            "You can now\ncarry \\num1 \\col(1)Bombs\\col(0).\nAnd I've filled\nyour bag.\nFarewell..."
        ];
        foreach (CutsceneShowTextCommand command in data.Commands.OfType<CutsceneShowTextCommand>())
            FailIf(command.Message != expectedTexts[command.TextId - 0x0c00] || command.TextboxPosition is not null,
                $"$83 TX_{command.TextId:x4} lost source text, page/color/choice commands or automatic position.");
        FailIf(data.Group != 0 || data.Room != 0x50 || data.Capacity(0x10) != 0x30 || data.Capacity(0x30) != 0x50 ||
            data.Commands[2] is not CutsceneMemoryJumpTableYieldCommand { TargetCommands.Count: 3 } ||
            !data.Bombs.Select(b => b.Delay).SequenceEqual([1,15,30,45]) || data.SourceOffset("sparkle") != 0x1c00 ||
            data.SourceGrayscaleInverted("silver") || !data.SourceGrayscaleInverted("bomb"),
            "$83 source room, capacity selection, yielding choice table, bomb delays, grayscale encoding or $84 graphics offset changed.");
        foreach (Vector2 point in new[] { new Vector2(0x58,0x41), new Vector2(0x78,0x46) })
            FailIf(!data.Contains(point), "$83 excluded an inclusive source trigger boundary.");
        foreach (Vector2 point in new[] { new Vector2(0x57,0x41), new Vector2(0x79,0x46), new Vector2(0x68,0x40), new Vector2(0x68,0x47) })
            FailIf(data.Contains(point), "$83 accepted a point outside its unsigned trigger rectangle.");

        // Independently decoded from source tile pair $0e and invert:false in
        // spr_moblinflag_bomb_portal.properties, OAM $08,$04,$00,$00, OBJ3 and
        // PALH_80's OBJ6 (paletteData46a8). Var03=0 is gold at x=$5a;
        // var03=1 is silver at x=$76. Both contain 63 opaque pixels.
        void CheckOfferedBombs()
        {
            foreach (NpcCharacter display in Effects(0x83, 2))
            {
                bool silver = display.Record.Var03 == 1;
                FailIf(display.Position != new Vector2(silver ? 0x76 : 0x5a, 0x3c) ||
                    display.Record.Palette != (silver ? 6 : 3) ||
                    display.CurrentAnimationTextureSize != new Vector2I(8, 16) ||
                    display.CurrentAnimationOffset != new Vector2(-4, -8) ||
                    display.CurrentAnimationOpaquePixels != 63 ||
                    display.CurrentAnimationPixelHash != (silver ? 0x69e3080356352bc8UL : 0xe0a23cfb5336b730UL),
                    $"$83:$02 var03=${display.Record.Var03:x2} lost its source bomb pixels, palette or OAM bounds: hash=${display.CurrentAnimationPixelHash:x16}.");
            }
        }
        // spr_common_items tile pair $10 keeps its ordinary inverted encoding.
        // OBJ4/5 flash changes only the palette; its 67 opaque pixels stay fixed.
        void CheckSurroundingBomb(NpcCharacter bomb, int palette)
        {
            FailIf(bomb.Record.Palette != palette ||
                bomb.CurrentAnimationTextureSize != new Vector2I(8, 16) ||
                bomb.CurrentAnimationOffset != new Vector2(-4, -8) ||
                bomb.CurrentAnimationOpaquePixels != 67 ||
                bomb.CurrentAnimationPixelHash != (palette == 4 ? 0x1126c3f46acd3993UL : 0x2daa5bb3fbf038acUL),
                $"$83:$01 var03=${bomb.Record.Var03:x2} lost bomb pixels or OBJ{palette} flash: hash=${bomb.CurrentAnimationPixelHash:x16}.");
        }

        bool batched = false;
        void Step(int updates, Vector2 movement = default, string? press = null) =>
            StepGameplayUpdates(updates, movement, press is null ? [] : [press], press is null ? [] : [press], batched);
        void Load(bool wet = true, bool obtained = false, int capacity = 0x10, int health = 12)
        {
            _dialogue.Close();
            _saveData.SetRoomFlag(0, 0x50, 1, wet);
            _saveData.SetGlobalFlag(data.GlobalFlag, obtained);
            _inventory.ApplyFairyBombCapacityUpgrade(capacity);
            _inventory.RefillHealth();
            _inventory.ApplyDamage(_inventory.HealthQuarters - health);
            _inventory.EquipB(InventoryState.ItemBomb);
            LoadValidationRoom(0, 0x50);
            _player.WarpTo(new Vector2(0x88, 0x68));
            FailIf(_currentRoom.IsSolid(_player.Position) || _currentRoom.GetTerrainInfo(_player.Position).Hazard != HazardType.None,
                "Room 0:50 approach must start on the actual dry path: " + string.Join("/", Enumerable.Range(0,8).Select(y =>
                    string.Concat(Enumerable.Range(0,10).Select(x => _currentRoom.IsSolid(new Vector2(x*16+8,y*16+8)) ? '#' :
                        _currentRoom.GetTerrainInfo(new Vector2(x*16+8,y*16+8)).Hazard != HazardType.None ? '~' : '.')))));
        }
        void Approach()
        {
            // Pass below the sign at tile $57, then walk up the center channel.
            for (int i = 0; i < 50 && _player.Position.X > 0x68; i++) Step(1, Vector2.Left);
            for (int i = 0; i < 60 && _player.Position.Y > 0x46; i++) Step(1, Vector2.Up);
            FailIf(_player.Position.Y > 0x46 || _player.Position.X > 0x68 || _currentRoom.IsSolid(_player.Position),
                $"Room 0:50 pond is unreachable through its geometry: Link={_player.Position}. " + string.Join("/", Enumerable.Range(0,8).Select(y =>
                    string.Concat(Enumerable.Range(0,10).Select(x => _currentRoom.IsSolid(new Vector2(x*16+8,y*16+8)) ? '#' :
                        _currentRoom.GetTerrainInfo(new Vector2(x*16+8,y*16+8)).Hazard != HazardType.None ? '~' : '.')))));
            Step(1);
        }
        BombEffect Throw()
        {
            Step(1, press: "item");
            for (int i = 0; i < 20 && _bomb.State != BombParentState.Holding; i++) Step(1);
            FailIf(_bomb.State != BombParentState.Holding, $"$83 could not lift a bomb from {_player.Position}; state={_bomb.State}.");
            BombEffect bomb = _bomb.Bomb!;
            Step(1, Vector2.Up, "item");
            FailIf(bomb.State != BombState.Thrown, "$83 bomb did not enter the ordinary throw path.");
            return bomb;
        }
        void Start()
        {
            Approach();
            FailIf(fairy.State != 1 || Signal() != 0 || Effects(0x83,0).Single().Visible,
                "$83 must remain hidden and wait for a bomb at the pond.");
            BombEffect bomb = Throw();
            for (int i = 0; i < 65 && fairy.State == 1; i++) Step(1);
            FailIf(fairy.State != 2 || !bomb.Finished || !fairy.BlocksGameplay || Signal() != 0 ||
                !_player.CutsceneControlled || _player.FacingVector != Vector2I.Up,
                $"Room 0:50 ordinary bomb hazard failed to hand control to $83: state={fairy.State}, Link={_player.Position}, signal={Signal()}, bomb={bomb.State}.");
            Vector2 position = _player.Position;
            Step(18, Vector2.Down);
            FailIf(fairy.State != 2 || _player.Position != position || !fairy.MenusDisabled,
                "$83 revealed before its PUFF02 terminal parameter or released input early.");
            Step(1);
            FailIf(fairy.State != 3 || !Effects(0x83,0).Single().Visible || Effects(0x83,2).Length != 2,
                "$83 did not reveal on the update after PUFF02's 6+8+4 animation updates.");
            Step(1);
            FailIf(Effects(0x83,2).Any(n => !n.Visible) || fairy.ScriptCounter != 30,
                "$83:$02 null relatedObj1 must reveal without waiting for its own puff, while the script installs wait 30.");
            Step(29);
            FailIf(_dialogue.IsOpen || fairy.ScriptCounter != 1, "$83 initial dialogue opened before wait 30 expired.");
            Step(1);
            Expect(0x0c00);
            CheckOfferedBombs();
        }
        void Expect(int id)
        {
            string text = expectedTexts[id - 0x0c00];
            int bcd = _entities.RuntimeState.ReadWramByte(0xcba8);
            text = text.Replace("\\num1", ((bcd >> 4) * 10 + (bcd & 15)).ToString(), StringComparison.Ordinal);
            FailIf(!_dialogue.IsOpen || _dialogue.CurrentMessage != DialogueBox.PlainText(text),
                $"$83 expected TX_{id:x4} at script {fairy.ScriptIndex}, counter={fairy.ScriptCounter}; got '{_dialogue.CurrentMessage}'.");
        }
        void AwaitText(int id)
        {
            for (int i = 0; i < 1000 && !_dialogue.IsOpen; i++) Step(1);
            Expect(id);
        }
        void Choose(int option)
        {
            FailIf(!_dialogue.ChoiceActive, "$83 expected a live choice prompt.");
            _dialogue.SubmitChoiceForValidation(option);
        }
        void FinishText()
        {
            for (int i = 0; i < 150 && _dialogue.IsOpen; i++)
            {
                Step(30); Step(1, press: "attack"); Step(1);
            }
            FailIf(_dialogue.IsOpen, "$83 dialogue failed to close through ordinary A input.");
        }
        void Finish()
        {
            FinishText();
            for (int i = 0; i < 70 && fairy.HasState; i++) Step(1);
            FailIf(fairy.HasState || fairy.BlocksGameplay || fairy.MenusDisabled || _player.CutsceneControlled ||
                Signal() != 1 || Effects(0x83,0).Length != 0 || Effects(0x84,0x0e).Length != 0,
                "$83 completion left an actor, sparkle, shared freeze or input owner alive.");
            Vector2 position = _player.Position;
            Step(8, Vector2.Down);
            FailIf(_player.Position.Y <= position.Y, "$83 completion failed to restore movement.");
        }

        Load(wet: false);
        FailIf(fairy.HasState || Effects(0x83,0).Length != 0, "$83 spawned while the water flag was clear.");
        Load(obtained: true);
        FailIf(fairy.HasState || Effects(0x83,0).Length != 0, "$83 ignored its obtained-upgrade global flag.");
        Load(); Approach();
        BombEffect pending = Throw();
        _player.ApplyInteractionInvincibility(90);
        for (int i = 0; i < 65 && !pending.Finished; i++) Step(1);
        FailIf(!pending.Finished || Signal() != 1 || fairy.State != 1 || fairy.BlocksGameplay,
            $"$83 failed to retain a landed bomb signal while checkLinkVulnerable rejected invincibility: bomb={pending.State}, signal={Signal()}, state={fairy.State}, invincibility={_player.InvincibilityFrames}.");
        Step(1, press: "item"); Step(14);
        BombEffect carried = _bomb.Bomb!;
        FailIf(_bomb.State != BombParentState.Holding || carried is null, "$83 pending signal test failed to lift a second bomb.");
        for (int i = 0; i < 100 && fairy.State == 1; i++) Step(1);
        FailIf(fairy.State != 2 || Signal() != 0 || _bomb.Active || _player.IsCarryingObject ||
            carried!.State != BombState.Thrown, "$83 did not consume the pending signal and release the held child when vulnerability returned.");
        Vector2 frozenPosition = carried.Position;
        int frozenFuse = carried.ElapsedFrames;
        Step(10);
        FailIf(carried.Position != frozenPosition || carried.ElapsedFrames != frozenFuse,
            "$83 wDisabledObjects=$80 did not freeze the released ITEM_BOMB child.");
        LoadValidationRoom(0, 0x51);
        FailIf(fairy.HasState || fairy.BlocksGameplay || _player.CutsceneControlled,
            "$83 appearance cancellation leaked an update or input owner.");
        foreach (bool batch in new[] { false, true })
        {
            batched = batch;
            Load(); Start();
            // All three confirmation returns must preserve $cfd0=0 and both bombs.
            Choose(0); Step(1);
            FailIf(fairy.ScriptCounter != 0, "$83 jumptable_memoryaddress must yield before the selected wait.");
            Step(1); Step(59);
            FailIf(_dialogue.IsOpen || fairy.ScriptCounter != 1, "$83 golden confirmation lost its exact 60-update wait.");
            Step(1); Expect(0x0c01); Choose(1); AwaitText(0x0c00);
            Choose(0); AwaitText(0x0c01); Choose(0); AwaitText(0x0c02); Choose(1); AwaitText(0x0c00);
            Choose(1); AwaitText(0x0c05); Choose(1); AwaitText(0x0c00);
            FailIf(_entities.RuntimeState.ReadWramByte(0xcfd0) != 0 || Effects(0x83,2).Length != 2,
                "$83 confirmation cancellation consumed the displayed bombs.");
            CheckOfferedBombs();
            Choose(2); AwaitText(0x0c07);
            FailIf(Effects(0x83,2).Length != 0 || _inventory.MaxBombs != 0x10, "$83 honesty removed bombs or upgraded capacity at the wrong boundary.");
            FinishText();
            for (int i = 0; i < 90 && Effects(0x83,1).Length != 4; i++) Step(1);
            NpcCharacter[] bombs = Effects(0x83,1);
            FailIf(bombs.Length != 4 || !bombs.Select(n => n.Position - _player.Position).SequenceEqual(
                [new Vector2(0,-16), new Vector2(16,0), new Vector2(0,16), new Vector2(-16,0)]),
                "$83 helper lost descending var03 allocation order and source offsets.");
            foreach (NpcCharacter bomb in bombs) CheckSurroundingBomb(bomb, 4);
            Step(45);
            FailIf(bombs.Any(bomb => !bomb.Visible), "$83:$01 delayed bombs did not all appear by update $2d.");
            for (int i = 0; i < bombs.Length; i++) CheckSurroundingBomb(bombs[i], i == 0 ? 4 : 5);
            Step(4);
            for (int i = 0; i < bombs.Length; i++) CheckSurroundingBomb(bombs[i], i == 0 ? 5 : 4);
            AwaitText(0x0c08);
            FailIf(_inventory.MaxBombs != 0x30 || _inventory.Bombs != 0x30 || _inventory.HealthQuarters != 12 ||
                _saveData.HasGlobalFlag(data.GlobalFlag) || Effects(0x83,1).Length != 0,
                "$83 truthful branch failed its BCD refill, early flag boundary or bomb cleanup.");
            Finish();
            FailIf(!_saveData.HasGlobalFlag(data.GlobalFlag), "$83 did not persist completion after the final wait.");
            // Repeat the actual action in the same room, then on re-entry.
            Approach(); BombEffect repeated = Throw(); Step(65);
            FailIf(!repeated.Finished || fairy.HasState, "$83 replayed its reward without leaving the room.");
            Load(obtained: true, capacity: 0x30);
            FailIf(fairy.HasState, "$83 reward was not suppressed on room reload.");

            Load(capacity: 0x30); Start(); Choose(2); AwaitText(0x0c07); FinishText(); AwaitText(0x0c08);
            FailIf(_inventory.MaxBombs != 0x50 || _inventory.Bombs != 0x50, "$83 second capacity route failed to grant packed BCD $50.");
            Finish();
            foreach (int health in new[] { 12, 3 })
            {
                Load(health: health); Start(); Choose(0); AwaitText(0x0c01); Choose(0); AwaitText(0x0c02); Choose(0);
                AwaitText(0x0c03); FinishText(); AwaitText(0x0c04);
                FailIf(_inventory.HealthQuarters != Math.Min(health,4) || _inventory.MaxBombs != 0x10 || _inventory.Bombs != 9,
                    "$83 golden lie did not clamp health to $04 while retaining lower health and bomb inventory.");
                FailIf(_player.ScriptedLinkAnimationMode != (health >= 4 ? 0x02 : null) ||
                    health >= 4 && _player.ScriptedLinkAnimationPixelHash == 0,
                    "$83 golden penalty lost the source collapsed pose or ignored the below-one-heart early return.");
                Finish();
                FailIf(_saveData.HasGlobalFlag(data.GlobalFlag), "$83 punished a lie but consumed the permanent upgrade flag.");
            }
            Load(); Start(); Choose(1); AwaitText(0x0c05); Choose(0); AwaitText(0x0c06); FinishText(); AwaitText(0x0c04);
            FailIf(_inventory.Bombs != 0 || _inventory.MaxBombs != 0x10 || _inventory.HealthQuarters != 12,
                "$83 silver lie failed to confiscate only the bomb count.");
            Finish();
            Load(); Start(); Choose(2); AwaitText(0x0c07); FinishText();
            for (int i = 0; i < 250 && !fairy.Fading; i++) Step(1);
            FailIf(!fairy.Fading, "$83 cancellation test did not reach the palette thread.");
            LoadValidationRoom(0, 0x51);
            FailIf(fairy.HasState || fairy.BlocksGameplay || _player.CutsceneControlled ||
                _saveData.HasGlobalFlag(data.GlobalFlag), "$83 room cancellation retained cutscene state or granted an unearned upgrade.");
        }
        GD.Print("Validated room 0:50 bomb fairy: real pond approach/throw, source bomb pixels and palettes, complete source dialogue and all answers, confirmation returns, exact waits, BCD $30/$50 refill, penalties, replay suppression, cancellation and batched gameplay updates.");
    }
}
