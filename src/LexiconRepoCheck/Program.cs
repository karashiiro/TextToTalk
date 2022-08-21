using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using TextToTalk.Lexicons;
using TextToTalk.Lexicons.Updater;

namespace LexiconRepoCheck
{
    internal class Program
    {
        public static async Task Main()
        {
            using var http = new HttpClient();
            var repo = new LexiconRepository(http, "cache");
            var manager = new LexiconManager();
            var packages = await repo.FetchPackages();
            Console.WriteLine("Lexicon packages:");
            Console.WriteLine(packages.Select(p => p.Path).Aggregate("", (agg, next) => agg + next + '\n'));
            
            foreach (var package in packages)
            {
                var packageName = LexiconPackage.GetInternalName(package.Path);
                await InstallPackage(manager, repo, packageName);
            }
        }

        private static async Task InstallPackage(LexiconManager lm, LexiconRepository lr, string packageName)
        {
            var package = lr.GetPackage(packageName);
            var info = await package.GetPackageInfo();
            Console.WriteLine($"{info.Name} by {info.Author}");
            Console.WriteLine(info.Description);
            Console.WriteLine(info.Files.Aggregate("Files:\n", (agg, next) => agg + next + "\n"));

            foreach (var file in info.Files)
            {
                await using var part = await package.GetPackageFile(file);
                lm.AddLexicon(part, $"{packageName}.{file}");
            }
        }
    }
}
