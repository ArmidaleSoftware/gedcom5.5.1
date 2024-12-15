using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

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
        }

        static string MakeUri(int level, string tag)
        {
            if (level == 0 && (tag != "TRLR" && tag != "HEAD"))
            {
                return "https://gedcom.io/terms/v5.5.1/record-" + tag;
            }
            return "https://gedcom.io/terms/v5.5.1/" + tag;
        }

        GedcomStructureSchema[] _schemaPath = new GedcomStructureSchema[10];

        void GenerateStructureSchemas(SymbolDefinition symbol, int baseLevel = 0, string? cardinality = null)
        {
            foreach (SymbolComponent component in symbol.Components)
            {
                int combinedLevel = baseLevel + component.LevelDelta;
                if (component.SymbolDefinition != null)
                {
                    if (component.PayloadType != null || component.Id != null)
                    {
                        throw new Exception("oops");
                    }
                    GenerateStructureSchemas(component.SymbolDefinition, component.LevelDelta, component.Cardinality);
                    continue;
                }

                string input = component.TagOrSymbolReference;
                // Remove '[' from the beginning and ']' from the end if present.
                string trimmedInput = input.Trim('[', ']');
                // Split the string using '|' as the separator
                string[] result = trimmedInput.Split('|');
                foreach (string tag in result)
                {
                    if (tag == "CONC" || tag == "CONT")
                    {
                        // Ignore this structure.
                        continue;
                    }
                    string uri = MakeUri(combinedLevel, tag);
                    GedcomStructureSchema schema = GedcomStructureSchema.AddSchema(string.Empty, tag, uri);
                    _schemaPath[combinedLevel] = schema;

                    if ((combinedLevel > 0) && (tag != "CONT"))
                    {
                        GedcomStructureSchema superstructureSchema = _schemaPath[combinedLevel - 1];
                        var info = GedcomStructureCountInfo.FromCardinality(component.Cardinality);
                        superstructureSchema.Substructures[uri] = info;
                        schema.Superstructures[superstructureSchema.Uri] = info;
                    }
                }
            }
        }

        public void GenerateOutput(string destinationDirectory)
        {
            // Generate YAML files for standard structures.
            GedcomStructureSchema.SaveAll(destinationDirectory);
            // XXX
        }
    }
}
