using System;
using System.Collections.Generic;
using System.IO;

// Copyright (c) 2015 Steve Hansen
// https://github.com/stevehansen/csv

namespace Csv
{
    /// <summary>
    /// Helper class to write csv (comma separated values) data.
    /// </summary>
    public static class CsvWriter
    {
        /// <summary>
        /// Writes the lines to the writer.
        /// </summary>
        /// <param name="writer">The text writer to write the data to.</param>
        /// <param name="headers">The headers that should be used for the first line, determines the number of columns.</param>
        /// <param name="lines">The lines with data that should be written.</param>
        /// <param name="separator">The separator to use between columns (comma, semicolon, tab, ...)</param>
        public static void Write(TextWriter writer, string[] headers, IEnumerable<string[]> lines, char separator = ',')
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));
            if (headers == null)
                throw new ArgumentNullException(nameof(headers));
            if (lines == null)
                throw new ArgumentNullException(nameof(lines));

            var columnCount = headers.Length;
            WriteLine(writer, headers, columnCount, separator);
            foreach (var line in lines)
                WriteLine(writer, line, columnCount, separator);
        }

        /// <summary>
        /// Writes the lines and return the result.
        /// </summary>
        /// <param name="headers">The headers that should be used for the first line, determines the number of columns.</param>
        /// <param name="lines">The lines with data that should be written.</param>
        /// <param name="separator">The separator to use between columns (comma, semicolon, tab, ...)</param>
        public static string WriteToText(string[] headers, IEnumerable<string[]> lines, char separator = ',')
        {
            using (var writer = new StringWriter())
            {
                Write(writer, headers, lines, separator);

                return writer.ToString();
            }
        }

        private static void WriteLine(TextWriter writer, string[] data, int columnCount, char separator)
        {
            var escapeChars = new[] { separator, '\'', '\n' };
            for (var i = 0; i < columnCount; i++)
            {
                if (i > 0)
                    writer.Write(separator);

                if (i < data.Length)
                {
                    var escape = false;
                    var cell = data[i];
                    if (cell.Contains("\""))
                    {
                        escape = true;
                        cell = cell.Replace("\"", "\"\"");
                    }
                    else if (cell.IndexOfAny(escapeChars) >= 0)
                        escape = true;
                    if (escape)
                        writer.Write('"');
                    writer.Write(cell);
                    if (escape)
                        writer.Write('"');
                }
            }
            writer.WriteLine();
        }
    }
}