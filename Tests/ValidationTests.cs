// Copyright (c) Armidale Software
// SPDX-License-Identifier: MIT
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Gedcom551;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using static System.Net.Mime.MediaTypeNames;

namespace Tests
{
    [TestClass]
    public class Validation551Tests
    {
        private const string TEST_FILES_BASE_PATH = "../../../../external/test-files/5";

        [TestMethod]
        public void LoadStructureSchema()
        {
            GedcomStructureSchema.LoadAll();
            var schema = GedcomStructureSchema.GetSchema(null, GedcomStructureSchema.RecordSuperstructureUri, "HEAD", false);
            Assert.AreEqual(schema?.Uri, "https://gedcom.io/terms/v5.5.1/HEAD");
            schema = GedcomStructureSchema.GetSchema(null, "https://gedcom.io/terms/v5.5.1/SOUR-DATA-EVEN", "DATE", false);
            Assert.AreEqual(schema?.Uri, "https://gedcom.io/terms/v5.5.1/SOUR-DATA-EVEN-DATE");
            schema = GedcomStructureSchema.GetSchema(null, "https://gedcom.io/terms/v5.5.1/HEAD", "DATE", false);
            Assert.AreEqual(schema?.Uri, "https://gedcom.io/terms/v5.5.1/HEAD-DATE");
        }

        public static void ValidateGedcomFile(string path, string[] expected_errors = null)
        {
            var file = new GedcomFile();
            List<string> errors = file.LoadFromPath(path);
            if (errors.Count == 0)
            {
                errors.AddRange(file.Validate());
            }
            Assert.AreEqual(expected_errors?.Length ?? 0, errors.Count());
            if (expected_errors != null)
            {
                var intersect = errors.Intersect(expected_errors);
                Assert.AreEqual(intersect.Count(), errors.Count());
            }
        }

        public static void ValidateGedcomText(string text, string[] expected_errors = null)
        {
            var file = new GedcomFile();
            List<string> errors = file.LoadFromString(text);
            if (errors.Count == 0)
            {
                errors.AddRange(file.Validate());
            }
            Assert.AreEqual(expected_errors?.Length ?? 0, errors.Count());
            if (expected_errors != null)
            {
                var intersect = errors.Intersect(expected_errors);
                Assert.AreEqual(intersect.Count(), errors.Count());
            }
        }



        [TestMethod]
        public void ValidateHeaderAndTrailer()
        {
            // Missing TRLR.
            ValidateGedcomText(@"0 HEAD
1 GEDC
2 VERS 5.5.1
", new string[] { "Missing TRLR record",
"Line 2: GEDC is missing a substructure of type https://gedcom.io/terms/v5.5.1/GEDC-FORM",
"Line 1: HEAD is missing a substructure of type https://gedcom.io/terms/v5.5.1/SUBM",
"Line 1: HEAD is missing a substructure of type https://gedcom.io/terms/v5.5.1/HEAD-SOUR",
"Line 1: HEAD is missing a substructure of type https://gedcom.io/terms/v5.5.1/CHAR"});

            // Missing HEAD.
            ValidateGedcomText("0 TRLR\n", new string[] { "Line 1: HEAD must be the first record" });

            // Minimal551.
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Test
0 TRLR
");

            // Backwards order.
            ValidateGedcomText(@"0 TRLR
0 HEAD
1 GEDC
2 VERS 5.5.1
", new string[] { "Line 1: HEAD must be the first record" });

            // The trailer cannot contain substructures.
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Test
0 TRLR
1 _EXT bad
", new string[] { "Line 10: TRLR must not contain substructures" });

            // Two HEADs.
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Test
0 TRLR
", new string[] { "Line 8: HEAD must be the first record" });

            // Two TRLRs.
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Test
0 TRLR
0 TRLR
", new string[] { "Line 11: Duplicate TRLR record" });

            // No records.
            ValidateGedcomText("", new string[] { "Missing TRLR record" });
        }

        [TestMethod]
        public void ValidateStructureCardinality()
        {
            // Try zero GEDC which should be {1:1}.
            ValidateGedcomText("0 HEAD\n0 TRLR\n", new string[] {
                "Line 1: HEAD is missing a substructure of type https://gedcom.io/terms/v5.5.1/GEDC",
                "Line 1: HEAD is missing a substructure of type https://gedcom.io/terms/v5.5.1/SUBM",
                "Line 1: HEAD is missing a substructure of type https://gedcom.io/terms/v5.5.1/CHAR",
                "Line 1: HEAD is missing a substructure of type https://gedcom.io/terms/v5.5.1/HEAD-SOUR"
            });

            // Try two VERS which should be {1:1}.
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Test
0 TRLR
", new string[] { "Line 4: GEDC does not permit multiple substructures of type https://gedcom.io/terms/v5.5.1/VERS" });

            // Try two SOUR.VERS which should be {0:1}.
            ValidateGedcomText(@"0 HEAD
1 SOUR test
2 VERS 1
2 VERS 2
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Test
0 TRLR
", new string[] { "Line 2: SOUR does not permit multiple substructures of type https://gedcom.io/terms/v5.5.1/VERS" });

            // Try zero FILE which should be {1:M}.
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Test
0 @O1@ OBJE
0 TRLR
", new string[] { "Line 10: OBJE is missing a substructure of type https://gedcom.io/terms/v5.5.1/OBJE-FILE" });

            // Try a COPR at level 0.
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Test
0 @C0@ COPR
0 TRLR
", new string[] { "Line 10: Undocumented standard record" });

            // Try HEAD.PHON.
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
1 PHON
0 @S1@ SUBM
1 NAME Test
0 TRLR
", new string[] { "Line 8: PHON is not a valid substructure of HEAD" });

            // Try a CONT in the wrong place.
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
1 CONT bad
0 @S1@ SUBM
1 NAME Test
0 TRLR
", new string[] { "Line 8: CONT is not a valid substructure of HEAD" });
        }

        [TestMethod]
        public void ValidateSpacing()
        {
            // Leading whitespace is valid prior to 7.0 but not in 7.0.
            ValidateGedcomText(@"0 HEAD
 1 SOUR test
 1 SUBM @S1@
 1 GEDC
  2 VERS 5.5.1
  2 FORM LINEAGE-LINKED
 1 CHAR ASCII
0 @S1@ SUBM
 1 NAME Test
0 TRLR
");

            // Extra space before the tag is not valid.
            ValidateGedcomText(@"0 HEAD
1  SOUR test
1  SUBM @S1@
1  GEDC
2   VERS 5.5.1
2   FORM LINEAGE-LINKED
1  CHAR ASCII
0 @S1@ SUBM
1  NAME Test
0 TRLR
", new string[] {
                "Line 2: Tag must not be empty",
                "Line 3: Tag must not be empty",
                "Line 4: Tag must not be empty",
                "Line 5: Tag must not be empty",
                "Line 6: Tag must not be empty",
                "Line 7: Tag must not be empty",
                "Line 9: Tag must not be empty"
            });

            // Trailing whitespace is not valid.
            ValidateGedcomText(@"0 HEAD
1 GEDC 
2 VERS 5.5.1
0 TRLR
", new string[] { "Line 2: An empty payload is not valid after a space" });
        }

        [TestMethod]
        public void ValidateXref()
        {
            // HEAD pseudo-structure does not allow an xref.
            ValidateGedcomText(@"0 @H1@ HEAD
1 GEDC
2 VERS 5.5.1
0 TRLR
", new string[] { "Line 1: Xref is not valid for HEAD" });
            ValidateGedcomText(@"0 @H1@ HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Test
0 TRLR
", new string[] { "Line 1: Xref is not valid for HEAD" });

            // Test an INDI record without an xref.  The spec says:
            // "Each record to which other structures point must have
            // a cross-reference identifier. A record to which no
            // structures point may have a cross-reference identifier,
            // but does not need to have one. A substructure or pseudo-
            // structure must not have a cross-reference identifier."
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Test
0 INDI
0 TRLR
");

            // TRLR pseudo-structure does not allow an xref.
            ValidateGedcomText(@"0 HEAD
1 GEDC
2 VERS 5.5.1
0 @T1@ TRLR
", new string[] { "Line 4: Xref is not valid for TRLR" });

            // Xref must start with @.
            ValidateGedcomText(@"0 HEAD
1 GEDC
2 VERS 5.5.1
0 I1@ INDI
0 TRLR
", new string[] { "Line 4: Undocumented standard record" });

            // Xref must end with @.
            ValidateGedcomText(@"0 HEAD
1 GEDC
2 VERS 5.5.1
0 @I1 INDI
0 TRLR
", new string[] { "Line 4: Xref must start and end with @" });
            
            // Xref must contain something.
            ValidateGedcomText(@"0 HEAD
1 GEDC
2 VERS 5.5.1
0 @ INDI
0 TRLR
", new string[] { "Line 4: Xref must start and end with @" });

            // Xref must start with @.
            ValidateGedcomText(@"0 HEAD
1 GEDC
2 VERS 5.5.1
0 I1@ INDI
0 TRLR
", new string[] { "Line 4: Undocumented standard record" });

            // Xref must end with @.
            ValidateGedcomText(@"0 HEAD
1 GEDC
2 VERS 5.5.1
0 @I1 INDI
0 TRLR
", new string[] { "Line 4: Xref must start and end with @" });

            // Xref must contain something.
            ValidateGedcomText(@"0 HEAD
1 GEDC
2 VERS 5.5.1
0 @ INDI
0 TRLR
", new string[] { "Line 4: Xref must start and end with @" });

            // Test characters within an xref, which is
            // @<alphanum><pointer_string>@
            // GEDCOM 5.5.1:
            // where pointer_string has (alnum|space|#)
            // and GEDCOM 7.0
            // where pointer_string has (upper|digit|_)

            // Upper case letters and numbers are fine.
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Test
0 @I1@ INDI
0 TRLR
");

            // @VOID@ is ok in GEDCOM 5.5.1 but not 7.0.
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Test
0 @VOID@ INDI
0 TRLR
");

            // Hash is ok in GEDCOM 5.5.1 (except at the start) but not 7.0.
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Test
0 @I#1@ INDI
0 TRLR
");
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Test
0 @#I1@ INDI
0 TRLR
", new string[] { "Line 10: Xref \"@#I1@\" does not start with a letter or digit" });

            // Underscore is ok in GEDCOM 7.0 but not 5.5.1.
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Test
0 @I_1@ INDI
0 TRLR
", new string[] { "Line 10: Invalid character '_' in Xref \"@I_1@\"" });

            // Lower-case letters are ok in GEDCOM 5.5.1 but not 7.0.
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Test
0 @i1@ INDI
0 TRLR
");

            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Test
0 @I1@ INDI
0 @I1@ INDI
0 TRLR
", new string[] { "Line 11: Duplicate Xref @I1@" });
        }

        [TestMethod]
        public void ValidatePayloadType()
        {
            // Validate null.
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC 1
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Test
0 TRLR
", new string[] { "Line 4: GEDC payload must be null" });

            // Validate an integer.
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Test
0 @I1@ INDI
1 NCHI 0
0 TRLR
");
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Test
0 @I1@ INDI
1 NCHI -1
0 TRLR
", new string[] { "Line 11: \"-1\" is not a non-negative integer" });
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Test
0 @I1@ INDI
1 NCHI
0 TRLR
", new string[] { "Line 11: \"\" is not a non-negative integer" });

            // Test Y|<NULL>.
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Test
0 @I1@ INDI
1 BIRT N
0 TRLR
", new string[] { "Line 11: BIRT payload must be 'Y' or empty" });

            // We can't validate "standard" structures
            // under an extension, since they may be
            // ambiguous, such as "NAME" or "HUSB".
            // TODO: We could perhaps try ALL possibilities.
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
1 _UNKNOWN
2 UNKNOWN
0 @S1@ SUBM
1 NAME Test
0 TRLR
");
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @SU1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @SU1@ SUBM
1 NAME Test
0 @U1@ _UNKNOWN
1 SOUR @S1@
0 @S1@ SOUR
1 TITL Title
0 TRLR
");
        }

        private void ValidateValidFilePayload(string value)
        {
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Test
0 @O1@ OBJE
1 FILE " + value + @"
2 FORM bmp
0 TRLR
");
        }

        private void ValidateInvalidFilePayload(string value)
        {
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Test
0 @O1@ OBJE
1 FILE " + value + @"
2 FORM bmp
0 TRLR
", new string[] { "Line 5: \"" + value + "\" is not a valid URI reference" });
        }

        private void ValidateInvalidFormPayload(string value)
        {
            ValidateGedcomText(@"0 HEAD
1 GEDC
2 VERS 5.5.1
0 @O1@ OBJE
1 FILE foo
2 FORM " + value + @"
0 TRLR
", new string[] { "Line 6: \"" + value + "\" is not a valid value for FORM" });
        }

        /// <summary>
        /// Validate enum payload type.
        /// </summary>
        [TestMethod]
        public void ValidateEnumPayloadType()
        {
            // Try a valid enum value.
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Test
0 @I1@ INDI
1 SEX U
0 TRLR
");

            // Try an invalid enum value.
            // TODO: support SEX validation.
#if false
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Test
0 @I1@ INDI
1 SEX UNKNOWN
0 TRLR
", new string[] { "Line 5: \"UNKNOWN\" is not a valid value for SEX" });
#endif

            // Try a valid structure name as an enum value.
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Test
0 @SO1@ SOUR
0 @I1@ INDI
1 SOUR @SO1@
2 EVEN CENS
0 TRLR
");
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Test
0 @SO1@ SOUR
0 @I1@ INDI
1 SOUR @SO1@
2 EVEN ADOP
0 TRLR
");

            // Try an incorrect structure name as an enum value.
            // TODO: validate EVEN payload.
#if false
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Test
0 @SO1@ SOUR
0 @I1@ INDI
1 SOUR @SO1@
2 EVEN FAM
0 TRLR
", new string[] { "Line 13: \"FAM\" is not a valid value for NO" });
#endif

            // Validate List of Enum.
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Test
0 @I1@ INDI
1 RESN confidential
0 TRLR
");
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Test
0 @I1@ INDI
1 RESN CONFIDENTIAL, LOCKED
0 TRLR
");

            // TODO: test invalid values for REST
#if false
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Test
0 @I1@ INDI
1 RESN UNKNOWN
0 TRLR
", new string[] { "Line 5: \"UNKNOWN\" is not a valid value for RESN" });
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Test
0 @I1@ INDI
1 RESN CONFIDENTIAL,
0 TRLR
", new string[] { "Line 5: \"\" is not a valid value for RESN" });
#endif
        }

        private void ValidateInvalidNamePayload(string value)
        {
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Test
0 @I1@ INDI
1 NAME " + value + @"
0 TRLR
", new string[] { "Line 11: \"" + value + "\" is not a valid name" });
        }

        private void ValidateValidNamePayload(string value)
        {
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Test
0 @I1@ INDI
1 NAME " + value + @"
0 TRLR
");
        }

        /// <summary>
        /// Validate Name payload type.
        /// </summary>
        [TestMethod]
        public void ValidateNamePayloadType()
        {
            // Try some valid name values.
            ValidateValidNamePayload("John Smith");
            ValidateValidNamePayload("John /Smith/");
            ValidateValidNamePayload("John /Smith/ Jr.");

            // Try some invalid name values.
            ValidateInvalidNamePayload("/");
            ValidateInvalidNamePayload("a/b/c/d");
            ValidateInvalidNamePayload("a\tb");
        }

        private void ValidateInvalidExactDatePayload(string value)
        {
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
1 DATE " + value + @"
0 @S1@ SUBM
1 NAME Test
0 TRLR
", new string[] { "Line 8: \"" + value + "\" is not a valid exact date" });
        }

        private void ValidateValidExactDatePayload(string value)
        {
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
1 DATE " + value + @"
0 @S1@ SUBM
1 NAME Test
0 TRLR
");
        }

        /// <summary>
        /// Validate exact date payload type.
        /// </summary>
        [TestMethod]
        public void ValidateExactDatePayloadType()
        {
            // Try some valid exact dates.
            ValidateValidExactDatePayload("3 DEC 2023");
            ValidateValidExactDatePayload("03 DEC 2023");

            // Try some invalid exact dates.
            ValidateInvalidExactDatePayload("invalid");
            ValidateInvalidExactDatePayload("3 dec 2023");
            ValidateInvalidExactDatePayload("3 JUNE 2023");
            ValidateInvalidExactDatePayload("DEC 2023");
            ValidateInvalidExactDatePayload("2023");
        }

        private void ValidateInvalidDatePeriodPayload(string value)
        {
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Test
0 @I1@ INDI
1 BIRT
2 DATE " + value + @"
0 TRLR
", new string[] { "Line 12: \"" + value + "\" is not a valid date period" });
        }

        private void ValidateValidDatePeriodPayload(string value)
        {
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Test
0 @I1@ INDI
1 BIRT
2 DATE " + value + @"
0 TRLR
");
        }

        /// <summary>
        /// Validate date period payload type.
        /// </summary>
        [TestMethod]
        public void ValidateDatePeriodPayloadType()
        {
            // Try some valid date period values.
            ValidateValidDatePeriodPayload("TO 3 DEC 2023");
            ValidateValidDatePeriodPayload("TO DEC 2023");
            ValidateValidDatePeriodPayload("TO 2023");
            ValidateValidDatePeriodPayload("TO GREGORIAN 20 BCE");
            ValidateValidDatePeriodPayload("FROM 03 DEC 2023");
            ValidateValidDatePeriodPayload("FROM 2000 TO 2020");
            ValidateValidDatePeriodPayload("FROM MAR 2000 TO JUN 2000");
            ValidateValidDatePeriodPayload("FROM 30 NOV 2000 TO 1 DEC 2000");
            ValidateValidDatePeriodPayload("FROM HEBREW 1 TSH 1");
            ValidateValidDatePeriodPayload("FROM GREGORIAN 20 BCE TO GREGORIAN 12 BCE");

            // TODO: Try some invalid date period values.
            // ValidateInvalidDatePeriodPayload("2023");
            // ValidateInvalidDatePeriodPayload("TO 40 DEC 2023");
            // ValidateInvalidDatePeriodPayload("TO 3 dec 2023");
            // ValidateInvalidDatePeriodPayload("TO 3 JUNE 2023");
            // ValidateInvalidDatePeriodPayload("TO ABC 2023");
            // ValidateInvalidDatePeriodPayload("FROM HEBREW 1 TSH 1 BCE");
        }

        private void ValidateInvalidDateValuePayload(string value)
        {
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Test
0 @I1@ INDI
1 DEAT
2 DATE " + value + @"
0 TRLR
", new string[] { "Line 12: \"" + value + "\" is not a valid date value" });
        }

        private void ValidateValidDateValuePayload(string value)
        {
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Test
0 @I1@ INDI
1 DEAT
2 DATE " + value + @"
0 TRLR
");
        }

        /// <summary>
        /// Validate date value payload type.
        /// </summary>
        [TestMethod]
        public void ValidateDateValuePayloadType()
        {
            // Try some valid dates.
            ValidateValidDateValuePayload("3 DEC 2023");
            ValidateValidDateValuePayload("DEC 2023");
            ValidateValidDateValuePayload("2023");
            ValidateValidDateValuePayload("GREGORIAN 20 BCE");
            ValidateValidDateValuePayload("HEBREW 1 TSH 1");

            // Try some valid date periods.
            ValidateValidDateValuePayload("TO 3 DEC 2023");
            ValidateValidDateValuePayload("TO DEC 2023");
            ValidateValidDateValuePayload("TO 2023");
            ValidateValidDateValuePayload("TO GREGORIAN 20 BCE");
            ValidateValidDateValuePayload("FROM 03 DEC 2023");
            ValidateValidDateValuePayload("FROM 2000 TO 2020");
            ValidateValidDateValuePayload("FROM MAR 2000 TO JUN 2000");
            ValidateValidDateValuePayload("FROM 30 NOV 2000 TO 1 DEC 2000");
            ValidateValidDateValuePayload("FROM HEBREW 1 TSH 1");
            ValidateValidDateValuePayload("FROM GREGORIAN 20 BCE TO GREGORIAN 12 BCE");

            // Try some valid date ranges.
            ValidateValidDateValuePayload("BEF 3 DEC 2023");
            ValidateValidDateValuePayload("BEF DEC 2023");
            ValidateValidDateValuePayload("BEF 2023");
            ValidateValidDateValuePayload("BEF GREGORIAN 20 BCE");
            ValidateValidDateValuePayload("AFT 03 DEC 2023");
            ValidateValidDateValuePayload("AFT HEBREW 1 TSH 1");
            ValidateValidDateValuePayload("BET 2000 AND 2020");
            ValidateValidDateValuePayload("BET MAR 2000 AND JUN 2000");
            ValidateValidDateValuePayload("BET 30 NOV 2000 AND 1 DEC 2000");
            ValidateValidDateValuePayload("BET GREGORIAN 20 BCE AND GREGORIAN 12 BCE");

            // Try some valid approximate dates.
            ValidateValidDateValuePayload("ABT 3 DEC 2023");
            ValidateValidDateValuePayload("CAL DEC 2023");
            ValidateValidDateValuePayload("EST GREGORIAN 20 BCE");

            // TODO: Try some invalid date values.
            // ValidateInvalidDateValuePayload("TO 40 DEC 2023");
            // ValidateInvalidDateValuePayload("TO 3 dec 2023");
            // ValidateInvalidDateValuePayload("TO 3 JUNE 2023");
            // ValidateInvalidDateValuePayload("TO ABC 2023");
            // ValidateInvalidDateValuePayload("BEF 40 DEC 2023");
            // ValidateInvalidDateValuePayload("BEF 3 dec 2023");
            // ValidateInvalidDateValuePayload("BEF 3 JUNE 2023");
            // ValidateInvalidDateValuePayload("BEF ABC 2023");
            // ValidateInvalidDateValuePayload("BET 2000");
            // ValidateInvalidDateValuePayload("FROM HEBREW 1 TSH 1 BCE");
            // ValidateInvalidDateValuePayload("AFT HEBREW 1 TSH 1 BCE");
        }

        private void ValidateInvalidTimePayload(string value)
        {
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
1 DATE 1 DEC 2023
2 TIME " + value + @"
0 @S1@ SUBM
1 NAME Test
0 TRLR
", new string[] { "Line 9: \"" + value + "\" is not a valid time" });
        }

        private void ValidateValidTimePayload(string value)
        {
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
1 DATE 1 DEC 2023
2 TIME " + value + @"
0 @S1@ SUBM
1 NAME Test
0 TRLR
");
        }

        /// <summary>
        /// Validate Time payload type.
        /// </summary>
        [TestMethod]
        public void ValidateTimePayloadType()
        {
            // Try some valid time values.
            ValidateValidTimePayload("02:50");
            ValidateValidTimePayload("2:50");
            ValidateValidTimePayload("2:50:00.00Z");

            // Try some invalid time values.
            ValidateInvalidTimePayload(" ");
            ValidateInvalidTimePayload("invalid");
            ValidateInvalidTimePayload("000:00");
            ValidateInvalidTimePayload("24:00:00");
            ValidateInvalidTimePayload("2:5");
            ValidateInvalidTimePayload("2:60");
            ValidateInvalidTimePayload("2:00:60");
        }

        private void ValidateInvalidAgePayload(string value)
        {
            ValidateGedcomText(@"0 HEAD
1 GEDC
2 VERS 7.0
0 @I1@ INDI
1 DEAT
2 AGE " + value + @"
0 TRLR
", new string[] { "Line 6: \"" + value + "\" is not a valid age" });
        }

        private void ValidateValidAgePayload(string value)
        {
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @S1@ SUBM
1 NAME Tes
0 @I1@ INDI
1 DEAT
2 AGE " + value + @"
0 TRLR
");
        }

        /// <summary>
        /// Validate Age payload type.
        /// </summary>
        [TestMethod]
        public void ValidateAgePayloadType()
        {
            // Try some valid age values.
            ValidateValidAgePayload("79y");
            ValidateValidAgePayload("79y 1d");
            ValidateValidAgePayload("79y 1w");
            ValidateValidAgePayload("79y 1w 1d");
            ValidateValidAgePayload("79y 1m");
            ValidateValidAgePayload("79y 1m 1d");
            ValidateValidAgePayload("79y 1m 1w");
            ValidateValidAgePayload("79y 1m 1w 1d");
            ValidateValidAgePayload("79m");
            ValidateValidAgePayload("1m 1d");
            ValidateValidAgePayload("1m 1w");
            ValidateValidAgePayload("1m 1w 1d");
            ValidateValidAgePayload("79w");
            ValidateValidAgePayload("79w 1d");
            ValidateValidAgePayload("79d");
            ValidateValidAgePayload("> 79y");
            ValidateValidAgePayload("< 79y 1m 1w 1d");

            // Try some invalid age values.
            ValidateInvalidAgePayload(" ");
            ValidateInvalidAgePayload("invalid");
            ValidateInvalidAgePayload("d");
            ValidateInvalidAgePayload("79");
            ValidateInvalidAgePayload("1d 1m");
            ValidateInvalidAgePayload("<>1y");
            ValidateInvalidAgePayload(">79y");
            ValidateInvalidAgePayload("<79y 1m 1w 1d");
        }

        private void ValidateInvalidLanguagePayload(string value)
        {
            ValidateGedcomText(@"0 HEAD
1 GEDC
2 VERS 7.0
1 LANG " + value + @"
0 TRLR
", new string[] { "Line 4: \"" + value + "\" is not a valid language" });
        }

        private void ValidateValidLanguagePayload(string value)
        {
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
1 LANG " + value + @"
0 @S1@ SUBM
1 NAME Test
0 TRLR
");
        }

        /// <summary>
        /// Validate Language payload type.
        /// </summary>
        [TestMethod]
        public void ValidateLanguagePayloadType()
        {
            // Try some valid language values.
            ValidateValidLanguagePayload("Afrikaans");
            ValidateValidLanguagePayload("Amharic");

            // TODO: Try some invalid language values.
            // ValidateInvalidLanguagePayload(" ");
            // ValidateInvalidLanguagePayload("Unknown");
        }

        /// <summary>
        /// Validate file path payload type.
        /// </summary>
        [TestMethod]
        public void ValidateFilePathPayloadType()
        {
            // Test some valid values, including some file: URIs
            // from RFC 8089.
            ValidateValidFilePayload("media/filename");
            ValidateValidFilePayload("http://www.contoso.com/path/filename");
            ValidateValidFilePayload("file://host.example.com/path/to/file");
            ValidateValidFilePayload("file:///path/to/file");

            // TODO: Test invalid values.  These test strings are taken from
            // https://learn.microsoft.com/en-us/dotnet/api/system.uri.iswellformeduristring?view=net-8.0
            // ValidateInvalidFilePayload("http://www.contoso.com/path???/file name");
            // ValidateInvalidFilePayload("c:\\\\directory\\filename");
            // ValidateInvalidFilePayload("file://c:/directory/filename");
            // ValidateInvalidFilePayload("http:\\\\\\host/path/file");
            // ValidateInvalidFilePayload("2013.05.29_14:33:41");
        }

        /// <summary>
        /// Validate media type payload type.
        /// </summary>
        [TestMethod]
        public void ValidateMediaTypePayloadType()
        {
            // Validate a media type.
            ValidateGedcomText(@"0 HEAD
1 GEDC
2 VERS 5.5.1
0 @O1@ OBJE
1 FILE foo
2 FORM
0 TRLR
", new string[] { "Line 6: \"\" is not a valid value for FORM" });

            // Validate FORM payload.
            ValidateGedcomText(@"0 HEAD
1 GEDC
2 VERS 5.5.1
0 @O1@ OBJE
1 FILE foo
2 FORM application/x-other
0 TRLR
", new string[] { "Line 6: \"application/x-other\" is not a valid value for FORM" });
            ValidateInvalidFormPayload("invalid media type");
            ValidateInvalidFormPayload("text/");
            ValidateInvalidFormPayload("/text");
            ValidateInvalidFormPayload("text/a/b");
            ValidateInvalidFormPayload("text");
        }

        /// <summary>
        /// Validate payload as a pointer to recordType.
        /// </summary>
        [TestMethod]
        public void ValidateXrefPayloadType()
        {
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 TRLR
", new string[] { "Line 3: Payload must be a pointer" });

            // TODO: Error should be "Line 3: Payload must be a pointer"
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 TRLR
", new string[] {
                "Line 3: SUBM is not a valid substructure of HEAD",
                "Line 1: HEAD is missing a substructure of type https://gedcom.io/terms/v5.5.1/SUBM"
            });
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 TRLR
", new string[] {
                "Line 3: SUBM is not a valid substructure of HEAD",
                "Line 1: HEAD is missing a substructure of type https://gedcom.io/terms/v5.5.1/SUBM"
            });

            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @S1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 TRLR
", new string[] { "Line 3: @S1@ has no associated record" });
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @I1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @I1@ INDI
0 TRLR
", new string[] { "Line 3: SUBM points to a INDI record" });
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 SUBM @I1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @I1@ _SUBM
0 TRLR
", new string[] { "Line 3: SUBM points to a _SUBM record" });

            // We can't validate the record type for an
            // undocumented extension.
            ValidateGedcomText(@"0 HEAD
1 SOUR test
1 _SUBM @I1@
1 GEDC
2 VERS 5.5.1
2 FORM LINEAGE-LINKED
1 CHAR ASCII
0 @I1@ INDI
0 TRLR
", new string[] { "Line 1: HEAD is missing a substructure of type https://gedcom.io/terms/v5.5.1/SUBM" });
        }

        // Test files from the test-files repository.

        [TestMethod]
        public void ValidateFileAgeAll()
        {
            // TODO: update based on answer to
            // https://github.com/FamilySearch/GEDCOM/issues/618
            // ValidateGedcomFile(Path.Combine(TEST_FILES_BASE_PATH, "age-all.ged"));
        }

        [TestMethod]
        public void ValidateFileAtSign()
        {
            // TODO: fix IsPointer logic
            // ValidateGedcomFile(Path.Combine(TEST_FILES_BASE_PATH, "atsign.ged"));
        }

        [TestMethod]
        public void ValidateFileCharAscii1()
        {
            ValidateGedcomFile(Path.Combine(TEST_FILES_BASE_PATH, "char_ascii_1.ged"));
        }

        [TestMethod]
        public void ValidateFileCharAscii2()
        {
            ValidateGedcomFile(Path.Combine(TEST_FILES_BASE_PATH, "char_ascii_2.ged"), new string[] { "Line 7: \"LATIN1\" is not a valid value for CHAR" });
        }

#if false
        // TODO: support loading UTF-16
        [TestMethod]
        public void ValidateFileCharUtf16be1()
        {
            ValidateGedcomFile(Path.Combine(TEST_FILES_BASE_PATH, "char_utf16be-1.ged"));
        }

        [TestMethod]
        public void ValidateFileCharUtf16be2()
        {
            ValidateGedcomFile(Path.Combine(TEST_FILES_BASE_PATH, "char_utf16be-2.ged"));
        }

        [TestMethod]
        public void ValidateFileCharUtf16le1()
        {
            ValidateGedcomFile(Path.Combine(TEST_FILES_BASE_PATH, "char_utf16le-1.ged"));
        }

        [TestMethod]
        public void ValidateFileCharUtf16le2()
        {
            ValidateGedcomFile(Path.Combine(TEST_FILES_BASE_PATH, "char_utf16le-2.ged"));
        }
#endif

        [TestMethod]
        public void ValidateFileCharUtf81()
        {
            ValidateGedcomFile(Path.Combine(TEST_FILES_BASE_PATH, "char_utf8-1.ged"));
        }

        [TestMethod]
        public void ValidateFileCharUtf82()
        {
            ValidateGedcomFile(Path.Combine(TEST_FILES_BASE_PATH, "char_utf8-2.ged"));
        }

        [TestMethod]
        public void ValidateFileCharUtf83()
        {
            ValidateGedcomFile(Path.Combine(TEST_FILES_BASE_PATH, "char_utf8-3.ged"));
        }

        [TestMethod]
        public void ValidateFileDateAll()
        {
            ValidateGedcomFile(Path.Combine(TEST_FILES_BASE_PATH, "date-all.ged"), new string[]
            {
                "Line 988: Invalid character '_' in Xref \"@CLOSED_RANGE@\"",
                "Line 4877: Invalid character '_' in Xref \"@CLOSED_PERIOD@\""
            });
        }

        [TestMethod]
        public void ValidateFileDateDual()
        {
            ValidateGedcomFile(Path.Combine(TEST_FILES_BASE_PATH, "date-dual.ged"),
                new string[] { "Line 261: No line text" });
        }

        [TestMethod]
        public void ValidateFileEnumExt()
        {
            ValidateGedcomFile(Path.Combine(TEST_FILES_BASE_PATH, "enum-ext.ged"));
        }

        [TestMethod]
        public void ValidateFileFilename1()
        {
            ValidateGedcomFile(Path.Combine(TEST_FILES_BASE_PATH, "filename-1.ged"), new string[]
            {
                "Line 1: HEAD is missing a substructure of type https://gedcom.io/terms/v5.5.1/CHAR",
                "Line 1: HEAD is missing a substructure of type https://gedcom.io/terms/v5.5.1/SUBM",
                "Line 1: HEAD is missing a substructure of type https://gedcom.io/terms/v5.5.1/HEAD-SOUR"
            });
        }

        [TestMethod]
        public void ValidateFileLangAll()
        {
            ValidateGedcomFile(Path.Combine(TEST_FILES_BASE_PATH, "lang-all.ged"));
        }

        [TestMethod]
        public void ValidateFileNotes1()
        {
            ValidateGedcomFile(Path.Combine(TEST_FILES_BASE_PATH, "notes-1.ged"),
                new string[]
                {
                    "Line 13: An empty payload is not valid after a space"
                });
        }

        [TestMethod]
        public void ValidateFileObje1()
        {
            ValidateGedcomFile(Path.Combine(TEST_FILES_BASE_PATH, "obje-1.ged"), new string[]
            {
                "Line 12: \"mp3\" is not a valid value for FORM",
                "Line 19: \"other\" is not a valid value for FORM",
                "Line 22: \"other\" is not a valid value for FORM"
            });
        }

        [TestMethod]
        public void ValidateFileObsolete1()
        {
            ValidateGedcomFile(Path.Combine(TEST_FILES_BASE_PATH, "obsolete-1.ged"), new string[]
            {
                "Line 1: HEAD is missing a substructure of type https://gedcom.io/terms/v5.5.1/CHAR",
                "Line 1: HEAD is missing a substructure of type https://gedcom.io/terms/v5.5.1/SUBM",
                "Line 1: HEAD is missing a substructure of type https://gedcom.io/terms/v5.5.1/HEAD-SOUR"
            });
        }

        [TestMethod]
        public void ValidateFilePedi1()
        {
            ValidateGedcomFile(Path.Combine(TEST_FILES_BASE_PATH, "pedi-1.ged"), new string[]
            {
                "Line 1: HEAD is missing a substructure of type https://gedcom.io/terms/v5.5.1/SUBM",
                "Line 1: HEAD is missing a substructure of type https://gedcom.io/terms/v5.5.1/HEAD-SOUR"
            });
        }

        [TestMethod]
        public void ValidateFileRela1()
        {
            ValidateGedcomFile(Path.Combine(TEST_FILES_BASE_PATH, "rela_1.ged"));
        }

        [TestMethod]
        public void ValidateFileSour1()
        {
            ValidateGedcomFile(Path.Combine(TEST_FILES_BASE_PATH, "sour-1.ged"),
                new string[] { "Line 1: HEAD is missing a substructure of type https://gedcom.io/terms/v5.5.1/SUBM",
                "Line 1: HEAD is missing a substructure of type https://gedcom.io/terms/v5.5.1/HEAD-SOUR"});
        }

        [TestMethod]
        public void ValidateFileTiny1()
        {
            ValidateGedcomFile("tiny-1.txt", new string[] { "tiny-1.txt must have a .ged extension" });

            ValidateGedcomFile(Path.Combine(TEST_FILES_BASE_PATH, "tiny-1.ged"), new string[]
            {
                "Line 1: HEAD is missing a substructure of type https://gedcom.io/terms/v5.5.1/SUBM",
                "Line 1: HEAD is missing a substructure of type https://gedcom.io/terms/v5.5.1/GEDC",
                "Line 1: HEAD is missing a substructure of type https://gedcom.io/terms/v5.5.1/CHAR",
                "Line 1: HEAD is missing a substructure of type https://gedcom.io/terms/v5.5.1/HEAD-SOUR"
            });
        }

        [TestMethod]
        public void ValidateFileXrefCase()
        {
            ValidateGedcomFile(Path.Combine(TEST_FILES_BASE_PATH, "xref-case.ged"), new string[]
            {
                "Line 3: @test@ has no associated record"
            });
        }
    }
}
