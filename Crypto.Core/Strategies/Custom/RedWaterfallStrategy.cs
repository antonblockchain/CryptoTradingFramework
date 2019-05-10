﻿using Crypto.Core.Indicators;
using Crypto.Core.Strategies.Arbitrages.Statistical;
using CryptoMarketClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace Crypto.Core.Strategies.Custom {
    public class RedWaterfallStrategy : CustomTickerStrategy {
        public override string TypeName => "Red Waterfall";

        protected override void OnTickCore() {
            ProcessTicker(Ticker);
        }

        public override bool SupportSimulation => true;

        public override List<StrategyValidationError> Validate() {
            List<StrategyValidationError> res = base.Validate();
            if(StrategyInfo.Tickers.Count != 1) {
                res.Add(new StrategyValidationError() { DataObject = this, PropertyName = "Tickers", Description = "Right now only one ticker is suported per strategy", Value = StrategyInfo.Tickers.Count.ToString() });
            }
            return res;
        }

        public List<RedWaterfallOpenedOrder> OpenedOrders { get; } = new List<RedWaterfallOpenedOrder>();

        OrderGridInfo orderGrid;
        [TypeConverter(typeof(ExpandableObjectConverter))]
        public OrderGridInfo OrderGrid {
            get { return orderGrid; }
            set {
                if(orderGrid == value)
                    return;
                orderGrid = value;
                OnOrderGridChanged();
            }
        }

        protected virtual void OnOrderGridChanged() {
            //if(OrderGrid == null || OrderGrid.Start.Value == 0)
            //    InitializeOrderGrid();
        }

        //protected virtual void InitializeOrderGrid() {
        //    OrderGridInfo info = new OrderGridInfo();
        //    info.Start.Value = 10;
        //    info.Start.AmountPercent = 1;
        //    info.End.Value = 20;
        //    info.End.AmountPercent = 2;
        //    info.ZoneCount = 1;
        //    info.Normalize();
        //    OrderGrid = info;
        //}

        public double MinBreakPercent { get; set; } = 120;
        public int MinSupportPower { get; set; } = 5;
        public int MinSupportLength { get; set; } = 8;
        public double MinAtrValue { get; set; } = 30;
        protected SupportResistanceIndicator SRIndicator { get; private set; }
        protected AtrIndicator AtrIndicator { get; private set; }

        protected int LastCount { get; set; } = -1;
        protected RedWaterfallDataItem LastBreak { get; set; }
        protected List<RedWaterfallDataItem> Breaks { get; } = new List<RedWaterfallDataItem>();
        private void ProcessTicker(Ticker ticker) {
            if(LastCount == SRIndicator.Result.Count)
                return;

            LastCount = SRIndicator.Result.Count;
            int index = LastCount - 1;

            AddStrategyData();
            RedWaterfallDataItem item = StrategyData.Count > 0 ? (RedWaterfallDataItem)StrategyData.Last() : null;
            if(item == null)
                return;
            if(Ticker.CandleStickData.Count < 2000) /// need back data for simulation
                return;
            SRValue lastResistance = GetLastResistance();
            SRValue lastSupport = GetLastSupport();
            if(lastSupport != null & lastResistance != null) {
                item.ResIndex = lastResistance.Index;
                item.SupIndex = lastSupport.Index;
                double breakDelta = item.Close - lastSupport.Value;
                double range = lastResistance.Value - lastSupport.Value;
                item.BreakPercent = (-breakDelta) / range * 100;
                item.BreakPercent2 = (item.BreakPercent - ((RedWaterfallDataItem)StrategyData[StrategyData.Count - 2]).BreakPercent);
                if(Math.Abs(lastSupport.Value - lastResistance.Value) / lastSupport.Value < 0.003) {
                    item.BreakPercent = 0;
                    item.BreakPercent2 = 0;
                }
                if(item.BreakPercent > MinBreakPercent && item.Atr >= MinAtrValue && breakDelta < 0) {
                    if(LastBreak == null || LastBreak.ResIndex != item.ResIndex || LastBreak.SupIndex != item.SupIndex) {
                        item.Break = true;
                        item.BreakResistanceLevel = lastResistance.Value;
                        item.BreakSupportLevel = lastSupport.Value;
                        item.BreakSupportPower = lastSupport.Power;
                        LastBreak = item;
                        Breaks.Add(item);
                    }
                }
            }
            foreach(RedWaterfallDataItem bItem in Breaks) {
                if((item.Close - bItem.Close) >= (bItem.BreakSupportLevel - bItem.Close) * 0.8) {
                    bItem.CloseLength = item.Index - bItem.Index;
                    bItem.Closed = true;
                }
            }

            //if(IsRedWaterfallDetected(ticker)) {
                
            //}
        }

        private SRValue GetSupportBeforeResistance(SRValue lastResistance) {
            for(int i = SRIndicator.Support.Count - 1; i >= 0; i--) {
                SRValue val = SRIndicator.Support[i];
                if(val.Index <= lastResistance.Index)
                    return val;
            }
            return null;
        }

        private SRValue GetLastResistance() {
            return SRIndicator.Resistance.Count > 0 ? SRIndicator.Resistance.Last() : null;
        }

        private SRValue GetLastSupport() {
            return SRIndicator.Support.Count > 0 ? SRIndicator.Support.Last() : null;
        }

        protected override void InitializeDataItems() {
            TimeItem("Time");
            CandleStickItem();

            //StrategyDataItemInfo res = DataItem("ResistanceLength"); res.Color = System.Drawing.Color.Red; res.ChartType = ChartType.Bar; res.PanelIndex = 1;
            //StrategyDataItemInfo sup = DataItem("SupportLength"); sup.Color = System.Drawing.Color.Green; sup.ChartType = ChartType.Bar; sup.PanelIndex = 1;

            StrategyDataItemInfo atr = DataItem("Atr"); atr.Name = "Atr Indicator"; atr.Color = System.Drawing.Color.Red; atr.ChartType = ChartType.Line; atr.PanelIndex = 1;
            StrategyDataItemInfo resValue = DataItem("Value"); resValue.DataSourcePath = "SRIndicator.Resistance"; resValue.Color = Color.Red; resValue.ChartType = ChartType.StepLine;
            StrategyDataItemInfo supValue = DataItem("Value"); supValue.DataSourcePath = "SRIndicator.Support"; supValue.Color = Color.Blue; supValue.ChartType = ChartType.StepLine;

            //AnnotationItem("Support", "Support", System.Drawing.Color.Green, "SRLevel");
            //AnnotationItem("Resistance", "Resistance", System.Drawing.Color.Red, "SRLevel");
            StrategyDataItemInfo br = AnnotationItem("Break", "Break", System.Drawing.Color.Blue, "Close"); br.Visibility = DataVisibility.Both; br.AnnotationText = "Br"; //"Br={BreakPercent:0.00} Closed={Closed} CloseStickCount={CloseLength}";
            StrategyDataItemInfo bp = DataItem("BreakPercent"); bp.Color = Color.Green; bp.ChartType = ChartType.Bar; bp.PanelIndex = 2;

            
            //StrategyDataItemInfo sc = DataItem("SupportChange", Color.FromArgb(0x40, Color.Green), 1); sc.PanelIndex = 4; sc.ChartType = ChartType.Area;
            //StrategyDataItemInfo rc = DataItem("ResistanceChange", Color.FromArgb(0x40, Color.Red), 1); rc.PanelIndex = 4; rc.ChartType = ChartType.Area;
            //StrategyDataItemInfo bp2 = DataItem("BreakPercent2"); bp2.Color = Color.FromArgb(0x20, Color.Green);  bp2.ChartType = ChartType.Area; bp2.PanelIndex = 5;

            DataItem("Closed").Visibility = DataVisibility.Table;
            DataItem("CloseLength").Visibility = DataVisibility.Table;

            br = AnnotationItem("Break", "Break", System.Drawing.Color.Blue, "Atr"); br.PanelIndex = 1; br.Visibility = DataVisibility.Chart; br.AnnotationText = "Br"; //"Br={BreakPercent:0.00} Closed={Closed} CloseStickCount={CloseLength}";
        }

        protected List<RedWaterfallDataItem> PostProcessItems { get; } = new List<RedWaterfallDataItem>();
        private void AddStrategyData() {
            if(Ticker.CandleStickData.Count == 0)
                return;
            RedWaterfallDataItem item = new RedWaterfallDataItem();
            CandleStickData data = Ticker.CandleStickData.Last();
            item.Time = data.Time;
            item.Open = data.Open;
            item.Close = data.Close;
            item.Low = data.Low;
            item.High = data.High;
            item.Atr = AtrIndicator.Result.Last().Value;
            item.Index = StrategyData.Count;
            StrategyData.Add(item);

            PostProcessItems.Add(item);
            if(PostProcessItems.Count > 10)
                PostProcessItems.RemoveAt(0);
            SRValue sr = (SRValue)SRIndicator.Result.Last();
            if(sr.Type == SupResType.None)
                return;
            RedWaterfallDataItem srItem = PostProcessItems.FirstOrDefault(i => i.Time == sr.Time);
            if(srItem != null)
                srItem.SRValue = sr;
        }

        private bool IsRedWaterfallDetected(Ticker ticker) {
            if(ticker.CandleStickData.Count < 100)
                return false;
            int index = ticker.CandleStickData.Count - 1;
            CandleStickData l3 = ticker.CandleStickData[index];

            CandleStickData l2 = ticker.CandleStickData[index - 1];
            CandleStickData l1 = ticker.CandleStickData[index - 2];
            CandleStickData l0 = ticker.CandleStickData[index - 3];

            if(l3.Close < l3.Open && l3.Open <= l2.Close && l2.Close < l2.Open && l2.Open <= l1.Close && l1.Close < l1.Open) {
                StrategyData.Add(new RedWaterfallDataItem() { Time = l3.Time, StartPrice = l0.High, EndPrice = l3.Close, RedWaterfall = true });
                return true;
            }
            return false;
        }

        public override void Assign(StrategyBase from) {
            base.Assign(from);
            RedWaterfallStrategy st = from as RedWaterfallStrategy;
            if(st == null)
                return;
            Range = st.Range;
            ClasterizationRange = st.ClasterizationRange;
            ThresoldPerc = st.ThresoldPerc;
        }

        public int Range { get; set; } = 3;
        public int ClasterizationRange { get; set; } = 24;
        public double ThresoldPerc { get; set; } = 0.6;

        public override bool Start() {
            bool res = base.Start();
            if(!res)
                return res;
            SRIndicator = new SupportResistanceIndicator() { Ticker = Ticker, Range = Range, ClasterizationRange = ClasterizationRange, ThresoldPerc = ThresoldPerc };
            AtrIndicator = new AtrIndicator() { Ticker = Ticker };
            return res;
        }
    }

    public class RedWaterfallDataItem {
        public DateTime Time { get; set; }
        public double Open { get; set; }
        public double Close { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public bool RedWaterfall { get; set; }
        public double StartPrice { get; set; }
        public double Spread { get { return StartPrice - EndPrice; } }
        public double EndPrice { get; set; }
        public SRValue SRValue { get; set; }
        public double Atr { get; set; }
        public int Index { get; set; }
        public bool Resistance { get { return SRValue == null ? false : SRValue.Type == SupResType.Resistance; } }
        public bool Support { get { return SRValue == null ? false : SRValue.Type == SupResType.Support; } }

        public double ResistancePower { get { return SRValue == null || SRValue.Type != SupResType.Resistance ? 0 : SRValue.Power; } }
        public double SupportPower { get { return SRValue == null || SRValue.Type != SupResType.Support ? 0 : SRValue.Power; } }

        public double ResistanceChange { get { return SRValue == null || SRValue.Type != SupResType.Resistance ? 0 : SRValue.ChangePc; } }
        public double SupportChange { get { return SRValue == null || SRValue.Type != SupResType.Support ? 0 : SRValue.ChangePc; } }

        public double ResistanceLength { get { return SRValue == null || SRValue.Type != SupResType.Resistance ? 0 : SRValue.Length; } }
        public double SupportLength { get { return SRValue == null || SRValue.Type != SupResType.Support ? 0 : SRValue.Length; } }

        public double BreakResistanceLevel { get; set; }
        public double BreakSupportLevel { get; set; }
        public double BreakSupportPower { get; set; }
        public double BreakPercent { get; set; }
        public double BreakPercent2 { get; set; }

        public double SRLevel { get { return SRValue == null ? 0 : SRValue.Value; } }
        public bool Break { get; set; }
        public int ResIndex { get; set; }
        public int SupIndex { get; set; }

        public int CloseLength { get; set; }
        public bool Closed { get; set; }
    }

    public class RedWaterfallOpenedOrder {
        public string MarketName { get; set; }
        public double Value { get; set; }
        public double Amount { get; set; }
    }
}