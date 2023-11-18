namespace TextToTalk.Migrations
{
    public interface IConfigurationMigration
    {
        public string Name { get; }

        public bool ShouldMigrate(PluginConfiguration config);

        public void Migrate(PluginConfiguration config);
    }
}