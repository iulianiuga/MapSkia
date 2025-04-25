using System;
using System.Drawing;

namespace FCoreMap.Geometries
{
    /// <summary>
    /// Represents the shape of a point in a point layer.
    /// </summary>
    public enum PointShape
    {
        Circle,
        Square,
        Triangle,
        Diamond,
        Cross,
        Star
    }

    /// <summary>
    /// Represents the style of a line in a line layer.
    /// </summary>
    public enum LineStyle
    {
        Solid,
        Dashed,
        Dotted,
        DashDot,
        DashDotDot
    }

    /// <summary>
    /// Represents the fill pattern of a polygon in a polygon layer.
    /// </summary>
    public enum FillPattern
    {
        Solid,
        Horizontal,
        Vertical,
        Diagonal,
        CrossHatch,
        DiagonalCrossHatch,
        None
    }

    /// <summary>
    /// Represents the position of a label relative to its feature.
    /// </summary>
    public enum LabelPosition
    {
        Center,
        Top,
        Bottom,
        Left,
        Right,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    /// <summary>
    /// Provides styling options for map layers.
    /// </summary>
    public class LayerStyle
    {
        // Common properties
        public Color Color { get; set; }
        public Color OutlineColor { get; set; }
        public float OutlineWidth { get; set; }
        public float Opacity { get; set; } // 0.0 to 1.0

        // Point specific properties
        public PointShape PointShape { get; set; }
        public float PointSize { get; set; }

        // Line specific properties
        public LineStyle LineStyle { get; set; }
        public float LineWidth { get; set; }

        // Polygon specific properties
        public FillPattern FillPattern { get; set; }
        public bool ShowFill { get; set; }

        // Label properties
        public bool ShowLabels { get; set; }
        public string LabelField { get; set; }
        public Color LabelColor { get; set; }
        public Font LabelFont { get; set; }
        public float LabelOffset { get; set; }
        public LabelPosition LabelPosition { get; set; }
        public bool LabelHalo { get; set; }
        public Color LabelHaloColor { get; set; }
        public float LabelScale { get; set; }
        public bool LabelBackground { get; set; }
        public Color LabelBackgroundColor { get; set; }

        /// <summary>
        /// Creates a default layer style.
        /// </summary>
        public LayerStyle()
        {
            // Default values
            Color = Color.Blue;
            OutlineColor = Color.Black;
            OutlineWidth = 1.0f;
            Opacity = 1.0f;

            // Point defaults
            PointShape = PointShape.Circle;
            PointSize = 6.0f;

            // Line defaults
            LineStyle = LineStyle.Solid;
            LineWidth = 1.0f;

            // Polygon defaults
            FillPattern = FillPattern.Solid;
            ShowFill = true;

            // Label defaults
            ShowLabels = false;
            LabelField = "Name";
            LabelColor = Color.Black;
            LabelFont = new Font("Arial", 8);
            LabelOffset = 2.0f;
            LabelPosition = LabelPosition.Center;
            LabelHalo = false;
            LabelHaloColor = Color.White;
            LabelScale = 1.0f;
            LabelBackground = true;
            LabelBackgroundColor = Color.FromArgb(180, Color.White);
        }

        /// <summary>
        /// Creates a layer style with the specified color.
        /// </summary>
        /// <param name="color">The main color for the style.</param>
        public LayerStyle(Color color) : this()
        {
            Color = color;
        }

        /// <summary>
        /// Creates a basic point style.
        /// </summary>
        /// <param name="color">The point color.</param>
        /// <param name="size">The point size.</param>
        /// <param name="shape">The point shape.</param>
        /// <returns>A new LayerStyle instance configured for points.</returns>
        public static LayerStyle CreatePointStyle(Color color, float size, PointShape shape)
        {
            return new LayerStyle
            {
                Color = color,
                PointSize = size,
                PointShape = shape,
                OutlineColor = Color.FromArgb(Math.Max(0, color.R - 50), Math.Max(0, color.G - 50), Math.Max(0, color.B - 50)),
                OutlineWidth = 1.0f
            };
        }

        /// <summary>
        /// Creates a basic line style.
        /// </summary>
        /// <param name="color">The line color.</param>
        /// <param name="width">The line width.</param>
        /// <param name="style">The line style.</param>
        /// <returns>A new LayerStyle instance configured for lines.</returns>
        public static LayerStyle CreateLineStyle(Color color, float width, LineStyle style)
        {
            return new LayerStyle
            {
                Color = color,
                LineWidth = width,
                LineStyle = style
            };
        }

        /// <summary>
        /// Creates a basic polygon style.
        /// </summary>
        /// <param name="fillColor">The fill color.</param>
        /// <param name="outlineColor">The outline color.</param>
        /// <param name="fillPattern">The fill pattern.</param>
        /// <param name="opacity">The fill opacity (0.0 to 1.0).</param>
        /// <returns>A new LayerStyle instance configured for polygons.</returns>
        public static LayerStyle CreatePolygonStyle(Color fillColor, Color outlineColor, FillPattern fillPattern, float opacity)
        {
            return new LayerStyle
            {
                Color = fillColor,
                OutlineColor = outlineColor,
                OutlineWidth = 1.5f,
                FillPattern = fillPattern,
                Opacity = opacity,
                ShowFill = true
            };
        }

        /// <summary>
        /// Creates a basic circle style.
        /// </summary>
        /// <param name="fillColor">The fill color.</param>
        /// <param name="outlineColor">The outline color.</param>
        /// <param name="fillPattern">The fill pattern.</param>
        /// <param name="opacity">The fill opacity (0.0 to 1.0).</param>
        /// <returns>A new LayerStyle instance configured for circles.</returns>
        public static LayerStyle CreateCircleStyle(Color fillColor, Color outlineColor, FillPattern fillPattern, float opacity)
        {
            return new LayerStyle
            {
                Color = fillColor,
                OutlineColor = outlineColor,
                OutlineWidth = 1.5f,
                FillPattern = fillPattern,
                Opacity = opacity,
                ShowFill = true
            };
        }

        /// <summary>
        /// Configures label settings for this style.
        /// </summary>
        /// <param name="showLabels">Whether to show labels.</param>
        /// <param name="labelField">The field from attribute data to use for labeling.</param>
        /// <param name="color">The label color.</param>
        /// <param name="fontSize">The label font size.</param>
        public void ConfigureLabels(bool showLabels, string labelField, Color color, float fontSize = 8)
        {
            ShowLabels = showLabels;
            LabelField = labelField;
            LabelColor = color;
            LabelFont = new Font("Arial", fontSize);
        }

        /// <summary>
        /// Creates a copy of this style.
        /// </summary>
        /// <returns>A new LayerStyle with the same properties as this one.</returns>
        public LayerStyle Clone()
        {
            return new LayerStyle
            {
                Color = this.Color,
                OutlineColor = this.OutlineColor,
                OutlineWidth = this.OutlineWidth,
                Opacity = this.Opacity,
                PointShape = this.PointShape,
                PointSize = this.PointSize,
                LineStyle = this.LineStyle,
                LineWidth = this.LineWidth,
                FillPattern = this.FillPattern,
                ShowFill = this.ShowFill,
                ShowLabels = this.ShowLabels,
                LabelField = this.LabelField,
                LabelColor = this.LabelColor,
                LabelFont = (Font)this.LabelFont.Clone(),
                LabelOffset = this.LabelOffset,
                LabelPosition = this.LabelPosition,
                LabelHalo = this.LabelHalo,
                LabelHaloColor = this.LabelHaloColor,
                LabelScale = this.LabelScale,
                LabelBackground = this.LabelBackground,
                LabelBackgroundColor = this.LabelBackgroundColor
            };
        }

        /// <summary>
        /// Returns a string representation of this style.
        /// </summary>
        /// <returns>A string describing this style.</returns>
        public override string ToString()
        {
            return $"Style [Color: {Color}, Opacity: {Opacity:P0}, Labels: {(ShowLabels ? "Visible" : "Hidden")}]";
        }
    }
}