# ProceduralGenerator

`ProceduralGenerator` loads procedural data from either root-level settings or
an optional `proceduralSettings` wrapper. The checked-in settings use that
wrapper, with `traits` and `phobias` represented as arrays of one-key objects.
Generated entries remain `Dictionary<string, object>` values containing
`Title`, `Affection`, and `Description`.

Two-parent generation reads active (`true`) `BaseRelationKeys.Child` relations
whose source is the generated element and target is a parent. It requires exactly two parent elements,
and both must have `ElemType.Character`.

Random selection continues to use `Random.Shared`; callers cannot provide a
seed. Schema validation beyond the supported wrapper and one-key arrays, and
semantics for `relatedTraitTable`, remain intentionally deferred.
