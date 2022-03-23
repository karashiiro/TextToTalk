using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace TextToTalk.Lexicons.Updater
{
    public class LexiconRepository
    {
        private const string IndexUrl = "https://api.github.com/repos/karashiiro/TextToTalk/git/trees/main?recursive=1";

        private readonly HttpClient http;
        private readonly string cachePath;

        public LexiconRepository(HttpClient http, string cachePath)
        {
            this.http = http;
            this.cachePath = cachePath;
        }

        /// <summary>
        /// Gets a lexicon package. This does not actually download any data; data is downloaded on-demand
        /// using the <see cref="LexiconPackage"/> methods.
        /// </summary>
        /// <param name="packageName">The name of the lexicon package's folder in the repo.</param>
        public LexiconPackage GetPackage(string packageName)
        {
            return new LexiconPackage(this.http, packageName, this.cachePath);
        }

        /// <summary>
        /// Deletes all files associated with a lexicon package.
        /// </summary>
        /// <param name="packageName">The name of the lexicon package's folder in the repo.</param>
        public void RemovePackage(string packageName)
        {
            var package = GetPackage(packageName);
            package.Delete();
        }

        /// <summary>
        /// Fetches all available packages from the repository.
        /// </summary>
        public async Task<IList<LexiconDirectoryItem>> FetchPackages()
        {
            var items = await FetchAllFiles();
            return items
                .Where(i => i.Path.StartsWith("lexicons/"))
                .Where(i => IsPackageMetadataFile(i.Path))
                .ToList();
        }

        private async Task<LexiconDirectoryItem[]> FetchAllFiles()
        {
            using var req = new HttpRequestMessage
            {
                RequestUri = new Uri(IndexUrl),
                Method = HttpMethod.Get,
            };

            // Set the user agent. The GitHub API will reject our request if we don't change the user agent.
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