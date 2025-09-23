using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GoLive.JsonPolymorphicGenerator;

public static class Scanner
{
	public static bool InheritsFrom(INamedTypeSymbol symbol, ITypeSymbol type)
	{
		INamedTypeSymbol baseType = symbol.BaseType;
		while (baseType is not null)
		{
			if (SymbolEqualityComparer.Default.Equals(type, baseType))
				return true;

			baseType = baseType.BaseType;
		}

		return false;
	}

	public static bool IsCandidate(SyntaxNode node)
	{
		return node is ClassDeclarationSyntax c
			&& c.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword))
			&& c.AttributeLists.Count > 0;
	}
}