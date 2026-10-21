using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateAnglerFishRom() => ValidateAnglerFishCore(false);
    private void ValidateAnglerFishPublicationRom() => ValidateAnglerFishCore(true);
    private void ValidateAnglerFishCore(bool publication)
    {
        foreach(bool batch in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(7,0x4a);
            _inventory.EquipA(0);_inventory.EquipB(0);_player.WarpTo(new(24,152));
            FailIf(_collision.Collides(_player.Position),"Angler Fish fixture requires source floor in room$5:$4a.");
            var slots=SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_enemySlots");
            var body=_entities.Entities<AnglerFishCharacter>().Single();
            FailIf(body.Position!=new Vector2(104,56) || body.Health!=30 || _entities.RoomEnemyCount!=1,
                "Room$5:$4a requires counted Angler Fish$76:$00 at Y$38/X$68, HP30.");
            var seed=_random.CaptureState();var rom=new EnemyStatusRom(_currentRoom,_saveData,seed.Calls);
            rom[0xcc08]=0xb8;rom[0xcc0a]=0xa5;rom[0xcc0c]=0x78;rom[0xcc0e]=0xa7;
            rom[0xd080]=1;rom[0xd081]=0x76;rom[0xd08b]=56;rom[0xd08d]=104;
            var interactions=SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_interactionSlots");
            int ladder=0xd040+interactions.Single(pair=>pair.Key is JabuLadderRoomEntity).Value*256;
            rom[ladder]=1;rom[ladder+1]=0x90;rom[ladder+2]=0x0b;rom[ladder+11]=8;rom[ladder+13]=0xd8;
            rom[0xd000]=1;rom[0xd004]=1;rom[0xd024]=0x80;rom[0xd029]=0x38;
            rom[0xd026]=rom[0xd027]=6;rom[0xff94]=seed.Rng1;rom[0xff95]=seed.Rng2;
            rom[0xca4a]=_saveData.GetRoomFlags(5,0x4a);rom[0xca4d]=_saveData.GetRoomFlags(5,0x4d);
            int update=0;var states=new HashSet<int>();
            var checkedAntennaTextures=new HashSet<ulong>();
            byte[] observed=new byte[4],expectedScratch=new byte[4];int probes=0,publications=0;
            if(publication)
            {
                var probe=new PartMovementScratchProbe(()=>
                {probes++;for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);});
                _entities.RegisterEnemySlot(probe,15);_entities.AddEntity(probe);
            }
            void Compare()
            {
                int calls=rom.MovementCalls;bool moved=false;
                rom.Update(_entities.FrameCounter,_player.Position,afterEnemies:()=>
                {moved=rom.MovementCalls!=calls;for(int i=0;i<4;i++)expectedScratch[i]=rom[0xcec0+i];});update++;
                if(publication)
                {
                    FailIf(rom[0xdf80]!=0,"Angler publication observer's reserved ENEMY$0f must remain unused in the original trace.");
                    if(moved)
                    {
                        FailIf(probes!=1||!observed.SequenceEqual(expectedScratch),
                            $"Angler update{update}, batch={batch}: original body/bubble movement scratch before PARTs differs: [{string.Join(',',observed)}]/[{string.Join(',',expectedScratch)}].");
                        publications++;
                    }
                    probes=0;
                }
                foreach(var (entity,slot) in slots.Where(pair=>pair.Key.Node is AnglerFishCharacter))
                {
                    var actor=(AnglerFishCharacter)entity.Node;int s=0xd080+slot*256;
                    FailIf(rom[s]==0 || actor.SubId!=rom[s+2] || actor.State!=rom[s+4] || actor.Counter1!=rom[s+6] ||
                        actor.Counter2!=rom[s+7] || actor.Angle!=rom[s+9] || actor.Direction!=rom[s+8] || actor.Speed!=rom[s+0x10] ||
                        actor.Health!=rom[s+0x29] || actor.Position!=new Vector2(rom.Word(s+12)/256f,rom.Word(s+10)/256f) ||
                        actor.Visible!=((rom[s+0x1a]&0x80)!=0) || actor.AnimationParameter!=rom[s+0x21] ||
                        (actor.InvincibilityCounter&255)!=rom[s+0x2b],
                        $"Angler slot${slot:x2} update{update}, batch={batch}: runtime sub/state/counts/angle/dir/speed/HP/XY/visible/anim={actor.SubId}/{actor.State}/{actor.Counter1},{actor.Counter2}/{actor.Angle}/{actor.Direction}/${actor.Speed:x2}/{actor.Health}/{actor.Position}/{actor.Visible}/{actor.AnimationParameter}; ROM={rom[s+2]}/{rom[s+4]}/{rom[s+6]},{rom[s+7]}/{rom[s+9]}/{rom[s+8]}/${rom[s+0x10]:x2}/{rom[s+0x29]}/{rom.Word(s+12)/256f},{rom.Word(s+10)/256f}/{rom[s+0x1a]:x2}/{rom[s+0x21]}.");
                }
                foreach(var antenna in _entities.Entities<AnglerFishCharacter>().Where(actor=>actor.IsAntenna && actor.State==9))
                    ValidateAnglerAntennaPixels(antenna,checkedAntennaTextures);
                states.Add(body.State);
                FailIf(RandomDiffers(seed, rom[0xff94], rom[0xff95], rom.RandomCalls) || _entities.RoomEnemyCount!=rom[0xcdd1],
                    $"Angler update{update}: shared RNG/count differs {_random.CaptureState().Calls-seed.Calls}/{rom.RandomCalls}, {_entities.RoomEnemyCount}/{rom[0xcdd1]}.");
                foreach(byte packed in new byte[]{0x80,0x90})FailIf(_currentRoom.GetPackedStorageMetatile(packed)!=rom[0xcf00+packed],
                    $"Angler update{update}: original entrance tile${packed:x2} differs: runtime=${_currentRoom.GetPackedStorageMetatile(packed):x2}, ROM=${rom[0xcf00+packed]:x2}.");
                for(int row=0;row<10;row++)FailIf(_currentRoom.GetPackedStorageMetatile((byte)(row*16+13))!=rom[0xcf0d+row*16],
                    $"Angler update{update}: ladder row${row:x2} differs.");
                FailIf(_saveData.GetRoomFlags(5,0x4a)!=rom[0xca4a] || _saveData.GetRoomFlags(5,0x4d)!=rom[0xca4d],
                    $"Angler update{update}: persistent ladder/portal flags differ.");
            }
            StepGameplayUpdates(360,Vector2.Zero,batched:batch,afterUpdate:Compare);
            FailIf(!new[]{8,9,10,11,12}.All(states.Contains),"Angler ROM trace must cover entry, descent, bouncing and bubble attack.");
            // Resolve one declared Scent Seed through the native collision
            // table; subsequent deflation and reinflation run in gameplay.
            var main=slots.Single(pair=>pair.Key.Node==body);
            rom.HitWithSword(ItemCollisionType.ScentSeed,0,main.Value);
            FailIf(!((IItemCollisionHittableRoomEntity)main.Key).ApplyItemCollision((RoomEntityItemCollision)ItemCollisionType.ScentSeed,
                body.CollisionBounds,body.Position,0,new List<RoomEntitySpawn>()),"Angler body must accept Scent Seed deflation.");
            StepGameplayUpdates(240,Vector2.Zero,batched:batch,afterUpdate:Compare);
            FailIf(!new[]{13,14,15}.All(states.Contains) || body.State is 13 or 14 or 15,
                $"Scent Seed must complete the native deflation/wait/reinflation states and restore the fight: states={string.Join(',',states)}, state={body.State}, c={body.Counter1}, XY={body.Position}.");
            // The three-pixel antenna has its own HP. Killing it must kill the
            // body, wait for the counted explosion and release the real ladder.
            var weak=slots.Single(pair=>pair.Key.Node is AnglerFishCharacter {IsAntenna:true});
            var antenna=(AnglerFishCharacter)weak.Key.Node;
            FailIf(antenna.Health!=30,"Angler weak spot retains independent source HP30.");
            ((ILinkSwordStateAwareRoomEntity)weak.Key).SetLinkSwordState(SwordActionState.Swing,1);
            rom.HitWithSword(ItemCollisionType.L1Sword,1,weak.Value);
            FailIf(!((ISwordHittableRoomEntity)weak.Key).ApplySwordHit(antenna.CollisionBounds,antenna.Position-Vector2.Right,1,
                EnemyKnockbackStrength.Normal,new List<RoomEntitySpawn>()),"Angler antenna must accept a nonlethal native sword hit.");
            StepGameplayUpdates(1,Vector2.Zero,batched:batch,afterUpdate:Compare);
            FailIf(body.Health!=30 || antenna.Health!=29 || body.InvincibilityCounter!=32 || antenna.Visible,
                "ENEMY$76:$01 copies its hit invincibility to the body and hides the glow, retaining independent HP.");
            StepGameplayUpdates(40,Vector2.Zero,batched:batch,afterUpdate:Compare);
            FailIf(body.InvincibilityCounter!=0 || antenna.InvincibilityCounter!=0,
                "ENEMY$76 antenna/body must clear the native hit cooldown before a repeated sword hit.");
            rom.HitWithSword(ItemCollisionType.L1Sword,30,weak.Value);
            FailIf(!((ISwordHittableRoomEntity)weak.Key).ApplySwordHit(antenna.CollisionBounds,antenna.Position-Vector2.Right,30,
                EnemyKnockbackStrength.Normal,new List<RoomEntitySpawn>()),"Angler antenna must accept native sword damage.");
            StepGameplayUpdates(300,Vector2.Zero,batched:batch,afterUpdate:Compare);
            FailIf(!body.IsDead || _entities.RoomEnemyCount!=0 || !_saveData.HasRoomFlag(5,0x4a,0x80) || !_saveData.HasRoomFlag(5,0x4d,0x80),
                $"Angler death must finish its counted explosion and persist both ladder/portal room flags: dead={body.IsDead}, count={_entities.RoomEnemyCount}, flags=${_saveData.GetRoomFlags(5,0x4a):x2}/${_saveData.GetRoomFlags(5,0x4d):x2}.");
            FailIf(publication&&publications<200,"Angler source comparison must observe live body/bubble movement before later phases reuse scratch.");
            LoadValidationRoom(7,0x4a);
            StepGameplayUpdates(1,Vector2.Zero,batched:batch);
            FailIf(_entities.Entities<AnglerFishCharacter>().Count!=0 || _entities.RoomEnemyCount!=0 ||
                _entities.Entities<JabuLadderRoomEntity>().Count!=0,
                "Room$7:$4a re-entry after flag$80 must suppress the defeated miniboss and completed ladder controller.");
        }
        GD.Print("Validated clean-US Angler Fish entry, antenna allocation/fixed-bank glow pixels, side-view bounce/bubbles, Scent Seed deflation and independent weak-spot death through split/batched gameplay.");
    }

    private void ValidateAnglerAntennaPixels(AnglerFishCharacter antenna,HashSet<ulong> checkedTextures)
    {
        Texture2D texture=antenna.CurrentAnimationTexture;
        if(!checkedTextures.Add(texture.GetInstanceId()))return;
        var native=new ObjectAnimationRom(1,0x76);native.Set(6);
        FailIf(native.Oam!="8,4,0,0" || native.Counter!=127 || native.Parameter!=0 ||
            antenna.AnimationIndex!=6 || antenna.Animation.CurrentOffset!=new Vector2(-4,-8) ||
            texture.GetSize()!=new Vector2(8,16),
            "ENEMY$76:$01 glow requires native animation$06, OAM Y$08/X$04/tile$00/flags$00 and positioned 8x16 bounds.");
        // Independent golden from GFXH_COMMON_SPRITES at $8000/bank 1,
        // 8x16 tile pair $1e/$1f, with common OBJ palette$05. The fish
        // sheet's tile$1e is a deflated body fragment, not this round glow.
        string[] pixels=["00000000","00000000","00000000","00000000","00000000",
            "00111100","01233210","01333310","01333310","01233210","00111100",
            "00000000","00000000","00000000","00000000","00000000"];
        Color[] colors=[new(0,0,0,0),new(1,22/31f,6/31f),new(27/31f,0,0),new(0,0,0)];
        using Image expected=Image.CreateEmpty(8,16,false,Image.Format.Rgba8);
        for(int y=0;y<16;y++)for(int x=0;x<8;x++)expected.SetPixel(x,y,colors[pixels[y][x]-'0']);
        using Image actual=texture.GetImage();
        for(int y=0;y<16;y++)for(int x=0;x<8;x++)
            FailIf(actual.GetPixel(x,y)!=expected.GetPixel(x,y),
                $"ENEMY$76:$01 antenna pixel({x},{y}) differs from fixed-bank common glow tile$1e/palette$05: " +
                $"actual={actual.GetPixel(x,y)}, expected={expected.GetPixel(x,y)}.");
    }
}
