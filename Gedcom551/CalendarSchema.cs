﻿// Copyright (c) Armidale Software
// SPDX-License-Identifier: MIT
using Gedcom551.Construct;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using System.Yaml.Serialization;

namespace Gedcom551
{
    public class CalendarSchema
    {
        public string Uri { get; private set; }
        public string Label { get; private set; }
        public string StandardTag { get; private set; }
        public override string ToString() => this.Label;
        public List<string> MonthUris { get; private set; }
        public List<string> MonthTags { get; private set; }
        public List<string> Epochs { get; private set; }
        CalendarSchema(Dictionary<object, object> dictionary)
        {
            this.Uri = dictionary["uri"] as string;
            this.Label = dictionary["label"] as string;
            this.StandardTag = dictionary["standard tag"] as string;
            this.Epochs = new List<string>();
            GedcomStructureSchema.AddStrings(this.Epochs, dictionary["epochs"] as Object[]);
            this.MonthUris = new List<string>();
            GedcomStructureSchema.AddStrings(this.MonthUris, dictionary["months"] as Object[]);

            this.MonthTags = new List<string>();
            foreach (var uri in this.MonthUris)
            {
                MonthSchema value = MonthSchema.GetMonth(uri);
                if (value != null)
                {
                    this.MonthTags.Add(value.StandardTag);
                    continue;
                }
            }
        }

        static Dictionary<string, CalendarSchema> s_CalendarsByTag = new Dictionary<string, CalendarSchema>();

        private static void AddOldCalendar(string key, List<string> months)
        {
            var dictionary = new Dictionary<object, object>();
            dictionary["label"] = key;
            dictionary["uri"] = key;
            dictionary["standard tag"] = key;
            dictionary["epochs"] = new object[] { "B.C." };
            var monthObjects = new object[months.Count];
            for (int i = 0; i < months.Count; i++)
            {
                string month = months[i];
                if (string.IsNullOrEmpty(month))
                {
                    throw new ArgumentException("Month cannot be null or empty", nameof(months));
                }
                monthObjects[i] = "https://gedcom.io/terms/v7/month-" + month;
            }
            dictionary["months"] = monthObjects;
            var schema = new CalendarSchema(dictionary);
            s_CalendarsByTag.Add(schema.StandardTag, schema);
        }
        public static void LoadAll(string gedcomRegistriesPath)
        {
            if (s_CalendarsByTag.Count > 0)
            {
                return;
            }

            MonthSchema.LoadAll(gedcomRegistriesPath);

            // Manually construct 5.5.1 calendars.
            AddOldCalendar("@#DFRENCH R@", new List<string>
            {
                "VEND", "BRUM", "FRIM", "NIVO", "PLUV", "VENT",
                "GERM", "FLOR", "PRAI", "MESS", "THER", "FRUC",
                "COMP"
            });
            AddOldCalendar("@#DGREGORIAN@", new List<string>
            {
                "JAN", "FEB", "MAR", "APR", "MAY", "JUN",
                "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"
            });
            AddOldCalendar("@#DHEBREW@", new List<string>
            {
                "TSH", "CSH", "KSL", "TVT", "SHV", "ADR", "ADS",
                "NSN", "IYR", "SVN", "TMZ", "AAV", "ELL"
            });
            AddOldCalendar("@#JULIAN@", new List<string>
            {
                "JAN", "FEB", "MAR", "APR", "MAY", "JUN",
                "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"
            });

            var path = Path.Combine(gedcomRegistriesPath, "calendar/standard");
            string[] files;
            try
            {
                files = Directory.GetFiles(path);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return;
            }
            foreach (string filename in files)
            {
                var serializer = new YamlSerializer();
                object[] myObject = serializer.DeserializeFromFile(filename);
                var dictionary = myObject[0] as Dictionary<object, object>;
                var schema = new CalendarSchema(dictionary);
                s_CalendarsByTag.Add(schema.StandardTag, schema);
            }
        }

        public static CalendarSchema GetCalendarByTag(string tag) => s_CalendarsByTag[tag];

        public bool IsValidMonth(string value)
        {
            return this.MonthTags.Contains(value);
        }

        public bool IsValidEpoch(string value)
        {
            return this.Epochs.Contains(value);
        }
    }
}
