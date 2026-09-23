using System.Collections.Generic;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateSeedShooterEyeStatueState()
    {
        var data = new SeedShooterEyeStatueDatabase();
        var crown = data.GetRoomRecords(4,0xba);
        FailIf(crown.Count != 3 || data.GetRoomRecords(4,0xc6).Count != 4,
            "PART $46 typed database lost Crown/shared placements.");
        foreach (var record in crown)
        {
            int triggers = 0x80; // Other owners' bits must survive.
            var writes = new List<(int Bit,bool Set)>();
            var statue = new SeedShooterEyeStatueState(record.Subid,record.ActiveCounter,(bit,set) =>
            {
                writes.Add((bit,set));
                if (set) triggers |= 1 << bit; else triggers &= ~(1 << bit);
            });
            statue.Update(false);
            FailIf(!statue.Initialized || statue.Visible || statue.Counter != 0 || writes.Count != 0,
                "PART $46 state0 must initialize without clearing triggers or activating its eye.");
            statue.Update(false);
            FailIf(writes.Count != 1 || writes[0] != (record.Subid,false) || triggers != 0x80,
                "Idle PART $46 must clear only its own trigger on every eligible update.");
            statue.Update(true);
            FailIf(!statue.Visible || statue.Counter != 0x2c || triggers != (0x80 | (1 << record.Subid)),
                "Initialized PART $46 must load $2d then decrement to $2c on the activation update.");
            for (int i = 0; i < 43; i++) statue.Update(false);
            FailIf(!statue.Visible || statue.Counter != 1,"PART $46 must remain active through counter $01.");
            statue.Update(true); // A new accepted status wins over expiry.
            FailIf(!statue.Visible || statue.Counter != 0x2c,"PART $46 repeated status must reload before decrementing.");
            for (int i = 0; i < 43; i++) statue.Update(false);
            statue.Update(false);
            FailIf(statue.Visible || statue.Counter != 0 || triggers != 0x80,
                "PART $46 must clear its trigger and hide on the counter-zero update.");
            int beforeIdle = writes.Count;
            statue.Update(false);
            FailIf(writes.Count != beforeIdle + 1,"Expired PART $46 continues clearing its trigger while idle.");
            // partCode46 branches on NZ, so DEAD also repeatedly reloads;
            // it does not distinguish that status from JUST_HIT.
            statue.Update(true); statue.Update(true);
            FailIf(statue.Counter != 0x2c || !statue.Visible,"PART $46 must preserve consecutive nonzero-status dispatch.");
        }
        Godot.GD.Print("Validated PART $46 isolated initialization, trigger lifetime, expiry and repeated-status ordering; collision integration remains separate.");
    }
}
