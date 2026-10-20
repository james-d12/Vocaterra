namespace Vocaterra.Nlp.Decision.Learning;

/// <summary>The tracked mastery of one skill: probability known, number of attempts and number of those that were errors.</summary>
public sealed record SkillMastery(string SkillId, double PKnown, int Attempts, int Errors);
