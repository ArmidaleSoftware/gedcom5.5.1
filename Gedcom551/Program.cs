// Copyright (c) Armidale Software
// SPDX-License-Identifier: MIT

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
                GedcomFileSchema.ParseSpecFile(specFullPathname);
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
