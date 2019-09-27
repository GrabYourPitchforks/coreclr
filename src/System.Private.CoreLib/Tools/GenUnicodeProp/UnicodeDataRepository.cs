// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;

namespace GenUnicodeProp
{
    internal sealed class UnicodeDataRepository
    {
        private static readonly Dictionary<string, UnicodeCategory> UnicodeCategoryMap = new Dictionary<string, UnicodeCategory>
        {
            ["Lu"] = UnicodeCategory.UppercaseLetter,
            ["Ll"] = UnicodeCategory.LowercaseLetter,
            ["Lt"] = UnicodeCategory.TitlecaseLetter,
            ["Lm"] = UnicodeCategory.ModifierLetter,
            ["Lo"] = UnicodeCategory.OtherLetter,
            ["Mn"] = UnicodeCategory.NonSpacingMark,
            ["Mc"] = UnicodeCategory.SpacingCombiningMark,
            ["Me"] = UnicodeCategory.EnclosingMark,
            ["Nd"] = UnicodeCategory.DecimalDigitNumber,
            ["Nl"] = UnicodeCategory.LetterNumber,
            ["No"] = UnicodeCategory.OtherNumber,
            ["Zs"] = UnicodeCategory.SpaceSeparator,
            ["Zl"] = UnicodeCategory.LineSeparator,
            ["Zp"] = UnicodeCategory.ParagraphSeparator,
            ["Cc"] = UnicodeCategory.Control,
            ["Cf"] = UnicodeCategory.Format,
            ["Cs"] = UnicodeCategory.Surrogate,
            ["Co"] = UnicodeCategory.PrivateUse,
            ["Pc"] = UnicodeCategory.ConnectorPunctuation,
            ["Pd"] = UnicodeCategory.DashPunctuation,
            ["Ps"] = UnicodeCategory.OpenPunctuation,
            ["Pe"] = UnicodeCategory.ClosePunctuation,
            ["Pi"] = UnicodeCategory.InitialQuotePunctuation,
            ["Pf"] = UnicodeCategory.FinalQuotePunctuation,
            ["Po"] = UnicodeCategory.OtherPunctuation,
            ["Sm"] = UnicodeCategory.MathSymbol,
            ["Sc"] = UnicodeCategory.CurrencySymbol,
            ["Sk"] = UnicodeCategory.ModifierSymbol,
            ["So"] = UnicodeCategory.OtherSymbol,
            ["Cn"] = UnicodeCategory.OtherNotAssigned,
        };

        private readonly List<CodePointInfo> _data = new List<CodePointInfo>();

        private UnicodeDataRepository()
        {
            // First, read all of the ancillary data files into their own distinct data
            // structures. We'll use these as helper structures once we parse the
            // main data file.

            HashSet<uint> whitespaceCodePoints = GetWhiteSpaceCodePoints();
            Dictionary<uint, uint> caseFoldMap = GetSimpleCaseFoldMap();
            Dictionary<uint, GraphemeBoundaryCategory> graphemeBreakMap = GetGraphemeBreakPropertyMap();
            Dictionary<uint, RestrictedBidiClass> bidiClassMap = GetBidiClassMap();

            // Next, process the main data file, folding in the ancillary data.

            foreach (UnicodeDataEntry entry in ReadUnicodeDataFile())
            {
                CodePointInfo newData = new CodePointInfo()
                {
                    CodePoint = entry.codePoint,
                    UnicodeCategory = UnicodeCategoryMap[entry.generalCategory]
                };

                // Read and populate the simple case mappings & case fold mappings.
                // If a mapping exists, we store the difference between the original code point
                // and the mapped result, since this allows greater data compaction than storing
                // the mapped result as-is.

                if (entry.simpleUppercaseMapping.HasValue)
                {
                    newData.OffsetToSimpleUppercase = (int)(entry.simpleUppercaseMapping.Value - entry.codePoint);
                }

                if (entry.simpleLowercaseMapping.HasValue)
                {
                    newData.OffsetToSimpleLowercase = (int)(entry.simpleLowercaseMapping - entry.codePoint);
                }

                if (entry.simpleTitlecaseMapping.HasValue)
                {
                    newData.OffsetToSimpleTitlecase = (int)(entry.simpleTitlecaseMapping - entry.codePoint);
                }

                if (caseFoldMap.TryGetValue(entry.codePoint, out uint caseFoldMappedValue))
                {
                    newData.OffsetToSimpleCaseFold = (int)(caseFoldMappedValue - entry.codePoint);
                }

                // Read and populate the "isWhitespace?" flag

                newData.IsWhitespace = whitespaceCodePoints.Contains(entry.codePoint);

                // Read and populate the digit & numeric data values

                if (!string.IsNullOrEmpty(entry.decimalDigitValue))
                {
                    newData.DecimalDigitValue = sbyte.Parse(entry.decimalDigitValue, NumberStyles.Integer, CultureInfo.InvariantCulture);
                }

                if (!string.IsNullOrEmpty(entry.digitValue))
                {
                    newData.DigitValue = sbyte.Parse(entry.digitValue, NumberStyles.Integer, CultureInfo.InvariantCulture);
                }

                if (!string.IsNullOrEmpty(entry.numericValue))
                {
                    // Nuemeric value will be in form "[-]BigInteger [/ BigInteger ]"

                    string[] split = entry.numericValue.Split('/');
                    if (split.Length > 2)
                    {
                        throw new Exception($"Unexpected numeric value: {entry.numericValue}");
                    }

                    newData.NumericValue = double.Parse(split[0], CultureInfo.InvariantCulture);

                    if (split.Length == 2)
                    {
                        newData.NumericValue /= double.Parse(split[1], CultureInfo.InvariantCulture);
                    }
                }

                // Read and populate the grapheme cluster boundary break data
                // and the bidi class data.

                graphemeBreakMap.TryGetValue(entry.codePoint, out newData.GraphemeBoundaryCategory);
                bidiClassMap.TryGetValue(entry.codePoint, out newData.RestrictedBidiClass);

                // We're done! Add all the info to the list.

                _data.Add(newData);
            }

            // Finally, double-check our data files to make sure that each entry in the ancillary
            // data files had a corresponding entry in the UnicodeData.txt main data file. There
            // are some code points (like U+2065) which have categories of interest to us but which
            // aren't yet assigned.

            HashSet<uint> assignedCodePoints = new HashSet<uint>(_data.Select(entry => entry.CodePoint));

            for (uint i = 0; i <= 0x10FFFF; i++)
            {
                if (whitespaceCodePoints.Contains(i)
                    || caseFoldMap.ContainsKey(i)
                    || graphemeBreakMap.ContainsKey(i)
                    || bidiClassMap.ContainsKey(i))
                {
                    if (!assignedCodePoints.Contains(i))
                    {
                        CodePointInfo newCodePointInfo = new CodePointInfo()
                        {
                            IsWhitespace = whitespaceCodePoints.Contains(i)
                        };

                        if (caseFoldMap.TryGetValue(i, out uint mappedCaseFoldValue))
                        {
                            newCodePointInfo.OffsetToSimpleCaseFold = (int)(mappedCaseFoldValue - i);
                        }

                        graphemeBreakMap.TryGetValue(i, out newCodePointInfo.GraphemeBoundaryCategory);

                        bidiClassMap.TryGetValue(i, out newCodePointInfo.RestrictedBidiClass);

                        _data.Add(newCodePointInfo);
                    }
                }
            }
        }

        public IReadOnlyList<CodePointInfo> Data => _data;

        /// <summary>
        /// Reads DerivedBidiClass.txt and returns the map of code points to bidi classes.
        /// </summary>
        private static Dictionary<uint, RestrictedBidiClass> GetBidiClassMap()
        {
            Dictionary<uint, RestrictedBidiClass> map = new Dictionary<uint, RestrictedBidiClass>();

            foreach (string line in File.ReadAllLines("DerivedBidiClass.txt"))
            {
                if (PropsFileEntry.TryParseLine(line, out PropsFileEntry parsedEntry))
                {
                    RestrictedBidiClass bidiClass;

                    switch (parsedEntry.PropName)
                    {
                        case "L":
                            bidiClass = RestrictedBidiClass.StrongLeftToRight;
                            break;

                        case "R":
                        case "AL":
                            bidiClass = RestrictedBidiClass.StrongRightToLeft;
                            break;

                        default:
                            bidiClass = RestrictedBidiClass.Other;
                            break;
                    }

                    for (uint i = parsedEntry.FirstCodePoint; i <= parsedEntry.LastCodePoint; i++)
                    {
                        map[i] = bidiClass;
                    }
                }
            }

            return map;
        }

        /// <summary>
        /// Reads GraphemeBreakProperty.txt and emoji-data.txt and returns the map of code points to grapheme break properties.
        /// </summary>
        private static Dictionary<uint, GraphemeBoundaryCategory> GetGraphemeBreakPropertyMap()
        {
            Dictionary<uint, GraphemeBoundaryCategory> map = new Dictionary<uint, GraphemeBoundaryCategory>();

            foreach (string line in File.ReadAllLines("GraphemeBreakProperty.txt"))
            {
                if (PropsFileEntry.TryParseLine(line, out PropsFileEntry parsedEntry))
                {
                    for (uint i = parsedEntry.FirstCodePoint; i <= parsedEntry.LastCodePoint; i++)
                    {
                        map[i] = Enum.Parse<GraphemeBoundaryCategory>(parsedEntry.PropName);
                    }
                }
            }

            foreach (string line in File.ReadAllLines("emoji-data.txt"))
            {
                if (PropsFileEntry.TryParseLine(line, out PropsFileEntry parsedEntry)
                    && parsedEntry.PropName == "Extended_Pictographic")
                {
                    for (uint i = parsedEntry.FirstCodePoint; i <= parsedEntry.LastCodePoint; i++)
                    {
                        map[i] = GraphemeBoundaryCategory.Extended_Pictographic;
                    }
                }
            }

            return map;
        }

        /// <summary>
        /// Reads CaseFolding.txt and returns a map of code points to their case-fold values.
        /// </summary>
        private static Dictionary<uint, uint> GetSimpleCaseFoldMap()
        {
            Dictionary<uint, uint> map = new Dictionary<uint, uint>();

            // The format we expect is "<code>; <status>; <mapping>; # <name>"

            foreach (string line in File.ReadLines("CaseFolding.txt"))
            {
                string[] split = line.Split('#', 2);
                if (split.Length < 2) { continue; } // not in correct format

                split = split[0].Split(';');
                if (split.Length != 4) { continue; } // not in correct format

                string status = split[1].Trim();
                if (status != "C" && status != "S") { continue; } // not a simple mapping

                uint thisCodePoint = uint.Parse(split[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                uint mappedCodePoint = uint.Parse(split[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);

                map[thisCodePoint] = mappedCodePoint;
            }

            return map;
        }

        /// <summary>
        /// Reads PropList.txt and returns the set of code points with the "White_Space" property.
        /// </summary>
        private static HashSet<uint> GetWhiteSpaceCodePoints()
        {
            HashSet<uint> set = new HashSet<uint>();

            foreach (string line in File.ReadAllLines("PropList.txt"))
            {
                if (PropsFileEntry.TryParseLine(line, out PropsFileEntry parsedEntry)
                    && parsedEntry.PropName == "White_Space")
                {
                    for (uint i = parsedEntry.FirstCodePoint; i <= parsedEntry.LastCodePoint; i++)
                    {
                        set.Add(i);
                    }
                }
            }

            return set;
        }

        public static UnicodeDataRepository ParseFiles() => new UnicodeDataRepository();

        /// <summary>
        /// Reads UnicodeData.txt and returns a list of all entries in the file.
        /// </summary>
        /// <returns></returns>
        private static IEnumerable<UnicodeDataEntry> ReadUnicodeDataFile()
        {
            // Format is described in UAX #44: https://www.unicode.org/reports/tr44/

            UnicodeDataEntry thisEntry = new UnicodeDataEntry();

            foreach (string line in File.ReadAllLines("UnicodeData.txt"))
            {
                if (string.IsNullOrWhiteSpace(line)) { continue; }

                string[] split = line.Split(';');
                if (split.Length != 15)
                {
                    throw new Exception($"Unexpected line: {line}");
                }

                uint thisCodePoint = uint.Parse(split[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                string name = split[1];

                // Is this the end of the range which began in the previous line?
                // If so, return a duplicate value for each code point in the range.

                if (name.EndsWith(", Last>", StringComparison.Ordinal))
                {
                    // 'thisEntry' actually points to the previous entry

                    if (thisEntry.name != name[..^(", Last>".Length)] + ", First>")
                    {
                        throw new Exception($"Unexpected line: {line}");
                    }

                    for (uint i = thisEntry.codePoint; i <= thisCodePoint; i++)
                    {
                        thisEntry.codePoint = i;
                        yield return thisEntry;
                    }

                    continue;
                }

                static uint? TryParseUInt32AsHex(string value)
                {
                    return uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint parsed) ? (uint?)parsed : null;
                }

                thisEntry = new UnicodeDataEntry()
                {
                    codePoint = thisCodePoint,
                    name = name,
                    generalCategory = split[2],
                    decimalDigitValue = split[6],
                    digitValue = split[7],
                    numericValue = split[8],
                    simpleUppercaseMapping = TryParseUInt32AsHex(split[12]),
                    simpleLowercaseMapping = TryParseUInt32AsHex(split[13]),
                    simpleTitlecaseMapping = TryParseUInt32AsHex(split[14]),
                };

                yield return thisEntry;
            }
        }

        // struct since we rely on copy-by-value in a few places
        private struct UnicodeDataEntry
        {
            public uint codePoint;
            public string name;
            public string generalCategory;
            public string decimalDigitValue;
            public string digitValue;
            public string numericValue;
            public uint? simpleUppercaseMapping;
            public uint? simpleLowercaseMapping;
            public uint? simpleTitlecaseMapping;
        }
    }
}
