namespace TextToTalk.Migrations
{
    public interface IMigration
    {
        public void Migrate(PluginConfiguration config);
    }
}