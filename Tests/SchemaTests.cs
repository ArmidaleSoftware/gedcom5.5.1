using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using Gedcom551;

namespace Tests
{
    [TestClass]

    public class SchemaTests
    {
        private static GedcomFileSchema? fileSchema;

        [ClassInitialize]
        public static void ClassSetup(TestContext context)
        {
            // TODO: don't hard-code path.
            string sourceFile = "C:\\Users\\dthal\\git\\ArmidaleSoftware\\Gedcom551\\input\\ged.5.5.1.txt";
            fileSchema = new GedcomFileSchema(sourceFile);
        }

        // URI design
        // -----------------
        // There are 5 cases:
        // 	1) "TRLR" or "HEAD": "<tag>"
	    //  2) If no superstructures: "record-<tag>"
	    //  3) If there is only one non-record schema with the tag: "<tag>"
        //	4) If there is only one super: "<super>-<tag>"  where <super> is the super URI minus "record-" if present (would this collide in any actual case?)
        //	5) "<tag>-<payload>"

        private GedcomStructureSchema VerifyPseudoStructure(string tag)
        {
            GedcomStructureSchema schema = GedcomStructureSchema.GetFinalSchemaByUri(GedcomStructureSchema.UriPrefix + tag);
            Debug.Assert(schema.StandardTag == tag);
            Debug.Assert(schema.Superstructures.Count == 0);
            return schema;
        }

        /// <summary>
        /// Test pseudo-structures.
        /// </summary>
        [TestMethod]
        public void TestPseudoStructures()
        {
            VerifyPseudoStructure("HEAD");
            VerifyPseudoStructure("TRLR");
            // TODO: VerifyPseudoStructure("CONT");
        }

        private GedcomStructureSchema VerifyRecord(string tag)
        {
            GedcomStructureSchema schema = GedcomStructureSchema.GetFinalSchemaByUri(GedcomStructureSchema.UriPrefix + "record-" + tag);
            Debug.Assert(schema.StandardTag == tag);
            Debug.Assert(schema.Superstructures.Count == 0);
            return schema;
        }

        [TestMethod]
        public void TestRecords()
        {
            VerifyRecord("FAM");
            VerifyRecord("INDI");
            VerifyRecord("NOTE");
            VerifyRecord("OBJE");
            VerifyRecord("REPO");
            VerifyRecord("SOUR");
            VerifyRecord("SUBM");
            VerifyRecord("SUBN");
        }

        private GedcomStructureSchema VerifyUniqueTag(string tag)
        {
            GedcomStructureSchema schema = GedcomStructureSchema.GetFinalSchemaByUri(GedcomStructureSchema.UriPrefix + tag);
            Debug.Assert(schema.StandardTag == tag);
            Debug.Assert(schema.Superstructures.Count > 0);
            return schema;
        }

        /// <summary>
        /// Test cases where the tag is unique across all non-record structures.
        /// </summary>
        [TestMethod]
        public void TestSomeUniqueTags()
        {
            VerifyUniqueTag("CORP");
            VerifyUniqueTag("NPFX");
            VerifyUniqueTag("ORDN");
        }

        private GedcomStructureSchema VerifyQualifiedTag(string super, string tag)
        {
            GedcomStructureSchema schema = GedcomStructureSchema.GetFinalSchemaByUri(GedcomStructureSchema.UriPrefix + super + "-" + tag);
            Debug.Assert(schema.StandardTag == tag);
            Debug.Assert(schema.Superstructures.Count == 1);

            string superstructureUri = schema.Superstructures.First().AbsoluteUri;
            Debug.Assert(superstructureUri == GedcomStructureSchema.UriPrefix + super ||
                         superstructureUri == GedcomStructureSchema.UriPrefix + "record-" + super);
            return schema;
        }

        /// <summary>
        /// Verify some schemas that are unique given a prefix.
        /// </summary>

        [TestMethod]
        public void TestGedcForm()
        {
            VerifyQualifiedTag("GEDC", "FORM");
        }

        [TestMethod]
        public void TestMultimediaRecordFileForm()
        {
            VerifyQualifiedTag("OBJE-FILE", "FORM");
        }

        [TestMethod]
        public void TestMultimediaLinkFileForm()
        {
            VerifyQualifiedTag("OBJE-NULL-FILE", "FORM");
        }

        [TestMethod]
        public void TestHeadSour()
        {
            VerifyQualifiedTag("HEAD", "SOUR");
        }

        [TestMethod]
        public void TestNameFone()
        {
            // TODO: change this to ("NAME", "FONE")
            VerifyQualifiedTag("INDI-NAME", "FONE");
        }

        [TestMethod]
        public void TestPlacFone()
        {
            VerifyQualifiedTag("PLAC-PLACE_NAME", "FONE");
        }

        [TestMethod]
        public void TestHeadFile()
        {
            VerifyQualifiedTag("HEAD", "FILE");
        }

        [TestMethod]
        public void TestObjeFile()
        {
            VerifyQualifiedTag("OBJE", "FILE");
        }

        private GedcomStructureSchema VerifyPayloadTag(string tag, string suffix, string? payload)
        {
            GedcomStructureSchema schema = GedcomStructureSchema.GetFinalSchemaByUri(GedcomStructureSchema.UriPrefix + tag + "-" + suffix);
            Debug.Assert(schema.StandardTag == tag);
            Debug.Assert(schema.Superstructures.Count > 1);
            Debug.Assert(schema.Payload == payload);
            return schema;
        }

        /// <summary>
        /// Verify some structures that just need to be qualified by their payload type.
        /// </summary>

        [TestMethod]
        public void TestObjeNull()
        {
            VerifyPayloadTag("OBJE", "NULL", null);
        }

        [TestMethod]
        public void TestObjeXref()
        {
            VerifyPayloadTag("OBJE", "XREF_OBJE", "@<XREF:OBJE>@");
        }

        [TestMethod]
        public void TestFormPlaceHierarchy()
        {
            VerifyPayloadTag("FORM", "PLACE_HIERARCHY", "PLACE_HIERARCHY");
        }
    }
}

