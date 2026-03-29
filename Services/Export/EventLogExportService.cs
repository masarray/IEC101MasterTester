using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Packaging;
using System.Security;
using System.Text;
using System.Xml;
using IEC101MasterTester.Models;
using IEC101MasterTester.Models.Export;

namespace IEC101MasterTester.Services.Export
{
    public sealed class EventLogExportService
    {
        public string ExportToExcel(EventLogExportRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.OutputPath))
            {
                throw new ArgumentException("Output path is required.", nameof(request));
            }

            if (request.Rows == null || request.Rows.Count == 0)
            {
                throw new InvalidOperationException("No event-log rows available for export.");
            }

            string outputPath = request.OutputPath;
            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            using (Package package = Package.Open(outputPath, FileMode.Create))
            {
                CreateContent(package, request);
            }

            return outputPath;
        }

        private static void CreateContent(Package package, EventLogExportRequest request)
        {
            Uri workbookUri = new Uri("/xl/workbook.xml", UriKind.Relative);
            Uri worksheetUri = new Uri("/xl/worksheets/sheet1.xml", UriKind.Relative);
            Uri stylesUri = new Uri("/xl/styles.xml", UriKind.Relative);
            Uri coreUri = new Uri("/docProps/core.xml", UriKind.Relative);
            Uri appUri = new Uri("/docProps/app.xml", UriKind.Relative);

            PackagePart workbookPart = package.CreatePart(workbookUri, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml", CompressionOption.Maximum);
            PackagePart worksheetPart = package.CreatePart(worksheetUri, "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml", CompressionOption.Maximum);
            PackagePart stylesPart = package.CreatePart(stylesUri, "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml", CompressionOption.Maximum);
            PackagePart corePart = package.CreatePart(coreUri, "application/vnd.openxmlformats-package.core-properties+xml", CompressionOption.Maximum);
            PackagePart appPart = package.CreatePart(appUri, "application/vnd.openxmlformats-officedocument.extended-properties+xml", CompressionOption.Maximum);

            package.CreateRelationship(workbookUri, TargetMode.Internal, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument");
            package.CreateRelationship(coreUri, TargetMode.Internal, "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties");
            package.CreateRelationship(appUri, TargetMode.Internal, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties");

            workbookPart.CreateRelationship(worksheetUri, TargetMode.Internal, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet", "rId1");
            workbookPart.CreateRelationship(stylesUri, TargetMode.Internal, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles", "rId2");

            WriteXml(workbookPart, BuildWorkbookXml());
            WriteXml(stylesPart, BuildStylesXml());
            WriteXml(worksheetPart, BuildWorksheetXml(request));
            WriteXml(corePart, BuildCoreXml(request));
            WriteXml(appPart, BuildAppXml());
        }

        private static string BuildWorkbookXml()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">"
                + "<sheets><sheet name=\"IEC60870 Event Log Data\" sheetId=\"1\" r:id=\"rId1\"/></sheets>"
                + "</workbook>";
        }

        private static string BuildStylesXml()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">"
                + "<fonts count=\"4\">"
                + "<font><sz val=\"10\"/><color rgb=\"FF111827\"/><name val=\"Segoe UI\"/></font>"
                + "<font><b/><sz val=\"16\"/><color rgb=\"FF0F172A\"/><name val=\"Segoe UI\"/></font>"
                + "<font><b/><sz val=\"10\"/><color rgb=\"FFF8FAFC\"/><name val=\"Segoe UI\"/></font>"
                + "<font><b/><sz val=\"10\"/><color rgb=\"FF1E293B\"/><name val=\"Segoe UI\"/></font>"
                + "</fonts>"
                + "<fills count=\"5\">"
                + "<fill><patternFill patternType=\"none\"/></fill>"
                + "<fill><patternFill patternType=\"gray125\"/></fill>"
                + "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFDCEBFA\"/><bgColor indexed=\"64\"/></patternFill></fill>"
                + "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF1E293B\"/><bgColor indexed=\"64\"/></patternFill></fill>"
                + "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFF8FAFC\"/><bgColor indexed=\"64\"/></patternFill></fill>"
                + "</fills>"
                + "<borders count=\"2\">"
                + "<border><left/><right/><top/><bottom/><diagonal/></border>"
                + "<border><left style=\"thin\"><color rgb=\"FFCBD5E1\"/></left><right style=\"thin\"><color rgb=\"FFCBD5E1\"/></right><top style=\"thin\"><color rgb=\"FFCBD5E1\"/></top><bottom style=\"thin\"><color rgb=\"FFCBD5E1\"/></bottom><diagonal/></border>"
                + "</borders>"
                + "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>"
                + "<cellXfs count=\"5\">"
                + "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>"
                + "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\"/></xf>"
                + "<xf numFmtId=\"0\" fontId=\"3\" fillId=\"4\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyAlignment=\"1\"><alignment horizontal=\"left\" vertical=\"center\"/></xf>"
                + "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"3\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\" wrapText=\"1\"/></xf>"
                + "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"4\" borderId=\"1\" xfId=\"0\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment vertical=\"top\" wrapText=\"1\"/></xf>"
                + "</cellXfs>"
                + "</styleSheet>";
        }

        private static string BuildWorksheetXml(EventLogExportRequest request)
        {
            EventLogExportMetadata metadata = request.Metadata ?? new EventLogExportMetadata();
            IList<EventLogRow> rows = request.Rows ?? new List<EventLogRow>();
            StringBuilder sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            sb.Append("<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"7\" topLeftCell=\"A8\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>");
            sb.Append("<cols>");
            AppendCol(sb, 1, 1, 8);
            AppendCol(sb, 2, 2, 22);
            AppendCol(sb, 3, 3, 14);
            AppendCol(sb, 4, 4, 28);
            AppendCol(sb, 5, 5, 12);
            AppendCol(sb, 6, 6, 14);
            AppendCol(sb, 7, 7, 28);
            AppendCol(sb, 8, 8, 16);
            AppendCol(sb, 9, 9, 14);
            AppendCol(sb, 10, 10, 14);
            sb.Append("</cols><sheetData>");

            AppendRow(sb, 1, new[] { Cell("A1", metadata.Title ?? "IEC60870 Event Log Data", 1) });
            AppendRow(sb, 2, new[] { Cell("A2", "Module", 2), Cell("B2", metadata.ModuleName ?? "IEC101MasterTester", 4), Cell("D2", "Exported At", 2), Cell("E2", metadata.ExportedAtText ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture), 4) });
            AppendRow(sb, 3, new[] { Cell("A3", "Source Window", 2), Cell("B3", metadata.SourceWindow ?? "NUC SOE Audit", 4), Cell("D3", "Session Started", 2), Cell("E3", metadata.SessionStartedText ?? "-", 4) });
            AppendRow(sb, 4, new[] { Cell("A4", "Context", 2), Cell("B4", metadata.ContextSummary ?? "-", 4) });
            AppendRow(sb, 5, new[] { Cell("A5", "Filters", 2), Cell("B5", metadata.FilterSummary ?? "No filter applied", 4) });
            AppendRow(sb, 7, new[] { Cell("A7", "No", 3), Cell("B7", "Time", 3), Cell("C7", "Channel", 3), Cell("D7", "Signal Name", 3), Cell("E7", "IOA", 3), Cell("F7", "Type", 3), Cell("G7", "Event", 3), Cell("H7", "Value", 3), Cell("I7", "COT", 3), Cell("J7", "Quality", 3) });

            int excelRow = 8;
            for (int index = 0; index < rows.Count; index++, excelRow++)
            {
                EventLogRow row = rows[index] ?? new EventLogRow();
                AppendRow(sb, excelRow, new[]
                {
                    Cell("A" + excelRow, (index + 1).ToString(CultureInfo.InvariantCulture), 4),
                    Cell("B" + excelRow, row.Time ?? "-", 4),
                    Cell("C" + excelRow, row.Source ?? "-", 4),
                    Cell("D" + excelRow, row.Name ?? "-", 4),
                    Cell("E" + excelRow, row.IOA ?? "-", 4),
                    Cell("F" + excelRow, row.Type ?? "-", 4),
                    Cell("G" + excelRow, row.Event ?? "-", 4),
                    Cell("H" + excelRow, row.Value ?? "-", 4),
                    Cell("I" + excelRow, row.Cot ?? "-", 4),
                    Cell("J" + excelRow, row.Quality ?? "-", 4)
                });
            }

            sb.Append("</sheetData>");
            sb.Append("<mergeCells count=\"3\"><mergeCell ref=\"A1:J1\"/><mergeCell ref=\"B4:J4\"/><mergeCell ref=\"B5:J5\"/></mergeCells>");
            sb.Append("<autoFilter ref=\"A7:J");
            sb.Append(Math.Max(7, rows.Count + 7).ToString(CultureInfo.InvariantCulture));
            sb.Append("\"/>");
            sb.Append("</worksheet>");
            return sb.ToString();
        }

        private static string BuildCoreXml(EventLogExportRequest request)
        {
            string now = DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture) + "Z";
            string title = XmlEscape((request.Metadata?.Title) ?? "IEC60870 Event Log Data");
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\" xmlns:dcmitype=\"http://purl.org/dc/dcmitype/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">"
                + "<dc:title>" + title + "</dc:title><dc:creator>IEC101MasterTester</dc:creator><cp:lastModifiedBy>IEC101MasterTester</cp:lastModifiedBy>"
                + "<dcterms:created xsi:type=\"dcterms:W3CDTF\">" + now + "</dcterms:created>"
                + "<dcterms:modified xsi:type=\"dcterms:W3CDTF\">" + now + "</dcterms:modified>"
                + "</cp:coreProperties>";
        }

        private static string BuildAppXml()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/extended-properties\" xmlns:vt=\"http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes\">"
                + "<Application>IEC101MasterTester</Application><DocSecurity>0</DocSecurity><ScaleCrop>false</ScaleCrop>"
                + "<HeadingPairs><vt:vector size=\"2\" baseType=\"variant\"><vt:variant><vt:lpstr>Worksheets</vt:lpstr></vt:variant><vt:variant><vt:i4>1</vt:i4></vt:variant></vt:vector></HeadingPairs>"
                + "<TitlesOfParts><vt:vector size=\"1\" baseType=\"lpstr\"><vt:lpstr>IEC60870 Event Log Data</vt:lpstr></vt:vector></TitlesOfParts>"
                + "<Company>Arisulistiono</Company></Properties>";
        }

        private static void AppendCol(StringBuilder sb, int min, int max, double width)
        {
            sb.AppendFormat(CultureInfo.InvariantCulture, "<col min=\"{0}\" max=\"{1}\" width=\"{2}\" customWidth=\"1\"/>", min, max, width);
        }

        private static void AppendRow(StringBuilder sb, int rowIndex, IEnumerable<string> cells)
        {
            sb.AppendFormat(CultureInfo.InvariantCulture, "<row r=\"{0}\">", rowIndex);
            foreach (string cell in cells)
            {
                sb.Append(cell);
            }
            sb.Append("</row>");
        }

        private static string Cell(string reference, string value, int styleIndex)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "<c r=\"{0}\" s=\"{1}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{2}</t></is></c>",
                reference, styleIndex, XmlEscape(value ?? string.Empty));
        }

        private static void WriteXml(PackagePart part, string xml)
        {
            using (Stream stream = part.GetStream(FileMode.Create, FileAccess.Write))
            using (XmlWriter writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = Encoding.UTF8, Indent = false }))
            {
                writer.WriteRaw(xml);
            }
        }

        private static string XmlEscape(string value)
        {
            return SecurityElement.Escape(value ?? string.Empty) ?? string.Empty;
        }
    }
}
