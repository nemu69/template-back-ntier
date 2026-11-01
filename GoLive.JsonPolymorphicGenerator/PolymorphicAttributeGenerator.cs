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
		// Build the complete hierarchy map
		var hierarchyMap = BuildHierarchyMap(allClasses.polyClasses, allClasses.Right);

		// Generate for each class that has descendants
		foreach (var kvp in hierarchyMap)
		{
			INamedTypeSymbol parentType = kvp.Key;
			List<INamedTypeSymbol> allDescendants = kvp.Value;

			if (!allDescendants.Any())
				continue;

			// Find the corresponding ClassDeclarationSyntax for the config
			var (cl, nts) = allClasses.polyClasses
				.FirstOrDefault(p => SymbolEqualityComparer.Default.Equals(p.nts, parentType));

			ClassDeclarationSyntax configSource = cl
				?? allClasses.Right
					.Select(s => s.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax())
					.OfType<ClassDeclarationSyntax>()
					.FirstOrDefault(c => c != null);

			if (configSource == null)
				continue;

			config.GetOptions(configSource.SyntaxTree).TryGetValue("jsonpolymorphicgenerator.text_preappend", out string preAppend);
			config.GetOptions(configSource.SyntaxTree).TryGetValue("jsonpolymorphicgenerator.text_postappend", out string postAppend);
			config.GetOptions(configSource.SyntaxTree).TryGetValue("jsonpolymorphicgenerator.text_transform", out string codeExecution);

			GenerateSourceForType(parentType, allDescendants, preAppend, postAppend, codeExecution, spc);
		}
	}

	/// <summary>
	/// Build a map: Parent Class → All its descendants (direct and indirect)
	/// </summary>
	/// <param name="rootClasses">All classes with [JsonPolymorphic]</param>
	/// <param name="allClasses">All classes in the compilation</param>
	/// <returns>A dictionary where keys are parent classes and values are lists of their descendants</returns>
	private Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>> BuildHierarchyMap(
		ImmutableArray<(ClassDeclarationSyntax cl, INamedTypeSymbol nts)> rootClasses,
		ImmutableArray<INamedTypeSymbol> allClasses)
	{
		var map = new Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>(SymbolEqualityComparer.Default);

		// 1. Identify all root classes (with [JsonPolymorphic])
		var roots = new HashSet<INamedTypeSymbol>(
			rootClasses.Select(r => r.nts),
			SymbolEqualityComparer.Default
		);

		// 2. For each root class, find ALL its descendants
		foreach (var root in roots)
		{
			var allDescendants = allClasses
				.Where(c => !c.IsAbstract && Scanner.InheritsFrom(c, root))
				.GroupBy(p => p.ToDisplayString(_fullDisplayFormat))
				.Select(g => g.First())
				.OrderBy(s => GetInheritanceDepth(s, root))
				.ToList();

			map[root] = allDescendants;
		}

		// 3. Identify intermediate classes that also have children
		var allDescendantsFlat = map.Values.SelectMany(v => v).ToList();

		foreach (var potentialParent in allDescendantsFlat)
		{
			// Find DIRECT children of this class
			var directChildren = allClasses
				.Where(c => !c.IsAbstract && IsDirectChild(c, potentialParent))
				.GroupBy(p => p.ToDisplayString(_fullDisplayFormat))
				.Select(g => g.First())
				.ToList();

			if (directChildren.Any())
			{
				// This intermediate class has children, so we generate for it as well
				// But we include ALL its descendants (not just direct ones)
				var allChildDescendants = allClasses
					.Where(c => !c.IsAbstract && Scanner.InheritsFrom(c, potentialParent))
					.GroupBy(p => p.ToDisplayString(_fullDisplayFormat))
					.Select(g => g.First())
					.OrderBy(s => GetInheritanceDepth(s, potentialParent))
					.ToList();

				map[potentialParent] = allChildDescendants;
			}
		}

		return map;
	}

	/// <summary>
	/// Generates the source file for a given class
	/// </summary>
	/// <param name="parentType">The class for which we are generating the source</param>
	/// <param name="descendants">All descendants of this class (direct and indirect)</param>
	/// <param name="preAppend">Text to prepend to the derived type name</param>
	/// <param name="postAppend">Text to append to the derived type name</param>
	/// <param name="codeExecution">Code to execute for custom name transformation</param>
	/// <param name="spc">The source production context</param>
	private void GenerateSourceForType(
		INamedTypeSymbol parentType,
		List<INamedTypeSymbol> descendants,
		string preAppend,
		string postAppend,
		string codeExecution,
		SourceProductionContext spc)
	{
		SourceStringBuilder ssb = new();
		ssb.AppendLine("using System.Text.Json.Serialization;");
		ssb.AppendLine();

		ssb.AppendLine($"namespace {parentType.ContainingNamespace.ToDisplayString()}");
		ssb.AppendOpenCurlyBracketLine();

		foreach (INamedTypeSymbol symbol in descendants)
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
					string derivedTypeAttribute
						= $"[JsonDerivedType(typeof({derivedType}), \"{preAppend}{outp}{postAppend}\")]";
					ssb.AppendLine(derivedTypeAttribute);
				}
				catch (Exception)
				{
					string derivedType = symbol.ToDisplayString(_fullDisplayFormat);
					string derivedTypeAttribute
						= $"[JsonDerivedType(typeof({derivedType}), \"{preAppend}{formattedName}{postAppend}\")]";
					ssb.AppendLine(derivedTypeAttribute);
				}
			}
			else
			{
				string derivedType = symbol.ToDisplayString(_fullDisplayFormat);
				string derivedTypeAttribute
					= $"[JsonDerivedType(typeof({derivedType}), \"{preAppend}{formattedName}{postAppend}\")]";
				ssb.AppendLine(derivedTypeAttribute);
			}
		}

		ssb.AppendLine($"public partial class {parentType.Name}");
		ssb.AppendOpenCurlyBracketLine();
		ssb.AppendCloseCurlyBracketLine();
		ssb.AppendCloseCurlyBracketLine();
		ssb.AppendLine();

		spc.AddSource($"{parentType.Name}.g.cs", ssb.ToString());
	}

	/// <summary>
	/// Checks if a type is a DIRECT child of another (i.e., it inherits directly without intermediate classes)
	/// </summary>
	/// <param name="candidate">The type to check</param>
	/// <param name="parent">The potential parent type</param>
	/// <returns>True if candidate is a direct child of parent, false otherwise</returns>
	private static bool IsDirectChild(INamedTypeSymbol candidate, INamedTypeSymbol parent)
	{
		return candidate.BaseType != null &&
			   SymbolEqualityComparer.Default.Equals(candidate.BaseType, parent);
	}

	/// <summary>
	/// Calculates the inheritance depth
	/// </summary>
	/// <param name="type">The type whose depth we want to calculate</param>
	/// <param name="ancestor">The ancestor type we are measuring against</param>
	/// <returns>The number of inheritance levels between type and ancestor (0 if direct child, 1 if one level in between, etc.)</returns>
	private static int GetInheritanceDepth(INamedTypeSymbol type, INamedTypeSymbol ancestor)
	{
		int depth = 0;
		INamedTypeSymbol current = type.BaseType;

		while (current != null)
		{
			depth++;
			if (SymbolEqualityComparer.Default.Equals(current, ancestor))
			{
				return depth;
			}
			current = current.BaseType;
		}

		return int.MaxValue;
	}

	/// <summary>
	/// Removes the "DTO" prefix from a class name if it exists
	/// </summary>
	/// <param name="className">The class name to process</param>
	/// <returns>The class name without the "DTO" prefix</returns>
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
					continue;
				}

				INamedTypeSymbol attributeContainingTypeSymbol = attributeSymbol.ContainingType;
				string fullName = attributeContainingTypeSymbol.ToDisplayString();

				if (fullName == "System.Text.Json.Serialization.JsonPolymorphicAttribute")
				{
					return (classDeclarationSyntax, context.SemanticModel.GetDeclaredSymbol(classDeclarationSyntax));
				}
			}
		}

		return (null, null);
	}
}