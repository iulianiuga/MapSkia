using System;
using SkiaSharp;
using FCoreMap.Geometries;

namespace FCoreMap.Controls
{
    /// <summary>
    /// Handles the transformation between world coordinates and screen (pixel) coordinates using SkiaSharp.
    /// </summary>
    public class CoordinateTransformerSk
    {
        // World coordinate bounds
        private double worldMinX;
        private double worldMinY;
        private double worldMaxX;
        private double worldMaxY;

        // Screen (pixel) dimensions
        private int screenWidth;
        private int screenHeight;

        // Margin as a percentage of the view size
        private double marginPercent = 0.00;

        // Scale factor - how many pixels per world unit
        private double scaleFactorX;
        private double scaleFactorY;

        // Offset values for translating coordinates
        private float offsetX;
        private float offsetY;

        // SkiaSharp matrix for transformation
        private SKMatrix transformMatrix;
        private SKMatrix inverseTransformMatrix;
        private bool matrixValid;

        /// <summary>
        /// Gets or sets the margin percentage (0.0 to 1.0) around the world extents.
        /// </summary>
        public double MarginPercent
        {
            get { return marginPercent; }
            set 
            { 
                marginPercent = Math.Max(0, Math.Min(0.5, value));
                matrixValid = false;
            }
        }

        /// <summary>
        /// Creates a new coordinate transformer with default values.
        /// </summary>
        public CoordinateTransformerSk()
        {
            // Default values
            worldMinX = -100;
            worldMinY = -100;
            worldMaxX = 100;
            worldMaxY = 100;
            screenWidth = 800;
            screenHeight = 600;
            matrixValid = false;

            // Calculate initial transformation parameters
            CalculateTransformationParameters();
        }

        /// <summary>
        /// Gets the current world extents.
        /// </summary>
        /// <param name="minX">Output parameter for minimum X coordinate</param>
        /// <param name="minY">Output parameter for minimum Y coordinate</param>
        /// <param name="maxX">Output parameter for maximum X coordinate</param>
        /// <param name="maxY">Output parameter for maximum Y coordinate</param>
        public void GetWorldExtents(out double minX, out double minY, out double maxX, out double maxY)
        {
            minX = worldMinX;
            minY = worldMinY;
            maxX = worldMaxX;
            maxY = worldMaxY;
        }

        /// <summary>
        /// Sets the world coordinate bounds.
        /// </summary>
        public void SetWorldExtents(double minX, double minY, double maxX, double maxY)
        {
            // Validate the extents (ensure max > min)
            if (maxX <= minX)
            {
                maxX = minX + 1;
            }

            if (maxY <= minY)
            {
                maxY = minY + 1;
            }

            worldMinX = minX;
            worldMinY = minY;
            worldMaxX = maxX;
            worldMaxY = maxY;
            matrixValid = false;

            // Recalculate transformation parameters
            CalculateTransformationParameters();
        }

        /// <summary>
        /// Sets the screen dimensions in pixels.
        /// </summary>
        public void SetScreenDimensions(int width, int height)
        {
            screenWidth = Math.Max(1, width);
            screenHeight = Math.Max(1, height);
            matrixValid = false;

            // Recalculate transformation parameters
            CalculateTransformationParameters();
        }

        /// <summary>
        /// Calculates the transformation parameters based on current world and screen dimensions.
        /// </summary>
        private void CalculateTransformationParameters()
        {
            try
            {
                // Calculate world dimensions with margin
                double worldWidth = worldMaxX - worldMinX;
                double worldHeight = worldMaxY - worldMinY;

                // Add margins
                double marginX = worldWidth * marginPercent;
                double marginY = worldHeight * marginPercent;

                double extendedMinX = worldMinX - marginX;
                double extendedMinY = worldMinY - marginY;
                double extendedMaxX = worldMaxX + marginX;
                double extendedMaxY = worldMaxY + marginY;

                double extendedWidth = extendedMaxX - extendedMinX;
                double extendedHeight = extendedMaxY - extendedMinY;

                // If width or height are too small, adjust them to avoid division by zero
                if (extendedWidth < 0.00001) extendedWidth = 0.00001;
                if (extendedHeight < 0.00001) extendedHeight = 0.00001;

                // Calculate the scale factors (how many pixels per world unit)
                scaleFactorX = screenWidth / extendedWidth;
                scaleFactorY = screenHeight / extendedHeight;

                // Use the smaller scale to ensure the entire world fits within the screen
                double scaleFactor = Math.Min(scaleFactorX, scaleFactorY);
                scaleFactorX = scaleFactor;
                scaleFactorY = scaleFactor;

                // Calculate translation to center the world in the screen
                double worldCenterX = (extendedMinX + extendedMaxX) / 2;
                double worldCenterY = (extendedMinY + extendedMaxY) / 2;

                double screenCenterX = screenWidth / 2.0;
                double screenCenterY = screenHeight / 2.0;

                // Calculate offsets (translation after scaling)
                offsetX = (float)(screenCenterX - worldCenterX * scaleFactor);
                offsetY = (float)(screenCenterY + worldCenterY * scaleFactor); // Flipping Y axis

                // Create the transformation matrix for SkiaSharp
                // The matrix transforms world coordinates to screen coordinates
                // We need to:
                // 1. Scale by scaleFactorX and scaleFactorY (inverted for Y to flip the Y axis)
                // 2. Translate by offsetX and offsetY
                
                transformMatrix = SKMatrix.CreateIdentity();
                
                // Apply scaling
                transformMatrix = transformMatrix.PostConcat(
                    SKMatrix.CreateScale((float)scaleFactorX, (float)-scaleFactorY));
                
                // Apply translation
                transformMatrix = transformMatrix.PostConcat(
                    SKMatrix.CreateTranslation(offsetX, offsetY));
                
                // Calculate the inverse matrix for screen to world transformation
                if (!transformMatrix.TryInvert(out inverseTransformMatrix))
                {
                    // If matrix can't be inverted, set a default identity matrix
                    inverseTransformMatrix = SKMatrix.CreateIdentity();
                }
                
                matrixValid = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in CalculateTransformationParameters: {ex.Message}");
                // Use default values in case of error
                scaleFactorX = 1.0;
                scaleFactorY = 1.0;
                offsetX = screenWidth / 2.0f;
                offsetY = screenHeight / 2.0f;
                
                transformMatrix = SKMatrix.CreateIdentity();
                inverseTransformMatrix = SKMatrix.CreateIdentity();
                matrixValid = false;
            }
        }

        /// <summary>
        /// Applies the transformation to the canvas
        /// </summary>
        public void ApplyTransformation(SKCanvas canvas)
        {
            if (!matrixValid)
            {
                CalculateTransformationParameters();
            }
            
            canvas.SetMatrix(transformMatrix);
        }

        /// <summary>
        /// Resets the transformation on the canvas
        /// </summary>
        public void ResetTransformation(SKCanvas canvas)
        {
            canvas.ResetMatrix();
        }

        /// <summary>
        /// Converts world coordinates directly to screen coordinates without using a transformation matrix.
        /// </summary>
        public SKPoint WorldToScreen(double x, double y)
        {
            if (!matrixValid)
            {
                CalculateTransformationParameters();
            }
            
            // Apply the transformation matrix to the point
            SKPoint screenPoint = transformMatrix.MapPoint(new SKPoint((float)x, (float)y));
            return screenPoint;
        }

        /// <summary>
        /// Converts screen coordinates to world coordinates without using a transformation matrix.
        /// </summary>
        public PointD ScreenToWorld(float x, float y)
        {
            if (!matrixValid)
            {
                CalculateTransformationParameters();
            }
            
            // Apply the inverse transformation matrix to the point
            SKPoint worldPoint = inverseTransformMatrix.MapPoint(new SKPoint(x, y));
            return new PointD(worldPoint.X, worldPoint.Y);
        }

        /// <summary>
        /// Gets the current scale factor (pixels per world unit).
        /// </summary>
        public double GetWorldToScreenScale()
        {
            return scaleFactorX; // X and Y scales are the same
        }

        /// <summary>
        /// Returns a string representation of this transformer's settings.
        /// </summary>
        public override string ToString()
        {
            return $"Scale: {scaleFactorX:F3}, Offset: ({offsetX:F1}, {offsetY:F1})";
        }
    }
}