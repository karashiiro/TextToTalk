using System;
using System.Numerics;
using System.Threading.Tasks;
using TextToTalk.Backends.Polly;
using TextToTalk.Backends.System;
using TextToTalk.Backends.Websocket;
using TextToTalk.GameEnums;

namespace TextToTalk.Backends
{
    public class VoiceBackendManager : VoiceBackend
    {
        private readonly PluginConfiguration config;
        private readonly SharedState sharedState;

        private VoiceBackend backend;

        public bool BackendLoading { get; private set; }

        public VoiceBackendManager(PluginConfiguration config, SharedState sharedState)
        {
            this.config = config;
            this.sharedState = sharedState;
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
            _ = Task.Run(() =>
            {
                BackendLoading = true;
                var newBackend = CreateBackendFor(this.config.Backend);
                this.backend?.Dispose();
                this.backend = newBackend;
                BackendLoading = false;
            });
        }

        public Vector4 GetBackendTitleBarColor()
        {
            return this.backend.TitleBarColor;
        }

        private VoiceBackend CreateBackendFor(TTSBackend backendKind)
        {
            return backendKind switch
            {
                TTSBackend.System => new SystemBackend(this.config),
                TTSBackend.Websocket => new WebsocketBackend(this.config, this.sharedState),
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