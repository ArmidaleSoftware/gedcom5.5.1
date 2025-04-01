// Copyright (c) Armidale Software
// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Gedcom551.Construct
{
    public enum SpecSection
    {
        None = 0,
        PrimitiveElements,
        AppendixA,
        Done,
    }

    public class GedcomFileSchema
    {
        Dictionary<string, SymbolDefinition> symbols = new Dictionary<string,SymbolDefinition>();
        SymbolDefinition? currentSymbolDefinition = null;
        
        void ProcessInputLine(string line)
        {
            if (currentSymbolDefinition == null && !line.StartsWith('~'))
            {
                // Skip any lines before the first symbol definition.
                return;
            }
            if (line == string.Empty)
            {
                // Skip any blank lines.
                return;
            }
            if (line.StartsWith('~') && line.EndsWith(":="))
            {
                // Start a new symbol definition.
                string symbol = line.Substring(1, line.Length - 3);
                currentSymbolDefinition = new SymbolDefinition(symbol);
                symbols.Add(symbol, currentSymbolDefinition);
                return;
            }
            if (currentSymbolDefinition == null)
            {
                // This should never happen.
                return;
            }
            if (line == "//0 TRLR {1:1}")
            {
                // Special case: remove the "//" for TRLR
                line = line.Substring(2);
            }
            if (line.StartsWith("//"))
            {
                // Process description text.
                currentSymbolDefinition.Description += line.Substring(2) + "\n";
                return;
            }
            if (line == "[")
            {
                currentSymbolDefinition.Kind = SymbolDefinitionKind.Alternatives;
                return;
            }
            if (line == "|" || line == "]")
            {
                if (currentSymbolDefinition.Kind != SymbolDefinitionKind.Alternatives)
                {
                    throw new Exception("Bad symbol definition");
                }
                return;
            }
            currentSymbolDefinition.AddComponent(line);
        }

        // Constructor
        public GedcomFileSchema(string sourceFile)
        {
            // First read all symbol definitions.
            using (StreamReader sr = new StreamReader(sourceFile))
            {
                string line;
                int lineNumber = 0;
                while ((line = sr.ReadLine()) != null)
                {
                    lineNumber++;
                    line = line.Trim();
                    ProcessInputLine(line);
                }
            }

            // Now resolve all symbol references.
            foreach (var symbol in symbols)
            {
                foreach (SymbolComponent component in symbol.Value.Components)
                {
                    if (component.TagOrSymbolReference.StartsWith("<<") &&
                        component.TagOrSymbolReference.EndsWith(">>"))
                    {
                        string name = component.TagOrSymbolReference.Substring(2, component.TagOrSymbolReference.Length - 4);
                        component.SymbolDefinition = symbols[name];
                    }
                }
            }

            GenerateStructureSchemas(symbols["LINEAGE_LINKED_GEDCOM"]);
            GedcomStructureSchema.PinAndVerifyUniqueUris();
        }

        /// <summary>
        /// Given a symbol, recursively update the schemas database using everything under this symbol.
        /// </summary>
        /// <param name="symbol">Symbol to start from</param>
        /// <param name="baseLevel">Structure level in GEDCOM file</param>
        /// <param name="cardinality">Cardinality string</param>
        /// <exception cref="Exception"></exception>
        void GenerateStructureSchemas(SymbolDefinition symbol, int baseLevel = 0, string? cardinality = null)
        {
            foreach (SymbolComponent component in symbol.Components)
            {
                Console.Write("Generating");
                for (int i = 0; i < baseLevel; i++)
                {
                    if (i > 0)
                    {
                        Console.Write(".");
                    }
                    else
                    {
                        Console.Write(" ");
                    }
                    int count = GedcomStructureSchema.SchemaPath[i].Count;
                    if (count > 1)
                    {
                        Console.Write("{");
                    }
                    for (int j = 0; j < count; j++)
                    {
                        GedcomStructureSchema s = GedcomStructureSchema.SchemaPath[i][j];
                        if (j > 0)
                        {
                            Console.Write("|");
                        }
                        Console.Write(s.StandardTag);
                    }
                    if (count > 1)
                    {
                        Console.Write("}");
                    }
                }
                Console.WriteLine(" " + symbol.ToString() + ": " + component.ToString());

                int combinedLevel = baseLevel + component.LevelDelta;
                if (component.SymbolDefinition != null)
                {
                    if (component.PayloadType != null || component.Id != null)
                    {
                        throw new Exception("oops");
                    }
                    GenerateStructureSchemas(component.SymbolDefinition, combinedLevel, component.Cardinality);
                    continue;
                }

                string input = component.TagOrSymbolReference;
                // Remove '[' from the beginning and ']' from the end if present.
                string trimmedInput = input.Trim('[', ']');
                // Split the string using '|' as the separator
                string[] result = trimmedInput.Split('|');
                GedcomStructureSchema.SchemaPath[combinedLevel] = new List<GedcomStructureSchema>();
                for (int i = combinedLevel + 1; i < GedcomStructureSchema.SchemaPath.Length; i++)
                {
                    GedcomStructureSchema.SchemaPath[i] = null;
                }
                foreach (string tag in result)
                {
                    if (tag == "CONC" || tag == "CONT")
                    {
                        // Ignore this structure.
                        continue;
                    }

                    string effectiveCardinality =  (component.LevelDelta == 0) ? GetEffectiveCardinality(cardinality, component.Cardinality) : component.Cardinality;
                    GedcomStructureSchema schema = GedcomStructureSchema.AddOrUpdateSchema(combinedLevel, tag, component.PayloadType, effectiveCardinality);
                    Debug.Assert(!GedcomStructureSchema.SchemaPath[combinedLevel].Contains(schema));
                    GedcomStructureSchema.SchemaPath[combinedLevel].Add(schema);
                }
            }
        }

        private string GetEffectiveCardinality(string typeCardinality, string structureCardinality)
        {
            if (typeCardinality == "{1:1}")
            {
                return structureCardinality;
            }
            if (structureCardinality == "{1:1}")
            {
                return typeCardinality;
            }
            if (typeCardinality == structureCardinality)
            {
                return typeCardinality;
            }
            if (typeCardinality == "{0:1}" && structureCardinality.StartsWith("{0:"))
            {
                return structureCardinality;
            }
            Debug.Assert(false); // Should never happen.
            return structureCardinality;
        }

        private const string XsdString = "http://www.w3.org/2001/XMLSchema#string";
        private const string XsdNonNegativeInteger = "http://www.w3.org/2001/XMLSchema#nonNegativeInteger";

        private static void AddTypeSpecificationLine(string payload, string line)
        {
            var schemas = GedcomStructureSchema.GetAllSchemasForPayload(payload);

            // TODO: handle nested references to types.
            // Debug.Assert(schemas.Count > 0);

            foreach (GedcomStructureSchema schema in schemas)
            {
                schema.TypeSpecification.Add(line);

                string primitiveType = schema.HasIntegerPayloadType ? XsdNonNegativeInteger : XsdString;

                // Don't change the original payload since there may be more
                // specification lines to add yet.
                schema.ActualPayload = schema.HasComplexPayloadType ? schema.OriginalPayload : primitiveType;
                // TODO: try to combine schemas after changing payload.
                // Maybe we do this after changing lastPayload away from this time?
            }
        }

        public static void ParseSpecFile(string specFilename)
        {
            SpecSection currentSection = SpecSection.None;
            string tagPattern = @"(\w+)\s+\{([\w-]+)\}:=";
            string typePattern = @"(\w+):=\s*{Size=(\d+):(\d+)}";
            using (StreamReader sr = new StreamReader(specFilename))
            {
                string line;
                int lineNumber = 0;
                string lastTag = string.Empty;
                string lastPayload = string.Empty;
                int lastMin = 0;
                int lastMax = 0;
                while ((line = sr.ReadLine()) != null)
                {
                    lineNumber++;
                    line = line.Trim();

                    if (line.StartsWith("Appendix A"))
                    {
                        currentSection = SpecSection.AppendixA;
                        continue;
                    }

                    if (line.StartsWith("Primitive Elements of the Lineage-Linked Form"))
                    {
                        currentSection = SpecSection.PrimitiveElements;
                        continue;
                    }

                    // See if we're done.
                    if (line.StartsWith("Appendix B Latter-day Saints Temple Codes"))
                    {
                        lastTag = string.Empty;
                        currentSection = SpecSection.Done;
                        break;
                    }

                    if (currentSection == SpecSection.PrimitiveElements)
                    {
                        Match match = Regex.Match(line, typePattern);
                        if (match.Success)
                        {
                            string payload = match.Groups[1].Value;
                            string min = match.Groups[2].Value;
                            string max = match.Groups[3].Value;
                            lastMin = int.Parse(min);
                            lastMax = int.Parse(max);
                            lastPayload = payload;

                            int i = line.IndexOf('[');
                            if (i >= 0)
                            {
                                string specLine = line.Substring(i);
                                AddTypeSpecificationLine(payload, specLine);
                            }
                        }
                        else if (lastPayload != string.Empty)
                        {
                            AddTypeSpecificationLine(lastPayload, line);
                        }
                    }

                    if (currentSection == SpecSection.AppendixA)
                    {
                        Match match = Regex.Match(line, tagPattern);
                        if (match.Success)
                        {
                            string tag = match.Groups[1].Value;
                            string longname = match.Groups[2].Value;
                            if (tag == "EMAI")
                            {
                                // Work around https://github.com/FamilySearch/GEDCOM/issues/583
                                tag = "EMAIL";
                            }
                            lastTag = tag;

                            var schemas = GedcomStructureSchema.GetAllSchemasForTag(lastTag);
                            // TODO: handle CONT.
                            Debug.Assert(lastTag == "CONC" || lastTag == "CONT" || schemas.Count > 0);

                            foreach (GedcomStructureSchema schema in schemas)
                            {
                                string label = longname.Replace('_', ' ').ToLower();
                                string label2 = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(label);
                                string label3 = label2.Replace("Lds", "LDS");
                                label3 = label3.Replace("Gedcom", "GEDCOM");
                                label3 = label3.Replace("Afn", "AFN");
                                schema.Label = label3;
                            }
                        }
                        else if (lastTag != string.Empty && !string.IsNullOrEmpty(line))
                        {
                            string value = line;
                            var schemas = GedcomStructureSchema.GetAllSchemasForTag(lastTag);
                            foreach (GedcomStructureSchema schema in schemas)
                            {
                                schema.TagSpecification.Add(value);
                            }
                        }
                    }
                }
            }
        }

        public void GenerateOutput(string destinationDirectory)
        {
            // Generate YAML files for standard structures.
            GedcomStructureSchema.SaveAll(destinationDirectory);
        }
    }
}
