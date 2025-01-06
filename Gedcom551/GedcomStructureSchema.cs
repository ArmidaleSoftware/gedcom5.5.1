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

    public class GedcomStructureSchema
    {
        public static readonly string Wildcard = "*";

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
            this.TagSpecification = new List<string>();
            this.TypeSpecification = new List<string>();
            this.Substructures = new Dictionary<GedcomStructureSchema, GedcomStructureCountInfo>();
            this.Superstructures = new List<GedcomStructureSchema>();
            this.Lang = string.Empty;
            this.Label = string.Empty;
            this.OriginalPayload = string.Empty;
            this.ActualPayload = string.Empty;
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
            return this.StandardTag + " " + ((this.OriginalPayload == null) ? "<NULL>" : this.OriginalPayload);
        }

        public static List<GedcomStructureSchema> GetAllSchemasForTag(string tag) => s_StructureSchemas.Where(s => s.StandardTag == tag).ToList();
        public static List<GedcomStructureSchema> GetAllSchemasForPayload(string payload) => s_StructureSchemas.Where(s => s.OriginalPayload == payload).ToList();


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
        public List<string> TagSpecification { get; private set; }
        public List<string> TypeSpecification { get; private set; }
        public string Label { get; set; }
        public string OriginalPayload { get; set; }
        public string ActualPayload { get; set; }
        public string EnumerationSetUri { get; private set; }
        public EnumerationSet EnumerationSet => EnumerationSet.GetEnumerationSet(EnumerationSetUri);
        public bool HasPointer => (this.OriginalPayload != null) && this.OriginalPayload.StartsWith("@<") && this.OriginalPayload.EndsWith(">@");
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
                    if (schema.OriginalPayload != null)
                    {
                        Console.Write("[" + schema.OriginalPayload + "]");
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
            return s_StructureSchemas.Where(s => s.StandardTag == tag && s.OriginalPayload == payloadType && isRecord == s.IsRecord && s.DoSubstructuresMatch(substructures)).ToList();
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
                    other.OriginalPayload = this.OriginalPayload;
                    other.ActualPayload = this.ActualPayload;
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
                // See if we can now combine the superstructure with any others.
                TryCombineOtherSchemasIntoThis();
            }

            VerifyBackpointers();
        }

        /// <summary>
        /// See if we can combine any other schemas into this one as duplicates.
        /// </summary>
        private void TryCombineOtherSchemasIntoThis()
        {
            List<GedcomStructureSchema> found = FindPossibleSchemas(this.StandardTag, this.OriginalPayload, this.IsRecord, this.Substructures);
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

        private void CollapseOtherSchemaIntoThis(GedcomStructureSchema other)
        {
            Debug.Assert(this != other);
            Debug.Assert(this.OriginalPayload == other.OriginalPayload);
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
                List<GedcomStructureSchema> found = FindPossibleSchemas(super.StandardTag, super.OriginalPayload, super.IsRecord, super.Substructures);
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
            Debug.Assert(payloadType != Wildcard);
            Debug.Assert(sourceProgram != Wildcard);
            if (tag.Contains('|') || tag.Contains('['))
            {
                throw new Exception("Invalid tag");
            }

            // Verify a duplicate doesn't exist.
            List<GedcomStructureSchema> found = FindPossibleSchemas(tag, payloadType, (level == 0), new Dictionary<GedcomStructureSchema, GedcomStructureCountInfo>());
            Debug.Assert(found.Count == 0);

            // Create a new schema to add.
            var schema = new GedcomStructureSchema(sourceProgram, tag);
            schema.OriginalPayload = payloadType;
            schema.ActualPayload = payloadType;
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

        public static void TrimSpecification(List<string> specification)
        {
            // Trim any leading blank lines.
            while (specification.Count > 0 && string.IsNullOrEmpty(specification[0]))
            {
                specification.RemoveAt(0);
            }

            // Trim any trailing blank lines.
            while (specification.Count > 0 && string.IsNullOrEmpty(specification[specification.Count - 1]))
            {
                specification.RemoveAt(specification.Count - 1);
            }
        }

        public static void ShowSpecification(StreamWriter writer, List<string> specification)
        {
            int count = 0;
            List<string> specificationLines = specification.ToList();
            foreach (string line in specificationLines)
            {
                if (string.IsNullOrEmpty(line))
                {
                    count = 0;
                    continue;
                }

                if (count == 0)
                {
                    writer.Write("  - ");
                }
                else
                {
                    writer.Write("    ");
                }

                if (line.EndsWith(':'))
                {
                    writer.WriteLine("\"" + line + "\"");
                }
                else
                {
                    writer.WriteLine(line);
                }
                count++;
            }
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

            foreach (GedcomStructureSchema schema in s_StructureSchemas)
            {
                var serializer = new YamlSerializer();
                string relativeUri = schema.RelativeUri;
                TrimSpecification(schema.TagSpecification);
                TrimSpecification(schema.TypeSpecification);

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

                        writer.Write("specification:");
                        if (schema.TagSpecification.Count + schema.TypeSpecification.Count == 0)
                        {
                            writer.Write(" {}");
                        }
                        writer.WriteLine();
                        ShowSpecification(writer, schema.TagSpecification);
                        ShowSpecification(writer, schema.TypeSpecification);
                        writer.WriteLine();

                        if (!string.IsNullOrEmpty(schema.Label))
                        {
                            writer.WriteLine("label: '" + schema.Label + "'");
                            writer.WriteLine();
                        }

                        // Payload.
                        writer.Write("payload: ");
                        if (string.IsNullOrEmpty(schema.ActualPayload))
                        {
                            writer.WriteLine("null");
                        }
                        else if (schema.ActualPayload == "[Y|<NULL>]")
                        {
                            writer.WriteLine("Y|<NULL>");
                        }
                        else if (schema.ActualPayload.StartsWith("@<XREF:"))
                        {
                            string recordType = schema.ActualPayload.Substring(7).Trim('@', '>');
                            writer.WriteLine("\"@<https://gedcom.io/terms/v5.5.1/record-" + recordType + ">@\"");
                        }
                        else if (schema.ActualPayload.StartsWith("[@<XREF:") && schema.ActualPayload.EndsWith(">@|<NULL>]"))
                        {
                            string recordType = schema.ActualPayload.Substring(8, schema.ActualPayload.Length - 18);
                            writer.WriteLine("\"@<https://gedcom.io/terms/v5.5.1/record-" + recordType + ">@|<NULL>\"");
                        }
                        else if (schema.ActualPayload.StartsWith("[<") && schema.ActualPayload.EndsWith(">|<NULL>]"))
                        {
                            string payloadType = schema.ActualPayload.Substring(2, schema.ActualPayload.Length - 11);
                            writer.WriteLine("https://gedcom.io/terms/v5.5.1/type-" + payloadType);
                        }
                        else if (schema.ActualPayload.Contains('@') || schema.ActualPayload.Contains('|'))
                        {
                            throw new Exception("Bad payload");
                        }
                        else if (!schema.ActualPayload.StartsWith("http"))
                        {
                            writer.WriteLine("https://gedcom.io/terms/v5.5.1/type-" + schema.ActualPayload);
                        }
                        else
                        {
                            writer.WriteLine(schema.ActualPayload);
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

        private string _pinnedFinalRelativeUri = string.Empty;

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
            schemas = s_StructureSchemas.Where(s => s.StandardTag == tag && s.ActualPayload == ActualPayload && !s.IsRecord).ToList();
            if (schemas.Count == 1)
            {
                return GenerateTagPayloadRelativeUri(string.Empty, tag, ActualPayload);
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
            return GenerateTagPayloadRelativeUri(supers, tag, ActualPayload);
        }

        public static readonly string UriPrefix = "https://gedcom.io/terms/v5.5.1/";

        private static string GenerateTagPayloadRelativeUri(string super, string tag, string payload)
        {
            Debug.Assert(payload != Wildcard);

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
    }
}

