using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Text;
using FCoreMap.Geometries;
using FCoreMap.Interactions;
using SkiaSharp;

namespace FCoreMap.Controls
{
    /// <summary>
    /// Handles the rendering operations for the FCoreMapControl using SkiaSharp.
    /// Modified to support tile-based rendering for improved panning performance.
    /// </summary>
    public class MapRendererSk
    {
        private readonly CoordinateTransformerSk transformer;
        private SKTypeface defaultTypeface;

        /// <summary>
        /// Gets the current viewport bounding box in world coordinates.
        /// </summary>
        public BoundingBox ViewportBounds { get; private set; }

        /// <summary>
        /// Initializes a new instance of the MapRendererSk class.
        /// </summary>
        /// <param name="transformer">The coordinate transformer to use for coordinate transformations.</param>
        public MapRendererSk(CoordinateTransformerSk transformer)
        {
            this.transformer = transformer ?? throw new ArgumentNullException(nameof(transformer));
            ViewportBounds = new BoundingBox();
            defaultTypeface = SKTypeface.FromFamilyName("Arial");
        }

        /// <summary>
        /// Updates the viewport bounds based on the current transformation and visible area.
        /// </summary>
        /// <param name="visibleRectangle">The visible rectangle in screen coordinates.</param>
        public void UpdateViewportBounds(SKRect visibleRectangle)
        {
            // Get the world coordinates of the visible rectangle corners
            PointD topLeft = transformer.ScreenToWorld(visibleRectangle.Left, visibleRectangle.Top);
            PointD topRight = transformer.ScreenToWorld(visibleRectangle.Right, visibleRectangle.Top);
            PointD bottomLeft = transformer.ScreenToWorld(visibleRectangle.Left, visibleRectangle.Bottom);
            PointD bottomRight = transformer.ScreenToWorld(visibleRectangle.Right, visibleRectangle.Bottom);

            // Create a new viewport bounding box
            ViewportBounds = new BoundingBox(
                Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomLeft.X, bottomRight.X)),
                Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomLeft.Y, bottomRight.Y)),
                Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomLeft.X, bottomRight.X)),
                Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomLeft.Y, bottomRight.Y))
            );
        }

        /// <summary>
        /// Calculates the actual line width in world units based on the desired pixel width at the current zoom level.
        /// </summary>
        /// <param name="desiredPixelWidth">The desired width in pixels</param>
        /// <returns>The line width adjusted for the current zoom level</returns>
        private float GetScaledLineWidth(float desiredPixelWidth)
        {
            // Scale is pixels per world unit. To get a consistent pixel width, 
            // divide the desired pixel width by the scale.
            double scale = transformer.GetWorldToScreenScale();

            // Avoid division by zero
            if (scale <= 0.00001)
                return desiredPixelWidth;

            return (float)(desiredPixelWidth / scale);
        }

        /// <summary>
        /// Renders the map with all layers and UI elements.
        /// </summary>
        public void RenderMap(SKCanvas canvas, SKColor backgroundColor, LayerManager layerManager,
            bool showWorldCoordinates, PointD lastMouseWorldPosition, SKRect zoomRectangle,
            bool isInteracting, MapBehavior currentBehavior)
        {
            // Set up high-quality rendering
            canvas.Clear(backgroundColor);

            // Update the viewport bounds based on the current transformation
            var visibleBounds = new SKRect(0, 0, canvas.DeviceClipBounds.Width, canvas.DeviceClipBounds.Height);
            UpdateViewportBounds(visibleBounds);

            // Apply the coordinate transformation
            transformer.ApplyTransformation(canvas);

            // Draw layers if the layer manager exists
            if (layerManager != null)
            {
                // Get all layers
                var layers = layerManager.GetLayers();

                // Draw layers in reverse order (to ensure proper z-ordering)
                for (int i = layers.Count - 1; i >= 0; i--)
                {
                    Layer layer = layers[i];

                    // Skip if layer is not visible
                    if (!layer.Visible)
                        continue;

                    DrawLayer(canvas, layer);
                }
            }

            // Reset the transformation for UI elements
            transformer.ResetTransformation(canvas);

            // Draw world coordinates if enabled
            if (showWorldCoordinates)
            {
                DrawWorldCoordinates(canvas, lastMouseWorldPosition);
            }

            // Draw zoom rectangle if in zoom mode and rectangle exists
            if ((currentBehavior == MapBehavior.ZoomIn ||
                 currentBehavior == MapBehavior.ZoomOut) &&
                isInteracting && !zoomRectangle.IsEmpty)
            {
                DrawZoomRectangle(canvas, zoomRectangle, currentBehavior);
            }
        }

        /// <summary>
        /// Renders map overlays - elements that should appear on top of all map content.
        /// </summary>
        public void RenderMapOverlays(SKCanvas canvas, bool showWorldCoordinates, 
                                     PointD lastMouseWorldPosition, SKRect zoomRectangle,
                                     bool isInteracting, MapBehavior currentBehavior)
        {
            // Reset the transformation for UI elements
            transformer.ResetTransformation(canvas);
            
            // Draw world coordinates if enabled
            if (showWorldCoordinates)
            {
                DrawWorldCoordinates(canvas, lastMouseWorldPosition);
            }

            // Draw zoom rectangle if in zoom mode and rectangle exists
            if ((currentBehavior == MapBehavior.ZoomIn ||
                 currentBehavior == MapBehavior.ZoomOut) &&
                isInteracting && !zoomRectangle.IsEmpty)
            {
                DrawZoomRectangle(canvas, zoomRectangle, currentBehavior);
            }
        }
        
        /// <summary>
        /// Draws a specific layer.
        /// </summary>
        private void DrawLayer(SKCanvas canvas, Layer layer)
        {
            switch (layer.Type)
            {
                case LayerType.Point:
                    DrawPointLayer(canvas, layer);
                    break;
                case LayerType.Line:
                    DrawLineLayer(canvas, layer);
                    break;
                case LayerType.Polygon:
                    DrawPolygonLayer(canvas, layer);
                    break;
                case LayerType.Circle:
                    DrawCircleLayer(canvas, layer);
                    break;
            }
        }

        /// <summary>
        /// Draws layer geometries that fall within a specific bounding box - used for tile rendering.
        /// </summary>
        public void DrawLayerInBounds(SKCanvas canvas, Layer layer, BoundingBox bounds, int detailLevel = 3)
        {
            switch (layer.Type)
            {
                case LayerType.Point:
                    DrawPointLayerInBounds(canvas, layer, bounds, detailLevel);
                    break;
                case LayerType.Line:
                    DrawLineLayerInBounds(canvas, layer, bounds, detailLevel);
                    break;
                case LayerType.Polygon:
                    DrawPolygonLayerInBounds(canvas, layer, bounds, detailLevel);
                    break;
                case LayerType.Circle:
                    DrawCircleLayerInBounds(canvas, layer, bounds, detailLevel);
                    break;
            }
        }

        /// <summary>
        /// Draws a point layer.
        /// </summary>
        private void DrawPointLayer(SKCanvas canvas, Layer layer)
        {
            // Simply delegate to DrawPointLayerInBounds using the viewport bounds
            DrawPointLayerInBounds(canvas, layer, ViewportBounds, 3); // Full detail level
        }

        /// <summary>
        /// Draws a point layer with content filtered by bounds
        /// </summary>
        private void DrawPointLayerInBounds(SKCanvas canvas, Layer layer, BoundingBox bounds, int detailLevel)
        {
            var points = layer.GetPoints();
            if (points == null || points.Count == 0)
                return;

            LayerStyle style = layer.Style;
            if (style == null)
                return;

            // Calculate scaled outline width to maintain consistent pixel width
            float scaledOutlineWidth = GetScaledLineWidth(style.OutlineWidth);

            // Calculate scaled point size to maintain consistent appearance
            float pointSizeInWorldUnits = GetScaledLineWidth(style.PointSize);

            using (var pointPaint = new SKPaint
            {
                Color = ColorToSKColor(style.Color),
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            })
            using (var outlinePaint = new SKPaint
            {
                Color = ColorToSKColor(style.OutlineColor),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = scaledOutlineWidth, // Use scaled width
                IsAntialias = true
            })
            {
                // Adjust quality based on detail level
                if (detailLevel < 3)
                {
                    pointPaint.FilterQuality = detailLevel == 1 ? SKFilterQuality.Low : SKFilterQuality.Medium;
                    outlinePaint.FilterQuality = detailLevel == 1 ? SKFilterQuality.Low : SKFilterQuality.Medium;
                }

                // Sample rate for lower detail levels
                int samplingRate = detailLevel == 1 ? 4 : (detailLevel == 2 ? 2 : 1);

                for (int i = 0; i < points.Count; i += samplingRate)
                {
                    var point = points[i];

                    // Skip points that don't intersect with the given bounds
                    if (!bounds.Contains(point))
                        continue;

                    // Draw point directly in world coordinates
                    float x = (float)point.X;
                    float y = (float)point.Y;

                    DrawPointAtWorld(canvas, x, y, pointSizeInWorldUnits, style.PointShape, pointPaint, outlinePaint);

                    // Draw label if enabled at highest detail level
                    if (detailLevel == 3 && style.ShowLabels && layer.AttributeData != null)
                    {
                        string labelText = layer.GetLabelText(point.Id);
                        if (!string.IsNullOrEmpty(labelText))
                        {
                            DrawLabelAtWorld(canvas, point.X, point.Y, labelText, style);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Draws a line layer.
        /// </summary>
        private void DrawLineLayer(SKCanvas canvas, Layer layer)
        {
            // Simply delegate to DrawLineLayerInBounds using the viewport bounds
            DrawLineLayerInBounds(canvas, layer, ViewportBounds, 3); // Full detail level
        }

        /// <summary>
        /// Draws a line layer with content filtered by bounds
        /// </summary>
        private void DrawLineLayerInBounds(SKCanvas canvas, Layer layer, BoundingBox bounds, int detailLevel)
        {
            var lines = layer.GetLines();
            if (lines == null || lines.Count == 0)
                return;

            LayerStyle style = layer.Style;
            if (style == null)
                return;

            // Calculate scaled line width to maintain consistent pixel width
            float scaledLineWidth = GetScaledLineWidth(style.LineWidth);

            using (var linePaint = new SKPaint
            {
                Color = ColorToSKColor(style.Color),
                StrokeWidth = scaledLineWidth, // Use scaled width
                IsAntialias = true,
                Style = SKPaintStyle.Stroke
            })
            {
                // Adjust quality based on detail level
                if (detailLevel < 3)
                {
                    linePaint.FilterQuality = detailLevel == 1 ? SKFilterQuality.Low : SKFilterQuality.Medium;
                }

                // Set line style
                switch (style.LineStyle)
                {
                    case LineStyle.Dashed:
                        float[] dashedPattern = new float[] { GetScaledLineWidth(8), GetScaledLineWidth(4) };
                        linePaint.PathEffect = SKPathEffect.CreateDash(dashedPattern, 0);
                        break;
                    case LineStyle.Dotted:
                        float[] dottedPattern = new float[] { GetScaledLineWidth(2), GetScaledLineWidth(2) };
                        linePaint.PathEffect = SKPathEffect.CreateDash(dottedPattern, 0);
                        break;
                    case LineStyle.DashDot:
                        float[] dashDotPattern = new float[] {
                            GetScaledLineWidth(8), GetScaledLineWidth(4),
                            GetScaledLineWidth(2), GetScaledLineWidth(4)
                        };
                        linePaint.PathEffect = SKPathEffect.CreateDash(dashDotPattern, 0);
                        break;
                    case LineStyle.DashDotDot:
                        float[] dashDotDotPattern = new float[] {
                            GetScaledLineWidth(8), GetScaledLineWidth(4),
                            GetScaledLineWidth(2), GetScaledLineWidth(4),
                            GetScaledLineWidth(2), GetScaledLineWidth(4)
                        };
                        linePaint.PathEffect = SKPathEffect.CreateDash(dashDotDotPattern, 0);
                        break;
                    default: // Solid
                        linePaint.PathEffect = null;
                        break;
                }

                for (int i = 0; i < lines.Count; i++)
                {
                    var line = lines[i];

                    if (line.PointCount < 2)
                        continue;

                    // Skip lines that don't intersect with the given bounds
                    if (!line.BoundingBox.Intersects(bounds))
                        continue;

                    var points = line.Points;

                    // Create a path for the line
                    using (var path = new SKPath())
                    {
                        // Calculate sampling rate for point reduction at lower detail levels
                        int samplingRate = detailLevel == 1 ? 4 : (detailLevel == 2 ? 2 : 1);
                        
                        // Ensure we always include first and last points
                        path.MoveTo((float)points[0].X, (float)points[0].Y);

                        // Add lines to sampled points
                        for (int j = samplingRate; j < points.Count; j += samplingRate)
                        {
                            // Always add the last point
                            int pointIndex = Math.Min(j, points.Count - 1);
                            path.LineTo((float)points[pointIndex].X, (float)points[pointIndex].Y);
                        }

                        // Draw the line path
                        canvas.DrawPath(path, linePaint);
                    }

                    // Draw label if enabled at highest detail level
                    if (detailLevel == 3 && style.ShowLabels && layer.AttributeData != null)
                    {
                        string labelText = layer.GetLabelText(line.Id);
                        if (!string.IsNullOrEmpty(labelText))
                        {
                            // Calculate the midpoint of the line for label placement
                            int midIndex = points.Count / 2;
                            double labelX = points[midIndex].X;
                            double labelY = points[midIndex].Y;

                            DrawLabelAtWorld(canvas, labelX, labelY, labelText, style);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Draws a polygon layer.
        /// </summary>
        private void DrawPolygonLayer(SKCanvas canvas, Layer layer)
        {
            // Simply delegate to DrawPolygonLayerInBounds using the viewport bounds
            DrawPolygonLayerInBounds(canvas, layer, ViewportBounds, 3); // Full detail level
        }

        /// <summary>
        /// Draws a polygon layer with content filtered by bounds
        /// </summary>
        private void DrawPolygonLayerInBounds(SKCanvas canvas, Layer layer, BoundingBox bounds, int detailLevel)
        {
            var polygons = layer.GetPolygons();
            if (polygons == null || polygons.Count == 0)
                return;

            LayerStyle style = layer.Style;
            if (style == null)
                return;

            // Calculate scaled outline width to maintain consistent pixel width
            float scaledOutlineWidth = GetScaledLineWidth(style.OutlineWidth);

            using (var fillPaint = CreateFillPaint(style))
            using (var outlinePaint = new SKPaint
            {
                Color = ColorToSKColor(style.OutlineColor),
                StrokeWidth = scaledOutlineWidth, // Use scaled width
                IsAntialias = true,
                Style = SKPaintStyle.Stroke
            })
            {
                // Adjust quality based on detail level
                if (detailLevel < 3)
                {
                    fillPaint.FilterQuality = detailLevel == 1 ? SKFilterQuality.Low : SKFilterQuality.Medium;
                    outlinePaint.FilterQuality = detailLevel == 1 ? SKFilterQuality.Low : SKFilterQuality.Medium;
                }

                for (int i = 0; i < polygons.Count; i++)
                {
                    var polygon = polygons[i];

                    if (polygon.VertexCount < 3)
                        continue;

                    // Skip polygons that don't intersect with the given bounds
                    if (!polygon.BoundingBox.Intersects(bounds))
                        continue;

                    var vertices = polygon.Vertices;

                    // Create a path for the polygon
                    using (var path = new SKPath())
                    {
                        // Calculate sampling rate for vertex reduction at lower detail levels
                        int samplingRate = detailLevel == 1 ? 4 : (detailLevel == 2 ? 2 : 1);
                        
                        // Ensure we always include first vertex
                        path.MoveTo((float)vertices[0].X, (float)vertices[0].Y);

                        // Add lines to sampled vertices
                        for (int j = samplingRate; j < vertices.Count; j += samplingRate)
                        {
                            // Ensure the last point is always included
                            int vertexIndex = Math.Min(j, vertices.Count - 1);
                            path.LineTo((float)vertices[vertexIndex].X, (float)vertices[vertexIndex].Y);
                        }

                        // Close the path to form a polygon
                        path.Close();

                        // Draw fill if enabled
                        if (style.ShowFill && style.FillPattern != FillPattern.None)
                        {
                            canvas.DrawPath(path, fillPaint);
                        }

                        // Draw outline
                        canvas.DrawPath(path, outlinePaint);
                    }

                    // Draw label if enabled at highest detail level
                    if (detailLevel == 3 && style.ShowLabels && layer.AttributeData != null)
                    {
                        string labelText = layer.GetLabelText(polygon.Id);
                        if (!string.IsNullOrEmpty(labelText))
                        {
                            // Calculate centroid for label placement
                            double sumX = 0, sumY = 0;
                            foreach (var vertex in vertices)
                            {
                                sumX += vertex.X;
                                sumY += vertex.Y;
                            }
                            double centerX = sumX / vertices.Count;
                            double centerY = sumY / vertices.Count;

                            DrawLabelAtWorld(canvas, centerX, centerY, labelText, style);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Draws a circle layer.
        /// </summary>
        private void DrawCircleLayer(SKCanvas canvas, Layer layer)
        {
            var circles = layer.GetCircles();
            if (circles == null || circles.Count == 0)
                return;

            LayerStyle style = layer.Style;
            if (style == null)
                return;

            // Calculate scaled line width to maintain consistent pixel width
            float scaledOutlineWidth = GetScaledLineWidth(style.OutlineWidth);

            using (var fillPaint = CreateFillPaint(style))
            using (var outlinePaint = new SKPaint
            {
                Color = ColorToSKColor(style.OutlineColor),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = scaledOutlineWidth, // Use scaled width
                IsAntialias = true
            })
            {
                for (int i = 0; i < circles.Count; i++)
                {
                    var circle = circles[i];

                    // Skip circles that don't intersect with the viewport bounds
                    if (!circle.BoundingBox.Intersects(ViewportBounds))
                        continue;

                    // Get the center in world coordinates
                    float centerX = (float)circle.Center.X;
                    float centerY = (float)circle.Center.Y;
                    float radius = (float)circle.Radius;

                    // Draw fill if enabled
                    if (style.ShowFill && style.FillPattern != FillPattern.None)
                    {
                        canvas.DrawCircle(centerX, centerY, radius, fillPaint);
                    }

                    // Draw outline
                    canvas.DrawCircle(centerX, centerY, radius, outlinePaint);

                    // Draw label if enabled
                    if (style.ShowLabels && layer.AttributeData != null)
                    {
                        string labelText = layer.GetLabelText(circle.Id);
                        if (!string.IsNullOrEmpty(labelText))
                        {
                            // Convert position to screen coordinates for label
                            DrawLabelAtWorld(canvas, circle.Center.X, circle.Center.Y, labelText, style);
                        }
                    }

                    // Draw user-defined points if any
                    if (circle.UserDefinedPointCount > 0)
                    {
                        // Calculate consistent point size in world units
                        float pointSize = GetScaledLineWidth(10); // 10 pixels

                        using (var pointPaint = new SKPaint
                        {
                            Color = new SKColor(255, 0, 0, 200),
                            Style = SKPaintStyle.Fill,
                            IsAntialias = true
                        })
                        using (var pointOutlinePaint = new SKPaint
                        {
                            Color = SKColors.Black,
                            Style = SKPaintStyle.Stroke,
                            StrokeWidth = GetScaledLineWidth(2), // 2 pixel outline
                            IsAntialias = true
                        })
                        {
                            foreach (var point in circle.UserDefinedPoints)
                            {
                                float x = (float)point.X;
                                float y = (float)point.Y;

                                // Draw a small marker for each user point
                                canvas.DrawCircle(x, y, pointSize / 2, pointPaint);
                                canvas.DrawCircle(x, y, pointSize / 2, pointOutlinePaint);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Draws a circle layer with content filtered by bounds
        /// </summary>
        private void DrawCircleLayerInBounds(SKCanvas canvas, Layer layer, BoundingBox bounds, int detailLevel)
        {
            var circles = layer.GetCircles();
            if (circles == null || circles.Count == 0)
                return;

            LayerStyle style = layer.Style;
            if (style == null)
                return;

            // Calculate scaled line width to maintain consistent pixel width
            float scaledOutlineWidth = GetScaledLineWidth(style.OutlineWidth);

            using (var fillPaint = CreateFillPaint(style))
            using (var outlinePaint = new SKPaint
            {
                Color = ColorToSKColor(style.OutlineColor),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = scaledOutlineWidth, // Use scaled width
                IsAntialias = true
            })
            {
                // Adjust quality based on detail level
                if (detailLevel < 3)
                {
                    fillPaint.FilterQuality = detailLevel == 1 ? SKFilterQuality.Low : SKFilterQuality.Medium;
                    outlinePaint.FilterQuality = detailLevel == 1 ? SKFilterQuality.Low : SKFilterQuality.Medium;
                }

                // Calculate segment count based on detail level
                int segments = detailLevel == 1 ? 12 : (detailLevel == 2 ? 24 : 36);

                for (int i = 0; i < circles.Count; i++)
                {
                    var circle = circles[i];

                    // Skip circles that don't intersect with the bounds
                    if (!circle.BoundingBox.Intersects(bounds))
                        continue;

                    // Get the center in world coordinates
                    float centerX = (float)circle.Center.X;
                    float centerY = (float)circle.Center.Y;
                    float radius = (float)circle.Radius;

                    // Draw fill if enabled
                    if (style.ShowFill && style.FillPattern != FillPattern.None)
                    {
                        DrawFilledCircleApproximation(canvas, circle, segments, fillPaint);
                    }

                    // Draw outline
                    if (detailLevel < 3)
                    {
                        // For lower detail levels, approximate circle with a polygon
                        DrawFilledCircleApproximation(canvas, circle, segments, outlinePaint);
                    }
                    else
                    {
                        // For highest detail level, use native circle drawing
                        canvas.DrawCircle(centerX, centerY, radius, outlinePaint);
                    }

                    // Draw label if enabled at highest detail level
                    if (detailLevel == 3 && style.ShowLabels && layer.AttributeData != null)
                    {
                        string labelText = layer.GetLabelText(circle.Id);
                        if (!string.IsNullOrEmpty(labelText))
                        {
                            // Convert position to screen coordinates for label
                            DrawLabelAtWorld(canvas, circle.Center.X, circle.Center.Y, labelText, style);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Draws the zoom rectangle on the map
        /// </summary>
        public void DrawZoomRectangle(SKCanvas canvas, SKRect zoomRect, MapBehavior behavior)
        {
            // Different styles for zoom in and zoom out
            bool isZoomingIn = behavior == MapBehavior.ZoomIn;

            // Create different styles for zoom in and zoom out
            using (var rectPaint = new SKPaint
            {
                Color = isZoomingIn ? new SKColor(0, 0, 255) : new SKColor(255, 0, 0),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2, // Fixed 2px width for UI element
                PathEffect = SKPathEffect.CreateDash(new float[] { 4, 4 }, 0)
            })
            {
                // Draw the rectangle
                canvas.DrawRect(zoomRect, rectPaint);

                // Add a semi-transparent fill to indicate zoom in/out
                using (var fillPaint = new SKPaint
                {
                    Color = isZoomingIn
                        ? new SKColor(0, 0, 255, 30)
                        : new SKColor(255, 0, 0, 30),
                    Style = SKPaintStyle.Fill
                })
                {
                    canvas.DrawRect(zoomRect, fillPaint);
                }

                // Draw a label to indicate zoom in/out
                string zoomText = isZoomingIn ? "Zoom In" : "Zoom Out";

                using (var textPaint = new SKPaint
                {
                    Color = SKColors.Black,
                    TextSize = 16,
                    IsAntialias = true,
                    Typeface = defaultTypeface
                })
                {
                    // Position the text in the top-left of the rectangle
                    float x = zoomRect.Left + 2;
                    float y = zoomRect.Top + 16 + 2; // 16 is approximately the text height

                    // Measure text size
                    SKRect textBounds = new SKRect();
                    textPaint.MeasureText(zoomText, ref textBounds);

                    // Draw text background
                    using (var bgPaint = new SKPaint
                    {
                        Color = new SKColor(255, 255, 255, 200),
                        Style = SKPaintStyle.Fill
                    })
                    {
                        canvas.DrawRect(x, y - textBounds.Height, textBounds.Width, textBounds.Height, bgPaint);
                    }

                    // Draw text
                    canvas.DrawText(zoomText, x, y, textPaint);
                }
            }
        }

        /// <summary>
        /// Draws the current world coordinates in the bottom corner.
        /// </summary>
        public void DrawWorldCoordinates(SKCanvas canvas, PointD mousePos)
        {
            if (mousePos == null)
                return;

            string coordsText = $"X: {mousePos.X:F2}, Y: {mousePos.Y:F2}";
            string viewportText = $"Viewport: [{ViewportBounds.MinX:F2}, {ViewportBounds.MinY:F2}] - [{ViewportBounds.MaxX:F2}, {ViewportBounds.MaxY:F2}]";

            using (var textPaint = new SKPaint
            {
                Color = SKColors.Black,
                TextSize = 14,
                IsAntialias = true,
                Typeface = defaultTypeface
            })
            {
                // Measure text
                SKRect coordsTextBounds = new SKRect();
                textPaint.MeasureText(coordsText, ref coordsTextBounds);

                SKRect viewportTextBounds = new SKRect();
                textPaint.MeasureText(viewportText, ref viewportTextBounds);

                float maxWidth = Math.Max(coordsTextBounds.Width, viewportTextBounds.Width);

                // Calculate position (bottom-right corner with padding)
                float x = canvas.DeviceClipBounds.Width - maxWidth - 10;
                float y = canvas.DeviceClipBounds.Height - coordsTextBounds.Height - viewportTextBounds.Height - 10;

                // Draw background for both texts
                using (var bgPaint = new SKPaint
                {
                    Color = new SKColor(255, 255, 255, 200),
                    Style = SKPaintStyle.Fill
                })
                {
                    canvas.DrawRect(
                        x - 2,
                        y - 2,
                        maxWidth + 4,
                        coordsTextBounds.Height + viewportTextBounds.Height + 6,
                        bgPaint);
                }

                // Draw viewport text
                canvas.DrawText(viewportText, x, y + viewportTextBounds.Height, textPaint);

                // Draw mouse coords text below viewport text
                canvas.DrawText(coordsText, x, y + viewportTextBounds.Height + coordsTextBounds.Height + 2, textPaint);
            }
        }

        /// <summary>
        /// Draws a single point with the specified style at world coordinates.
        /// </summary>
        private void DrawPointAtWorld(SKCanvas canvas, float x, float y, float size, PointShape shape,
            SKPaint fillPaint, SKPaint outlinePaint)
        {
            float halfSize = size / 2;

            switch (shape)
            {
                case PointShape.Circle:
                    canvas.DrawCircle(x, y, halfSize, fillPaint);
                    canvas.DrawCircle(x, y, halfSize, outlinePaint);
                    break;

                case PointShape.Square:
                    var squareRect = new SKRect(
                        x - halfSize, y - halfSize,
                        x + halfSize, y + halfSize);
                    canvas.DrawRect(squareRect, fillPaint);
                    canvas.DrawRect(squareRect, outlinePaint);
                    break;

                case PointShape.Triangle:
                    using (var path = new SKPath())
                    {
                        path.MoveTo(x, y - halfSize);
                        path.LineTo(x - halfSize, y + halfSize);
                        path.LineTo(x + halfSize, y + halfSize);
                        path.Close();

                        canvas.DrawPath(path, fillPaint);
                        canvas.DrawPath(path, outlinePaint);
                    }
                    break;

                case PointShape.Diamond:
                    using (var path = new SKPath())
                    {
                        path.MoveTo(x, y - halfSize);
                        path.LineTo(x + halfSize, y);
                        path.LineTo(x, y + halfSize);
                        path.LineTo(x - halfSize, y);
                        path.Close();

                        canvas.DrawPath(path, fillPaint);
                        canvas.DrawPath(path, outlinePaint);
                    }
                    break;

                case PointShape.Cross:
                    float thickness = size / 6;

                    // Horizontal line
                    var hRect = new SKRect(
                        x - halfSize, y - thickness / 2,
                        x + halfSize, y + thickness / 2);

                    // Vertical line
                    var vRect = new SKRect(
                        x - thickness / 2, y - halfSize,
                        x + thickness / 2, y + halfSize);

                    canvas.DrawRect(hRect, fillPaint);
                    canvas.DrawRect(vRect, fillPaint);

                    canvas.DrawLine(x - halfSize, y, x + halfSize, y, outlinePaint);
                    canvas.DrawLine(x, y - halfSize, x, y + halfSize, outlinePaint);
                    break;

                case PointShape.Star:
                    using (var path = new SKPath())
                    {
                        // Create a simple five-pointed star
                        for (int i = 0; i < 10; i++)
                        {
                            float radius = (i % 2 == 0) ? halfSize : halfSize / 2;
                            double angle = Math.PI / 2 + i * Math.PI * 2 / 10;
                            float px = (float)(x + radius * Math.Cos(angle));
                            float py = (float)(y - radius * Math.Sin(angle));

                            if (i == 0)
                                path.MoveTo(px, py);
                            else
                                path.LineTo(px, py);
                        }
                        path.Close();

                        canvas.DrawPath(path, fillPaint);
                        canvas.DrawPath(path, outlinePaint);
                    }
                    break;
            }
        }

        /// <summary>
        /// Creates a paint object based on the polygon fill style.
        /// </summary>
        private SKPaint CreateFillPaint(LayerStyle style)
        {
            SKColor fillColor = new SKColor(
                (byte)style.Color.R,
                (byte)style.Color.G,
                (byte)style.Color.B,
                (byte)(style.Opacity * 255));

            SKPaint paint = new SKPaint
            {
                Color = fillColor,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };

            // Create the appropriate pattern based on fill pattern
            switch (style.FillPattern)
            {
                case FillPattern.None:
                    paint.Color = SKColors.Transparent;
                    break;

                case FillPattern.Horizontal:
                    paint.Shader = CreateHatchShader(fillColor, GetScaledLineWidth(8), true, false);
                    break;

                case FillPattern.Vertical:
                    paint.Shader = CreateHatchShader(fillColor, GetScaledLineWidth(8), false, true);
                    break;

                case FillPattern.Diagonal:
                    paint.Shader = CreateDiagonalShader(fillColor, GetScaledLineWidth(8), false);
                    break;

                case FillPattern.CrossHatch:
                    paint.Shader = CreateHatchShader(fillColor, GetScaledLineWidth(8), true, true);
                    break;

                case FillPattern.DiagonalCrossHatch:
                    paint.Shader = CreateDiagonalCrossShader(fillColor, GetScaledLineWidth(8));
                    break;

                case FillPattern.Solid:
                default:
                    // Default is solid color, no additional setup needed
                    break;
            }

            return paint;
        }

        /// <summary>
        /// Creates a shader for horizontal and/or vertical hatching.
        /// </summary>
        private SKShader CreateHatchShader(SKColor color, float spacing, bool horizontal, bool vertical)
        {
            int size = (int)(spacing * 2);
            // Ensure minimum size
            size = Math.Max(4, size);

            // Create a bitmap for the pattern
            using (var bitmap = new SKBitmap(size, size))
            {
                // Create a canvas to draw on the bitmap
                using (var canvas = new SKCanvas(bitmap))
                {
                    canvas.Clear(new SKColor(color.Red, color.Green, color.Blue, 0)); // Transparent background

                    using (var paint = new SKPaint
                    {
                        Color = color,
                        StrokeWidth = Math.Max(1, GetScaledLineWidth(1)), // Ensure minimum line width of 1 pixel
                        Style = SKPaintStyle.Stroke
                    })
                    {
                        if (horizontal)
                        {
                            float y = size / 2;
                            canvas.DrawLine(0, y, size, y, paint);
                        }

                        if (vertical)
                        {
                            float x = size / 2;
                            canvas.DrawLine(x, 0, x, size, paint);
                        }
                    }
                }

                // Create a shader from the bitmap
                return SKShader.CreateBitmap(bitmap, SKShaderTileMode.Repeat, SKShaderTileMode.Repeat);
            }
        }

        /// <summary>
        /// Creates a shader for diagonal hatching.
        /// </summary>
        private SKShader CreateDiagonalShader(SKColor color, float spacing, bool backslash)
        {
            int size = (int)(spacing * 2);
            // Ensure minimum size
            size = Math.Max(4, size);

            // Create a bitmap for the pattern
            using (var bitmap = new SKBitmap(size, size))
            {
                // Create a canvas to draw on the bitmap
                using (var canvas = new SKCanvas(bitmap))
                {
                    canvas.Clear(new SKColor(color.Red, color.Green, color.Blue, 0)); // Transparent background

                    using (var paint = new SKPaint
                    {
                        Color = color,
                        StrokeWidth = Math.Max(1, GetScaledLineWidth(1)), // Ensure minimum line width of 1 pixel
                        Style = SKPaintStyle.Stroke
                    })
                    {
                        if (backslash)
                        {
                            canvas.DrawLine(0, 0, size, size, paint);
                        }
                        else
                        {
                            canvas.DrawLine(0, size, size, 0, paint);
                        }
                    }
                }

                // Create a shader from the bitmap
                return SKShader.CreateBitmap(bitmap, SKShaderTileMode.Repeat, SKShaderTileMode.Repeat);
            }
        }

        /// <summary>
        /// Creates a shader for diagonal crosshatching.
        /// </summary>
        private SKShader CreateDiagonalCrossShader(SKColor color, float spacing)
        {
            int size = (int)(spacing * 2);
            // Ensure minimum size
            size = Math.Max(4, size);

            // Create a bitmap for the pattern
            using (var bitmap = new SKBitmap(size, size))
            {
                // Create a canvas to draw on the bitmap
                using (var canvas = new SKCanvas(bitmap))
                {
                    canvas.Clear(new SKColor(color.Red, color.Green, color.Blue, 0)); // Transparent background

                    using (var paint = new SKPaint
                    {
                        Color = color,
                        StrokeWidth = Math.Max(1, GetScaledLineWidth(1)), // Ensure minimum line width of 1 pixel
                        Style = SKPaintStyle.Stroke
                    })
                    {
                        // Forward diagonal
                        canvas.DrawLine(0, size, size, 0, paint);

                        // Back diagonal
                        canvas.DrawLine(0, 0, size, size, paint);
                    }
                }

                // Create a shader from the bitmap
                return SKShader.CreateBitmap(bitmap, SKShaderTileMode.Repeat, SKShaderTileMode.Repeat);
            }
        }

        /// <summary>
        /// Draws a filled circle approximated by a polygon with the specified number of segments.
        /// </summary>
        public void DrawFilledCircleApproximation(SKCanvas canvas, CircleD circle, int segments, SKPaint paint)
        {
            if (circle == null || segments < 8)
                return;

            // Create path for the circle
            using (var path = new SKPath())
            {
                // Get the center and radius in world coordinates
                float centerX = (float)circle.Center.X;
                float centerY = (float)circle.Center.Y;
                float radius = (float)circle.Radius;

                // Start at the right-most point of the circle
                path.MoveTo(centerX + radius, centerY);

                // Add arcs to approximate the circle
                for (int i = 1; i <= segments; i++)
                {
                    double angle = i * 2 * Math.PI / segments;
                    float x = centerX + (float)(radius * Math.Cos(angle));
                    float y = centerY + (float)(radius * Math.Sin(angle));
                    path.LineTo(x, y);
                }

                // Close the path
                path.Close();

                // Draw the path
                canvas.DrawPath(path, paint);
            }
        }

        /// <summary>
        /// Draws a label at the specified world coordinates.
        /// </summary>
        private void DrawLabelAtWorld(SKCanvas canvas, double x, double y, string labelText, LayerStyle style)
        {
            // Skip empty labels or null parameters
            if (canvas == null || string.IsNullOrEmpty(labelText) || style == null)
                return;

            // Save the current state of the canvas
            canvas.Save();

            // Reset transformation to screen coordinates for text rendering
            transformer.ResetTransformation(canvas);

            // Get screen coordinates for the label
            SKPoint screenPoint = transformer.WorldToScreen((float)x, (float)y);

            try
            {
                // Calculate font size that will appear the correct pixel size regardless of zoom
                float fontPixelSize = style.LabelFont?.Size ?? 16; // Default text size in pixels

                // Create text paint
                using (var textPaint = new SKPaint
                {
                    Color = ColorToSKColor(style.LabelColor),
                    TextSize = fontPixelSize, // Use pixel size directly since we're in screen coordinates
                    IsAntialias = true,
                    Typeface = defaultTypeface
                })
                {
                    // Measure the label text
                    SKRect textBounds = new SKRect();
                    textPaint.MeasureText(labelText, ref textBounds);

                    // Calculate offset based on label position
                    float offsetX = style.LabelOffset;
                    float offsetY = style.LabelOffset;

                    // Adjust position based on LabelPosition enum
                    switch (style.LabelPosition)
                    {
                        case LabelPosition.Center:
                            offsetX = -textBounds.Width / 2;
                            offsetY = -textBounds.Height / 2;
                            break;
                        case LabelPosition.Top:
                            offsetX = -textBounds.Width / 2;
                            offsetY = -textBounds.Height - offsetY;
                            break;
                        case LabelPosition.Bottom:
                            offsetX = -textBounds.Width / 2;
                            offsetY = offsetY;
                            break;
                        case LabelPosition.Left:
                            offsetX = -textBounds.Width - offsetX;
                            offsetY = -textBounds.Height / 2;
                            break;
                        case LabelPosition.Right:
                            offsetX = offsetX;
                            offsetY = -textBounds.Height / 2;
                            break;
                        case LabelPosition.TopLeft:
                            offsetX = -textBounds.Width - offsetX;
                            offsetY = -textBounds.Height - offsetY;
                            break;
                        case LabelPosition.TopRight:
                            offsetX = offsetX;
                            offsetY = -textBounds.Height - offsetY;
                            break;
                        case LabelPosition.BottomLeft:
                            offsetX = -textBounds.Width - offsetX;
                            offsetY = offsetY;
                            break;
                        case LabelPosition.BottomRight:
                            offsetX = offsetX;
                            offsetY = offsetY;
                            break;
                    }

                    float x1 = screenPoint.X + offsetX;
                    float y1 = screenPoint.Y + offsetY;

                    // Create a background rectangle
                    SKRect bgRect = new SKRect(
                        x1 - 2,
                        y1 - 2,
                        x1 + textBounds.Width + 4,
                        y1 + textBounds.Height + 2);

                    // Draw a background for better readability if enabled
                    if (style.LabelBackground)
                    {
                        using (var bgPaint = new SKPaint
                        {
                            Color = new SKColor(255, 255, 255, 180),
                            Style = SKPaintStyle.Fill
                        })
                        using (var outlinePaint = new SKPaint
                        {
                            Color = new SKColor(0, 0, 0, 100),
                            Style = SKPaintStyle.Stroke,
                            StrokeWidth = 1
                        })
                        {
                            canvas.DrawRect(bgRect, bgPaint);
                            canvas.DrawRect(bgRect, outlinePaint);
                        }
                    }

                    // Draw the halo effect if enabled
                    if (style.LabelHalo)
                    {
                        using (var haloPaint = new SKPaint
                        {
                            Color = ColorToSKColor(style.LabelHaloColor),
                            TextSize = textPaint.TextSize,
                            IsAntialias = true,
                            Typeface = defaultTypeface
                        })
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                for (int dy = -1; dy <= 1; dy++)
                                {
                                    if (dx != 0 || dy != 0) // Skip the center
                                    {
                                        canvas.DrawText(labelText, x1 + dx, y1 + textBounds.Height + dy, haloPaint);
                                    }
                                }
                            }
                        }
                    }

                    // Draw the label text
                    canvas.DrawText(labelText, x1, y1 + textBounds.Height, textPaint);
                }
            }
            catch (Exception ex)
            {
                // Handle any unexpected exceptions - could log this instead
                System.Diagnostics.Debug.WriteLine($"Error drawing label: {ex.Message}");
            }

            // Restore the canvas state
            canvas.Restore();

            // Reapply the world coordinate transformation
            transformer.ApplyTransformation(canvas);
        }

        /// <summary>
        /// Helper function to convert System.Drawing.Color to SKColor
        /// </summary>
        private SKColor ColorToSKColor(Color color)
        {
            return new SKColor((byte)color.R, (byte)color.G, (byte)color.B, (byte)color.A);
        }

        /// <summary>
        /// Converts screen rectangle to SKRect
        /// </summary>
        public SKRect RectToSKRect(Rectangle rect)
        {
            return new SKRect(rect.Left, rect.Top, rect.Right, rect.Bottom);
        }
    }
}