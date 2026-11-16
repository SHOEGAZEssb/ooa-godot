namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsSokra()
    {
        CheckSeasonsSokraVisibility();
        foreach(bool batched in new[]{false,true})
        foreach(int stage in new[]{-2,-1,-3,-4,0,1,2})
        {
            bool portal=stage==-2,summer=stage==-4;
            int room=portal?0x9b:summer?0x71:0xe9;
            var root=StartSeasonsAfterImpa(batched);
            try
            {
                var s=root._seasonsOpening!;var app=new ApplicationValidationFixture(root);
                s.Save.WriteField("wObtainedSeasons",(byte)(portal?0x88:stage>=0?0x80:0));
                if(stage>=0)s.Inventory.GiveTreasure(TreasureId.RodOfSeasons,7);
                s.Save.WriteField("wEssencesObtained",(byte)(stage>=1?2:0));
                s.Save.SetGlobalFlag(SeasonsGlobalFlag.SeasonAlwaysSpring,stage==2);
                s.Save.SetRoomFlag(0,room,0x40,stage==-3||stage>=0);
                s.Inventory.GiveTreasure(TreasureId.SeedSatchel,1);
                s.Memory.SetField("wRoomStateModifier",2);s.DebugTravel(0,room);
                s.Link.WarpTo(portal?new(24,44):summer?new(32,120):new(120,80));
                var sokra=s.Entities.EntityAdapters<SeasonsSokraRoomEntity>().Single();
                int page=(0xd0+s.Entities.NativeInteractionSlot(sokra))<<8;
                var native=new BraceletRom(s.Room,s.Link.PrecisePosition,2,0xff,game:OracleGame.Seasons);
                native.LoadSave(s.Save);native.InitializeWalkingAnimation();native[0xcc2d]=0;
                native.SetNativeByte(page+0x40,1);native.SetNativeByte(page+0x41,0x27);native.SetNativeByte(page+0x42,(byte)(portal?1:summer?2:0));
                native.SetNativeByte(page+0x4b,(byte)sokra.Npc.Position.Y);native.SetNativeByte(page+0x4d,(byte)sokra.Npc.Position.X);
                var sounds=s.Sound.AttachPlayRequestAudit();native.ClearSounds();
                int updates=0;var texts=new List<int>();
                void Compare()
                {
                    updates++;
                    // Declared Link handoff: compare the original interaction
                    // dispatcher, while the real application owns Link/text.
                    native.Word(0xd00a,(int)(s.Link.PrecisePosition.Y*256));native.Word(0xd00c,(int)(s.Link.PrecisePosition.X*256));
                    native.AdvanceInteractions(s.FrameCounter);
                    if(!sokra.Finished)
                    {
                        foreach(int offset in new[]{0x44,0x45,0x46,0x47,0x48,0x49,0x4a,0x4b,0x4c,0x4d,0x4e,0x4f,0x50,0x54,0x55,0x76,0x77})
                        {
                            // Summer's $76/$77 hold ROM return addresses; the typed runner owns that call stack.
                            if(summer&&offset is 0x76 or 0x77)continue;
                            FailIf(sokra.ReadByte(offset)!=native.ReadNativeByte(page+offset),
                                $"Sokra $27:${(portal?1:summer?2:0):x2} stage={stage}, batch={batched}, update={updates}, byte${offset:x2}: port=${sokra.ReadByte(offset):x2}, ROM=${native.ReadNativeByte(page+offset):x2}, command={sokra.Script?.CurrentCommandIndex}.");
                        }
                        FailIf(sokra.Npc.CurrentAnimationCounter!=native.ReadNativeByte(page+0x60)||sokra.Npc.CurrentAnimationParameter!=native.ReadNativeByte(page+0x61),
                            $"Sokra stage={stage}, update={updates}: native animation counter/parameter differs.");
                    }
                    FailIf((s.DisabledObjects!=0)!=(native.ReadNativeByte(s.Memory.FieldAddress("wDisabledObjects"))!=0),
                        $"Sokra stage={stage}, update={updates}: native input ownership differs.");
                    FailIf(sounds.Requests.Count(id=>id==SoundId.SndClink)!=native.Sounds.Count(id=>id==SoundId.SndClink),
                        $"Sokra stage={stage}, update={updates}: alert helper and script must preserve both native CLINK requests.");
                    if(stage>=0)
                    {
                        int notes=Enumerable.Range(2,14).Count(slot=>native.ReadNativeByte(0xd040+slot*256)!=0&&
                            native.ReadNativeByte(0xd041+slot*256)==0xa0&&native.ReadNativeByte(0xd042+slot*256)==1);
                        FailIf(s.Entities.EntityAdapters<FloatingMusicNoteRoomEntity>().Count(note=>!note.Finished&&((NpcCharacter)note.Node).Record.SubId==1)!=notes,
                            $"Sokra stage={stage}, update={updates}: native music-note allocation/lifetime differs.");
                    }
                    if(s.Dialogue.IsOpen)
                        // wTextIndex includes the native four-dictionary $0400 bias.
                        FailIf(s.Dialogue.SourceTextId!=(native.ReadNativeByte(0xcba2)|(native.ReadNativeByte(0xcba3)<<8))-0x400,
                            $"Sokra stage={stage}, update={updates}: text=${s.Dialogue.SourceTextId:x4}, ROM=${native.ReadNativeByte(0xcba3):x2}{native.ReadNativeByte(0xcba2):x2}.");
                }
                bool Complete()=>portal?sokra.Finished:texts.Count>=(summer?2:1)&&s.DisabledObjects==0&&!s.Dialogue.IsOpen;
                bool repeatQueued=false;
                while(!Complete()&&updates<2000)
                {
                    if((stage>=0||stage==-3||summer)&&updates==8 || summer&&texts.Count==1&&s.DisabledObjects==0&&!repeatQueued)
                    {sokra.WriteByte(0x71,1);native.SetNativeByte(page+0x71,1);if(texts.Count==1)repeatQueued=true;}
                    if(s.Dialogue.IsOpen)
                    {
                        int text=s.Dialogue.SourceTextId!.Value;texts.Add(text);
                        app.Step(3,Vector2.Zero,batched:batched,afterUpdate:Compare);
                        if(text==0x5206)
                        {
                            int choice=batched&&texts.Count(id=>id==0x5206)==1?1:0;
                            s.Dialogue.SubmitChoiceForValidation(choice);native.SetNativeByte(0xcba5,(byte)choice);
                        }
                        else s.Dialogue.Close();
                        native.SetNativeByte(0xcba0,0);
                    }
                    else app.Step(batched?2:1,Vector2.Zero,batched:batched,afterUpdate:Compare);
                }
                int[] expected=summer?[0x5208,0x5209]:portal?batched?[0x5204,0x5205,0x5206,0x5206,0x5207]:[0x5204,0x5205,0x5206,0x5207]:
                    [stage switch{-3=>0x5202,-1=>0x5200,0=>0x5201,1=>0x5203,_=>0x520a}];
                FailIf(!Complete()||!texts.SequenceEqual(expected),
                    $"Sokra stage={stage} did not finish its source dialogue: {string.Join(',',texts.Select(id=>id.ToString("x4")))}.");
                if(!portal)
                {
                    if(stage==-1)
                        FailIf(!s.Save.HasRoomFlag(0,0xe9,0x40),"Sokra $27:$00 introduction must retain room flag $40.");
                    if(stage>=0)app.Step(140,Vector2.Zero,batched:batched,afterUpdate:Compare);
                    continue;
                }
                FailIf(!s.Save.HasRoomFlag(0,0x9b,0x40)||native.ReadNativeByte(page+0x40)!=0,
                    "Sokra $27:$01 must set room flag $40 and delete after the accepted portal explanation.");
                s.Link.WarpTo(new(40.25f,23.5f));s.Link.Face(Vector2I.Right);
                for(int approach=0;approach<40&&!s.Link.TopDownAirborne;approach++)app.Step(1,Vector2.Right);
                app.Step(40,Vector2.Zero,batched:batched);
                FailIf((int)s.Link.Position.X!=72||(int)s.Link.Position.Y!=18||s.Link.TopDownAirborne,
                    "Seasons $0:9b: completed Sokra must leave stump $20 mountable from the left.");
                s.DebugTravel(0,0xb6);s.DebugTravel(0,0x9b);app.Step(2,Vector2.Zero);
                FailIf(s.Entities.EntityAdapters<SeasonsSokraRoomEntity>().Any(actor=>!actor.Finished),
                    "Seasons $0:9b: room flag $40 must prevent Sokra's completed portal sequence from replaying.");
                s.Save.SetRoomFlag(0,0x9b,0x40,false);s.DebugTravel(0,0xb6);s.DebugTravel(0,0x9b);
                s.Link.WarpTo(new(24,44));app.Step(batched?72:3,Vector2.Zero,batched:batched);
                FailIf(s.DisabledObjects==0,"Sokra $27:$01 must own input during its counter/text before cancellation.");
                s.DebugTravel(0,0xb6);app.Step(2,Vector2.Zero);
                FailIf(s.DisabledObjects!=0||s.Memory.ReadField("wMenuDisabled")!=0||s.Dialogue.IsOpen||s.Save.HasRoomFlag(0,0x9b,0x40),
                    "Sokra $27:$01 cancellation must release its input/text without committing the explanation flag.");
            }
            finally{root.Free();ResetValidationInput();}
        }
    }
    private void CheckSeasonsSokraVisibility()
    {
        var root=StartSeasonsAfterImpa(false);
        try
        {
            var s=root._seasonsOpening!;var app=new ApplicationValidationFixture(root);
            foreach(bool finished in new[]{false,true})
            foreach(bool summer in new[]{false,true})
            foreach(bool visited in new[]{false,true})
            {
                s.Save.SetGlobalFlag(SeasonsGlobalFlag.FinishedGame,finished);
                s.Save.WriteField("wObtainedSeasons",(byte)(summer?2:0));s.Save.SetRoomFlag(0,0x71,0x40,visited);
                s.DebugTravel(0,0xe9);app.Step(2,Vector2.Zero);
                FailIf(s.Entities.EntityAdapters<SeasonsSokraRoomEntity>().Any(actor=>!actor.Finished)!=(!finished&&(summer||!visited)),
                    "Sokra $27:$00 visibility must follow finished-game, summer power and room $0:71 flag $40 in source order.");
            }
            s.Save.SetGlobalFlag(SeasonsGlobalFlag.FinishedGame,false);s.Save.WriteField("wObtainedSeasons",0);
            s.Save.SetRoomFlag(0,0x71,0x40,false);s.Save.SetRoomFlag(0,0xe9,0x40,false);
            s.DebugTravel(0,0xe9);s.Link.WarpTo(new(120,80));app.Step(80,Vector2.Zero);
            FailIf(s.DisabledObjects!=0||s.Dialogue.IsOpen||s.Entities.EntityAdapters<SeasonsSokraRoomEntity>().Single().ReadByte(0x77)!=0,
                "Sokra $27:$00 must not start the village encounter before Link obtains the Seed Satchel.");
        }
        finally{root.Free();ResetValidationInput();}
    }
}
