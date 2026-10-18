using System;
using System.Collections.Generic;

namespace Keske
{
    [Serializable]
    public class KeskeProgress
    {
        public List<string> items = new List<string>();
        public List<string> fixedRisks = new List<string>();
        public List<string> memories = new List<string>();
        public List<string> observedRisks = new List<string>();
        public bool bagPacked;
        public bool familyPlan;
        public bool previewSeen;
        public bool testedCurrentPreparation;
        public float seconds;

        public static readonly string[] RiskIds = { "bookcase", "wardrobe", "heavy", "route", "television", "dryer" };
        public static readonly string[] SupplyIds = { "water", "food", "aid", "blanket", "radio", "torch", "batteries", "whistle", "documents" };

        public void Normalize()
        {
            if (items == null) items = new List<string>();
            if (fixedRisks == null) fixedRisks = new List<string>();
            if (memories == null) memories = new List<string>();
            if (observedRisks == null) observedRisks = new List<string>();
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0) seconds = 0;
        }

        public bool Has(string id) { return items.Contains(id); }
        public bool Fixed(string id) { return fixedRisks.Contains(id); }
        public void Changed() { testedCurrentPreparation = false; }
        public void Collect(string id) { if (!Has(id)) items.Add(id); Changed(); }
        public void Fix(string id) { if (!Fixed(id)) fixedRisks.Add(id); Changed(); }
        public int SupplyCount
        {
            get { int n = 0; foreach (var id in SupplyIds) if (Has(id)) n++; return n; }
        }
        public bool CanPack { get { return SupplyCount == SupplyIds.Length; } }
        public int Completed
        {
            get
            {
                int n = 0;
                foreach (var id in RiskIds) if (Fixed(id)) n++;
                if (bagPacked) n++;
                if (familyPlan) n++;
                return n;
            }
        }
        public static int TotalSteps { get { return RiskIds.Length + 2; } }
        public bool Ready { get { return Completed == TotalSteps; } }
        public bool CanFinish { get { return Ready && testedCurrentPreparation; } }
    }
}
