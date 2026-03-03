#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.Gui.Tools;
#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
    public enum Po3RangeType
    {
        Range3, Range9, Range27, Range81, Range243, Range729,
        Range2187, Range6561, Range19683, Range59049, Range177147, Range531441, Custom
    }
    
    public enum InfoBoxPosition
    {
        TopLeft, TopRight, BottomLeft, BottomRight
    }
    
    public enum Po3StopRunSize
    {
        PO3Minus2,  // e.g., 729 range uses 81 (729÷9)
        PO3Minus3,  // e.g., 729 range uses 27 (729÷27)
        PO3Minus4   // e.g., 729 range uses 9 (729÷81)
    }
    
    public class RedTailGoldbachLevels : Indicator
    {
        private DateTime currentSessionStart = DateTime.MinValue;
        private double fixPrice = 0;
        private double rangeSize = 0;
        
        private readonly int[] discountLevels = { 0, 3, 11, 17, 29, 41, 47 };
        private readonly int[] premiumLevels = { 100, 97, 89, 83, 71, 59, 53 };
        private readonly int[] nonGoldbachLevels = { 23, 35, 65, 77 }; // Semi-prime levels
        private readonly int[] invertedGoldbachLevels = { 14, 32, 38, 56, 74, 79, 92, 95, 98 }; // Inverted Goldbach levels (p.32-34)
        private readonly string[] pdAreas = { "HIGH", "REJECTION", "ORDER BLOCK", "FVG", "LIQ VOID", "BREAKER", "MITIGATION" };

        private bool enableLevelMerging;
        private double mergingThreshold;
        private bool useAutoScaling;
        private bool autoDetectSettlementTime;
        private bool debugMode;
        private int labelFontSize;
        private bool showPriceOnLabels;
        private int infoBoxFontSize;
        private bool useDynamicMode;
        private bool showNonGoldbachLevels;
        private bool showMidpointLevels;
        private bool showInvertedGoldbachLevels;
        private int nonGoldbachLineWidth;
        private DashStyleHelper nonGoldbachLineStyle;
        private int invertedGoldbachLineWidth;
        private DashStyleHelper invertedGoldbachLineStyle;
        private int midpointLineWidth;
        private DashStyleHelper midpointLineStyle;
        private bool showPo3StopRuns;
        private bool showPo3StdvLevels;
        private Po3StopRunSize po3StopRunSize;
        private int po3StdvInterval;
        private Brush po3StopRunColor;
        private Brush po3StdvColor;
        private Brush invertedGoldbachColor;
        private int historicalBarsToShow;
        private double po3Shift;
        private bool showHiddenRange;
        
        private double calculatedADR = 0;
        private Po3RangeType recommendedPO3 = Po3RangeType.Range729;
        private Po3RangeType activePO3 = Po3RangeType.Range729;
        private DateTime lastADRCalculationDate = DateTime.MinValue;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"RedTail Goldbach Po3 Levels - Dynamic/Fixed modes with PO3 ranges";
                Name = "RedTail Goldbach Po3 Levels";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = true;
                PaintPriceMarkers = true;
                ScaleJustification = ScaleJustification.Right;
                IsSuspendedWhileInactive = true;
                
                Po3Range = Po3RangeType.Range729;
                CustomPo3Value = 729;
                UseDynamicMode = true; // Default to dynamic mode
                
                UseManualFixPrice = false;
                ManualFixPrice = 0;
                FixPriceOffset = 0;
                
                FixPriceColor = Brushes.Red;
                PremiumColor = Brushes.DodgerBlue;
                DiscountColor = Brushes.Orange;
                EquilibriumColor = Brushes.Yellow;
                NonGoldbachColor = Brushes.Gray;
                MidpointColor = Brushes.White;
                FireEmojiColor = Brushes.Red;
				
                BarsToRight = 20;
                ProjectionOffset = 24;
                HistoricalBarsToShow = 0;
                FixLineWidth = 3;
                FixLineStyle = DashStyleHelper.Solid;
                PremiumLineWidth = 1;
                PremiumLineStyle = DashStyleHelper.Dot;
                DiscountLineWidth = 1;
                DiscountLineStyle = DashStyleHelper.Dot;
                BoundaryLineWidth = 2;
                BoundaryLineStyle = DashStyleHelper.Solid;
                NonGoldbachLineWidth = 1;
                NonGoldbachLineStyle = DashStyleHelper.Dash;
                MidpointLineWidth = 1;
                MidpointLineStyle = DashStyleHelper.Dot;
                
                ShowLabels = true;
                ShowPdAreas = true;
                ShowEquilibrium = true;
                LabelFontSize = 9;
                ShowPriceOnLabels = true;
                
                // Advanced Levels
                ShowNonGoldbachLevels = false;
                ShowMidpointLevels = false;
                ShowInvertedGoldbachLevels = false;
                InvertedGoldbachColor = Brushes.MediumPurple;
                InvertedGoldbachLineWidth = 1;
                InvertedGoldbachLineStyle = DashStyleHelper.DashDot;
                
                // PO3 DR Shift & Hidden Range
                Po3Shift = 0;
                ShowHiddenRange = false;
                
                // PO3 Stop Runs
                ShowPo3StopRuns = false;
                ShowPo3StdvLevels = false;
                Po3StopRunSize = NinjaTrader.NinjaScript.Indicators.Po3StopRunSize.PO3Minus2;
                Po3StdvInterval = 27;
                Po3StopRunColor = Brushes.Magenta;
                Po3StdvColor = Brushes.Cyan;
                
                EnableLevelMerging = true;
                MergingThreshold = 5.0;
                UseAutoScaling = true;
                
                AutoCalculatePO3 = false;
                ADRLookbackPeriod = 20;
                ShowInfoBox = true;
                InfoBoxLocation = InfoBoxPosition.BottomRight;
                InfoBoxFontSize = 10;
                
                SettlementHour = 16;
                SettlementMinute = 0;
                SessionStartHour = 18;
                SessionStartMinute = 0;
                SettlementTimeZone = "Eastern Standard Time";
                AutoDetectSettlementTime = true;
                
                DebugMode = false;
            }
            else if (State == State.Configure)
            {
                AddDataSeries(BarsPeriodType.Minute, 1);
            }
            else if (State == State.DataLoaded)
            {
                activePO3 = Po3Range;
            }
        }

        protected override void OnBarUpdate()
        {
            if (BarsInProgress != 0 || CurrentBars[0] < 1)
                return;

            bool shouldCalculateADR = false;
            if (CurrentBar >= ADRLookbackPeriod && Time[0].Date != lastADRCalculationDate)
            {
                shouldCalculateADR = true;
                lastADRCalculationDate = Time[0].Date;
            }

            if (shouldCalculateADR && (AutoCalculatePO3 || ShowInfoBox))
            {
                CalculateOptimalPO3();
            }
            
            activePO3 = AutoCalculatePO3 ? recommendedPO3 : Po3Range;

            // Check for new trading session
            DateTime newSessionStart = GetCurrentSessionStart();
            
            if (newSessionStart != currentSessionStart)
            {
                currentSessionStart = newSessionStart;
                
                if (UseManualFixPrice)
                {
                    fixPrice = RoundToTickSize(ManualFixPrice + FixPriceOffset);
                }
                else
                {
                    fixPrice = RoundToTickSize(FindSettlementPrice());
                }
                
                rangeSize = GetPo3RangeSize(activePO3);
                
                if (DebugMode)
                {
                    string mode = UseDynamicMode ? "DYNAMIC" : "FIXED";
                    Print($"[{mode}] Session: {currentSessionStart:MM/dd/yyyy HH:mm}, FIX: {fixPrice:F2}, PO3: {rangeSize:F2}");
                }
            }
            
            if (State == State.Historical && CurrentBar < Count - 2)
                return;
                
            DrawGoldbachLevels();
        }
        
        private DateTime GetCurrentSessionStart()
        {
            try
            {
                int sessionHour, sessionMinute;
                DetectSessionStart(out sessionHour, out sessionMinute);
                
                TimeZoneInfo targetTimeZone = TimeZoneInfo.FindSystemTimeZoneById(SettlementTimeZone);
                DateTime currentTimeInZone = TimeZoneInfo.ConvertTime(Time[0], targetTimeZone);
                
                // Calculate the current session's start time
                DateTime sessionStart = currentTimeInZone.Date.AddHours(sessionHour).AddMinutes(sessionMinute);
                
                // If we haven't reached today's session start yet, use yesterday's
                if (currentTimeInZone.Hour < sessionHour || 
                    (currentTimeInZone.Hour == sessionHour && currentTimeInZone.Minute < sessionMinute))
                {
                    sessionStart = sessionStart.AddDays(-1);
                }
                
                // Skip weekends
                while (sessionStart.DayOfWeek == DayOfWeek.Sunday || 
                       sessionStart.DayOfWeek == DayOfWeek.Saturday)
                {
                    sessionStart = sessionStart.AddDays(-1);
                }
                
                return sessionStart;
            }
            catch (Exception ex)
            {
                Print("Error in GetCurrentSessionStart: " + ex.Message);
                return currentSessionStart;
            }
        }
        
        private void DetectSessionStart(out int hour, out int minute)
        {
            // Use manual settings if not auto-detecting
            if (!AutoDetectSettlementTime)
            {
                hour = SessionStartHour;
                minute = SessionStartMinute;
                return;
            }
            
            hour = 18; 
            minute = 0;
            
            try
            {
                string instrumentName = Instrument.FullName.ToUpper();
                
                // Equity Indices - 18:00 EST session start
                if (instrumentName.Contains("NQ") || instrumentName.Contains("ES") || 
                    instrumentName.Contains("YM") || instrumentName.Contains("RTY"))
                { 
                    hour = 18; minute = 0; 
                }
                // Metals - 18:00 EST electronic session start
                else if (instrumentName.Contains("GC") || instrumentName.Contains("SI") || 
                         instrumentName.Contains("HG")) 
                { 
                    hour = 18; minute = 0; 
                }
                // Energy - 18:00 EST electronic session start
                else if (instrumentName.Contains("CL") || instrumentName.Contains("NG")) 
                { 
                    hour = 18; minute = 0; 
                }
                // Currencies - 17:00 EST (5pm) for forex
                else if (instrumentName.Contains("6E") || instrumentName.Contains("6B") ||
                         instrumentName.Contains("6J") || instrumentName.Contains("6C"))
                { 
                    hour = 17; minute = 0; 
                }
                // Bonds - 18:00 EST
                else if (instrumentName.Contains("ZB") || instrumentName.Contains("ZN") ||
                         instrumentName.Contains("ZF") || instrumentName.Contains("ZT")) 
                { 
                    hour = 18; minute = 0; 
                }
            }
            catch (Exception ex)
            {
                Print("Error in DetectSessionStart: " + ex.Message);
            }
        }
        
        private double RoundToTickSize(double price)
        {
            return Instrument.MasterInstrument.RoundToTickSize(price);
        }
        
        private double FindSettlementPrice()
        {
            try
            {
                int settlementHour = SettlementHour;
                int settlementMinute = SettlementMinute;
                
                if (AutoDetectSettlementTime)
                {
                    DetectSettlementTime(out settlementHour, out settlementMinute);
                }
                
                TimeZoneInfo targetTimeZone = TimeZoneInfo.FindSystemTimeZoneById(SettlementTimeZone);
                DateTime currentTimeInZone = TimeZoneInfo.ConvertTime(Times[0][0], targetTimeZone);
                
                // Get current session start
                int sessionHour, sessionMinute;
                DetectSessionStart(out sessionHour, out sessionMinute);
                DateTime currentSessionStartInZone = currentTimeInZone.Date.AddHours(sessionHour).AddMinutes(sessionMinute);
                if (currentTimeInZone.Hour < sessionHour || 
                    (currentTimeInZone.Hour == sessionHour && currentTimeInZone.Minute < sessionMinute))
                {
                    currentSessionStartInZone = currentSessionStartInZone.AddDays(-1);
                }
                
                // Find settlement time BEFORE the current session start
                // For most instruments, settlement is earlier on the same calendar day as session start
                DateTime targetSettlementTime = currentSessionStartInZone.Date
                    .AddHours(settlementHour).AddMinutes(settlementMinute);
                
                // If settlement time is after session start time (on same day), look at previous day
                if (settlementHour > sessionHour || (settlementHour == sessionHour && settlementMinute >= sessionMinute))
                {
                    targetSettlementTime = targetSettlementTime.AddDays(-1);
                }
                
                while (targetSettlementTime.DayOfWeek == DayOfWeek.Sunday || 
                       targetSettlementTime.DayOfWeek == DayOfWeek.Saturday)
                {
                    targetSettlementTime = targetSettlementTime.AddDays(-1);
                }
                
                double settlementPrice = Close[0];
                double closestTimeDiff = double.MaxValue;
                bool foundSettlement = false;
                
                int dataSeriesIndex = 0;
                int maxBarsToSearch = 1000;
                
                if (BarsArray.Length > 1 && CurrentBars[1] >= 0)
                {
                    dataSeriesIndex = 1;
                    maxBarsToSearch = 2000;
                }
                
                for (int i = 0; i < Math.Min(CurrentBars[dataSeriesIndex], maxBarsToSearch); i++)
                {
                    DateTime barTimeInZone = TimeZoneInfo.ConvertTime(Times[dataSeriesIndex][i], targetTimeZone);
                    double timeDiff = Math.Abs((barTimeInZone - targetSettlementTime).TotalMinutes);
                    
                    if (timeDiff < closestTimeDiff && timeDiff <= 5)
                    {
                        closestTimeDiff = timeDiff;
                        settlementPrice = Closes[dataSeriesIndex][i];
                        foundSettlement = true;
                        
                        if (DebugMode)
                        {
                            Print($"Settlement: {settlementPrice:F2} at {barTimeInZone:MM/dd HH:mm} (target: {targetSettlementTime:MM/dd HH:mm})");
                        }
                    }
                    
                    if (barTimeInZone < targetSettlementTime.AddHours(-2))
                        break;
                }
                
                if (!foundSettlement && DebugMode)
                {
                    Print($"WARNING: Settlement not found for {targetSettlementTime:MM/dd HH:mm}");
                }
                
                return settlementPrice + FixPriceOffset;
            }
            catch (Exception ex)
            {
                Print("Error in FindSettlementPrice: " + ex.Message);
                return CurrentBar > 0 ? Close[1] + FixPriceOffset : Close[0];
            }
        }
        
        private void DetectSettlementTime(out int hour, out int minute)
        {
            hour = 16; minute = 0;
            
            try
            {
                string instrumentName = Instrument.FullName.ToUpper();
                
                if (instrumentName.Contains("NQ") || instrumentName.Contains("ES") || 
                    instrumentName.Contains("YM") || instrumentName.Contains("RTY"))
                { hour = 16; minute = 0; }
                else if (instrumentName.Contains("GC")) { hour = 13; minute = 30; }
                else if (instrumentName.Contains("SI")) { hour = 13; minute = 25; }
                else if (instrumentName.Contains("CL")) { hour = 14; minute = 30; }
                else if (instrumentName.Contains("HG")) { hour = 13; minute = 0; }
                else if (instrumentName.Contains("NG")) { hour = 14; minute = 30; }
                else if (instrumentName.Contains("6E")) { hour = 16; minute = 0; }
                else if (instrumentName.Contains("ZB") || instrumentName.Contains("ZN")) 
                { hour = 14; minute = 0; }
            }
            catch (Exception ex)
            {
                Print("Error in DetectSettlementTime: " + ex.Message);
            }
        }
        
        private void CalculateOptimalPO3()
        {
            try
            {
                List<double> dailyRanges = new List<double>();
                DateTime lastDate = Time[0].Date;
                double dayHigh = High[0];
                double dayLow = Low[0];
                
                for (int i = 0; i < Math.Min(CurrentBar, 500); i++)
                {
                    if (Time[i].Date != lastDate)
                    {
                        if (dayHigh > dayLow) dailyRanges.Add(dayHigh - dayLow);
                        if (dailyRanges.Count >= ADRLookbackPeriod) break;
                        lastDate = Time[i].Date;
                        dayHigh = High[i];
                        dayLow = Low[i];
                    }
                    else
                    {
                        dayHigh = Math.Max(dayHigh, High[i]);
                        dayLow = Math.Min(dayLow, Low[i]);
                    }
                }
                
                if (dayHigh > dayLow) dailyRanges.Add(dayHigh - dayLow);
                if (dailyRanges.Count == 0) { calculatedADR = 0; return; }
                
                dailyRanges.Sort();
                int percentile75Index = (int)(dailyRanges.Count * 0.75);
                calculatedADR = dailyRanges[Math.Min(percentile75Index, dailyRanges.Count - 1)];
                
                double[] po3Values = { 3, 9, 27, 81, 243, 729, 2187, 6561, 19683, 59049, 177147, 531441 };
                Po3RangeType[] po3Types = { 
                    Po3RangeType.Range3, Po3RangeType.Range9, Po3RangeType.Range27, 
                    Po3RangeType.Range81, Po3RangeType.Range243, Po3RangeType.Range729,
                    Po3RangeType.Range2187, Po3RangeType.Range6561, Po3RangeType.Range19683,
                    Po3RangeType.Range59049, Po3RangeType.Range177147, Po3RangeType.Range531441
                };
                
                double targetSize = calculatedADR * 0.85;
                recommendedPO3 = Po3RangeType.Range531441;
                
                for (int i = 0; i < po3Values.Length; i++)
                {
                    if (po3Values[i] >= targetSize)
                    {
                        recommendedPO3 = po3Types[i];
                        break;
                    }
                }
                
                if (calculatedADR < 3) recommendedPO3 = Po3RangeType.Range3;
                
                if (DebugMode)
                {
                    Print($"ADR: {calculatedADR:F2}, Rec PO3: {GetPo3Value(recommendedPO3)}");
                }
            }
            catch (Exception ex)
            {
                Print("Error in CalculateOptimalPO3: " + ex.Message);
            }
        }

        private double GetPo3RangeSize(Po3RangeType rangeType)
        {
            switch (rangeType)
            {
                case Po3RangeType.Range3: return 3;
                case Po3RangeType.Range9: return 9;
                case Po3RangeType.Range27: return 27;
                case Po3RangeType.Range81: return 81;
                case Po3RangeType.Range243: return 243;
                case Po3RangeType.Range729: return 729;
                case Po3RangeType.Range2187: return 2187;
                case Po3RangeType.Range6561: return 6561;
                case Po3RangeType.Range19683: return 19683;
                case Po3RangeType.Range59049: return 59049;
                case Po3RangeType.Range177147: return 177147;
                case Po3RangeType.Range531441: return 531441;
                case Po3RangeType.Custom: return CustomPo3Value;
                default: return 729;
            }
        }
        
        private void DrawGoldbachLevels()
        {
            if (rangeSize == 0 || fixPrice == 0) return;
                
            if (EnableLevelMerging)
            {
                DrawMergedLevels();
            }
            else
            {
                if (UseDynamicMode)
                    DrawDynamicLevels();
                else
                    DrawFixedLevels();
            }
        }
        
        private void DrawDynamicLevels()
        {
            // DYNAMIC MODE: Settlement price is CENTER (50%)
            string dateTag = currentSessionStart.ToString("yyyyMMddHHmm");
            double halfRange = rangeSize / 2;
            int leftExtent = (HistoricalBarsToShow == 0) ? CurrentBar : Math.Min(CurrentBar, HistoricalBarsToShow);
            
            double rangeHigh = RoundToTickSize(fixPrice + halfRange);
            double rangeLow = RoundToTickSize(fixPrice - halfRange);
            
            if (DebugMode)
            {
                Print($"[DYNAMIC] High: {rangeHigh:F2}, Low: {rangeLow:F2}, Fix: {fixPrice:F2}");
            }
            
            // Draw FIX at center (50%)
            Draw.Line(this, "FIX_" + dateTag, false, leftExtent, fixPrice, 0, fixPrice, 
                     FixPriceColor, FixLineStyle, FixLineWidth);
            
            if (ShowLabels)
            {
                string label = "FIX (50%)";
                if (ShowPriceOnLabels) label += $" {fixPrice:F2}";
                Draw.Text(this, "FIX_Label_" + dateTag, false, label, -5, fixPrice, 0, FixPriceColor, 
                         new SimpleFont("Arial", LabelFontSize), System.Windows.TextAlignment.Left, 
                         Brushes.Transparent, Brushes.Transparent, 0);
            }
            
            // Draw all levels
            DrawLevelsInRange(rangeLow, rangeHigh, dateTag, leftExtent);
        }
        
        private void DrawFixedLevels()
        {
            // FIXED MODE: Calculate partition from base 0
            string dateTag = currentSessionStart.ToString("yyyyMMddHHmm");
            int leftExtent = (HistoricalBarsToShow == 0) ? CurrentBar : Math.Min(CurrentBar, HistoricalBarsToShow);
            
            // Calculate which PO3 partition we're in based on current price
            double currentPrice = Close[0];
            
            // Determine partition number with optional half-shift (p.183-184)
            double partition = Math.Floor(currentPrice / rangeSize);
            double rangeLow = partition * rangeSize + (rangeSize * Po3Shift);
            double rangeHigh = rangeLow + rangeSize;
            
            // If shift pushed us above current price, step back one partition
            if (rangeLow > currentPrice)
            {
                rangeLow -= rangeSize;
                rangeHigh -= rangeSize;
            }
            
            // Round to tick size
            rangeLow = RoundToTickSize(rangeLow);
            rangeHigh = RoundToTickSize(rangeHigh);
            
            if (DebugMode)
            {
                Print($"[FIXED] Partition: {partition}, Low: {rangeLow:F2}, High: {rangeHigh:F2}, FIX: {fixPrice:F2}");
            }
            
            // Draw FIX price line (for reference, but not the center)
            Draw.Line(this, "FIX_" + dateTag, false, leftExtent, fixPrice, 0, fixPrice, 
                     FixPriceColor, FixLineStyle, FixLineWidth);
            
            if (ShowLabels)
            {
                string label = "FIX";
                if (ShowPriceOnLabels) label += $" {fixPrice:F2}";
                Draw.Text(this, "FIX_Label_" + dateTag, false, label, -5, fixPrice, 0, FixPriceColor, 
                         new SimpleFont("Arial", LabelFontSize), System.Windows.TextAlignment.Left, 
                         Brushes.Transparent, Brushes.Transparent, 0);
            }
            
            // Draw all levels based on the partition range
            DrawLevelsInRange(rangeLow, rangeHigh, dateTag, leftExtent);
        }
        
private void DrawMergedLevels()
{
    string dateTag = currentSessionStart.ToString("yyyyMMddHHmm");
    double halfRange = rangeSize / 2;
    double rangeHigh, rangeLow;
    
    if (UseDynamicMode)
    {
        rangeHigh = RoundToTickSize(fixPrice + halfRange);
        rangeLow = RoundToTickSize(fixPrice - halfRange);
    }
    else
    {
        double partition = Math.Floor(fixPrice / rangeSize);
        rangeLow = RoundToTickSize(partition * rangeSize + (rangeSize * Po3Shift));
        rangeHigh = RoundToTickSize(rangeLow + rangeSize);
        
        // If shift pushed us above current price, step back
        if (rangeLow > Close[0])
        {
            rangeLow -= rangeSize;
            rangeHigh -= rangeSize;
            rangeLow = RoundToTickSize(rangeLow);
            rangeHigh = RoundToTickSize(rangeHigh);
        }
    }
    
    if (DebugMode)
    {
        string mode = UseDynamicMode ? "DYNAMIC" : "FIXED";
        Print($"[{mode} MERGED] Low: {rangeLow:F2}, High: {rangeHigh:F2}, FIX: {fixPrice:F2}");
    }
    
    double rangeSpan = rangeHigh - rangeLow;
    
    var allLevels = new List<(string label, double price, int priority, Brush color, int thickness, DashStyleHelper style)>();
    
    // In Dynamic mode, DO NOT add FIX to the allLevels list - we'll draw it separately at the end
    if (!UseDynamicMode)
    {
        allLevels.Add(("FIX", fixPrice, 50, FixPriceColor, FixLineWidth, FixLineStyle));
    }
    
    for (int i = 0; i < premiumLevels.Length; i++)
    {
        // SKIP level 50 in Dynamic mode
        if (UseDynamicMode && premiumLevels[i] == 50)
        {
            if (DebugMode) Print($"Skipping premium level 50 in merged mode");
            continue;
        }
        
        double percentage = premiumLevels[i] / 100.0;
        double priceLevel = RoundToTickSize(rangeLow + (rangeSpan * percentage));
        string label = premiumLevels[i].ToString();
        if (ShowPdAreas && i < pdAreas.Length)
        {
            label += " " + pdAreas[i];
        }

		// Use thicker line for Liquidity Void
        int lineWidth = (premiumLevels[i] == 71) ? 2 : PremiumLineWidth;
        allLevels.Add((label, priceLevel, premiumLevels[i], PremiumColor, PremiumLineWidth, PremiumLineStyle));
    }
    
for (int i = 0; i < discountLevels.Length; i++)
{
    if (discountLevels[i] == 0 || discountLevels[i] == 100) continue;
    
    // ... skip check ...
        
    double percentage = discountLevels[i] / 100.0;
    double priceLevel = RoundToTickSize(rangeLow + (rangeSpan * percentage));
    string label = discountLevels[i].ToString();
    if (ShowPdAreas && i < pdAreas.Length)
    {
        label += " " + pdAreas[i];
    }
    
    // Use thicker line for Liquidity Void
    int lineWidth = (discountLevels[i] == 29) ? 2 : DiscountLineWidth;
    
    allLevels.Add((label, priceLevel, 100 - discountLevels[i], DiscountColor, lineWidth, DiscountLineStyle));
    }
    
    if (ShowNonGoldbachLevels)
    {
        var nonGbBrush = NonGoldbachColor.Clone();
        if (nonGbBrush is System.Windows.Media.SolidColorBrush)
        {
            ((System.Windows.Media.SolidColorBrush)nonGbBrush).Opacity = 1.0;
        }
        
        for (int i = 0; i < nonGoldbachLevels.Length; i++)
        {
            // SKIP level 50 in Dynamic mode
            if (UseDynamicMode && nonGoldbachLevels[i] == 50)
            {
                if (DebugMode) Print($"Skipping non-Goldbach level 50 in merged mode");
                continue;
            }
            
            double percentage = nonGoldbachLevels[i] / 100.0;
            double priceLevel = RoundToTickSize(rangeLow + (rangeSpan * percentage));
            string label = nonGoldbachLevels[i].ToString();
            allLevels.Add((label, priceLevel, nonGoldbachLevels[i], nonGbBrush, NonGoldbachLineWidth, NonGoldbachLineStyle));
        }
    }
    
    // Inverted Goldbach levels (p.32-34)
    if (ShowInvertedGoldbachLevels)
    {
        var invBrush = InvertedGoldbachColor.Clone();
        if (invBrush is System.Windows.Media.SolidColorBrush)
        {
            ((System.Windows.Media.SolidColorBrush)invBrush).Opacity = 1.0;
        }
        
        for (int i = 0; i < invertedGoldbachLevels.Length; i++)
        {
            if (UseDynamicMode && invertedGoldbachLevels[i] == 50) continue;
            
            double percentage = invertedGoldbachLevels[i] / 100.0;
            double priceLevel = RoundToTickSize(rangeLow + (rangeSpan * percentage));
            string label = "~" + invertedGoldbachLevels[i].ToString();
            allLevels.Add((label, priceLevel, invertedGoldbachLevels[i], invBrush, InvertedGoldbachLineWidth, InvertedGoldbachLineStyle));
        }
    }
    
    if (ShowMidpointLevels)
    {
        var midBrush = MidpointColor.Clone();
        if (midBrush is System.Windows.Media.SolidColorBrush)
        {
            ((System.Windows.Media.SolidColorBrush)midBrush).Opacity = 1.0;
        }
        
        List<int> allMainLevels = new List<int>();
        allMainLevels.AddRange(discountLevels);
        allMainLevels.AddRange(premiumLevels);
        if (ShowNonGoldbachLevels)
        {
            allMainLevels.AddRange(nonGoldbachLevels);
        }
        allMainLevels = allMainLevels.Distinct().OrderBy(x => x).ToList();
        
        for (int i = 0; i < allMainLevels.Count - 1; i++)
        {
            double midpoint = (allMainLevels[i] + allMainLevels[i + 1]) / 2.0;
            
            // CRITICAL: Skip midpoint at 50% in Dynamic mode
            if (UseDynamicMode && Math.Abs(midpoint - 50.0) < 0.001)
            {
                if (DebugMode) Print($"Skipping midpoint at {midpoint}% in merged mode");
                continue;
            }
            
            double percentage = midpoint / 100.0;
            double priceLevel = RoundToTickSize(rangeLow + (rangeSpan * percentage));
            
            // Additional check: skip if within 1 tick of FIX price
            if (UseDynamicMode && Math.Abs(priceLevel - fixPrice) < Instrument.MasterInstrument.TickSize)
            {
                if (DebugMode) Print($"Skipping midpoint at {priceLevel:F2} in merged mode");
                continue;
            }
            
            string label = $"CE {midpoint:F1}";
            allLevels.Add((label, priceLevel, (int)midpoint, midBrush, MidpointLineWidth, MidpointLineStyle));
        }
    }
    
    // Equilibrium (50%) in Fixed mode
    if (ShowEquilibrium && !UseDynamicMode)
    {
        double eqPrice = RoundToTickSize(rangeLow + (rangeSpan * 0.5));
        allLevels.Add(("50 EQ", eqPrice, 50, EquilibriumColor, 2, DashStyleHelper.DashDotDot));
    }
    
    allLevels.Add(("100 HIGH", rangeHigh, 100, PremiumColor, BoundaryLineWidth, BoundaryLineStyle));
    allLevels.Add(("0 LOW", rangeLow, 0, DiscountColor, BoundaryLineWidth, BoundaryLineStyle));
    
    var mergedGroups = GroupLevelsByProximity(allLevels);
    
    int counter = 0;
    int leftExtent = (HistoricalBarsToShow == 0) ? CurrentBar : Math.Min(CurrentBar, HistoricalBarsToShow);
    
    foreach (var group in mergedGroups)
    {
        // In Dynamic mode, skip any group containing a level at FIX price
        if (UseDynamicMode)
        {
            bool containsFiftyPercent = false;
            foreach (var level in group)
            {
                if (Math.Abs(level.price - fixPrice) < Instrument.MasterInstrument.TickSize * 0.5)
                {
                    containsFiftyPercent = true;
                    if (DebugMode) Print($"Skipping merged group at {level.price:F2}");
                    break;
                }
            }
            
            if (containsFiftyPercent)
            {
                counter++;
                continue;
            }
        }
        
        DrawMergedGroup(group, counter, leftExtent, dateTag);
        counter++;
    }
    
    if (ShowPo3StopRuns)
    {
        double stopRunDistance = GetPo3StopRunDistance();
        double upperStopRun = RoundToTickSize(rangeHigh + stopRunDistance);
        double lowerStopRun = RoundToTickSize(rangeLow - stopRunDistance);
        
        var stopRunBrush = Po3StopRunColor.Clone();
        
        Draw.Line(this, "StopRun_Upper_" + dateTag, false, leftExtent, upperStopRun, 0, upperStopRun, 
                 stopRunBrush, DashStyleHelper.Dash, 2);
        Draw.Line(this, "StopRun_Lower_" + dateTag, false, leftExtent, lowerStopRun, 0, lowerStopRun, 
                 stopRunBrush, DashStyleHelper.Dash, 2);
        
        if (ShowLabels)
        {
            string upperLabel = $"SR {upperStopRun:F2}";
            string lowerLabel = $"SR {lowerStopRun:F2}";
            
            Draw.Text(this, "StopRun_Upper_Label_" + dateTag, false, upperLabel, -5, upperStopRun, 0, stopRunBrush, 
                     new SimpleFont("Arial", LabelFontSize), System.Windows.TextAlignment.Left, 
                     Brushes.Transparent, Brushes.Transparent, 0);
            Draw.Text(this, "StopRun_Lower_Label_" + dateTag, false, lowerLabel, -5, lowerStopRun, 0, stopRunBrush, 
                     new SimpleFont("Arial", LabelFontSize), System.Windows.TextAlignment.Left, 
                     Brushes.Transparent, Brushes.Transparent, 0);
        }
    }
    
    if (ShowPo3StdvLevels)
    {
        var stdvBrush = Po3StdvColor.Clone();
        if (stdvBrush is System.Windows.Media.SolidColorBrush)
        {
            ((System.Windows.Media.SolidColorBrush)stdvBrush).Opacity = 0.5;
        }
        
        int maxLevels = 10;
        
        for (int i = 1; i <= maxLevels; i++)
        {
            double offset = Po3StdvInterval * i;
            double upperLevel = RoundToTickSize(fixPrice + offset);
            double lowerLevel = RoundToTickSize(fixPrice - offset);
            
            if (upperLevel > rangeHigh + rangeSize || lowerLevel < rangeLow - rangeSize)
                break;
            
            Draw.Line(this, "STDV_Upper_" + i + "_" + dateTag, false, leftExtent, upperLevel, 0, upperLevel, 
                     stdvBrush, DashStyleHelper.Dot, 1);
            Draw.Line(this, "STDV_Lower_" + i + "_" + dateTag, false, leftExtent, lowerLevel, 0, lowerLevel, 
                     stdvBrush, DashStyleHelper.Dot, 1);
            
            if (ShowLabels && i % 3 == 0)
            {
                string upperLabel = $"+{offset:F0}";
                string lowerLabel = $"-{offset:F0}";
                
                Draw.Text(this, "STDV_Upper_Label_" + i + "_" + dateTag, false, upperLabel, -5, upperLevel, 0, stdvBrush, 
                         new SimpleFont("Arial", LabelFontSize - 2), System.Windows.TextAlignment.Left, 
                         Brushes.Transparent, Brushes.Transparent, 0);
                Draw.Text(this, "STDV_Lower_Label_" + i + "_" + dateTag, false, lowerLabel, -5, lowerLevel, 0, stdvBrush, 
                         new SimpleFont("Arial", LabelFontSize - 2), System.Windows.TextAlignment.Left, 
                         Brushes.Transparent, Brushes.Transparent, 0);
            }
        }
    }
    
    // *** DRAW FIX LINE LAST in Dynamic mode ***
    if (UseDynamicMode)
    {
        Draw.Line(this, "FIX_FINAL_" + dateTag, false, leftExtent, fixPrice, 0, fixPrice, 
                 FixPriceColor, FixLineStyle, FixLineWidth);
        
        if (ShowLabels)
        {
            string label = "FIX (50%)";
            if (ShowPriceOnLabels) label += $" {fixPrice:F2}";
            Draw.Text(this, "FIX_FINAL_Label_" + dateTag, false, label, -5, fixPrice, 0, FixPriceColor, 
                     new SimpleFont("Arial", LabelFontSize), System.Windows.TextAlignment.Left, 
                     Brushes.Transparent, Brushes.Transparent, 0);
        }
        
        if (DebugMode)
        {
            Print($"FIX line drawn LAST at {fixPrice:F2}");
        }
    }
    
    // Draw Hidden Range between PO3s (p.50) in merged mode
    if (ShowHiddenRange)
    {
        double nextPo3Size = rangeSize * 3;
        double hiddenRangeSize = (rangeSize + nextPo3Size) / 2.0;
        
        double hiddenRangeLow, hiddenRangeHigh;
        if (UseDynamicMode)
        {
            double halfHidden = hiddenRangeSize / 2;
            hiddenRangeLow = RoundToTickSize(fixPrice - halfHidden);
            hiddenRangeHigh = RoundToTickSize(fixPrice + halfHidden);
        }
        else
        {
            double partition = Math.Floor(Close[0] / hiddenRangeSize);
            hiddenRangeLow = RoundToTickSize(partition * hiddenRangeSize + (hiddenRangeSize * Po3Shift));
            hiddenRangeHigh = RoundToTickSize(hiddenRangeLow + hiddenRangeSize);
            if (hiddenRangeLow > Close[0])
            {
                hiddenRangeLow -= hiddenRangeSize;
                hiddenRangeHigh -= hiddenRangeSize;
                hiddenRangeLow = RoundToTickSize(hiddenRangeLow);
                hiddenRangeHigh = RoundToTickSize(hiddenRangeHigh);
            }
        }
        
        var hiddenBrush = Brushes.DarkGoldenrod.Clone();
        
        Draw.Line(this, "HiddenRange_High_" + dateTag, false, leftExtent, hiddenRangeHigh, 0, hiddenRangeHigh, 
                 hiddenBrush, DashStyleHelper.DashDot, 1);
        Draw.Line(this, "HiddenRange_Low_" + dateTag, false, leftExtent, hiddenRangeLow, 0, hiddenRangeLow, 
                 hiddenBrush, DashStyleHelper.DashDot, 1);
        
        if (ShowLabels)
        {
            string hrLabel = $"HR {hiddenRangeSize:F0}";
            Draw.Text(this, "HiddenRange_High_Label_" + dateTag, false, $"{hrLabel} H {hiddenRangeHigh:F2}", 
                     -5, hiddenRangeHigh, 0, hiddenBrush, 
                     new SimpleFont("Arial", LabelFontSize - 1), System.Windows.TextAlignment.Left, 
                     Brushes.Transparent, Brushes.Transparent, 0);
            Draw.Text(this, "HiddenRange_Low_Label_" + dateTag, false, $"{hrLabel} L {hiddenRangeLow:F2}", 
                     -5, hiddenRangeLow, 0, hiddenBrush, 
                     new SimpleFont("Arial", LabelFontSize - 1), System.Windows.TextAlignment.Left, 
                     Brushes.Transparent, Brushes.Transparent, 0);
        }
    }
}

// METHOD 2: DrawLevelsInRange (used when EnableLevelMerging = FALSE)
// This should go around line 410 in your original file, after DrawFixedLevels()


private void DrawLevelsInRange(double rangeLow, double rangeHigh, string dateTag, int leftExtent)
{
    double rangeSpan = rangeHigh - rangeLow;
    
    // Draw Premium levels
for (int i = 0; i < premiumLevels.Length; i++)
{
    double percentage = premiumLevels[i] / 100.0;
    double priceLevel = RoundToTickSize(rangeLow + (rangeSpan * percentage));
    
    string tag = "Premium_" + premiumLevels[i] + "_" + dateTag;
    
    // Use thicker line for Liquidity Void (71)
    int lineWidth = (premiumLevels[i] == 71) ? 2 : PremiumLineWidth;
    
    Draw.Line(this, tag, false, leftExtent, priceLevel, 0, priceLevel, 
             PremiumColor, PremiumLineStyle, lineWidth);
        
if (ShowLabels)
{
    string label = premiumLevels[i].ToString();
    if (ShowPdAreas && i < pdAreas.Length)
    {
        label += " " + pdAreas[i];
    }
    if (ShowPriceOnLabels) label += $" {priceLevel:F2}";
    
    int fontSize = (premiumLevels[i] == 71) ? LabelFontSize + 2 : LabelFontSize;
    
    Draw.Text(this, tag + "_Label", false, label, -5, priceLevel, 0, PremiumColor, 
             new SimpleFont("Arial", fontSize), System.Windows.TextAlignment.Left, 
             Brushes.Transparent, Brushes.Transparent, 0);
    
    // Draw fire emoji separately for 71% level
    if (premiumLevels[i] == 71)
    {
        Draw.Text(this, tag + "_FireEmoji", false, "🔥", -20, priceLevel, 0, FireEmojiColor, 
                 new SimpleFont("Arial", fontSize + 2), System.Windows.TextAlignment.Left, 
                 Brushes.Transparent, Brushes.Transparent, 0);
    }
}
}
    
    // Draw Discount levels
    for (int i = 0; i < discountLevels.Length; i++)
    {
        if (discountLevels[i] == 0 || discountLevels[i] == 100) continue;
            
        double percentage = discountLevels[i] / 100.0;
        double priceLevel = RoundToTickSize(rangeLow + (rangeSpan * percentage));
        
        string tag = "Discount_" + discountLevels[i] + "_" + dateTag;
		
		// Use thicker line for Liquidity Void (29)
        int lineWidth = (discountLevels[i] == 29) ? 2 : DiscountLineWidth;
        
        Draw.Line(this, tag, false, leftExtent, priceLevel, 0, priceLevel, 
                 DiscountColor, DiscountLineStyle, DiscountLineWidth);
        
        if (ShowLabels)
{
    string label = discountLevels[i].ToString();
    if (ShowPdAreas && i < pdAreas.Length)
    {
        label += " " + pdAreas[i];
    }
    if (ShowPriceOnLabels) label += $" {priceLevel:F2}";
    
    int fontSize = (discountLevels[i] == 29) ? LabelFontSize + 2 : LabelFontSize;
    
    Draw.Text(this, tag + "_Label", false, label, -5, priceLevel, 0, DiscountColor, 
             new SimpleFont("Arial", fontSize), System.Windows.TextAlignment.Left, 
             Brushes.Transparent, Brushes.Transparent, 0);
    
    // Draw fire emoji separately for 29% level
    if (discountLevels[i] == 29)
    {
        Draw.Text(this, tag + "_FireEmoji", false, "🔥", -20, priceLevel, 0, FireEmojiColor, 
                 new SimpleFont("Arial", fontSize + 2), System.Windows.TextAlignment.Left, 
                 Brushes.Transparent, Brushes.Transparent, 0);
    }
}
}
    
    // Draw Non-Goldbach levels (23, 35, 65, 77)
    if (ShowNonGoldbachLevels)
    {
        var nonGbBrush = NonGoldbachColor.Clone();
        if (nonGbBrush is System.Windows.Media.SolidColorBrush)
        {
            ((System.Windows.Media.SolidColorBrush)nonGbBrush).Opacity = 1.0;
        }
        
        for (int i = 0; i < nonGoldbachLevels.Length; i++)
        {
            double percentage = nonGoldbachLevels[i] / 100.0;
            double priceLevel = RoundToTickSize(rangeLow + (rangeSpan * percentage));
            
            string tag = "NonGB_" + nonGoldbachLevels[i] + "_" + dateTag;
            
            Draw.Line(this, tag, false, leftExtent, priceLevel, 0, priceLevel, 
                     nonGbBrush, NonGoldbachLineStyle, NonGoldbachLineWidth);
            
            if (ShowLabels)
            {
                string label = nonGoldbachLevels[i].ToString();
                if (ShowPriceOnLabels) label += $" {priceLevel:F2}";
                
                Draw.Text(this, tag + "_Label", false, label, -5, priceLevel, 0, nonGbBrush, 
                         new SimpleFont("Arial", LabelFontSize - 1), System.Windows.TextAlignment.Left, 
                         Brushes.Transparent, Brushes.Transparent, 0);
            }
        }
    }
    
    // Draw Midpoint levels - SKIP 50% in Dynamic mode
    if (ShowMidpointLevels)
    {
        var midBrush = MidpointColor.Clone();
        if (midBrush is System.Windows.Media.SolidColorBrush)
        {
            ((System.Windows.Media.SolidColorBrush)midBrush).Opacity = 1.0;
        }
        
        List<int> allMainLevels = new List<int>();
        allMainLevels.AddRange(discountLevels);
        allMainLevels.AddRange(premiumLevels);
        if (ShowNonGoldbachLevels)
        {
            allMainLevels.AddRange(nonGoldbachLevels);
        }
        allMainLevels = allMainLevels.Distinct().OrderBy(x => x).ToList();
        
        for (int i = 0; i < allMainLevels.Count - 1; i++)
        {
            double midpoint = (allMainLevels[i] + allMainLevels[i + 1]) / 2.0;
            
            // SKIP midpoint at 50% in Dynamic mode
            if (UseDynamicMode && Math.Abs(midpoint - 50.0) < 0.001)
            {
                continue;
            }
            
            double percentage = midpoint / 100.0;
            double priceLevel = RoundToTickSize(rangeLow + (rangeSpan * percentage));
            
            string tag = "Mid_" + allMainLevels[i] + "_" + allMainLevels[i + 1] + "_" + dateTag;
            
            Draw.Line(this, tag, false, leftExtent, priceLevel, 0, priceLevel, 
                     midBrush, MidpointLineStyle, MidpointLineWidth);
            
            if (ShowLabels)
            {
                string label = $"CE {midpoint:F1}";
                if (ShowPriceOnLabels) label += $" {priceLevel:F2}";
                
                Draw.Text(this, tag + "_Label", false, label, -5, priceLevel, 0, midBrush, 
                         new SimpleFont("Arial", LabelFontSize - 2), System.Windows.TextAlignment.Left, 
                         Brushes.Transparent, Brushes.Transparent, 0);
            }
        }
    }
    
    // Draw Inverted Goldbach levels (p.32-34: 14, 32, 38, 56, 74, 79, 92, 95, 98)
    if (ShowInvertedGoldbachLevels)
    {
        var invBrush = InvertedGoldbachColor.Clone();
        if (invBrush is System.Windows.Media.SolidColorBrush)
        {
            ((System.Windows.Media.SolidColorBrush)invBrush).Opacity = 1.0;
        }
        
        for (int i = 0; i < invertedGoldbachLevels.Length; i++)
        {
            double percentage = invertedGoldbachLevels[i] / 100.0;
            double priceLevel = RoundToTickSize(rangeLow + (rangeSpan * percentage));
            
            string tag = "InvGB_" + invertedGoldbachLevels[i] + "_" + dateTag;
            
            Draw.Line(this, tag, false, leftExtent, priceLevel, 0, priceLevel, 
                     invBrush, InvertedGoldbachLineStyle, InvertedGoldbachLineWidth);
            
            if (ShowLabels)
            {
                string label = "~" + invertedGoldbachLevels[i].ToString();
                if (ShowPriceOnLabels) label += $" {priceLevel:F2}";
                
                Draw.Text(this, tag + "_Label", false, label, -5, priceLevel, 0, invBrush, 
                         new SimpleFont("Arial", LabelFontSize - 1), System.Windows.TextAlignment.Left, 
                         Brushes.Transparent, Brushes.Transparent, 0);
            }
        }
    }
    
    // Draw Equilibrium line (50%) in Fixed mode
    if (ShowEquilibrium && !UseDynamicMode)
    {
        double eqPrice = RoundToTickSize(rangeLow + (rangeSpan * 0.5));
        string eqTag = "Equilibrium_" + dateTag;
        
        Draw.Line(this, eqTag, false, leftExtent, eqPrice, 0, eqPrice, 
                 EquilibriumColor, DashStyleHelper.DashDotDot, 2);
        
        if (ShowLabels)
        {
            string eqLabel = "50 EQ";
            if (ShowPriceOnLabels) eqLabel += $" {eqPrice:F2}";
            Draw.Text(this, eqTag + "_Label", false, eqLabel, -5, eqPrice, 0, EquilibriumColor, 
                     new SimpleFont("Arial", LabelFontSize), System.Windows.TextAlignment.Left, 
                     Brushes.Transparent, Brushes.Transparent, 0);
        }
    }
    
    // Draw Boundaries
    Draw.Line(this, "High_100_" + dateTag, false, leftExtent, rangeHigh, 0, rangeHigh, 
             PremiumColor, BoundaryLineStyle, BoundaryLineWidth);
    Draw.Line(this, "Low_0_" + dateTag, false, leftExtent, rangeLow, 0, rangeLow, 
             DiscountColor, BoundaryLineStyle, BoundaryLineWidth);
    
    if (ShowLabels)
    {
        string highLabel = "100 HIGH";
        string lowLabel = "0 LOW";
        if (ShowPriceOnLabels)
        {
            highLabel += $" {rangeHigh:F2}";
            lowLabel += $" {rangeLow:F2}";
        }
        Draw.Text(this, "High_Label_" + dateTag, false, highLabel, -5, rangeHigh, 0, PremiumColor, 
                 new SimpleFont("Arial", LabelFontSize), System.Windows.TextAlignment.Left, 
                 Brushes.Transparent, Brushes.Transparent, 0);
        Draw.Text(this, "Low_Label_" + dateTag, false, lowLabel, -5, rangeLow, 0, DiscountColor, 
                 new SimpleFont("Arial", LabelFontSize), System.Windows.TextAlignment.Left, 
                 Brushes.Transparent, Brushes.Transparent, 0);
    }
    
    // Draw PO3 Stop Run Levels
    if (ShowPo3StopRuns)
    {
        double stopRunDistance = GetPo3StopRunDistance();
        double upperStopRun = RoundToTickSize(rangeHigh + stopRunDistance);
        double lowerStopRun = RoundToTickSize(rangeLow - stopRunDistance);
        
        var stopRunBrush = Po3StopRunColor.Clone();
        
        Draw.Line(this, "StopRun_Upper_" + dateTag, false, leftExtent, upperStopRun, 0, upperStopRun, 
                 stopRunBrush, DashStyleHelper.Dash, 2);
        Draw.Line(this, "StopRun_Lower_" + dateTag, false, leftExtent, lowerStopRun, 0, lowerStopRun, 
                 stopRunBrush, DashStyleHelper.Dash, 2);
        
        if (ShowLabels)
        {
            string upperLabel = $"SR {upperStopRun:F2}";
            string lowerLabel = $"SR {lowerStopRun:F2}";
            
            Draw.Text(this, "StopRun_Upper_Label_" + dateTag, false, upperLabel, -5, upperStopRun, 0, stopRunBrush, 
                     new SimpleFont("Arial", LabelFontSize), System.Windows.TextAlignment.Left, 
                     Brushes.Transparent, Brushes.Transparent, 0);
            Draw.Text(this, "StopRun_Lower_Label_" + dateTag, false, lowerLabel, -5, lowerStopRun, 0, stopRunBrush, 
                     new SimpleFont("Arial", LabelFontSize), System.Windows.TextAlignment.Left, 
                     Brushes.Transparent, Brushes.Transparent, 0);
        }
    }
    
    // Draw PO3 STDV Levels
    if (ShowPo3StdvLevels)
    {
        var stdvBrush = Po3StdvColor.Clone();
        if (stdvBrush is System.Windows.Media.SolidColorBrush)
        {
            ((System.Windows.Media.SolidColorBrush)stdvBrush).Opacity = 0.5;
        }
        
        int maxLevels = 10;
        
        for (int i = 1; i <= maxLevels; i++)
        {
            double offset = Po3StdvInterval * i;
            double upperLevel = RoundToTickSize(fixPrice + offset);
            double lowerLevel = RoundToTickSize(fixPrice - offset);
            
            if (upperLevel > rangeHigh + rangeSize || lowerLevel < rangeLow - rangeSize)
                break;
            
            Draw.Line(this, "STDV_Upper_" + i + "_" + dateTag, false, leftExtent, upperLevel, 0, upperLevel, 
                     stdvBrush, DashStyleHelper.Dot, 1);
            Draw.Line(this, "STDV_Lower_" + i + "_" + dateTag, false, leftExtent, lowerLevel, 0, lowerLevel, 
                     stdvBrush, DashStyleHelper.Dot, 1);
            
            if (ShowLabels && i % 3 == 0)
            {
                string upperLabel = $"+{offset:F0}";
                string lowerLabel = $"-{offset:F0}";
                
                Draw.Text(this, "STDV_Upper_Label_" + i + "_" + dateTag, false, upperLabel, -5, upperLevel, 0, stdvBrush, 
                         new SimpleFont("Arial", LabelFontSize - 2), System.Windows.TextAlignment.Left, 
                         Brushes.Transparent, Brushes.Transparent, 0);
                Draw.Text(this, "STDV_Lower_Label_" + i + "_" + dateTag, false, lowerLabel, -5, lowerLevel, 0, stdvBrush, 
                         new SimpleFont("Arial", LabelFontSize - 2), System.Windows.TextAlignment.Left, 
                         Brushes.Transparent, Brushes.Transparent, 0);
            }
        }
    }
    
    // Draw Hidden Range between PO3s (p.50): (currentPO3 + nextPO3) / 2
    if (ShowHiddenRange)
    {
        double nextPo3Size = rangeSize * 3; // next PO3 is always 3x current
        double hiddenRangeSize = (rangeSize + nextPo3Size) / 2.0;
        
        // Calculate hidden range boundaries from same base
        double hiddenRangeLow, hiddenRangeHigh;
        if (UseDynamicMode)
        {
            double halfHidden = hiddenRangeSize / 2;
            hiddenRangeLow = RoundToTickSize(fixPrice - halfHidden);
            hiddenRangeHigh = RoundToTickSize(fixPrice + halfHidden);
        }
        else
        {
            double partition = Math.Floor(Close[0] / hiddenRangeSize);
            hiddenRangeLow = RoundToTickSize(partition * hiddenRangeSize + (hiddenRangeSize * Po3Shift));
            hiddenRangeHigh = RoundToTickSize(hiddenRangeLow + hiddenRangeSize);
            if (hiddenRangeLow > Close[0])
            {
                hiddenRangeLow -= hiddenRangeSize;
                hiddenRangeHigh -= hiddenRangeSize;
                hiddenRangeLow = RoundToTickSize(hiddenRangeLow);
                hiddenRangeHigh = RoundToTickSize(hiddenRangeHigh);
            }
        }
        
        var hiddenBrush = Brushes.DarkGoldenrod.Clone();
        
        Draw.Line(this, "HiddenRange_High_" + dateTag, false, leftExtent, hiddenRangeHigh, 0, hiddenRangeHigh, 
                 hiddenBrush, DashStyleHelper.DashDot, 1);
        Draw.Line(this, "HiddenRange_Low_" + dateTag, false, leftExtent, hiddenRangeLow, 0, hiddenRangeLow, 
                 hiddenBrush, DashStyleHelper.DashDot, 1);
        
        if (ShowLabels)
        {
            string hrLabel = $"HR {hiddenRangeSize:F0}";
            Draw.Text(this, "HiddenRange_High_Label_" + dateTag, false, $"{hrLabel} H {hiddenRangeHigh:F2}", 
                     -5, hiddenRangeHigh, 0, hiddenBrush, 
                     new SimpleFont("Arial", LabelFontSize - 1), System.Windows.TextAlignment.Left, 
                     Brushes.Transparent, Brushes.Transparent, 0);
            Draw.Text(this, "HiddenRange_Low_Label_" + dateTag, false, $"{hrLabel} L {hiddenRangeLow:F2}", 
                     -5, hiddenRangeLow, 0, hiddenBrush, 
                     new SimpleFont("Arial", LabelFontSize - 1), System.Windows.TextAlignment.Left, 
                     Brushes.Transparent, Brushes.Transparent, 0);
        }
    }
}
        
        private double GetPo3StopRunDistance()
        {
            switch (po3StopRunSize)
            {
                case NinjaTrader.NinjaScript.Indicators.Po3StopRunSize.PO3Minus2:
                    return rangeSize / 9.0;
                case NinjaTrader.NinjaScript.Indicators.Po3StopRunSize.PO3Minus3:
                    return rangeSize / 27.0;
                case NinjaTrader.NinjaScript.Indicators.Po3StopRunSize.PO3Minus4:
                    return rangeSize / 81.0;
                default:
                    return rangeSize / 9.0;
            }
        }
        
 
        
        private List<List<(string label, double price, int priority, Brush color, int thickness, DashStyleHelper style)>> 
            GroupLevelsByProximity(List<(string label, double price, int priority, Brush color, int thickness, DashStyleHelper style)> levels)
        {
            var groups = new List<List<(string label, double price, int priority, Brush color, int thickness, DashStyleHelper style)>>();
            var remaining = new List<(string label, double price, int priority, Brush color, int thickness, DashStyleHelper style)>(levels);
            
            while (remaining.Count > 0)
            {
                var group = new List<(string label, double price, int priority, Brush color, int thickness, DashStyleHelper style)>();
                var seed = remaining[0];
                group.Add(seed);
                remaining.RemoveAt(0);
                
                for (int i = remaining.Count - 1; i >= 0; i--)
                {
                    bool isClose = false;
                    foreach (var groupMember in group)
                    {
                        double effectiveThreshold = UseAutoScaling ? GetScaledThreshold() : MergingThreshold;
                        if (Math.Abs(remaining[i].price - groupMember.price) <= effectiveThreshold)
                        {
                            isClose = true;
                            break;
                        }
                    }
                    
                    if (isClose)
                    {
                        group.Add(remaining[i]);
                        remaining.RemoveAt(i);
                    }
                }
                
                groups.Add(group);
            }
            
            return groups;
        }
        
private void DrawMergedGroup(List<(string label, double price, int priority, Brush color, int thickness, DashStyleHelper style)> group, 
                             int counter, int leftExtent, string dateTag)
{
    if (group.Count == 0) return;
    
    group.Sort((a, b) => a.priority.CompareTo(b.priority));
    var primaryLevel = group[0];
    double avgPrice = RoundToTickSize(group.Average(l => l.price));
    
    var brush = primaryLevel.color.Clone();
    string lineTag = "MergedLevel_" + counter + "_" + dateTag;
    string textTag = "MergedText_" + counter + "_" + dateTag;
    
    Draw.Line(this, lineTag, false, leftExtent, avgPrice, 0, avgPrice, brush, primaryLevel.style, primaryLevel.thickness);
    
    if (ShowLabels)
    {
        string mergedLabel = string.Join("/", group.Select(l => l.label));
        if (ShowPriceOnLabels) mergedLabel += $" {avgPrice:F2}";
        
        Draw.Text(this, textTag, false, mergedLabel, -5, avgPrice, 0, brush, 
                 new SimpleFont("Arial", LabelFontSize), System.Windows.TextAlignment.Left, 
                 Brushes.Transparent, Brushes.Transparent, 0);
        
        // Draw fire emoji separately for 71% and 29% levels
        bool isLiqVoid = group.Any(l => 
            (l.label.Contains("71") && l.label.Contains("LIQ VOID")) || 
            (l.label.Contains("29") && l.label.Contains("LIQ VOID")));
        
        if (isLiqVoid)
        {
            string emojiTag = "FireEmoji_" + counter + "_" + dateTag;
            Draw.Text(this, emojiTag, false, "🔥", -20, avgPrice, 0, FireEmojiColor, 
                     new SimpleFont("Arial", LabelFontSize + 2), System.Windows.TextAlignment.Left, 
                     Brushes.Transparent, Brushes.Transparent, 0);
        }
    }
}
        
        private double GetScaledThreshold()
        {
            try
            {
                string instrumentName = Instrument.FullName.ToUpper();
                
                if (instrumentName.Contains("ES")) return MergingThreshold * 0.25;
                else if (instrumentName.Contains("NQ")) return MergingThreshold;
                else if (instrumentName.Contains("YM")) return MergingThreshold * 2.0;
                else if (instrumentName.Contains("RTY")) return MergingThreshold * 0.5;
                else if (instrumentName.Contains("GC")) return MergingThreshold * 0.1;
                else return MergingThreshold;
            }
            catch { return MergingThreshold; }
        }
        
        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            base.OnRender(chartControl, chartScale);
            
            if (ShowInfoBox && calculatedADR > 0)
            {
                DrawInfoBox(chartControl, chartScale);
            }
        }
        
        private void DrawInfoBox(ChartControl chartControl, ChartScale chartScale)
        {
            try
            {
                var textFormat = new SharpDX.DirectWrite.TextFormat(Core.Globals.DirectWriteFactory, "Arial", InfoBoxFontSize);
                
                float x = 10, y = 10;
                float boxWidth = 220, boxHeight = 120;
                
                switch (InfoBoxLocation)
                {
                    case InfoBoxPosition.TopLeft: x = 10; y = 10; break;
                    case InfoBoxPosition.TopRight: x = (float)(ChartPanel.W - boxWidth - 10); y = 10; break;
                    case InfoBoxPosition.BottomLeft: x = 10; y = (float)(ChartPanel.H - boxHeight - 10); break;
                    case InfoBoxPosition.BottomRight: x = (float)(ChartPanel.W - boxWidth - 10); y = (float)(ChartPanel.H - boxHeight - 10); break;
                }
                
                var backgroundBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(0, 0, 0, 180));
                RenderTarget.FillRectangle(new SharpDX.RectangleF(x, y, boxWidth, boxHeight), backgroundBrush);
                
                var borderBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(100, 100, 100, 255));
                RenderTarget.DrawRectangle(new SharpDX.RectangleF(x, y, boxWidth, boxHeight), borderBrush, 1);
                
                string status = "Optimal";
                SharpDX.Color statusColor = new SharpDX.Color(0, 255, 0, 255);
                
                if (AutoCalculatePO3)
                {
                    status = "Optimal (Auto)";
                }
                else if (Po3Range != recommendedPO3)
                {
                    int currentValue = GetPo3Value(Po3Range);
                    int recommendedValue = GetPo3Value(recommendedPO3);
                    
                    if (Math.Abs(currentValue - recommendedValue) <= 1)
                    {
                        status = "Acceptable";
                        statusColor = new SharpDX.Color(255, 255, 0, 255);
                    }
                    else
                    {
                        status = "Suboptimal";
                        statusColor = new SharpDX.Color(255, 100, 0, 255);
                    }
                }
                
                var textBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(255, 255, 255, 255));
                var statusBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, statusColor);
                
                string modeText = UseDynamicMode ? "Mode: DYNAMIC" : "Mode: FIXED";
                string po3Text = AutoCalculatePO3 ? 
                    $"PO3: {GetPo3DisplayValue(activePO3)} (Auto)" : 
                    $"PO3: {GetPo3DisplayValue(Po3Range)}";
                string adrText = $"ADR: {calculatedADR:F2}";
                string sessionText = $"Session: {currentSessionStart:MM/dd HH:mm}";
                string recommendText = AutoCalculatePO3 ? "" : $"Rec: {GetPo3DisplayValue(recommendedPO3)}";
                
                RenderTarget.DrawText(modeText, textFormat, new SharpDX.RectangleF(x + 5, y + 5, boxWidth - 10, 20), textBrush);
                RenderTarget.DrawText(po3Text, textFormat, new SharpDX.RectangleF(x + 5, y + 25, boxWidth - 10, 20), textBrush);
                RenderTarget.DrawText(adrText, textFormat, new SharpDX.RectangleF(x + 5, y + 45, boxWidth - 10, 20), textBrush);
                RenderTarget.DrawText(sessionText, textFormat, new SharpDX.RectangleF(x + 5, y + 65, boxWidth - 10, 20), textBrush);
                
                if (!AutoCalculatePO3)
                {
                    RenderTarget.DrawText(recommendText, textFormat, new SharpDX.RectangleF(x + 5, y + 85, boxWidth - 10, 20), textBrush);
                }
                
                RenderTarget.DrawText($"Status: {status}", textFormat, 
                    new SharpDX.RectangleF(x + 5, y + (AutoCalculatePO3 ? 85 : 105), boxWidth - 10, 20), statusBrush);
                
                backgroundBrush.Dispose();
                borderBrush.Dispose();
                textBrush.Dispose();
                statusBrush.Dispose();
                textFormat.Dispose();
            }
            catch (Exception ex)
            {
                Print("Error in DrawInfoBox: " + ex.Message);
            }
        }
        
        private int GetPo3Value(Po3RangeType rangeType)
        {
            switch (rangeType)
            {
                case Po3RangeType.Range3: return 3;
                case Po3RangeType.Range9: return 9;
                case Po3RangeType.Range27: return 27;
                case Po3RangeType.Range81: return 81;
                case Po3RangeType.Range243: return 243;
                case Po3RangeType.Range729: return 729;
                case Po3RangeType.Range2187: return 2187;
                case Po3RangeType.Range6561: return 6561;
                case Po3RangeType.Range19683: return 19683;
                case Po3RangeType.Range59049: return 59049;
                case Po3RangeType.Range177147: return 177147;
                case Po3RangeType.Range531441: return 531441;
                default: return 729;
            }
        }
        
        private string GetPo3DisplayValue(Po3RangeType rangeType)
        {
            return GetPo3Value(rangeType).ToString();
        }

        #region Properties
        
        [NinjaScriptProperty]
        [Display(Name = "PO3 Range", Order = 1, GroupName = "Parameters")]
        public NinjaTrader.NinjaScript.Indicators.Po3RangeType Po3Range { get; set; }
        
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Custom PO3 Value", Order = 2, GroupName = "Parameters")]
        public int CustomPo3Value { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Use Dynamic Mode", Description = "Center range on settlement (recommended for intraday)", Order = 3, GroupName = "Parameters")]
        public bool UseDynamicMode 
        { 
            get { return useDynamicMode; }
            set { useDynamicMode = value; }
        }
        
        [NinjaScriptProperty]
        [Display(Name = "Use Manual FIX Price", Order = 4, GroupName = "Parameters")]
        public bool UseManualFixPrice { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Manual FIX Price", Order = 5, GroupName = "Parameters")]
        public double ManualFixPrice { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "FIX Price Offset", Order = 6, GroupName = "Parameters")]
        public double FixPriceOffset { get; set; }
        
        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "FIX Price Color", Order = 1, GroupName = "Visual")]
        public Brush FixPriceColor { get; set; }
        
        [Browsable(false)]
        public string FixPriceColorSerializable
        {
            get { return Serialize.BrushToString(FixPriceColor); }
            set { FixPriceColor = Serialize.StringToBrush(value); }
        }
        
        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Premium Color", Order = 2, GroupName = "Visual")]
        public Brush PremiumColor { get; set; }
        
        [Browsable(false)]
        public string PremiumColorSerializable
        {
            get { return Serialize.BrushToString(PremiumColor); }
            set { PremiumColor = Serialize.StringToBrush(value); }
        }
        
        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Discount Color", Order = 3, GroupName = "Visual")]
        public Brush DiscountColor { get; set; }
        
        [Browsable(false)]
        public string DiscountColorSerializable
        {
            get { return Serialize.BrushToString(DiscountColor); }
            set { DiscountColor = Serialize.StringToBrush(value); }
        }
        
        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Equilibrium Color", Order = 4, GroupName = "Visual")]
        public Brush EquilibriumColor { get; set; }
        
        [Browsable(false)]
        public string EquilibriumColorSerializable
        {
            get { return Serialize.BrushToString(EquilibriumColor); }
            set { EquilibriumColor = Serialize.StringToBrush(value); }
        }
        
        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Non-Goldbach Color", Order = 5, GroupName = "Visual")]
        public Brush NonGoldbachColor { get; set; }
        
        [Browsable(false)]
        public string NonGoldbachColorSerializable
        {
            get { return Serialize.BrushToString(NonGoldbachColor); }
            set { NonGoldbachColor = Serialize.StringToBrush(value); }
        }
        
        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Midpoint Color", Order = 6, GroupName = "Visual")]
        public Brush MidpointColor { get; set; }
        
        [Browsable(false)]
        public string MidpointColorSerializable
        {
            get { return Serialize.BrushToString(MidpointColor); }
            set { MidpointColor = Serialize.StringToBrush(value); }
        }
		
		[NinjaScriptProperty]
[XmlIgnore]
[Display(Name = "Fire Emoji Color", Order = 13, GroupName = "Visual")]
public Brush FireEmojiColor { get; set; }

[Browsable(false)]
public string FireEmojiColorSerializable
{
    get { return Serialize.BrushToString(FireEmojiColor); }
    set { FireEmojiColor = Serialize.StringToBrush(value); }
}
        
        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "PO3 Stop Run Color", Order = 7, GroupName = "Visual")]
        public Brush Po3StopRunColor 
        { 
            get { return po3StopRunColor; }
            set { po3StopRunColor = value; }
        }
        
        [Browsable(false)]
        public string Po3StopRunColorSerializable
        {
            get { return Serialize.BrushToString(Po3StopRunColor); }
            set { Po3StopRunColor = Serialize.StringToBrush(value); }
        }
        
        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "PO3 STDV Color", Order = 8, GroupName = "Visual")]
        public Brush Po3StdvColor 
        { 
            get { return po3StdvColor; }
            set { po3StdvColor = value; }
        }
        
        [Browsable(false)]
        public string Po3StdvColorSerializable
        {
            get { return Serialize.BrushToString(Po3StdvColor); }
            set { Po3StdvColor = Serialize.StringToBrush(value); }
        }
        
        [NinjaScriptProperty]
        [Range(0, 500)]
        [Display(Name = "Line Extension Left (Bars)", Description = "How many bars to extend lines to the left. 0 = extend to chart edge", Order = 1, GroupName = "Line Styles")]
        public int HistoricalBarsToShow
        { 
            get { return historicalBarsToShow; }
            set { historicalBarsToShow = value; }
        }
        
        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "Bars Extend Right", Order = 2, GroupName = "Line Styles")]
        public int BarsToRight { get; set; }
        
        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "Projection Offset (Minutes)", Order = 3, GroupName = "Line Styles")]
        public int ProjectionOffset { get; set; }
        
        [NinjaScriptProperty]
        [Range(1, 10)]
        [Display(Name = "FIX Line Width", Order = 4, GroupName = "Line Styles")]
        public int FixLineWidth { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "FIX Line Style", Order = 5, GroupName = "Line Styles")]
        public DashStyleHelper FixLineStyle { get; set; }
        
        [NinjaScriptProperty]
        [Range(1, 10)]
        [Display(Name = "Premium Line Width", Order = 6, GroupName = "Line Styles")]
        public int PremiumLineWidth { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Premium Line Style", Order = 7, GroupName = "Line Styles")]
        public DashStyleHelper PremiumLineStyle { get; set; }
        
        [NinjaScriptProperty]
        [Range(1, 10)]
        [Display(Name = "Discount Line Width", Order = 8, GroupName = "Line Styles")]
        public int DiscountLineWidth { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Discount Line Style", Order = 9, GroupName = "Line Styles")]
        public DashStyleHelper DiscountLineStyle { get; set; }
        
        [NinjaScriptProperty]
        [Range(1, 10)]
        [Display(Name = "Boundary Line Width", Order = 10, GroupName = "Line Styles")]
        public int BoundaryLineWidth { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Boundary Line Style", Order = 11, GroupName = "Line Styles")]
        public DashStyleHelper BoundaryLineStyle { get; set; }
        
        [NinjaScriptProperty]
        [Range(1, 10)]
        [Display(Name = "Non-Goldbach Line Width", Order = 12, GroupName = "Line Styles")]
        public int NonGoldbachLineWidth 
        { 
            get { return nonGoldbachLineWidth; }
            set { nonGoldbachLineWidth = value; }
        }
        
        [NinjaScriptProperty]
        [Display(Name = "Non-Goldbach Line Style", Order = 13, GroupName = "Line Styles")]
        public DashStyleHelper NonGoldbachLineStyle 
        { 
            get { return nonGoldbachLineStyle; }
            set { nonGoldbachLineStyle = value; }
        }
        
        [NinjaScriptProperty]
        [Range(1, 10)]
        [Display(Name = "Midpoint Line Width", Order = 14, GroupName = "Line Styles")]
        public int MidpointLineWidth 
        { 
            get { return midpointLineWidth; }
            set { midpointLineWidth = value; }
        }
        
        [NinjaScriptProperty]
        [Display(Name = "Midpoint Line Style", Order = 15, GroupName = "Line Styles")]
        public DashStyleHelper MidpointLineStyle 
        { 
            get { return midpointLineStyle; }
            set { midpointLineStyle = value; }
        }
        
        [NinjaScriptProperty]
        [Display(Name = "Show Labels", Order = 6, GroupName = "Visual")]
        public bool ShowLabels { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Show PD Areas", Order = 7, GroupName = "Visual")]
        public bool ShowPdAreas { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Show Equilibrium", Order = 8, GroupName = "Visual")]
        public bool ShowEquilibrium { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Show Non-Goldbach Levels", Description = "Show semi-prime levels (23, 35, 65, 77)", Order = 9, GroupName = "Visual")]
        public bool ShowNonGoldbachLevels 
        { 
            get { return showNonGoldbachLevels; }
            set { showNonGoldbachLevels = value; }
        }
        
        [NinjaScriptProperty]
        [Display(Name = "Show Midpoint Levels", Description = "Show CE/MT levels between Goldbach levels", Order = 10, GroupName = "Visual")]
        public bool ShowMidpointLevels 
        { 
            get { return showMidpointLevels; }
            set { showMidpointLevels = value; }
        }
        
        [NinjaScriptProperty]
        [Display(Name = "Show Inverted Goldbach", Description = "Show inverted Goldbach levels (14,32,38,56,74,79,92,95,98) - explains erratic price action at extremes", Order = 10, GroupName = "Advanced Levels")]
        public bool ShowInvertedGoldbachLevels 
        { 
            get { return showInvertedGoldbachLevels; }
            set { showInvertedGoldbachLevels = value; }
        }
        
        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Inverted Goldbach Color", Order = 11, GroupName = "Advanced Levels")]
        public Brush InvertedGoldbachColor 
        { 
            get { return invertedGoldbachColor; }
            set { invertedGoldbachColor = value; }
        }
        
        [Browsable(false)]
        public string InvertedGoldbachColorSerializable
        {
            get { return Serialize.BrushToString(InvertedGoldbachColor); }
            set { InvertedGoldbachColor = Serialize.StringToBrush(value); }
        }
        
        [NinjaScriptProperty]
        [Range(1, 10)]
        [Display(Name = "Inverted Goldbach Line Width", Order = 12, GroupName = "Advanced Levels")]
        public int InvertedGoldbachLineWidth 
        { 
            get { return invertedGoldbachLineWidth; }
            set { invertedGoldbachLineWidth = value; }
        }
        
        [NinjaScriptProperty]
        [Display(Name = "Inverted Goldbach Line Style", Order = 13, GroupName = "Advanced Levels")]
        public DashStyleHelper InvertedGoldbachLineStyle 
        { 
            get { return invertedGoldbachLineStyle; }
            set { invertedGoldbachLineStyle = value; }
        }
        
        [NinjaScriptProperty]
        [Display(Name = "Show Hidden Range", Description = "Show (currentPO3 + nextPO3)/2 intermediate range boundaries", Order = 14, GroupName = "Advanced Levels")]
        public bool ShowHiddenRange 
        { 
            get { return showHiddenRange; }
            set { showHiddenRange = value; }
        }
        
        [NinjaScriptProperty]
        [Range(-0.5, 0.5)]
        [Display(Name = "PO3 DR Shift", Description = "Half-shift dealing range for SMT alignment (0 = no shift, 0.5 = half shift)", Order = 15, GroupName = "Advanced Levels")]
        public double Po3Shift 
        { 
            get { return po3Shift; }
            set { po3Shift = value; }
        }
        
        [NinjaScriptProperty]
        [Display(Name = "Show PO3 Stop Runs", Description = "Show stop run levels outside range boundaries", Order = 11, GroupName = "Visual")]
        public bool ShowPo3StopRuns 
        { 
            get { return showPo3StopRuns; }
            set { showPo3StopRuns = value; }
        }
        
        [NinjaScriptProperty]
        [Display(Name = "Show PO3 STDV Levels", Description = "Show standard deviation levels from settlement", Order = 12, GroupName = "Visual")]
        public bool ShowPo3StdvLevels 
        { 
            get { return showPo3StdvLevels; }
            set { showPo3StdvLevels = value; }
        }
        
        [NinjaScriptProperty]
        [Range(6, 20)]
        [Display(Name = "Label Font Size", Order = 11, GroupName = "Visual")]
        public int LabelFontSize 
        { 
            get { return labelFontSize; }
            set { labelFontSize = value; }
        }
        
        [NinjaScriptProperty]
        [Display(Name = "Show Price on Labels", Order = 12, GroupName = "Visual")]
        public bool ShowPriceOnLabels 
        { 
            get { return showPriceOnLabels; }
            set { showPriceOnLabels = value; }
        }
        
        [NinjaScriptProperty]
        [Display(Name = "PO3 Stop Run Size", Description = "Distance for stop run levels", Order = 13, GroupName = "Visual")]
        public NinjaTrader.NinjaScript.Indicators.Po3StopRunSize Po3StopRunSize 
        { 
            get { return po3StopRunSize; }
            set { po3StopRunSize = value; }
        }
        
        [NinjaScriptProperty]
        [Range(3, 243)]
        [Display(Name = "PO3 STDV Interval", Description = "Point interval for STDV levels (3, 9, 27, 81)", Order = 14, GroupName = "Visual")]
        public int Po3StdvInterval 
        { 
            get { return po3StdvInterval; }
            set { po3StdvInterval = value; }
        }
        
        [NinjaScriptProperty]
        [Display(Name = "Enable Level Merging", Order = 1, GroupName = "Level Merging")]
        public bool EnableLevelMerging
        { 
            get { return enableLevelMerging; }
            set { enableLevelMerging = value; }
        }
        
        [NinjaScriptProperty]
        [Display(Name = "Merging Threshold (Ticks)", Order = 2, GroupName = "Level Merging")]
        [Range(0.1, 50.0)]
        public double MergingThreshold
        { 
            get { return mergingThreshold; }
            set { mergingThreshold = value; }
        }
        
        [NinjaScriptProperty]
        [Display(Name = "Use Auto Scaling", Order = 3, GroupName = "Level Merging")]
        public bool UseAutoScaling
        { 
            get { return useAutoScaling; }
            set { useAutoScaling = value; }
        }
        
        [NinjaScriptProperty]
        [Display(Name = "Auto Calculate PO3", Order = 1, GroupName = "Auto PO3")]
        public bool AutoCalculatePO3 { get; set; }
        
        [NinjaScriptProperty]
        [Range(5, 100)]
        [Display(Name = "ADR Lookback Period", Order = 2, GroupName = "Auto PO3")]
        public int ADRLookbackPeriod { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Show Info Box", Order = 3, GroupName = "Auto PO3")]
        public bool ShowInfoBox { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Info Box Location", Order = 4, GroupName = "Auto PO3")]
        public NinjaTrader.NinjaScript.Indicators.InfoBoxPosition InfoBoxLocation { get; set; }
        
        [NinjaScriptProperty]
        [Range(8, 24)]
        [Display(Name = "Info Box Font Size", Order = 5, GroupName = "Auto PO3")]
        public int InfoBoxFontSize 
        { 
            get { return infoBoxFontSize; }
            set { infoBoxFontSize = value; }
        }
        
        [NinjaScriptProperty]
        [Range(0, 23)]
        [Display(Name = "Settlement Hour", Order = 1, GroupName = "Session Timing")]
        public int SettlementHour { get; set; }
        
        [NinjaScriptProperty]
        [Range(0, 59)]
        [Display(Name = "Settlement Minute", Order = 2, GroupName = "Session Timing")]
        public int SettlementMinute { get; set; }
        
        [NinjaScriptProperty]
        [Range(0, 23)]
        [Display(Name = "Session Start Hour", Order = 3, GroupName = "Session Timing")]
        public int SessionStartHour { get; set; }
        
        [NinjaScriptProperty]
        [Range(0, 59)]
        [Display(Name = "Session Start Minute", Order = 4, GroupName = "Session Timing")]
        public int SessionStartMinute { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Settlement TimeZone", Order = 5, GroupName = "Session Timing")]
        public string SettlementTimeZone { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Auto Detect Settlement Time", Order = 6, GroupName = "Session Timing")]
        public bool AutoDetectSettlementTime 
        { 
            get { return autoDetectSettlementTime; }
            set { autoDetectSettlementTime = value; }
        }
        
        [NinjaScriptProperty]
        [Display(Name = "Debug Mode", Order = 1, GroupName = "Debug")]
        public bool DebugMode 
        { 
            get { return debugMode; }
            set { debugMode = value; }
        }
        
        #endregion
    }
}


#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private RedTailGoldbachLevels[] cacheRedTailGoldbachLevels;
		public RedTailGoldbachLevels RedTailGoldbachLevels(NinjaTrader.NinjaScript.Indicators.Po3RangeType po3Range, int customPo3Value, bool useDynamicMode, bool useManualFixPrice, double manualFixPrice, double fixPriceOffset, Brush fixPriceColor, Brush premiumColor, Brush discountColor, Brush equilibriumColor, Brush nonGoldbachColor, Brush midpointColor, Brush fireEmojiColor, Brush po3StopRunColor, Brush po3StdvColor, Brush invertedGoldbachColor, int historicalBarsToShow, int barsToRight, int projectionOffset, int fixLineWidth, DashStyleHelper fixLineStyle, int premiumLineWidth, DashStyleHelper premiumLineStyle, int discountLineWidth, DashStyleHelper discountLineStyle, int boundaryLineWidth, DashStyleHelper boundaryLineStyle, int nonGoldbachLineWidth, DashStyleHelper nonGoldbachLineStyle, int invertedGoldbachLineWidth, DashStyleHelper invertedGoldbachLineStyle, int midpointLineWidth, DashStyleHelper midpointLineStyle, bool showLabels, bool showPdAreas, bool showEquilibrium, bool showNonGoldbachLevels, bool showMidpointLevels, bool showInvertedGoldbachLevels, bool showHiddenRange, bool showPo3StopRuns, bool showPo3StdvLevels, int labelFontSize, bool showPriceOnLabels, NinjaTrader.NinjaScript.Indicators.Po3StopRunSize po3StopRunSize, int po3StdvInterval, bool enableLevelMerging, double mergingThreshold, bool useAutoScaling, bool autoCalculatePO3, int aDRLookbackPeriod, bool showInfoBox, NinjaTrader.NinjaScript.Indicators.InfoBoxPosition infoBoxLocation, int infoBoxFontSize, int settlementHour, int settlementMinute, int sessionStartHour, int sessionStartMinute, string settlementTimeZone, bool autoDetectSettlementTime, double po3Shift, bool debugMode)
		{
			return RedTailGoldbachLevels(Input, po3Range, customPo3Value, useDynamicMode, useManualFixPrice, manualFixPrice, fixPriceOffset, fixPriceColor, premiumColor, discountColor, equilibriumColor, nonGoldbachColor, midpointColor, fireEmojiColor, po3StopRunColor, po3StdvColor, invertedGoldbachColor, historicalBarsToShow, barsToRight, projectionOffset, fixLineWidth, fixLineStyle, premiumLineWidth, premiumLineStyle, discountLineWidth, discountLineStyle, boundaryLineWidth, boundaryLineStyle, nonGoldbachLineWidth, nonGoldbachLineStyle, invertedGoldbachLineWidth, invertedGoldbachLineStyle, midpointLineWidth, midpointLineStyle, showLabels, showPdAreas, showEquilibrium, showNonGoldbachLevels, showMidpointLevels, showInvertedGoldbachLevels, showHiddenRange, showPo3StopRuns, showPo3StdvLevels, labelFontSize, showPriceOnLabels, po3StopRunSize, po3StdvInterval, enableLevelMerging, mergingThreshold, useAutoScaling, autoCalculatePO3, aDRLookbackPeriod, showInfoBox, infoBoxLocation, infoBoxFontSize, settlementHour, settlementMinute, sessionStartHour, sessionStartMinute, settlementTimeZone, autoDetectSettlementTime, po3Shift, debugMode);
		}

		public RedTailGoldbachLevels RedTailGoldbachLevels(ISeries<double> input, NinjaTrader.NinjaScript.Indicators.Po3RangeType po3Range, int customPo3Value, bool useDynamicMode, bool useManualFixPrice, double manualFixPrice, double fixPriceOffset, Brush fixPriceColor, Brush premiumColor, Brush discountColor, Brush equilibriumColor, Brush nonGoldbachColor, Brush midpointColor, Brush fireEmojiColor, Brush po3StopRunColor, Brush po3StdvColor, Brush invertedGoldbachColor, int historicalBarsToShow, int barsToRight, int projectionOffset, int fixLineWidth, DashStyleHelper fixLineStyle, int premiumLineWidth, DashStyleHelper premiumLineStyle, int discountLineWidth, DashStyleHelper discountLineStyle, int boundaryLineWidth, DashStyleHelper boundaryLineStyle, int nonGoldbachLineWidth, DashStyleHelper nonGoldbachLineStyle, int invertedGoldbachLineWidth, DashStyleHelper invertedGoldbachLineStyle, int midpointLineWidth, DashStyleHelper midpointLineStyle, bool showLabels, bool showPdAreas, bool showEquilibrium, bool showNonGoldbachLevels, bool showMidpointLevels, bool showInvertedGoldbachLevels, bool showHiddenRange, bool showPo3StopRuns, bool showPo3StdvLevels, int labelFontSize, bool showPriceOnLabels, NinjaTrader.NinjaScript.Indicators.Po3StopRunSize po3StopRunSize, int po3StdvInterval, bool enableLevelMerging, double mergingThreshold, bool useAutoScaling, bool autoCalculatePO3, int aDRLookbackPeriod, bool showInfoBox, NinjaTrader.NinjaScript.Indicators.InfoBoxPosition infoBoxLocation, int infoBoxFontSize, int settlementHour, int settlementMinute, int sessionStartHour, int sessionStartMinute, string settlementTimeZone, bool autoDetectSettlementTime, double po3Shift, bool debugMode)
		{
			if (cacheRedTailGoldbachLevels != null)
				for (int idx = 0; idx < cacheRedTailGoldbachLevels.Length; idx++)
					if (cacheRedTailGoldbachLevels[idx] != null && cacheRedTailGoldbachLevels[idx].Po3Range == po3Range && cacheRedTailGoldbachLevels[idx].CustomPo3Value == customPo3Value && cacheRedTailGoldbachLevels[idx].UseDynamicMode == useDynamicMode && cacheRedTailGoldbachLevels[idx].UseManualFixPrice == useManualFixPrice && cacheRedTailGoldbachLevels[idx].ManualFixPrice == manualFixPrice && cacheRedTailGoldbachLevels[idx].FixPriceOffset == fixPriceOffset && cacheRedTailGoldbachLevels[idx].FixPriceColor == fixPriceColor && cacheRedTailGoldbachLevels[idx].PremiumColor == premiumColor && cacheRedTailGoldbachLevels[idx].DiscountColor == discountColor && cacheRedTailGoldbachLevels[idx].EquilibriumColor == equilibriumColor && cacheRedTailGoldbachLevels[idx].NonGoldbachColor == nonGoldbachColor && cacheRedTailGoldbachLevels[idx].MidpointColor == midpointColor && cacheRedTailGoldbachLevels[idx].FireEmojiColor == fireEmojiColor && cacheRedTailGoldbachLevels[idx].Po3StopRunColor == po3StopRunColor && cacheRedTailGoldbachLevels[idx].Po3StdvColor == po3StdvColor && cacheRedTailGoldbachLevels[idx].InvertedGoldbachColor == invertedGoldbachColor && cacheRedTailGoldbachLevels[idx].HistoricalBarsToShow == historicalBarsToShow && cacheRedTailGoldbachLevels[idx].BarsToRight == barsToRight && cacheRedTailGoldbachLevels[idx].ProjectionOffset == projectionOffset && cacheRedTailGoldbachLevels[idx].FixLineWidth == fixLineWidth && cacheRedTailGoldbachLevels[idx].FixLineStyle == fixLineStyle && cacheRedTailGoldbachLevels[idx].PremiumLineWidth == premiumLineWidth && cacheRedTailGoldbachLevels[idx].PremiumLineStyle == premiumLineStyle && cacheRedTailGoldbachLevels[idx].DiscountLineWidth == discountLineWidth && cacheRedTailGoldbachLevels[idx].DiscountLineStyle == discountLineStyle && cacheRedTailGoldbachLevels[idx].BoundaryLineWidth == boundaryLineWidth && cacheRedTailGoldbachLevels[idx].BoundaryLineStyle == boundaryLineStyle && cacheRedTailGoldbachLevels[idx].NonGoldbachLineWidth == nonGoldbachLineWidth && cacheRedTailGoldbachLevels[idx].NonGoldbachLineStyle == nonGoldbachLineStyle && cacheRedTailGoldbachLevels[idx].InvertedGoldbachLineWidth == invertedGoldbachLineWidth && cacheRedTailGoldbachLevels[idx].InvertedGoldbachLineStyle == invertedGoldbachLineStyle && cacheRedTailGoldbachLevels[idx].MidpointLineWidth == midpointLineWidth && cacheRedTailGoldbachLevels[idx].MidpointLineStyle == midpointLineStyle && cacheRedTailGoldbachLevels[idx].ShowLabels == showLabels && cacheRedTailGoldbachLevels[idx].ShowPdAreas == showPdAreas && cacheRedTailGoldbachLevels[idx].ShowEquilibrium == showEquilibrium && cacheRedTailGoldbachLevels[idx].ShowNonGoldbachLevels == showNonGoldbachLevels && cacheRedTailGoldbachLevels[idx].ShowMidpointLevels == showMidpointLevels && cacheRedTailGoldbachLevels[idx].ShowInvertedGoldbachLevels == showInvertedGoldbachLevels && cacheRedTailGoldbachLevels[idx].ShowHiddenRange == showHiddenRange && cacheRedTailGoldbachLevels[idx].ShowPo3StopRuns == showPo3StopRuns && cacheRedTailGoldbachLevels[idx].ShowPo3StdvLevels == showPo3StdvLevels && cacheRedTailGoldbachLevels[idx].LabelFontSize == labelFontSize && cacheRedTailGoldbachLevels[idx].ShowPriceOnLabels == showPriceOnLabels && cacheRedTailGoldbachLevels[idx].Po3StopRunSize == po3StopRunSize && cacheRedTailGoldbachLevels[idx].Po3StdvInterval == po3StdvInterval && cacheRedTailGoldbachLevels[idx].EnableLevelMerging == enableLevelMerging && cacheRedTailGoldbachLevels[idx].MergingThreshold == mergingThreshold && cacheRedTailGoldbachLevels[idx].UseAutoScaling == useAutoScaling && cacheRedTailGoldbachLevels[idx].AutoCalculatePO3 == autoCalculatePO3 && cacheRedTailGoldbachLevels[idx].ADRLookbackPeriod == aDRLookbackPeriod && cacheRedTailGoldbachLevels[idx].ShowInfoBox == showInfoBox && cacheRedTailGoldbachLevels[idx].InfoBoxLocation == infoBoxLocation && cacheRedTailGoldbachLevels[idx].InfoBoxFontSize == infoBoxFontSize && cacheRedTailGoldbachLevels[idx].SettlementHour == settlementHour && cacheRedTailGoldbachLevels[idx].SettlementMinute == settlementMinute && cacheRedTailGoldbachLevels[idx].SessionStartHour == sessionStartHour && cacheRedTailGoldbachLevels[idx].SessionStartMinute == sessionStartMinute && cacheRedTailGoldbachLevels[idx].SettlementTimeZone == settlementTimeZone && cacheRedTailGoldbachLevels[idx].AutoDetectSettlementTime == autoDetectSettlementTime && cacheRedTailGoldbachLevels[idx].Po3Shift == po3Shift && cacheRedTailGoldbachLevels[idx].DebugMode == debugMode && cacheRedTailGoldbachLevels[idx].EqualsInput(input))
						return cacheRedTailGoldbachLevels[idx];
			return CacheIndicator<RedTailGoldbachLevels>(new RedTailGoldbachLevels(){ Po3Range = po3Range, CustomPo3Value = customPo3Value, UseDynamicMode = useDynamicMode, UseManualFixPrice = useManualFixPrice, ManualFixPrice = manualFixPrice, FixPriceOffset = fixPriceOffset, FixPriceColor = fixPriceColor, PremiumColor = premiumColor, DiscountColor = discountColor, EquilibriumColor = equilibriumColor, NonGoldbachColor = nonGoldbachColor, MidpointColor = midpointColor, FireEmojiColor = fireEmojiColor, Po3StopRunColor = po3StopRunColor, Po3StdvColor = po3StdvColor, InvertedGoldbachColor = invertedGoldbachColor, HistoricalBarsToShow = historicalBarsToShow, BarsToRight = barsToRight, ProjectionOffset = projectionOffset, FixLineWidth = fixLineWidth, FixLineStyle = fixLineStyle, PremiumLineWidth = premiumLineWidth, PremiumLineStyle = premiumLineStyle, DiscountLineWidth = discountLineWidth, DiscountLineStyle = discountLineStyle, BoundaryLineWidth = boundaryLineWidth, BoundaryLineStyle = boundaryLineStyle, NonGoldbachLineWidth = nonGoldbachLineWidth, NonGoldbachLineStyle = nonGoldbachLineStyle, InvertedGoldbachLineWidth = invertedGoldbachLineWidth, InvertedGoldbachLineStyle = invertedGoldbachLineStyle, MidpointLineWidth = midpointLineWidth, MidpointLineStyle = midpointLineStyle, ShowLabels = showLabels, ShowPdAreas = showPdAreas, ShowEquilibrium = showEquilibrium, ShowNonGoldbachLevels = showNonGoldbachLevels, ShowMidpointLevels = showMidpointLevels, ShowInvertedGoldbachLevels = showInvertedGoldbachLevels, ShowHiddenRange = showHiddenRange, ShowPo3StopRuns = showPo3StopRuns, ShowPo3StdvLevels = showPo3StdvLevels, LabelFontSize = labelFontSize, ShowPriceOnLabels = showPriceOnLabels, Po3StopRunSize = po3StopRunSize, Po3StdvInterval = po3StdvInterval, EnableLevelMerging = enableLevelMerging, MergingThreshold = mergingThreshold, UseAutoScaling = useAutoScaling, AutoCalculatePO3 = autoCalculatePO3, ADRLookbackPeriod = aDRLookbackPeriod, ShowInfoBox = showInfoBox, InfoBoxLocation = infoBoxLocation, InfoBoxFontSize = infoBoxFontSize, SettlementHour = settlementHour, SettlementMinute = settlementMinute, SessionStartHour = sessionStartHour, SessionStartMinute = sessionStartMinute, SettlementTimeZone = settlementTimeZone, AutoDetectSettlementTime = autoDetectSettlementTime, Po3Shift = po3Shift, DebugMode = debugMode }, input, ref cacheRedTailGoldbachLevels);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.RedTailGoldbachLevels RedTailGoldbachLevels(NinjaTrader.NinjaScript.Indicators.Po3RangeType po3Range, int customPo3Value, bool useDynamicMode, bool useManualFixPrice, double manualFixPrice, double fixPriceOffset, Brush fixPriceColor, Brush premiumColor, Brush discountColor, Brush equilibriumColor, Brush nonGoldbachColor, Brush midpointColor, Brush fireEmojiColor, Brush po3StopRunColor, Brush po3StdvColor, Brush invertedGoldbachColor, int historicalBarsToShow, int barsToRight, int projectionOffset, int fixLineWidth, DashStyleHelper fixLineStyle, int premiumLineWidth, DashStyleHelper premiumLineStyle, int discountLineWidth, DashStyleHelper discountLineStyle, int boundaryLineWidth, DashStyleHelper boundaryLineStyle, int nonGoldbachLineWidth, DashStyleHelper nonGoldbachLineStyle, int invertedGoldbachLineWidth, DashStyleHelper invertedGoldbachLineStyle, int midpointLineWidth, DashStyleHelper midpointLineStyle, bool showLabels, bool showPdAreas, bool showEquilibrium, bool showNonGoldbachLevels, bool showMidpointLevels, bool showInvertedGoldbachLevels, bool showHiddenRange, bool showPo3StopRuns, bool showPo3StdvLevels, int labelFontSize, bool showPriceOnLabels, NinjaTrader.NinjaScript.Indicators.Po3StopRunSize po3StopRunSize, int po3StdvInterval, bool enableLevelMerging, double mergingThreshold, bool useAutoScaling, bool autoCalculatePO3, int aDRLookbackPeriod, bool showInfoBox, NinjaTrader.NinjaScript.Indicators.InfoBoxPosition infoBoxLocation, int infoBoxFontSize, int settlementHour, int settlementMinute, int sessionStartHour, int sessionStartMinute, string settlementTimeZone, bool autoDetectSettlementTime, double po3Shift, bool debugMode)
		{
			return indicator.RedTailGoldbachLevels(Input, po3Range, customPo3Value, useDynamicMode, useManualFixPrice, manualFixPrice, fixPriceOffset, fixPriceColor, premiumColor, discountColor, equilibriumColor, nonGoldbachColor, midpointColor, fireEmojiColor, po3StopRunColor, po3StdvColor, invertedGoldbachColor, historicalBarsToShow, barsToRight, projectionOffset, fixLineWidth, fixLineStyle, premiumLineWidth, premiumLineStyle, discountLineWidth, discountLineStyle, boundaryLineWidth, boundaryLineStyle, nonGoldbachLineWidth, nonGoldbachLineStyle, invertedGoldbachLineWidth, invertedGoldbachLineStyle, midpointLineWidth, midpointLineStyle, showLabels, showPdAreas, showEquilibrium, showNonGoldbachLevels, showMidpointLevels, showInvertedGoldbachLevels, showHiddenRange, showPo3StopRuns, showPo3StdvLevels, labelFontSize, showPriceOnLabels, po3StopRunSize, po3StdvInterval, enableLevelMerging, mergingThreshold, useAutoScaling, autoCalculatePO3, aDRLookbackPeriod, showInfoBox, infoBoxLocation, infoBoxFontSize, settlementHour, settlementMinute, sessionStartHour, sessionStartMinute, settlementTimeZone, autoDetectSettlementTime, po3Shift, debugMode);
		}

		public Indicators.RedTailGoldbachLevels RedTailGoldbachLevels(ISeries<double> input , NinjaTrader.NinjaScript.Indicators.Po3RangeType po3Range, int customPo3Value, bool useDynamicMode, bool useManualFixPrice, double manualFixPrice, double fixPriceOffset, Brush fixPriceColor, Brush premiumColor, Brush discountColor, Brush equilibriumColor, Brush nonGoldbachColor, Brush midpointColor, Brush fireEmojiColor, Brush po3StopRunColor, Brush po3StdvColor, Brush invertedGoldbachColor, int historicalBarsToShow, int barsToRight, int projectionOffset, int fixLineWidth, DashStyleHelper fixLineStyle, int premiumLineWidth, DashStyleHelper premiumLineStyle, int discountLineWidth, DashStyleHelper discountLineStyle, int boundaryLineWidth, DashStyleHelper boundaryLineStyle, int nonGoldbachLineWidth, DashStyleHelper nonGoldbachLineStyle, int invertedGoldbachLineWidth, DashStyleHelper invertedGoldbachLineStyle, int midpointLineWidth, DashStyleHelper midpointLineStyle, bool showLabels, bool showPdAreas, bool showEquilibrium, bool showNonGoldbachLevels, bool showMidpointLevels, bool showInvertedGoldbachLevels, bool showHiddenRange, bool showPo3StopRuns, bool showPo3StdvLevels, int labelFontSize, bool showPriceOnLabels, NinjaTrader.NinjaScript.Indicators.Po3StopRunSize po3StopRunSize, int po3StdvInterval, bool enableLevelMerging, double mergingThreshold, bool useAutoScaling, bool autoCalculatePO3, int aDRLookbackPeriod, bool showInfoBox, NinjaTrader.NinjaScript.Indicators.InfoBoxPosition infoBoxLocation, int infoBoxFontSize, int settlementHour, int settlementMinute, int sessionStartHour, int sessionStartMinute, string settlementTimeZone, bool autoDetectSettlementTime, double po3Shift, bool debugMode)
		{
			return indicator.RedTailGoldbachLevels(input, po3Range, customPo3Value, useDynamicMode, useManualFixPrice, manualFixPrice, fixPriceOffset, fixPriceColor, premiumColor, discountColor, equilibriumColor, nonGoldbachColor, midpointColor, fireEmojiColor, po3StopRunColor, po3StdvColor, invertedGoldbachColor, historicalBarsToShow, barsToRight, projectionOffset, fixLineWidth, fixLineStyle, premiumLineWidth, premiumLineStyle, discountLineWidth, discountLineStyle, boundaryLineWidth, boundaryLineStyle, nonGoldbachLineWidth, nonGoldbachLineStyle, invertedGoldbachLineWidth, invertedGoldbachLineStyle, midpointLineWidth, midpointLineStyle, showLabels, showPdAreas, showEquilibrium, showNonGoldbachLevels, showMidpointLevels, showInvertedGoldbachLevels, showHiddenRange, showPo3StopRuns, showPo3StdvLevels, labelFontSize, showPriceOnLabels, po3StopRunSize, po3StdvInterval, enableLevelMerging, mergingThreshold, useAutoScaling, autoCalculatePO3, aDRLookbackPeriod, showInfoBox, infoBoxLocation, infoBoxFontSize, settlementHour, settlementMinute, sessionStartHour, sessionStartMinute, settlementTimeZone, autoDetectSettlementTime, po3Shift, debugMode);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.RedTailGoldbachLevels RedTailGoldbachLevels(NinjaTrader.NinjaScript.Indicators.Po3RangeType po3Range, int customPo3Value, bool useDynamicMode, bool useManualFixPrice, double manualFixPrice, double fixPriceOffset, Brush fixPriceColor, Brush premiumColor, Brush discountColor, Brush equilibriumColor, Brush nonGoldbachColor, Brush midpointColor, Brush fireEmojiColor, Brush po3StopRunColor, Brush po3StdvColor, Brush invertedGoldbachColor, int historicalBarsToShow, int barsToRight, int projectionOffset, int fixLineWidth, DashStyleHelper fixLineStyle, int premiumLineWidth, DashStyleHelper premiumLineStyle, int discountLineWidth, DashStyleHelper discountLineStyle, int boundaryLineWidth, DashStyleHelper boundaryLineStyle, int nonGoldbachLineWidth, DashStyleHelper nonGoldbachLineStyle, int invertedGoldbachLineWidth, DashStyleHelper invertedGoldbachLineStyle, int midpointLineWidth, DashStyleHelper midpointLineStyle, bool showLabels, bool showPdAreas, bool showEquilibrium, bool showNonGoldbachLevels, bool showMidpointLevels, bool showInvertedGoldbachLevels, bool showHiddenRange, bool showPo3StopRuns, bool showPo3StdvLevels, int labelFontSize, bool showPriceOnLabels, NinjaTrader.NinjaScript.Indicators.Po3StopRunSize po3StopRunSize, int po3StdvInterval, bool enableLevelMerging, double mergingThreshold, bool useAutoScaling, bool autoCalculatePO3, int aDRLookbackPeriod, bool showInfoBox, NinjaTrader.NinjaScript.Indicators.InfoBoxPosition infoBoxLocation, int infoBoxFontSize, int settlementHour, int settlementMinute, int sessionStartHour, int sessionStartMinute, string settlementTimeZone, bool autoDetectSettlementTime, double po3Shift, bool debugMode)
		{
			return indicator.RedTailGoldbachLevels(Input, po3Range, customPo3Value, useDynamicMode, useManualFixPrice, manualFixPrice, fixPriceOffset, fixPriceColor, premiumColor, discountColor, equilibriumColor, nonGoldbachColor, midpointColor, fireEmojiColor, po3StopRunColor, po3StdvColor, invertedGoldbachColor, historicalBarsToShow, barsToRight, projectionOffset, fixLineWidth, fixLineStyle, premiumLineWidth, premiumLineStyle, discountLineWidth, discountLineStyle, boundaryLineWidth, boundaryLineStyle, nonGoldbachLineWidth, nonGoldbachLineStyle, invertedGoldbachLineWidth, invertedGoldbachLineStyle, midpointLineWidth, midpointLineStyle, showLabels, showPdAreas, showEquilibrium, showNonGoldbachLevels, showMidpointLevels, showInvertedGoldbachLevels, showHiddenRange, showPo3StopRuns, showPo3StdvLevels, labelFontSize, showPriceOnLabels, po3StopRunSize, po3StdvInterval, enableLevelMerging, mergingThreshold, useAutoScaling, autoCalculatePO3, aDRLookbackPeriod, showInfoBox, infoBoxLocation, infoBoxFontSize, settlementHour, settlementMinute, sessionStartHour, sessionStartMinute, settlementTimeZone, autoDetectSettlementTime, po3Shift, debugMode);
		}

		public Indicators.RedTailGoldbachLevels RedTailGoldbachLevels(ISeries<double> input , NinjaTrader.NinjaScript.Indicators.Po3RangeType po3Range, int customPo3Value, bool useDynamicMode, bool useManualFixPrice, double manualFixPrice, double fixPriceOffset, Brush fixPriceColor, Brush premiumColor, Brush discountColor, Brush equilibriumColor, Brush nonGoldbachColor, Brush midpointColor, Brush fireEmojiColor, Brush po3StopRunColor, Brush po3StdvColor, Brush invertedGoldbachColor, int historicalBarsToShow, int barsToRight, int projectionOffset, int fixLineWidth, DashStyleHelper fixLineStyle, int premiumLineWidth, DashStyleHelper premiumLineStyle, int discountLineWidth, DashStyleHelper discountLineStyle, int boundaryLineWidth, DashStyleHelper boundaryLineStyle, int nonGoldbachLineWidth, DashStyleHelper nonGoldbachLineStyle, int invertedGoldbachLineWidth, DashStyleHelper invertedGoldbachLineStyle, int midpointLineWidth, DashStyleHelper midpointLineStyle, bool showLabels, bool showPdAreas, bool showEquilibrium, bool showNonGoldbachLevels, bool showMidpointLevels, bool showInvertedGoldbachLevels, bool showHiddenRange, bool showPo3StopRuns, bool showPo3StdvLevels, int labelFontSize, bool showPriceOnLabels, NinjaTrader.NinjaScript.Indicators.Po3StopRunSize po3StopRunSize, int po3StdvInterval, bool enableLevelMerging, double mergingThreshold, bool useAutoScaling, bool autoCalculatePO3, int aDRLookbackPeriod, bool showInfoBox, NinjaTrader.NinjaScript.Indicators.InfoBoxPosition infoBoxLocation, int infoBoxFontSize, int settlementHour, int settlementMinute, int sessionStartHour, int sessionStartMinute, string settlementTimeZone, bool autoDetectSettlementTime, double po3Shift, bool debugMode)
		{
			return indicator.RedTailGoldbachLevels(input, po3Range, customPo3Value, useDynamicMode, useManualFixPrice, manualFixPrice, fixPriceOffset, fixPriceColor, premiumColor, discountColor, equilibriumColor, nonGoldbachColor, midpointColor, fireEmojiColor, po3StopRunColor, po3StdvColor, invertedGoldbachColor, historicalBarsToShow, barsToRight, projectionOffset, fixLineWidth, fixLineStyle, premiumLineWidth, premiumLineStyle, discountLineWidth, discountLineStyle, boundaryLineWidth, boundaryLineStyle, nonGoldbachLineWidth, nonGoldbachLineStyle, invertedGoldbachLineWidth, invertedGoldbachLineStyle, midpointLineWidth, midpointLineStyle, showLabels, showPdAreas, showEquilibrium, showNonGoldbachLevels, showMidpointLevels, showInvertedGoldbachLevels, showHiddenRange, showPo3StopRuns, showPo3StdvLevels, labelFontSize, showPriceOnLabels, po3StopRunSize, po3StdvInterval, enableLevelMerging, mergingThreshold, useAutoScaling, autoCalculatePO3, aDRLookbackPeriod, showInfoBox, infoBoxLocation, infoBoxFontSize, settlementHour, settlementMinute, sessionStartHour, sessionStartMinute, settlementTimeZone, autoDetectSettlementTime, po3Shift, debugMode);
		}
	}
}

#endregion
