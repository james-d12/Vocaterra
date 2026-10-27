using System.Reflection;
using System.Text.RegularExpressions;

namespace Vocaterra.Nlp.Fuzzing.Tests.Languages.Xx;

/// <summary>
/// The toy pack uses no type of the Spanish pack (M53). This project references <c>Vocaterra.Nlp.Es</c> for its Spanish fuzz tests, so the compiler does not
/// stop an xx file from using it; these two tests do. The source scan reads every file of the xx folder, and the reflection scan reads every type, base type,
/// interface, field, property, method, parameter and return type of the xx types.
/// </summary>
public sealed class XxIndependenceTests
{
    private const string SpanishAssembly = "Vocaterra.Nlp.Es";

    // The name is built, so this file does not match its own scan.
    private static readonly Regex SpanishType = new(
        @"Vocaterra\.Nlp\." + "Es" + @"\b|\bSpanish(LanguagePack|Lexicon|Content|TextNormalizer|Numbers|PhoneticKey|G2p|RepairLines|AnalysisRules|AgreementChecker|TemplateFunctions|RecastPolicy)\b|\bMorphologyGenerator\b|\bPunctuationFixer\b",
        RegexOptions.CultureInvariant);

    [Fact]
    public void NoXxSourceFileNamesATypeOfTheSpanishPack()
    {
        var folder = XxFolder();
        var files = Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories)
            .Where(static file => !file.EndsWith("XxIndependenceTests.cs", StringComparison.Ordinal))
            .ToList();
        Assert.NotEmpty(files);

        var offenders = new List<string>();
        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                // Comments may say "Spanish"; code may not name a Spanish pack type.
                var code = lines[i].TrimStart();
                if (code.StartsWith("//", StringComparison.Ordinal) || code.StartsWith("///", StringComparison.Ordinal) || code.StartsWith('*'))
                {
                    continue;
                }

                if (SpanishType.IsMatch(lines[i]))
                {
                    offenders.Add($"{Path.GetFileName(file)}({i + 1}): {lines[i].Trim()}");
                }
            }
        }

        Assert.True(offenders.Count == 0, "The toy pack must not use the Spanish pack:" + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void NoXxTypeHasAMemberSignatureFromTheSpanishPackAssembly()
    {
        var xxTypes = typeof(XxIndependenceTests).Assembly.GetTypes()
            .Where(static type => type.Namespace is { } ns && ns.StartsWith("Vocaterra.Nlp.Fuzzing.Tests.Languages.Xx", StringComparison.Ordinal)
                && !type.Name.StartsWith("XxIndependence", StringComparison.Ordinal))
            .ToList();
        Assert.Contains(typeof(XxLanguagePack), xxTypes);

        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var offenders = new List<string>();
        foreach (var type in xxTypes)
        {
            var used = new List<Type?> { type.BaseType };
            used.AddRange(type.GetInterfaces());
            foreach (var field in type.GetFields(all))
            {
                used.Add(field.FieldType);
            }

            foreach (var property in type.GetProperties(all))
            {
                used.Add(property.PropertyType);
            }

            foreach (var method in type.GetMethods(all).Cast<MethodBase>().Concat(type.GetConstructors(all)))
            {
                used.AddRange(method.GetParameters().Select(static parameter => parameter.ParameterType));
                if (method is MethodInfo info)
                {
                    used.Add(info.ReturnType);
                }
            }

            foreach (var candidate in used)
            {
                foreach (var leaf in Leaves(candidate))
                {
                    if (string.Equals(leaf.Assembly.GetName().Name, SpanishAssembly, StringComparison.Ordinal))
                    {
                        offenders.Add($"{type.Name} uses {leaf.FullName}");
                    }
                }
            }
        }

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    // The type and, for arrays, by-ref and generic types, the types it is built from.
    private static IEnumerable<Type> Leaves(Type? type)
    {
        if (type is null)
        {
            yield break;
        }

        yield return type;
        if (type.HasElementType)
        {
            foreach (var inner in Leaves(type.GetElementType()))
            {
                yield return inner;
            }
        }

        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
            {
                foreach (var inner in Leaves(argument))
                {
                    yield return inner;
                }
            }
        }
    }

    private static string XxFolder()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "tests", "Vocaterra.Nlp.Fuzzing.Tests", "Languages", "Xx");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("Could not find tests/Vocaterra.Nlp.Fuzzing.Tests/Languages/Xx above the test binaries.");
    }
}
