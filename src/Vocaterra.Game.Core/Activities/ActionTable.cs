using System.Text.Json;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.Activities;

/// <summary>One row of the action table: the verbs a learner may use to act with the noun <paramref name="Use"/> on the fixture <paramref name="On"/>.</summary>
/// <param name="Use">The noun the learner uses (an item held, a hand, a tool).</param>
/// <param name="On">The station fixture the act is done on.</param>
/// <param name="Verbs">The verb lemmas, in the file's order; two only when they are different acts with different results.</param>
public sealed record ActionRule(string Use, string On, IReadOnlyList<string> Verbs);

/// <summary>
/// The actions of the stations (W05, design 10.7; <c>game/actions.json</c>, schema 1): for each pair of a use and an on, the verbs a step may name. A step's
/// verb must be listed for its pair (<see cref="ProcedureStep.ActUse"/>, <see cref="ProcedureStep.ActOn"/>); the validator checks that and the verbs' lexicon forms.
/// </summary>
public sealed class ActionTable
{
    /// <summary>The file's path inside the bundle folder.</summary>
    public const string RelativePath = "game/actions.json";

    /// <summary>The schema version of the file.</summary>
    public const int SchemaVersion = 1;

    private readonly Dictionary<(string Use, string On), IReadOnlyList<string>> _verbs;

    private ActionTable(IReadOnlyList<ActionRule> rules)
    {
        Rules = rules;
        _verbs = [];
        foreach (var rule in rules)
        {
            _verbs[(rule.Use, rule.On)] = rule.Verbs;
        }
    }

    /// <summary>The rows, in the file's order.</summary>
    public IReadOnlyList<ActionRule> Rules { get; }

    /// <summary>The table of a bundle with no action.</summary>
    public static ActionTable Empty { get; } = new([]);

    /// <summary>Every verb the table names, once each, in the file's order.</summary>
    public IReadOnlyList<string> Verbs => Rules.SelectMany(static r => r.Verbs).Distinct(StringComparer.Ordinal).ToList();

    /// <summary>The verbs a learner may use with the noun on the fixture; empty when the pair has no action.</summary>
    /// <param name="use">The noun the learner uses.</param>
    /// <param name="on">The fixture.</param>
    public IReadOnlyList<string> VerbsFor(string use, string on)
    {
        ArgumentNullException.ThrowIfNull(use);
        ArgumentNullException.ThrowIfNull(on);
        return _verbs.TryGetValue((use, on), out var verbs) ? verbs : [];
    }

    /// <summary>
    /// The same table with every use and every fixture read through the noun catalogue (T03): the file names them by neutral id (<c>flour</c> on <c>bowl</c>), the learner acts
    /// with the target language's words (<see cref="NounCatalog.LemmaOfId"/>). The verbs are the language's and stay. An id the catalogue does not have stays, for the validator.
    /// </summary>
    /// <param name="nouns">The noun catalogue.</param>
    public ActionTable InLanguage(NounCatalog nouns)
    {
        ArgumentNullException.ThrowIfNull(nouns);
        return new ActionTable(Rules.Select(rule => rule with { Use = nouns.LemmaOfId(rule.Use), On = nouns.LemmaOfId(rule.On) }).ToList());
    }

    /// <summary>Reads the table from the bundle's verified host file.</summary>
    /// <param name="bundle">The bundle, loaded with hash verification.</param>
    /// <exception cref="InvalidDataException">The file is missing from the bundle or malformed.</exception>
    public static ActionTable Load(ContentBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        return Parse(GameData.HostText(bundle, RelativePath));
    }

    /// <summary>Builds the table from the file's text.</summary>
    /// <param name="json">The file's text.</param>
    /// <exception cref="InvalidDataException">The text is malformed, a pair is listed twice, or a verb is listed twice in one pair.</exception>
    public static ActionTable Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.GetProperty("schemaVersion").GetInt32() != SchemaVersion)
            {
                throw new InvalidDataException($"{RelativePath} must have schemaVersion {SchemaVersion}.");
            }

            var rules = new List<ActionRule>();
            var pairs = new HashSet<(string Use, string On)>();
            foreach (var element in root.GetProperty("actions").EnumerateArray())
            {
                var use = element.GetProperty("use").GetString()!;
                var on = element.GetProperty("on").GetString()!;
                if (!pairs.Add((use, on)))
                {
                    throw new InvalidDataException($"{RelativePath}: the pair '{use}' on '{on}' is listed twice.");
                }

                var verbs = element.GetProperty("verbs").EnumerateArray().Select(static v => v.GetString()!).ToList();
                if (verbs.Distinct(StringComparer.Ordinal).Count() != verbs.Count)
                {
                    throw new InvalidDataException($"{RelativePath}: the pair '{use}' on '{on}' lists a verb twice.");
                }

                rules.Add(new ActionRule(use, on, verbs));
            }

            return new ActionTable(rules);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidDataException($"{RelativePath} is malformed: {ex.Message}", ex);
        }
    }
}
