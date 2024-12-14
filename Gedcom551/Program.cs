// Copyright (c) Armidale Software
// SPDX-License-Identifier: MIT

using System;
using System.IO;

namespace Gedcom551
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: Gedcom551Yaml.exe <source file> <destination directory>");
                return;
            }
            string sourceFile = args[0];
            string destinationDirectory = args[1];
            if (!File.Exists(sourceFile))
            {
                Console.WriteLine("File not found: " + sourceFile);
                return;
            }
            if (!Directory.Exists(destinationDirectory))
            {
                Console.WriteLine("Directory not found: " + destinationDirectory);
                return;
            }

            try
            {
                GedcomFileSchema file = new GedcomFileSchema(sourceFile);
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
