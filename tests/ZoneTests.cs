using System;
using System.Collections.Generic;
using NinjaTrader.NinjaScript.Indicators.FreeIndicators;

class ZoneTests
{
    static int checks;
    static void Check(bool v, string message)
    { checks++; if (!v) throw new Exception(message); }
    static RzrSettings Settings()
    { return new RzrSettings { Width=10, WickOvershoot=0, Departure=2,
        DepartureCloses=2, MinimumGap=1, MaximumGap=20, Retest=false }; }
    class Rig
    {
        public RzrEngine E;
        public int Index;
        public Rig(RzrSettings s) { E = new RzrEngine(s); }
        public RzrZone Seed(bool support, bool armed)
        {
            var z = new RzrZone(999, support, 100, 10, -1);
            z.Armed = armed;
            E.Zones.Add(z);
            return z;
        }
        public RzrResult B(double o, double h, double l, double c)
        { return E.Step(new RzrBar(Index++, o, h, l, c)); }
    }

    static void Main()
    {
        // Automatic first reversal creates a fixed level, without a signal.
        var s=Settings(); var r=new Rig(s);
        Check(r.B(90,100,89,98).Signal==0, "Initial bar");
        Check(r.B(98,101,90,92).Signal==0, "Creation cannot signal");
        Check(r.E.Zones.Count==1 && !r.E.Zones[0].IsSupport && r.E.Zones[0].Center==101,
            "Resistance is reversal bar high");
        Check(r.E.Zones[0].Lower==96 && r.E.Zones[0].Upper==106, "Total width");
        r.B(92,100,90,98); r.B(98,104,94,96);
        Check(r.E.Zones[0].Center==101, "Nearby reversal cannot move center");

        // Short wick touch plus close below the zone is accepted.
        r=new Rig(Settings()); var z=r.Seed(false,true);
        r.B(88,92,87,90);
        Check(r.B(99,105,89,90).Signal==-1, "Boundary wick and rejection-side close");
        Check(z.FirstSignalBar==1 && z.VisitSignaled, "Record signal bar");

        // Reversal may occur after initial contact inside the same visit.
        r=new Rig(Settings()); z=r.Seed(false,true);
        r.B(88,92,87,90);
        Check(r.B(90,100,89,98).Signal==0 && z.VisitEligible, "Touch without reversal arms visit");
        Check(r.B(98,102,95,97).Signal==-1, "Delayed reversal, spacing frozen");
        r.B(97,103,95,99);
        Check(r.B(99,103,94,96).Signal==0, "No duplicate in accumulation");
        r.B(93,94,88,90); r.B(90,94,89,93);
        Check(z.Armed, "Departure rearms");
        Check(r.B(99,102,88,91).Signal==-1, "Another separated visit can signal");
        Check(z.FirstSignalBar==2, "Level line remains anchored to first signal");

        // A too-early visit never becomes eligible simply by waiting inside.
        s=Settings(); s.MinimumGap=3; r=new Rig(s); z=r.Seed(false,true);
        r.B(88,92,87,90); r.B(90,100,89,98);
        Check(!z.VisitEligible && z.LastTouchBar==1, "Early contact updates touch but is ineligible");
        r.B(98,103,95,96); r.B(96,103,95,99);
        Check(r.B(99,104,95,96).Signal==0, "Waiting inside cannot cure an early entry");

        // Departure is consecutive closes, with wick contacts retaining latest touch.
        r=new Rig(Settings()); z=r.Seed(false,false);
        r.B(90,100,89,93);
        Check(!z.Armed && z.AwayCloses==1 && z.LastTouchBar==0, "Wick contact during departure");
        r.B(93,100,90,94);
        Check(z.AwayCloses==0, "Insufficient exit distance resets close count");
        r.B(94,100,89,93); r.B(93,100,90,92);
        Check(z.Armed && z.LastTouchBar==3, "Two closes rearms despite wicks");

        s=Settings(); s.Departure=0; s.DepartureCloses=1;
        r=new Rig(s); z=r.Seed(false,false);
        r.B(96,100,94,95); Check(!z.Armed, "On border is not outside");
        r.B(95,100,93,94); Check(z.Armed, "Zero distance needs strictly outside close");

        // Wick excess: reject this bar, keep the level; later valid reversal may signal.
        r=new Rig(Settings()); z=r.Seed(false,true);
        r.B(88,92,87,90);
        Check(r.B(99,106,96,97).Signal==0 && r.E.Zones.Contains(z), "Wick excess isn't a close breakout");
        r.B(97,103,96,99);
        Check(r.B(99,103,95,96).Signal==-1, "Eligible visit survives invalid wick");
        s=Settings(); s.WickOvershoot=1; r=new Rig(s); z=r.Seed(false,true);
        r.B(88,92,87,90);
        Check(r.B(99,106,96,97).Signal==-1, "Exact permitted overshoot accepted");

        // Wrong-side close invalidates even a reversal and large wick allowance.
        s=Settings(); s.WickOvershoot=100; r=new Rig(s); z=r.Seed(false,true);
        r.B(88,92,87,90);
        var res=r.B(110,112,100,106);
        Check(res.Signal==0 && res.Removed.Contains(999) && !r.E.Zones.Contains(z), "Close breakout wins");

        // Boundary close is accepted; mirrored long is accepted above zone.
        r=new Rig(Settings()); z=r.Seed(false,true); r.B(88,92,87,90);
        Check(r.B(106,107,100,105).Signal==0, "Zero wick tolerance still applies at boundary close");
        r=new Rig(Settings()); z=r.Seed(true,true); r.B(112,113,108,110);
        Check(r.B(101,112,95,110).Signal==1, "Long boundary wick and close above zone");

        // Optional role inversion. Breakout close starts departure; last actual touch counts.
        s=Settings(); s.Retest=true; s.MinimumGap=1;
        r=new Rig(s); z=r.Seed(false,true); r.B(88,92,87,90);
        Check(r.B(100,112,99,110).Signal==0 && z.IsSupport && z.LastTouchBar==1,
            "Flip but never signal on breakout");
        Check(z.AwayCloses==1, "Breakout counts as first departure close");
        r.B(112,114,108,109); Check(z.Armed, "Second exit close rearms new support");
        Check(r.B(100,112,95,110).Signal==1, "Support retest after resistance break");
        Check(z.FirstSignalBar==3, "New role line begins at retest");
        r.B(100,101,85,90);
        Check(!z.IsSupport && z.FirstSignalBar==-1 && !z.VisitSignaled, "Second break flips and clears role state");

        // Retest-off cannot auto-recreate the same broken level on the break bar.
        r=new Rig(Settings()); z=r.Seed(false,true); r.B(92,93,88,90);
        res=r.B(100,112,100,110);
        Check(res.Removed.Contains(999) && r.E.Zones.Count==0, "Retest-off recreation guard");

        // Inclusive max spacing, expiry only after max has been exceeded.
        s=Settings(); s.MaximumGap=2; r=new Rig(s); z=r.Seed(false,true);
        r.B(88,92,87,90); r.B(88,92,87,90);
        Check(r.B(99,100,89,90).Signal==-1, "Maximum gap inclusive");
        r=new Rig(s); z=r.Seed(false,true);
        r.B(88,92,87,90); r.B(88,92,87,90); r.B(88,92,87,90);
        res=r.B(99,100,89,90);
        Check(res.Signal==0 && res.Removed.Contains(999), "Late touch cannot revive expired level");

        // Width zero and odd widths retain exact geometry.
        foreach(int width in new[]{0,1,3,10})
        {
            s=Settings(); s.Width=width; s.DepartureCloses=1; s.Departure=0;
            r=new Rig(s); r.B(90,100,89,98); r.B(98,100,89,90);
            z=r.E.Zones[0];
            Check(z.Upper-z.Lower==width && z.Center==100, "Width " + width);
            r.B(89,92,88,91);
            Check(r.B(99,100,89,90).Signal==-1, "Exact touch works with width " + width);
        }

        // Doji interrupts the immediate color-change pattern, not the existing visit.
        r=new Rig(Settings()); z=r.Seed(false,true); r.B(88,92,87,90);
        r.B(98,102,97,98);
        Check(r.B(98,102,95,96).Signal==0, "Doji isn't green");

        // Bar-creation color and one-sided logic are symmetrical on deterministic OHLC.
        // Replay checks exercise many overlapping/expiring/flipping levels.
        for(int seed=0;seed<8;seed++)
        {
            var random=new Random(seed); s=Settings(); s.Retest=(seed%2==0);
            s.Width=seed; s.MinimumGap=0; s.MaximumGap=15; s.DepartureCloses=1;
            var a=new RzrEngine(s); var b=new RzrEngine(s); var replay=new RzrEngine(s);
            double price=100;
            for(int i=0;i<600;i++)
            {
                double o=price; double c=o+random.Next(-5,6);
                double h=Math.Max(o,c)+random.Next(0,5), l=Math.Min(o,c)-random.Next(0,5);
                var bar=new RzrBar(i,o,h,l,c);
                var ra=a.Step(bar); var rr=replay.Step(bar);
                var rb=b.Step(new RzrBar(i,200-o,200-l,200-h,200-c));
                Check(ra.Signal==rr.Signal && a.Zones.Count==replay.Zones.Count, "Replay determinism");
                Check(ra.Signal==-rb.Signal && a.Zones.Count==b.Zones.Count, "Long/short mirror");
                Check(ra.Signal>=-1 && ra.Signal<=1, "Signal domain");
                foreach(var za in a.Zones)
                {
                    Check(za.LastTouchBar<=i && za.RoleStartBar<=i, "No future anchors");
                    Check(za.Lower<=za.Center && za.Center<=za.Upper, "Geometry invariant");
                }
                price=c;
            }
        }
        Console.WriteLine("PASS: " + checks + " zone-engine assertions");
    }
}
