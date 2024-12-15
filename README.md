# GEDCOM 5.5.1 Library

This repository contains code for doing FamilySearch GEDCOM 5.5.1 operations on Windows.
It contains:

- Gedcom5.5.1 - a project that builds a GEDCOM parser library.
- GedCompare - a project that builds a command-line interface that uses the above library to do file comparison.
- GedValidate - a project that builds a command-line interface that uses the above library to do file validation.

The same Gedcom5.5.1 library is used by the online web site tools:
- [GEDCOM 5.5.1 to 7.0 converter](https://magikeygedcomconverter.azurewebsites.net)
- [GEDCOM Validator](https://magikeygedcomconverter.azurewebsites.net/Validate)
- [GEDCOM 5.5.1 Compatibility web tool](https://magikeygedcomconverter.azurewebsites.net/Compatibility)
  which should give the same results as the GedCompare command-line tool here.

# Prerequisites

- Windows 10 or above
- [Visual Studio 2022](https://visualstudio.microsoft.com/downloads/), any edition (the free Community edition will do)

# GedValidate Command-line Tool Usage

GedValidate.exe is a command-line tool usable as follows.

```
usage: GedValidate <filename>
          to check a file as being a valid GEDCOM 5.5.1 file
```

The filename should end in .ged.

# GedCompare Command-line Tool Usage

GedCompare.exe is a command-line tool usable as follows.

```
usage: GedCompare <filename1> <filename2>
          to simply compare two GEDCOM files
       GedCompare <filename>
          to generate a GEDCOM 5.5.1 compatibility report
```

# Gedcom5.5.1 library Usage

The GEDCOM5.5.1 parser library can be used as follows.

## Loading a GEDCOM file from disk

```csharp
GedcomFile gedcomFile = new GedcomFile();
List<string> errors = gedcomFile.LoadFromPath(fileName);
if (errors.Count > 0) {
    // ... handle file errors ...
}
```

## Loading a GEDCOM file from memory

```csharp
GedcomFile gedcomFile = new GedcomFile();
List<string> errors = gedcomFile.LoadFromString(text);
if (errors.Count > 0) {
    // ... handle file errors ...
}
```

## Validating a GEDCOM file according to the GEDCOM 5.5.1 schema

```csharp
List<string> errors = file.Validate();
if (errors.Count > 0) {
    // ... handle validation errors ...
}
```

## Compare two GEDCOM files

```csharp
GedcomFile gedcomFile1 = ...
GedcomFile gedcomFile2 = ...
GedcomComparisonReport report = gedcomFile1.Compare(gedcomFile2);
// ... use report.StructuresAdded, report.StructuresRemoved, etc. ...
```

## Test GEDCOM 5.5.1 compatibility

```csharp
GedcomFile gedcomFile = ...
GedcomCompatibilityReport report = new GedcomCompatibilityReport(gedcomFile);
// ... use various members of report ...
```
