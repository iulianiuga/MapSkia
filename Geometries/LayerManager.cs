using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FCoreMap.Geometries
{
    public class LayerManager
    {
        private List<Layer> layers;
        private Dictionary<string, int> layerIndices;

        public LayerManager()
        {
            layers = new List<Layer>();
            layerIndices = new Dictionary<string, int>();
        }

        // Add a new layer
        public bool AddLayer(Layer layer)
        {
            // Check if a layer with the same name already exists
            if (layerIndices.ContainsKey(layer.Name))
            {
                return false;
            }

            // Add the layer
            layers.Add(layer);
            layerIndices[layer.Name] = layers.Count - 1;
            return true;
        }

        // Create and add a new layer with default style
        public Layer CreateLayer(string name, LayerType type)
        {
            Layer layer = new Layer(name, type);
            if (AddLayer(layer))
                return layer;
            return null;
        }

        // Create and add a new layer with custom style
        public Layer CreateLayer(string name, LayerType type, LayerStyle style)
        {
            Layer layer = new Layer(name, type, style);
            if (AddLayer(layer))
                return layer;
            return null;
        }

        // Create and add a circle layer with default style
        public Layer CreateCircleLayer(string name)
        {
            return CreateLayer(name, LayerType.Circle);
        }

        // Create and add a circle layer with custom style
        public Layer CreateCircleLayer(string name, LayerStyle style)
        {
            return CreateLayer(name, LayerType.Circle, style);
        }

        // Remove a layer by name
        public bool RemoveLayer(string layerName)
        {
            if (!layerIndices.TryGetValue(layerName, out int index))
            {
                return false;
            }

            // Remove the layer
            layers.RemoveAt(index);

            // Update the indices dictionary
            layerIndices.Clear();
            for (int i = 0; i < layers.Count; i++)
            {
                layerIndices[layers[i].Name] = i;
            }

            return true;
        }

        // Get a layer by name
        public Layer GetLayer(string layerName)
        {
            if (layerIndices.TryGetValue(layerName, out int index))
            {
                return layers[index];
            }
            return null;
        }

        // Get a layer by index
        public Layer GetLayerAt(int index)
        {
            if (index >= 0 && index < layers.Count)
            {
                return layers[index];
            }
            return null;
        }

        // Get all layers
        public IReadOnlyList<Layer> GetLayers()
        {
            return layers.AsReadOnly();
        }

        // Get layer by name (alias for GetLayer for consistency)
        public Layer GetLayerByName(string layerName)
        {
            return GetLayer(layerName);
        }

        // Get layers by name pattern (contains, starts with, etc.)
        public IEnumerable<Layer> GetLayersByNamePattern(string pattern, bool ignoreCase = true)
        {
            StringComparison comparison = ignoreCase ?
                StringComparison.OrdinalIgnoreCase :
                StringComparison.Ordinal;

            return layers.Where(layer => layer.Name.Contains(pattern, comparison));
        }

        // Get all circle layers
        public IEnumerable<Layer> GetCircleLayers()
        {
            return layers.Where(layer => layer.Type == LayerType.Circle);
        }

        // Get layers by type
        public IEnumerable<Layer> GetLayersByType(LayerType type)
        {
            return layers.Where(layer => layer.Type == type);
        }

        // Remove all layers
        public void RemoveLayers()
        {
            layers.Clear();
            layerIndices.Clear();
        }

        // Remove multiple layers by name
        public int RemoveLayers(IEnumerable<string> layerNames)
        {
            int count = 0;
            foreach (string name in layerNames)
            {
                if (RemoveLayer(name))
                {
                    count++;
                }
            }
            return count;
        }

        // Remove layers by predicate
        public int RemoveLayersByPredicate(Func<Layer, bool> predicate)
        {
            var layersToRemove = layers.Where(predicate).ToList();
            int count = layersToRemove.Count;

            foreach (var layer in layersToRemove)
            {
                RemoveLayer(layer.Name);
            }

            return count;
        }

        // Get the number of layers
        public int LayerCount => layers.Count;

        // Move a layer up in the stack (decrease its index)
        public bool MoveLayerUp(string layerName)
        {
            if (!layerIndices.TryGetValue(layerName, out int index))
            {
                return false;
            }

            if (index <= 0)
            {
                return false; // Already at the top
            }

            // Swap the layer with the one above it
            Layer temp = layers[index];
            layers[index] = layers[index - 1];
            layers[index - 1] = temp;

            // Update the indices
            layerIndices[layers[index].Name] = index;
            layerIndices[layers[index - 1].Name] = index - 1;

            return true;
        }

        // Move a layer down in the stack (increase its index)
        public bool MoveLayerDown(string layerName)
        {
            if (!layerIndices.TryGetValue(layerName, out int index))
            {
                return false;
            }

            if (index >= layers.Count - 1)
            {
                return false; // Already at the bottom
            }

            // Swap the layer with the one below it
            Layer temp = layers[index];
            layers[index] = layers[index + 1];
            layers[index + 1] = temp;

            // Update the indices
            layerIndices[layers[index].Name] = index;
            layerIndices[layers[index + 1].Name] = index + 1;

            return true;
        }

        // Move a layer to a specific position in the stack
        public bool MoveLayerToPosition(string layerName, int newPosition)
        {
            if (!layerIndices.TryGetValue(layerName, out int currentIndex))
            {
                return false;
            }

            // Make sure the new position is valid
            if (newPosition < 0 || newPosition >= layers.Count)
            {
                return false;
            }

            // No change needed if already at this position
            if (currentIndex == newPosition)
            {
                return true;
            }

            // Get the layer to move
            Layer layerToMove = layers[currentIndex];

            // Remove from current position
            layers.RemoveAt(currentIndex);

            // Insert at new position
            layers.Insert(newPosition, layerToMove);

            // Update all indices
            layerIndices.Clear();
            for (int i = 0; i < layers.Count; i++)
            {
                layerIndices[layers[i].Name] = i;
            }

            return true;
        }

        // Set a layer's visibility
        public bool SetLayerVisibility(string layerName, bool visible)
        {
            Layer layer = GetLayer(layerName);
            if (layer == null)
            {
                return false;
            }

            layer.Visible = visible;
            return true;
        }

        // Set visibility for all layers
        public void SetAllLayersVisibility(bool visible)
        {
            foreach (var layer in layers)
            {
                layer.Visible = visible;
            }
        }

        // Set visibility for layers of a specific type
        public int SetLayerVisibilityByType(LayerType type, bool visible)
        {
            int count = 0;
            foreach (var layer in layers)
            {
                if (layer.Type == type)
                {
                    layer.Visible = visible;
                    count++;
                }
            }
            return count;
        }

        #region Style Management

        // Set the style for a layer
        public bool SetLayerStyle(string layerName, LayerStyle style)
        {
            Layer layer = GetLayer(layerName);
            if (layer == null)
            {
                return false;
            }

            layer.Style = style;
            return true;
        }

        // Get the style of a layer
        public LayerStyle GetLayerStyle(string layerName)
        {
            Layer layer = GetLayer(layerName);
            if (layer == null)
            {
                return null;
            }

            return layer.Style;
        }

        // Create a point style and apply it to a layer
        public bool ApplyPointStyle(string layerName, Color color, float size, PointShape shape)
        {
            Layer layer = GetLayer(layerName);
            if (layer == null || layer.Type != LayerType.Point)
            {
                return false;
            }

            layer.Style = LayerStyle.CreatePointStyle(color, size, shape);
            return true;
        }

        // Create a line style and apply it to a layer
        public bool ApplyLineStyle(string layerName, Color color, float width, LineStyle style)
        {
            Layer layer = GetLayer(layerName);
            if (layer == null || layer.Type != LayerType.Line)
            {
                return false;
            }

            layer.Style = LayerStyle.CreateLineStyle(color, width, style);
            return true;
        }

        // Create a polygon style and apply it to a layer
        public bool ApplyPolygonStyle(string layerName, Color fillColor, Color outlineColor, FillPattern fillPattern, float opacity)
        {
            Layer layer = GetLayer(layerName);
            if (layer == null || layer.Type != LayerType.Polygon)
            {
                return false;
            }

            layer.Style = LayerStyle.CreatePolygonStyle(fillColor, outlineColor, fillPattern, opacity);
            return true;
        }

        // Create a circle style and apply it to a layer
        public bool ApplyCircleStyle(string layerName, Color fillColor, Color outlineColor, FillPattern fillPattern, float opacity)
        {
            Layer layer = GetLayer(layerName);
            if (layer == null || layer.Type != LayerType.Circle)
            {
                return false;
            }

            layer.Style = LayerStyle.CreateCircleStyle(fillColor, outlineColor, fillPattern, opacity);
            return true;
        }

        // Update specific style properties
        public bool UpdateLayerColor(string layerName, Color color)
        {
            Layer layer = GetLayer(layerName);
            if (layer == null || layer.Style == null)
            {
                return false;
            }

            layer.Style.Color = color;
            return true;
        }

        // Update outline properties
        public bool UpdateLayerOutline(string layerName, Color outlineColor, float outlineWidth)
        {
            Layer layer = GetLayer(layerName);
            if (layer == null || layer.Style == null)
            {
                return false;
            }

            layer.Style.OutlineColor = outlineColor;
            layer.Style.OutlineWidth = outlineWidth;
            return true;
        }

        // Update opacity
        public bool UpdateLayerOpacity(string layerName, float opacity)
        {
            Layer layer = GetLayer(layerName);
            if (layer == null || layer.Style == null)
            {
                return false;
            }

            // Ensure opacity is within valid range
            opacity = Math.Max(0.0f, Math.Min(1.0f, opacity));
            layer.Style.Opacity = opacity;
            return true;
        }

        // Update fill pattern
        public bool UpdateLayerFillPattern(string layerName, FillPattern fillPattern)
        {
            Layer layer = GetLayer(layerName);
            if (layer == null || layer.Style == null)
            {
                return false;
            }

            layer.Style.FillPattern = fillPattern;
            return true;
        }

        // Update fill visibility
        public bool UpdateLayerFillVisibility(string layerName, bool showFill)
        {
            Layer layer = GetLayer(layerName);
            if (layer == null || layer.Style == null)
            {
                return false;
            }

            layer.Style.ShowFill = showFill;
            return true;
        }

        // Update label visibility
        public bool SetLayerLabelsVisible(string layerName, bool visible, string labelField = null)
        {
            Layer layer = GetLayer(layerName);
            if (layer == null || layer.Style == null)
            {
                return false;
            }

            layer.Style.ShowLabels = visible;
            if (labelField != null)
            {
                layer.Style.LabelField = labelField;
            }
            return true;
        }

        // Configure labels for a layer
        public bool ConfigureLayerLabels(string layerName, bool showLabels, string labelField, Color labelColor, float fontSize = 8)
        {
            Layer layer = GetLayer(layerName);
            if (layer == null || layer.Style == null)
            {
                return false;
            }

            layer.Style.ConfigureLabels(showLabels, labelField, labelColor, fontSize);
            return true;
        }

        // Set label position
        public bool SetLayerLabelPosition(string layerName, LabelPosition position)
        {
            Layer layer = GetLayer(layerName);
            if (layer == null || layer.Style == null)
            {
                return false;
            }

            layer.Style.LabelPosition = position;
            return true;
        }

        // Get layers with a specific style property
        public IEnumerable<Layer> GetLayersByStyleProperty<T>(Func<LayerStyle, T> propertySelector, T value)
        {
            return layers.Where(layer =>
                layer.Style != null &&
                EqualityComparer<T>.Default.Equals(propertySelector(layer.Style), value));
        }

        // Get layers with a specific point shape
        public IEnumerable<Layer> GetLayersByPointShape(PointShape shape)
        {
            return GetLayersByStyleProperty(style => style.PointShape, shape);
        }

        // Get layers with a specific line style
        public IEnumerable<Layer> GetLayersByLineStyle(LineStyle lineStyle)
        {
            return GetLayersByStyleProperty(style => style.LineStyle, lineStyle);
        }

        // Get layers with a specific fill pattern
        public IEnumerable<Layer> GetLayersByFillPattern(FillPattern pattern)
        {
            return GetLayersByStyleProperty(style => style.FillPattern, pattern);
        }
        #endregion

        // Clone a layer with a new name
        public Layer CloneLayer(string sourceLayerName, string newLayerName)
        {
            // Get the source layer
            Layer sourceLayer = GetLayer(sourceLayerName);
            if (sourceLayer == null)
            {
                return null;
            }

            // Check if the new name is already in use
            if (layerIndices.ContainsKey(newLayerName))
            {
                return null;
            }

            // Create a new layer with the same type and style
            LayerStyle clonedStyle = sourceLayer.Style.Clone();
            Layer newLayer = new Layer(newLayerName, sourceLayer.Type, clonedStyle);

            // Copy geometries based on the layer type
            switch (sourceLayer.Type)
            {
                case LayerType.Point:
                    var points = sourceLayer.GetPoints();
                    if (points != null && points.Count > 0)
                    {
                        // Create new points with same coordinates
                        List<PointD> newPoints = new List<PointD>();
                        foreach (var point in points)
                        {
                            newPoints.Add(new PointD(point.X, point.Y));
                        }
                        newLayer.AddPoints(newPoints);
                    }
                    break;

                case LayerType.Line:
                    var lines = sourceLayer.GetLines();
                    if (lines != null && lines.Count > 0)
                    {
                        // Create new lines with same points
                        foreach (var line in lines)
                        {
                            LineD newLine = new LineD();
                            foreach (var point in line.Points)
                            {
                                newLine.AddPoint(new PointD(point.X, point.Y));
                            }
                            newLayer.AddLine(newLine);
                        }
                    }
                    break;

                case LayerType.Polygon:
                    var polygons = sourceLayer.GetPolygons();
                    if (polygons != null && polygons.Count > 0)
                    {
                        // Create new polygons with same vertices
                        foreach (var polygon in polygons)
                        {
                            PolygonD newPolygon = new PolygonD();
                            foreach (var vertex in polygon.Vertices)
                            {
                                newPolygon.AddVertex(new PointD(vertex.X, vertex.Y));
                            }
                            newLayer.AddPolygon(newPolygon);
                        }
                    }
                    break;

                case LayerType.Circle:
                    var circles = sourceLayer.GetCircles();
                    if (circles != null && circles.Count > 0)
                    {
                        // Create new circles with same properties
                        foreach (var circle in circles)
                        {
                            CircleD newCircle = new CircleD(
                                new PointD(circle.Center.X, circle.Center.Y),
                                circle.Radius,
                                circle.Elevation
                            );
                            newLayer.AddCircle(newCircle);
                        }
                    }
                    break;
            }

            // Copy attribute data if it exists
            if (sourceLayer.AttributeData != null)
            {
                // Clone the DataTable
                newLayer.AttributeData = sourceLayer.AttributeData.Copy();

                // Copy the geometry-to-attribute mapping
                Dictionary<int, int> newMapping = new Dictionary<int, int>();
                foreach (var kvp in sourceLayer.GeometryToAttributeMap)
                {
                    newMapping[kvp.Key] = kvp.Value;
                }
                newLayer.GeometryToAttributeMap = newMapping;
            }

            // Add the new layer
            if (AddLayer(newLayer))
            {
                return newLayer;
            }

            return null;
        }

        // Merge multiple layers of the same type into a single layer
        public Layer MergeLayers(IEnumerable<string> layerNames, string newLayerName)
        {
            // Check if the target name is already in use
            if (layerIndices.ContainsKey(newLayerName))
            {
                return null;
            }

            // Get the layers to merge
            List<Layer> layersToMerge = new List<Layer>();
            foreach (string name in layerNames)
            {
                Layer layer = GetLayer(name);
                if (layer != null)
                {
                    layersToMerge.Add(layer);
                }
            }

            // Make sure we have at least one layer
            if (layersToMerge.Count == 0)
            {
                return null;
            }

            // All layers must be the same type
            LayerType layerType = layersToMerge[0].Type;
            if (layersToMerge.Any(l => l.Type != layerType))
            {
                return null;
            }

            // Create the new merged layer
            Layer mergedLayer = new Layer(newLayerName, layerType);

            // Add all geometries to the new layer
            int geometryIndex = 0;
            Dictionary<int, int> mergedMapping = new Dictionary<int, int>();

            // Create a new DataTable for merged attributes
            DataTable mergedAttributes = null;

            switch (layerType)
            {
                case LayerType.Point:
                    foreach (var layer in layersToMerge)
                    {
                        var points = layer.GetPoints();
                        if (points != null && points.Count > 0)
                        {
                            // Create new points with same coordinates
                            List<PointD> newPoints = new List<PointD>();
                            foreach (var point in points)
                            {
                                newPoints.Add(new PointD(point.X, point.Y));

                                // Copy attribute mapping if exists
                                if (layer.AttributeData != null && layer.GeometryToAttributeMap.TryGetValue(point.Id, out int attrIdx))
                                {
                                    // Initialize merged attributes if needed
                                    if (mergedAttributes == null)
                                    {
                                        mergedAttributes = layer.AttributeData.Clone();
                                    }

                                    // Add attribute row
                                    DataRow sourceRow = layer.AttributeData.Rows[attrIdx];
                                    DataRow newRow = mergedAttributes.NewRow();
                                    foreach (DataColumn col in mergedAttributes.Columns)
                                    {
                                        if (layer.AttributeData.Columns.Contains(col.ColumnName))
                                            newRow[col.ColumnName] = sourceRow[col.ColumnName];
                                    }
                                    mergedAttributes.Rows.Add(newRow);

                                    // Map to the new attribute index
                                    mergedMapping[geometryIndex] = mergedAttributes.Rows.Count - 1;
                                }

                                geometryIndex++;
                            }
                            mergedLayer.AddPoints(newPoints);
                        }
                    }
                    break;

                case LayerType.Line:
                    foreach (var layer in layersToMerge)
                    {
                        var lines = layer.GetLines();
                        if (lines != null && lines.Count > 0)
                        {
                            foreach (var line in lines)
                            {
                                LineD newLine = new LineD();
                                foreach (var point in line.Points)
                                {
                                    newLine.AddPoint(new PointD(point.X, point.Y));
                                }
                                mergedLayer.AddLine(newLine);

                                // Copy attribute mapping if exists
                                if (layer.AttributeData != null && layer.GeometryToAttributeMap.TryGetValue(line.Id, out int attrIdx))
                                {
                                    // Initialize merged attributes if needed
                                    if (mergedAttributes == null)
                                    {
                                        mergedAttributes = layer.AttributeData.Clone();
                                    }

                                    // Add attribute row
                                    DataRow sourceRow = layer.AttributeData.Rows[attrIdx];
                                    DataRow newRow = mergedAttributes.NewRow();
                                    foreach (DataColumn col in mergedAttributes.Columns)
                                    {
                                        if (layer.AttributeData.Columns.Contains(col.ColumnName))
                                            newRow[col.ColumnName] = sourceRow[col.ColumnName];
                                    }
                                    mergedAttributes.Rows.Add(newRow);

                                    // Map to the new attribute index
                                    mergedMapping[geometryIndex] = mergedAttributes.Rows.Count - 1;
                                }

                                geometryIndex++;
                            }
                        }
                    }
                    break;

                case LayerType.Polygon:
                    foreach (var layer in layersToMerge)
                    {
                        var polygons = layer.GetPolygons();
                        if (polygons != null && polygons.Count > 0)
                        {
                            foreach (var polygon in polygons)
                            {
                                PolygonD newPolygon = new PolygonD();
                                foreach (var vertex in polygon.Vertices)
                                {
                                    newPolygon.AddVertex(new PointD(vertex.X, vertex.Y));
                                }
                                mergedLayer.AddPolygon(newPolygon);

                                // Copy attribute mapping if exists
                                if (layer.AttributeData != null && layer.GeometryToAttributeMap.TryGetValue(polygon.Id, out int attrIdx))
                                {
                                    // Initialize merged attributes if needed
                                    if (mergedAttributes == null)
                                    {
                                        mergedAttributes = layer.AttributeData.Clone();
                                    }

                                    // Add attribute row
                                    DataRow sourceRow = layer.AttributeData.Rows[attrIdx];
                                    DataRow newRow = mergedAttributes.NewRow();
                                    foreach (DataColumn col in mergedAttributes.Columns)
                                    {
                                        if (layer.AttributeData.Columns.Contains(col.ColumnName))
                                            newRow[col.ColumnName] = sourceRow[col.ColumnName];
                                    }
                                    mergedAttributes.Rows.Add(newRow);

                                    // Map to the new attribute index
                                    mergedMapping[geometryIndex] = mergedAttributes.Rows.Count - 1;
                                }

                                geometryIndex++;
                            }
                        }
                    }
                    break;

                case LayerType.Circle:
                    foreach (var layer in layersToMerge)
                    {
                        var circles = layer.GetCircles();
                        if (circles != null && circles.Count > 0)
                        {
                            foreach (var circle in circles)
                            {
                                CircleD newCircle = new CircleD(
                                    new PointD(circle.Center.X, circle.Center.Y),
                                    circle.Radius,
                                    circle.Elevation
                                );
                                mergedLayer.AddCircle(newCircle);

                                // Copy attribute mapping if exists
                                if (layer.AttributeData != null && layer.GeometryToAttributeMap.TryGetValue(circle.Id, out int attrIdx))
                                {
                                    // Initialize merged attributes if needed
                                    if (mergedAttributes == null)
                                    {
                                        mergedAttributes = layer.AttributeData.Clone();
                                    }

                                    // Add attribute row
                                    DataRow sourceRow = layer.AttributeData.Rows[attrIdx];
                                    DataRow newRow = mergedAttributes.NewRow();
                                    foreach (DataColumn col in mergedAttributes.Columns)
                                    {
                                        if (layer.AttributeData.Columns.Contains(col.ColumnName))
                                            newRow[col.ColumnName] = sourceRow[col.ColumnName];
                                    }
                                    mergedAttributes.Rows.Add(newRow);

                                    // Map to the new attribute index
                                    mergedMapping[geometryIndex] = mergedAttributes.Rows.Count - 1;
                                }

                                geometryIndex++;
                            }
                        }
                    }
                    break;
            }

            // Assign merged attributes and mapping
            if (mergedAttributes != null)
            {
                mergedLayer.AttributeData = mergedAttributes;
                mergedLayer.GeometryToAttributeMap = mergedMapping;
            }

            // Use the style from the first layer
            mergedLayer.Style = layersToMerge[0].Style.Clone();

            // Add the merged layer
            if (AddLayer(mergedLayer))
            {
                return mergedLayer;
            }

            return null;
        }

        // Calculate total features across all layers
        public int CountTotalFeatures()
        {
            int count = 0;
            foreach (var layer in layers)
            {
                count += layer.Count;
            }
            return count;
        }

        // Calculate total features by layer type
        public int CountFeaturesByType(LayerType type)
        {
            int count = 0;
            foreach (var layer in layers)
            {
                if (layer.Type == type)
                {
                    count += layer.Count;
                }
            }
            return count;
        }

        // Get a string summary of all layers
        public string GetLayersSummary()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Layer Manager - {layers.Count} layers");

            foreach (var layer in layers)
            {
                sb.AppendLine($"- {layer.Name} ({layer.Type}): {layer.Count} features, Visible: {layer.Visible}");

                switch (layer.Type)
                {
                    case LayerType.Point:
                        sb.AppendLine($"  Points: {layer.GetPointCount()}");
                        break;
                    case LayerType.Line:
                        sb.AppendLine($"  Lines: {layer.GetLineCount()}, Total Length: {layer.CalculateTotalLength():F2}");
                        break;
                    case LayerType.Polygon:
                        sb.AppendLine($"  Polygons: {layer.GetPolygonCount()}, Total Area: {layer.CalculateTotalArea():F2}");
                        break;
                    case LayerType.Circle:
                        sb.AppendLine($"  Circles: {layer.GetCircleCount()}, Total Area: {layer.CalculateTotalArea():F2}");
                        break;
                }

                if (layer.AttributeData != null)
                {
                    sb.AppendLine($"  Attributes: {layer.AttributeData.Columns.Count} columns, {layer.AttributeData.Rows.Count} rows");
                }
            }

            return sb.ToString();
        }
    }
}