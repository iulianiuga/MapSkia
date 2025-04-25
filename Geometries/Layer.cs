using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FCoreMap.Geometries
{
    // Define an enum for layer type
    public enum LayerType
    {
        Point,
        Line,
        Polygon,
        Circle
    }

    public class Layer
    {
        private string name;
        private LayerType type;
        private bool visible;
        private LayerStyle style;

        // Collections for each geometry type
        private List<PointD> points;
        private List<LineD> lines;
        private List<PolygonD> polygons;
        private List<CircleD> circles;

        // Add attribute data storage
        private DataTable attributeData;
        private Dictionary<int, int> geometryToAttributeMap;

        // Properties
        public string Name
        {
            get { return name; }
            set { name = value; }
        }

        public LayerType Type
        {
            get { return type; }
            private set { type = value; }
        }

        public bool Visible
        {
            get { return visible; }
            set { visible = value; }
        }

        public LayerStyle Style
        {
            get { return style; }
            set { style = value; }
        }

        // Add property for attribute data
        public DataTable AttributeData
        {
            get { return attributeData; }
            set { attributeData = value; }
        }

        // Add property for the geometry-to-attribute mapping
        public Dictionary<int, int> GeometryToAttributeMap
        {
            get { return geometryToAttributeMap; }
            set { geometryToAttributeMap = value; }
        }

        // Constructor for a layer with default style
        public Layer(string name, LayerType type)
        {
            this.name = name;
            this.type = type;
            this.visible = true;
            this.geometryToAttributeMap = new Dictionary<int, int>();

            // Initialize with default style based on layer type
            InitializeDefaultStyle();

            // Initialize the appropriate collection based on layer type
            switch (type)
            {
                case LayerType.Point:
                    points = new List<PointD>();
                    break;
                case LayerType.Line:
                    lines = new List<LineD>();
                    break;
                case LayerType.Polygon:
                    polygons = new List<PolygonD>();
                    break;
                case LayerType.Circle:
                    circles = new List<CircleD>();
                    break;
            }
        }

        // Constructor with custom style
        public Layer(string name, LayerType type, LayerStyle customStyle)
        {
            this.name = name;
            this.type = type;
            this.visible = true;
            this.style = customStyle;
            this.geometryToAttributeMap = new Dictionary<int, int>();

            // Initialize the appropriate collection based on layer type
            switch (type)
            {
                case LayerType.Point:
                    points = new List<PointD>();
                    break;
                case LayerType.Line:
                    lines = new List<LineD>();
                    break;
                case LayerType.Polygon:
                    polygons = new List<PolygonD>();
                    break;
                case LayerType.Circle:
                    circles = new List<CircleD>();
                    break;
            }
        }

        // Initialize default style based on layer type
        private void InitializeDefaultStyle()
        {
            switch (type)
            {
                case LayerType.Point:
                    style = LayerStyle.CreatePointStyle(System.Drawing.Color.Blue, 6.0f, PointShape.Circle);
                    break;
                case LayerType.Line:
                    style = LayerStyle.CreateLineStyle(System.Drawing.Color.Red, 1.5f, LineStyle.Solid);
                    break;
                case LayerType.Polygon:
                    style = LayerStyle.CreatePolygonStyle(System.Drawing.Color.Green, System.Drawing.Color.Black, FillPattern.Solid, 0.7f);
                    break;
                case LayerType.Circle:
                    style = LayerStyle.CreatePolygonStyle(System.Drawing.Color.Purple, System.Drawing.Color.Black, FillPattern.Solid, 0.6f);
                    break;
            }
        }

        // Get label text for a geometry based on its index
        public string GetLabelText(int geometryIndex)
        {
            if (attributeData == null || string.IsNullOrEmpty(style.LabelField) ||
                !geometryToAttributeMap.TryGetValue(geometryIndex, out int attributeRowIndex) ||
                attributeRowIndex >= attributeData.Rows.Count)
            {
                return string.Empty;
            }

            // Get the value from the attribute table
            DataRow row = attributeData.Rows[attributeRowIndex];
            if (!attributeData.Columns.Contains(style.LabelField))
            {
                return string.Empty;
            }

            object value = row[style.LabelField];
            return value != null ? value.ToString() : string.Empty;
        }

        #region Point Management

        // Add a point (only valid for point layers)
        public bool AddPoint(PointD point)
        {
            if (type != LayerType.Point)
                return false;

            // Set the ID for the point to its index in the collection
            point.Id = points.Count;
            points.Add(point);
            return true;
        }

        // Add multiple points (only valid for point layers)
        public bool AddPoints(IEnumerable<PointD> newPoints)
        {
            if (type != LayerType.Point)
                return false;

            // Set IDs for each point based on its index in the collection
            int startId = points.Count;
            foreach (var point in newPoints)
            {
                point.Id = startId++;
                points.Add(point);
            }
            return true;
        }

        // Remove a point at a specific index (only valid for point layers)
        public bool RemovePointAt(int index)
        {
            if (type != LayerType.Point || index < 0 || index >= points.Count)
                return false;

            points.RemoveAt(index);

            // Update IDs for all points after the removed one
            for (int i = index; i < points.Count; i++)
            {
                points[i].Id = i;
            }

            // Remove from the attribute mapping
            geometryToAttributeMap.Remove(index);

            // Update the mappings for all geometries after the removed one
            var keysToUpdate = geometryToAttributeMap.Keys.Where(k => k > index).ToList();
            foreach (var key in keysToUpdate)
            {
                int value = geometryToAttributeMap[key];
                geometryToAttributeMap.Remove(key);
                geometryToAttributeMap[key - 1] = value;
            }

            return true;
        }

        // Get point count (only valid for point layers)
        public int GetPointCount()
        {
            return type == LayerType.Point ? points.Count : 0;
        }

        // Get all points (only valid for point layers)
        public IReadOnlyList<PointD> GetPoints()
        {
            return type == LayerType.Point ? points.AsReadOnly() : null;
        }

        #endregion

        #region Line Management

        // Add a line (only valid for line layers)
        public bool AddLine(LineD line)
        {
            if (type != LayerType.Line)
                return false;

            // Set the ID for the line to its index in the collection
            line.Id = lines.Count;
            lines.Add(line);
            return true;
        }

        // Add multiple lines (only valid for line layers)
        public bool AddLines(IEnumerable<LineD> newLines)
        {
            if (type != LayerType.Line)
                return false;

            // Set IDs for each line based on its index in the collection
            int startId = lines.Count;
            foreach (var line in newLines)
            {
                line.Id = startId++;
                lines.Add(line);
            }
            return true;
        }

        // Remove a line at a specific index (only valid for line layers)
        public bool RemoveLineAt(int index)
        {
            if (type != LayerType.Line || index < 0 || index >= lines.Count)
                return false;

            lines.RemoveAt(index);

            // Update IDs for all lines after the removed one
            for (int i = index; i < lines.Count; i++)
            {
                lines[i].Id = i;
            }

            // Remove from the attribute mapping
            geometryToAttributeMap.Remove(index);

            // Update the mappings for all geometries after the removed one
            var keysToUpdate = geometryToAttributeMap.Keys.Where(k => k > index).ToList();
            foreach (var key in keysToUpdate)
            {
                int value = geometryToAttributeMap[key];
                geometryToAttributeMap.Remove(key);
                geometryToAttributeMap[key - 1] = value;
            }

            return true;
        }

        // Get line count (only valid for line layers)
        public int GetLineCount()
        {
            return type == LayerType.Line ? lines.Count : 0;
        }

        // Get all lines (only valid for line layers)
        public IReadOnlyList<LineD> GetLines()
        {
            return type == LayerType.Line ? lines.AsReadOnly() : null;
        }

        #endregion

        #region Polygon Management

        // Add a polygon (only valid for polygon layers)
        public bool AddPolygon(PolygonD polygon)
        {
            if (type != LayerType.Polygon)
                return false;

            // Set the ID for the polygon to its index in the collection
            polygon.Id = polygons.Count;
            polygons.Add(polygon);
            return true;
        }

        // Add multiple polygons (only valid for polygon layers)
        public bool AddPolygons(IEnumerable<PolygonD> newPolygons)
        {
            if (type != LayerType.Polygon)
                return false;

            // Set IDs for each polygon based on its index in the collection
            int startId = polygons.Count;
            foreach (var polygon in newPolygons)
            {
                polygon.Id = startId++;
                polygons.Add(polygon);
            }
            return true;
        }

        // Remove a polygon at a specific index (only valid for polygon layers)
        public bool RemovePolygonAt(int index)
        {
            if (type != LayerType.Polygon || index < 0 || index >= polygons.Count)
                return false;

            polygons.RemoveAt(index);

            // Update IDs for all polygons after the removed one
            for (int i = index; i < polygons.Count; i++)
            {
                polygons[i].Id = i;
            }

            // Remove from the attribute mapping
            geometryToAttributeMap.Remove(index);

            // Update the mappings for all geometries after the removed one
            var keysToUpdate = geometryToAttributeMap.Keys.Where(k => k > index).ToList();
            foreach (var key in keysToUpdate)
            {
                int value = geometryToAttributeMap[key];
                geometryToAttributeMap.Remove(key);
                geometryToAttributeMap[key - 1] = value;
            }

            return true;
        }

        // Get polygon count (only valid for polygon layers)
        public int GetPolygonCount()
        {
            return type == LayerType.Polygon ? polygons.Count : 0;
        }

        // Get all polygons (only valid for polygon layers)
        public IReadOnlyList<PolygonD> GetPolygons()
        {
            return type == LayerType.Polygon ? polygons.AsReadOnly() : null;
        }

        #endregion

        #region Circle Management

        // Add a circle (only valid for circle layers)
        public bool AddCircle(CircleD circle)
        {
            if (type != LayerType.Circle)
                return false;

            // Set the ID for the circle to its index in the collection
            circle.Id = circles.Count;
            circles.Add(circle);
            return true;
        }

        // Add multiple circles (only valid for circle layers)
        public bool AddCircles(IEnumerable<CircleD> newCircles)
        {
            if (type != LayerType.Circle)
                return false;

            // Set IDs for each circle based on its index in the collection
            int startId = circles.Count;
            foreach (var circle in newCircles)
            {
                circle.Id = startId++;
                circles.Add(circle);
            }
            return true;
        }

        // Remove a circle at a specific index (only valid for circle layers)
        public bool RemoveCircleAt(int index)
        {
            if (type != LayerType.Circle || index < 0 || index >= circles.Count)
                return false;

            circles.RemoveAt(index);

            // Update IDs for all circles after the removed one
            for (int i = index; i < circles.Count; i++)
            {
                circles[i].Id = i;
            }

            // Remove from the attribute mapping
            geometryToAttributeMap.Remove(index);

            // Update the mappings for all geometries after the removed one
            var keysToUpdate = geometryToAttributeMap.Keys.Where(k => k > index).ToList();
            foreach (var key in keysToUpdate)
            {
                int value = geometryToAttributeMap[key];
                geometryToAttributeMap.Remove(key);
                geometryToAttributeMap[key - 1] = value;
            }

            return true;
        }

        // Get circle count (only valid for circle layers)
        public int GetCircleCount()
        {
            return type == LayerType.Circle ? circles.Count : 0;
        }

        // Get all circles (only valid for circle layers)
        public IReadOnlyList<CircleD> GetCircles()
        {
            return type == LayerType.Circle ? circles.AsReadOnly() : null;
        }

        #endregion

        // Clear all geometries in the layer
        public void Clear()
        {
            switch (type)
            {
                case LayerType.Point:
                    points.Clear();
                    break;
                case LayerType.Line:
                    lines.Clear();
                    break;
                case LayerType.Polygon:
                    polygons.Clear();
                    break;
                case LayerType.Circle:
                    circles.Clear();
                    break;
            }

            // Clear the geometry-to-attribute mapping
            geometryToAttributeMap.Clear();
        }

        // Get the count of geometries in the layer
        public int Count
        {
            get
            {
                switch (type)
                {
                    case LayerType.Point:
                        return points.Count;
                    case LayerType.Line:
                        return lines.Count;
                    case LayerType.Polygon:
                        return polygons.Count;
                    case LayerType.Circle:
                        return circles.Count;
                    default:
                        return 0;
                }
            }
        }

        // Calculate total length (for line layers) or perimeter (for polygon layers) or circumference (for circle layers)
        public double CalculateTotalLength()
        {
            double total = 0;

            switch (type)
            {
                case LayerType.Line:
                    foreach (LineD line in lines)
                    {
                        total += line.CalculateLength();
                    }
                    break;
                case LayerType.Polygon:
                    foreach (PolygonD polygon in polygons)
                    {
                        total += polygon.CalculatePerimeter();
                    }
                    break;
                case LayerType.Circle:
                    foreach (CircleD circle in circles)
                    {
                        total += circle.CalculateCircumference();
                    }
                    break;
            }

            return total;
        }

        // Calculate total area (only for polygon and circle layers)
        public double CalculateTotalArea()
        {
            double total = 0;

            switch (type)
            {
                case LayerType.Polygon:
                    foreach (PolygonD polygon in polygons)
                    {
                        total += polygon.CalculateArea();
                    }
                    break;
                case LayerType.Circle:
                    foreach (CircleD circle in circles)
                    {
                        total += circle.CalculateArea();
                    }
                    break;
            }

            return total;
        }

        // Override ToString to provide a string representation of the layer
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Layer: {name} ({type})");
            sb.AppendLine($"Visible: {visible}");
            sb.AppendLine($"Style: {style}");

            switch (type)
            {
                case LayerType.Point:
                    sb.AppendLine($"Point Count: {points.Count}");
                    break;
                case LayerType.Line:
                    sb.AppendLine($"Line Count: {lines.Count}");
                    sb.AppendLine($"Total Length: {CalculateTotalLength():F2}");
                    break;
                case LayerType.Polygon:
                    sb.AppendLine($"Polygon Count: {polygons.Count}");
                    sb.AppendLine($"Total Area: {CalculateTotalArea():F2}");
                    sb.AppendLine($"Total Perimeter: {CalculateTotalLength():F2}");
                    break;
                case LayerType.Circle:
                    sb.AppendLine($"Circle Count: {circles.Count}");
                    sb.AppendLine($"Total Area: {CalculateTotalArea():F2}");
                    sb.AppendLine($"Total Circumference: {CalculateTotalLength():F2}");
                    break;
            }

            if (attributeData != null)
            {
                sb.AppendLine($"Attribute Rows: {attributeData.Rows.Count}");
                sb.AppendLine($"Attribute Columns: {string.Join(", ", attributeData.Columns.Cast<DataColumn>().Select(c => c.ColumnName))}");
            }

            return sb.ToString();
        }
    }
}