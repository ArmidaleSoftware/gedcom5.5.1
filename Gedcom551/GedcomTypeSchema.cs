// Copyright (c) Armidale Software
// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Text;

namespace Gedcom551.Construct
{
    public class GedcomTypeSchema
    {
        static List<GedcomTypeSchema> s_TypeSchemas = new List<GedcomTypeSchema>();

        public string Name { get; private set; }
        public List<string> Specification { get; private set; }

        public static GedcomTypeSchema GetTypeSchema(string name)
        {
            foreach (var typeSchema in s_TypeSchemas)
            {
                if (typeSchema.Name == name)
                {
                    return typeSchema;
                }
            }

            var schema = new GedcomTypeSchema(name);
            s_TypeSchemas.Add(schema);
            return schema;
        }

        GedcomTypeSchema(string name)
        {
            Specification = new List<string>();
            Name = name;
        }
    }
}
