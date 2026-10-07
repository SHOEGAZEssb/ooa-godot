using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateOctogonBubbleRom()
    {
        foreach (bool batched in new[] { false,true })
        foreach (bool mash in new[] { false,true })
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.Flippers,0); _inventory.GiveTreasure(TreasureId.MermaidSuit,0);
            _inventory.GiveTreasure(TreasureId.Sword,1);
            LoadValidationRoom(5,0x2d); _entities.Clear(); _inventory.EquipA(TreasureId.Sword); _inventory.EquipB(0);
            _player.WarpTo(new(0x78,0x98));
            FailIf(_collision.Collides(_player.Position),"Bubble capture fixture must retain Mermaid's Cave's actual lower floor.");
            var rom = new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,0x78,0x98) { HostilePartsEnabled = true };
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom[0xcc08] = 0xa5;
            int update = 0,pressed = 0;
            void Compare()
            {
                rom.UpdateGameplay(pressed,0,0xff,_entities.FrameCounter); pressed = 0;
                FailIf(_player.CollapsedActive != (rom[0xd004] == 0x14) ||
                    _player.CollapsePending != (rom[0xcc4f] == 0x14) ||
                    _player.CollapsedActive && (_player.CollapseSubstate != rom[0xd005] || _player.CollapseCounter != rom[0xd006]),
                    $"Octogon bubble update{update} batch={batched}, mash={mash}: runtime collapse={_player.CollapsedActive}/{_player.CollapsePending}, sub/c={_player.CollapseSubstate}/{_player.CollapseCounter}; ROM state=${rom[0xd004]:x2}, force=${rom[0xcc4f]:x2}, sub/c={rom[0xd005]}/{rom[0xd006]}.");
                var bubble = _entities.Entities<OctogonPart>().SingleOrDefault();
                if (bubble is not null)
                    FailIf(bubble.State != rom[0xd0c4] || bubble.Counter != rom[0xd0c6] ||
                        bubble.Position != new Vector2(rom.Word(0xd0cc)/256f,rom.Word(0xd0ca)/256f) ||
                        (bubble.ZFixed&0xffff) != rom.Word(0xd0ce) || bubble.CollisionEnabled != ((rom[0xd0e4]&128) != 0),
                        $"Octogon trapping bubble update{update}: state/Link attachment or collision lifetime differs from the ROM.");
                else FailIf(rom[0xd0c0] != 0,$"Octogon bubble update{update}: native bubble has not deleted.");
                update++;
            }
            void Step(int count = 1) => StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:Compare);
            Step(2);
            pressed = 1; StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],batched,Compare);
            FailIf(!_player.NativeItemUseActive || rom[0xcc5f] == 0,"Bubble fixture must begin with a real active sword parent.");
            FailIf(!_entities.TryCreatePart(new OctogonPartSpawn(_player.Position,0x55,0,-1)),"PART$55 fixture allocation failed.");
            rom[0xd0c0] = 1; rom[0xd0c1] = 0x55; rom[0xd0cb] = (byte)_player.Position.Y; rom[0xd0cd] = (byte)_player.Position.X;
            Step(4);
            FailIf(!_player.CollapsedActive || _player.CollapseCounter != 0xf0,"Bubble must capture through post-object contact, then initialize LINK_STATE_COLLAPSED with counter$f0.");
            FailIf(_player.IsAttacking || _player.NativeItemUseActive || rom[0xd600] != 0,
                "Collapse substate0 must cancel the active native sword parent.");
            int start = update;
            while (_player.CollapsedActive && update-start < 242)
            {
                if (mash && ((update-start)&1) == 0)
                { pressed = 1; StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],batched,Compare); }
                else Step();
            }
            FailIf(_player.CollapsedActive || update-start != (mash ? 97 : 241),
                "Collapse must release on byte subtraction borrow: 241 ordinary decrements, or alternating fresh-A four-count decrements.");
            Step(24);
            FailIf(_entities.Entities<OctogonPart>().Any(),"Released PART$55 must finish its burst animation and delete.");
        }
        GD.Print("Validated executed clean-US PART$55 post-object capture, forced Link/item handoff, exact $f0 borrow boundary, fresh-button acceleration, attached bubble and completion through split/batched gameplay.");
    }
}
