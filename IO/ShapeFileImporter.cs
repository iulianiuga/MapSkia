using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Data;
using FCoreMap.Geometries;
using System.Drawing;
using System.Linq;

namespace FCoreMap.IO
{
    /// <summary>
    /// Imports ESRI Shapefiles into FCoreMap geometries and layers.
    /// </summary>
    public class ShapeFileImporter
    {
        // ESRI Shapefile type codes
        private const int ShapeTypeNull = 0;
        private const int ShapeTypePoint = 1;
        private const int ShapeTypePolyLine = 3;
        private const int ShapeTypePolygon = 5;
        private const int ShapeTypeMultiPoint = 8;
        private const int ShapeTypePointZ = 11;
        private const int ShapeTypePolyLineZ = 13;
        private const int ShapeTypePolygonZ = 15;
        private const int ShapeTypeMultiPointZ = 18;
        private const int ShapeTypePointM = 21;
        private const int ShapeTypePolyLineM = 23;
        private const int ShapeTypePolygonM = 25;
        private const int ShapeTypeMultiPointM = 28;
        private const int ShapeTypeMultiPatch = 31;

        // File paths
        private string shpFilePath;
        private string dbfFilePath;
        private string shxFilePath;

        // Imported attribute data
        private DataTable attributeTable;

        // Record to geometry mapping
        private Dictionary<int, int> recordToGeometryMap;

        /// <summary>
        /// Gets the attribute table imported from the DBF file.
        /// </summary>
        public DataTable AttributeTable => attributeTable;

        /// <summary>
        /// Initializes a new instance of the ShapeFileImporter class.
        /// </summary>
        /// <param name="shapeFilePath">Path to the .shp file (without extension)</param>
        public ShapeFileImporter(string shapeFilePath)
        {
            // Set file paths based on the base path
            string basePath = Path.Combine(
                Path.GetDirectoryName(shapeFilePath),
                Path.GetFileNameWithoutExtension(shapeFilePath));

            shpFilePath = basePath + ".shp";
            dbfFilePath = basePath + ".dbf";
            shxFilePath = basePath + ".shx";

            // Verify files exist
            if (!File.Exists(shpFilePath))
                throw new FileNotFoundException("Shape file not found.", shpFilePath);

            if (!File.Exists(dbfFilePath))
                throw new FileNotFoundException("DBF file not found.", dbfFilePath);

            if (!File.Exists(shxFilePath))
                throw new FileNotFoundException("SHX file not found.", shxFilePath);

            // Initialize the record-to-geometry mapping
            recordToGeometryMap = new Dictionary<int, int>();
        }

        /// <summary>
        /// Imports the shapefile and creates a new layer in the given LayerManager.
        /// </summary>
        /// <param name="layerManager">The LayerManager to add the imported layer to.</param>
        /// <param name="layerName">Name for the new layer. If null, uses the filename.</param>
        /// <returns>The created layer containing the imported geometries.</returns>
        public Layer ImportToLayer(LayerManager layerManager, string layerName = null)
        {
            // Use filename as layer name if not specified
            if (string.IsNullOrEmpty(layerName))
                layerName = Path.GetFileNameWithoutExtension(shpFilePath);

            // Import the shapefile
            ShapefileData shapeData = ReadShapefile();

            // Create an appropriate layer based on the geometry type
            Layer layer = null;

            switch (shapeData.ShapeType)
            {
                case ShapeTypePoint:
                case ShapeTypePointZ:
                case ShapeTypePointM:
                case ShapeTypeMultiPoint:
                case ShapeTypeMultiPointM:
                case ShapeTypeMultiPointZ:
                    layer = layerManager.CreateLayer(layerName, LayerType.Point);
                    ImportPointsToLayer(layer, shapeData);
                    break;

                case ShapeTypePolyLine:
                case ShapeTypePolyLineZ:
                case ShapeTypePolyLineM:
                    layer = layerManager.CreateLayer(layerName, LayerType.Line);
                    ImportLinesToLayer(layer, shapeData);
                    break;

                case ShapeTypePolygon:
                case ShapeTypePolygonZ:
                case ShapeTypePolygonM:
                    layer = layerManager.CreateLayer(layerName, LayerType.Polygon);
                    ImportPolygonsToLayer(layer, shapeData);
                    break;

                default:
                    throw new NotSupportedException($"Shape type {shapeData.ShapeType} is not supported.");
            }

            // Associate attributes with the layer
            if (layer != null)
            {
                AssociateAttributes(layerManager, layer.Name);

                // Set a default label field if the layer has attributes
                if (layer.AttributeData != null && layer.AttributeData.Columns.Count > 0)
                {
                    // Try to find a name-like field for labeling
                    string labelField = FindSuitableLabelField(layer.AttributeData);
                    if (!string.IsNullOrEmpty(labelField))
                    {
                        layer.Style.LabelField = labelField;
                    }
                }
            }

            // Return the created layer
            return layer;
        }

        /// <summary>
        /// Finds a suitable field for labeling from the attribute data.
        /// </summary>
        /// <param name="attributeData">The attribute data table</param>
        /// <returns>The name of a suitable field for labeling</returns>
        private string FindSuitableLabelField(DataTable attributeData)
        {
            if (attributeData == null || attributeData.Columns.Count == 0)
                return null;

            // Priority list of field name patterns that might contain good label values
            string[] priorityPatterns = new string[]
            {
                "name", "label", "title", "id", "code", "desc", "type"
            };

            // First, look for columns matching our priority patterns
            foreach (string pattern in priorityPatterns)
            {
                foreach (DataColumn column in attributeData.Columns)
                {
                    if (column.ColumnName.ToLower().Contains(pattern))
                    {
                        return column.ColumnName;
                    }
                }
            }

            // If no matches found, prefer string columns over others
            foreach (DataColumn column in attributeData.Columns)
            {
                if (column.DataType == typeof(string))
                {
                    return column.ColumnName;
                }
            }

            // If still no match, just return the first column
            return attributeData.Columns[0].ColumnName;
        }

        /// <summary>
        /// Reads the shapefile and returns the geometry data.
        /// </summary>
        private ShapefileData ReadShapefile()
        {
            ShapefileData data = new ShapefileData();
            attributeTable = ReadDBF();

            using (FileStream fs = new FileStream(shpFilePath, FileMode.Open, FileAccess.Read))
            using (BinaryReader br = new BinaryReader(fs))
            {
                // Read the shapefile header
                ReadShapefileHeader(br, data);

                // Read all the shape records
                while (fs.Position < fs.Length)
                {
                    ReadShapeRecord(br, data);
                }
            }

            return data;
        }

        /// <summary>
        /// Reads the shapefile header.
        /// </summary>
        private void ReadShapefileHeader(BinaryReader br, ShapefileData data)
        {
            // Skip file code (4 bytes) and 5 unused ints (20 bytes)
            br.BaseStream.Position = 24;

            // Read file length
            int fileLength = SwapEndian(br.ReadInt32()) * 2; // In 16-bit words, convert to bytes

            // Read version and shape type
            byte version = br.ReadByte();
            br.BaseStream.Position = 32;
            data.ShapeType = br.ReadInt32();

            // Read bounding box (8 doubles = 64 bytes)
            data.XMin = br.ReadDouble();
            data.YMin = br.ReadDouble();
            data.XMax = br.ReadDouble();
            data.YMax = br.ReadDouble();

            // Skip Z and M bounds (4 doubles = 32 bytes)
            br.BaseStream.Position = 100;
        }

        /// <summary>
        /// Reads a single shape record from the shapefile and stores mapping information.
        /// </summary>
        private void ReadShapeRecord(BinaryReader br, ShapefileData data)
        {
            // Read record header
            int recordNumber = SwapEndian(br.ReadInt32());
            int contentLength = SwapEndian(br.ReadInt32()) * 2; // In 16-bit words, convert to bytes
            long startPosition = br.BaseStream.Position;

            // Store the current count of geometries in the data for mapping
            int currentGeometryIndex = 0;
            switch (data.ShapeType)
            {
                case ShapeTypePoint:
                case ShapeTypePointZ:
                case ShapeTypePointM:
                case ShapeTypeMultiPoint:
                case ShapeTypeMultiPointZ:
                case ShapeTypeMultiPointM:
                    currentGeometryIndex = data.Points.Count;
                    break;
                case ShapeTypePolyLine:
                case ShapeTypePolyLineZ:
                case ShapeTypePolyLineM:
                    currentGeometryIndex = data.Lines.Count;
                    break;
                case ShapeTypePolygon:
                case ShapeTypePolygonZ:
                case ShapeTypePolygonM:
                    currentGeometryIndex = data.Polygons.Count;
                    break;
            }

            // Read shape type
            int shapeType = br.ReadInt32();

            // Skip null shapes
            if (shapeType == ShapeTypeNull)
            {
                // Skip to the next record
                br.BaseStream.Position = startPosition + contentLength;
                // Don't map null shapes
                return;
            }

            // Verify shape type matches the file header
            if (shapeType != data.ShapeType)
            {
                // This is allowed in some shapefiles, but we'll continue with the record
                // just logging a warning
                Console.WriteLine($"Warning: Shape type mismatch. Expected {data.ShapeType}, got {shapeType}");
            }

            switch (shapeType)
            {
                case ShapeTypePoint:
                case ShapeTypePointZ:
                case ShapeTypePointM:
                    ReadPoint(br, data);
                    break;

                case ShapeTypeMultiPoint:
                case ShapeTypeMultiPointZ:
                case ShapeTypeMultiPointM:
                    ReadMultiPoint(br, data, contentLength, startPosition);
                    break;

                case ShapeTypePolyLine:
                case ShapeTypePolyLineZ:
                case ShapeTypePolyLineM:
                    ReadPolyLine(br, data, contentLength, startPosition);
                    break;

                case ShapeTypePolygon:
                case ShapeTypePolygonZ:
                case ShapeTypePolygonM:
                    ReadPolygon(br, data, contentLength, startPosition);
                    break;

                default:
                    // Skip unsupported shape types
                    br.BaseStream.Position = startPosition + contentLength;
                    break;
            }

            // Map the record number to the geometry index
            // We're using recordNumber-1 because DBF records are 1-based, but our arrays are 0-based
            int dbfIndex = recordNumber - 1;
            if (dbfIndex >= 0 && dbfIndex < attributeTable?.Rows.Count)
            {
                // For points, we map directly
                if (shapeType == ShapeTypePoint || shapeType == ShapeTypePointZ || shapeType == ShapeTypePointM)
                {
                    recordToGeometryMap[currentGeometryIndex] = dbfIndex;
                }
                // For multi-points, we need to handle multiple points per record
                else if (shapeType == ShapeTypeMultiPoint || shapeType == ShapeTypeMultiPointZ || shapeType == ShapeTypeMultiPointM)
                {
                    int numPoints = data.Points.Count - currentGeometryIndex;
                    for (int i = 0; i < numPoints; i++)
                    {
                        recordToGeometryMap[currentGeometryIndex + i] = dbfIndex;
                    }
                }
                // For lines and polygons, we map directly
                else
                {
                    int count = 0;
                    switch (shapeType)
                    {
                        case ShapeTypePolyLine:
                        case ShapeTypePolyLineZ:
                        case ShapeTypePolyLineM:
                            count = data.Lines.Count - currentGeometryIndex;
                            break;
                        case ShapeTypePolygon:
                        case ShapeTypePolygonZ:
                        case ShapeTypePolygonM:
                            count = data.Polygons.Count - currentGeometryIndex;
                            break;
                    }

                    // Map each geometry to the attribute record
                    for (int i = 0; i < count; i++)
                    {
                        recordToGeometryMap[currentGeometryIndex + i] = dbfIndex;
                    }
                }
            }
        }

        /// <summary>
        /// Reads a point shape.
        /// </summary>
        private void ReadPoint(BinaryReader br, ShapefileData data)
        {
            double x = br.ReadDouble();
            double y = br.ReadDouble();
            data.Points.Add(new PointD(x, y));
        }

        /// <summary>
        /// Reads a multi-point shape.
        /// </summary>
        private void ReadMultiPoint(BinaryReader br, ShapefileData data, int contentLength, long startPosition)
        {
            // Skip bounding box (4 doubles = 32 bytes)
            br.BaseStream.Position += 32;

            // Read number of points
            int numPoints = br.ReadInt32();

            // Read all points
            for (int i = 0; i < numPoints; i++)
            {
                double x = br.ReadDouble();
                double y = br.ReadDouble();
                data.Points.Add(new PointD(x, y));
            }

            // Skip to the end of the record
            br.BaseStream.Position = startPosition + contentLength;
        }

        /// <summary>
        /// Reads a polyline shape.
        /// </summary>
        private void ReadPolyLine(BinaryReader br, ShapefileData data, int contentLength, long startPosition)
        {
            // Skip bounding box (4 doubles = 32 bytes)
            br.BaseStream.Position += 32;

            // Read number of parts (line segments) and points
            int numParts = br.ReadInt32();
            int numPoints = br.ReadInt32();

            // Read part indices (where each line segment starts)
            int[] partIndices = new int[numParts];
            for (int i = 0; i < numParts; i++)
            {
                partIndices[i] = br.ReadInt32();
            }

            // Read all points
            PointD[] allPoints = new PointD[numPoints];
            for (int i = 0; i < numPoints; i++)
            {
                double x = br.ReadDouble();
                double y = br.ReadDouble();
                allPoints[i] = new PointD(x, y);
            }

            // Create line geometries for each part
            for (int part = 0; part < numParts; part++)
            {
                int startIndex = partIndices[part];
                int endIndex = (part < numParts - 1) ? partIndices[part + 1] : numPoints;
                int count = endIndex - startIndex;

                LineD line = new LineD();
                for (int i = 0; i < count; i++)
                {
                    line.AddPoint(allPoints[startIndex + i]);
                }

                data.Lines.Add(line);
            }

            // Skip to the end of the record
            br.BaseStream.Position = startPosition + contentLength;
        }

        /// <summary>
        /// Reads a polygon shape.
        /// </summary>
        private void ReadPolygon(BinaryReader br, ShapefileData data, int contentLength, long startPosition)
        {
            // Skip bounding box (4 doubles = 32 bytes)
            br.BaseStream.Position += 32;

            // Read number of parts (rings) and points
            int numParts = br.ReadInt32();
            int numPoints = br.ReadInt32();

            // Read part indices (where each ring starts)
            int[] partIndices = new int[numParts];
            for (int i = 0; i < numParts; i++)
            {
                partIndices[i] = br.ReadInt32();
            }

            // Read all points
            PointD[] allPoints = new PointD[numPoints];
            for (int i = 0; i < numPoints; i++)
            {
                double x = br.ReadDouble();
                double y = br.ReadDouble();
                allPoints[i] = new PointD(x, y);
            }

            // Create polygon geometries for each part
            // Note: In shapefiles, polygons may contain multiple rings where
            // the first is the exterior and the rest are holes, or there might be
            // multiple separate polygons. We'll create separate polygons for each ring.
            for (int part = 0; part < numParts; part++)
            {
                int startIndex = partIndices[part];
                int endIndex = (part < numParts - 1) ? partIndices[part + 1] : numPoints;
                int count = endIndex - startIndex;

                // Skip rings with too few points
                if (count < 3)
                    continue;

                PolygonD polygon = new PolygonD();
                for (int i = 0; i < count; i++)
                {
                    polygon.AddVertex(allPoints[startIndex + i]);
                }

                // Check if this is a hole (counter-clockwise winding)
                // For simplicity, we're adding all rings as separate polygons
                // A more sophisticated implementation would detect holes and associate them with shells
                data.Polygons.Add(polygon);
            }

            // Skip to the end of the record
            br.BaseStream.Position = startPosition + contentLength;
        }

        /// <summary>
        /// Reads the DBF file and returns a DataTable with the attribute data.
        /// </summary>
        private DataTable ReadDBF()
        {
            DataTable table = new DataTable();

            try
            {
                using (FileStream fs = new FileStream(dbfFilePath, FileMode.Open, FileAccess.Read))
                using (BinaryReader br = new BinaryReader(fs))
                {
                    // Read header
                    byte version = br.ReadByte();

                    // Read last update date (3 bytes: YY MM DD)
                    byte year = br.ReadByte();
                    byte month = br.ReadByte();
                    byte day = br.ReadByte();

                    // Read number of records
                    int numRecords = br.ReadInt32();

                    // Read header size and record size
                    short headerSize = br.ReadInt16();
                    short recordSize = br.ReadInt16();

                    // Skip 20 bytes (reserved)
                    br.BaseStream.Position += 20;

                    // Calculate number of fields (header size - 32) / 32
                    int numFields = (headerSize - 32) / 32;

                    // Read field descriptors
                    List<DBFField> fields = new List<DBFField>();
                    for (int i = 0; i < numFields; i++)
                    {
                        DBFField field = new DBFField();

                        // Read field name (11 bytes, null-terminated)
                        byte[] nameBytes = br.ReadBytes(11);
                        int nullPos = Array.IndexOf(nameBytes, (byte)0);
                        field.Name = Encoding.ASCII.GetString(nameBytes, 0, nullPos > 0 ? nullPos : nameBytes.Length).Trim();

                        // Read field type (1 byte)
                        field.Type = (char)br.ReadByte();

                        // Skip field data address (4 bytes)
                        br.BaseStream.Position += 4;

                        // Read field length and decimal count
                        field.Length = br.ReadByte();
                        field.DecimalCount = br.ReadByte();

                        // Skip 14 bytes (reserved)
                        br.BaseStream.Position += 14;

                        fields.Add(field);

                        // Add column to the data table
                        DataColumn column = new DataColumn(field.Name);
                        switch (field.Type)
                        {
                            case 'N': // Numeric
                                if (field.DecimalCount > 0)
                                    column.DataType = typeof(double);
                                else
                                    column.DataType = typeof(int);
                                break;
                            case 'F': // Float
                                column.DataType = typeof(double);
                                break;
                            case 'D': // Date
                                column.DataType = typeof(DateTime);
                                break;
                            case 'L': // Logical (boolean)
                                column.DataType = typeof(bool);
                                break;
                            case 'M': // Memo
                            case 'C': // Character
                            default:
                                column.DataType = typeof(string);
                                break;
                        }

                        table.Columns.Add(column);
                    }

                    // Check for header terminator (0x0D)
                    byte terminator = br.ReadByte();
                    if (terminator != 0x0D)
                    {
                        throw new FormatException("Invalid DBF format: Header terminator not found.");
                    }

                    // Read records
                    for (int i = 0; i < numRecords; i++)
                    {
                        DataRow row = table.NewRow();

                        // Read deleted flag
                        byte deletedFlag = br.ReadByte();
                        bool isDeleted = deletedFlag == 0x2A; // '*' means record is deleted

                        if (!isDeleted)
                        {
                            // Read field values
                            for (int j = 0; j < fields.Count; j++)
                            {
                                DBFField field = fields[j];
                                byte[] valueBytes = br.ReadBytes(field.Length);
                                string valueString = Encoding.ASCII.GetString(valueBytes).Trim();

                                // Convert to appropriate type
                                object value = DBNull.Value;
                                if (!string.IsNullOrWhiteSpace(valueString))
                                {
                                    switch (field.Type)
                                    {
                                        case 'N': // Numeric
                                            if (field.DecimalCount > 0)
                                            {
                                                double doubleValue;
                                                if (double.TryParse(valueString, out doubleValue))
                                                    value = doubleValue;
                                            }
                                            else
                                            {
                                                int intValue;
                                                if (int.TryParse(valueString, out intValue))
                                                    value = intValue;
                                            }
                                            break;
                                        case 'F': // Float
                                            double floatValue;
                                            if (double.TryParse(valueString, out floatValue))
                                                value = floatValue;
                                            break;
                                        case 'D': // Date (YYYYMMDD)
                                            if (valueString.Length == 8)
                                            {
                                                try
                                                {
                                                    int yearValue = int.Parse(valueString.Substring(0, 4));
                                                    int monthValue = int.Parse(valueString.Substring(4, 2));
                                                    int dayValue = int.Parse(valueString.Substring(6, 2));
                                                    value = new DateTime(yearValue, monthValue, dayValue);
                                                }
                                                catch { }
                                            }
                                            break;
                                        case 'L': // Logical (boolean)
                                            char logicalChar = valueString.Length > 0 ? valueString[0] : ' ';
                                            if ("YyTt1".Contains(logicalChar))
                                                value = true;
                                            else if ("NnFf0".Contains(logicalChar))
                                                value = false;
                                            break;
                                        case 'M': // Memo
                                        case 'C': // Character
                                        default:
                                            value = valueString;
                                            break;
                                    }
                                }

                                row[j] = value;
                            }

                            table.Rows.Add(row);
                        }
                        else
                        {
                            // Skip deleted record
                            br.BaseStream.Position += recordSize - 1;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading DBF file: {ex.Message}");
                // Return an empty table rather than null
                table = new DataTable();
            }

            return table;
        }

        /// <summary>
        /// Imports point geometry data to the given point layer.
        /// </summary>
        private void ImportPointsToLayer(Layer layer, ShapefileData data)
        {
            if (layer.Type != LayerType.Point)
                throw new ArgumentException("Layer must be a point layer.");

            // Add all points to the layer
            layer.AddPoints(data.Points);
        }

        /// <summary>
        /// Imports line geometry data to the given line layer.
        /// </summary>
        private void ImportLinesToLayer(Layer layer, ShapefileData data)
        {
            if (layer.Type != LayerType.Line)
                throw new ArgumentException("Layer must be a line layer.");

            // Add all lines to the layer
            layer.AddLines(data.Lines);
        }

        /// <summary>
        /// Imports polygon geometry data to the given polygon layer.
        /// </summary>
        private void ImportPolygonsToLayer(Layer layer, ShapefileData data)
        {
            if (layer.Type != LayerType.Polygon)
                throw new ArgumentException("Layer must be a polygon layer.");

            // Add all polygons to the layer
            layer.AddPolygons(data.Polygons);
        }

        /// <summary>
        /// Swaps the byte order of an integer (between big-endian and little-endian).
        /// </summary>
        private int SwapEndian(int value)
        {
            return ((value & 0xFF) << 24) |
                   ((value & 0xFF00) << 8) |
                   ((value & 0xFF0000) >> 8) |
                   ((int)((uint)(value & 0xFF000000) >> 24));
        }

        /// <summary>
        /// Associates each geometry with attribute data from the DBF file.
        /// </summary>
        /// <param name="layerManager">The layer manager containing the imported layer.</param>
        /// <param name="layerName">The name of the imported layer.</param>
        /// <param name="idField">The field in the DBF file to use as an identifier.</param>
        public void AssociateAttributes(LayerManager layerManager, string layerName, string idField = null)
        {
            if (attributeTable == null || attributeTable.Rows.Count == 0)
                return;

            // Get the layer
            Layer layer = layerManager.GetLayer(layerName);
            if (layer == null)
                return;

            // Associate the attribute table with the layer
            layer.AttributeData = attributeTable;

            // Create a mapping between geometry IDs and attribute rows
            Dictionary<int, int> geometryToAttributeMap = new Dictionary<int, int>();

            // Copy the record-to-geometry mapping (it's already in the right format)
            foreach (var kvp in recordToGeometryMap)
            {
                geometryToAttributeMap[kvp.Key] = kvp.Value;
            }

            // Set the mapping on the layer
            layer.GeometryToAttributeMap = geometryToAttributeMap;

            // Enable labels by default if the layer has attribute data
            layer.Style.ShowLabels = true;
        }

        /// <summary>
        /// A helper class to store shapefile field information.
        /// </summary>
        private class DBFField
        {
            public string Name { get; set; }
            public char Type { get; set; }
            public byte Length { get; set; }
            public byte DecimalCount { get; set; }
        }

        /// <summary>
        /// A class to store the data read from a shapefile.
        /// </summary>
        private class ShapefileData
        {
            public int ShapeType { get; set; }
            public double XMin { get; set; }
            public double YMin { get; set; }
            public double XMax { get; set; }
            public double YMax { get; set; }
            public List<PointD> Points { get; } = new List<PointD>();
            public List<LineD> Lines { get; } = new List<LineD>();
            public List<PolygonD> Polygons { get; } = new List<PolygonD>();
        }
    }
}