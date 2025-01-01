using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using Gedcom551;

namespace Tests
{
    [TestClass]
    public class SchemaTests
    {
        private GedcomFileSchema fileSchema;

        public SchemaTests()
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

        private GedcomStructureSchema VerifyPseudoRecord(string tag)
        {
            GedcomStructureSchema schema = GedcomStructureSchema.GetFinalSchemaByUri(GedcomStructureSchema.UriPrefix + tag);
            Debug.Assert(schema.StandardTag == tag);
            Debug.Assert(schema.Superstructures.Count == 0);
            return schema;
        }

        [TestMethod]
        public void VerifyPseudoRecords()
        {
            VerifyPseudoRecord("HEAD");
            VerifyPseudoRecord("TRLR");
        }

        private GedcomStructureSchema VerifyRecord(string tag)
        {
            GedcomStructureSchema schema = GedcomStructureSchema.GetFinalSchemaByUri(GedcomStructureSchema.UriPrefix + "record-" + tag);
            Debug.Assert(schema.StandardTag == tag);
            Debug.Assert(schema.Superstructures.Count == 0);
            return schema;
        }

        [TestMethod]
        public void VerifyRecords()
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

        [TestMethod]
        public void VerifySomeUniqueTags()
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

            string superUri = schema.Superstructures.First().Key;
            Debug.Assert(superUri == GedcomStructureSchema.UriPrefix + super ||
                         superUri == GedcomStructureSchema.UriPrefix + "record-" + super);
            return schema;
        }

        [TestMethod]
        public void VerifySomeQualifiedTags()
        {
            VerifyQualifiedTag("HEAD", "SOUR");
        }

        private GedcomStructureSchema VerifyPayloadTag(string tag, string payload)
        {
            GedcomStructureSchema schema = GedcomStructureSchema.GetFinalSchemaByUri(GedcomStructureSchema.UriPrefix + tag + "-" + payload);
            Debug.Assert(schema.StandardTag == tag);
            Debug.Assert(schema.Superstructures.Count > 1);
            Debug.Assert(payload == schema.Payload);
            return schema;
        }

        [TestMethod]
        public void VerifySomePayloadTags()
        {
            VerifyPayloadTag("OBJE", "NULL");
            VerifyPayloadTag("OBJE", "XREF_OBJE");
        }
    }
}

