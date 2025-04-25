using System;
using System.IO;
using System.Drawing;
using FCoreMap.Geometries;

namespace FCoreMap.IO
{
    /// <summary>
    /// Extension methods for working with shapefiles.
    /// </summary>
    public static class ShapeFileExtensions
    {
        /// <summary>
        /// Imports a shapefile directly into a layer manager.
        /// </summary>
        /// <param name="layerManager">The layer manager to add the imported layer to.</param>
        /// <param name="shapeFilePath">Path to the shapefile (.shp).</param>
        /// <param name="layerName">Optional name for the layer. If null, uses the filename.</param>
        /// <param name="enableLabels">Whether to enable labels for the imported layer.</param>
        /// <returns>The created layer.</returns>
        public static Layer ImportShapefile(this LayerManager layerManager, string shapeFilePath, string layerName = null, bool enableLabels = true)
        {
            ShapeFileImporter importer = new ShapeFileImporter(shapeFilePath);
            Layer layer = importer.ImportToLayer(layerManager, layerName);

            // Enable or disable labels based on parameter
            if (layer != null)
            {
                layer.Style.ShowLabels = enableLabels;
            }

            return layer;
        }

        /// <summary>
        /// Imports a shapefile with the specified style.
        /// </summary>
        /// <param name="layerManager">The layer manager to add the imported layer to.</param>
        /// <param name="shapeFilePath">Path to the shapefile (.shp).</param>
        /// <param name="layerStyle">The style to apply to the imported layer.</param>
        /// <param name="layerName">Optional name for the layer. If null, uses the filename.</param>
        /// <param name="labelField">The field from attribute data to use for labeling.</param>
        /// <returns>The created layer.</returns>
        public static Layer ImportShapefile(this LayerManager layerManager, string shapeFilePath, LayerStyle layerStyle, string layerName = null, string labelField = null)
        {
            // Import the shapefile
            Layer layer = layerManager.ImportShapefile(shapeFilePath, layerName);

            // Apply the style if the layer was created successfully
            if (layer != null)
            {
                layer.Style = layerStyle;

                // Set label field if specified
                if (!string.IsNullOrEmpty(labelField) && layer.AttributeData != null &&
                    layer.AttributeData.Columns.Contains(labelField))
                {
                    layer.Style.LabelField = labelField;
                }
            }

            return layer;
        }

        /// <summary>
        /// Imports multiple shapefiles from a directory.
        /// </summary>
        /// <param name="layerManager">The layer manager to add the imported layers to.</param>
        /// <param name="directory">Directory containing shapefiles.</param>
        /// <param name="searchPattern">Pattern to match shapefile names (e.g., "*.shp").</param>
        /// <param name="recursive">Whether to search subdirectories.</param>
        /// <param name="enableLabels">Whether to enable labels for the imported layers.</param>
        /// <returns>The number of layers successfully imported.</returns>
        public static int ImportShapefilesFromDirectory(this LayerManager layerManager, string directory,
            string searchPattern = "*.shp", bool recursive = false, bool enableLabels = true)
        {
            // Find all shapefiles matching the pattern
            string[] shapeFiles = Directory.GetFiles(directory, searchPattern,
                recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

            int importedCount = 0;

            // Import each shapefile
            foreach (string shapeFile in shapeFiles)
            {
                try
                {
                    // Use the filename as the layer name
                    string layerName = Path.GetFileNameWithoutExtension(shapeFile);

                    // Create a random color for the layer
                    Random rand = new Random(layerName.GetHashCode());
                    Color randomColor = Color.FromArgb(
                        rand.Next(50, 200),  // R
                        rand.Next(50, 200),  // G
                        rand.Next(50, 200)   // B
                    );

                    // Import the shapefile
                    Layer layer = layerManager.ImportShapefile(shapeFile, layerName);

                    // Set a random color for the layer
                    if (layer != null)
                    {
                        layer.Style.Color = randomColor;
                        layer.Style.ShowLabels = enableLabels;

                        // Find a good field for labels if labels are enabled
                        if (enableLabels && layer.AttributeData != null && layer.AttributeData.Columns.Count > 0)
                        {
                            // Try to find fields with common label names
                            string[] commonLabelFields = new[] { "NAME", "LABEL", "TITLE", "ID", "CODE", "DESC", "TYPE" };

                            bool foundField = false;
                            foreach (string fieldName in commonLabelFields)
                            {
                                foreach (System.Data.DataColumn column in layer.AttributeData.Columns)
                                {
                                    if (column.ColumnName.ToUpper().Contains(fieldName))
                                    {
                                        layer.Style.LabelField = column.ColumnName;
                                        foundField = true;
                                        break;
                                    }
                                }

                                if (foundField)
                                    break;
                            }

                            // If no match found, use the first column
                            if (!foundField && layer.AttributeData.Columns.Count > 0)
                            {
                                layer.Style.LabelField = layer.AttributeData.Columns[0].ColumnName;
                            }
                        }

                        importedCount++;
                    }
                }
                catch (Exception ex)
                {
                    // Log the error and continue with the next file
                    Console.WriteLine($"Error importing shapefile {shapeFile}: {ex.Message}");
                }
            }

            return importedCount;
        }

        /// <summary>
        /// Creates a style for the shapefile based on its geometry type.
        /// </summary>
        /// <param name="shapeFilePath">Path to the shapefile (.shp).</param>
        /// <param name="color">The color to use for the style.</param>
        /// <param name="enableLabels">Whether to enable labels for the style.</param>
        /// <returns>A suitable style for the shapefile's geometry type.</returns>
        public static LayerStyle CreateStyleForShapefile(string shapeFilePath, Color color, bool enableLabels = true)
        {
            // Determine the geometry type from the shapefile header
            int shapeType = ReadShapefileType(shapeFilePath);

            // Create an appropriate style
            LayerStyle style;

            switch (shapeType)
            {
                case 1:  // Point
                case 11: // PointZ
                case 21: // PointM
                case 8:  // MultiPoint
                case 18: // MultiPointZ
                case 28: // MultiPointM
                    style = LayerStyle.CreatePointStyle(color, 6.0f, PointShape.Circle);
                    break;

                case 3:  // Polyline
                case 13: // PolylineZ
                case 23: // PolylineM
                    style = LayerStyle.CreateLineStyle(color, 1.5f, LineStyle.Solid);
                    break;

                case 5:  // Polygon
                case 15: // PolygonZ
                case 25: // PolygonM
                    style = LayerStyle.CreatePolygonStyle(color, Color.Black, FillPattern.Solid, 0.5f);
                    break;

                default:
                    // Default style for unknown types
                    style = new LayerStyle(color);
                    break;
            }

            // Configure labels
            style.ShowLabels = enableLabels;

            return style;
        }

        /// <summary>
        /// Reads the shapefile type from the header.
        /// </summary>
        private static int ReadShapefileType(string shapeFilePath)
        {
            try
            {
                using (FileStream fs = new FileStream(shapeFilePath, FileMode.Open, FileAccess.Read))
                using (BinaryReader br = new BinaryReader(fs))
                {
                    // Shape type is at byte 32
                    fs.Position = 32;
                    return br.ReadInt32();
                }
            }
            catch (Exception)
            {
                // Return 0 (null shape) if we can't read the file
                return 0;
            }
        }
    }
}