using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Enumeration.Source.Generation.Tests.Exceptions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Credfeto.Enumeration.Source.Generation.Tests.Verifiers;

public abstract partial class DiagnosticVerifier
{
    private const string DEFAULT_FILE_PATH_PREFIX = "Test";
    private const string C_SHARP_DEFAULT_FILE_EXT = "cs";
    private const string VISUAL_BASIC_DEFAULT_EXT = "vb";
    private const string TEST_PROJECT_NAME = "TestProject";
    private static readonly string? AssemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location);
    private static readonly MetadataReference CorlibReference = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
    private static readonly MetadataReference SystemCoreReference = MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location);
    private static readonly MetadataReference CSharpSymbolsReference = MetadataReference.CreateFromFile(typeof(CSharpCompilation).Assembly.Location);
    private static readonly MetadataReference CodeAnalysisReference = MetadataReference.CreateFromFile(typeof(Compilation).Assembly.Location);

    private static readonly MetadataReference SystemRuntimeReference = MetadataReference.CreateFromFile(Path.Combine(AssemblyPath ?? string.Empty, path2: "System.Runtime.dll"));

    private static readonly MetadataReference SystemReference = MetadataReference.CreateFromFile(Path.Combine(AssemblyPath ?? string.Empty, path2: "System.dll"));
    private static readonly MetadataReference SystemConsoleReference = MetadataReference.CreateFromFile(typeof(Console).Assembly.Location);

    #region Get Diagnostics

    private static Task<IReadOnlyList<Diagnostic>> GetSortedDiagnosticsAsync(string[] sources, MetadataReference[] references, string language, DiagnosticAnalyzer analyzer)
    {
        return GetSortedDiagnosticsFromDocumentsAsync(analyzer: analyzer, GetDocuments(sources: sources, references: references, language: language));
    }

    protected static async Task<IReadOnlyList<Diagnostic>> GetSortedDiagnosticsFromDocumentsAsync(DiagnosticAnalyzer analyzer, IReadOnlyList<Document> documents)
    {
        HashSet<Project> projects = new();

        foreach (Document document in documents)
        {
            projects.Add(document.Project);
        }

        IReadOnlyList<Diagnostic> diagnostics = Array.Empty<Diagnostic>();

        foreach (Project project in projects)
        {
            Compilation? compilation = await project.GetCompilationAsync(CancellationToken.None);

            if (compilation is null)
            {
                continue;
            }

            IReadOnlyList<Diagnostic> additionalDiagnostics = await CollectMoreDiagnosticsAsync(analyzer: analyzer, documents: documents, compilation: compilation);

            if (additionalDiagnostics.Count != 0)
            {
                diagnostics = diagnostics.Concat(additionalDiagnostics)
                                         .ToArray();
            }
        }

        return SortDiagnostics(diagnostics);
    }

    private static async Task<IReadOnlyList<Diagnostic>> CollectMoreDiagnosticsAsync(DiagnosticAnalyzer analyzer, IReadOnlyList<Document> documents, Compilation compilation)
    {
        EnsureNoCompilationErrors(compilation);

        CompilationWithAnalyzers compilationWithAnalyzers = CompilationWithAnalyzers(analyzer: analyzer, compilation: compilation);
        IReadOnlyList<Diagnostic> additionalDiagnostics = await CollectDiagnosticsAsync(documents: documents, compilationWithAnalyzers: compilationWithAnalyzers);

        return additionalDiagnostics;
    }

    private static CompilationWithAnalyzers CompilationWithAnalyzers(DiagnosticAnalyzer analyzer, Compilation compilation)
    {
        return compilation.WithAnalyzers(IaHelper.For(analyzer), options: null);
    }

    private static async Task<IReadOnlyList<Diagnostic>> CollectDiagnosticsAsync(IReadOnlyList<Document> documents, CompilationWithAnalyzers compilationWithAnalyzers)
    {
        IReadOnlyList<Diagnostic> diags = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync(CancellationToken.None);

        return await ExtractDiagnosticsAsync(documents: documents, diags: diags);
    }

    private static async Task<IReadOnlyList<Diagnostic>> ExtractDiagnosticsAsync(IReadOnlyList<Document> documents, IReadOnlyList<Diagnostic> diags)
    {
        List<Diagnostic> diagnostics = new();

        foreach (Diagnostic diag in diags)
        {
            bool add = diag.Location == Location.None || diag.Location.IsInMetadata || await ShouldAddDocumentDiagnosticAsync(documents: documents, diag: diag);

            if (add)
            {
                diagnostics.Add(diag);
            }
        }

        return diagnostics;
    }

    private static async Task<bool> ShouldAddDocumentDiagnosticAsync(IReadOnlyList<Document> documents, Diagnostic diag)
    {
        foreach (Document document in documents)
        {
            bool add = await ShouldAddDiagnosticAsync(document: document, diag: diag);

            if (add)
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<bool> ShouldAddDiagnosticAsync(Document document, Diagnostic diag)
    {
        SyntaxTree? tree = await document.GetSyntaxTreeAsync(CancellationToken.None);
        bool add = tree is not null && tree == diag.Location.SourceTree;

        return add;
    }

    private static void EnsureNoCompilationErrors(Compilation compilation)
    {
        IReadOnlyList<Diagnostic> compilerErrors = compilation.GetDiagnostics(CancellationToken.None);

        if (compilerErrors.Count != 0)
        {
            StringBuilder errors = compilerErrors.Where(IsReportableCSharpError)
                                                 .Aggregate(new StringBuilder(), func: (current, compilerError) => current.Append(compilerError));

            if (errors.Length != 0)
            {
                throw new UnitTestSourceException("Please correct following compiler errors in your unit test source:" + errors);
            }
        }
    }

    private static bool IsReportableCSharpError(Diagnostic compilerError)
    {
        return !compilerError.ToString()
                             .Contains(value: "netstandard") && !compilerError.ToString()
                                                                              .Contains(value: "static 'Main' method") && !compilerError.ToString()
            .Contains(value: "CS1002") && !compilerError.ToString()
                                                        .Contains(value: "CS1702");
    }

    private static IReadOnlyList<Diagnostic> SortDiagnostics(IEnumerable<Diagnostic> diagnostics)
    {
        return diagnostics.OrderBy(keySelector: d => d.Location.SourceSpan.Start)
                          .ToArray();
    }

    #endregion

    #region Set up compilation and documents

    private static Document[] GetDocuments(string[] sources, MetadataReference[] references, string language)
    {
        if (!IsCSharp(language) && !IsVisualBasic(language))
        {
            throw new ArgumentException(message: "Unsupported Language", nameof(language));
        }

        Project project = CreateProject(sources: sources, references: references, language: language);
        Document[] documents = project.Documents.ToArray();

        if (sources.Length != documents.Length)
        {
            throw new InvalidOperationException(message: "Amount of sources did not match amount of Documents created");
        }

        return documents;
    }

    private static bool IsCSharp(string language)
    {
        return IsLanguage(namedLanguage: language, languageName: LanguageNames.CSharp);
    }

    private static bool IsVisualBasic(string language)
    {
        return IsLanguage(namedLanguage: language, languageName: LanguageNames.VisualBasic);
    }

    private static bool IsLanguage(string namedLanguage, string languageName)
    {
        return StringComparer.Ordinal.Equals(x: namedLanguage, y: languageName);
    }

    private static Project CreateProject(string[] sources, MetadataReference[] references, string language = LanguageNames.CSharp)
    {
        const string fileNamePrefix = DEFAULT_FILE_PATH_PREFIX;
        string fileExt = IsCSharp(language)
            ? C_SHARP_DEFAULT_FILE_EXT
            : VISUAL_BASIC_DEFAULT_EXT;

        ProjectId projectId = ProjectId.CreateNewId(TEST_PROJECT_NAME);

        Solution solution = references.Aggregate(BuildSolutionWithStandardReferences(language: language, projectId: projectId),
                                                 func: (current, reference) => current.AddMetadataReference(projectId: projectId, metadataReference: reference));

        int count = 0;

        foreach (string source in sources)
        {
            string newFileName = fileNamePrefix + count.ToString(CultureInfo.InvariantCulture) + "." + fileExt;
            DocumentId documentId = DocumentId.CreateNewId(projectId: projectId, debugName: newFileName);
            solution = solution.AddDocument(documentId: documentId, name: newFileName, SourceText.From(source));
            count++;
        }

        Project? proj = solution.GetProject(projectId);
        Assert.NotNull(proj);

        return proj;
    }

    [SuppressMessage(category: "codecracker.CSharp", checkId: "CC0022:DisposeObjectsBeforeLosingScope", Justification = "Test code")]
    [SuppressMessage(category: "Microsoft.Reliability", checkId: "CA2000:DisposeObjectsBeforeLosingScope", Justification = "Test code")]
    private static Solution BuildSolutionWithStandardReferences(string language, ProjectId projectId)
    {
        return new AdhocWorkspace().CurrentSolution.AddProject(projectId: projectId, name: TEST_PROJECT_NAME, assemblyName: TEST_PROJECT_NAME, language: language)
                                   .AddMetadataReference(projectId: projectId, metadataReference: CorlibReference)
                                   .AddMetadataReference(projectId: projectId, metadataReference: SystemCoreReference)
                                   .AddMetadataReference(projectId: projectId, metadataReference: CSharpSymbolsReference)
                                   .AddMetadataReference(projectId: projectId, metadataReference: CodeAnalysisReference)
                                   .AddMetadataReference(projectId: projectId, metadataReference: SystemRuntimeReference)
                                   .AddMetadataReference(projectId: projectId, metadataReference: SystemReference)
                                   .AddMetadataReference(projectId: projectId, metadataReference: SystemConsoleReference);
    }

    #endregion
}