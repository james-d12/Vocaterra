# M87 report (agent, 2026-11-08), condensed

Branch `M87-pack-surface`, SHA `57fa4d5b`; commits d0f03bd0 (c), 9fdb0fb2 (a)(b), 57fa4d5b (api.md). 10411 passed, 3 skipped (agent).

Mapping for T01: `SpanishMorphology()` → `bundle.LanguageMorphology` (game uses `Conjugate`, `Article` 4-arg, `Pluralize`); retype
`MorphologyGenerator` params in BookPages.Verbs, ConjugationTable, VerbReading, LessonBook to `IMorphology`; `ConjugationTable.Persons` →
`Paradigm.Persons`, columns → `Paradigm.Tenses` (near-future and obligation stay game periphrases); `SpanishNumbers.ToWords`/
`TryParsePrefix` → `bundle.Language.Numbers`; `SpanishTextNormalizer.Normalize` → `bundle.Language.Normalizer`; `SpanishAccentNotes.Describe`
→ `bundle.Language.Orthography.Describe`.

New public: `IOrthographyNotes`, `ILanguagePack.Orthography`, `ParadigmLayout`, `ParadigmPerson`, `ParadigmCell`, `IMorphology.Paradigm`,
`IMorphology.ParadigmOf`, optional `errorTags` on the scorers. Not neutral: German separable verbs and stem changes (string `Conjugate`),
formal Sie shares 3 pl. (d) already done by M34; ContentBuilder tools stay on V1.
