using System;
using TextToTalk.Backends.Polly;
using TextToTalk.Backends.System;
using TextToTalk.Backends.Websocket;
using TextToTalk.GameEnums;

namespace TextToTalk.Backends
{
    public class VoiceBackendManager : VoiceBackend
    {
        private readonly PluginConfiguration config;

        private VoiceBackend backend;

        public VoiceBackendManager(PluginConfiguration config)
        {
            this.config = config;
            this.backend = CreateBackendFor(this.config.Backend);
        }

        public override void Say(Gender gender, string text)
        {
            this.backend.Say(gender, text);
        }

        public override void CancelSay()
        {
            this.backend.CancelSay();
        }

        public override void DrawSettings(ImExposedFunctions helpers)
        {
            this.backend.DrawSettings(helpers);
        }

        public void SetBackend(TTSBackend backendKind)
        {
            this.backend?.Dispose();
            this.backend = CreateBackendFor(backendKind);
        }

        private VoiceBackend CreateBackendFor(TTSBackend backendKind)
        {
            return backendKind switch
            {
                TTSBackend.System => new SystemBackend(this.config),
                TTSBackend.Websocket => new WebsocketBackend(this.config),
                TTSBackend.AmazonPolly => new AmazonPollyBackend(this.config),
                _ => throw new NotImplementedException(),
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.backend.Dispose();
            }
        }
    }
}