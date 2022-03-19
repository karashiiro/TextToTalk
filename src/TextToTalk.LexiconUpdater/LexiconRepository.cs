using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace TextToTalk.LexiconUpdater
{
    public class LexiconRepository
    {
        private const string IndexUrl = "https://api.github.com/repos/karashiiro/TextToTalk/git/trees/main?recursive=1";

        private readonly HttpClient http;

        public LexiconRepository(HttpClient http)
        {
            this.http = http;
        }

        public async Task<IList<LexiconDirectoryItem>> FetchPackages()
        {
            var items = await FetchAll();
            return items
                .Where(i => i.Path.StartsWith("lexicons/"))
                .Where(i => IsPackageMetadataFile(i.Path))
                .ToList();
        }

        private async Task<LexiconDirectoryItem[]> FetchAll()
        {
            using var req = new HttpRequestMessage
            {
                RequestUri = new Uri(IndexUrl),
                Method = HttpMethod.Get,
            };
            req.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:98.0) Gecko/20100101 Firefox/98.0");

            var res = await this.http.SendAsync(req);
            if (!res.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Response status code does not indicate success: {res.StatusCode}");
            }

            var data = await res.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<LexiconDirectory>(data);
            return result?.Tree ?? throw new InvalidOperationException("GitHub API response was null.");
        }

        private static bool IsPackageMetadataFile(string filename)
        {
            var filenameLower = filename.ToLowerInvariant();
            return filenameLower.EndsWith("package.yml");
        }
    }
}