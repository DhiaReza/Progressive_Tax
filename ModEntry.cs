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
using StardewModdingAPI.Utilities;

namespace Progressive_Tax
{
    public class TaxMod : Mod
    {

        private ModConfig? config;
        // Get Player Data
        int LoveLewis => Game1.player.getFriendshipLevelForNPC("Lewis");
        int buildingCount => Game1.getFarm().buildings.Count;
        int animalCount => Game1.getFarm().getAllFarmAnimals().Count();
        private IList<Item> shippingBin => Game1.getFarm().getShippingBin(Game1.player);
        private int currentYear => Game1.year;

        private float _CurrentTaxRate;

        private TaxData? taxData;

        private string _previousSeason;
        public float CurrentTaxRate
        {
            get => _CurrentTaxRate;
            private set => _CurrentTaxRate = value;
        }

        // Tax Rates
        public sealed class ModConfig
        {
            public float BuildingTaxValue { get; set; } = 0.02f; // Default: 1%
            public float AnimalTaxValue { get; set; } = 0.001f;  // Default: 0.1%
            public float MaxYearlyTax { get; set; } = 0.1f;   // Default: 10%
            public float YearlyTaxValue { get; set; } = 0.005f;// Default: 0.5%

            // Lewis Friendship to Tax conversion rate (1 int = 0.5%) max 5%
            public float LewisLoveRate { get; set; } = 0.005f; // Default 0.5%
            public bool TaxGather { get; set; } = true; // handles when the tax should be collected
            // if true, every day, every shipped items
            // if false, every end season
            public void ResetToDefaults()
            {
                BuildingTaxValue = 0.02f;
                AnimalTaxValue = 0.001f;
                MaxYearlyTax = 0.1f;
                YearlyTaxValue = 0.005f;
                LewisLoveRate = 0.005f;
                TaxGather = true;
            }
        }

        public class TaxData
        {
            public int TotalTaxPaid { get; set; } = 0; // Start with no taxes paid
            //public int ConsecutiveSeasonsPaid { get; set; } = 0; // No streak yet
            //public bool TaxPaidThisSeason { get; set; } = false; // Assume no taxes this season
            public List<int> TaxesPaidHistory { get; set; } = new List<int>(); // Empty history
        }

        public override void Entry(IModHelper helper)
        {
            // Load configuration from the JSON file
            config = helper.ReadConfig<ModConfig>();

            // Hook into the in-game state
            helper.Events.GameLoop.DayEnding += this.OnDayEnding;

            Helper.Events.GameLoop.GameLaunched += this.GameLaunched;

            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;

            helper.Events.GameLoop.Saving += this.OnSaving;

            helper.Events.GameLoop.DayStarted += this.OnDayStarded;

            //helper.Events.GameLoop.

        }

        private void GameLaunched(object sender, GameLaunchedEventArgs e)
        {
            // load gmcm
            var configMenuHandler = new ConfigMenuHandler(config, Helper, ModManifest);
            configMenuHandler.RegisterMenu();
        }

        private void OnDayEnding(object? sender, DayEndingEventArgs e)
        {
            // Check if the shipping bin contains items
            if (shippingBin.Count > 0)
            {
                Monitor.Log(ShippingBinToString(), LogLevel.Info);
                int dailyTax = ApplyTaxToShippingBin(shippingBin, config.TaxGather, config.LewisLoveRate);
                taxData.TotalTaxPaid += dailyTax;
                Monitor.Log($"{dailyTax}g paid today for tax", LogLevel.Info);
                Monitor.Log($"Total tax paid {taxData.TotalTaxPaid}g", LogLevel.Info);
                //taxData.TaxPaidThisSeason = true;
            } else
            {
                Monitor.Log("You have no items in the shipping bin", LogLevel.Info);
            }
        }

        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            Monitor.Log(getDay().ToString(),LogLevel.Info);
            taxData = Helper.Data.ReadSaveData<TaxData>("TaxData") ?? new TaxData();
            NotifyNewInstallation();
        }

        private void OnDayStarded(object sender, DayStartedEventArgs e)
        {

        }

        private void OnSaving(object sender, SavingEventArgs e)
        {
            Helper.Data.WriteSaveData("TaxData", taxData);
        }

        private int ApplyTaxToShippingBin(System.Collections.Generic.IList<Item> shippingBin, bool immediateMode, float LewisRate)
        {
            // Calculate tax rate
            float taxRate = CalculateTaxRate(buildingCount, animalCount, currentYear, LewisRate);

            int totalGoldLost = 0; // Track total lost gold
            int totalTaxAmount = 0; // Track tax amount for deferred payment

            // Apply tax to each item
            for (int i = 0; i < shippingBin.Count; i++)
            {
                if (shippingBin[i] is StardewValley.Object obj && obj.canBeShipped())
                {
                    int ItemQuantity = obj.Stack;
                    int basePrice = obj.sellToStorePrice(); // Original sell price
                    int totalBasePrice = basePrice*ItemQuantity;
                    int taxedPrice = (int)(totalBasePrice * (1 - taxRate)); // Apply tax

                    // Calculate the gold lost due to tax
                    int goldLost = totalBasePrice - taxedPrice;
                    totalGoldLost += goldLost;

                    if (immediateMode)
                    {
                        // Immediate tax collection: Update price
                        obj.Price = taxedPrice;
                        Monitor.Log($"Immediate Tax: Applied {taxRate:P1} tax to {obj.DisplayName}. New price: {taxedPrice}g (was {basePrice}g). Gold lost: {goldLost}g.", LogLevel.Info);
                    }
                    else
                    {
                        // Deferred tax: Keep original price, store tax
                        totalTaxAmount += goldLost;
                        Monitor.Log($"Deferred Tax: Calculated {taxRate:P1} tax for {obj.DisplayName}. Tax amount: {goldLost}g. Original price retained: {basePrice}g.", LogLevel.Info);
                    }
                }
            }

            if (immediateMode)
            {
                // Log total gold lost immediately
                Monitor.Log($"Total gold lost due to tax (immediate mode): {totalGoldLost}g.", LogLevel.Info);
                return totalGoldLost;
            }
            else
            {
                // Store total tax amount for deferred collection
                Monitor.Log($"Total tax stored for deferred collection: {totalTaxAmount}g.", LogLevel.Info);
                return totalTaxAmount;
            }
        }


        // Tax Calculation
        private float CalculateTaxRate(int buildingCount, int animalCount, int currentYear, float LewisRate)
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
            float LoveLewisTaxReduction = config.LewisLoveRate * LoveLewis; 

            // Ensure the tax does not exceed the maximum allowed
            float totalTax = buildingTax + animalTax + yearTax - LoveLewisTaxReduction;
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
/*        public SDate getCurrentSeason()
        {
            var date = SDate.Now();
            Monitor.Log($"{ date.Season}", LogLevel.Debug);
            return date;
        }*/
        private void NotifyNewInstallation()
        {
            if (taxData.TotalTaxPaid == 0 && !taxData.TaxesPaidHistory.Any())
            {
                //Game1.addMailForTomorrow("new_tax_mod_install", false, false);
                Monitor.Log("Detected mid-game installation. Default values will be used instead.", LogLevel.Info);
            }
            else 
            {
                Monitor.Log("Previous save detected, using it now", LogLevel.Info);
                Monitor.Log($"Total tax paid {taxData.TotalTaxPaid}g", LogLevel.Info);
            }
        }

        // handle season change
        private int getDay()
        {
            var day = SDate.Now().Day;
            return day;
        }
    }
}
