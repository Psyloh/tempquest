
using System.Collections.Generic;

namespace VsQuest
{
    public class ItemConfig
    {
        public List<ActionItem> actionItems { get; set; } = new List<ActionItem>();
    }

    public class ActionItem
    {
        public string id { get; set; }
        public string itemCode { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public List<ItemAction> actions { get; set; } = new List<ItemAction>();
        public Dictionary<string, float> attributes { get; set; } = new Dictionary<string, float>();

        /// List of custom attribute keys to show in tooltip (e.g., ["attackpower", "warmth"])
        /// If empty, NO custom attributes are shown.
        public List<string> showAttributes { get; set; } = new List<string>();

        /// List of vanilla tooltip sections to hide/suppress.
        /// Available values:
        /// - "durability" : Hides "Durability: X / Y"
        /// - "miningspeed" : Hides tool tier and mining speeds
        /// - "storage" : Hides bag storage slots and contents
        /// - "nutrition" : Hides food satiety and nutrition info
        /// - "attackpower" : Hides vanilla attack power and tier
        /// - "combustible" : Hides burn temperature, smelting info
        /// - "grinding" : Hides grinding output
        /// - "crushing" : Hides pulverizer output
        /// - "temperature" : Hides current item temperature
        /// - "modsource" : Hides which mod the item is from
        /// If empty, ALL vanilla tooltips are shown (re-implemented logic).
        public List<string> hideVanillaTooltips { get; set; } = new List<string>();
    }

    public class ItemAction
    {
        public string id { get; set; }
        public string[] args { get; set; }
    }
}
