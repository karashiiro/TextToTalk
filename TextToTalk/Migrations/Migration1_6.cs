namespace TextToTalk.Migrations
{
    public class Migration1_6 : IConfigurationMigration
    {
        public bool ShouldMigrate(PluginConfiguration config)
        {
            return !config.MigratedTo1_6;
        }

        public void Migrate(PluginConfiguration config)
        {
#pragma warning disable 618
            if (config.UseWebsocket)
#pragma warning restore 618
            {
                config.Backend = TTSBackend.Websocket;
            }

            config.MigratedTo1_6 = true;
        }
    }
}