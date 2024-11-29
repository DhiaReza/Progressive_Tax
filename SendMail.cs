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
using static Progressive_Tax.TaxMod.TaxData;
using static Progressive_Tax.TaxMod.ModConfig;

namespace Progressive_Tax
{
    public class SendMail
    {
        private Progressive_Tax.TaxMod.ModConfig config;
        
        private Progressive_Tax.TaxMod.TaxData taxInfo;
        private TaxMod gameInfo;
        private IMonitor Monitor;
        private IModHelper helper;
        public Dictionary<string, MailEntry> SeasonalMail;

        // Constructor to initialize the MailData object

        public SendMail(IMonitor monitor, IModHelper helper, TaxMod taxMod, TaxMod.TaxData taxInfo)
        {
            this.Monitor = monitor;
            this.helper = helper;
            this.config = taxMod.config;
            this.taxInfo = taxMod.taxData;
            this.gameInfo = taxMod;
        }

        private Dictionary<int, string> seasonKey = new Dictionary<int, string>()
        {
            { 0, "spring" },
            { 1, "summer" },
            { 2, "fall" },
            { 3, "winter" }
        };

        public class MailEntry
        {
            public string MailID { get; set; }
            public string Subject { get; set; }
            public string Body { get; set; }
            public string Ending { get; set; }
            public RewardData Rewards { get; set; }
        }

        public class RewardData
        {
            public List<ItemReward> Items { get; set; } = new();
            public bool Money { get; set; } = false;
        }

        public class ItemReward
        {
            public string Id { get; set; }
            public bool Quantity { get; set; }
        }
        public Dictionary<string, MailEntry> LoadMailData()
        {
            string modPath = Path.Combine("assets", "seasonal_mail.json");
            // Load the mail data using the helper method
            var mailData = helper.Data.ReadJsonFile<Dictionary<string, MailEntry>>(modPath);
            return mailData;
        }

        public void SendSeasonalMail(int season) //send mail about yesterseason
        {
            SeasonalMail = LoadMailData();
            int nextSeason = season;
            if (SeasonalMail.TryGetValue(seasonKey[nextSeason], out var mailEntry))
            {
                int localCurrentYear = gameInfo.currentYear; // safe guard if people play for more than 6 years
                string mailContent = $"{mailEntry.Subject}\n\n{mailEntry.Body}";

                // Add rewards to the mail
                if (mailEntry.Rewards.Items.Count > 0)
                    foreach (var item in mailEntry.Rewards.Items)
                    {
                        {
                            if (item.Quantity == true)
                            {
                                if (localCurrentYear > 6)
                                {
                                    localCurrentYear = 6;
                                }
                                mailContent += $"^%item object {item.Id} {itemCount(localCurrentYear)} %% ";
                            }
                            else
                            {
                                mailContent += $"^%item object {item.Id} 1 %% ";
                            }
                        }
                    }
                if (mailEntry.Rewards.Money == true)
                {
                    int refundMoney = taxInfo.TotalTaxPaidThisYear * (config.refundRate*100);
                    mailContent += $"^%item money {refundMoney} %% ";
                }
                Game1.IsThereABuildingUnderConstruction();
                mailContent += $"[#]{mailEntry.Ending}";
                switch (seasonKey[nextSeason])
                {
                    case "spring":
                        mailContent = mailContent.Replace("{gold}", taxInfo.TotalTaxPaidCurrentSeason.ToString());
                        Monitor.Log($"Sending rewards for {seasonKey[nextSeason]}");
                        break;
                    case "summer":
                        mailContent = mailContent.Replace("{gold}", taxInfo.TotalTaxPaidCurrentSeason.ToString());
                        Monitor.Log($"Sending rewards for {seasonKey[nextSeason]}");
                        break;
                    case "fall":
                        mailContent = mailContent.Replace("{gold}", taxInfo.TotalTaxPaidCurrentSeason.ToString());
                        Monitor.Log($"Sending rewards for {seasonKey[nextSeason]}");
                        break;
                    case "winter":
                        mailContent = mailContent.Replace("{gold}", taxInfo.TotalTaxPaidThisYear.ToString());
                        Monitor.Log($"Sending rewards for {seasonKey[nextSeason]}");
                        break;
                }
                // Add the mail to the game
                Game1.content.Load<Dictionary<string, string>>("Data\\Mail")[mailEntry.MailID] = mailContent;
                Game1.mailbox.Add(mailEntry.MailID);

                Monitor.Log($"Mail for {seasonKey[nextSeason]} sent: {mailContent}");
            }
            else
            {
                Monitor.Log($"No mail entry found for season: {seasonKey[nextSeason]}");
            }
        }
        private int itemCount(int x)
        {
            int count = 9 + 18 * (x - 1); //9...18..27..45..63...99...
            // 1000...2000...3000...4000...5000
            return count;
        }
        public void SendRegularMail(string mailID)
        {
            if (SeasonalMail.TryGetValue(mailID, out var mailEntry))
            {
                string mailContent = $"{mailEntry.Subject}\n\n{mailEntry.Body}";

                if (mailEntry.Rewards.Items.Count > 0)
                {
                    foreach (var item in mailEntry.Rewards.Items)
                    {
                        mailContent += $"^%item object {item.Id} {(item.Quantity ? 1 : 0)} %% ";
                    }
                }
                if (mailEntry.Rewards.Money)
                {
                    int refundMoney = taxInfo.TotalTaxPaidThisYear * config.refundRate / 100;
                    mailContent += $"^%item money {refundMoney} %% ";
                }

                mailContent += $"[#]{mailEntry.Ending} {gameInfo.currentYear}";

                Game1.content.Load<Dictionary<string, string>>("Data\\Mail")[mailEntry.MailID] = mailContent;
                Game1.addMailForTomorrow(mailEntry.MailID);

                Monitor.Log($"Regular mail '{mailID}' sent: {mailContent}", LogLevel.Info);
            }
            else
            {
                Monitor.Log($"No mail entry found for mailID: {mailID}", LogLevel.Warn);
            }
        }
    }
}
