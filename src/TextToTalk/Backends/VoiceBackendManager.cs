using System;
using System.Net.Http;
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
        private readonly HttpClient http;
        private readonly PluginConfiguration config;
        private readonly SharedState sharedState;

        private VoiceBackend backend;

        public bool BackendLoading { get; private set; }

        public VoiceBackendManager(PluginConfiguration config, HttpClient http, SharedState sharedState)
        {
            this.config = config;
            this.http = http;
            this.sharedState = sharedState;
            this.backend = CreateBackendFor(this.config.Backend);
        }

        public override void Say(TextSource source, Gender gender, string text)
        {
            this.backend?.Say(source, gender, text);
        }

        public override void CancelAllSpeech()
        {
            this.backend?.CancelAllSpeech();
        }

        public override void CancelSay(TextSource source)
        {
            this.backend?.CancelSay(source);
        }

        public override void DrawSettings(ImExposedFunctions helpers)
        {
            this.backend?.DrawSettings(helpers);
        }

        public override TextSource GetCurrentlySpokenTextSource()
        {
            return this.backend?.GetCurrentlySpokenTextSource() ?? TextSource.None;
        }

        public void SetBackend(TTSBackend backendKind)
        {
            _ = Task.Run(() =>
            {
                BackendLoading = true;
                var newBackend = CreateBackendFor(backendKind);
                var oldBackend = this.backend;
                this.backend = newBackend;
                BackendLoading = false;
                oldBackend?.Dispose();
            });
        }

        public Vector4 GetBackendTitleBarColor()
        {
            return this.backend?.TitleBarColor ?? TitleBarColor;
        }

        private VoiceBackend CreateBackendFor(TTSBackend backendKind)
        {
            return backendKind switch
            {
                TTSBackend.System => new SystemBackend(this.config, this.http),
                TTSBackend.Websocket => new WebsocketBackend(this.config, this.sharedState),
                TTSBackend.AmazonPolly => new AmazonPollyBackend(this.config, this.http),
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