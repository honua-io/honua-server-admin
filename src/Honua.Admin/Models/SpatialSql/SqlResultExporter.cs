using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Honua.Admin.Models.SpatialSql;

/// <summary>
/// Serializes <see cref="SqlExecuteResult"/> into the three supported export formats.
/// All exporters refuse to walk a result that has no rows; callers are expected to
/// guard the truncation override before invoking the GeoJSON or CSV paths.
/// </summary>
public static class SqlResultExporter
{
    public static string ToCsv(SqlExecuteResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var sb = new StringBuilder();
        for (var i = 0; i < result.Columns.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }
            sb.Append(QuoteCsv(result.Columns[i].Name));
        }
        sb.Append("\r\n");

        foreach (var row in result.Rows)
        {
            for (var i = 0; i < result.Columns.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }
                var cell = i < row.Cells.Count ? row.Cells[i] : null;
                sb.Append(QuoteCsv(cell));
            }
            sb.Append("\r\n");
        }

        return sb.ToString();
    }

    /// <summary>
    /// GeoJSON FeatureCollection. Geometry cells are passed through verbatim — the
    /// server returns geometry columns as already-serialized GeoJSON strings.
    /// </summary>
    public static string ToGeoJson(SqlExecuteResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.HasGeometry)
        {
            throw new InvalidOperationException("Result has no geometry column to export as GeoJSON.");
        }

        var geometryIndex = result.GeometryColumnIndex!.Value;

        using var stream = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("type", "FeatureCollection");
            writer.WriteStartArray("features");

            foreach (var row in result.Rows)
            {
                writer.WriteStartObject();
                writer.WriteString("type", "Feature");

                writer.WritePropertyName("geometry");
                var geometryCell = geometryIndex < row.Cells.Count ? row.Cells[geometryIndex] : null;
                if (geometryCell is null)
                {
                    writer.WriteNullValue();
                }
                else
                {
                    if (TryWriteRawJson(writer, geometryCell))
                    {
                        // value already written
                    }
                    else
                    {
                        writer.WriteStringValue(geometryCell);
                    }
                }

                writer.WriteStartObject("properties");
                for (var i = 0; i < result.Columns.Count; i++)
                {
                    if (i == geometryIndex)
                    {
                        continue;
                    }
                    var cell = i < row.Cells.Count ? row.Cells[i] : null;
                    writer.WriteString(result.Columns[i].Name, cell);
                }
                writer.WriteEndObject();

                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            if (result.GeometrySrid is int srid)
            {
                writer.WriteStartObject("crs");
                writer.WriteString("type", "name");
                writer.WriteStartObject("properties");
                writer.WriteString("name", string.Create(CultureInfo.InvariantCulture, $"EPSG:{srid}"));
                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string ToClipboardText(SqlExecuteResult result) => ToCsv(result);

    private static bool TryWriteRawJson(Utf8JsonWriter writer, string candidate)
    {
        try
        {
            using var doc = JsonDocument.Parse(candidate);
            doc.RootElement.WriteTo(writer);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string QuoteCsv(string? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        var needsQuoting = false;
        foreach (var ch in value)
        {
            if (ch is ',' or '"' or '\n' or '\r')
            {
                needsQuoting = true;
                break;
            }
        }

        if (!needsQuoting)
        {
            return value;
        }

        var sb = new StringBuilder(value.Length + 2);
        sb.Append('"');
        foreach (var ch in value)
        {
            if (ch == '"')
            {
                sb.Append('"').Append('"');
            }
            else
            {
                sb.Append(ch);
            }
        }
        sb.Append('"');
        return sb.ToString();
    }
}
