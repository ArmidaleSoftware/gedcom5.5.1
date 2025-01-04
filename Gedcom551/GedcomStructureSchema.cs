// Copyright (c) Armidale Software
// SPDX-License-Identifier: MIT
using System.Data;
using System.Diagnostics;
using System.Yaml.Serialization;

namespace Gedcom551
{
    public struct GedcomStructureCountInfo
    {
        public bool Required; // True: Minimum = 1, False: Minimum = 0.
        public bool Singleton; // True: Maximum = 1, False: Maximum = M.
        public int FixedMax; // 0: no arbitrary max, non-zero: arbitrary max.
        public string Cardinality => "{" + (Required? "1" : "0") + ":" + (Singleton? "1" : (FixedMax > 0 ? FixedMax : "M")) + "}";
        public override string ToString() => Cardinality;
        public bool Matches(GedcomStructureCountInfo other) => (Cardinality == other.Cardinality);
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

        /// <summary>
        /// Unique schema id, for debugging purposes.
        /// </summary>
        private Guid Guid = Guid.NewGuid();

        GedcomStructureSchema(string sourceProgram, string tag)
        {
            this.StandardTag = tag;
            this.Specification = new List<string>();
            this.Substructures = new Dictionary<GedcomStructureSchema, GedcomStructureCountInfo>();
            this.Superstructures = new List<GedcomStructureSchema>();
            this.Lang = string.Empty;
            this.Label = string.Empty;
            this.Payload = string.Empty;
            this.Type = string.Empty;
            this.EnumerationSetUri = string.Empty;
        }

        /// <summary>
        /// Check whether this schema is a standard schema (even if relocated).
        /// </summary>
        public bool IsStandard => (this.StandardTag[0] != '_');
        public bool IsDocumented => (this.RelativeUri != null);

        public override string ToString()
        {
            return this.StandardTag + " " + ((this.Payload == null) ? "<NULL>" : this.Payload);
        }

#if false
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
            this.Substructures = new Dictionary<GedcomStructureSchema, GedcomStructureCountInfo>();
            AddDictionary(this.Substructures, dictionary["substructures"] as Dictionary<object, object>);
            this.Superstructures = new List<GedcomStructureSchema>();
            AddDictionary(this.Superstructures, dictionary["superstructures"] as Dictionary<object, object>);
        }
#endif
        public string Lang { get; private set; }
        public string Type { get; private set; }

        public string RelativeUri => ConstructFinalRelativeUri();
        public string AbsoluteUri => UriPrefix + RelativeUri;
        public string StandardTag { get; private set; }
        public List<string> Specification { get; private set; }
        public string Label { get; private set; }
        public string Payload { get; private set; }
        public string EnumerationSetUri { get; private set; }
        public EnumerationSet EnumerationSet => EnumerationSet.GetEnumerationSet(EnumerationSetUri);
        public bool HasPointer => (this.Payload != null) && this.Payload.StartsWith("@<") && this.Payload.EndsWith(">@");
        public Dictionary<GedcomStructureSchema, GedcomStructureCountInfo> Substructures { get; private set; }
        public List<GedcomStructureSchema> Superstructures { get; private set; }
        public bool IsRecord => (Superstructures.Count == 0);

        static List<GedcomStructureSchema> s_StructureSchemas = new List<GedcomStructureSchema>();
        static Dictionary<string, string> s_StructureSchemaAliases = new System.Collections.Generic.Dictionary<string, string>();
        static Dictionary<string, GedcomStructureSchema> s_StructureSchemasByUri = new Dictionary<string, GedcomStructureSchema>();
        static List<GedcomStructureSchema>[] s_SchemaPath = new List<GedcomStructureSchema>[10];
        public static List<GedcomStructureSchema>[] SchemaPath => s_SchemaPath;

        static void DumpSchemaPath(int max)
        {
            for (int i = 0; i <= max; i++)
            {
                if (SchemaPath[i] == null)
                {
                    continue;
                }
                Console.Write(" " + i.ToString() + ": {");
                for (int j = 0; j < SchemaPath[i].Count; j++)
                {
                    GedcomStructureSchema schema = SchemaPath[i][j];
                    Console.Write(schema.ToString());
                    if (schema.Payload != null)
                    {
                        Console.Write("[" + schema.Payload + "]");
                    }
                    if (j + 1 < SchemaPath[i].Count)
                    {
                        Console.Write(", ");
                    }
                }
                Console.WriteLine("}");
            }
        }

        public const string RecordSuperstructureUri = "TOP";


#if false
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
            s_StructureSchemas.Add(schema);
        }
#endif

        public static void VerifyUniqueUri(GedcomStructureSchema schema)
        {
            List<GedcomStructureSchema> matches = s_StructureSchemas.Where(s => s.RelativeUri == schema.RelativeUri).ToList();
            Debug.Assert(matches.Count == 1);
        }

        /// <summary>
        /// Verify that all URIs are unique.
        /// </summary>
        public static void PinAndVerifyUniqueUris()
        {
            PinFinalUris();
            foreach (GedcomStructureSchema schema in s_StructureSchemas)
            {
                VerifyUniqueUri(schema);
            }
        }

        public static void PinFinalUris()
        {
            foreach (GedcomStructureSchema schema in s_StructureSchemas)
            {
                schema._pinnedFinalRelativeUri = schema.ConstructFinalRelativeUri();
            }

#if false
            // Trim URIs.
            foreach (GedcomStructureSchema schema in s_StructureSchemas)
            {
                string prefix = schema.GetPrefix();
                //schema._pinnedFinalRelativeUri = finalUri;
            }
#endif
        }

        /// <summary>
        /// Sanity check that supers point to subs and vice versa.
        /// </summary>
        private static void VerifyBackpointers()
        {
            foreach (GedcomStructureSchema schema in s_StructureSchemas)
            {
                foreach (GedcomStructureSchema super in schema.Superstructures)
                {
                    Debug.Assert(super.Substructures.Keys.Contains(schema));
                }
                foreach (GedcomStructureSchema sub in schema.Substructures.Keys)
                {
                    Debug.Assert(sub.Superstructures.Contains(schema));
                }
            }
        }

        /// <summary>
        /// Look for an existing schema we could combine with.
        /// </summary>
        /// <param name="tag"></param>
        /// <param name="payloadType"></param>
        /// <param name="isRecord"></param>
        /// <param name="substructures"></param>
        /// <returns></returns>
        private static List<GedcomStructureSchema> FindPossibleSchemas(string tag, string payloadType, bool isRecord, Dictionary<GedcomStructureSchema, GedcomStructureCountInfo> substructures)
        {
            if (tag == "HEAD" || tag == "TRLR")
            {
                return new List<GedcomStructureSchema>();
            }
            return s_StructureSchemas.Where(s => s.StandardTag == tag && s.Payload == payloadType && isRecord == s.IsRecord && s.DoSubstructuresMatch(substructures)).ToList();
        }

        private bool DoSubstructuresMatch(Dictionary<GedcomStructureSchema, GedcomStructureCountInfo> substructures)
        {
            if (substructures.Count != this.Substructures.Count)
            {
                return false;
            }
            foreach (var sub in substructures.Keys)
            {
                if (!this.Substructures.ContainsKey(sub))
                {
                    return false;
                }

                if (!this.Substructures[sub].Matches(substructures[sub]))
                {
                    return false;
                }
            }
            return true;
        }

        private static void DumpStructurePathUris(GedcomStructureSchema schema, string prefix = "")
        {
            Console.WriteLine(prefix + "{" + schema.ToString() + "} " + schema.RelativeUri);
            foreach (GedcomStructureSchema super in schema.Superstructures)
            {
                DumpStructurePathUris(super, "   " + prefix);
            }
        }

        private static void DumpSubstructures(GedcomStructureSchema schema)
        {
            foreach (GedcomStructureSchema sub in schema.Substructures.Keys)
            {
                Console.WriteLine("   {" + sub.ToString() + "} " + sub.RelativeUri);
            }
        }

        /// <summary>
        /// Add a substructure, which might require splitting or collapsing this structure.
        /// </summary>
        /// <param name="level">Level of new substructure</param>
        /// <param name="newSubstructure"></param>
        /// <param name="info">Cardinality info</param>
        /// <param name="repath"Update path as needed</param>
        private void AddSubstructure(int level, GedcomStructureSchema newSubstructure, GedcomStructureCountInfo info, bool repath)
        {
            if (repath)
            {
                var superstructureSubset = (level >= 2) ? SchemaPath[level - 2] : new List<GedcomStructureSchema>();
                var yes = this.Superstructures.Intersect(superstructureSubset).ToList();
                if (yes.Count() != this.Superstructures.Count)
                {
                    // We need to split the superstructure.  This happens when we're adding
                    // a substructure to a schema with multiple superstructures.
                    {
                        Console.WriteLine();
                        Console.Write("Adding sub ");
                        DumpStructurePathUris(newSubstructure);
                        Console.Write("Under this ");
                        DumpStructurePathUris(this);
                        Console.WriteLine("Which already has these substructures:");
                        DumpSubstructures(this);

                        Console.WriteLine();
                        DumpSchemaPath(SchemaPath.Length - 1);
                        Console.WriteLine("Subset of supers of this: ");
                        foreach (GedcomStructureSchema super in superstructureSubset)
                        {
                            Console.WriteLine("   {" + super.ToString() + "}");
                        }
                    }
                    Debug.Assert(yes.Count() > 0);
                    Debug.Assert(this.Superstructures.Count > 0);
                    var no = this.Superstructures.Where(s => !yes.Contains(s)).ToList();
                    var oldInfo = no.First().Substructures[this];

                    // Add a new duplicate schema.
                    var other = new GedcomStructureSchema(string.Empty, this.StandardTag);
                    other.Payload = this.Payload;
                    other.AddSuperstructures(level, oldInfo.Cardinality, no, false);
                    s_StructureSchemas.Add(other);

                    // Remove the other's superstructures from this structure.
                    foreach (GedcomStructureSchema otherSuperstructure in no)
                    {
                        this.Superstructures.Remove(otherSuperstructure);
                        otherSuperstructure.Substructures.Remove(this);
                    }
                    Debug.Assert(yes.Count() == this.Superstructures.Count);
                }
            }

            newSubstructure.Superstructures.Add(this);
            this.Substructures[newSubstructure] = info;

            if (repath)
            {
                // See if we can collapse the superstructure.
                List<GedcomStructureSchema> found = FindPossibleSchemas(this.StandardTag, this.Payload, this.IsRecord, this.Substructures);
                if (found.Count > 1)
                {
                    foreach (GedcomStructureSchema other in found)
                    {
                        // Verify 'other' hasn't already been merged.
                        Debug.Assert(s_StructureSchemas.Contains(other));

                        if (other != this)
                        {
                            CollapseOtherSchemaIntoThis(other);
                        }
                    }
                }
            }

            VerifyBackpointers();
        }

        private void CollapseOtherSchemaIntoThis(GedcomStructureSchema other)
        {
            Debug.Assert(this != other);
            Debug.Assert(this.Payload == other.Payload);
            Debug.Assert(this.StandardTag == other.StandardTag);
            Debug.Assert(this.Superstructures.Any() == other.Superstructures.Any());
            Debug.Assert(this.Substructures.Intersect(other.Substructures).Count() == this.Substructures.Count());

            // Move all supers from other to this.
            List<GedcomStructureSchema> supersToMove = other.Superstructures.ToList();
            foreach (GedcomStructureSchema super in supersToMove)
            {
                GedcomStructureCountInfo info = super.Substructures[other];

                other.Superstructures.Remove(super);
                super.Substructures.Remove(other);

                this.Superstructures.Add(super);
                super.Substructures[this] = info;
            }

            // Move all subs from other to this.
            var subsToMove = other.Substructures.ToList();
            foreach (var sub in subsToMove)
            {
                if (!this.Substructures.Contains(sub))
                {
                    this.Substructures[sub.Key] = sub.Value;
                    sub.Key.Superstructures.Add(this);
                }
                other.Substructures.Remove(sub.Key);
                sub.Key.Superstructures.Remove(other);
            }

            // Remove other.
            s_StructureSchemas.Remove(other);
            for (int i = 0; i < SchemaPath.Length; i++)
            {
                if (SchemaPath[i] == null)
                {
                    continue;
                }
                if (SchemaPath[i].Contains(other))
                {
                    SchemaPath[i].Remove(other);
                    SchemaPath[i].Add(this);
                }
            }

            VerifyBackpointers();

            // See if we can now merge any supers.
            var superList = this.Superstructures.ToList();
            foreach (var super in superList)
            {
                if (!s_StructureSchemas.Contains(super))
                {
                    // This superstructure was already merged with another one.
                    continue;
                }

                // See if we can collapse the superstructure.
                List<GedcomStructureSchema> found = FindPossibleSchemas(super.StandardTag, super.Payload, super.IsRecord, super.Substructures);
                if (found.Count > 1)
                {
                    foreach (GedcomStructureSchema otherSuper in found)
                    {
                        // Verify 'otherSuper' hasn't already been merged.
                        Debug.Assert(s_StructureSchemas.Contains(otherSuper));

                        if (otherSuper != super)
                        {
                            super.CollapseOtherSchemaIntoThis(otherSuper);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Add a set of superstructures to an existing schema.
        /// </summary>
        /// <param name="level"></param>
        /// <param name="cardinality"></param>
        /// <param name="repath">Update path if needed</param>
        private void AddSuperstructures(int level, string cardinality, List<GedcomStructureSchema> superstructuresToAdd, bool repath)
        {
            if (level == 0)
            {
                // Nothing to do.
                Debug.Assert(superstructuresToAdd.Count == 0);
                return;
            }
            var info = GedcomStructureCountInfo.FromCardinality(cardinality);
            foreach (GedcomStructureSchema superstructureToAdd in superstructuresToAdd)
            {
                superstructureToAdd.AddSubstructure(level, this, info, repath);
            }
        }

        public static GedcomStructureSchema AddOrUpdateSchema(int level, string tag, string payloadType, string cardinality)
        {
            GedcomStructureSchema schema;
            List<GedcomStructureSchema> superstructures = (level >= 1) ? SchemaPath[level - 1] : new List<GedcomStructureSchema>();

            List<GedcomStructureSchema> found = FindPossibleSchemas(tag, payloadType, (superstructures.Count == 0), new Dictionary<GedcomStructureSchema, GedcomStructureCountInfo>());
            if (found.Count == 0)
            {
                schema = AddSchema(level, string.Empty, tag, payloadType, cardinality);
            }
            else
            {
                Debug.Assert(found.Count == 1);
                schema = found.First();

                if (tag != "CONT")
                {
                    schema.AddSuperstructures(level, cardinality, SchemaPath[level - 1], true);
                }
            }

            VerifyBackpointers();
            return schema;
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
        /// <param name="payloadType">Payload type</param>
        /// <param name="cardinality">Cardinality</param>
        private static GedcomStructureSchema AddSchema(int level, string sourceProgram, string tag, string payloadType, string cardinality)
        {
            Debug.Assert(payloadType != GedcomStructureSchemaKey.Wildcard);
            Debug.Assert(sourceProgram != GedcomStructureSchemaKey.Wildcard);
            if (tag.Contains('|') || tag.Contains('['))
            {
                throw new Exception("Invalid tag");
            }

            // Verify a duplicate doesn't exist.
            List<GedcomStructureSchema> found = FindPossibleSchemas(tag, payloadType, (level == 0), new Dictionary<GedcomStructureSchema, GedcomStructureCountInfo>());
            Debug.Assert(found.Count == 0);

            // Create a new schema to add.
            var schema = new GedcomStructureSchema(sourceProgram, tag);
            schema.Payload = payloadType;
            List<GedcomStructureSchema> superstructures = (level >= 1) ? SchemaPath[level - 1] : new List<GedcomStructureSchema>();
            s_StructureSchemas.Add(schema);
            schema.AddSuperstructures(level, cardinality, superstructures, true);
            return schema;
        }

#if false
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
                if (schema.IsRecord)
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
#endif

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

            foreach (GedcomStructureSchema schema in s_StructureSchemas)
            {
                var serializer = new YamlSerializer();
                string relativeUri = schema.RelativeUri;

                string filePath = Path.Combine(path, relativeUri + ".yaml");
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
                        writer.WriteLine("uri: " + schema.AbsoluteUri);
                        writer.WriteLine();
                        writer.WriteLine("standard tag: '" + schema.StandardTag + "'");
                        writer.WriteLine();

                        writer.WriteLine("specification:");
                        foreach (var line in schema.Specification)
                        {
                            writer.WriteLine(line);
                        }
                        writer.WriteLine();

                        if (!string.IsNullOrEmpty(schema.Label))
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
                            List<GedcomStructureSchema> sortedKeys = schema.Substructures.Keys.OrderBy(key => key.AbsoluteUri).ToList();
                            foreach (GedcomStructureSchema key in sortedKeys)
                            {
                                GedcomStructureCountInfo subInfo = schema.Substructures[key];
                                writer.WriteLine($"  \"{key.AbsoluteUri}\": \"{subInfo}\"");
                            }
                        }
                        writer.WriteLine();

                        // Superstructures.
                        writer.Write("superstructures:");
                        if (schema.IsRecord)
                        {
                            writer.WriteLine(" {}");
                        }
                        else
                        {
                            writer.WriteLine();
                            List<GedcomStructureSchema> sortedKeys = schema.Superstructures.OrderBy(key => key.AbsoluteUri).ToList();
                            foreach (GedcomStructureSchema key in sortedKeys)
                            {
                                GedcomStructureCountInfo superInfo = key.Substructures[schema];
                                writer.WriteLine($"  \"{key.AbsoluteUri}\": \"{superInfo}\"");
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
            var list = s_StructureSchemas.Where(s => s.AbsoluteUri == uri).ToList();
            Debug.Assert(list.Count == 1);
            return list.First();
        }

#if false
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
#endif

        private static bool IsSubset(Dictionary<GedcomStructureSchema, GedcomStructureCountInfo> small, Dictionary<GedcomStructureSchema, GedcomStructureCountInfo> large)
        {
            foreach (GedcomStructureSchema key in small.Keys)
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

#if false
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
                    if (pair.Key.IsRecord)
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
                    if (!current.IsRecord && found.HasValue &&
                        IsSubset(newStructureSchemas[found.Value].Substructures, current.Substructures) &&
                        IsSubset(current.Substructures, newStructureSchemas[found.Value].Substructures))
                    {
                        if (found.Value.Payload != null && found.Value.Payload != current.Payload)
                        {
                            throw new Exception();
                        }

                        // This schema matches a common schema for the tag so merge it.
                        foreach (GedcomStructureSchema super in current.Superstructures)
                        {
                            if (!newStructureSchemas[found.Value].Superstructures.Contains(super))
                            {
                                newStructureSchemas[found.Value].Superstructures.Add(super);
                            }
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
#endif

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

        private string _pinnedFinalRelativeUri = string.Empty;

#if false
        private string GetPrefix()
        {
            string super = RelativeUri;
            if (super.StartsWith("record-"))
            {
                super = super.Substring(7);
            }
            Debug.Assert(!super.Contains("-record"));
            return super;
        }

        /// <summary>
        /// Construct the shortest unambiguous prefix among matches.
        /// </summary>
        /// <param name="matches"></param>
        /// <returns></returns>
        private string GetUniquePrefix(List<GedcomStructureSchema> matches)
        {
            GedcomStructureSchema superstructure = this.Superstructures.First();
            string prefix = superstructure.GetPrefix();
            string[] prefixComponents = prefix.Split('-');

            return prefix;
        }
#endif

        /// <summary>
        /// Construct what the correct URI for a schema should be, once all schemas are known.
        /// </summary>
        /// <returns>Correct URI for a schema</returns>
        private string ConstructFinalRelativeUri()
        {
            if (_pinnedFinalRelativeUri != string.Empty)
            {
                return _pinnedFinalRelativeUri;
            }
            string tag = this.StandardTag;

            // Case 1: Pseudo-records.
            if (tag == "TRLR" || tag == "HEAD")
            {
                return tag;
            }

            // Case 2: Actual records.
            if (this.IsRecord)
            {
                return "record-" + tag;
            }

            // Case 3: When there is only one non-record schema with the tag.
            var schemas = s_StructureSchemas.Where(s => (s.StandardTag == tag) && !s.IsRecord).ToList();
            if (schemas.Count == 1)
            {
                return tag;
            }

            // Case 4: If there is only one superstructure, and the tag is sufficient under there.
            if (this.Superstructures.Count == 1)
            {
                GedcomStructureSchema superstructure = this.Superstructures.First();
                var subs = superstructure.Substructures.Keys.Where(s => s.StandardTag == tag).ToList();
                if (subs.Count == 1)
                {
                    string super = superstructure.RelativeUri;
                    if (super.StartsWith("record-"))
                    {
                        super = super.Substring(7);
                    }
                    Debug.Assert(!super.Contains("-record"));
                    return super + "-" + tag;
                }
            }

            // Case 5: When the payload type is needed to disambiguate.
            // This case never occurs with FamilySearch GEDCOM 7.0,
            // but does occur with earlier versions of GEDCOM.
            schemas = s_StructureSchemas.Where(s => s.StandardTag == tag && s.Payload == Payload && !s.IsRecord).ToList();
            if (schemas.Count == 1)
            {
                return GenerateTagPayloadRelativeUri(string.Empty, tag, Payload);
            }

            // Case 6: When above is not sufficient.
            string supers = string.Empty;
            if (this.Superstructures.Count == 1)
            {
                supers = this.Superstructures.First().RelativeUri;
            }
            else if (this.Superstructures.Count < 3)
            {
                supers = "(";
                foreach (var superstructure in this.Superstructures)
                {
                    string token = superstructure.RelativeUri;
                    if (supers == "(")
                    {
                        supers += token;
                    }
                    else
                    {
                        supers += "+" + token;
                    }
                }
                supers += ")";
                Debug.Assert(!supers.Contains("-record"));
            }
            return GenerateTagPayloadRelativeUri(supers, tag, Payload);
        }

        public static readonly string UriPrefix = "https://gedcom.io/terms/v5.5.1/";

        private static string GenerateTagPayloadRelativeUri(string super, string tag, string payload)
        {
            Debug.Assert(payload != GedcomStructureSchemaKey.Wildcard);

            string trimmedPayload = (payload == null) ? "NULL" : payload;
            trimmedPayload = trimmedPayload.Trim('@', '[', '<', '>', ']');
            trimmedPayload = trimmedPayload.Replace(">|<", "_OR_");
            trimmedPayload = trimmedPayload.Replace(':', '_');

            if (super != string.Empty)
            {
                return super + "-" + tag + "-" + trimmedPayload;
            }
            return tag + "-" + trimmedPayload;
        }

        /// <summary>
        /// Construct a uri given a set of schema fields.
        /// </summary>
        /// <param name="superstructureUri">Superstructure URI, or "*" for wildcard</param>
        /// <param name="tag">Tag</param>
        /// <param name="payload">Payload type (short form, not URI), or "*" for wildcard</param>
        /// <returns>URI</returns>
        public static string MakeRelativeUri(string superstructureUri, string tag, string? payload)
        {
            Debug.Assert(superstructureUri != null);

            if (tag == "TRLR" || tag == "HEAD")
            {
                return tag;
            }
            if (superstructureUri == "-")
            {
                return "record-" + tag;
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
                return suffix;
            }

            // Get rightmost component.
            string[] tokens = superstructureUri.Split('/');
            string super = tokens[tokens.Length - 1];
            if (super.StartsWith("record-")) {
                super = super.Substring(7);
            }
            return super + "-" + suffix;
        }
    }
}

