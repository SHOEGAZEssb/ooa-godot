using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateZeldaKidnappedRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(true); _inventory.GiveTreasure(TreasureId.Essence, 2);
            _inventory.EquipA(0); _inventory.EquipB(0);
            LoadValidationRoom(0, 0x65);
            _player.WarpTo(new(0x48, 0x78)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position), "0:65 Impa approach must begin in the real south aisle.");
            var rom = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 0x48, 0x78)
                { CompanionDispatchEnabled = true };
            rom[0xcc01] = 1; rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            foreach (var npc in _entities.Entities<NpcCharacter>())
            {
                int at = 0xd040 + 0x100 * _entities.InteractionSlot(npc);
                rom[at]=1; rom[at+1]=(byte)npc.Record.Id; rom[at+2]=(byte)npc.Record.SubId;
                rom[at+11]=(byte)npc.Record.Y; rom[at+13]=(byte)npc.Record.X;
            }
            int generation=0, texts=0, updates=0;
            int[] expected=[0x0127,0x0129,0x3216,0x3216];
            var trace=new ValidationCutsceneTrace(); _roomEvents.CommandTraceSink=trace;
            void Step(int count, Vector2 movement, bool press=false)
            {
                int edge=press?1:0;
                string[] held=press?["attack"]:movement==Vector2.Up?["move_up"]:movement==Vector2.Down?["move_down"]:movement==Vector2.Left?["move_left"]:movement==Vector2.Right?["move_right"]:[];
                int mask=press?1:movement==Vector2.Up?0x40:movement==Vector2.Down?0x80:movement==Vector2.Left?0x20:movement==Vector2.Right?0x10:0;
                int angle=movement==Vector2.Up?0:movement==Vector2.Down?16:movement==Vector2.Left?24:movement==Vector2.Right?8:0xff;
                StepGameplayUpdates(count,movement,held,press?["attack"]:[],batched,()=>
                {
                    rom.UpdateGameplay(edge,mask,angle,++updates); edge=0;
                    foreach(var npc in _entities.Entities<NpcCharacter>().Where(n=>n.Active))
                    {
                        int at=0xd040+0x100*_entities.InteractionSlot(npc);
                        if ((updates & 31) == 1) CompareInteractionOamRom(rom, npc, at, $"0:65 kidnapped ${npc.Record.Id:x2} update{updates}");
                        FailIf(rom[at]==0 || npc.Position!=new Vector2(rom[at+13],rom[at+11]) ||
                            (int)npc.ScriptDrawOffset.Y!=unchecked((sbyte)rom[at+15]) ||
                            npc.CurrentAnimationCounter!=rom[at+0x20] || npc.ZIndex!=ObjectDrawPriority.FromVisible(rom[at+0x1a]),
                            $"0:65 kidnapped ${npc.Record.Id:x2} update{updates} differs: XY/Z={npc.Position}/{npc.ScriptDrawOffset.Y}, ROM=({rom[at+13]},{rom[at+11]})/{unchecked((sbyte)rom[at+15])}; anim={npc.CurrentAnimationCounter}/{rom[at+0x20]}, priority={npc.ZIndex}/{ObjectDrawPriority.FromVisible(rom[at+0x1a])}; {string.Join(';',trace.Entries.TakeLast(5))}.");
                    }
                    FailIf(_player.PrecisePosition!=new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f) ||
                        _entities.RuntimeState.ReadWramByte(AgesWramAddress.wDisabledObjects)!=rom[0xcc8a] ||
                        _entities.RuntimeState.ReadWramByte(AgesWramAddress.wMenuDisabled)!=rom[0xcc02],
                        $"0:65 kidnapping player/masks update{updates} differs: Link={_player.PrecisePosition}/({rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}), masks={_entities.RuntimeState.ReadWramByte(AgesWramAddress.wDisabledObjects)}/{rom[0xcc8a]}.");
                    if(rom.TextGeneration!=generation)
                    {
                        FailIf(texts>=expected.Length || rom.Word(0xcba2)!=expected[texts]+0x400 || !_dialogue.IsOpen,
                            $"0:65 conversation {texts} at {updates} lost its native text: ROM=${rom.Word(0xcba2):x4}, port={_dialogue.CurrentMessage}.");
                        generation=rom.TextGeneration; texts++;
                    }
                });
            }
            void Close() { Step(5,Vector2.Zero); _dialogue.Close(); rom[0xcba0]=0; Step(1,Vector2.Zero); }
            void Walk(Vector2 destination)
            {
                for(int axis=0;axis<2;axis++)
                {
                    int limit=100;
                    while(Mathf.Abs(axis==0?_player.Position.X-destination.X:_player.Position.Y-destination.Y)>1)
                    {
                        FailIf(--limit==0,$"0:65 real collision approach blocked at {_player.Position} toward {destination}.");
                        Step(1,axis==0?new(Mathf.Sign(destination.X-_player.Position.X),0):new(0,Mathf.Sign(destination.Y-_player.Position.Y)));
                    }
                }
                Step(1,Vector2.Zero);
                FailIf(_collision.Collides(_player.Position)||_entities.BlocksLink(_player.Position),"0:65 approach entered solid NPC/terrain geometry.");
            }
            Step(3,Vector2.Zero); Walk(new(0x48,0x75));
            Step(1,Vector2.Zero,true); FailIf(texts!=1,"0:65 Impa must be reachable from south.");
            Close(); Step(40,Vector2.Zero);
            FailIf(!_saveData.HasGlobalFlag(0x39) || _saveData.HasGlobalFlag(0x38) ||
                _roomEvents.MenusDisabled || _entities.Entities<NpcCharacter>().Single(n=>n.Record.Id==0x31).Position!=new Vector2(0x39,0x68) ||
                _entities.Entities<NpcCharacter>().Single(n=>n.Record.Id==0x4c).Position!=new Vector2(0x68,0x68),
                $"0:65 first conversation must move Impa and bird aside, persist only $39 and restore control: flags={_saveData.HasGlobalFlag(0x39)}/{_saveData.HasGlobalFlag(0x38)}, menus={_roomEvents.MenusDisabled}, positions={string.Join(';',_entities.Entities<NpcCharacter>().Select(n=>n.Position))}; {string.Join(';',trace.Entries.TakeLast(5))}.");
            Walk(new(0x48,0x58)); Walk(new(0x38,0x5b)); Step(1,Vector2.Zero,true); FailIf(texts!=2,"0:65 moved Impa must offer repeat dialogue."); Close();
            Walk(new(0x38,0x58)); Walk(new(0x68,0x5b)); Step(2,Vector2.Zero,true); FailIf(texts!=3,"0:65 bird must show text after its setanimation yield."); Close();
            Step(2,Vector2.Zero,true); FailIf(texts!=4,"0:65 bird must be repeatable after completing its text."); Close();
            LoadValidationRoom(0,0x65); StepGameplayUpdates(3,Vector2.Zero,batched:batched);
            FailIf(_entities.Entities<NpcCharacter>().Single(n=>n.Record.Id==0x31).Position!=new Vector2(0x38,0x68) ||
                _entities.Entities<NpcCharacter>().Single(n=>n.Record.Id==0x4c).Position!=new Vector2(0x68,0x68),
                "0:65 moved positions did not persist across re-entry.");
        }
        GD.Print("Validated clean-US room $0:$65 real collision approach, first/repeat Impa and bird conversations, ordered movement, text gates, animation, masks and re-entry in split/batched gameplay.");
    }
}
