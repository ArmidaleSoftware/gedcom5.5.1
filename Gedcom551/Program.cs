// Copyright (c) Armidale Software
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Gedcom551
{
    public class Program
    {
        public enum SpecSection
        {
            None = 0,
            PrimitiveElements,
            AppendixA,
            Done,
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
                        }
                        else if (lastPayload != string.Empty)
                        {
                            string value = line;
                            var schemas = GedcomStructureSchema.GetAllSchemasForPayload(lastPayload);

                            // TODO: handle nested references to types.
                            // Debug.Assert(schemas.Count > 0);

                            foreach (GedcomStructureSchema schema in schemas)
                            {
                                schema.Specification.Add(value);

                                // Don't change the original payload since there may be more
                                // specification lines to add yet.
                                schema.ActualPayload = "http://www.w3.org/2001/XMLSchema#string";
                                // TODO: try to combine schemas after changing payload.
                                // Maybe we do this after changing lastPayload away from this time?
                            }
                        }
                    }

                    if (currentSection == SpecSection.AppendixA)
                    {
                        Match match = Regex.Match(line, tagPattern);
                        if (match.Success)
                        {
                            string tag = match.Groups[1].Value;
                            string longname = match.Groups[2].Value;
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
                        else if (lastTag != string.Empty)
                        {
                            string value = line;
                            var schemas = GedcomStructureSchema.GetAllSchemasForTag(lastTag);
                            foreach (GedcomStructureSchema schema in schemas)
                            {
                                schema.Specification.Add(value);
                            }
                        }
                    }
                }
            }
        }

        public static void Main(string[] args)
        {
            string schemaFilename = "ged.5.5.1.txt";
            string specFilename = "ged551.txt";

            if (args.Length < 2)
            {
                Console.WriteLine("Usage: Gedcom551Yaml.exe <input directory> <output directory>");
                return;
            }
            string sourceDirectory = args[0];
            string destinationDirectory = args[1];
            string schemaFullPathname = Path.Combine(sourceDirectory, schemaFilename);
            string specFullPathname = Path.Combine(sourceDirectory, specFilename);
            if (!File.Exists(schemaFullPathname))
            {
                Console.WriteLine("File not found: " + schemaFullPathname);
                return;
            }
            if (!File.Exists(specFullPathname))
            {
                Console.WriteLine("File not found: " + specFullPathname);
                return;
            }
            if (!Directory.Exists(destinationDirectory))
            {
                Console.WriteLine("Directory not found: " + destinationDirectory);
                return;
            }

            try
            {
                var file = new GedcomFileSchema(schemaFullPathname);
                ParseSpecFile(specFullPathname);
                file.GenerateOutput(destinationDirectory);
            }
            catch (Exception ex)
            {
                Console.WriteLine("The file could not be read:");
                Console.WriteLine(ex.Message);
            }
        }
    }
}
