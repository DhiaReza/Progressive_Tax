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
            AddNumberOption(configMenu, "Building Tax Rate", "Adjust the tax rate applied per building.\n(Default: 0.1%)",
                () => config.BuildingTaxValue, value => config.BuildingTaxValue = value, 0f, 0.1f, 0.005f);

            AddNumberOption(configMenu, "Animal Tax Rate", "Adjust the tax rate applied per animal.\n(Default: 0.5%)",
                () => config.AnimalTaxValue, value => config.AnimalTaxValue = value, 0f, 0.1f, 0.005f);

            AddNumberOption(configMenu, "Yearly Tax Rate", "Adjust the tax rate applied per year.\n(Default: 1%)",
                () => config.YearlyTaxValue, value => config.YearlyTaxValue = value, 0f, 0.1f, 0.005f);

            AddNumberOption(configMenu, "Max Yearly Tax Rate", "Adjust the maximum yearly tax rate.\n(Default: 10%)",
                () => config.MaxYearlyTax, value => config.MaxYearlyTax = value, 0f, 0.2f, 0.005f);
            AddBoolOption(configMenu, () => config.TaxGather, value => config.TaxGather = value, "Tax Collect Time", "Checked : At every shippning\nNot Checked : At the end of season");
            
        }

        private void AddNumberOption(IGenericModConfigMenuApi configMenu, string name, string tooltip,
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
