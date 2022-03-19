using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TextToTalk.Lexicons.Updater
{
    public class LexiconPackage
    {
        private const string RepoBase = "https://raw.githubusercontent.com/karashiiro/TextToTalk/main/lexicons/";

        private readonly string cachePath;
        private readonly HttpClient http;
        private readonly string packageName;

        private CachedLexicon cache;

        public LexiconPackage(HttpClient http, string packageName, string cachePath)
        {
            this.cachePath = cachePath;
            this.http = http;
            this.packageName = packageName;
        }

        public async Task<LexiconPackageInfo> GetPackageInfo()
        {
            await using var data = await GetPackageFile("package.yml");
            using var sr = new StreamReader(data);
            var info = await sr.ReadToEndAsync();
            return new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build()
                .Deserialize<LexiconPackageInfo>(info);
        }

        public async Task<Stream> GetPackageFile(string filename)
        {
            // Get the package metadata
            this.cache ??= GetCacheInfo();

            // Check the file etag
            var url = new Uri(RepoBase + this.packageName + "/" + filename);

            using var req = new HttpRequestMessage(HttpMethod.Head, url);
            var res = await this.http.SendAsync(req);
            if (!res.Headers.TryGetValues("etag", out var values))
            {
                throw new InvalidOperationException("Response has no etag header.");
            }

            var etag = values.FirstOrDefault();
            if (string.IsNullOrEmpty(etag))
            {
                throw new InvalidOperationException("Response has no etag value.");
            }

            // Check if the remote etag matches our local etag
            if (this.cache.FileETags.TryGetValue(filename, out var cachedETag) && etag == cachedETag)
            {
                if (TryGetLocalPackageStream(filename, out var localData))
                {
                    return localData;
                }
            }

            // Download the updated lexicon file and cache the updated data
            await using var fileData = await this.http.GetStreamAsync(url);
            var fileDataCopy = new MemoryStream();
            await fileData.CopyToAsync(fileDataCopy);
            fileDataCopy.Seek(0, SeekOrigin.Begin);
            SaveLocalPackageStream(filename, fileDataCopy);
            fileDataCopy.Seek(0, SeekOrigin.Begin);

            this.cache.FileETags[filename] = etag;
            SaveCacheInfo();

            return fileDataCopy;
        }

        private bool TryGetLocalPackageStream(string filename, out Stream stream)
        {
            var data = new MemoryStream();
            var path = GetCacheFilePath(filename);
            if (!File.Exists(path))
            {
                stream = Stream.Null;
                return false;
            }

            using var localData = File.OpenRead(path);
            localData.CopyTo(data);
            data.Seek(0, SeekOrigin.Begin);
            stream = data;
            return true;
        }

        private void SaveLocalPackageStream(string filename, Stream data)
        {
            var path = GetCacheFilePath(filename);
            var dir = Path.GetDirectoryName(path) ?? throw new InvalidOperationException("Directory is null.");
            Directory.CreateDirectory(dir);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            
            using var file = File.OpenWrite(path);
            data.CopyTo(file);
        }

        private CachedLexicon GetCacheInfo()
        {
            var path = GetCacheFilePath("config.json");
            if (!File.Exists(path))
            {
                return new CachedLexicon();
            }

            var data = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<CachedLexicon>(data);
        }

        private void SaveCacheInfo()
        {
            var path = GetCacheFilePath("config.json");
            var dir = Path.GetDirectoryName(path) ?? throw new InvalidOperationException("Directory is null.");
            Directory.CreateDirectory(dir);
            var data = JsonConvert.SerializeObject(this.cache);
            File.WriteAllText(path, data);
        }

        private string GetCacheFilePath(string filename)
        {
            return Path.Join(this.cachePath, this.packageName, filename);
        }

        public static string GetInternalNameFromPath(string path)
        {
            var reversed = path.Reverse().ToList();
            return string.Concat(reversed.SkipWhile(c => c != '/').Skip(1).TakeWhile(c => c != '/').Reverse());
        }
    }
}