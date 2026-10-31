using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedOpeningEffects(FrontendValidationRoot root,bool batched)
    {
        var rooms=root._gameplaySession!.Rooms;
        var entities=root._seasonsOpening?.Entities??root._entities;
        var player=root._scene.Player;
        var memory=rooms.RuntimeState;
        var app=new ApplicationValidationFixture(root);
        try
        {
            player.BeginCutsceneControl();
            foreach(int id in new[]{5,8})
            foreach(string mode in new[]{"normal","text","freeze"})
            {
                entities.Clear();
                var sounds=root._sound.AttachPlayRequestAudit();
                int rng=entities.Random.Calls;
                RoomEntitySpawn request=id==5?new PuzzlePuffSpawn(new(72,64),SoundId.SndPoof):new KillEnemyPuffSpawn(new(72,64));
                var actor=entities.TryCreateInteractionEntity(request)!;
                var life=(IRoomEntityLifetime)actor;
                int at=0xd040+entities.NativeInteractionSlot(actor)*256;
                var native=new BraceletRom(rooms.CurrentRoom,player.PrecisePosition,0,player.Inventory.ActiveRing,game:rooms.SaveData.Game);
                native.LoadSave(rooms.SaveData);native[0xcc2d]=(byte)rooms.ActiveGroup;
                native[at]=1;native[at+1]=(byte)id;native[at+0xb]=64;native[at+0xd]=72;
                if(mode=="text") {root._scene.Dialogue.ShowMessage("Common stationary effect eligibility.",104);native[0xcba0]=1;}
                if(mode=="freeze") {memory.SetField("wDisabledObjects",1);native.SetNativeByte(memory.FieldAddress("wDisabledObjects"),1);}
                FailIf(actor.Node.Visible || sounds.Requests.Any(sound=>sound!=SoundId.SndText),
                    "Stationary interaction effects must defer visibility and cue to their physical state-zero visit.");
                int updates=0;
                while(!life.Finished && updates<64)
                    app.Step(1,Vector2.Zero,batched:batched,afterUpdate:()=>
                    {
                        native.AdvanceInteractions(entities.FrameCounter);updates++;
                        bool alive=!life.Finished;
                        int parameter=actor.Node is KillEnemyPuffEffect kill?kill.AnimationParameter:((StationaryDebrisEffect)actor.Node).CurrentParameter;
                        FailIf(alive!=(native[at]!=0) || alive &&
                            (actor.Node.Visible!=((native[at+0x1a]&128)!=0) || parameter!=native[at+0x21]) ||
                            !sounds.Requests.Where(sound=>sound!=SoundId.SndText).SequenceEqual(native.Sounds) || entities.Random.Calls!=rng ||
                            entities.RoomEnemyCount!=0 || entities.Entities<ItemDropEffect>().Count!=0,
                            $"{rooms.SaveData.Game}/INTERAC${id:x2}/{mode}/batch{batched}/update{updates}: alive{alive}/{native[at]!=0}, visible{(alive?actor.Node.Visible:false)}/{(native[at+0x1a]&128)!=0}, parameter{parameter}/{native[at+0x21]}, cues[{string.Join(',',sounds.Requests)}]/[{string.Join(',',native.Sounds)}], RNG{entities.Random.Calls}/{rng}, enemies{entities.RoomEnemyCount}, drops{entities.Entities<ItemDropEffect>().Count}.");
                        if(id==8 && updates==1)
                        {
                            var row=GeneratedTable.Load(rooms.AssetRoot+"effects/kill_enemy_puff.tsv",
                                new("kill puff native palette",GeneratedTableKeySemantics.Ordered,
                                    ["tile-base","palette","animation"],headerRequired:true)).SingleRow();
                            FailIf(row.UnsignedDecimal(1)!=(native[at+0x1b]&7),
                                "INTERAC $08 palette comes from interactionData's upper nibble, retaining native palette3.");
                        }
                    });
                FailIf(!life.Finished || id==8 && updates!=22,
                    "Stationary effects must delete after their native terminal check; INTERAC $08 lasts22 visits including state0.");
                root._scene.Dialogue.Close();memory.SetField("wDisabledObjects",0);
            }
        }
        finally {player.EndCutsceneControl();root._scene.Dialogue.Close();memory.SetField("wDisabledObjects",0);entities.Clear();}
    }
}
