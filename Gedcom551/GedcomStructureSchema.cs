// Copyright (c) Armidale Software
// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Yaml.Serialization;

namespace Gedcom551
{
    public struct GedcomStructureCountInfo
    {
        public bool Required; // True: Minimum = 1, False: Minimum = 0.
        public bool Singleton; // True: Maximum = 1, False: Maximum = M.
        public int FixedMax; // 0: no arbitrary max, non-zero: arbitrary max.
        public override string ToString()
        {
            return "{" + (Required ? "1" : "0") + ":" + (Singleton ? "1" : (FixedMax > 0 ? FixedMax : "M")) + "}";
        }
        public static GedcomStructureCountInfo FromCardinality(string cardinality)
        {
            var info = new GedcomStructureCountInfo();
            if (cardinality == "{0:1}")
            {
                info.Singleton = true;
            }
            else if (cardinality == "{0:M}")
            {
                // Nothing to do.
            }
            else if (cardinality == "{1:M}")
            {
                info.Required = true;
            }
            else if (cardinality == "{1:1}")
            {
                info.Singleton = true;
                info.Required = true;
            }
            else if (cardinality == "{0:3}")
            {
                info.FixedMax = 3;
            }
            else
            {
                throw new Exception("Bad cardinality: " + cardinality);
            }
            return info;
        }
    }
    public struct GedcomStructureSchemaKey
    {
        public string SourceProgram; // null (wildcard) for standard tags.
        public string SuperstructureUri; // null (wildcard) for undocumented extensions, "-" for records.
        public string Tag;
        public override string ToString()
        {
            return SourceProgram + "|" + SuperstructureUri + "|" + Tag;
        }
    }
    public class GedcomStructureSchema
    {
        public static void AddStrings(List<string> list, Object[] array)
        {
            if (array != null)
            {
                foreach (var value in array)
                {
                    list.Add(value as string);
                }
            }
        }
        static void AddDictionary(Dictionary<string, GedcomStructureCountInfo> dictionary, Dictionary<object, object> input)
        {
            if (input != null)
            {
                foreach (var key in input.Keys)
                {
                    var value = input[key] as string;
                    var info = new GedcomStructureCountInfo();
                    if (value == "{0:1}")
                    {
                        info.Required = false;
                        info.Singleton = true;
                    }
                    else if (value == "{1:1}")
                    {
                        info.Required = true;
                        info.Singleton = true;
                    }
                    else if (value == "{0:M}")
                    {
                        info.Required = false;
                        info.Singleton = false;
                    }
                    else if (value == "{1:M}")
                    {
                        info.Required = true;
                        info.Singleton = false;
                    }
                    else
                    {
                        throw new Exception();
                    }
                    dictionary[key as string] = info;
                }
            }
        }

        GedcomStructureSchema(string sourceProgram, string tag)
        {
            this.StandardTag = tag;
            this.Specification = new List<string>();
            this.Substructures = new Dictionary<string, GedcomStructureCountInfo>();
            this.Superstructures = new Dictionary<string, GedcomStructureCountInfo>();
        }

        /// <summary>
        /// Check whether this schema is a standard schema (even if relocated).
        /// </summary>
        public bool IsStandard => (this.StandardTag[0] != '_');
        public bool IsDocumented => (this.Uri != null);

        public override string ToString()
        {
            return this.StandardTag;
        }

        GedcomStructureSchema(Dictionary<object, object> dictionary)
        {
            this.Lang = dictionary["lang"] as string;
            this.Type = dictionary["type"] as string;
            this.Uri = dictionary["uri"] as string;
            this.StandardTag = dictionary["standard tag"] as string;
            this.Label = dictionary["label"] as string;
            this.Payload = dictionary["payload"] as string;
            if (dictionary.ContainsKey("enumeration set"))
            {
                this.EnumerationSetUri = dictionary["enumeration set"] as string;
            }
            this.Specification = new List<string>();
            AddStrings(this.Specification, dictionary["specification"] as Object[]);
            this.Substructures = new Dictionary<string, GedcomStructureCountInfo>();
            AddDictionary(this.Substructures, dictionary["substructures"] as Dictionary<object, object>);
            this.Superstructures = new Dictionary<string, GedcomStructureCountInfo>();
            AddDictionary(this.Superstructures, dictionary["superstructures"] as Dictionary<object, object>);
        }
        public string Lang { get; private set; }
        public string Type { get; private set; }
        public string Uri { get; private set; }
        public string StandardTag { get; private set; }
        public List<string> Specification { get; private set; }
        public string Label { get; private set; }
        public string Payload { get; private set; }
        public string EnumerationSetUri { get; private set; }
        public EnumerationSet EnumerationSet => EnumerationSet.GetEnumerationSet(EnumerationSetUri);
        public bool HasPointer => (this.Payload != null) && this.Payload.StartsWith("@<") && this.Payload.EndsWith(">@");
        public Dictionary<string, GedcomStructureCountInfo> Substructures { get; private set; }
        public Dictionary<string, GedcomStructureCountInfo> Superstructures { get; private set; }

        static Dictionary<GedcomStructureSchemaKey, GedcomStructureSchema> s_StructureSchemas = new Dictionary<GedcomStructureSchemaKey, GedcomStructureSchema>();
        static Dictionary<string, string> s_StructureSchemaAliases = new System.Collections.Generic.Dictionary<string, string>();
        static Dictionary<string, GedcomStructureSchema> s_StructureSchemasByUri = new Dictionary<string, GedcomStructureSchema>();

        public const string RecordSuperstructureUri = "TOP";

        /// <summary>
        /// Add a schema.
        /// </summary>
        /// <param name="sourceProgram">null (wildcard) for standard tags, else extension</param>
        /// <param name="superstructureUri">null (wildcard) for undocumented tags, RecordSuperstructureUri for records, else URI of superstructure schema</param>
        /// <param name="tag">Tag</param>
        /// <param name="schema">Schema</param>
        public static void AddSchema(string sourceProgram, string superstructureUri, string tag, GedcomStructureSchema schema)
        {
            if (tag.Contains('|') || tag.Contains('['))
            {
                throw new Exception("Invalid tag");
            }
            GedcomStructureSchemaKey structureSchemaKey = new GedcomStructureSchemaKey();
            structureSchemaKey.SourceProgram = sourceProgram;
            structureSchemaKey.SuperstructureUri = superstructureUri;
            structureSchemaKey.Tag = tag;
            Debug.Assert(!s_StructureSchemas.ContainsKey(structureSchemaKey));
            s_StructureSchemas[structureSchemaKey] = schema;
        }

        /// <summary>
        /// Add a schema.
        /// </summary>
        /// <param name="sourceProgram">null (wildcard) for standard tags, else extension</param>
        /// <param name="tag">Tag</param>
        /// <param name="uri">Structure URI</param>
        public static GedcomStructureSchema? AddSchema(string sourceProgram, string tag, string uri)
        {
            if (tag.Contains('|') || tag.Contains('['))
            {
                throw new Exception("Invalid tag");
            }
            GedcomStructureSchemaKey structureSchemaKey = new GedcomStructureSchemaKey();
            structureSchemaKey.SourceProgram = sourceProgram;
            // Leave SuperstructureUri as null for a wildcard.
            structureSchemaKey.Tag = tag;

            // The spec says:
            //    "The schema structure may contain the same tag more than once with different URIs.
            //    Reusing tags in this way must not be done unless the concepts identified by those
            //    URIs cannot appear in the same place in a dataset..."
            // But for now we just overwrite it in the index for SCHMA defined schemas.

            if (s_StructureSchemasByUri.ContainsKey(uri))
            {
                // This is an alias.
                s_StructureSchemaAliases[tag] = uri;
                return null;
            }

            var schema = new GedcomStructureSchema(sourceProgram, tag);
            schema.Uri = uri;
            s_StructureSchemas[structureSchemaKey] = schema;
            return schema;
        }

        public static void LoadAll(string gedcomRegistriesPath = null)
        {
            if (s_StructureSchemas.Count > 0)
            {
                return;
            }
            if (gedcomRegistriesPath == null)
            {
                var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                gedcomRegistriesPath = Path.Combine(baseDirectory, "../../../../../gedcom7/external/GEDCOM-registries");
            }
            var path = Path.Combine(gedcomRegistriesPath, "structure/standard");
            string[] files = Directory.GetFiles(path);
            foreach (string filename in files)
            {
                var serializer = new YamlSerializer();
                object[] myObject = serializer.DeserializeFromFile(filename);
                var dictionary = myObject[0] as Dictionary<object, object>;
                var schema = new GedcomStructureSchema(dictionary);
                s_StructureSchemasByUri[schema.Uri] = schema;
                if (schema.Superstructures.Count == 0)
                {
                    AddSchema(null, RecordSuperstructureUri, schema.StandardTag, schema);
                }
                else
                {
                    foreach (var superstructureUri in schema.Superstructures.Keys)
                    {
                        AddSchema(null, superstructureUri, schema.StandardTag, schema);
                    }
                }
            }
            EnumerationSet.LoadAll(gedcomRegistriesPath);
            CalendarSchema.LoadAll(gedcomRegistriesPath);
        }
        public static void SaveAll(string gedcomRegistriesPath)
        {
            var path = Path.Combine(gedcomRegistriesPath, "structure", "standard");
            try
            {
                // Create the directory if it doesn't exist.
                Directory.CreateDirectory(path);
            }
            catch (Exception e)
            {
                Console.WriteLine($"An error occurred: {e.Message}");
                return;
            }

            foreach (var info in s_StructureSchemas)
            {
                GedcomStructureSchema schema = info.Value;
                var serializer = new YamlSerializer();
                string filePath = Path.Combine(path, schema.StandardTag + ".yaml");
                try
                {
                    // Create a StreamWriter object to open the file for writing.
                    using (StreamWriter writer = new StreamWriter(filePath))
                    {
                        // Write lines to the file.
                        writer.WriteLine("%YAML 1.2\r\n---");
                        writer.WriteLine("lang: en-US\n");
                        writer.WriteLine("type: structure\n");
                        writer.WriteLine("uri: " + schema.Uri + "\n");
                        writer.WriteLine("standard tag: '" + schema.StandardTag + "'\n");

                        writer.WriteLine("specification:");
                        foreach (var line in schema.Specification)
                        {
                            writer.WriteLine(line);
                        }
                        writer.WriteLine();

                        if (schema.Label != null)
                        {
                            writer.WriteLine("label: '" + schema.Label + "'\n");
                        }
                        if (schema.Payload == null)
                        {
                            writer.WriteLine("payload: null\n");
                        }
                        else
                        {
                            writer.WriteLine("payload: " + schema.Payload + "\n");
                        }
                        if (schema.Substructures.Count == 0)
                        {
                            writer.WriteLine("substructures: {}\n");
                        }
                        else
                        {
                            writer.WriteLine("substructures:");
                            foreach (var sub in schema.Substructures)
                            {
                                string key = sub.Key;
                                GedcomStructureCountInfo subInfo = sub.Value;
                                writer.WriteLine($"  \"{key}\": \"{subInfo}\"");
                            }
                            writer.WriteLine();
                        }
                        if (schema.Superstructures.Count == 0)
                        {
                            writer.WriteLine("superstructures: {}\n");
                        }
                        else
                        {
                            writer.WriteLine("superstructures:");
                            foreach (var super in schema.Superstructures)
                            {
                                string key = super.Key;
                                GedcomStructureCountInfo superInfo = super.Value;
                                writer.WriteLine($"  \"{key}\": \"{superInfo}\"");
                            }
                            writer.WriteLine();
                        }
                        writer.WriteLine("contact: https://gedcom.io/community/\n");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"An error occurred: {e.Message}");
                }
            }
        }

        public static GedcomStructureSchema GetSchema(string uri) => s_StructureSchemasByUri.ContainsKey(uri) ? s_StructureSchemasByUri[uri] : null;

        /// <summary>
        /// Get a GEDCOM structure schema.
        /// </summary>
        /// <param name="sourceProgram">source program string, or null for wildcard</param>
        /// <param name="superstructureUri">superstructure URI, or null for wildcard</param>
        /// <param name="tag"></param>
        /// <returns></returns>
        public static GedcomStructureSchema GetSchema(string sourceProgram, string superstructureUri, string tag)
        {
            if (tag.Contains('|') || tag.Contains('['))
            {
                throw new Exception();
            }
            // First look for a schema with a wildcard source program.
            GedcomStructureSchemaKey structureSchemaKey = new GedcomStructureSchemaKey();
            structureSchemaKey.SuperstructureUri = superstructureUri;
            structureSchemaKey.Tag = tag;
            if (s_StructureSchemas.ContainsKey(structureSchemaKey))
            {
                return s_StructureSchemas[structureSchemaKey];
            }

            // Now look for a schema specific to the source program
            // and superstructure URI, which would be a documented
            // extension tag.
            if (sourceProgram == null)
            {
                sourceProgram = "Unknown";
            }
            structureSchemaKey.SourceProgram = sourceProgram;
            if (s_StructureSchemas.ContainsKey(structureSchemaKey))
            {
                return s_StructureSchemas[structureSchemaKey];
            }

            // Now look for a schema specific to the source program
            // and wildcard superstructure URI, which would be an
            // undocumented extension tag.
            structureSchemaKey.SuperstructureUri = null;
            if (s_StructureSchemas.ContainsKey(structureSchemaKey))
            {
                return s_StructureSchemas[structureSchemaKey];
            }

            // Now look for a schema alias defined in HEAD.SCHMA.
            GedcomStructureSchema schema;
            if (s_StructureSchemaAliases.ContainsKey(tag))
            {
                string uri = s_StructureSchemaAliases[tag];
                if (s_StructureSchemasByUri.TryGetValue(uri, out schema))
                {
                    return schema;
                }
            }

            // Create a new schema for it.
            structureSchemaKey.SuperstructureUri = superstructureUri;
            schema = new GedcomStructureSchema(sourceProgram, tag);
            s_StructureSchemas[structureSchemaKey] = schema;
            return s_StructureSchemas[structureSchemaKey];
        }
    }
}

