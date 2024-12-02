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
using System.Runtime.CompilerServices;
using StardewValley.Buildings;
using System.Reflection;
using StardewValley.GameData.Buildings;
using System.Globalization;
using StardewValley.SpecialOrders.Rewards;
using static Progressive_Tax.SendMail;
namespace Progressive_Tax
{
    public class TaxMod : Mod
    {
        public ModConfig? config;
        // Get Player Data
        public int LoveLewis => Game1.player.getFriendshipHeartLevelForNPC("Lewis");
        public int AllBuildingCount => Game1.getFarm().buildings.Count;
        public int animalCount => Game1.getFarm().getAllFarmAnimals().Count();
        public IList<Item> shippingBin => Game1.getFarm().getShippingBin(Game1.player);
        public int currentYear => Game1.year;

        public SendMail sendMail;

        public float _CurrentTaxRate;

        public string _currentSeason;

        public string _previousSeason;

        public string buildingBeingReduced;

        public int taxTier => DetermineTaxTier(currentYear);

        public Dictionary<string, int> BuildingCountperType;

        public Dictionary<string, int> BuildingNameAndPrice;
        //public string _mailPath;
        public float CurrentTaxRate
        {
            
            get => _CurrentTaxRate;
            private set => _CurrentTaxRate = value;
        }
        public class ModConfig
        {
            /*
             * 1 = 100%
             * 0.1 = 10%
             * 0.01 = 1%
             * 0.001 = 0.1%
             */
            public float BuildingTaxValue { get; set; } = 0.01f;
            public float AnimalTaxValue { get; set; } = 0.005f;
            public float MaxYearlyTax { get; set; } = 0.2f;
            public float YearlyTaxValue { get; set; } = 0.01f;

            // Lewis Friendship to Tax conversion rate (1 int = 0.5%) max 5%
            public float LewisLoveRate { get; set; } = 0.005f; // Default 0.005%
            
            public float refundRate { get; set; } = 0.15f;
            public int lowTier { get; set; } = 1000;
            public int mediumTier { get; set; } = 5000;
            public int highTier { get; set; } = 10000;
            public bool TaxGather { get; set; } = true; // handles when the tax should be collected
            // if true, every day, every shipped items
            // if false, every end season
            // warning Dont Change! haven't implement anything to this.
            public void ResetToDefaults()
            {
                BuildingTaxValue = 0.01f;
                AnimalTaxValue = 0.005f;
                MaxYearlyTax = 0.2f;
                YearlyTaxValue = 0.01f;
                LewisLoveRate = 0.005f;
                lowTier = 1000;
                mediumTier = 5000;
                highTier = 10000;
                refundRate = 0.15f;
                TaxGather = true;
            }
        }

        public Dictionary<int, string> seasonKey = new Dictionary<int, string>()
        {
            { 0, "spring" },
            { 1, "summer" },
            { 2, "fall" },
            { 3, "winter" }
        };
        public TaxData? taxData;
        public class TaxData
        {
            public int TotalTaxPaidThisSave { get; set; } = 0; // Start with no taxes paid
            public int TotalTaxPaidCurrentSeason { get; set; } = 0;
            public int TotalTaxPaidLastSeason { get; set; } = 0;
            public int TotalTaxPaidThisYear { get; set; } = 0;
            public int TotalBuildingTax { get; set; } = 0;
            public void EnsureDefaults()
            {
                var properties = GetType().GetProperties();
                foreach (var property in properties)
                {
                    var propertyType = property.PropertyType;
                    var defaultValue = propertyType.IsValueType ? Activator.CreateInstance(propertyType) : null;

                    if (property.GetValue(this) == null)
                    {
                        property.SetValue(this, defaultValue);
                    }
                }
            }
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

        }
       
        private void GameLaunched(object sender, GameLaunchedEventArgs e)
        {
            // load gmcm
            var configMenuHandler = new ConfigMenuHandler(config, Helper, ModManifest);
            configMenuHandler.RegisterMenu();
            sendMail = new SendMail(Monitor, Helper);
            sendMail.SeasonalMail = LoadMailData();
        }

        private void OnDayEnding(object? sender, DayEndingEventArgs e)
        {
            // Check if the shipping bin contains items
            if (shippingBin.Count > 0)
            {
                Monitor.Log(ShippingBinToString(), LogLevel.Info);
                int dailyTax = ApplyTaxToShippingBin(shippingBin, config.TaxGather, config.LewisLoveRate);
            } else
            {
                Monitor.Log("You have no items in the shipping bin", LogLevel.Info);
            }
            
            int tier = DetermineTaxTier(currentYear);
            Monitor.Log($"Your Tax Tier : {tier}", LogLevel.Info);
            switch (tier)
            {
                case 1:
                    TierOneRewards();
                    break;
                case 2: //love uwu
                    TierOneRewards();
                    TierTwoRewards();
                    break;
                case 3:
                    TierOneRewards();
                    TierTwoRewards();
                    // to be implemented
                    break;
                case 0:
                    Monitor.Log($"Not Enough Tax");
                    break;
            }
            Monitor.Log($"Total tax paid this season {taxData.TotalTaxPaidCurrentSeason}g", LogLevel.Info);
        }

        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            taxData = Helper.Data.ReadSaveData<TaxData>("TaxData") ?? new TaxData();
            taxData.EnsureDefaults();
            if(taxData.TotalTaxPaidThisSave != 0)
            {
                Monitor.Log("Previous save detected, using it now", LogLevel.Info);
                Monitor.Log($"Total tax paid : {taxData.TotalTaxPaidThisSave}g", LogLevel.Info);
                Monitor.Log($"Total tax paid this season {taxData.TotalTaxPaidCurrentSeason}g", LogLevel.Info);
                Monitor.Log($"Total tax paid this year : {taxData.TotalTaxPaidThisYear}", LogLevel.Info);
                Monitor.Log($"Total tax paid previous sesaon : {taxData.TotalTaxPaidLastSeason}", LogLevel.Info);
            }
            else
            {
                Monitor.Log("No save detected, starting from the beginning", LogLevel.Info);
                Monitor.Log($"Total tax paid : {taxData.TotalTaxPaidThisSave}g", LogLevel.Info);
                Monitor.Log($"Total tax paid this season {taxData.TotalTaxPaidCurrentSeason}g", LogLevel.Info);
                Monitor.Log($"Total tax paid this year : {taxData.TotalTaxPaidThisYear}", LogLevel.Info);
                Monitor.Log($"Total tax paid previous sesaon : {taxData.TotalTaxPaidLastSeason}", LogLevel.Info);
            }
            //sendMail.SeasonalMail = LoadMailData();
            BuildingCountperType = GetBuildingCounts();
            sendMail.Initialize(this, taxData);
        }

        private void OnDayStarded(object sender, DayStartedEventArgs e)
        {

        }

        private void OnSaving(object sender, SavingEventArgs e)
        {
            Helper.Data.WriteSaveData("TaxData", taxData);
        }
        // how to count, buildingtax = building price * building tax rate/28. Counted on day 28. 
        private int ApplyTaxToBuildings()
        {
            return 0;
        }

        private void TierOneRewards()
        {
            Monitor.Log($"Total tax paid : {taxData.TotalTaxPaidThisSave}g", LogLevel.Info);
            Monitor.Log($"Total tax paid this year : {taxData.TotalTaxPaidThisYear}", LogLevel.Info);
            Monitor.Log($"Total tax paid previous sesaon : {taxData.TotalTaxPaidLastSeason}", LogLevel.Info);
            int thisSeason = getSeason();
            int today = getDay();
            if (today == 28)
            {
                //int CurrentSeason, int currentYear, int currentSeasonTaxData, int ThisYearTaxData, int refundRate
                sendMail.SendSeasonalMail(thisSeason);

                taxData.TotalTaxPaidLastSeason = taxData.TotalTaxPaidCurrentSeason;
                Monitor.Log($"Changing season, sending rewards and reset tax record", LogLevel.Info);
                Monitor.Log($"Last season tax : {taxData.TotalTaxPaidLastSeason}", LogLevel.Info);
                taxData.TotalTaxPaidCurrentSeason = 0;
                if (thisSeason == 3)
                {
                    Monitor.Log($"New year, new tax record. Reseting last year tax record and sending refund money.", LogLevel.Info);
                    Monitor.Log($"Last year tax : {taxData.TotalTaxPaidThisYear}", LogLevel.Info);
                    sendMail.SendRegularMail("RefundMoney");
                    taxData.TotalTaxPaidThisYear = 0;
                }
            }
        }
        private void TierTwoRewards()
        {
            if (isThereBuildingBuilt() == true)
            {
                reduceBuildingBuiltTime();
            }
        }
        private int ApplyTaxToShippingBin(System.Collections.Generic.IList<Item> shippingBin, bool immediateMode, float LewisRate)
        {
            // Calculate tax rate
            float taxRate = CalculateShippingTaxRate(AllBuildingCount, animalCount, currentYear, LewisRate);

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
                taxData.TotalTaxPaidThisSave += totalGoldLost;
                taxData.TotalTaxPaidCurrentSeason += totalGoldLost;
                taxData.TotalTaxPaidThisYear += totalGoldLost;
                Monitor.Log($"Total gold lost due to tax (immediate mode): {totalGoldLost}g.", LogLevel.Info);
                Monitor.Log($"{totalGoldLost}g paid today for tax", LogLevel.Info);
                Monitor.Log($"Total tax paid this season :{taxData.TotalTaxPaidCurrentSeason}g", LogLevel.Info);
                Monitor.Log($"Total tax paid this year :{taxData.TotalTaxPaidThisYear}g", LogLevel.Info);
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
        private float CalculateShippingTaxRate(int buildingCount, int animalCount, int currentYear, float LewisRate)
        {
            if (config == null)
            {
                Monitor.Log("Config is not loaded; using default tax rates.", LogLevel.Warn);
                return 0;
            }

            float buildingTax = buildingCount * config.BuildingTaxValue;
            Monitor.Log($"Building Tax : {buildingTax}", LogLevel.Info);
            float animalTax = animalCount * config.AnimalTaxValue;
            Monitor.Log($"Animal Tax : {animalTax}", LogLevel.Info);
            float yearTax = currentYear * config.YearlyTaxValue;

            if (yearTax > config.MaxYearlyTax)
            {
                yearTax = config.MaxYearlyTax;
            }
            Monitor.Log($"Yearly Tax : {yearTax}", LogLevel.Info);
            float LoveLewisTaxReduction = config.LewisLoveRate * LoveLewis;
            Monitor.Log($"Lewis Tax Reduction : {config.LewisLoveRate} * {LoveLewis}", LogLevel.Info);

            // Ensure the tax does not exceed the maximum allowed
            float totalTax = buildingTax + animalTax + yearTax - LoveLewisTaxReduction;
            if (totalTax > 1)
            {
                totalTax = 1;
            }
            Monitor.Log($"Total Tax : {totalTax}", LogLevel.Info);
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

        // get current day
        public int getDay()
        {
            var day = SDate.Now().Day;
            return day;
        }
        public int getYear()
        {
            var year = SDate.Now().Year;
            return year;
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
        private static int GetBuildingPrice(string buildingType)
        {
            var blueprints = Game1.content.Load<Dictionary<string, string>>("Data/Blueprints");

            if (blueprints.TryGetValue(buildingType, out string blueprintData))
            {
                var data = blueprintData.Split('/');
                return int.TryParse(data[0], out int price) ? price : -1;
            }

            return -1; // Building type not found
        }

        private string? GetBuildingType(Building building)
        {
            if(building?.buildingType != null)
            {
                var StringBuild = building.buildingType.ToString();
                return StringBuild;
            }
            else
            {
                return "unknown building";
            }

        }

        // reduce building built time
        private void reduceBuildingBuiltTime()
        {
            var building = Game1.GetBuildingUnderConstruction();
            var buildingName = GetBuildingType(building);
            if (building.daysOfConstructionLeft != null)
            {
                if(buildingBeingReduced != buildingName)
                {
                    int currentDays = building.daysOfConstructionLeft.Value;
                    Monitor.Log($"Building built time : {currentDays}", LogLevel.Info);
                    building.daysOfConstructionLeft.Value = Math.Max(currentDays - 1, 0); // Prevent negative values
                    Monitor.Log($"Reduced construction days. Remaining days: {building.daysOfConstructionLeft.Value}");
                    buildingBeingReduced = buildingName;
                    Monitor.Log($"BuildingBeingReduced : {buildingBeingReduced} and next building {buildingName}", LogLevel.Info);
                }
                else
                {
                    Monitor.Log($"Construction time for this {buildingName} has already been reduced.", LogLevel.Info);
                }

            }
            else
            {
                // Reset when no building is under construction
                if (isThereBuildingBuilt() == false)
                {
                    Monitor.Log($"Construction of {buildingBeingReduced} is complete.");
                    buildingBeingReduced = string.Empty;
                }
                else
                {
                    Monitor.Log("No buildings are under construction.");
                }
            }
        }

        // get if there's a building under construction
        private bool isThereBuildingBuilt()
        {
            if (Game1.getFarm().isThereABuildingUnderConstruction() == true)
            {
                Monitor.Log($"Construction of {GetBuildingType(Game1.GetBuildingUnderConstruction())}.", LogLevel.Warn);
                return true;
            }
            else
            {
                Monitor.Log("No buildings are under construction.", LogLevel.Warn);
                return false;
            }

        }
        public static Dictionary<string, int> GetBuildingCounts()
        {
            // Get the current player's farm
            var farm = Game1.getFarm();

            // Initialize a dictionary to store building counts
            Dictionary<string, int> buildingCounts = new Dictionary<string, int>();

            // Loop through each building on the farm
            foreach (Building building in farm.buildings)
            {
                string buildingType = building.buildingType.Value;

                // Increment the count for this building type
                if (buildingCounts.ContainsKey(buildingType))
                {
                    buildingCounts[buildingType]++;
                }
                else
                {
                    buildingCounts[buildingType] = 1;
                }
            }

            return buildingCounts;
        }
        private int DetermineTaxTier(int year)
        {
            if (taxData.TotalTaxPaidCurrentSeason >= config.lowTier * year) return 1;
            else if (taxData.TotalTaxPaidCurrentSeason >= config.mediumTier) return 2;
            else if (taxData.TotalTaxPaidCurrentSeason >= config.highTier) return 3;
            else return 0;
        }

        public Dictionary<string, MailEntry> LoadMailData()
        {
            string modPath = Path.Combine("assets", "seasonal_mail.json");
            // Load the mail data using the helper method
            var mailData = Helper.Data.ReadJsonFile<Dictionary<string, MailEntry>>(modPath);
            return mailData;
        }
    }
}
