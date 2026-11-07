namespace BotTraversal
{
    // BepInEx ConfigurationManager reads these optional public fields through reflection.
    // Display metadata only: keep the original ConfigDefinition keys for saved settings.
    internal sealed class ConfigurationManagerAttributes
    {
        public string DispName;
        public int? Order;
    }
}
