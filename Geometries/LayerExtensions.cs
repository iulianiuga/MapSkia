using System;
using FCoreMap.Geometries;

namespace FCoreMap.Geometries
{
    /// <summary>
    /// Extension methods for the Layer class to calculate bounding boxes.
    /// </summary>
    public static class LayerExtensions
    {
        /// <summary>
        /// Calculates the bounding box of a layer.
        /// </summary>
        /// <param name="layer">The layer to compute the bounds for.</param>
        /// <param name="minX">Output minimum X coordinate.</param>
        /// <param name="minY">Output minimum Y coordinate.</param>
        /// <param name="maxX">Output maximum X coordinate.</param>
        /// <param name="maxY">Output maximum Y coordinate.</param>
        /// <returns>True if the bounds were successfully calculated, false if the layer is empty.</returns>
        public static bool CalculateBounds(this Layer layer, out double minX, out double minY, out double maxX, out double maxY)
        {
            minX = double.MaxValue;
            minY = double.MaxValue;
            maxX = double.MinValue;
            maxY = double.MinValue;
            bool hasPoints = false;

            switch (layer.Type)
            {
                case LayerType.Point:
                    var points = layer.GetPoints();
                    if (points != null && points.Count > 0)
                    {
                        foreach (var point in points)
                        {
                            UpdateBounds(point.X, point.Y, ref minX, ref minY, ref maxX, ref maxY);
                            hasPoints = true;
                        }
                    }
                    break;

                case LayerType.Line:
                    var lines = layer.GetLines();
                    if (lines != null && lines.Count > 0)
                    {
                        foreach (var line in lines)
                        {
                            foreach (var point in line.Points)
                            {
                                UpdateBounds(point.X, point.Y, ref minX, ref minY, ref maxX, ref maxY);
                                hasPoints = true;
                            }
                        }
                    }
                    break;

                case LayerType.Polygon:
                    var polygons = layer.GetPolygons();
                    if (polygons != null && polygons.Count > 0)
                    {
                        foreach (var polygon in polygons)
                        {
                            foreach (var vertex in polygon.Vertices)
                            {
                                UpdateBounds(vertex.X, vertex.Y, ref minX, ref minY, ref maxX, ref maxY);
                                hasPoints = true;
                            }
                        }
                    }
                    break;

                case LayerType.Circle:
                    var circles = layer.GetCircles();
                    if (circles != null && circles.Count > 0)
                    {
                        foreach (var circle in circles)
                        {
                            // For circles, use the bounding box which is already calculated
                            UpdateBounds(circle.BoundingBox.MinX, circle.BoundingBox.MinY, ref minX, ref minY, ref maxX, ref maxY);
                            UpdateBounds(circle.BoundingBox.MaxX, circle.BoundingBox.MaxY, ref minX, ref minY, ref maxX, ref maxY);
                            hasPoints = true;
                        }
                    }
                    break;
            }

            return hasPoints;
        }

        /// <summary>
        /// Updates the bounding box coordinates with the given point.
        /// </summary>
        private static void UpdateBounds(double x, double y, ref double minX, ref double minY, ref double maxX, ref double maxY)
        {
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
        }
    }
}

namespace FCoreMap.Geometries
{
    /// <summary>
    /// Extension methods for the LayerManager class to calculate bounding boxes.
    /// </summary>
    public static class LayerManagerExtensions
    {
        /// <summary>
        /// Calculates the combined bounding box of all visible layers.
        /// </summary>
        /// <param name="layerManager">The layer manager containing the layers.</param>
        /// <param name="minX">Output minimum X coordinate.</param>
        /// <param name="minY">Output minimum Y coordinate.</param>
        /// <param name="maxX">Output maximum X coordinate.</param>
        /// <param name="maxY">Output maximum Y coordinate.</param>
        /// <returns>True if the bounds were successfully calculated, false if there are no visible layers with data.</returns>
        public static bool CalculateVisibleLayersBounds(this LayerManager layerManager, out double minX, out double minY, out double maxX, out double maxY)
        {
            minX = double.MaxValue;
            minY = double.MaxValue;
            maxX = double.MinValue;
            maxY = double.MinValue;
            bool hasPoints = false;

            foreach (var layer in layerManager.GetLayers())
            {
                if (!layer.Visible)
                    continue;

                double layerMinX, layerMinY, layerMaxX, layerMaxY;
                if (layer.CalculateBounds(out layerMinX, out layerMinY, out layerMaxX, out layerMaxY))
                {
                    minX = Math.Min(minX, layerMinX);
                    minY = Math.Min(minY, layerMinY);
                    maxX = Math.Max(maxX, layerMaxX);
                    maxY = Math.Max(maxY, layerMaxY);
                    hasPoints = true;
                }
            }

            // If no points were found, set default bounds
            if (!hasPoints)
            {
                minX = -1;
                minY = -1;
                maxX = 1;
                maxY = 1;
            }

            return hasPoints;
        }
    }
}