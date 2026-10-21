using System.Collections.Immutable;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Nlp.Decision.Learning;

/// <summary>Immutable per-skill BKT state; every update returns a new model and skills stay ordinal-sorted by id.</summary>
public sealed record LearnerModel(BktParameters Parameters, IReadOnlyList<SkillMastery> Skills)
{
    /// <summary>The BKT parameters used for every update.</summary>
    public BktParameters Parameters { get; init; } = Parameters ?? throw new ArgumentNullException(nameof(Parameters));

    /// <summary>The tracked skills, normalized to an ordinal-sorted immutable list; duplicate ids are rejected.</summary>
    public IReadOnlyList<SkillMastery> Skills { get; init; } = Normalize(Skills);

    /// <summary>A model with no evidence yet, using <see cref="BktParameters.Default"/> unless parameters are given.</summary>
    public static LearnerModel New(BktParameters? parameters = null)
    {
        return new LearnerModel(parameters ?? BktParameters.Default, []);
    }

    /// <summary>Returns the tracked mastery of the skill, or null when it has never been exercised.</summary>
    public SkillMastery? Find(string skillId)
    {
        ArgumentNullException.ThrowIfNull(skillId);
        var low = 0;
        var high = Skills.Count - 1;
        while (low <= high)
        {
            var mid = low + ((high - low) / 2);
            var order = string.CompareOrdinal(Skills[mid].SkillId, skillId);
            if (order == 0)
            {
                return Skills[mid];
            }

            if (order < 0)
            {
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return null;
    }

    /// <summary>The probability the skill is known; the prior for a skill that has never been exercised.</summary>
    public double PKnown(string skillId)
    {
        return Find(skillId)?.PKnown ?? Parameters.PriorKnown;
    }

    /// <summary>
    /// Applies one BKT step per skill. The skills updated are the distinct, ordinal-sorted union of
    /// <paramref name="exercisedSkills"/> and the families (<see cref="ErrorTags.Family(string)"/>) of the verdict's errors.
    /// A skill counts as incorrect when some error in the verdict has that family, otherwise as correct.
    /// </summary>
    public LearnerModel Update(Verdict verdict, IReadOnlyList<string> exercisedSkills)
    {
        ArgumentNullException.ThrowIfNull(verdict);
        ArgumentNullException.ThrowIfNull(exercisedSkills);

        var errorFamilies = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var error in verdict.Errors)
        {
            errorFamilies.Add(ErrorTags.Family(error.Tag));
        }

        var touched = new SortedSet<string>(errorFamilies, StringComparer.Ordinal);
        foreach (var skill in exercisedSkills)
        {
            ArgumentNullException.ThrowIfNull(skill, nameof(exercisedSkills));
            touched.Add(skill);
        }

        return Apply(touched, errorFamilies);
    }

    /// <summary>
    /// Applies one BKT step to each of the named skills and nothing else: no verdict, so no error families are involved. Use it for a host that
    /// exercises skills without an utterance; for an utterance, pass the skills to <see cref="Update"/> instead so its error families are
    /// stepped once together with them. Ids are validated with <see cref="ValidateSkillId"/>; duplicates step once.
    /// </summary>
    /// <param name="skills">The skills exercised.</param>
    /// <param name="correct">True when the learner used them correctly, false when they got them wrong.</param>
    public LearnerModel StepSkills(IReadOnlyList<string> skills, bool correct)
    {
        ArgumentNullException.ThrowIfNull(skills);
        var touched = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var skill in skills)
        {
            touched.Add(ValidateSkillId(skill, nameof(skills)));
        }

        return Apply(touched, correct ? new SortedSet<string>(StringComparer.Ordinal) : touched);
    }

    /// <summary>
    /// Checks a host-supplied skill id: it must not be empty and must contain no whitespace (so it stays one token in logs and keys).
    /// Anything else is allowed (<c>word.árbol</c>, <c>intent.greet</c>); ids are compared ordinally.
    /// </summary>
    /// <returns>The id, unchanged.</returns>
    /// <exception cref="ArgumentException">The id is empty or contains whitespace.</exception>
    public static string ValidateSkillId(string skillId, string? paramName = null)
    {
        ArgumentNullException.ThrowIfNull(skillId, paramName);
        if (skillId.Length == 0 || skillId.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException($"Skill id '{skillId}' must be non-empty and contain no whitespace.", paramName);
        }

        return skillId;
    }

    private LearnerModel Apply(SortedSet<string> touched, SortedSet<string> incorrectSkills)
    {
        if (touched.Count == 0)
        {
            return this;
        }

        var updated = new SortedDictionary<string, SkillMastery>(StringComparer.Ordinal);
        foreach (var existing in Skills)
        {
            updated[existing.SkillId] = existing;
        }

        foreach (var skillId in touched)
        {
            var incorrect = incorrectSkills.Contains(skillId);
            var before = updated.TryGetValue(skillId, out var known)
                ? known
                : new SkillMastery(skillId, Parameters.PriorKnown, 0, 0);
            updated[skillId] = new SkillMastery(
                skillId,
                Parameters.Step(before.PKnown, !incorrect),
                before.Attempts + 1,
                before.Errors + (incorrect ? 1 : 0));
        }

        return this with { Skills = updated.Values.ToImmutableArray() };
    }

    /// <summary>Structural equality including the skill list.</summary>
    public bool Equals(LearnerModel? other)
    {
        return other is not null
            && Parameters.Equals(other.Parameters)
            && ListEquality.Equal(Skills, other.Skills);
    }

    /// <summary>Hash consistent with <see cref="Equals(LearnerModel)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(Parameters);
        ListEquality.Add(ref hash, Skills);
        return hash.ToHashCode();
    }

    private static ImmutableArray<SkillMastery> Normalize(IReadOnlyList<SkillMastery> skills)
    {
        ArgumentNullException.ThrowIfNull(skills, nameof(Skills));
        var sorted = skills.ToArray();
        Array.Sort(sorted, static (a, b) => string.CompareOrdinal(a.SkillId, b.SkillId));
        for (var i = 1; i < sorted.Length; i++)
        {
            if (string.Equals(sorted[i - 1].SkillId, sorted[i].SkillId, StringComparison.Ordinal))
            {
                throw new ArgumentException($"Duplicate skill '{sorted[i].SkillId}'.", nameof(skills));
            }
        }

        return ImmutableArray.Create(sorted);
    }
}
