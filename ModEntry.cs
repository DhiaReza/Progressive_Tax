using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Objects;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

/*
For future update ideas:

Progressive Tax System
    Introduce a tax rate that increases with the player's wealth or income. 
        This adds realism and makes the game progressively challenging as the player becomes more successful.
    Add tax deductions or breaks for certain expenses or charitable actions (like donating to the community center).

Monthly Tax Statements
    Provide players with a breakdown of their taxes at the end of each in-game month. This could include:

Dynamic Tax Rates
    Make tax rates depend on in-game factors:
        Seasonal Variations: Higher taxes in winter when productivity is lower.
        Event-Based Changes: Tax changes tied to festivals, elections, or disasters.
        Local Governance: Allow players to influence the rates by befriending certain NPCs (e.g., Lewis).

Current goal:
    Add configuration file
 */

namespace Progressive_Tax
{
    public class TaxMod : Mod
    {

        private TaxConfig? config;
        // Get Player Data
        int buildingCount => Game1.getFarm().buildings.Count;
        int animalCount => Game1.getFarm().getAllFarmAnimals().Count();
        private IList<Item> shippingBin => Game1.getFarm().getShippingBin(Game1.player);
        private int currentYear => Game1.year;

        // Tax Rates
        public class TaxConfig
        {
            public float BuildingTaxValue { get; set; } = 0.01f; // Default: 1%
            public float AnimalTaxValue { get; set; } = 0.001f;  // Default: 0.1%
            public float MaxYearlyTax { get; set; } = 0.1f;      // Default: 10%
            public float YearlyTaxValue { get; set; } = 0.005f;  // Default: 0.5%
        }

        public override void Entry(IModHelper helper)
        {
            // Load configuration from the JSON file
            config = helper.ReadConfig<TaxConfig>();

            // Log the loaded values for debugging
            Monitor.Log($"BuildingTaxValue: {config.BuildingTaxValue}", LogLevel.Info);
            Monitor.Log($"AnimalTaxValue: {config.AnimalTaxValue}", LogLevel.Info);
            Monitor.Log($"MaxYearlyTax: {config.MaxYearlyTax}", LogLevel.Info);
            Monitor.Log($"YearlyTaxValue: {config.YearlyTaxValue}", LogLevel.Info);

            // Hook into the item shipping event
            helper.Events.GameLoop.DayEnding += this.OnDayEnding;
        }

        private void OnDayEnding(object? sender, DayEndingEventArgs e)
        {
            // Check if the shipping bin contains items
            Monitor.Log(ShippingBinToString(), LogLevel.Info);
            if (shippingBin.Count > 0)
            {
                ApplyTaxToShippingBin(shippingBin);
            }
        }

        private void ApplyTaxToShippingBin(System.Collections.Generic.IList<Item> shippingBin)
        {
            // Calculate tax rate
            float taxRate = CalculateTaxRate(buildingCount, animalCount, currentYear);

            // Apply tax to each item
            for (int i = 0; i < shippingBin.Count; i++)
            {
                if (shippingBin[i] is StardewValley.Object obj && obj.canBeShipped())
                {
                    int basePrice = obj.sellToStorePrice(); // Original sell price
                    int taxedPrice = (int)(basePrice * (1 - taxRate)); // Apply tax

                    // Update the price (this only affects profits)
                    obj.Price = taxedPrice;
                    Monitor.Log($"Applying tax according to {buildingCount} building(s), {animalCount} animal(s), and {currentYear} year(s) of playing");
                    Monitor.Log($"Applied {taxRate:P1} tax to {obj.DisplayName} according to. New price: {taxedPrice}g (was {basePrice}g).", LogLevel.Info);
                }
            }
        }

        // Tax Calculation
        private float CalculateTaxRate(int buildingCount, int animalCount, int currentYear)
        {
            if (config == null)
            {
                Monitor.Log("Config is not loaded; using default tax rates.", LogLevel.Warn);
                return 0;
            }

            float buildingTax = buildingCount * config.BuildingTaxValue;
            float animalTax = animalCount * config.AnimalTaxValue;
            float yearTax = currentYear * config.YearlyTaxValue;

            // Ensure the tax does not exceed the maximum allowed
            float totalTax = buildingTax + animalTax + yearTax;
            return totalTax > config.MaxYearlyTax ? config.MaxYearlyTax : totalTax;
        }

        // Debug stuff
        private string ShippingBinToString()
        {
            if (shippingBin == null || shippingBin.Count == 0)
            {
                return "Shipping bin is empty.";
            }

            System.Text.StringBuilder logBuilder = new System.Text.StringBuilder();

            logBuilder.AppendLine("Shipping Bin Contents:");

            foreach (var item in shippingBin)
            {
                if (item is StardewValley.Object obj)
                {
                    string itemName = obj.DisplayName ?? "Unknown Item";
                    int quantity = obj.Stack;
                    int basePrice = obj.sellToStorePrice();
                    logBuilder.AppendLine($"- {itemName}: Quantity {quantity}, Base Price {basePrice}g");
                }
                else
                {
                    logBuilder.AppendLine($"- {item.Name}: Non-shippable item.");
                }
            }

            return logBuilder.ToString();
        }
    }
}
