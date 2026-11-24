using System;
using BaseClasses.Enum;
using BaseClasses.Interface;
using BaseClasses.Model;
using BaseClasses.Model.Params;
using BaseClasses.Services.Binds;
using LocationGenerator.Params;

namespace LocationGenerator.Tests.Support
{
    internal sealed class TopologyGraphFixture
    {
        public Plot Plot { get; } = new Plot();

        public IElement Location() => Add(new Element(ElemType.Location));

        public IElement Item() => Add(new Element(ElemType.Item));

        public IElement MissingLocation() => new Element(ElemType.Location);

        public IElement InjectDuplicateIdLocation(Guid id)
        {
            var duplicate = new FixedIdElement(id, ElemType.Location);
            Plot.Elements.Add(duplicate);
            return duplicate;
        }

        public void Contains(IElement source, IElement target, bool active = true) =>
            Relate(source, target, BaseRelationKeys.Contains, active);

        public void Passage(IElement source, IElement target, bool active = true) =>
            Relate(source, target, BaseRelationKeys.Passage, active);

        public void Entrance(IElement source, IElement target, bool active = true) =>
            Relate(source, target, LocationRelationKeys.Entrance, active);

        public void Relate(IElement source, IElement target, IParamKey key, object? value) =>
            Plot.Relations.Add(new Relation(source, target, key, value));

        private IElement Add(IElement element)
        {
            Plot.Add(element);
            return element;
        }

        private sealed class FixedIdElement : IElement
        {
            public FixedIdElement(Guid id, ElemType type)
            {
                Id = id;
                Type = type;
                Name = string.Empty;
                Description = string.Empty;
                Params = new ParamBag();
                Time = -1;
            }

            public Guid Id { get; }
            public ElemType Type { get; }
            public string Name { get; set; }
            public string Description { get; set; }
            public ParamBag Params { get; }
            public int Time { get; set; }
            public bool IsEmpty() => true;
        }
    }
}
