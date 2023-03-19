using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace TextToTalk.UI.SourceGeneration.Tests;

public static class CSharpSourceGeneratorVerifier<TTest, TSourceGenerator>
    where TSourceGenerator : IIncrementalGenerator, new()
{
    public class Test : CSharpSourceGeneratorTest<EmptySourceGeneratorProvider, XUnitVerifier>
    {
        private readonly string? _testMethod;

        public Test([CallerMemberName] string? testMethod = null)
        {
            CompilerDiagnostics = CompilerDiagnostics.Warnings;

            _testMethod = testMethod;
        }

        public LanguageVersion LanguageVersion { get; set; } = LanguageVersion.Default;

        protected override IEnumerable<ISourceGenerator> GetSourceGenerators()
        {
            yield return new TSourceGenerator().AsSourceGenerator();
        }

        protected override CompilationOptions CreateCompilationOptions()
        {
            var compilationOptions = (CSharpCompilationOptions)base.CreateCompilationOptions();
            return compilationOptions
                .WithAllowUnsafe(false)
                .WithWarningLevel(99)
                .WithSpecificDiagnosticOptions(
                    compilationOptions.SpecificDiagnosticOptions
                        .SetItem("CS8019", ReportDiagnostic.Warn)
                        .SetItem("CS1701", ReportDiagnostic.Suppress)
                        .SetItem("CS1591", ReportDiagnostic.Suppress));
        }

        protected override ParseOptions CreateParseOptions()
        {
            return ((CSharpParseOptions)base.CreateParseOptions()).WithLanguageVersion(LanguageVersion);
        }

        protected override async Task<Compilation> GetProjectCompilationAsync(Project project, IVerifier verifier,
            CancellationToken cancellationToken)
        {
            var compilation = await base.GetProjectCompilationAsync(project, verifier, cancellationToken);
            var expectedNames = new HashSet<string>();
            foreach (var tree in compilation.SyntaxTrees.Skip(project.DocumentIds.Count))
            {
                expectedNames.Add(Path.GetFileName(tree.FilePath));
            }

            var currentTestPrefix = $"{typeof(TTest).Namespace}.Resources.{_testMethod}.";
            foreach (var name in GetType().Assembly.GetManifestResourceNames())
            {
                if (!name.StartsWith(currentTestPrefix))
                {
                    continue;
                }

                if (!expectedNames.Contains(name[currentTestPrefix.Length..]))
                {
                    throw new InvalidOperationException(
                        $"Unexpected test resource: {name[currentTestPrefix.Length..]}");
                }
            }

            return compilation;
        }

        public Test AddGeneratedSources([CallerMemberName] string? testMethod = null)
        {
            var expectedPrefix = $"{typeof(TTest).Namespace}.Resources.{testMethod}.";
            foreach (var resourceName in typeof(Test).Assembly.GetManifestResourceNames())
            {
                if (!resourceName.StartsWith(expectedPrefix))
                {
                    continue;
                }

                using var resourceStream =
                    typeof(TTest).Assembly.GetManifestResourceStream(resourceName);
                if (resourceStream is null)
                {
                    throw new InvalidOperationException();
                }

                using var reader = new StreamReader(resourceStream, Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);
                var name = resourceName[expectedPrefix.Length..];
                TestState.GeneratedSources.Add((typeof(TTest), name, reader.ReadToEnd()));
            }

            return this;
        }
    }
}