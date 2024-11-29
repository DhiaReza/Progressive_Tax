using GenericModConfigMenu;
using StardewModdingAPI;
using static Progressive_Tax.TaxMod;

namespace Progressive_Tax
{
    public class ConfigMenuHandler
    {
        private readonly ModConfig config;
        private readonly IModHelper helper;
        private readonly IManifest manifest;

        public ConfigMenuHandler(ModConfig config, IModHelper helper, IManifest manifest)
        {
            this.config = config;
            this.helper = helper;
            this.manifest = manifest;
        }

        public void RegisterMenu()
        {
            var configMenu = helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            // Register mod
            configMenu.Register(
                mod: manifest,
                reset: () => this.config.ResetToDefaults(),
                save: () => helper.WriteConfig(this.config)
            );

            // Add options for configuration
            AddFloatOption(configMenu, "Building Tax Rate", "Adjust the tax rate applied per building.\n(Default: 0.1%)",
                () => config.BuildingTaxValue, value => config.BuildingTaxValue = value, 0f, 0.1f, 0.005f);

            AddFloatOption(configMenu, "Animal Tax Rate", "Adjust the tax rate applied per animal.\n(Default: 0.5%)",
                () => config.AnimalTaxValue, value => config.AnimalTaxValue = value, 0f, 0.1f, 0.005f);

            AddFloatOption(configMenu, "Yearly Tax Rate", "Adjust the tax rate applied per year.\n(Default: 1%)",
                () => config.YearlyTaxValue, value => config.YearlyTaxValue = value, 0f, 0.1f, 0.005f);

            AddFloatOption(configMenu, "Max Yearly Tax Rate", "Adjust the maximum yearly tax rate.\n(Default: 10%)",
                () => config.MaxYearlyTax, value => config.MaxYearlyTax = value, 0f, 0.2f, 0.005f);
            AddFloatOption(configMenu, "Lewis Heart Tax Reduction", "Adjust tax reduction based on lewis heart.\n(Default: 0.5%)",
    () => config.LewisLoveRate, value => config.LewisLoveRate = value, 0f, 0.2f, 0.005f);
            AddFloatOption(configMenu, "Tax Refund Rate", "Adjust the winter tax refund rate.\n(Default: 10%)", () => config.refundRate, value => config.refundRate = value, 0, 20, 1);

            AddNumberOption(configMenu, "Low Tier Threshold", "Income threshold for low tax tier reward.", () => config.lowTier, value => config.lowTier = value, 0,
                100000, 100 );

            AddNumberOption(configMenu,"Medium Tier Threshold", "Income threshold for medium tax tier reward.",
                () => config.mediumTier, value => config.mediumTier = value, 0, 100000, 100 );

            AddNumberOption(configMenu, "High Tier Threshold", "Income threshold for high tax tier.", () => config.highTier, value => config.highTier = value, 0, 100000, 100);
        }

        private void AddFloatOption(IGenericModConfigMenuApi configMenu, string name, string tooltip,
            Func<float> getValue, Action<float> setValue, float min, float max, float interval)
        {
            configMenu.SetTitleScreenOnlyForNextOptions(manifest, true);
            configMenu.AddNumberOption(
                mod: manifest,
                getValue: getValue,
                setValue: setValue,
                name: () => name,
                tooltip: () => tooltip,
                min: min,
                max: max,
                interval: interval,
                formatValue: value => $"{value * 100:F1}%"
            );
        }
        private void AddNumberOption(IGenericModConfigMenuApi configMenu, string name, string tooltip,
    Func<int> getValue, Action<int> setValue, int min, int max, int interval)
        {
            configMenu.SetTitleScreenOnlyForNextOptions(manifest, true);
            configMenu.AddNumberOption(
                mod: manifest,
                getValue: getValue,
                setValue: setValue,
                name: () => name,
                tooltip: () => tooltip,
                min: min,
                max: max,
                interval: interval,
                formatValue: value => $"{value}"
            );
        }
        private void AddBoolOption(IGenericModConfigMenuApi configMenu, Func<bool> getValue, Action<bool> setValue, string name, string tooltip = null, string fieldId = null)
        {
            configMenu.SetTitleScreenOnlyForNextOptions(manifest, false);
            configMenu.AddBoolOption(
                mod: manifest,
                name: () => name,
                tooltip: () => tooltip,
                getValue: getValue,
                setValue: setValue,
                fieldId: fieldId
            );
        }
    }
}
