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
using static Progressive_Tax.TaxMod;

namespace Progressive_Tax
{
    public class SendMail
    {

        public int SeasonNow { get; set; }
        public int CurrentYear { get; set; }
        public int TaxPaidCurrentSeason { get; set; }
        public int TaxPaidThisYear { get; set; }
        public int RefundRate { get; set; }
        public int PaidThisSave { get; set; }

        private IMonitor Monitor;
        private IModHelper helper;
        private Dictionary<string, MailEntry> seasonalMail;

        // Constructor to initialize the MailData object
        public SendMail(IMonitor monitor, IModHelper helper, int season, int currentYear, int taxPaidCurrentSeason, int taxPaidThisYear, int taxPaidThisSave, int refundRate)
        {
            Monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            this.helper = helper ?? throw new ArgumentNullException(nameof(helper));

            SeasonNow = season;
            CurrentYear = currentYear;
            TaxPaidCurrentSeason = taxPaidCurrentSeason;
            TaxPaidThisYear = taxPaidThisYear;
            RefundRate = refundRate;
            PaidThisSave = taxPaidThisSave;

            seasonalMail = LoadMailData(); // Initialize mail data
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
        private Dictionary<string, MailEntry> LoadMailData()
        {
            // Load the mail data using the helper method
            var mailData = helper.Data.ReadJsonFile<Dictionary<string, MailEntry>>("./assets/seasonal_mail.json");

            // If the file doesn't exist or isn't loaded correctly, return an empty dictionary
            if (mailData == null)
            {
                Monitor.Log("Failed to load mail data.", LogLevel.Error);
                return new Dictionary<string, MailEntry>();
            }

            return mailData;
        }

        public string SendSeasonalMail(int season) //send mail about yesterday season
        {
            int nextSeason = season;

            if (seasonalMail.TryGetValue(seasonKey[nextSeason], out var mailEntry))
            {
                int localCurrentYear = CurrentYear; // safe guard if people play for more than 6 years
                string mailContent = $"{mailEntry.Subject}\n\n{mailEntry.Body}";

                // Add rewards to the mail
                if (mailEntry.Rewards.Items.Count > 0)
                    foreach (var item in mailEntry.Rewards.Items)
                    {
                        {
                            if (item.Quantity == true)
                            {
                                if (CurrentYear > 6)
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
                    int refundMoney = TaxPaidThisYear * (RefundRate*100);
                    mailContent += $"^%item money {refundMoney} %% ";
                }
                Game1.IsThereABuildingUnderConstruction();
                mailContent += $"[#]{mailEntry.Ending}";
                switch (seasonKey[nextSeason])
                {
                    case "spring":
                        mailContent = mailContent.Replace("{gold}", TaxPaidCurrentSeason.ToString());
                        break;
                    case "summer":
                        mailContent = mailContent.Replace("{gold}", TaxPaidCurrentSeason.ToString());
                        break;
                    case "fall":
                        mailContent = mailContent.Replace("{gold}", TaxPaidCurrentSeason.ToString());
                        break;
                    case "winter":
                        mailContent = mailContent.Replace("{gold}", TaxPaidThisYear.ToString());
                        break;
                }
                // Add the mail to the game
                Game1.content.Load<Dictionary<string, string>>("Data\\Mail")[mailEntry.MailID] = mailContent;
                Game1.mailbox.Add(mailEntry.MailID);

                return $"Mail for {seasonKey[nextSeason]} sent: {mailContent}";
            }
            else
            {
                return "$No mail entry found for season: {seasonKey[nextSeason]}";
            }
        }
        private int itemCount(int x)
        {
            int count = 9 + 18 * (x - 1); //9...18..27..45..63...99...
            return count;
        }
        public void SendRegularMail(string mailID)
        {
            if (seasonalMail.TryGetValue(mailID, out var mailEntry))
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
                    int refundMoney = TaxPaidThisYear * RefundRate / 100;
                    mailContent += $"^%item money {refundMoney} %% ";
                }

                mailContent += $"[#]{mailEntry.Ending}";

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
