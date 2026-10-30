using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.Formatting;

// Uses Roslyn bundled with the .NET SDK. No downloaded formatter or NuGet dependency.
if (args.Length < 2 || (args[0] != "--check" && args[0] != "--write"))
{
    Console.Error.WriteLine("Usage: SourceFormatter --check|--write <repository> [receipt.json]");
    return 2;
}

string repository = Path.GetFullPath(args[1]);
using var workspace = new AdhocWorkspace();
var options = workspace.Options
    .WithChangedOption(FormattingOptions.UseTabs, LanguageNames.CSharp, false)
    .WithChangedOption(FormattingOptions.IndentationSize, LanguageNames.CSharp, 4)
    .WithChangedOption(FormattingOptions.NewLine, LanguageNames.CSharp, "\n")
    .WithChangedOption(CSharpFormattingOptions.WrappingPreserveSingleLine, false)
    .WithChangedOption(CSharpFormattingOptions.WrappingKeepStatementsOnSingleLine, false);
var records = new List<object>();
int changed = 0;

foreach (string directory in new[] { "src", "updater" })
{
    foreach (string file in Directory.EnumerateFiles(Path.Combine(repository, directory), "*.cs").Order())
    {
        string before = File.ReadAllText(file);
        var original = CSharpSyntaxTree.ParseText(before).GetRoot();
        var formatted = Formatter.Format(original.NormalizeWhitespace(indentation: "    ", eol: "\n"), workspace, options);
        string after = formatted.ToFullString().TrimEnd() + "\n";
        var reparsed = CSharpSyntaxTree.ParseText(after).GetRoot();
        string[] Tokens(SyntaxNode node) => node.DescendantTokens(descendIntoTrivia: true)
            .Select(token => token.RawKind + ":" + token.Text).ToArray();

        // Refuse edits that change code, strings, directives, or documentation tokens.
        string[] Shape(SyntaxNode node) => node.DescendantNodesAndSelf()
            .Select(child => child.RawKind + ":" + child.ChildNodesAndTokens().Count).ToArray();
        string[] InactiveCode(SyntaxNode node) => node.DescendantTrivia(descendIntoTrivia: true)
            .Where(trivia => trivia.IsKind(SyntaxKind.DisabledTextTrivia))
            .Select(trivia => trivia.ToFullString()).ToArray();
        if (!Tokens(original).SequenceEqual(Tokens(reparsed)) || !Shape(original).SequenceEqual(Shape(reparsed))
            || !InactiveCode(original).SequenceEqual(InactiveCode(reparsed)))
            throw new InvalidDataException("Formatting changed syntax: " + file);

        bool differs = before != after;
        if (differs) changed++;
        if (differs && args[0] == "--write")
            File.WriteAllText(file, after, new UTF8Encoding(false));

        string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        records.Add(new { File = Path.GetRelativePath(repository, file), Changed = differs,
            BeforeSha256 = Hash(before), AfterSha256 = Hash(after), SyntaxTokensUnchanged = true });
    }
}

if (args.Length > 2)
{
    string receipt = Path.GetFullPath(args[2]);
    Directory.CreateDirectory(Path.GetDirectoryName(receipt)!);
    File.WriteAllText(receipt, JsonSerializer.Serialize(new { Mode = args[0], ChangedFiles = changed,
        SyntaxTokensUnchanged = true, Files = records }, new JsonSerializerOptions { WriteIndented = true }));
}
Console.WriteLine($"{records.Count} source files checked; {changed} require formatting; syntax tokens unchanged.");
return args[0] == "--check" && changed > 0 ? 1 : 0;



