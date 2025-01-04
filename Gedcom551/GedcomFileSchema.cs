// Copyright (c) Armidale Software
// SPDX-License-Identifier: MIT
using System.Diagnostics;

namespace Gedcom551
{
    public class GedcomFileSchema
    {
        Dictionary<string, SymbolDefinition> symbols = new Dictionary<string,SymbolDefinition>();
        SymbolDefinition? currentSymbolDefinition = null;
        
        void ProcessInputLine(string line)
        {
            if ((currentSymbolDefinition == null) && !line.StartsWith('~'))
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

                    GedcomStructureSchema schema = GedcomStructureSchema.AddOrUpdateSchema(combinedLevel, tag, component.PayloadType, component.Cardinality);
                    Debug.Assert(!GedcomStructureSchema.SchemaPath[combinedLevel].Contains(schema));
                    GedcomStructureSchema.SchemaPath[combinedLevel].Add(schema);
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
