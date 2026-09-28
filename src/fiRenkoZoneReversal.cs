// Version 1.0 - closed-bar zones and reversal signals. No order submission.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Gui;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;

namespace NinjaTrader.NinjaScript.Indicators.FreeIndicators
{
    public class fiRenkoZoneReversal : Indicator
    {
        private RzrEngine engine;
        private int lastProcessed = -1;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name = "fiRenkoZoneReversal";
                Description = "Automatic zones, distinct visits and bar-close reversals. Signal plot for Predator X.";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                IsAutoScale = false;
                DisplayInDataBox = true;
                ShowTransparentPlotsInDataBox = true;
                DrawOnPricePanel = true;
                PaintPriceMarkers = false;
                IsSuspendedWhileInactive = false;
                BarsRequiredToPlot = 0;
                MaximumBarsLookBack = MaximumBarsLookBack.Infinite;
                ZoneWidthTicks = 10;
                WickOvershootTicks = 0;
                DepartureTicks = 2;
                DepartureCloses = 2;
                MinimumBarsBetweenTouches = 3;
                MaximumBarsBetweenTouches = 100;
                EnableBreakoutRetest = false;
                ZoneOpacity = 15;
                MarkerOffsetTicks = 3;
                SupportColor = Brushes.SeaGreen;
                ResistanceColor = Brushes.IndianRed;
                AddPlot(Brushes.Transparent, "Signal");
            }
            else if (State == State.Configure)
            {
                Calculate = Calculate.OnBarClose;
                if (MinimumBarsBetweenTouches > MaximumBarsBetweenTouches)
                    throw new ArgumentException("The minimum bar count must be less than or equal to the maximum.");
            }
            else if (State == State.DataLoaded)
            {
                engine = new RzrEngine(new RzrSettings {
                    Width = ZoneWidthTicks, WickOvershoot = WickOvershootTicks,
                    Departure = DepartureTicks, DepartureCloses = DepartureCloses,
                    MinimumGap = MinimumBarsBetweenTouches, MaximumGap = MaximumBarsBetweenTouches,
                    Retest = EnableBreakoutRetest
                });
                lastProcessed = -1;
            }
        }

        protected override void OnBarUpdate()
        {
            if (BarsInProgress != 0)
                return;
            // Never expose an intrabar signal if a calling strategy overrides Calculate.
            if (Calculate != Calculate.OnBarClose)
            {
                Signal[0] = 0;
                return;
            }
            if (CurrentBar == lastProcessed)
                return;
            // A rewind requires a complete NinjaTrader recalculation of this instance.
            if (CurrentBar < lastProcessed)
                throw new InvalidOperationException("Bar history changed: reload NinjaScript data.");

            Signal[0] = 0;
            RzrResult result = engine.Step(new RzrBar(CurrentBar,
                InTicks(Open[0]), InTicks(High[0]), InTicks(Low[0]), InTicks(Close[0])));
            lastProcessed = CurrentBar;
            Signal[0] = result.Signal;

            foreach (int id in result.Removed)
            {
                RemoveDrawObject("RZR_Zone_" + id);
                RemoveDrawObject("RZR_Level_" + id);
            }
            foreach (RzrZone zone in engine.Zones)
            {
                Brush color = zone.IsSupport ? SupportColor : ResistanceColor;
                // Exact geometric width; a zero-width zone has no filled area.
                Draw.Rectangle(this, "RZR_Zone_" + zone.Id, false,
                    CurrentBar - zone.RoleStartBar, zone.Upper * TickSize,
                    -1, zone.Lower * TickSize, Brushes.Transparent, color, ZoneOpacity);
                if (zone.FirstSignalBar >= 0)
                {
                    Draw.Line(this, "RZR_Level_" + zone.Id, false,
                        CurrentBar - zone.FirstSignalBar, zone.Center * TickSize,
                        -1, zone.Center * TickSize, color, DashStyleHelper.Solid, 1);
                }
                else
                    RemoveDrawObject("RZR_Level_" + zone.Id);
            }
            if (result.Signal == 1)
                Draw.ArrowUp(this, "RZR_Long_" + CurrentBar, false, 0,
                    Low[0] - MarkerOffsetTicks * TickSize, SupportColor);
            else if (result.Signal == -1)
                Draw.ArrowDown(this, "RZR_Short_" + CurrentBar, false, 0,
                    High[0] + MarkerOffsetTicks * TickSize, ResistanceColor);
        }

        private double InTicks(double price) { return Math.Round(price / TickSize, 8); }

        [Browsable(false), XmlIgnore]
        public Series<double> Signal { get { return Values[0]; } }

        [NinjaScriptProperty, Range(0, 10000)]
        [Display(Name = "TOTAL zone width (ticks)", Order = 0, GroupName = "1. Zones")]
        public int ZoneWidthTicks { get; set; }

        [NinjaScriptProperty, Range(0, 10000)]
        [Display(Name = "Maximum wick overshoot (ticks)", Order = 1, GroupName = "1. Zones")]
        public int WickOvershootTicks { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Retest after breakout", Order = 2, GroupName = "1. Zones")]
        public bool EnableBreakoutRetest { get; set; }

        [NinjaScriptProperty, Range(0, 10000)]
        [Display(Name = "Departure distance (ticks)", Order = 0, GroupName = "2. Visits")]
        public int DepartureTicks { get; set; }

        [NinjaScriptProperty, Range(1, 1000)]
        [Display(Name = "Consecutive departure closes", Order = 1, GroupName = "2. Visits")]
        public int DepartureCloses { get; set; }

        [NinjaScriptProperty, Range(0, 100000)]
        [Display(Name = "Minimum bars BETWEEN touches", Order = 2, GroupName = "2. Visits")]
        public int MinimumBarsBetweenTouches { get; set; }

        [NinjaScriptProperty, Range(0, 100000)]
        [Display(Name = "Maximum bars BETWEEN touches", Order = 3, GroupName = "2. Visits")]
        public int MaximumBarsBetweenTouches { get; set; }

        [NinjaScriptProperty, Range(0, 100)]
        [Display(Name = "Zone opacity (%)", Order = 0, GroupName = "3. Visual")]
        public int ZoneOpacity { get; set; }

        [NinjaScriptProperty, Range(0, 100)]
        [Display(Name = "Arrow offset (ticks)", Order = 1, GroupName = "3. Visual")]
        public int MarkerOffsetTicks { get; set; }

        [XmlIgnore]
        [Display(Name = "Support / long color", Order = 2, GroupName = "3. Visual")]
        public Brush SupportColor { get; set; }

        [Browsable(false)]
        public string SupportColorSerializable
        {
            get { return Serialize.BrushToString(SupportColor); }
            set { SupportColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Resistance / short color", Order = 3, GroupName = "3. Visual")]
        public Brush ResistanceColor { get; set; }

        [Browsable(false)]
        public string ResistanceColorSerializable
        {
            get { return Serialize.BrushToString(ResistanceColor); }
            set { ResistanceColor = Serialize.StringToBrush(value); }
        }
    }

    // BEGIN TESTABLE ENGINE: prices are expressed in ticks; no platform dependencies.
    internal sealed class RzrSettings
    {
        public int Width, WickOvershoot, Departure, DepartureCloses, MinimumGap, MaximumGap;
        public bool Retest;
    }

    internal sealed class RzrBar
    {
        public readonly int Index;
        public readonly double Open, High, Low, Close;
        public RzrBar(int index, double open, double high, double low, double close)
        { Index = index; Open = open; High = high; Low = low; Close = close; }
    }

    internal sealed class RzrZone
    {
        public int Id, RoleStartBar, LastTouchBar, AwayCloses;
        public int FirstSignalBar = -1;
        public readonly double Center, Lower, Upper;
        public bool IsSupport, Armed, VisitEligible, VisitSignaled;
        public RzrZone(int id, bool support, double center, double width, int bar)
        {
            Id = id; IsSupport = support; Center = center;
            Lower = center - width / 2.0; Upper = center + width / 2.0;
            RoleStartBar = LastTouchBar = bar;
        }
    }

    internal sealed class RzrResult
    {
        public int Signal;
        public readonly List<int> Removed = new List<int>();
    }

    internal sealed class RzrEngine
    {
        private const double Epsilon = 0.00000001;
        private readonly RzrSettings settings;
        private RzrBar previous;
        private int nextId;
        public readonly List<RzrZone> Zones = new List<RzrZone>();

        public RzrEngine(RzrSettings settings)
        {
            if (settings.Width < 0 || settings.WickOvershoot < 0 || settings.Departure < 0
                || settings.DepartureCloses < 1 || settings.MinimumGap < 0
                || settings.MaximumGap < settings.MinimumGap)
                throw new ArgumentException("Invalid zone settings");
            this.settings = settings;
        }

        public RzrResult Step(RzrBar bar)
        {
            if (previous != null && bar.Index != previous.Index + 1)
                throw new ArgumentException("Bars must be consecutive and closed");
            var result = new RzrResult();
            bool bullish = previous != null && bar.Close > bar.Open && previous.Close < previous.Open;
            bool bearish = previous != null && bar.Close < bar.Open && previous.Close > previous.Open;
            var candidates = new List<RzrZone>();
            var brokenThisBar = new List<RzrZone>();
            foreach (RzrZone zone in Zones)
            {
                int gap = bar.Index - zone.LastTouchBar - 1;
                // Expiry is checked before a late contact can revive an old level.
                if (gap > settings.MaximumGap)
                {
                    result.Removed.Add(zone.Id);
                    continue;
                }

                bool touch = bar.High >= zone.Lower - Epsilon && bar.Low <= zone.Upper + Epsilon;
                bool broken = zone.IsSupport
                    ? bar.Close < zone.Lower - Epsilon : bar.Close > zone.Upper + Epsilon;
                if (broken)
                {
                    if (touch) zone.LastTouchBar = bar.Index;
                    brokenThisBar.Add(zone);
                    if (!settings.Retest)
                        result.Removed.Add(zone.Id);
                    else
                    {
                        zone.IsSupport = !zone.IsSupport;
                        zone.RoleStartBar = bar.Index;
                        zone.FirstSignalBar = -1;
                        zone.Armed = zone.VisitEligible = zone.VisitSignaled = false;
                        zone.AwayCloses = 0;
                        UpdateDeparture(zone, bar.Close);
                    }
                    continue; // A breakout can never be a rejection signal of this zone.
                }

                if (touch)
                {
                    if (zone.Armed)
                    {
                        zone.VisitEligible = gap >= settings.MinimumGap && gap <= settings.MaximumGap;
                        zone.VisitSignaled = false;
                        zone.Armed = false;
                        zone.AwayCloses = 0;
                    }
                    zone.LastTouchBar = bar.Index;
                    bool reversal = zone.IsSupport ? bullish : bearish;
                    bool wickFits = zone.IsSupport
                        ? bar.Low >= zone.Lower - settings.WickOvershoot - Epsilon
                        : bar.High <= zone.Upper + settings.WickOvershoot + Epsilon;
                    if (zone.VisitEligible && !zone.VisitSignaled && reversal && wickFits)
                        candidates.Add(zone);
                }

                // Departure needs consecutive closes, not entire candles outside.
                // Contacts still update LastTouchBar even during departure.
                UpdateDeparture(zone, bar.Close);
            }

            bool hasLong = candidates.Exists(delegate(RzrZone z) { return z.IsSupport; });
            bool hasShort = candidates.Exists(delegate(RzrZone z) { return !z.IsSupport; });
            if (hasLong != hasShort)
            {
                result.Signal = hasLong ? 1 : -1;
                foreach (RzrZone zone in candidates)
                {
                    zone.VisitSignaled = true;
                    if (zone.FirstSignalBar < 0) zone.FirstSignalBar = bar.Index;
                }
            }
            Zones.RemoveAll(delegate(RzrZone z) { return result.Removed.Contains(z.Id); });

            // Process existing zones first: creation never signals on its own bar.
            if (bullish || bearish)
            {
                double center = bullish ? bar.Low : bar.High;
                bool alreadyCovered = Zones.Exists(delegate(RzrZone z) {
                    return center >= z.Lower - Epsilon && center <= z.Upper + Epsilon;
                });
                // Do not silently recreate a broken level as its opposite on the same bar
                // when optional breakout retests have been disabled.
                bool justBroken = brokenThisBar.Exists(delegate(RzrZone z) {
                    return center >= z.Lower - Epsilon && center <= z.Upper + Epsilon;
                });
                if (!alreadyCovered && !justBroken)
                {
                    var zone = new RzrZone(++nextId, bullish, center, settings.Width, bar.Index);
                    UpdateDeparture(zone, bar.Close);
                    Zones.Add(zone);
                }
            }
            previous = bar;
            return result;
        }

        private void UpdateDeparture(RzrZone zone, double close)
        {
            if (zone.Armed) return;
            double distance = zone.IsSupport ? close - zone.Upper : zone.Lower - close;
            bool farEnough = distance > Epsilon && distance + Epsilon >= settings.Departure;
            zone.AwayCloses = farEnough ? zone.AwayCloses + 1 : 0;
            if (zone.AwayCloses >= settings.DepartureCloses)
            {
                zone.Armed = true;
                zone.VisitEligible = false;
            }
        }
    }
}
