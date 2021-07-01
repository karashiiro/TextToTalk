namespace TextToTalk.Migrations
{
    public interface IConfigurationMigration
    {
        public bool ShouldMigrate(PluginConfiguration config);

        public void Migrate(PluginConfiguration config);
    }
}