using BaseClasses.Enum;
using BaseClasses.Model;
using BaseClasses.Model.Params;
using BaseClasses.Services.Binds;
using Xunit;

namespace ProceduralGenerator.Tests;

public sealed class ProceduralGeneratorTests
{
    private static readonly ParamKey<List<Dictionary<string, object>>> TraitsKey = new("Traits", "Procedural");

    [Fact]
    public void Generate_LoadsCheckedInWrappedSettingsWithTraitAndPhobiaShapes()
    {
        var generator = new global::ProceduralGenerator.ProceduralGenerator(1, 1,
            Path.Combine(AppContext.BaseDirectory, "proceduralSettings.json"));
        generator.GenerationMethod = GenerationMethods.ChaoticRandom;
        var character = new Element(ElemType.Character);

        generator.Generate(new Plot(), character);

        Assert.True(character.Params.TryGet<List<Dictionary<string, object>>>(TraitsKey, out var traits));
        Assert.True(character.Params.TryGet<List<Dictionary<string, object>>>(new ParamKey<List<Dictionary<string, object>>>("Phobias", "Procedural"), out var phobias));
        Assert.NotEmpty(traits);
        Assert.NotEmpty(phobias);
        Assert.All(traits.Concat(phobias), trait =>
        {
            Assert.Contains("Title", trait.Keys);
            Assert.Contains("Affection", trait.Keys);
            Assert.Contains("Description", trait.Keys);
        });
    }

    [Fact]
    public void Generate_TwoParentsHalfUsesTwoActiveBaseChildRelations()
    {
        WithLegacySettings(settingsPath =>
        {
            var generator = CreateTwoParentsGenerator(settingsPath);
            var plot = new Plot();
            var firstParent = Parent("brave");
            var secondParent = Parent("calm");
            var child = new Element(ElemType.Character);
            Add(plot, firstParent, secondParent, child);
            plot.Relations.Add(new Relation(firstParent, child, BaseRelationKeys.Child, true));
            plot.Relations.Add(new Relation(secondParent, child, BaseRelationKeys.Child, true));

            generator.Generate(plot, child);

            Assert.True(child.Params.TryGet<List<Dictionary<string, object>>>(TraitsKey, out var traits));
            Assert.Single(traits);
            Assert.Contains(traits[0]["Title"], new[] { "Brave", "Calm" });
        });
    }

    [Fact]
    public void Generate_TwoParentsHalfRejectsMissingParents()
    {
        WithLegacySettings(settingsPath =>
        {
            var generator = CreateTwoParentsGenerator(settingsPath);
            var plot = new Plot();
            var parent = Parent("brave");
            var child = new Element(ElemType.Character);
            Add(plot, parent, child);
            plot.Relations.Add(new Relation(parent, child, BaseRelationKeys.Child, true));

            var exception = Assert.Throws<ArgumentException>(() => generator.Generate(plot, child));

            Assert.Contains("parent", exception.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Generate_TwoParentsHalfRejectsExtraParents()
    {
        WithLegacySettings(settingsPath =>
        {
            var generator = CreateTwoParentsGenerator(settingsPath);
            var plot = new Plot();
            var firstParent = Parent("brave");
            var secondParent = Parent("calm");
            var extraParent = Parent("brave");
            var child = new Element(ElemType.Character);
            Add(plot, firstParent, secondParent, extraParent, child);
            plot.Relations.Add(new Relation(firstParent, child, BaseRelationKeys.Child, true));
            plot.Relations.Add(new Relation(secondParent, child, BaseRelationKeys.Child, true));
            plot.Relations.Add(new Relation(extraParent, child, BaseRelationKeys.Child, true));

            var exception = Assert.Throws<ArgumentException>(() => generator.Generate(plot, child));

            Assert.Contains("more than two", exception.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Generate_TwoParentsHalfRejectsNonCharacterParents()
    {
        WithLegacySettings(settingsPath =>
        {
            var generator = CreateTwoParentsGenerator(settingsPath);
            var plot = new Plot();
            var characterParent = Parent("brave");
            var itemParent = Parent("calm", ElemType.Item);
            var child = new Element(ElemType.Character);
            Add(plot, characterParent, itemParent, child);
            plot.Relations.Add(new Relation(characterParent, child, BaseRelationKeys.Child, true));
            plot.Relations.Add(new Relation(itemParent, child, BaseRelationKeys.Child, true));

            var exception = Assert.Throws<ArgumentException>(() => generator.Generate(plot, child));

            Assert.Contains("Character", exception.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Generate_TwoParentsHalfIgnoresRelationsOutsideBaseChildKey()
    {
        WithLegacySettings(settingsPath =>
        {
            var generator = CreateTwoParentsGenerator(settingsPath);
            var plot = new Plot();
            var firstParent = Parent("brave");
            var secondParent = Parent("calm");
            var child = new Element(ElemType.Character);
            Add(plot, firstParent, secondParent, child);
            var lookAlikeKey = new ParamKey<bool>("Child", "Other");
            plot.Relations.Add(new Relation(firstParent, child, lookAlikeKey, true));
            plot.Relations.Add(new Relation(secondParent, child, lookAlikeKey, true));

            var exception = Assert.Throws<ArgumentException>(() => generator.Generate(plot, child));

            Assert.Contains("parent", exception.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Generate_TwoParentsHalfIgnoresInactiveBaseChildRelations()
    {
        WithLegacySettings(settingsPath =>
        {
            var generator = CreateTwoParentsGenerator(settingsPath);
            var plot = new Plot();
            var firstParent = Parent("brave");
            var secondParent = Parent("calm");
            var child = new Element(ElemType.Character);
            Add(plot, firstParent, secondParent, child);
            plot.Relations.Add(new Relation(firstParent, child, BaseRelationKeys.Child, false));
            plot.Relations.Add(new Relation(secondParent, child, BaseRelationKeys.Child, false));

            var exception = Assert.Throws<ArgumentException>(() => generator.Generate(plot, child));

            Assert.Contains("parent", exception.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static global::ProceduralGenerator.ProceduralGenerator CreateTwoParentsGenerator(string settingsPath) =>
        new(1, 0, settingsPath) { GenerationMethod = GenerationMethods.TwoParentsHalf };

    private static Element Parent(string traitName, ElemType type = ElemType.Character)
    {
        var parent = new Element(type);
        parent.Params.Set(TraitsKey, new List<Dictionary<string, object>>
        {
            new()
            {
                ["Title"] = char.ToUpperInvariant(traitName[0]) + traitName[1..],
                ["Affection"] = 0.5,
                ["Description"] = traitName
            }
        });
        return parent;
    }

    private static void Add(Plot plot, params Element[] elements)
    {
        foreach (var element in elements)
        {
            plot.Add(element);
        }
    }

    private static void WithLegacySettings(Action<string> test)
    {
        var path = Path.Combine(Path.GetTempPath(), $"procedural-settings-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """
            {
              "traits": {
                "brave": ["Brave description"],
                "calm": ["Calm description"]
              },
              "phobias": {
                "dark": ["Dark description"]
              }
            }
            """);
        try
        {
            test(path);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
