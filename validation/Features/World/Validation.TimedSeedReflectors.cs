using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateTimedSeedReflectors()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput",flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates",flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate",flags)!.CreateDelegate(typeof(Action),this);
        var data = new DungeonMechanicDatabase();
        var records = data.GetRoomRecords(4,0xa6).Where(r=>r.Id==0x33).ToArray();
        FailIf(records.Length!=2 || records[0].Order!=2 || records[0].SubId!=0x08 || records[0].PackedPosition!=0x26 ||
            records[1].Order!=3 || records[1].SubId!=0x88 || records[1].PackedPosition!=0x86 || data.SeedBouncerPeriod!=60,
            "Crown timed reflectors require source PART$33:$08/$88 at $26/$86 and period$3c.");
        foreach (bool batch in new[] { false,true })
        {
            void Step(int count=1)
            {
                input.CaptureForValidation([],[],Vector2.Zero);
                if (batch) scheduler.Advance(count/60.0,update);
                else for(int i=0;i<count;i++) scheduler.Advance(1.0/60.0,update);
            }
            LoadValidationRoom(4,0xa6);
            _player.WarpTo(new(56,88));
            FailIf(_currentRoom.IsSolid(_player.Position),"Timed-reflector fixture must place Link on floor.");
            var parents = _entities.Entities<RotatableSeedThingRoomEntity>().OrderBy(p=>p.Position.Y).ToArray();
            FailIf(parents.Length!=2 || parents.Any(p=>p.Initialized),"Timed reflectors must wait for their first PART dispatch.");
            Step();
            var children = _entities.Entities<SeedReflectorChildRoomEntity>().OrderBy(p=>p.Position.Y).ToArray();
            FailIf(children.Length!=2 || children[0].Position!=new Vector2(104,52) || children[1].Position!=new Vector2(104,148) ||
                children.Any(c=>!c.Initialized || c.CollisionRadii!=Vector2.Zero) ||
                parents.Any(p=>!p.Initialized || p.Counter!=60 || p.Orientation!=2),
                "Parent initialization must allocate children that initialize later in the same native PART pass with zero radii.");
            Step();
            FailIf(children.Any(c=>c.CollisionRadii!=new Vector2(6,4) || c.SeedBounceOrientation!=2),
                "The next child dispatch must copy parent collision radii and animation parameter.");
            var empty = new List<RoomEntitySpawn>();
            Rect2 bounds = new(children[0].Position-Vector2.One*2,Vector2.One*4);
            FailIf(children[0].ApplySeedHitAtHeight(bounds,Vector2.Zero,0,0x1a,empty)!=SeedHitResult.None ||
                children[0].ApplySeedHitAtHeight(bounds,Vector2.Zero,-14,0x1a,empty)!=SeedHitResult.Bounce,
                "PART$33:03 at Z$f2 must reflect a height-matched seed but not a ground-level shooter seed.");
            var shooter = SeedShooterRecord.Load();
            var ember = new SeedSatchelDatabase().Ember;
            Vector2 edge = parents[0].Position+Vector2.Left*10;
            var seed = _entities.Spawn<EmberSeedEffect>(new EmberSeedSpawn(edge-shooter.Offsets[3],Vector2I.Up,ember,4,SeedLaunchKind.Shooter,3));
            Step(2);
            FailIf(seed.State!=EmberState.Flying || seed.Angle!=1 || seed.BouncesRemaining!=2,
                "Crown's timed reflector must reflect the real shooter projectile at its asymmetric left boundary.");
            seed.OnCollision(SeedHitResult.Consume);
            Step(parents[0].Counter-1);
            FailIf(parents.Any(p=>p.Counter!=1 || p.Orientation!=2),"Timed reflectors rotated before update60.");
            var text = _entities.TextActiveSource;
            try { _entities.TextActiveSource=()=>true; Step(8); FailIf(parents.Any(p=>p.Counter!=1),"Text must freeze initialized reflector counters."); }
            finally { _entities.TextActiveSource=text; }
            Step();
            FailIf(parents[0].Orientation!=3 || parents[1].Orientation!=1 || parents.Any(p=>p.Counter!=60) ||
                children[0].SeedBounceOrientation!=3 || children[1].SeedBounceOrientation!=1,
                "Update60 must rotate $08 forward/$88 backward and update child parameters in the same PART pass.");
            Step(60);
            FailIf(parents.Any(p=>p.Orientation!=0 || p.Counter!=60),"Second timed quarter-turn must wrap both parents to orientation0.");

            bool allocate=false; int attempts=0;
            var isolated = new RotatableSeedThingRoomEntity(records[0],data,
                new DungeonInteractionVisualDatabase().Visual("rotatable-seed-thing"),_currentRoom,_runtimeState,
                ()=>0,(_,_,_)=>{ attempts++; return allocate; });
            var frame = new RoomEntityFrame(_player,0,false);
            for(int i=0;i<4;i++) isolated.UpdateFrame(frame,empty);
            FailIf(isolated.Initialized || isolated.Counter!=60 || attempts!=4 || isolated.Orientation!=2,
                "A full child pool must retry state0, reloading its initial timer and orientation each update.");
            allocate=true; isolated.UpdateFrame(frame,empty);
            FailIf(!isolated.Initialized || isolated.Counter!=60 || attempts!=5,"Successful child allocation alone may complete parent initialization.");
            isolated.Free();
            // Isolate incoming-room initialization; no dungeon traversal.
            LoadValidationRoom(4,0xa6);
            _player.WarpTo(new(56,88));
            _entities.BeginScreenTransition(4,_currentRoom,new(240,0),_player);
            parents = _entities.Entities<RotatableSeedThingRoomEntity>().OrderBy(p=>p.Position.Y).ToArray();
            children = _entities.Entities<SeedReflectorChildRoomEntity>().OrderBy(p=>p.Position.Y).ToArray();
            FailIf(parents.Length!=2 || children.Length!=2 ||
                parents.Any(p=>!p.Initialized || !p.Visible || p.Counter!=60 || p.Orientation!=2) ||
                children.Any(c=>!c.Initialized || c.Visible || c.CollisionRadii!=Vector2.Zero) ||
                children[0].Position!=new Vector2(104,52) || children[1].Position!=new Vector2(104,148),
                "Scrolling preload must initialize visible PART$33 parents and hidden zero-radius children in source order.");
            Step(32);
            FailIf(parents.Any(p=>p.Counter!=60 || p.Orientation!=2) ||
                children.Any(c=>c.CollisionRadii!=Vector2.Zero),
                "Initialized reflector parents and children must freeze throughout scrolling.");
            _entities.FinishScreenTransition();
            Step();
            FailIf(parents.Any(p=>p.Counter!=59 || p.Orientation!=2) ||
                children.Any(c=>c.CollisionRadii!=new Vector2(6,4) || c.SeedBounceOrientation!=2),
                "First post-scroll dispatch must decrement the parent and copy child collisions without reinitializing.");
            LoadValidationRoom(0,0x60);
            FailIf(_entities.Entities<SeedReflectorChildRoomEntity>().Count!=0,"Room departure must remove reflector children.");
        }
    }
}
