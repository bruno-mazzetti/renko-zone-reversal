// Minimal host to exercise the actual indicator adapter without starting NT8.
using System;
using System.Collections.Generic;
using System.Windows.Media;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators.FreeIndicators;
using NinjaTrader.NinjaScript.DrawingTools;

namespace NinjaTrader.Gui
{
    public enum DashStyleHelper { Solid }
    public static class Serialize
    {
        public static string BrushToString(Brush b) { return b.ToString(); }
        public static Brush StringToBrush(string s) { return Brushes.Black; }
    }
}
namespace NinjaTrader.NinjaScript
{
    public enum State { SetDefaults, Configure, DataLoaded }
    public enum Calculate { OnBarClose, OnEachTick }
    public enum MaximumBarsLookBack { Infinite }
    public class NinjaScriptPropertyAttribute : Attribute { }
    public class Series<T>
    {
        private readonly Indicators.Indicator owner;
        private readonly Dictionary<int,T> data = new Dictionary<int,T>();
        public Series(Indicators.Indicator o) { owner=o; }
        public T this[int barsAgo]
        {
            get { T result; return data.TryGetValue(owner.CurrentBar-barsAgo,out result) ? result : default(T); }
            set { data[owner.CurrentBar-barsAgo]=value; }
        }
    }
}
namespace NinjaTrader.NinjaScript.Indicators
{
    public class Indicator
    {
        public State State;
        public Calculate Calculate;
        public MaximumBarsLookBack MaximumBarsLookBack;
        public string Name, Description, PlotName;
        public bool IsOverlay, IsAutoScale, DisplayInDataBox, ShowTransparentPlotsInDataBox,
            DrawOnPricePanel, PaintPriceMarkers, IsSuspendedWhileInactive;
        public int BarsRequiredToPlot, BarsInProgress, CurrentBar=-1;
        public double TickSize=0.25;
        public Series<double> Open,High,Low,Close;
        public List<Series<double>> Values=new List<Series<double>>();
        public Indicator()
        { Open=new Series<double>(this); High=new Series<double>(this); Low=new Series<double>(this); Close=new Series<double>(this); }
        protected virtual void OnStateChange() { }
        protected virtual void OnBarUpdate() { }
        protected void AddPlot(Brush brush,string name) { PlotName=name; Values.Add(new Series<double>(this)); }
        protected void RemoveDrawObject(string tag) { Draw.Objects.Remove(tag); }
        public void SetState(State state) { State=state; OnStateChange(); }
        public void Feed(double o,double h,double l,double c)
        { CurrentBar++; Open[0]=o; High[0]=h; Low[0]=l; Close[0]=c; OnBarUpdate(); }
        public void Repeat() { OnBarUpdate(); }
    }
}
namespace NinjaTrader.NinjaScript.DrawingTools
{
    public class Record { public string Kind; public int Start,End; public double Top,Bottom; }
    public static class Draw
    {
        public static Dictionary<string,Record> Objects=new Dictionary<string,Record>();
        public static void Rectangle(object o,string tag,bool scale,int start,double top,int end,double bottom,Brush outline,Brush area,int opacity)
        { Objects[tag]=new Record{Kind="Zone",Start=start,End=end,Top=top,Bottom=bottom}; }
        public static void Line(object o,string tag,bool scale,int start,double top,int end,double bottom,Brush color,NinjaTrader.Gui.DashStyleHelper dash,int width)
        { Objects[tag]=new Record{Kind="Line",Start=start,End=end,Top=top,Bottom=bottom}; }
        public static void ArrowUp(object o,string tag,bool scale,int start,double price,Brush color)
        { Objects[tag]=new Record{Kind="Long",Top=price}; }
        public static void ArrowDown(object o,string tag,bool scale,int start,double price,Brush color)
        { Objects[tag]=new Record{Kind="Short",Top=price}; }
    }
}
class ZoneAdapterTests
{
    static int checks;
    static void Check(bool v,string m) { checks++; if(!v) throw new Exception(m); }
    static void Main()
    {
        Draw.Objects.Clear();
        var indicator=new fiRenkoZoneReversal();
        indicator.SetState(State.SetDefaults);
        indicator.ZoneWidthTicks=10;
        indicator.DepartureTicks=0;
        indicator.DepartureCloses=1;
        indicator.MinimumBarsBetweenTouches=1;
        indicator.MaximumBarsBetweenTouches=10;
        indicator.SetState(State.Configure); indicator.SetState(State.DataLoaded);
        Check(indicator.PlotName=="Signal" && indicator.Values.Count==1,"Public native Signal plot");
        Check(indicator.ShowTransparentPlotsInDataBox && indicator.DisplayInDataBox,"Readable plot values");
        Check(!indicator.IsAutoScale && !indicator.IsSuspendedWhileInactive,"No price-axis or inactive-chart interference");
        indicator.Feed(22.5,25,22.25,24.5);
        indicator.Feed(24.5,25,22.25,22.5);
        Check(indicator.Signal[0]==0,"Creation has zero plot");
        Check(Draw.Objects.ContainsKey("RZR_Zone_1") && !Draw.Objects.ContainsKey("RZR_Level_1"),"Band before any line");
        var zone=Draw.Objects["RZR_Zone_1"];
        Check(zone.Top==26.25 && zone.Bottom==23.75,"TickSize and total-width mapping");
        indicator.Feed(22.25,23,22,22.75);
        indicator.Feed(24.75,25,22.25,22.5);
        Check(indicator.Signal[0]==-1 && indicator.Signal[1]==0,"Signal on actual reversal bar");
        Check(Draw.Objects.ContainsKey("RZR_Short_3"),"Arrow matches plot");
        Check(Draw.Objects.ContainsKey("RZR_Level_1") && Draw.Objects["RZR_Level_1"].Start==0,"Line starts at signal");
        indicator.Repeat(); Check(indicator.Signal[0]==-1,"Duplicate notification preserves signal");
        indicator.Feed(22.5,23,21.5,22);
        Check(indicator.Signal[0]==0 && indicator.Signal[1]==-1,"Pulse reset retains closed signal");
        indicator.Feed(25,28,24,27.5);
        Check(!Draw.Objects.ContainsKey("RZR_Zone_1") && !Draw.Objects.ContainsKey("RZR_Level_1"),"Break removes zone and level");
        Check(Draw.Objects.ContainsKey("RZR_Short_3"),"Past arrow survives zone break");
        Check(indicator.Signal[0]==0,"No entry on breakout");
        indicator.Calculate=Calculate.OnEachTick;
        indicator.Feed(27,28,24,25);
        Check(indicator.Signal[0]==0,"Intrabar parent cannot generate signals");
        Console.WriteLine("PASS: " + checks + " indicator-adapter assertions (simulated host)");
    }
}
