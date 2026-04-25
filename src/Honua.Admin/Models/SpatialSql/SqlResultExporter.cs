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
    /// GeoJSON FeatureCollection per RFC 7946. Geometry cells are passed through
    /// verbatim — the server returns geometry columns as already-serialized
    /// GeoJSON strings in WGS84/EPSG:4326. RFC 7946 §4 mandates WGS84 longitude
    /// and latitude and removed the legacy <c>crs</c> member, so this exporter
    /// emits no <c>crs</c> envelope. If <see cref="SqlExecuteResult.GeometrySrid"/>
    /// is set to anything other than 4326 the exporter throws — reprojection is
    /// the server's responsibility, not the admin's.
    /// </summary>
    public static string ToGeoJson(SqlExecuteResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.HasGeometry)
        {
            throw new InvalidOperationException("Result has no geometry column to export as GeoJSON.");
        }

        if (result.GeometrySrid is int srid && srid != Wgs84Srid)
        {
            throw new InvalidOperationException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"GeoJSON export requires WGS84 (SRID 4326); got SRID {srid}. Reproject server-side before export."));
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

            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// SRID 4326 — the WGS84 geographic coordinate system. RFC 7946 §4 mandates
    /// this for GeoJSON output, and MapLibre expects WGS84 lng/lat for the in-page
    /// preview, so both the exporter and <c>SpatialSqlPlaygroundState</c> guard
    /// against non-4326 results using this single constant.
    /// </summary>
    internal const int Wgs84Srid = 4326;

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
