// Copyright (c) Armidale Software
// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gedcom551
{
    public class SymbolComponent
    {
        public override string ToString()
        {
            return LevelString + " " + Id + " " + TagOrSymbolReference + " " + PayloadType + " " + Cardinality;
        }
        public string LevelString { get; set; }
        public int LevelDelta => LevelStringToInt(LevelString);
        public static int LevelStringToInt(string? level)
        {
            if (level == null || level == "n" || level == "0")
            {
                return 0;
            }
            if (level.StartsWith('+'))
            {
                return int.Parse(level.Substring(1));
            }
            throw new Exception();
        }

        public string Id { get; set; }
        public string TagOrSymbolReference { get; set; }
        public SymbolDefinition? SymbolDefinition { get; set; } = null;
        public string PayloadType { get; set; }
        public string Cardinality { get; set; }
    }

    public enum SymbolDefinitionKind
    {
        None = 0,
        Sequence = 1,
        Alternatives = 2,
    }

    public class SymbolDefinition
    {
        public string Name { get; private set; }
        public string Description { get; set; } = string.Empty;
        public List<SymbolComponent> Components { get; private set; } = new List<SymbolComponent>();
        private SymbolDefinitionKind _kind = SymbolDefinitionKind.Sequence;
        public SymbolDefinitionKind Kind
        {
            get { return _kind; }
            set
            {
                if (Components.Count > 0)
                {
                    throw new Exception("Already has components");
                }
                _kind = value;
            }
        }

        public SymbolDefinition(string symbol)
        {
            Name = symbol;
        }
        public override string ToString() => this.Name;
        public void AddComponent(string line)
        {
            SymbolComponent component = new SymbolComponent();
            var tokens = line.Split(' ');
            for (int index = 0;  index < tokens.Length; index++)
            {
                if (component.LevelString == null)
                {
                    component.LevelString = tokens[index];
                    continue;
                }
                if (component.Id == null && component.TagOrSymbolReference == null && tokens[index].StartsWith('@'))
                {
                    component.Id = tokens[index];
                    continue;
                }
                if (component.TagOrSymbolReference == null)
                {
                    component.TagOrSymbolReference = tokens[index];
                    continue;
                }
                if (component.PayloadType == null && !tokens[index].StartsWith('{'))
                {
                    string payload = tokens[index].Trim('<', '>');
                    component.PayloadType = payload;
                    continue;
                }
                if (component.Cardinality == null && tokens[index].StartsWith('{'))
                {
                    component.Cardinality = tokens[index];
                    continue;
                }
                throw new Exception("Malformed line: " + line);
            }
            if (component.Cardinality == null)
            {
                // Work around spec bug in MULTIMEDIA_LINK and FAMILY_EVENT_STRUCTURE.
                component.Cardinality = "{1:1}";
            }
            Components.Add(component);
        }
    }
}
