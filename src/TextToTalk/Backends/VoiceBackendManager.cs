using System;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Interface;
using TextToTalk.Backends.Azure;
using TextToTalk.Backends.ElevenLabs;
using TextToTalk.Backends.GoogleCloud;
using TextToTalk.Backends.OpenAI;
using TextToTalk.Backends.Polly;
using TextToTalk.Backends.System;
using TextToTalk.Backends.Uberduck;
using TextToTalk.Backends.Websocket;
using TextToTalk.Services;

namespace TextToTalk.Backends
{
    public class VoiceBackendManager : VoiceBackend
    {
        private readonly HttpClient http;
        private readonly PluginConfiguration config;
        private readonly IUiBuilder uiBuilder;
        private readonly INotificationService notificationService;

        public VoiceBackend? Backend { get; private set; }
        public bool BackendLoading { get; private set; }

        public VoiceBackendManager(PluginConfiguration config, HttpClient http, IUiBuilder uiBuilder,
            INotificationService notificationService)
        {
            this.config = config;
            this.http = http;
            this.uiBuilder = uiBuilder;
            this.notificationService = notificationService;

            SetBackend(this.config.Backend);
        }

        public override void Say(SayRequest request)
        {
            Backend?.Say(request);
        }

        public override void CancelAllSpeech()
        {
            Backend?.CancelAllSpeech();
        }

        public override void CancelSay(TextSource source)
        {
            Backend?.CancelSay(source);
        }

        public override void DrawSettings(IConfigUIDelegates helpers)
        {
            Backend?.DrawSettings(helpers);
        }

        public override TextSource GetCurrentlySpokenTextSource()
        {
            return Backend?.GetCurrentlySpokenTextSource() ?? TextSource.None;
        }

        public void SetBackend(TTSBackend backendKind)
        {
            _ = Task.Run(() =>
            {
                BackendLoading = true;
                var newBackend = CreateBackendFor(backendKind);
                var oldBackend = Backend;
                Backend = newBackend;
                BackendLoading = false;
                oldBackend?.Dispose();
                WarnIfNoPresetsConfiguredForBackend();
            });
        }

        private void WarnIfNoPresetsConfiguredForBackend()
        {
            if (!this.config.Enabled ||
                this.config.Backend == TTSBackend.Websocket ||
                this.config.GetVoiceConfig().VoicePresets.Any(vp => vp.EnabledBackend == this.config.Backend))
            {
                return;
            }

            this.notificationService.NotifyWarning(
                "You have no voice presets configured.",
                "Please create a voice preset in the TextToTalk configuration.");
        }

        public Vector4 GetBackendTitleBarColor()
        {
            return Backend?.TitleBarColor ?? TitleBarColor;
        }

        private VoiceBackend CreateBackendFor(TTSBackend backendKind)
        {
            return backendKind switch
            {
                TTSBackend.System => new SystemBackend(this.config, this.http),
                TTSBackend.Websocket => new WebsocketBackend(this.config, this.notificationService),
                TTSBackend.AmazonPolly => new PollyBackend(this.config, this.http),
                TTSBackend.Uberduck => new UberduckBackend(this.config, this.http),
                TTSBackend.Azure => new AzureBackend(this.config, this.http),
                TTSBackend.ElevenLabs => new ElevenLabsBackend(this.config, this.http, this.notificationService),
                TTSBackend.OpenAi => new OpenAiBackend(this.config, this.http, this.notificationService),
                TTSBackend.GoogleCloud => new GoogleCloudBackend(this.config),
                _ => throw new ArgumentOutOfRangeException(nameof(backendKind)),
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Backend?.Dispose();
            }
        }
    }
}