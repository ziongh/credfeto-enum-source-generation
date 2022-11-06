using System.Collections.Immutable;
using System.Linq;
using Credfeto.Enumeration.Source.Generation.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Credfeto.Enumeration.Source.Generation;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ProhibitEnumToStringsDiagnosticsAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = RuleHelpers.CreateRule(code: "ENUM001",
                                                                               category: "Do not use ToString() on an enum use EnumHelpers.GetName(this Enum value) instead",
                                                                               title: "Do not use ToString() on an enum use EnumHelpers.GetName(this Enum value) instead",
                                                                               message: "Do not use ToString() on an enum use EnumHelpers.GetName(this Enum value) instead");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        new[]
        {
            Rule
        }.ToImmutableArray();

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(PerformCheck);
    }

    private static void PerformCheck(CompilationStartAnalysisContext compilationStartContext)
    {
        compilationStartContext.RegisterSyntaxNodeAction(action: LookForBannedMethods,
                                                         SyntaxKind.PointerMemberAccessExpression,
                                                         SyntaxKind.SimpleMemberAccessExpression,
                                                         SyntaxKind.InterpolatedStringExpression);
    }

    private static void LookForBannedMethods(SyntaxNodeAnalysisContext syntaxNodeAnalysisContext)
    {
        LookForExplicitBannedMethods(syntaxNodeAnalysisContext);
        LookForImplicitBannedMethods(syntaxNodeAnalysisContext);
    }

    private static void LookForExplicitBannedMethods(SyntaxNodeAnalysisContext syntaxNodeAnalysisContext)
    {
        if (syntaxNodeAnalysisContext.Node is MemberAccessExpressionSyntax memberAccessExpressionSyntax)
        {
            LookForBannedMethod(memberAccessExpressionSyntax: memberAccessExpressionSyntax, syntaxNodeAnalysisContext: syntaxNodeAnalysisContext);
        }
    }

    private static void LookForBannedMethod(MemberAccessExpressionSyntax memberAccessExpressionSyntax, SyntaxNodeAnalysisContext syntaxNodeAnalysisContext)
    {
        INamedTypeSymbol? typeInfo = ExtractExpressionSyntax(invocation: memberAccessExpressionSyntax, syntaxNodeAnalysisContext: syntaxNodeAnalysisContext);

        if (typeInfo == null)
        {
            return;
        }

        if (typeInfo.EnumUnderlyingType == null)
        {
            // not an enum
            return;
        }

        if (memberAccessExpressionSyntax.Name.Identifier.ToString() == nameof(ToString))
        {
            syntaxNodeAnalysisContext.ReportDiagnostic(Diagnostic.Create(descriptor: Rule, memberAccessExpressionSyntax.GetLocation()));
        }
    }

    private static INamedTypeSymbol? ExtractExpressionSyntax(MemberAccessExpressionSyntax invocation, SyntaxNodeAnalysisContext syntaxNodeAnalysisContext)
    {
        ExpressionSyntax e;

        if (invocation.Expression is MemberAccessExpressionSyntax syntax)
        {
            e = syntax;
        }
        else if (invocation.Expression is IdentifierNameSyntax expression)
        {
            e = expression;
        }
        else
        {
            return null;
        }

        INamedTypeSymbol? typeInfo = syntaxNodeAnalysisContext.SemanticModel.GetTypeInfo(e)
                                                              .Type as INamedTypeSymbol;

        if (typeInfo?.ConstructedFrom == null)
        {
            return null;
        }

        return typeInfo;
    }

    private static void LookForImplicitBannedMethods(SyntaxNodeAnalysisContext syntaxNodeAnalysisContext)
    {
        if (syntaxNodeAnalysisContext.Node is InterpolatedStringExpressionSyntax interpolatedStringExpressionSyntax)
        {
            LookForImplicitBannedMethods(interpolatedStringExpressionSyntax: interpolatedStringExpressionSyntax, syntaxNodeAnalysisContext: syntaxNodeAnalysisContext);
        }
    }

    private static void LookForImplicitBannedMethods(InterpolatedStringExpressionSyntax interpolatedStringExpressionSyntax, SyntaxNodeAnalysisContext syntaxNodeAnalysisContext)
    {
        foreach (InterpolationSyntax part in interpolatedStringExpressionSyntax.Contents.OfType<InterpolationSyntax>())
        {
            if (syntaxNodeAnalysisContext.SemanticModel.GetTypeInfo(part.Expression)
                                         .Type is not INamedTypeSymbol typeInfo)
            {
                return;
            }

            if (typeInfo.EnumUnderlyingType == null)
            {
                // not an enum
                return;
            }

            //if (interpolatedStringExpressionSyntax.Name.Identifier.ToString() == nameof(ToString))
            syntaxNodeAnalysisContext.ReportDiagnostic(Diagnostic.Create(descriptor: Rule, interpolatedStringExpressionSyntax.GetLocation()));
        }
    }
}