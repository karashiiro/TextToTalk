using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using TextToTalk.Lexicons.Updater;

namespace LexiconRepoCheck
{
    internal class Program
    {
        public static async Task Main()
        {
            using var http = new HttpClient();
            var repo = new LexiconRepository(http);
            var packages = await repo.FetchPackages();
            Console.WriteLine("Lexicon packages:");
            Console.WriteLine(packages.Select(p => p.Path).Aggregate("", (agg, next) => agg + next + '\n'));

            var package = packages[0];
            var packageName = LexiconPackage.GetInternalNameFromPath(package.Path);
            var lexicon = new LexiconPackage(http, packageName, "./cache");
            var info = await lexicon.GetPackageInfo();
            Console.WriteLine(info.Description);
        }
    }
}
