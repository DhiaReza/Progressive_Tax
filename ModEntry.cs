﻿using GenericModConfigMenu;
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
using System.Runtime.CompilerServices;

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

        private SendMail mailing;

        private float _CurrentTaxRate;

        private TaxData? taxData;

        private string _currentSeason;

        private string _previousSeason;

        //public string _mailPath;

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
            public int refundRate { get; set; } = 30;
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
                refundRate = 30;
                TaxGather = true;
            }
        }

        private Dictionary<int, string> seasonKey = new Dictionary<int, string>()
        {
            { 0, "spring" },
            { 1, "summer" },
            { 2, "fall" },
            { 3, "winter" }
        };

        public class TaxData
        {
            public int TotalTaxPaidThisSave { get; set; } = 0; // Start with no taxes paid
            public int TotalTaxPaidCurrentSeason { get; set; } = 0;
            public int TotalTaxPaidThisYear{ get; set; } = 0;
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

            // get data for mailing

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
                taxData.TotalTaxPaidThisSave += dailyTax;
                taxData.TotalTaxPaidCurrentSeason += dailyTax;
                taxData.TotalTaxPaidThisYear += dailyTax;
                Monitor.Log($"{dailyTax}g paid today for tax", LogLevel.Info);
                Monitor.Log($"Total tax paid this season :{taxData.TotalTaxPaidCurrentSeason}g", LogLevel.Info);
                Monitor.Log($"Total tax paid this year :{taxData.TotalTaxPaidThisYear}g", LogLevel.Info);
                //taxData.TaxPaidThisSeason = true;
            } else
            {
                Monitor.Log("You have no items in the shipping bin", LogLevel.Info);
            }
            int today = getDay();
            int thisSeason = getSeason();
            if (today == 28)
            {
                //int CurrentSeason, int currentYear, int currentSeasonTaxData, int ThisYearTaxData, int refundRate
                mailing.SendSeasonalMail(getSeason()); //returns season index
                taxData.TotalTaxPaidCurrentSeason = 0;
                if (thisSeason == 3)
                {
                    taxData.TotalTaxPaidThisYear = 0;
                    mailing.SendRegularMail("TaxRefund");
                }
            }
        }

        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            Monitor.Log($"Year : {this.currentYear}" ,LogLevel.Info);
            taxData = Helper.Data.ReadSaveData<TaxData>("TaxData") ?? new TaxData();
            NotifyNewInstallation();

            mailing = new SendMail(Monitor, Helper, getSeason(), currentYear, taxData.TotalTaxPaidCurrentSeason, taxData.TotalTaxPaidThisYear, taxData.TotalTaxPaidThisSave, config.refundRate);

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

        private void NotifyNewInstallation()
        {
            if (taxData.TotalTaxPaidThisSave == 0)
            {
                //Game1.addMailForTomorrow("new_tax_mod_install", false, false);
                Monitor.Log("Detected mid-game installation. Starting from 0.", LogLevel.Info);
            }
            else 
            {
                Monitor.Log("Previous save detected, using it now", LogLevel.Info);
                Monitor.Log($"Total tax paid {taxData.TotalTaxPaidThisSave}g", LogLevel.Info);
                Monitor.Log($"Total tax paid this season {taxData.TotalTaxPaidCurrentSeason}g");
            }
        }

        // get current day
        public int getDay()
        {
            var day = SDate.Now().Day;
            return day;
        }

        public int getSeason()
        {
            int day = SDate.Now().SeasonIndex;
            return day;
        }
        
        // linear item count growth
        private int itemCount(int x)
        {
            int count = 9 + 18* (x - 1); //9...18..27..45..63...99...
            return count;
        }

    }
}
