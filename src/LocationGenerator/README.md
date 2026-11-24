# LocationGenerator

`LocationGenerator` builds a location topology inside `BaseClasses.Model.Plot`.
It owns generation requests, strategies, diagnostics, and the local
`LocationRelationKeys.Entrance` key. It does not own the reusable graph keys:
new locations are structurally connected with `BaseRelationKeys.Contains`, and
their traversable graph uses `BaseRelationKeys.Passage`.

`Contains` and `Passage` are supplied by `BaseClasses`; `Plot.Bind` creates the
supported reciprocal `ContainedBy` and symmetric-passage relations. Entrance
remains distinct and is never synthesized by topology generation.

```csharp
using BaseClasses.Services.Binds;

var result = generator.Generate(plot, root, request);
var children = LocationTopology.GetChildren(plot, root);
var neighbours = LocationTopology.GetPassageNeighbors(plot, children[0]);
// result adds BaseRelationKeys.Contains and BaseRelationKeys.Passage relations.
```

Consumers that need map output should pass the resulting plot to `Map.Core`.
Active explicit base passages guide placement and are the only candidates for
generated roads. `Map.Core` projects those pairs only when
`RoadGenerationPolicy.ConnectFeatures` is enabled; containment, entrance, and
geometric proximity do not imply a road.
