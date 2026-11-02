using System.Globalization;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Es.Text;
using Vocaterra.Nlp.Pipeline;

namespace Vocaterra.Host;

/// <summary>Writes one utterance's outcome as text: the verdict summary, the agent's lines, the effects and the repair step.</summary>
public static class VerdictReport
{

    /// <summary>
    /// Writes the prompt the text loop shows before each turn: its id, mode, agent and intents, then the agent's line
    /// (<see cref="PromptSpec.Text"/>) when the prompt has one. A prompt without text, or with only white space as its text, gets the header
    /// line alone.
    /// </summary>
    public static void WritePrompt(TextWriter output, PromptSpec prompt)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(prompt);
        output.WriteLine(
            $"[{prompt.PromptId}] {prompt.Mode}, agent {prompt.AgentId ?? ConversationSession.NoAgentId}, "
            + $"intents {string.Join(", ", prompt.AllowedIntentIds)}");
        if (!string.IsNullOrWhiteSpace(prompt.Text))
        {
            output.WriteLine($"  agent: {prompt.Text}");
        }
    }

    /// <summary>
    /// Writes the outcome of evaluating <paramref name="transcript"/> on <paramref name="promptId"/>. With the NPCs' names, a correction line spells them as the game
    /// shows it (<see cref="RecastNames"/>, V20: <c>«buscaré madera para Luz»</c>).
    /// </summary>
    public static void Write(TextWriter output, string promptId, string transcript, PipelineResult result, ExchangeAnswer? exchange = null, IEnumerable<string>? names = null)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(result);
        var verdict = result.Verdict;
        output.WriteLine($"{promptId}: \"{transcript}\"");
        output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"  intent={verdict.Intent.Value} communicated={Lower(verdict.Communicated)} correctness={Format(verdict.Correctness)}"));
        foreach (var error in verdict.Errors)
        {
            output.WriteLine($"  error {error.Tag}: expected '{error.Expected}', heard '{error.Actual}'");
        }

        foreach (var effect in result.Effects)
        {
            output.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  effect {effect.EffectId} {effect.Kind} {Format(effect.Value)}"));
        }

        if (exchange is null)
        {
            foreach (var line in result.Response.Lines)
            {
                var correction = line.IsRecast || line.LineId.StartsWith(NpcTurnRules.FeedbackLinePrefix, StringComparison.Ordinal);
                output.WriteLine($"  agent: {(correction && names is not null ? RecastNames.Capitalise(line.Text, names) : line.Text)}");
            }
        }
        else
        {
            // The game's reply (NpcTurnRules, V21b): the NPC's lines, the one correction, then the NPC's answers to what the world could not do.
            foreach (var text in exchange.Replies)
            {
                output.WriteLine($"  agent: {text}");
            }

            if (exchange.Feedback is { } feedback)
            {
                output.WriteLine($"  agent: {feedback}");
            }

            foreach (var text in exchange.Lines)
            {
                output.WriteLine($"  agent: {text}");
            }
        }

        foreach (var note in result.Notes ?? [])
        {
            output.WriteLine($"  note: {SpanishAccentNotes.Describe(note)}"); // M70: the accent the learner left out, accepted with no deduction
        }

        if (exchange?.OffPromptIntent is { } offPrompt)
        {
            output.WriteLine($"  off-prompt: {offPrompt}"); // V27: the intent the core found for a line the prompt does not take (a diagnostic, like repair:)
        }

        if (exchange?.OffPromptIntent is null && result.Repair is { } repair)
        {
            output.WriteLine($"  repair: {repair.Kind} {repair.AgentLine}");
        }
    }

    private static string Format(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string Lower(bool value)
    {
        return value ? "true" : "false";
    }
}
