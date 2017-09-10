using com.bloomberg.emsx.samples;
using com.bloomberg.mktdata.samples;
using LogEmsx = com.bloomberg.emsx.samples.Log;
using LogEmkt = com.bloomberg.mktdata.samples.Log;
using EMSXField = com.bloomberg.emsx.samples.Field;
using EasyMSXNotification = com.bloomberg.emsx.samples.Notification;
using EasyMSXFieldChange = com.bloomberg.emsx.samples.FieldChange;
using EasyMSXNotificationHandler = com.bloomberg.emsx.samples.NotificationHandler;
using EasyMKTNotificationHandler = com.bloomberg.mktdata.samples.NotificationHandler;
using EasyMKTNotification = com.bloomberg.mktdata.samples.Notification;
using System;
using com.bloomberg.samples.rulemsx;
using static com.bloomberg.samples.rulemsx.RuleMSX;
using System.Collections.Generic;

namespace com.bloomberg.samples.rulemsx.test {

    public class RuleMSXTest {

        private RuleMSX rmsx;
        private EasyMSX emsx;
        private EasyMKT emkt;

        static void Main(string[] args) {
            System.Console.WriteLine("Bloomberg - RMSX - RuleMSXTest\n");
            RuleMSXTest example = new RuleMSXTest();
            example.run();
            System.Console.WriteLine("Press any key to terminate...");
            System.Console.ReadLine();

        }

        public RuleMSXTest() {

            System.Console.WriteLine("RuleMSXTest started...");

            System.Console.WriteLine("Instantiating RuleMSX...");
            this.rmsx = new RuleMSX();

            System.Console.WriteLine("...done.");

            System.Console.WriteLine("Instantiating EasyMSX...");

            LogEmsx.logLevel = LogEmsx.LogLevels.NONE;

            try
            {
                this.emsx = new EasyMSX(EasyMSX.Environment.BETA);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine(ex);
            }

            System.Console.WriteLine("...done.");

            System.Console.WriteLine("Instantiating EasyMKT...");
            this.emkt = new EasyMKT();
            System.Console.WriteLine("...done.");

        }

        public void run()
        {

            System.Console.WriteLine("RuleMSXTest running...");

            emkt.AddField("BID");
            emkt.AddField("ASK");
            emkt.AddField("MID");
            emkt.AddField("LAST_PRICE");

            DataSet rmsxTest;

            foreach (Order o in emsx.orders)
            {

                // Create new DataSet for each order
                rmsxTest = this.rmsx.createDataSet("RMSXTest" + o.field("EMSX_SEQUENCE").value());

                System.Console.WriteLine("New DataSet created: " + rmsxTest.getName());

                // Create new data point for each required field
                DataPoint orderNo = rmsxTest.addDataPoint("OrderNo");
                orderNo.SetDataPointSource(new EMSXFieldDataPoint(o.field("EMSX_SEQUENCE"), orderNo));
                System.Console.WriteLine("New DataPoint added : " + orderNo.GetName());

                DataPoint assetClass = rmsxTest.addDataPoint("AssetClass");
                assetClass.SetDataPointSource(new EMSXFieldDataPoint(o.field("EMSX_ASSET_CLASS"), assetClass));
                System.Console.WriteLine("New DataPoint added : " + assetClass.GetName());

                DataPoint amount = rmsxTest.addDataPoint("Amount");
                amount.SetDataPointSource(new EMSXFieldDataPoint(o.field("EMSX_AMOUNT"), amount));
                System.Console.WriteLine("New DataPoint added : " + amount.GetName());

                DataPoint exchange = rmsxTest.addDataPoint("Exchange");
                exchange.SetDataPointSource(new EMSXFieldDataPoint(o.field("EMSX_EXCHANGE"), exchange));
                System.Console.WriteLine("New DataPoint added : " + exchange.GetName());

                DataPoint ticker = rmsxTest.addDataPoint("Ticker");
                ticker.SetDataPointSource(new EMSXFieldDataPoint(o.field("EMSX_TICKER"), ticker));
                System.Console.WriteLine("New DataPoint added : " + ticker.GetName());

                DataPoint isin = rmsxTest.addDataPoint("ISIN");
                isin.SetDataPointSource(new RefDataDataPoint("ID_ISIN", o.field("EMSX_TICKER").value()));
                System.Console.WriteLine("New DataPoint added : " + isin.GetName());

                System.Console.WriteLine("Adding order secuity to EasyMKT...");
                Security sec = emkt.securities.Get(o.field("EMSX_TICKER").value());
                if (sec == null)
                {
                    sec = emkt.AddSecurity(o.field("EMSX_TICKER").value());
                }

                DataPoint lastPrice = rmsxTest.addDataPoint("LastPrice");
                lastPrice.SetDataPointSource(new MktDataDataPoint("LAST_PRICE", sec));
                System.Console.WriteLine("New DataPoint added : " + lastPrice.GetName());

                DataPoint margin = rmsxTest.addDataPoint("Margin");
                margin.SetDataPointSource(new CustomNumericDataPoint(2.0f));
                System.Console.WriteLine("New DataPoint added : " + margin.GetName());

                DataPoint price = rmsxTest.addDataPoint("NewPrice");
                price.SetDataPointSource(new CustomCompoundDataPoint(margin, lastPrice));
                price.AddDependency(margin);
                price.AddDependency(lastPrice);
                System.Console.WriteLine("New DataPoint added : " + price.GetName());
            }

            System.Console.WriteLine("Starting EasyMKT...");
            emkt.start();
            System.Console.WriteLine("EasyMKT started.");

            // Create new RuleSet
            RuleSet rset = rmsx.createRuleSet("TestRules");

            Rule ruleIsLNExchange = new Rule("IsLNExchange", new StringEqualityRule("Exchange", "LN"), new RouteToBroker("BB"));
            Rule ruleIsUSExchange = new Rule("IsUSExchange", new StringEqualityRule("Exchange", "US"), new RouteToBroker("DMTB"));
            Rule ruleIsIBM = new Rule("IsIBM", new StringEqualityRule("Ticker", "IBM US Equity"), new SendAdditionalSignal("This is IBM!!"));

            rset.AddRule(ruleIsLNExchange); // Parent is ruleset, so considered an Alpha node
            rset.AddRule(ruleIsUSExchange); // Parent is ruleset, so considered an Alpha node
            ruleIsUSExchange.AddRule(ruleIsIBM); // Parent is another rule, so considered a Beta node

            // Iterate all datasets, submiting them to the ruleset
            foreach (DataSet ds in rmsx.getDataSets()) {
                rset.execute(ds);
            }

            return;
        }

        class EMSXFieldDataPoint : DataPointSource, EasyMSXNotificationHandler {

    	    private EMSXField source;
            private bool isStale = true;
            private DataPoint dataPoint;

            internal EMSXFieldDataPoint(EMSXField source, DataPoint dataPoint) {
                this.source = source;
                this.dataPoint = dataPoint;
                source.addNotificationHandler(this);
            }

            public Object GetValue() {
                return this.source.value().ToString();
            }

            public DataPointState GetState() {
                if (this.isStale) return DataPointState.STALE;
                else return DataPointState.CURRENT;
            }

            public void SetState(DataPointState state) {
                this.isStale = (state == DataPointState.STALE);
            }

            public void ProcessNotification(EasyMSXNotification notification) {

                System.Console.WriteLine("Notification event: " + this.source.name() + " on " + dataPoint.GetDataSet().getName());

                try {
                    //System.Console.WriteLine("Category: " + notification.category.ToString());
                    //System.Console.WriteLine("Type: " + notification.type.ToString());
                    foreach (EasyMSXFieldChange fc in notification.getFieldChanges()) {
                        System.Console.WriteLine("Name: " + fc.field.name() + "\tOld: " + fc.oldValue + "\tNew: " + fc.newValue);
                    }
                }
                catch (Exception ex) {
                    System.Console.WriteLine("Failed!!: " + ex.ToString());
                }
                this.isStale = true;
            }

            public void processNotification(EasyMSXNotification notification)
            {
                throw new NotImplementedException();
            }
        }
    
        class RefDataDataPoint : DataPointSource {

            private string fieldName;
            private string ticker;
            private string value;
            private bool isStale = true;

            internal RefDataDataPoint(String fieldName, String ticker) {
                this.fieldName = fieldName;
                this.ticker = ticker;
                this.value = "";
            }

            public Object GetValue() {
                if (isStale) {
                    // make ref data call to get the field value for supplied ticker
                    this.SetState(DataPointState.CURRENT);
                }
                return value;
            }

            public DataPointState GetState() {
                if (this.isStale) return DataPointState.STALE;
                else return DataPointState.CURRENT;
            }

            public void SetState(DataPointState state) {
                this.isStale = (state == DataPointState.STALE);
            }
    	
        }
    
        class MktDataDataPoint : DataPointSource, EasyMKTNotificationHandler {

            private string fieldName;
            private Security security;
            private string value;
            private bool isStale = true;

            internal MktDataDataPoint(String fieldName, Security security) {
                this.fieldName = fieldName;
                this.security = security;
                this.security.field(this.fieldName).AddNotificationHandler(this);
            }

            public Object GetValue() {
                return value;
            }

            public DataPointState GetState() {
                if (this.isStale) return DataPointState.STALE;
                else return DataPointState.CURRENT;
            }

            public void SetState(DataPointState state) {
                this.isStale = (state == DataPointState.STALE);
            }

            public void ProcessNotification(EasyMKTNotification notification) {
               if (notification.type == com.bloomberg.mktdata.samples.Notification.NotificationType.FIELD)
                {
                    this.value = notification.GetFieldChanges()[0].newValue;
                    System.Console.WriteLine("Update for " + this.security.GetName() + ": " + this.fieldName + "=" + this.value);
                }
                this.SetState(DataPointState.STALE);
            }
    	
        }
    
        class CustomNumericDataPoint : DataPointSource {

            private float value;
            private bool isStale = true;

            internal CustomNumericDataPoint(float value) {
                this.value = value;
                this.isStale = false;
            }

            public Object GetValue() {
                return value;
            }

            public DataPointState GetState() {
                if (this.isStale) return DataPointState.STALE;
                else return DataPointState.CURRENT;
            }

            public void SetState(DataPointState state) {
                this.isStale = (state == DataPointState.STALE);
            }
        }
    
        class CustomCompoundDataPoint : DataPointSource {

            private DataPoint margin;
            private DataPoint lastPrice;
            private float value;
            private bool isStale = true;

            internal CustomCompoundDataPoint(DataPoint margin, DataPoint lastPrice) {
                this.margin = margin;
                this.lastPrice = lastPrice;
            }

            public Object GetValue() {
                if (this.isStale) {
                    this.value = (float)margin.GetSource().GetValue() + (float)lastPrice.GetSource().GetValue();
                    this.isStale = false;
                }
                return value;
            }

            public DataPointState GetState() {
                if (this.isStale) return DataPointState.STALE;
                else return DataPointState.CURRENT;
            }

            public void SetState(DataPointState state) {
                this.isStale = (state == DataPointState.STALE);
            }
        }
    
        class StringEqualityRule : RuleEvaluator {

            string dataPointName;
            string match;

            internal StringEqualityRule(string dataPointName, string match) {
                this.dataPointName = dataPointName;
                this.match = match;
            }

            public bool Evaluate(DataSet dataSet) {
                return dataSet.getDataPoint(this.dataPointName).GetSource().GetValue().Equals(this.match);
            }

            public List<string> GetDependencies() {
                return new List<string> { this.dataPointName };
            }
        }
    
        class RouteToBroker : RuleAction {

            private string brokerCode;

            internal RouteToBroker(string brokerCode) {
                this.brokerCode = brokerCode;
            }

            public void Execute(DataSet dataSet) {
                // create route instruction for the order based only on data from the dataset
                System.Console.WriteLine("Created route to broker '" + this.brokerCode + "' for DataSet: " + dataSet.getName());
            }
        }
    
        class SendAdditionalSignal : RuleAction {

            private String signal;

            internal  SendAdditionalSignal(String signal) {
                this.signal = signal;
            }

            public void Execute(DataSet dataSet) {
                System.Console.WriteLine("Sending Signal for " + dataSet.getName() + ": " + this.signal);
            }
        }
    }
}