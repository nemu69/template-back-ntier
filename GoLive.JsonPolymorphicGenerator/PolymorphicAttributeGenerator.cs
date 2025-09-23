using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Data.Eval;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GoLive.JsonPolymorphicGenerator;

[Generator]
public class PolymorphicAttributeGenerator : IIncrementalGenerator
{
	private readonly SymbolDisplayFormat _fullDisplayFormat = new(
		typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

	private readonly SymbolDisplayFormat _shortDisplayFormat = new(
		typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly);

	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		IncrementalValuesProvider<(ClassDeclarationSyntax cl, INamedTypeSymbol nts)> allParentPolyClasses = context
			.SyntaxProvider
			.CreateSyntaxProvider(
				predicate: static (s, _) => Scanner.IsCandidate(s),
				transform: static (ctx, _) => GetDeclarationsThatHasAttr(ctx)
				)
			.Where(static c => c is { cl: not null, nts: not null });

		IncrementalValuesProvider<INamedTypeSymbol> allClasses = context.SyntaxProvider.CreateSyntaxProvider(
			predicate: static (s, _) => s is ClassDeclarationSyntax c,
			transform: static (ctx, _) => GetDeclarations(ctx)
		)
			.Where(static c => c is not null);

		IncrementalValueProvider<(
			ImmutableArray<(ClassDeclarationSyntax cl, INamedTypeSymbol nts)> Left,
			ImmutableArray<INamedTypeSymbol> Right
		)> items = allParentPolyClasses.Collect().Combine(allClasses.Collect());

		IncrementalValueProvider<(
			AnalyzerConfigOptionsProvider Left,
			(
				ImmutableArray<(ClassDeclarationSyntax cl, INamedTypeSymbol nts)> Left,
				ImmutableArray<INamedTypeSymbol> Right
				) Right
			)> items2 = context.AnalyzerConfigOptionsProvider.Combine(items);

		context.RegisterSourceOutput(items2, (spc, source) => Execute2(source.Left, source.Right, spc));
	}

	private void Execute2(
		AnalyzerConfigOptionsProvider config,
		(
			ImmutableArray<(ClassDeclarationSyntax cl, INamedTypeSymbol nts)> polyClasses,
			ImmutableArray<INamedTypeSymbol> Right
		) allClasses,
		SourceProductionContext spc)
	{
		foreach ((ClassDeclarationSyntax cl, INamedTypeSymbol nts) in allClasses.polyClasses)
		{
			config.GetOptions(cl.SyntaxTree).TryGetValue("jsonpolymorphicgenerator.text_preappend", out string preAppend);
			config.GetOptions(cl.SyntaxTree).TryGetValue("jsonpolymorphicgenerator.text_postappend", out string postAppend);
			config.GetOptions(cl.SyntaxTree).TryGetValue("jsonpolymorphicgenerator.text_transform", out string codeExecution);

			SourceStringBuilder ssb = new();
			ssb.AppendLine("using System.Text.Json.Serialization;");
			ssb.AppendLine();
			IEnumerable<INamedTypeSymbol> res = allClasses.Right
				.Where(e => Scanner.InheritsFrom(e, nts))
				.GroupBy(p => p.ToDisplayString(_fullDisplayFormat))
				.Select(g => g.First());

			if (res.Any())
			{
				ssb.AppendLine($"namespace {nts.ContainingNamespace.ToDisplayString()}");
				ssb.AppendOpenCurlyBracketLine();

				foreach (INamedTypeSymbol symbol in res)
				{
					string className = symbol.ToDisplayString(_shortDisplayFormat);
					string formattedName = RemoveDtoPrefix(className);

					if (!string.IsNullOrWhiteSpace(codeExecution))
					{
						try
						{
							Evaluator eval = new(codeExecution);
							eval["classname"] = className;
							eval["namespacename"] = symbol.ContainingNamespace.ToDisplayString();
							string outp = eval.Eval<string>();
							string derivedType = symbol.ToDisplayString(_fullDisplayFormat);
							string derivedTypeAttribute =
								$"[JsonDerivedType(typeof({derivedType}), \"{preAppend}{formattedName}{postAppend}\")]";
							ssb.AppendLine(derivedTypeAttribute);
						}
						catch (Exception)
						{
							string derivedType = symbol.ToDisplayString(_fullDisplayFormat);
							string derivedTypeAttribute =
								$"[JsonDerivedType(typeof({derivedType}), \"{preAppend}{formattedName}{postAppend}\")]";
							ssb.AppendLine(derivedTypeAttribute);
						}
					}
					else
					{
						string derivedType = symbol.ToDisplayString(_fullDisplayFormat);
						string derivedTypeAttribute =
							$"[JsonDerivedType(typeof({derivedType}), \"{preAppend}{formattedName}{postAppend}\")]";
						ssb.AppendLine(derivedTypeAttribute);
					}
				}

				ssb.AppendLine($"public partial class {nts.Name}");
				ssb.AppendOpenCurlyBracketLine();
				ssb.AppendCloseCurlyBracketLine();
				ssb.AppendCloseCurlyBracketLine();
				ssb.AppendLine();
				ssb.AppendLine();

				spc.AddSource($"{nts.Name}.g.cs", ssb.ToString());
			}
		}
	}

	private static string RemoveDtoPrefix(string className)
		=> (className.StartsWith("DTO")) ? className.Substring(3) : className;

	private static INamedTypeSymbol GetDeclarations(GeneratorSyntaxContext context)
	{
		ClassDeclarationSyntax classDeclarationSyntax = (ClassDeclarationSyntax)context.Node;
		return context.SemanticModel.GetDeclaredSymbol(classDeclarationSyntax);
	}

	private static (ClassDeclarationSyntax cl, INamedTypeSymbol nts) GetDeclarationsThatHasAttr(
		GeneratorSyntaxContext context)
	{
		ClassDeclarationSyntax classDeclarationSyntax = (ClassDeclarationSyntax)context.Node;
		foreach (AttributeListSyntax attributeListSyntax in classDeclarationSyntax.AttributeLists)
		{
			foreach (AttributeSyntax attributeSyntax in attributeListSyntax.Attributes)
			{
				if (ModelExtensions.GetSymbolInfo(context.SemanticModel, attributeSyntax)
					.Symbol is not IMethodSymbol attributeSymbol)
				{
					// weird, we couldn't get the symbol, ignore it
					continue;
				}

				INamedTypeSymbol attributeContainingTypeSymbol = attributeSymbol.ContainingType;
				string fullName = attributeContainingTypeSymbol.ToDisplayString();

				// Is the attribute the [JsonPolymorphic] attribute?
				if (fullName == "System.Text.Json.Serialization.JsonPolymorphicAttribute")
				{
					// return the parent class of the method
					return (classDeclarationSyntax, context.SemanticModel.GetDeclaredSymbol(classDeclarationSyntax));
				}
			}
		}

		return (null, null);
	}
}