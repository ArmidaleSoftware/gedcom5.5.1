﻿// Copyright (c) Armidale Software
// SPDX-License-Identifier: MIT
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.XPath;
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
        public static readonly string Wildcard = "*";

        public string SourceProgram; // "*" (wildcard) for standard tags.
        public string SuperstructureUri; // "*"" (wildcard) for undocumented extensions, "-" for records.
        public string Tag;

        // Payload type.  For GEDCOM 7.0 this is always "*" since the rest of the tuple is unambiguous.
        // GEDCOM 5.5.1 and earlier, however, had cases with multiple possibilities.
        public string Payload = Wildcard;

        public override string ToString()
        {
            return SourceProgram + "|" + SuperstructureUri + "|" + Tag + "|" + Payload;
        }

        public GedcomStructureSchemaKey(string tag, string sourceProgram, string superstructureUri)
        {
            Tag = tag;
            SourceProgram = sourceProgram;
            SuperstructureUri = superstructureUri;
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
            this.Lang = string.Empty;
            this.Label = string.Empty;
            this._uri = string.Empty;
            this.Payload = string.Empty;
            this.Type = string.Empty;
            this.EnumerationSetUri = string.Empty;
        }

        /// <summary>
        /// Check whether this schema is a standard schema (even if relocated).
        /// </summary>
        public bool IsStandard => (this.StandardTag[0] != '_');
        public bool IsDocumented => (this.Uri != null);

        public override string ToString()
        {
            return this.StandardTag + " " + ((this.Payload == null) ? "<NULL>" : this.Payload);
        }

        GedcomStructureSchema(Dictionary<object, object> dictionary)
        {
            this.Lang = (dictionary["lang"] as string) ?? string.Empty;
            this.Type = (dictionary["type"] as string) ?? string.Empty;
            this._uri = (dictionary["uri"] as string) ?? string.Empty;
            this.StandardTag = (dictionary["standard tag"] as string) ?? string.Empty;
            this.Label = (dictionary["label"] as string) ?? string.Empty;
            this.Payload = (dictionary["payload"] as string) ?? string.Empty;
            if (dictionary.ContainsKey("enumeration set"))
            {
                this.EnumerationSetUri = (dictionary["enumeration set"] as string) ?? string.Empty;
            }
            else
            {
                this.EnumerationSetUri = string.Empty;
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
        private string _uri;
        public string Uri {
            get
            {
                return _uri;
            }
            private set
            {
                // Rename URI in superstructures.
                foreach (string super in this.Superstructures.Keys)
                {
                    foreach (var other in s_StructureSchemas)
                    {
                        GedcomStructureSchemaKey key = other.Key;
                        GedcomStructureSchema schema = other.Value;
                        if (schema.Uri == super)
                        {
                            if (!schema.Substructures.ContainsKey(_uri))
                            {
                                // TODO: throw new Exception();
                                continue;
                            }
                            GedcomStructureCountInfo info = schema.Substructures[_uri];
                            schema.Substructures.Remove(_uri);
                            schema.Substructures[value] = info;
                        }
                    }
                }

                // Rename URI in substructures.
                foreach (string sub in this.Substructures.Keys)
                {
                    foreach (var other in s_StructureSchemas)
                    {
                        GedcomStructureSchemaKey key = other.Key;
                        GedcomStructureSchema schema = other.Value;
                        if (schema.Uri == sub)
                        {
                            if (!schema.Superstructures.ContainsKey(_uri))
                            {
                                // TODO: throw new Exception();
                                continue;
                            }
                            GedcomStructureCountInfo info = schema.Superstructures[_uri];
                            schema.Superstructures.Remove(_uri);
                            schema.Superstructures[value] = info;
                        }
                    }
                }
                _uri = value;
            }
        }
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
            var structureSchemaKey = new GedcomStructureSchemaKey(tag, sourceProgram, superstructureUri);
            Debug.Assert(!s_StructureSchemas.ContainsKey(structureSchemaKey));
            s_StructureSchemas[structureSchemaKey] = schema;
        }

        /// <summary>
        /// Verify that all URIs are unique.
        /// </summary>
        public static void VerifyUniqueUris()
        {
            foreach (GedcomStructureSchema schema in s_StructureSchemas.Values)
            {
                var matches = s_StructureSchemas.Values.Where(s => s.Uri == schema.Uri).ToList();
                Debug.Assert(matches.Count == 1);
            }
        }

        /// <summary>
        /// Sanity check that supers point to subs and vice versa.
        /// </summary>
        public static void VerifyBackpointers()
        {
            foreach (GedcomStructureSchema schema in s_StructureSchemas.Values)
            {
                foreach (string superUri in schema.Superstructures.Keys)
                {
                    var supers = s_StructureSchemas.Values.Where(v => v.Uri == superUri);
                    bool found = false;
                    foreach (var super in supers)
                    {
                        if (super.Substructures.Keys.Contains(schema.Uri))
                        {
                            found = true;
                            break;
                        }
                    }
                    Debug.Assert(found);
                }
                foreach (string subUri in schema.Substructures.Keys)
                {
                    var subs = s_StructureSchemas.Values.Where(v => v.Uri == subUri);
                    bool found = false;
                    foreach (var sub in subs)
                    {
                        if (sub.Superstructures.Keys.Contains(schema.Uri))
                        {
                            found = true;
                            break;
                        }
                    }
                    Debug.Assert(found);
                }
            }
        }

        /// <summary>
        /// Add a schema.  When first adding schemas, we generate a separate entry for each
        /// superstructure since we can't know whether it is a duplicate until we're done reading
        /// all the substructures.  We'll collapse them into unique schemas in a separate pass
        /// once we're done (see CollapseSchemas()).  Until then, it's possible that two schemas
        /// with the same URI can have different lists of substructures.
        /// </summary>
        /// <param name="sourceProgram">null (wildcard) for standard tags, else extension</param>
        /// <param name="tag">Tag</param>
        /// <param name="uri">Structure URI</param>
        /// <param name="payloadType">Payload type</param>
        /// <param name="superstructureUri">Superstructure URI.  null (wildcard) for undocumented extensions, "-" for records</param>
        public static GedcomStructureSchema? AddSchema(string sourceProgram, string tag, string uri, string payloadType, string superstructureUri)
        {
            Debug.Assert(payloadType != GedcomStructureSchemaKey.Wildcard);
            Debug.Assert(sourceProgram != GedcomStructureSchemaKey.Wildcard);
            Debug.Assert(superstructureUri != GedcomStructureSchemaKey.Wildcard);

            if (tag.Contains('|') || tag.Contains('['))
            {
                throw new Exception("Invalid tag");
            }
            var wildcardPayloadSchemaKey = new GedcomStructureSchemaKey(tag, sourceProgram, superstructureUri);
            var specificPayloadSchemaKey = new GedcomStructureSchemaKey(tag, sourceProgram, superstructureUri);
            specificPayloadSchemaKey.Payload = payloadType;

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

            // See if we already have this schema.
            if (s_StructureSchemas.ContainsKey(specificPayloadSchemaKey))
            {
                GedcomStructureSchema oldSchema = s_StructureSchemas[specificPayloadSchemaKey];
                if (oldSchema.StandardTag == tag &&
                    oldSchema.Payload == payloadType)
                {
                    // The old schema is identical, so use it.
                    return oldSchema;
                }
                else
                {
                    Console.WriteLine();
                }
            }
            if (s_StructureSchemas.ContainsKey(wildcardPayloadSchemaKey))
            {
                GedcomStructureSchema oldSchema = s_StructureSchemas[wildcardPayloadSchemaKey];
                if (oldSchema.StandardTag == tag &&
                    oldSchema.Payload == payloadType &&
                    oldSchema.Uri == uri)
                {
                    // The old schema is identical, so use it.
                    return oldSchema;
                }
            }

            // Create a new schema to add.
            var schema = new GedcomStructureSchema(sourceProgram, tag);
            schema.Uri = uri;
            schema.Payload = payloadType;

            // Disambiguate schemas if there are now multiple.
            if (s_StructureSchemas.ContainsKey(wildcardPayloadSchemaKey))
            {
                // We already have one with the same general key.
                // Disambigate by using payload type in both the old and new keys.

                // First find and remove the ambiguous one.
                var oldKey = wildcardPayloadSchemaKey;
                var newKey = wildcardPayloadSchemaKey;
                GedcomStructureSchema oldSchema = s_StructureSchemas[wildcardPayloadSchemaKey];
                if (oldSchema.Payload == payloadType)
                {
                    throw new Exception("Duplicate schema");
                }
                s_StructureSchemas.Remove(wildcardPayloadSchemaKey);

                string newSchemaUri = MakeUri(GedcomStructureSchemaKey.Wildcard, tag, schema.Payload);
                string oldSchemaUri = MakeUri(GedcomStructureSchemaKey.Wildcard, tag, oldSchema.Payload);
                Console.WriteLine("splitting <" + schema.Uri + "> OLD: " + oldSchemaUri);
                Console.WriteLine("splitting <" + schema.Uri + "> NEW: " + newSchemaUri);

                // Add a more specific one for the old schema.
                if (oldKey.Payload != GedcomStructureSchemaKey.Wildcard)
                {
                    throw new Exception("Duplicate schema");
                }
                oldKey.Payload = oldSchema.Payload;
                if (s_StructureSchemas.ContainsKey(oldKey))
                {
                    return s_StructureSchemas[oldKey];
                }
                oldSchema.Uri = oldSchemaUri;
                s_StructureSchemas[oldKey] = oldSchema;

                VerifyBackpointers();

                // Add a more specific one for the new schema.
                if (newKey.Payload != GedcomStructureSchemaKey.Wildcard)
                {
                    throw new Exception("Duplicate schema");
                }
                newKey.Payload = schema.Payload;
                if (s_StructureSchemas.ContainsKey(newKey))
                {
                    return s_StructureSchemas[newKey];
                }
                schema.Uri = newSchemaUri;
                s_StructureSchemas[newKey] = schema;

                VerifyBackpointers();
            }
            else
            {
                GedcomStructureSchema? other = s_StructureSchemas.Values.Where(v => v.Uri == schema.Uri).FirstOrDefault();
                if (other != null)
                {
                    // This URI is already in use. Disambiguate them.
                    string newSchemaUri = MakeUri(GedcomStructureSchemaKey.Wildcard, tag, schema.Payload);
                    string oldSchemaUri = MakeUri(GedcomStructureSchemaKey.Wildcard, tag, other.Payload);
                    if (newSchemaUri != oldSchemaUri)
                    {
                        Console.WriteLine("splitting <" + schema.Uri + "> OLD: " + oldSchemaUri);
                        Console.WriteLine("splitting <" + schema.Uri + "> NEW: " + newSchemaUri);

                        // Update old URI.
                        other.Uri = oldSchemaUri;

                        VerifyBackpointers();

                        schema.Uri = newSchemaUri;

                        VerifyBackpointers();
                    }
                }
                s_StructureSchemas[wildcardPayloadSchemaKey] = schema;
            }
            return schema;
        }

        /// <summary>
        /// Load structure schemas from YAML files under a given file path.
        /// </summary>
        /// <param name="gedcomRegistriesPath"></param>
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

        /// <summary>
        /// Save all structure schemas in YAML files under a given file path.
        /// </summary>
        /// <param name="gedcomRegistriesPath"></param>
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

                // Get rightmost component of URI.
                string[] tokens = schema.Uri.Split('/');
                string name = tokens[tokens.Length - 1];

                string filePath = Path.Combine(path, name + ".yaml");
                try
                {
                    // Create a StreamWriter object to open the file for writing.
                    using (StreamWriter writer = new StreamWriter(filePath))
                    {
                        // Write lines to the file.
                        writer.WriteLine("%YAML 1.2");
                        writer.WriteLine("---");
                        writer.WriteLine("lang: en-US");
                        writer.WriteLine();
                        writer.WriteLine("type: structure");
                        writer.WriteLine();
                        writer.WriteLine("uri: " + schema.Uri);
                        writer.WriteLine();
                        writer.WriteLine("standard tag: '" + schema.StandardTag + "'");
                        writer.WriteLine();

                        writer.WriteLine("specification:");
                        foreach (var line in schema.Specification)
                        {
                            writer.WriteLine(line);
                        }
                        writer.WriteLine();

                        if (schema.Label != null)
                        {
                            writer.WriteLine("label: '" + schema.Label + "'");
                            writer.WriteLine();
                        }

                        // Payload.
                        writer.Write("payload: ");
                        if (string.IsNullOrEmpty(schema.Payload))
                        {
                            writer.WriteLine("null");
                        }
                        else if (schema.Payload == "[Y|<NULL>]")
                        {
                            writer.WriteLine("Y|<NULL>");
                        }
                        else if (schema.Payload.StartsWith("@<XREF:"))
                        {
                            string recordType = schema.Payload.Substring(7).Trim('@', '>');
                            writer.WriteLine("\"@<https://gedcom.io/terms/v5.5.1/record-" + recordType + ">@\"");
                        }
                        else if (schema.Payload.StartsWith("[@<XREF:") && schema.Payload.EndsWith(">@|<NULL>]"))
                        {
                            string recordType = schema.Payload.Substring(8, schema.Payload.Length - 18);
                            writer.WriteLine("\"@<https://gedcom.io/terms/v5.5.1/record-" + recordType + ">@|<NULL>\"");
                        }
                        else if (schema.Payload.StartsWith("[<") && schema.Payload.EndsWith(">|<NULL>]"))
                        {
                            string payloadType = schema.Payload.Substring(2, schema.Payload.Length - 11);
                            writer.WriteLine("https://gedcom.io/terms/v5.5.1/type-" + payloadType);
                        }
                        else if (schema.Payload.Contains('@') || schema.Payload.Contains('|'))
                        {
                            throw new Exception("Bad payload");
                        }
                        else
                        {
                            writer.WriteLine("https://gedcom.io/terms/v5.5.1/type-" + schema.Payload);
                        }
                        writer.WriteLine();

                        // Substructures.
                        writer.Write("substructures:");
                        if (schema.Substructures.Count == 0)
                        {
                            writer.WriteLine(" {}");
                        }
                        else
                        {
                            writer.WriteLine();
                            List<string> sortedKeys = schema.Substructures.Keys.OrderBy(key => key).ToList();
                            foreach (var key in sortedKeys)
                            {
                                GedcomStructureCountInfo subInfo = schema.Substructures[key];
                                writer.WriteLine($"  \"{key}\": \"{subInfo}\"");
                            }
                        }
                        writer.WriteLine();

                        // Superstructures.
                        writer.Write("superstructures:");
                        if (schema.Superstructures.Count == 0)
                        {
                            writer.WriteLine(" {}");
                        }
                        else
                        {
                            writer.WriteLine();
                            List<string> sortedKeys = schema.Superstructures.Keys.OrderBy(key => key).ToList();
                            foreach (var key in sortedKeys)
                            {
                                GedcomStructureCountInfo superInfo = schema.Superstructures[key];
                                writer.WriteLine($"  \"{key}\": \"{superInfo}\"");
                            }
                        }
                        writer.WriteLine();

                        writer.WriteLine("contact: https://gedcom.io/community/");
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
        /// Once everything is known, look up a structure schema by URI.
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public static GedcomStructureSchema GetFinalSchemaByUri(string uri)
        {
            var list = s_StructureSchemas.Values.Where(s => s.Uri == uri).ToList();
            Debug.Assert(list.Count == 1);
            return list.First();
        }

        /// <summary>
        /// Get a GEDCOM structure schema.
        /// </summary>
        /// <param name="sourceProgram">source program string, or null for wildcard</param>
        /// <param name="superstructureUri">superstructure URI, or null for wildcard</param>
        /// <param name="tag"></param>
        /// <returns>Schema</returns>
        public static GedcomStructureSchema GetSchema(string sourceProgram, string superstructureUri, string tag)
        {
            if (tag.Contains('|') || tag.Contains('['))
            {
                throw new Exception();
            }

            // First look for a schema with a wildcard source program.
            var structureSchemaKey = new GedcomStructureSchemaKey(tag, GedcomStructureSchemaKey.Wildcard, superstructureUri);
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
            structureSchemaKey.SuperstructureUri = GedcomStructureSchemaKey.Wildcard;
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

        private static bool IsSubset(Dictionary<string, GedcomStructureCountInfo> small, Dictionary<string, GedcomStructureCountInfo> large)
        {
            foreach (string key in small.Keys)
            {
                if (!large.ContainsKey(key))
                {
                    return false;
                }
                if (small[key].ToString() != large[key].ToString())
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Test whether two schemas match in Payload and Substructures.
        /// </summary>
        /// <param name="a">First schema to test</param>
        /// <param name="b">Second schema to test</param>
        /// <returns>true if match, false if not</returns>
        private static bool MatchSchema(GedcomStructureSchema a, GedcomStructureSchema b)
        {
            if (a.Payload != b.Payload)
            {
                return false;
            }

            if (!IsSubset(a.Substructures, b.Substructures))
            {
                return false;
            }

            if (!IsSubset(b.Substructures, a.Substructures))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Create a list of all unique tags in s_StructureSchemas.
        /// </summary>
        /// <returns>List of tags</returns>
        private static List<string> GetAllUniqueTags()
        {
            List<string> tags = new List<string>();
            foreach (var key in s_StructureSchemas.Keys)
            {
                if (!tags.Contains(key.Tag))
                {
                    tags.Add(key.Tag);
                }
            }
            return tags;
        }

        /// <summary>
        /// Collapse schemas into unique schemas by combining matching schemas with one
        /// superstructure each into a single schema with multiple superstructures.
        /// </summary>
        public static void CollapseSchemas()
        {
            List<string> tags = GetAllUniqueTags();

            var newStructureSchemas = new Dictionary<GedcomStructureSchemaKey, GedcomStructureSchema>();
            foreach (string tag in tags)
            {
                // Look for all unique schemas for the tag.
                var uniqueSchemasForTag = new Dictionary<GedcomStructureSchema, List<GedcomStructureSchema>>();
                var allSchemasForTag = new Dictionary<GedcomStructureSchemaKey, GedcomStructureSchema>();
                foreach (var pair in s_StructureSchemas)
                {
                    if (pair.Key.Tag != tag)
                    {
                        continue;
                    }
                    GedcomStructureSchema schema = pair.Value;
                    allSchemasForTag[pair.Key] = pair.Value;

                    // See if an entry exists in uniqueSchemas with the same
                    // payload and substructures.
                    bool found = false;
                    foreach (var unique in uniqueSchemasForTag)
                    {
                        if (MatchSchema(unique.Key, schema))
                        {
                            // Add matching schema to the unique schema's list of matching schemas.
                            uniqueSchemasForTag[unique.Key].Add(schema);
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        if (!uniqueSchemasForTag.ContainsKey(schema))
                        {
                            uniqueSchemasForTag[schema] = new List<GedcomStructureSchema>();
                        }
                        uniqueSchemasForTag[schema].Add(schema);
                    }
                }

                var newKeys = new List<GedcomStructureSchemaKey>();
                foreach (var pair in uniqueSchemasForTag)
                {
                    if (pair.Key.Superstructures.Count == 0)
                    {
                        // Don't wildcard records.
                        continue;
                    }
                    int pairCount = pair.Value.Count;
                    if (pairCount < 2)
                    {
                        continue;
                    }

                    // Add this schema to the new keys.  First use the payload
                    // since there might be multiple payloads with multiple schemas
                    // that differ by payload.
                    GedcomStructureSchema? bestSchema = pair.Key;
                    var key = new GedcomStructureSchemaKey(tag, GedcomStructureSchemaKey.Wildcard, GedcomStructureSchemaKey.Wildcard);
                    key.Payload = bestSchema.Payload;
                    bestSchema.Uri = MakeUri(GedcomStructureSchemaKey.Wildcard, tag, key.Payload);
                    newStructureSchemas[key] = bestSchema;

                    if (newKeys.Contains(key))
                    {
                        // TODO(issue #3)
                        // throw new Exception(); // duplicate, should never happen
                    }
                    newKeys.Add(key);
                }
                if (newKeys.Count == 1)
                {
                    // We only found one schema key with multiple schemas,
                    // so wildcard the payload.
                    GedcomStructureSchemaKey key = newKeys[0];
                    GedcomStructureSchema schema = newStructureSchemas[key];
                    key.Payload = GedcomStructureSchemaKey.Wildcard;
                    schema.Uri = MakeUri(GedcomStructureSchemaKey.Wildcard, tag, key.Payload);
                }

                foreach (var pair in allSchemasForTag)
                {
                    GedcomStructureSchema current = pair.Value;

                    GedcomStructureSchemaKey? found = FindMatchingKey(pair.Key, newKeys, current.Payload);
                    if (current.Superstructures.Count > 0 && found.HasValue &&
                        IsSubset(newStructureSchemas[found.Value].Substructures, current.Substructures) &&
                        IsSubset(current.Substructures, newStructureSchemas[found.Value].Substructures))
                    {
                        if (found.Value.Payload != null && found.Value.Payload != current.Payload)
                        {
                            throw new Exception();
                        }

                        // This schema matches a common schema for the tag so merge it.
                        foreach (var super in current.Superstructures)
                        {
                            newStructureSchemas[found.Value].Superstructures[super.Key] = super.Value;
                        }
                        continue;
                    }

                    // Copy it since it doesn't match any best schema for the tag.
                    if (allSchemasForTag.Count > 1)
                    {
                        current.Uri = MakeUri(pair.Key.SuperstructureUri, tag, GedcomStructureSchemaKey.Wildcard);
                    }
                    newStructureSchemas[pair.Key] = current;
                }
            }

            s_StructureSchemas = newStructureSchemas;
        }

        /// <summary>
        /// Try to match a key in newkeys, taking wildcards into account.
        /// </summary>
        /// <param name="key">Key to look for a match for</param>
        /// <param name="newKeys">List of keys to look for a match in</param>
        /// <param name="currentPayload">Payload associated with key</param>
        /// <returns>Matched key, or null if none</returns>
        private static GedcomStructureSchemaKey? FindMatchingKey(GedcomStructureSchemaKey key, List<GedcomStructureSchemaKey> newKeys, string currentPayload)
        {
            foreach (GedcomStructureSchemaKey newKey in newKeys)
            {
                if (key.Tag != newKey.Tag)
                {
                    // Different tag.
                    continue;
                }
                if (key.SourceProgram != GedcomStructureSchemaKey.Wildcard &&
                    newKey.SourceProgram != GedcomStructureSchemaKey.Wildcard &&
                    key.SourceProgram != newKey.SourceProgram)
                {
                    // Different source program.
                    continue;
                }
                if (newKey.Payload != GedcomStructureSchemaKey.Wildcard &&
                    currentPayload != newKey.Payload)
                {
                    // Different payload.
                    continue;
                }
                if (key.SuperstructureUri != GedcomStructureSchemaKey.Wildcard &&
                    newKey.SuperstructureUri != GedcomStructureSchemaKey.Wildcard &&
                    key.SuperstructureUri != newKey.SuperstructureUri)
                {
                    // Different superstructure URI.
                    continue;
                }
                return newKey;
            }
            return null;
        }

        /// <summary>
        /// Construct what the correct URI for a schema should be, once all schemas are known.
        /// </summary>
        /// <returns>Correct URI for a schema</returns>
        private string ConstructFinalUri()
        {
            string tag = this.StandardTag;

            // Case 1: HEAD, TRLR.
            if (tag == "TRLR" || tag == "HEAD")
            {
                return UriPrefix + tag;
            }

            // Case 2: Other records.
            if (this.Superstructures.Count == 0)
            {
                return UriPrefix + "record-" + tag;
            }

            // Case 3: When there is only one non-record schema with the tag.
            var schemas = s_StructureSchemas.Values.Where(s => s.StandardTag == tag && s.Superstructures.Count == 0).ToList();
            if (schemas.Count == 1)
            {
                return UriPrefix + tag;
            }

            // Case 4: If there is only one superstructure.
            if (this.Superstructures.Count == 1)
            {
                string super = this.Superstructures.First().Key.Substring(UriPrefix.Length);
                return UriPrefix + super + "-" + tag;
            }

            // Case 5: When the payload type is needed to disambiguate.
            // This case never occurs with FamilySearch GEDCOM 7.0,
            // but does occur with earlier versions of GEDCOM.
            return GenerateTagPayloadUri(tag, Payload);
        }

        public static void VerifyFinalUris()
        {
            foreach (GedcomStructureSchema schema in s_StructureSchemas.Values)
            {
                string finalUri = schema.ConstructFinalUri();
                Debug.Assert(finalUri == schema.Uri);
            }
        }

        public static readonly string UriPrefix = "https://gedcom.io/terms/v5.5.1/";

        private static string GenerateTagPayloadUri(string tag, string payload)
        {
            Debug.Assert(payload != GedcomStructureSchemaKey.Wildcard);

            string trimmedPayload = (payload == null) ? "NULL" : payload;
            trimmedPayload = trimmedPayload.Trim('@', '[', '<', '>', ']');
            trimmedPayload = trimmedPayload.Replace(">|<", "_OR_");
            trimmedPayload = trimmedPayload.Replace(':', '_');

            return UriPrefix + tag + "-" + trimmedPayload;
        }

        /// <summary>
        /// Construct a uri given a set of schema fields.
        /// </summary>
        /// <param name="superstructureUri">Superstructure URI, or "*" for wildcard</param>
        /// <param name="tag">Tag</param>
        /// <param name="payload">Payload type (short form, not URI), or "*" for wildcard</param>
        /// <returns>URI</returns>
        public static string MakeUri(string superstructureUri, string tag, string? payload)
        {
            Debug.Assert(superstructureUri != null);

            if (tag == "TRLR" || tag == "HEAD")
            {
                return UriPrefix + tag;
            }
            if (superstructureUri == "-")
            {
                return UriPrefix + "record-" + tag;
            }
            string suffix = tag;
            if (payload != GedcomStructureSchemaKey.Wildcard)
            {
                string trimmedPayload = (payload == null) ? "NULL" : payload;
                trimmedPayload = trimmedPayload.Trim('@', '[', '<', '>', ']');
                trimmedPayload = trimmedPayload.Replace(">|<", "_OR_");
                trimmedPayload = trimmedPayload.Replace(':', '_');
                suffix += "-" + trimmedPayload;
            }
            if (superstructureUri == GedcomStructureSchemaKey.Wildcard)
            {
                return UriPrefix + suffix;
            }

            // Get rightmost component.
            string[] tokens = superstructureUri.Split('/');
            string super = tokens[tokens.Length - 1];
            if (super.StartsWith("record-")) {
                super = super.Substring(7);
            }
            return UriPrefix + super + "-" + suffix;
        }
    }
}

