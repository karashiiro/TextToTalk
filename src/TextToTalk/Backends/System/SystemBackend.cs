using System.Net.Http;
using TextToTalk.GameEnums;
using TextToTalk.Lexicons;

namespace TextToTalk.Backends.System
{
    public class SystemBackend : VoiceBackend
    {
        private readonly PluginConfiguration config;
        private readonly SystemBackendUI ui;
        private readonly SystemSoundQueue soundQueue;

        public SystemBackend(PluginConfiguration config, HttpClient http)
        {
            var lexiconManager = new LexiconManager();
            LexiconUtils.LoadFromConfigSystem(lexiconManager, config);

            this.ui = new SystemBackendUI(config, lexiconManager, http);

            this.config = config;
            this.soundQueue = new SystemSoundQueue(lexiconManager);
        }

        public override void Say(TextSource source, Gender gender, string text)
        {
            var voicePreset = this.config.GetCurrentVoicePreset();
            if (this.config.UseGenderedVoicePresets)
            {
                voicePreset = gender switch
                {
                    Gender.Male => this.config.GetCurrentMaleVoicePreset(),
                    Gender.Female => this.config.GetCurrentFemaleVoicePreset(),
                    _ => this.config.GetCurrentUngenderedVoicePreset(),
                };
            }

            this.soundQueue.EnqueueSound(voicePreset, source, text);
        }

        public override void CancelAllSpeech()
        {
            this.soundQueue.CancelAllSounds();
        }

        public override void CancelSay(TextSource source)
        {
            this.soundQueue.CancelFromSource(source);
        }

        public override void DrawSettings(IConfigUIDelegates helpers)
        {
            this.ui.DrawSettings(helpers);
        }

        public override TextSource GetCurrentlySpokenTextSource()
        {
            return this.soundQueue.GetCurrentlySpokenTextSource();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.soundQueue.Dispose();
            }
        }
    }
}