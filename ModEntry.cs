using GenericModConfigMenu;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using StardewValley.Menus;

/*
For future update ideas:

Progressive Tax System 
    Add tax deductions or breaks for certain expenses or charitable actions (like donating to the community center).

Monthly Tax Statements
    Provide players with a breakdown of their taxes at the end of each in-game month. This could include:
        Revenue Breakdown: How much was taxed from different categories (e.g., crops, artisan goods, animal products).
        Expenses: Taxable deductions based on upgrades, maintenance costs for buildings, or animal care.

Dynamic Tax Rates
    Make tax rates depend on in-game factors:
        Seasonal Variations: Lower taxes in winter when productivity is lower.
        Local Governance: Allow players to influence the rates by befriending certain NPCs (e.g., Lewis).

Current goal:
    Introduce a tax rate that increases with the player's wealth or income. :
        building : OK
        animals : OK
        current year : OK
        tillable area : ???
    Add configuration file : OK
    Add GMCM Support : OK
 */

namespace Progressive_Tax
{
    public class TaxMod : Mod
    {

        private ModConfig? config;
        // Get Player Data
        int buildingCount => Game1.getFarm().buildings.Count;
        int animalCount => Game1.getFarm().getAllFarmAnimals().Count();
        private IList<Item> shippingBin => Game1.getFarm().getShippingBin(Game1.player);
        private int currentYear => Game1.year;

        private float _CurrentTaxRate;
        public float CurrentTaxRate
        {
            get => _CurrentTaxRate;
            private set => _CurrentTaxRate = value;
        }

        // Tax Rates
        public sealed class ModConfig
        {
            public float BuildingTaxValue { get; set; } = 0.01f; // Default: 1%
            public float AnimalTaxValue { get; set; } = 0.001f;  // Default: 0.1%
            public float MaxYearlyTax { get; set; } = 0.1f;      // Default: 10%
            public float YearlyTaxValue { get; set; } = 0.005f;  // Default: 0.5%
        }

        public override void Entry(IModHelper helper)
        {
            // Load configuration from the JSON file
            config = helper.ReadConfig<ModConfig>();

            // Log the loaded values for debugging
            Monitor.Log($"BuildingTaxValue: {config.BuildingTaxValue}", LogLevel.Info);
            Monitor.Log($"AnimalTaxValue: {config.AnimalTaxValue}", LogLevel.Info);
            Monitor.Log($"MaxYearlyTax: {config.MaxYearlyTax}", LogLevel.Info);
            Monitor.Log($"YearlyTaxValue: {config.YearlyTaxValue}", LogLevel.Info);

            // Hook into the item shipping event
            helper.Events.GameLoop.DayEnding += this.OnDayEnding;

            // Starts GMCM
            this.Helper.Events.GameLoop.GameLaunched += this.GameLaunched;
        }

        private void GameLaunched(object sender, GameLaunchedEventArgs e)
        {
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            // register mod
            configMenu.Register(
                mod: this.ModManifest,
                reset: () => this.config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this.config)
            );

            // for building
            configMenu.SetTitleScreenOnlyForNextOptions(ModManifest, true);
            configMenu.AddNumberOption(
                mod: ModManifest,
                getValue: () => config.BuildingTaxValue, // Getter for the current value
                setValue: value => config.BuildingTaxValue = value, // Setter for the value
                name: () => "Building Tax Rate", // Name displayed in the menu
                tooltip: () => "Adjust the tax rate applied per building.\n(Default: 0.1%)", // Tooltip for the option
                min: 0f, // Minimum value
                max: 0.1f, // Maximum value
                interval: 0.005f, // Step size
                formatValue: value => $"{value * 100:F1}%" // Format the value as a percentage (e.g., "1.0%")
            );

            //for animals
            configMenu.SetTitleScreenOnlyForNextOptions(ModManifest, true);
            configMenu.AddNumberOption(
                mod: ModManifest,
                getValue: () => config.AnimalTaxValue, // Getter for the current value
                setValue: value => config.AnimalTaxValue = value, // Setter for the value
                name: () => "Animal Tax Rate", // Name displayed in the menu
                tooltip: () => "Adjust the tax rate applied per animal.\n(Default: 0.5%)", // Tooltip for the option
                min: 0f, // Minimum value
                max: 0.1f, // Maximum value
                interval: 0.005f, // Step size
                formatValue: value => $"{value * 100:F1}%" // Format the value as a percentage (e.g., "1.0%")
            );

            // for yearly
            configMenu.SetTitleScreenOnlyForNextOptions(ModManifest, true);
            configMenu.AddNumberOption(
                mod: ModManifest,
                getValue: () => config.YearlyTaxValue, // Getter for the current value
                setValue: value => config.YearlyTaxValue = value, // Setter for the value
                name: () => "Yearly Tax Rate", // Name displayed in the menu
                tooltip: () => "Adjust the tax rate applied per year.\n(Default: 1%)", // Tooltip for the option
                min: 0f, // Minimum value
                max: 0.1f, // Maximum value
                interval: 0.005f, // Step size
                formatValue: value => $"{value * 100:F1}%" // Format the value as a percentage (e.g., "1.0%")
            );

            // for max yearly tax
            configMenu.SetTitleScreenOnlyForNextOptions(ModManifest, true);
            configMenu.AddNumberOption(
                mod: ModManifest,
                getValue: () => config.MaxYearlyTax, // Getter for the current value
                setValue: value => config.MaxYearlyTax = value, // Setter for the value
                name: () => "Max Yearly Tax Rate", // Name displayed in the menu
                tooltip: () => "Adjust the tax rate maximum yearly tax rate.\n(Default: 1%)", // Tooltip for the option
                min: 0f, // Minimum value
                max: 0.2f, // Maximum value
                interval: 0.005f, // Step size
                formatValue: value => $"{value * 100:F1}%" // Format the value as a percentage (e.g., "1.0%")
            );

        }

        private void OnDayEnding(object? sender, DayEndingEventArgs e)
        {
            // Check if the shipping bin contains items
            if (shippingBin.Count > 0)
            {
                Monitor.Log(ShippingBinToString(), LogLevel.Info);
                ApplyTaxToShippingBin(shippingBin);
            } else
            {
                Monitor.Log
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
            Monitor.Log($"Building Tax : {buildingTax}");
            float animalTax = animalCount * config.AnimalTaxValue;
            Monitor.Log($"Animal Tax : {animalTax}");
            float yearTax = currentYear * config.YearlyTaxValue;
            if (yearTax > config.MaxYearlyTax)
            {
                yearTax = config.MaxYearlyTax;
            }
            Monitor.Log($"Yearly Tax : {yearTax}");

            // Ensure the tax does not exceed the maximum allowed
            float totalTax = buildingTax + animalTax + yearTax;
            if (totalTax > 1)
            {
                totalTax = 1;
            }

            _CurrentTaxRate = totalTax;

            return totalTax;
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

        // to be used
        public (int tillableCount, int untillableCount, int TotalArea) GetFarmTileCounts()
        {
            int tillableCount = 0;
            int untillableCount = 0;
            int totalArea = 0;

            // Get the farm object
            Farm farm = Game1.getFarm();

            // Iterate through the tiles of the farm
            for (int x = 0; x < farm.Map.DisplayWidth / 64; x++) // Tile width
            {
                for (int y = 0; y < farm.Map.DisplayHeight / 64; y++) // Tile height
                {
                    Vector2 tile = new Vector2(x, y);

                    // Check if the tile is tillable
                    if (farm.doesTileHaveProperty(x, y, "Diggable", "Back") != null ||
                        (farm.terrainFeatures.ContainsKey(tile) && farm.terrainFeatures[tile] is HoeDirt))
                    {
                        tillableCount++;
                    }
                    else
                    {
                        untillableCount++;
                    }
                    totalArea++;
                }
            }

            return (tillableCount, untillableCount, totalArea);
        }
    }
}
