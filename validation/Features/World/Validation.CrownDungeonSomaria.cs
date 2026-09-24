using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateCrownDungeonSomariaSwing()
    {
        var data=new SomariaSwingDatabase();
        var geometry=new SomariaPlacementDatabase(); var graphics=new SomariaGraphicsDatabase();
        int[] normalParams=[0,2,0x64,6,0x86], mountedParams=[0,2,0x26,0x86];
        int[] normalGraphic=[0xac,0xb0,0xb4,0xb0,0xb0], mountedGraphic=[0xc8,0xcc,0xcc,0x58];
        foreach(int mode in new[]{0x22,0x26,0x2d})
        {
            var frames=data.Frames(mode); int[] parameters=mode==0x26?mountedParams:normalParams;
            for(int frame=0;frame<parameters.Length;frame++)
                FailIf(frames[frame].Parameter!=parameters[frame] ||
                    frames[frame].Duration!=(frame==parameters.Length-1?127:frame==2?8:3) ||
                    frames[frame].Graphic!=(mode==0x26?mountedGraphic[frame]:normalGraphic[frame]+(mode==0x2d?16:0)),
                    $"Somaria mode${mode:x2}/frame{frame} must retain exact source graphic, duration and parameter.");
        }
        // Test each substitution and priority: raft keeps mode22 even when
        // underwater/mounted; underwater wins over mounted otherwise.
        for(int flags=0;flags<8;flags++)
        {
            var parent=new SomariaParentAnimation(data,(flags&1)!=0,(flags&2)!=0,(flags&4)!=0);
            int mode=(flags&4)!=0?0x22:(flags&1)!=0?0x2d:(flags&2)!=0?0x26:0x22;
            FailIf(parent.Mode!=mode,"Somaria parent mode substitution order differs from commonCode.s.");
            var weapon=new SomariaWeapon(data,geometry,graphics,new(80,64),0);
            try
            {
                int sound=0,transfer=0,marks=0,allocations=0;
                var order=new System.Collections.Generic.List<string>();
                void StepItem(bool frozen) => weapon.UpdateItem(frozen,parent.Parameter,new(80.75f,64.5f),1,-3,
                    ()=>transfer++,id=>{ FailIf(id!=0x74,"Somaria uses fixed SND_SWORDSLASH, not randomized sword sounds."); sound++; },
                    ()=>{marks++;order.Add("mark");},(point,z)=>{
                        allocations++;order.Add("allocate");
                        FailIf(point!=new Vector2(99,64)||z!=-3,"Somaria allocation must use high Link XY offsets and copy Link Zh.");
                        return false; // Full slots still delete old block and consume the trigger.
                    });
                StepItem(true);
                FailIf(weapon.State!=1||sound!=1||transfer!=1,"Somaria uninitialized weapon must run despite frozen item updates.");
                StepItem(true);
                FailIf(transfer!=1,"Somaria initialized weapon must freeze before knockback transfer.");
                int end=mode==0x26?14:17;
                for(int update=1;update<=end;update++)
                {
                    parent.Update(); StepItem(false);
                    weapon.UpdatePost(4,parent.Parameter,new(80,64),1,-3,-3);
                    int expectedAllocations=mode!=0x26&&update>=14?1:0;
                    FailIf(!parent.Active || allocations!=expectedAllocations || marks!=expectedAllocations,
                        $"Somaria mode${mode:x2}/update{update} must create only at exact parameter06/update14.");
                }
                FailIf(parent.Parameter!=0x86||weapon.Finished,"Somaria terminal parameter must remain visible until next parent update.");
                if(mode!=0x26) FailIf(!order.SequenceEqual(new[]{"mark","allocate"})||weapon.State!=2,
                    "Somaria must mark old block before attempted allocation and never retry failure.");
                parent.Update(); FailIf(parent.Active,"Somaria parent must retire one update after terminal parameter.");
                // Post runs even while normal item updates are frozen.
                weapon.UpdatePost(0,parent.Parameter,new(80,64),1,0,0);
                FailIf(!weapon.Finished||weapon.Visible,"Somaria post must delete weapon after related parent ID changes.");
            }
            finally { weapon.Free(); }
        }
        int[] selectors=[0x02,0x41,0x80,0xc0,0x10,0x51,0x92,0xd2,0x26,0x65,0xa4,0xe4,0x30,0x77,0xb6,0xf6];
        for(int direction=0;direction<4;direction++)
        for(int phase=0;phase<4;phase++)
        {
            int value=selectors[direction*4+phase];
            var actual=data.Select(phase*2,direction);
            FailIf(actual.Animation!=(value&7)||actual.Arc!=(value>>4),"Somaria must retain full source swing selector, including arc index.");
        }
        var visual=new SomariaWeapon(data,geometry,graphics,new(80,64),0);
        try
        {
            visual.UpdatePost(4,0x64,new(80.9f,64.2f),1,-3,-3);
            FailIf(visual.Pose!=2 || visual.Position!=new Vector2(99,63) || visual.ZHigh!=-5 ||
                visual.Radius!=new Vector2I(9,6) || visual.Collision!=0x92 || visual.BaseDamage!=0xfc,
                "Somaria right phase2 post must use arc9, raised-floor Y, Link Z-2 and native weapon attributes.");
        }
        finally {visual.Free();}
        GD.Print("Validated isolated Somaria parent modes, terminal timing, exact06 creation, failed-slot ordering and weapon post geometry.");
    }

    private void ValidateCrownDungeonSomariaCarryThrow()
    {
        var geometry = new SomariaPlacementDatabase();
        var graphics = new SomariaGraphicsDatabase();
        var data = new SomariaLifecycleDatabase();
        Vector2 start = new(72,54);
        foreach (int mode in new[] {0,1,2,3,4,5,6})
        {
            var room = Room060MovementFixture();
            for (int y = 0; y < room.Height; y += 16)
            for (int x = 0; x < room.Width; x += 16) room.SetPositionTileAndCollision(new(x+8,y+8),0x0c,0,0);
            int puffs = 0, drops = 0, hazard = 0;
            Vector2 puff = default;
            var actor = new SomariaBlock(room, start, 0, geometry, graphics, data,
                new(_ => {}, _ => {}, _ => {}, (p, _) => { puffs++; puff=p; },
                    _ => drops++, (kind, _, _) => hazard=kind));
            try
            {
                for (int i = 0; i <= 9; i++) actor.Update(0,-1,1,i);
                actor.BeginPickup();
                FailIf(room.GetMetatile(start) != 0xda || !actor.IsHeld,
                    "Somaria pickup signal must defer tile removal until its state2 handler.");
                if(mode==6)
                {
                    actor.Release(0xff,dropped:true);
                    FailIf(actor.Substate!=3 || actor.IsHeld || room.GetMetatile(start)!=0xda,
                        "dropLinkHeldItem must accept state2/substate0 without synthesizing its skipped tile-removal update.");
                    continue;
                }
                actor.Update(0,-1,1,10);
                FailIf(actor.Substate != 1 || room.GetMetatile(start) != 0x0c,
                    "Somaria pickup initialization must restore the shared tile.");
                actor.CopyHeldPosition(new(72.75f,64.5f),0,2,1);
                FailIf(actor.Position != new Vector2(72,64) || actor.ZHigh != -14,
                    "Somaria weight0 carry copies high XY and direction-right frame2 Z=-14.");
                if (mode is 3 or 4) actor.Flags |= 0x20;
                actor.Update(0,-1,1,11);
                FailIf(actor.Finished || !actor.IsHeld, "Somaria held state must ignore the replacement flag.");
                if (mode == 4)
                {
                    actor.DamageToApply = -10; actor.Update(0,-1,1,12);
                    FailIf(!actor.Finished || drops != 1 || puffs != 1 || actor.Health != 0xff,
                        "Somaria held health underflow must release its Link owner before puff/deletion.");
                    continue;
                }
                if (mode == 5)
                {
                    // Boundary check is before itemBeginThrow's one-pixel
                    // facing displacement, and must suppress the puff.
                    actor.CopyHeldPosition(new(room.Width,64),0,2,1);
                    actor.Release(8); actor.Update(0,-1,1,12,1);
                    FailIf(!actor.Finished || puffs != 0 || drops != 0,
                        "Somaria out-of-room throw must delete without puff or held-owner callback.");
                    continue;
                }
                bool drop = mode == 0, toss = mode == 2;
                actor.Release(drop ? 0xff : 8, dropped:drop);
                FailIf(actor.Position != new Vector2(72,64) || actor.IsHeld,
                    "Somaria release must defer initial movement to the item update.");
                if (mode == 3)
                {
                    actor.Update(0,-1,1,12,1);
                    FailIf(!actor.Finished || puff != new Vector2(74,64) || actor.ZHigh != -14,
                        "Somaria replacement after release must run lateral initialization/motion before deletion, without gravity.");
                    continue;
                }
                int duration = drop ? 17 : 28;
                for (int update = 1; update <= duration; update++)
                {
                    actor.Update(0,-1,1,11+update,1,toss);
                    int expectedZ = -3584 + (drop ? 0 : -240)*update + 14*update*(update-1);
                    FailIf(actor.Finished != (update == duration) ||
                        actor.ZHigh != (update == duration ? 0 : expectedZ >> 8),
                        $"Somaria weight0 throw mode{mode} update{update} must preserve native gravity and first-contact deletion.");
                }
                Vector2 end = new(drop ? 73 : toss ? 143 : 115,64);
                FailIf(actor.Position != end || puffs != 1 || puff != end || drops != 0 || hazard != 0,
                    "Somaria thrown/drop item must end at its source-derived position with one puff and no bounce.");
            }
            finally { actor.Free(); }
        }
        // objectCheckIsOverHazard probes Y+5, distinct from the placement
        // hazard lookup at the object's tile. Use adjacent rows to prove it.
        var hazardRoom = Room060MovementFixture();
        hazardRoom.SetPositionTileAndCollision(new(73,60),0x0c,0,0);
        hazardRoom.SetPositionTileAndCollision(new(73,65),0xf3,0,0);
        var motion = new SomariaThrowMotion(hazardRoom,geometry);
        motion.Begin(new(72,60),0,1,0xff,false);
        int effect = 0;
        bool landed = motion.AdvanceVertical((kind,_,_)=>effect=kind,out bool deleted);
        FailIf(landed || !deleted || effect != 2,
            "Somaria throw landing must use the Y+5 hole probe and hazard effect instead of a puff.");
        hazardRoom.SetPositionTileAndCollision(new(73,54),0x22,15,0);
        motion = new SomariaThrowMotion(hazardRoom,geometry,_runtimeState);
        motion.Begin(new(72,54),-14,1,8,false);
        for (int i=0;i<4;i++) _runtimeState.SetWramByte(0xcec0+i,0xa5);
        motion.AdvanceLateral();
        FailIf(motion.Angle!=0xff || motion.Position!=new Vector2(73,54),
            "Somaria lateral wall contact must clear angle before displacement, while preserving vertical flight.");
        for (int i=0;i<4;i++)
        {
            FailIf(_runtimeState.ReadWramByte(0xcec0+i)!=0,
                "Somaria's new wall collision must execute invalid-angle velocity clearing.");
            _runtimeState.SetWramByte(0xcec0+i,0xa5);
        }
        motion.AdvanceLateral();
        for (int i=0;i<4;i++) FailIf(_runtimeState.ReadWramByte(0xcec0+i)!=0xa5,
            "A previously stopped Somaria throw must return before velocity writes.");
        landed=motion.AdvanceVertical((_,_,_)=>{},out deleted);
        FailIf(landed || deleted || motion.ZHigh!=-15,
            "Somaria lateral wall contact must not terminate its airborne arc.");
        LoadValidationRoom(6,0x93); _entities.Clear();
        _currentRoom.SetPositionTileAndCollision(start,0x1a,0,0);
        motion = new SomariaThrowMotion(_currentRoom,geometry);
        motion.Begin(new(72,54),0,1,0xff,false);
        int splashes = 0;
        for (int update=1;update<=4;update++)
        {
            motion.AdvanceLateral();
            landed=motion.AdvanceVertical((kind,_,_)=>{ FailIf(kind!=1,"Sideview water must emit splash1."); splashes++; },out deleted);
            FailIf(landed || deleted || motion.SpeedZ != (update/2)*28 || motion.Position != new Vector2(73,54),
                "Somaria sideview water must keep the block, splash once and advance gravity on alternating updates.");
        }
        FailIf(splashes!=1,"Somaria water entry must emit exactly one splash.");
        _currentRoom.SetPositionTileAndCollision(start,0,15,0);
        motion = new SomariaThrowMotion(_currentRoom,geometry);
        motion.Begin(new(72,54),0,1,8,false);
        landed=motion.AdvanceVertical((_,_,_)=>{},out deleted);
        FailIf(landed || deleted || motion.Position!=new Vector2(73,54) || motion.SpeedZ!=-212,
            "Somaria sideview ceiling must stop displacement while applying gravity.");
        motion = new SomariaThrowMotion(_currentRoom,geometry);
        motion.Begin(new(72,54),0,1,0xff,false);
        landed=motion.AdvanceVertical((_,_,_)=>{},out deleted);
        FailIf(!landed || deleted,"Somaria sideview floor must report first contact immediately.");
        // Sideview Z merges after lateral motion and before vertical motion.
        _currentRoom.SetPositionTileAndCollision(start,0,0,0);
        motion = new SomariaThrowMotion(_currentRoom,geometry);
        motion.Begin(new(72,64),-14,1,8,false);
        motion.AdvanceLateral();
        FailIf(motion.Position!=new Vector2(74,64) || motion.ZHigh!=-14,
            "Somaria sideview lateral motion must precede the high-Z merge.");
        landed=motion.AdvanceVertical((_,_,_)=>{},out deleted);
        FailIf(landed || deleted || motion.Position!=new Vector2(74,49) || motion.ZHigh!=0 || motion.SpeedZ!=-212,
            "Somaria sideview throw must merge Z then apply weight0 vertical motion.");
        GD.Print("Validated isolated Somaria pickup, held replacement/damage, drop/throw trajectories, first landing, boundary and sideview water/ceiling/floor; gameplay routing pending.");
    }

    private void ValidateCrownDungeonSomariaGroundStates()
    {
        var geometry = new SomariaPlacementDatabase();
        var graphics = new SomariaGraphicsDatabase();
        var data = new SomariaLifecycleDatabase();
        FailIf(data.PhaseSound != 0x7b || data.MoveSound != 0x71 ||
            data.NormalSpeed != 0x14 || data.NormalFrames != 32 || data.GloveSpeed != 0x1e || data.GloveFrames != 21,
            "Somaria must preserve SPEED_80/$20 and SPEED_c0/$15 push profiles.");
        Vector2 start = new(72,54);
        Vector2[] directions = [Vector2.Up, Vector2.Right, Vector2.Down, Vector2.Left];
        for (int level = 1; level <= 3; level++)
        for (int direction = 0; direction < 4; direction++)
        {
            var room = Room060MovementFixture();
            Vector2 destination = start + directions[direction]*16;
            room.SetPositionTileAndCollision(start, 0x0c, 0, 0);
            room.SetPositionTileAndCollision(destination, 0x0c, 0, 0);
            int pushes = 0, publications = 0, puffs = 0;
            var sounds = new System.Collections.Generic.List<int>();
            var world = new SomariaBlockEnvironment(sounds.Add,
                block => { pushes++; FailIf(block.Radius.Y != 7, "Somaria Link repulsion must use Y radius7."); },
                _ => publications++, (_, _) => puffs++);
            var actor = new SomariaBlock(room, start, -3, geometry, graphics, data, world);
            try
            {
                actor.Update(0, -1, level, 0);
                FailIf(actor.State != 1 || !actor.Visible || room.GetMetatile(start) != 0x0c || sounds.Count != 1,
                    "Somaria initialization must show phase-in without creating its solid tile.");
                for (int update = 1; update <= 8; update++) actor.Update(0, -1, level, update);
                FailIf(actor.State != 1 || room.GetMetatile(start) != 0x0c || pushes != 8 || publications != 0,
                    "Somaria phase updates1-8 must repel Link without a solid tile or grabbable publication.");
                actor.Update(0, -1, level, 9);
                FailIf(actor.State != 3 || actor.ZHigh != 0 || actor.Collision != 0x95 || actor.Radius != new Vector2I(4,4) ||
                    room.GetMetatile(start) != 0xda || pushes != 9 || publications != 0,
                    "Somaria phase update9 must place the block and enable collisions, deferring pickup publication.");
                actor.Update(0, -1, level, 10);
                FailIf(publications != 1, "Somaria solid handler must publish its pickup candidate.");
                actor.RequestPush(direction);
                actor.Update(0, -1, level, 11);
                FailIf(actor.State != 4 || actor.Substate != 0 || actor.Position != start || room.GetMetatile(start) != 0xda,
                    "Somaria push signal must select state4 without moving/removing the tile on that update.");
                int duration = level == 2 ? 21 : 32;
                float speed = level == 2 ? 0.75f : 0.5f;
                for (int update = 1; update <= duration; update++)
                {
                    actor.Update(0, -1, level, 11+update);
                    if (update < duration)
                        FailIf(actor.State != 4 || actor.Counter != duration-update ||
                            actor.Position != (start+directions[direction]*speed*update).Floor() ||
                            room.GetMetatile(start) != 0x0c || room.GetMetatile(destination) != 0x0c,
                            $"Somaria push level{level}/direction{direction} update{update} must preserve byte/fractional movement and delayed tile placement.");
                }
                FailIf(actor.Finished || actor.State != 3 || actor.Position != destination || actor.Flags != 0 ||
                    room.GetMetatile(destination) != 0xda || puffs != 0 || sounds.Count != 2 ||
                    sounds[0] != data.PhaseSound || sounds[1] != data.MoveSound,
                    "Somaria completed push must align and reacquire its destination tile on the exact zero counter update.");
                actor.DamageToApply = -9;
                actor.Update(0, -1, level, 50);
                FailIf(actor.Finished || actor.Health != 0 || actor.DamageToApply != 0,
                    "Somaria health zero must survive and consume pending damage.");
                actor.DamageToApply = -1;
                actor.Update(0, -1, level, 51);
                FailIf(!actor.Finished || actor.Health != 0xff || puffs != 1 || room.GetMetatile(destination) != 0x0c,
                    "Somaria health underflow must restore owned tile and create one puff.");
            }
            finally { actor.Free(); }
        }
        // Replacement during phase-in is deferred: state1 completes placement
        // first, then state3 sees bit5 and removes the tile on its next update.
        foreach (int cause in new[] {0,1,2,3})
        {
            var room = Room060MovementFixture(); room.SetPositionTileAndCollision(start, 0x0c, 0, 0);
            int puffs = 0;
            var actor = new SomariaBlock(room, start, 0, geometry, graphics, data,
                new(_ => {}, _ => {}, _ => {}, (_, _) => puffs++));
            try
            {
                if (cause == 0) actor.Flags = 0x20;
                for (int update = 0; update <= 9; update++) actor.Update(0, -1, 1, update);
                FailIf(actor.Finished || actor.State != 3, "Somaria replacement flag must not cancel phase-in early.");
                if (cause == 1) room.SetPositionTileAndCollision(start, 0x22, 0, 10);
                if (cause == 3) actor.Flags = 0x30;
                actor.Update(0, cause == 2 ? actor.PackedPosition : -1, 1, 10);
                FailIf(!actor.Finished || room.GetMetatile(start) != (cause == 1 ? 0x22 : 0x0c) ||
                    puffs != (cause == 3 ? 0 : 1),
                    $"Somaria removal cause{cause} must preserve external tile ownership, Link-tile deletion and bit4 puff suppression.");
            }
            finally { actor.Free(); }
        }
        foreach (int cause in new[] {0,1,2})
        {
            var room = Room060MovementFixture();
            room.SetPositionTileAndCollision(start, 0x0c, 0, 0);
            Vector2 destination = start + Vector2.Right*16;
            room.SetPositionTileAndCollision(destination, 0x0c, 0, 0);
            int puffs = 0; Vector2 puffPosition = default;
            var actor = new SomariaBlock(room, start, 0, geometry, graphics, data,
                new(_ => {}, _ => {}, _ => {}, (point, _) => { puffs++; puffPosition = point; }));
            try
            {
                for (int update = 0; update <= 9; update++) actor.Update(0, -1, 2, update);
                actor.RequestPush(1); actor.Update(0, -1, 2, 10);
                actor.Update(0, -1, 2, 11); // first movement removes source tile
                Vector2 previous = actor.Position;
                if (cause == 0)
                {
                    actor.Flags |= 0x20;
                    actor.Update(0, -1, 2, 12);
                    FailIf(actor.Position != previous || actor.Counter != 20,
                        "Somaria replacement while moving must delete before another movement/counter decrement.");
                }
                else
                {
                    room.SetPositionTileAndCollision(destination, cause == 1 ? (byte)0xf3 : (byte)0x22,
                        cause == 1 ? (byte)0 : (byte)15, 12);
                    for (int update = 2; update <= 21; update++) actor.Update(0, -1, 2, 10+update);
                    FailIf(puffPosition != (cause == 1 ? destination : new Vector2(87,54)),
                        "Somaria hazard failure must align before puff; wall failure must retain unaligned high-byte position.");
                }
                FailIf(!actor.Finished || puffs != 1 || room.GetMetatile(start) != 0x0c ||
                    room.GetMetatile(destination) != (cause == 0 ? 0x0c : cause == 1 ? 0xf3 : 0x22),
                    "Somaria cancelled/failed push must leave original and destination terrain intact.");
            }
            finally { actor.Free(); }
        }
        GD.Print("Validated isolated Somaria phase/solid/push lifecycle, fractional movement, health and removal; input/carry integration pending.");
    }

    private void ValidateCrownDungeonSomariaGraphics()
    {
        var data = new SomariaGraphicsDatabase();
        string[] expected = [
            "3,0@8,0,4,0;8,8,4,96|3,0@8,0,0,0;8,8,0,32|3,0@8,0,2,0;8,8,2,32|127,1@10,0,0,0;10,8,0,32",
            "127,1@10,0,0,0;10,8,0,32", "127,2@8,0,0,0;8,8,0,32"];
        for (int pose = 0; pose < 3; pose++)
        {
            var graphic = data.Block(pose);
            FailIf(graphic.Animation != expected[pose] || graphic.Sprite != "spr_common_sprites" ||
                graphic.TileBase != (pose == 0 ? 0x18 : 0x36) || graphic.OamFlags != (pose == 0 ? 0x0b : 0x0d) ||
                graphic.InitialCollision != 0x15 || graphic.InitialRadius != new Vector2I(7,7) ||
                graphic.Damage != 0xfc || graphic.Health != 9,
                $"Somaria ITEM$18 pose{pose} must preserve source OAM, phase fallthrough, VRAM mapping and initial attributes.");
        }
        using var owner = new Node2D();
        var visual = new SomariaBlockVisual(owner, data);
        for (int update = 0; update <= 9; update++)
        {
            FailIf(visual.Frame != update / 3 || visual.Parameter != (update == 9 ? 1 : 0),
                $"Somaria phase-in must reach frame3/parameter1 on update9, not update{update}.");
            if (update < 9) visual.AdvancePhase();
        }
        // The handler consumes parameter1 immediately; no subsequent animation
        // update may wrap back into the phase-in frames while awaiting its owner.
        visual.AdvancePhase();
        FailIf(visual.Frame != 3 || visual.Parameter != 1, "Somaria placement signal must remain available to the block owner.");
        visual.SetPose(1);
        FailIf(visual.Parameter != 1 || visual.Frame != 0, "Somaria solid pose must retain parameter1.");
        byte[] solid = visual.Texture.GetImage().GetData();
        visual.SetPose(2);
        FailIf(visual.Parameter != 2 || visual.Frame != 0, "Somaria lifted pose must retain parameter2.");
        // Static OAM is two pixels lower than carried OAM, with identical tile
        // and palette. The composed bitmap is equal; its origin carries Y.
        Vector2 carriedOffset = visual.Offset;
        FailIf(!solid.SequenceEqual(visual.Texture.GetImage().GetData()), "Somaria solid/carried tile colors must agree.");
        visual.SetPose(1);
        FailIf(visual.Offset != carriedOffset + Vector2.Down*2, "Somaria solid OAM must be two pixels below carried OAM.");
        visual.SetPose(0);
        FailIf(visual.Frame != 0 || visual.Parameter != 0, "Repeated Somaria phase-in must restart its initial frame.");
        for (int pose = 0; pose < 8; pose++)
        {
            var graphic = data.Weapon(pose);
            var frames = OracleGraphicsCache.GetAnimationDefinition(graphic.Animation).Frames;
            FailIf(graphic.Sprite != "spr_cane_of_somaria" || graphic.TileBase != 0x52 || graphic.OamFlags != 0x0a ||
                graphic.InitialCollision != 0x92 || graphic.InitialRadius != new Vector2I(4,4) ||
                graphic.Damage != 0xfc || graphic.Health != 0 || frames.Length != 1 ||
                frames[0].Duration != 127 || frames[0].Parameter != 0,
                $"Somaria ITEM$04 pose{pose} must preserve native swing graphics/attributes.");
            var animation = new EnemyAnimationPlayer(owner, 1);
            // Weapon sheet was loaded at $8521: the sheet's first tile is $52.
            animation.Load(OracleGraphicsCache.LoadImage($"res://assets/oracle/gfx/{graphic.Sprite}.png"),
                [graphic.Animation], 0, graphic.OamFlags & 7);
            animation.SetAnimation(0);
            FailIf(animation.CurrentTexture.GetWidth() <= 0, "Somaria swing OAM must resolve on its imported weapon sheet.");
        }
        owner.Free();
        GD.Print("Validated Somaria source OAM, phase-in update9 and solid/carried poses; item activation remains pending.");
    }

    private void ValidateCrownDungeonSomariaPlacement()
    {
        var data=new SomariaPlacementDatabase();
        FailIf(data.Tile!=0xda || data.Collision!=0x0f || data.CreateParameter!=6,
            "Somaria must retain tile$da, collision$f and parent animation trigger06.");
        Vector2[] creation=[new(80,44),new(99,64),new(80,83),new(60,64)];
        Vector2[] wrapped=[new(0,236),new(19,0),new(0,19),new(236,0)];
        for (int direction=0;direction<4;direction++)
            FailIf(data.CreationPosition(new(80.75f,64.5f),direction)!=creation[direction] ||
                data.CreationPosition(new(0.75f,0.5f),direction)!=wrapped[direction],
                "Somaria creation must copy high XY with source -20/+19 direction offsets and byte wrapping.");
        for (int value=0;value<256;value++)
        {
            FailIf(data.HeightAllowed(value)!=(value==0 || value>=253),
                $"Somaria dec/cp$fc height gate differs at Z${value:x2}.");
            FailIf(data.Align(new(value+0.75f,value+0.5f))!=new Vector2((value&0xf0)+8,(value&0xf0)+6),
                "Somaria alignment must center X, center Y minus2, discarding source low bytes.");
        }
        for (int group=0;group<8;group++)
        for (int roomId=0;roomId<256;roomId++)
            FailIf(data.RoomAllowed(group,roomId)!=(group!=5 || roomId!=0xe8),
                "Somaria must exclude exactly Patch room$5:$e8.");
        int[] outdoorWater=[0xfa,0xfc,0xfe,0xff,0xe0,0xe1,0xe2,0xe3,0xe9];
        int[] dungeonHoles=[0xf3,0xf4,0xf5,0xf6,0xf7,0x48,0x49,0x4a,0x4b];
        for (int mode=0;mode<6;mode++)
        for (int tile=0;tile<256;tile++)
        {
            int expected=mode switch {
                0 or 4 => outdoorWater.Contains(tile)?1:tile==0xf3?2:tile is >=0xe4 and <=0xe8?4:0,
                3 => tile is >=0x1a and <=0x1f?1:0,
                _ => tile is 0xfa or 0xfc?1:dungeonHoles.Contains(tile)?2:tile is >=0x61 and <=0x65?4:0 };
            FailIf(data.Hazard(mode,tile)!=expected,$"Somaria raw hazard mode{mode}/tile${tile:x2} differs from hazards.s.");
        }
        var room=Room060MovementFixture(); var placement=new SomariaBlockPlacement(room,data);
        Vector2 point=new(72,54);
        room.SetPositionTileAndCollision(point,0x0c,0,0);
        byte[] background=room.Texture.GetImage().GetData();
        FailIf(!placement.TryCreate(0,point,-3,0) || !placement.InPlace || placement.PackedPosition!=0x34 ||
            room.GetUnderlyingMetatile(point)!=0x0c || room.GetMetatile(point)!=0xda ||
            room.GetTerrainInfo(point).Collision!=15 || !room.Texture.GetImage().GetData().SequenceEqual(background),
            "Somaria placement must own raw layout/collision bytes while preserving background mappings and the shared underlying tile.");
        // A floor-button handler can update the shared buffer while the block
        // covers it. Removal must reveal that update rather than saved input.
        room.SetUnderlyingMetatile(point,0x0d);
        FailIf(!placement.Remove(0) || placement.InPlace || room.GetMetatile(point)!=0x0d ||
            room.GetTerrainInfo(point).Collision!=room.GetCollision(0x0d),
            "Somaria removal must restore the current room buffer and recompute collision.");
        room.SetPositionTileAndCollision(point,0x0c,0,0);
        FailIf(!placement.TryCreate(0,point,0,0),"Somaria tile owner must permit repeat creation.");
        room.SetPositionTileAndCollision(point,0xda,3,0);
        FailIf(placement.InPlace || placement.Remove(0) || room.GetMetatile(point)!=0xda,
            "Somaria must not remove a matching tile whose collision byte was changed by another owner.");
        room.SetPositionTileAndCollision(point,0x22,0,0);
        FailIf(placement.Remove(0) || room.GetMetatile(point)!=0x22,
            "Somaria must not overwrite a replacement tile after losing ownership.");
        room.SetPositionTileAndCollision(point,0xf3,0,0);
        FailIf(placement.TryCreate(0,point,0,0) || room.GetMetatile(point)!=0xf3,
            "Somaria raw hazard rejection must precede tile/buffer mutation even on zero collision.");
        LoadValidationRoom(6,0x93); _entities.Clear();
        var side=new SomariaBlockPlacement(_currentRoom,data);
        _currentRoom.SetPositionTileAndCollision(point,0,0,0);
        _currentRoom.SetPositionTileAndCollision(point+new Vector2(0,16),0,3,0);
        FailIf(side.CanAppear(6,point,0),"Somaria side-view support requires full collision$f, not partial floor collision.");
        _currentRoom.SetPositionTileAndCollision(point+new Vector2(0,16),0,15,0);
        FailIf(!side.CanAppear(6,point,0),"Somaria side-view block must accept full floor support.");
        GD.Print("Validated isolated Somaria source offsets, all byte-height and hazard gates, tile alignment, shared underlying-buffer restoration, ownership loss and side-view floor support; item activation remains pending.");
    }
}
