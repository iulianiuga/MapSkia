// BoundingBox.cs - New Class
using Newtonsoft.Json;
using System.Numerics;
using System.Text;

namespace FCoreMap.Geometries
{
    public class BoundingBox
    {
        /// <summary>
        /// Gets the minimum X coordinate of the bounding box.
        /// </summary>
        public double MinX { get; private set; }

        /// <summary>
        /// Gets the minimum Y coordinate of the bounding box.
        /// </summary>
        public double MinY { get; private set; }

        /// <summary>
        /// Gets the maximum X coordinate of the bounding box.
        /// </summary>
        public double MaxX { get; private set; }

        /// <summary>
        /// Gets the maximum Y coordinate of the bounding box.
        /// </summary>
        public double MaxY { get; private set; }

        /// <summary>
        /// Gets the width of the bounding box.
        /// </summary>
        public double Width => MaxX - MinX;

        /// <summary>
        /// Gets the height of the bounding box.
        /// </summary>
        public double Height => MaxY - MinY;

        /// <summary>
        /// Gets the center X coordinate of the bounding box.
        /// </summary>
        public double CenterX => MinX + Width / 2;

        /// <summary>
        /// Gets the center Y coordinate of the bounding box.
        /// </summary>
        public double CenterY => MinY + Height / 2;

        /// <summary>
        /// Initializes a new instance of the BoundingBox class with default values.
        /// This creates an "empty" bounding box that will be expanded as needed.
        /// </summary>
        public BoundingBox()
        {
            // Start with extreme values that will be replaced by the first point added
            MinX = double.MaxValue;
            MinY = double.MaxValue;
            MaxX = double.MinValue;
            MaxY = double.MinValue;
        }

        /// <summary>
        /// Initializes a new instance of the BoundingBox class with specified values.
        /// </summary>
        public BoundingBox(double minX, double minY, double maxX, double maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        /// <summary>
        /// Expands the bounding box to include the specified point.
        /// </summary>
        public void ExpandToInclude(double x, double y)
        {
            MinX = Math.Min(MinX, x);
            MinY = Math.Min(MinY, y);
            MaxX = Math.Max(MaxX, x);
            MaxY = Math.Max(MaxY, y);
        }

        /// <summary>
        /// Expands the bounding box to include the specified point.
        /// </summary>
        public void ExpandToInclude(PointD point)
        {
            if (point != null)
            {
                // Directly use X and Y values to avoid infinite recursion
                MinX = Math.Min(MinX, point.X);
                MinY = Math.Min(MinY, point.Y);
                MaxX = Math.Max(MaxX, point.X);
                MaxY = Math.Max(MaxY, point.Y);
            }
        }

        /// <summary>
        /// Expands the bounding box to include the specified bounding box.
        /// </summary>
        public void ExpandToInclude(BoundingBox other)
        {
            if (other != null)
            {
                MinX = Math.Min(MinX, other.MinX);
                MinY = Math.Min(MinY, other.MinY);
                MaxX = Math.Max(MaxX, other.MaxX);
                MaxY = Math.Max(MaxY, other.MaxY);
            }
        }

        /// <summary>
        /// Checks if this bounding box intersects with another bounding box.
        /// </summary>
        public bool Intersects(BoundingBox other)
        {
            if (other == null)
                return false;

            return MinX <= other.MaxX && MaxX >= other.MinX &&
                   MinY <= other.MaxY && MaxY >= other.MinY;
        }

        /// <summary>
        /// Checks if this bounding box contains a point.
        /// </summary>
        public bool Contains(PointD point)
        {
            if (point == null)
                return false;

            return point.X >= MinX && point.X <= MaxX &&
                   point.Y >= MinY && point.Y <= MaxY;
        }

        /// <summary>
        /// Checks if this bounding box completely contains another bounding box.
        /// </summary>
        public bool Contains(BoundingBox other)
        {
            if (other == null)
                return false;

            return MinX <= other.MinX && MaxX >= other.MaxX &&
                   MinY <= other.MinY && MaxY >= other.MaxY;
        }

        public override string ToString()
        {
            return $"BoundingBox: MinX={MinX:F2}, MinY={MinY:F2}, MaxX={MaxX:F2}, MaxY={MaxY:F2}, Width={Width:F2}, Height={Height:F2}";
        }
    }
}

// PointD.cs Modifications


namespace FCoreMap.Geometries
{
    public class PointD
    {
        public double X { get; set; }
        public double Y { get; set; }
        public bool IsSelected { get; set; }
        public int Id { get; set; }
        public BoundingBox BoundingBox { get; private set; }

        public PointD(double x, double y)
        {
            X = x;
            Y = y;
            IsSelected = false;
            Id = -1;
            CalculateBoundingBox();
        }

        // Calculate distance to another point
        public double DistanceTo(PointD other)
        {
            double dx = other.X - X;
            double dy = other.Y - Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        // Calculate the bounding box for this point
        private void CalculateBoundingBox()
        {
            // For a point, the min and max coordinates are the same
            BoundingBox = new BoundingBox(X, Y, X, Y);
        }

        // Update the bounding box after point coordinates change
        public void UpdateBoundingBox()
        {
            CalculateBoundingBox();
        }

        public override string ToString()
        {
            string selectionStatus = IsSelected ? " (Selected)" : "";
            return $"({X:F2}, {Y:F2}){selectionStatus} [ID: {Id}]";
        }
    }
}

// LineD.cs Modifications
namespace FCoreMap.Geometries
{
    public class LineD
    {
        private List<PointD> points;
        public bool IsSelected { get; set; }
        public int Id { get; set; }
        public BoundingBox BoundingBox { get; private set; }

        // Default constructor creates an empty line
        public LineD()
        {
            points = new List<PointD>();
            IsSelected = false;
            Id = -1;
            BoundingBox = new BoundingBox();
        }

        // Constructor that initializes with a collection of points
        public LineD(IEnumerable<PointD> initialPoints)
        {
            points = new List<PointD>(initialPoints);
            IsSelected = false;
            Id = -1;
            CalculateBoundingBox();
        }

        // Add a single point to the line
        public void AddPoint(PointD point)
        {
            points.Add(point);
            // Just update the existing bounding box by expanding it
            if (point != null && BoundingBox != null)
            {
                BoundingBox.ExpandToInclude(point);
            }
            else
            {
                // Full recalculation if needed
                CalculateBoundingBox();
            }
        }

        // Add multiple points to the line
        public void AddPoints(IEnumerable<PointD> newPoints)
        {
            points.AddRange(newPoints);
            CalculateBoundingBox();
        }

        // Remove a point at a specific index
        public bool RemovePointAt(int index)
        {
            if (index >= 0 && index < points.Count)
            {
                points.RemoveAt(index);
                CalculateBoundingBox();
                return true;
            }
            return false;
        }

        // Get a point at a specific index
        public PointD GetPointAt(int index)
        {
            if (index >= 0 && index < points.Count)
            {
                return points[index];
            }
            return null;
        }

        // Calculate the bounding box for this line
        private void CalculateBoundingBox()
        {
            // Create a fresh bounding box with extreme initial values
            BoundingBox = new BoundingBox();

            // Manually find min/max to avoid potential recursion issues
            if (points.Count > 0)
            {
                double minX = double.MaxValue;
                double minY = double.MaxValue;
                double maxX = double.MinValue;
                double maxY = double.MinValue;

                foreach (var point in points)
                {
                    minX = Math.Min(minX, point.X);
                    minY = Math.Min(minY, point.Y);
                    maxX = Math.Max(maxX, point.X);
                    maxY = Math.Max(maxY, point.Y);
                }

                // Now create a new bounding box with the calculated values
                BoundingBox = new BoundingBox(minX, minY, maxX, maxY);
            }
        }

        // Update the bounding box when a point changes
        public void UpdateBoundingBox()
        {
            CalculateBoundingBox();
        }

        // Property to get the number of points in the line
        public int PointCount => points.Count;

        // Property to access all points in the line
        public IReadOnlyList<PointD> Points => points.AsReadOnly();

        // Calculate the total length of the line
        public double CalculateLength()
        {
            double length = 0;
            for (int i = 0; i < points.Count - 1; i++)
            {
                length += points[i].DistanceTo(points[i + 1]);
            }
            return length;
        }

        // Override ToString to provide a string representation of the line
        public override string ToString()
        {
            if (points.Count == 0)
                return "Empty Line";

            StringBuilder sb = new StringBuilder();
            string selectionStatus = IsSelected ? " (Selected)" : "";
            sb.Append("Line with ");
            sb.Append(points.Count);
            sb.Append(" points");
            sb.Append(selectionStatus);
            sb.Append($" [ID: {Id}]");
            sb.AppendLine(":");
            sb.AppendLine(BoundingBox.ToString());

            for (int i = 0; i < points.Count; i++)
            {
                sb.Append("  ");
                sb.Append(i);
                sb.Append(": ");
                sb.AppendLine(points[i].ToString());
            }

            return sb.ToString();
        }
    }
}

// PolygonD.cs Modifications
namespace FCoreMap.Geometries
{
    public class PolygonD
    {
        private List<PointD> vertices;
        public bool IsSelected { get; set; }
        public int Id { get; set; }
        public BoundingBox BoundingBox { get; private set; }

        public Vector3 From3dPoint { get; set; }

        // Default constructor creates an empty polygon
        public PolygonD()
        {
            vertices = new List<PointD>();
            IsSelected = false;
            Id = -1;
            BoundingBox = new BoundingBox();
            From3dPoint = new Vector3(0,0,0);
        }

        // Constructor that initializes with a collection of vertices
        public PolygonD(IEnumerable<PointD> initialVertices)
        {
            vertices = new List<PointD>(initialVertices);
            IsSelected = false;
            Id = -1;
            CalculateBoundingBox();
        }

        // Add a single vertex to the polygon
        public void AddVertex(PointD vertex)
        {
            vertices.Add(vertex);
            // Just update the existing bounding box by expanding it
            if (vertex != null && BoundingBox != null)
            {
                BoundingBox.ExpandToInclude(vertex);
            }
            else
            {
                // Full recalculation if needed
                CalculateBoundingBox();
            }
        }

        // Add multiple vertices to the polygon
        public void AddVertices(IEnumerable<PointD> newVertices)
        {
            vertices.AddRange(newVertices);
            CalculateBoundingBox();
        }

        // Remove a vertex at a specific index
        public bool RemoveVertexAt(int index)
        {
            if (index >= 0 && index < vertices.Count)
            {
                vertices.RemoveAt(index);
                CalculateBoundingBox();
                return true;
            }
            return false;
        }

        // Get a vertex at a specific index
        public PointD GetVertexAt(int index)
        {
            if (index >= 0 && index < vertices.Count)
            {
                return vertices[index];
            }
            return null;
        }

        // Calculate the bounding box for this polygon
        private void CalculateBoundingBox()
        {
            // Create a fresh bounding box with extreme initial values
            BoundingBox = new BoundingBox();

            // Manually find min/max to avoid potential recursion issues
            if (vertices.Count > 0)
            {
                double minX = double.MaxValue;
                double minY = double.MaxValue;
                double maxX = double.MinValue;
                double maxY = double.MinValue;

                foreach (var vertex in vertices)
                {
                    minX = Math.Min(minX, vertex.X);
                    minY = Math.Min(minY, vertex.Y);
                    maxX = Math.Max(maxX, vertex.X);
                    maxY = Math.Max(maxY, vertex.Y);
                }

                // Now create a new bounding box with the calculated values
                BoundingBox = new BoundingBox(minX, minY, maxX, maxY);
            }
        }

        // Update the bounding box when a vertex changes
        public void UpdateBoundingBox()
        {
            CalculateBoundingBox();
        }

        // Property to get the number of vertices in the polygon
        public int VertexCount => vertices.Count;

        // Property to access all vertices in the polygon
        public IReadOnlyList<PointD> Vertices => vertices.AsReadOnly();

        // Calculate the perimeter of the polygon
        public double CalculatePerimeter()
        {
            if (vertices.Count < 3)
                return 0;

            double perimeter = 0;

            // Calculate the distance between consecutive vertices
            for (int i = 0; i < vertices.Count; i++)
            {
                int nextIndex = (i + 1) % vertices.Count; // Wrap around to the first vertex
                perimeter += vertices[i].DistanceTo(vertices[nextIndex]);
            }

            return perimeter;
        }

        // Calculate the area of the polygon using the Shoelace formula (Gauss's area formula)
        public double CalculateArea()
        {
            if (vertices.Count < 3)
                return 0;

            double area = 0;

            for (int i = 0; i < vertices.Count; i++)
            {
                int j = (i + 1) % vertices.Count;
                area += vertices[i].X * vertices[j].Y;
                area -= vertices[j].X * vertices[i].Y;
            }

            return Math.Abs(area) / 2;
        }

        // Check if a point is inside the polygon using the ray casting algorithm
        public bool ContainsPoint(PointD point)
        {
            if (vertices.Count < 3)
                return false;

            // Quick check using bounding box first
            if (!BoundingBox.Contains(point))
                return false;

            bool inside = false;

            for (int i = 0, j = vertices.Count - 1; i < vertices.Count; j = i++)
            {
                if (((vertices[i].Y > point.Y) != (vertices[j].Y > point.Y)) &&
                    (point.X < (vertices[j].X - vertices[i].X) * (point.Y - vertices[i].Y) /
                    (vertices[j].Y - vertices[i].Y) + vertices[i].X))
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        // Override ToString to provide a string representation of the polygon
        public override string ToString()
        {
            if (vertices.Count == 0)
                return "Empty Polygon";

            StringBuilder sb = new StringBuilder();
            string selectionStatus = IsSelected ? " (Selected)" : "";
            sb.Append("Polygon with ");
            sb.Append(vertices.Count);
            sb.Append(" vertices");
            sb.Append(selectionStatus);
            sb.Append($" [ID: {Id}]");
            sb.AppendLine(":");
            sb.AppendLine(BoundingBox.ToString());
            sb.AppendLine($"Area: {CalculateArea():F2}");
            sb.AppendLine($"Perimeter: {CalculatePerimeter():F2}");

            for (int i = 0; i < vertices.Count; i++)
            {
                sb.Append("  ");
                sb.Append(i);
                sb.Append(": ");
                sb.AppendLine(vertices[i].ToString());
            }

            return sb.ToString();
        }
    }
}

// CircleD.cs Modifications
namespace FCoreMap.Geometries
{
    public class CircleD
    {
        // Private backing field
        private List<PointD> _userDefinedPoints;

        /// <summary>
        /// Gets or sets the center point of the circle.
        /// </summary>
        public PointD Center { get; set; }

        /// <summary>
        /// Gets or sets the radius of the circle.
        /// </summary>
        public double Radius { get; set; }

        /// <summary>
        /// Gets or sets the elevation value of the circle.
        /// </summary>
        public double Elevation { get; set; }

        /// <summary>
        /// Gets or sets whether the circle is selected.
        /// </summary>
        public bool IsSelected { get; set; }

        /// <summary>
        /// Gets or sets the ID of the circle (for attribute mapping).
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets the bounding box of the circle.
        /// </summary>
        public BoundingBox BoundingBox { get; private set; }

        /// <summary>
        /// Gets the list of user-defined points associated with this circle (read-only view).
        /// </summary>
        [JsonIgnore]
        public IReadOnlyList<PointD> UserDefinedPoints => _userDefinedPoints.AsReadOnly();

        /// <summary>
        /// Used for JSON serialization/deserialization of user-defined points.
        /// </summary>
        [JsonProperty("UserDefinedPoints")]
        public List<PointD> SerializedUserDefinedPoints
        {
            get => _userDefinedPoints;
            set => _userDefinedPoints = value ?? new List<PointD>();
        }

        /// <summary>
        /// Constructor for JSON deserialization.
        /// </summary>
        [JsonConstructor]
        public CircleD(PointD center, double radius, double elevation, bool isSelected, int id,
                      BoundingBox boundingBox, List<PointD> userDefinedPoints)
        {
            Center = center ?? new PointD(0, 0);
            Radius = radius;
            Elevation = elevation;
            IsSelected = isSelected;
            Id = id;
            BoundingBox = boundingBox;

            // Use only the specified points, don't generate defaults
            _userDefinedPoints = userDefinedPoints ?? new List<PointD>();

            // If BoundingBox is null, calculate it
          //  if (boundingBox == null)
         //   {
                CalculateBoundingBox();
         //   }
        }

        /// <summary>
        /// Initializes a new instance of the CircleD class with default values.
        /// </summary>
        public CircleD()
        {
            Center = new PointD(0, 0);
            Radius = 1.0;
            Elevation = 0.0;
            IsSelected = false;
            Id = -1; // Default to -1 (unmapped)
            _userDefinedPoints = new List<PointD>();

            // Generate 3 default user-defined points at 0, 120, and 240 degrees
            for (int i = 0; i < 3; i++)
            {
                // Calculate the angle in radians: 0, 120, 240 degrees.
                double angleRadians = (i * 120.0) * Math.PI / 180.0;
                double x = Center.X + Radius * Math.Cos(angleRadians);
                double y = Center.Y + Radius * Math.Sin(angleRadians);
                _userDefinedPoints.Add(new PointD(x, y));
            }

            CalculateBoundingBox();
        }

        /// <summary>
        /// Initializes a new instance of the CircleD class with specified center and radius.
        /// </summary>
        /// <param name="center">The center point of the circle.</param>
        /// <param name="radius">The radius of the circle.</param>
        public CircleD(PointD center, double radius)
        {
            Center = center ?? new PointD(0, 0);
            Radius = Math.Max(0, radius);
            Elevation = 0.0;
            IsSelected = false;
            Id = -1; // Default to -1 (unmapped)
            _userDefinedPoints = new List<PointD>();

            // Generate 3 default user-defined points at 0, 120, and 240 degrees
            for (int i = 0; i < 3; i++)
            {
                // Calculate the angle in radians: 0, 120, 240 degrees.
                double angleRadians = (i * 120.0) * Math.PI / 180.0;
                double x = center.X + radius * Math.Cos(angleRadians);
                double y = center.Y + radius * Math.Sin(angleRadians);
                _userDefinedPoints.Add(new PointD(x, y));
            }

            CalculateBoundingBox();
        }

        /// <summary>
        /// Initializes a new instance of the CircleD class with specified center, radius, and elevation.
        /// </summary>
        /// <param name="center">The center point of the circle.</param>
        /// <param name="radius">The radius of the circle.</param>
        /// <param name="elevation">The elevation value of the circle.</param>
        public CircleD(PointD center, double radius, double elevation)
        {
            Center = center ?? new PointD(0, 0);
            Radius = Math.Max(0, radius);
            Elevation = elevation;
            IsSelected = false;
            Id = -1; // Default to -1 (unmapped)
            _userDefinedPoints = new List<PointD>();

            // Generate 3 default user-defined points at 0, 120, and 240 degrees
            for (int i = 0; i < 3; i++)
            {
                // Calculate the angle in radians: 0, 120, 240 degrees.
                double angleRadians = (i * 120.0) * Math.PI / 180.0;
                double x = center.X + radius * Math.Cos(angleRadians);
                double y = center.Y + radius * Math.Sin(angleRadians);
                _userDefinedPoints.Add(new PointD(x, y));
            }

            CalculateBoundingBox();
        }

        /// <summary>
        /// Initializes a new instance of the CircleD class with specified center, radius, and user-defined points.
        /// </summary>
        /// <param name="center">The center point of the circle.</param>
        /// <param name="radius">The radius of the circle.</param>
        /// <param name="userDefinedPoints">The collection of user-defined points.</param>
        /// <param name="elevation">The elevation value of the circle.</param>
        public CircleD(PointD center, double radius, IEnumerable<PointD> userDefinedPoints, double elevation = 0.0)
        {
            Center = center ?? new PointD(0, 0);
            Radius = Math.Max(0, radius);
            Elevation = elevation;
            IsSelected = false;
            Id = -1; // Default to -1 (unmapped)

            // Use only the specified points
            _userDefinedPoints = userDefinedPoints != null
                ? new List<PointD>(userDefinedPoints)
                : new List<PointD>();

            CalculateBoundingBox();
        }

        /// <summary>
        /// Initializes a new instance of the CircleD class with specified 3 Points
        /// </summary>
        /// <param name="p1">First pointd</param>
        /// <param name="p2">Second point</param>
        /// <param name="p3">Third point</param>
        /// <param name="elevation">The elevation value of the circle.</param>
        public CircleD(PointD p1, PointD p2, PointD p3, double elevation = 0)
        {
            double d = 2 * (p1.X * (p2.Y - p3.Y) +
                     p2.X * (p3.Y - p1.Y) +
                     p3.X * (p1.Y - p2.Y));

            //if (Math.Abs(d) < 1e-6)
            //  throw new ArgumentException("The three points are collinear and cannot define a unique circle.");

            // Calculate squared distances
            double p1Sq = p1.X * p1.X + p1.Y * p1.Y;
            double p2Sq = p2.X * p2.X + p2.Y * p2.Y;
            double p3Sq = p3.X * p3.X + p3.Y * p3.Y;

            // Compute center (circumcenter) coordinates
            double centerX = (p1Sq * (p2.Y - p3.Y) +
                              p2Sq * (p3.Y - p1.Y) +
                              p3Sq * (p1.Y - p2.Y)) / d;

            double centerY = (p1Sq * (p3.X - p2.X) +
                              p2Sq * (p1.X - p3.X) +
                              p3Sq * (p2.X - p1.X)) / d;

            PointD center = new PointD(centerX, centerY);

            // Calculate the radius (distance from the center to any of the three points)
            double radius = center.DistanceTo(p1);

            Center = center ?? new PointD(0, 0);
            Radius = Math.Max(0, radius);
            Elevation = elevation;
            IsSelected = false;
            Id = -1; // Default to -1 (unmapped)

            // Initialize with ONLY the 3 user-defined points, no default points
            _userDefinedPoints = new List<PointD>(3);
            _userDefinedPoints.Add(p1);
            _userDefinedPoints.Add(p2);
            _userDefinedPoints.Add(p3);

            CalculateBoundingBox();
        }

        /// <summary>
        /// Calculates the bounding box for this circle.
        /// </summary>
        private void CalculateBoundingBox()
        {
            BoundingBox = new BoundingBox(
                Center.X - Radius,
                Center.Y - Radius,
                Center.X + Radius,
                Center.Y + Radius
            );
        }

        /// <summary>
        /// Updates the bounding box when circle properties change.
        /// </summary>
        public void UpdateBoundingBox()
        {
            CalculateBoundingBox();
        }

        /// <summary>
        /// Adds a user-defined point to the circle.
        /// </summary>
        /// <param name="point">The point to add.</param>
        public void AddUserDefinedPoint(PointD point)
        {
            if (point != null)
            {
                _userDefinedPoints.Add(point);
                // Note: We don't update the bounding box here since user-defined points
                // don't affect the circle's geometric bounds
            }
        }

        /// <summary>
        /// Adds multiple user-defined points to the circle.
        /// </summary>
        /// <param name="points">The collection of points to add.</param>
        public void AddUserDefinedPoints(IEnumerable<PointD> points)
        {
            if (points != null)
            {
                _userDefinedPoints.AddRange(points);
            }
        }

        /// <summary>
        /// Removes a user-defined point at the specified index.
        /// </summary>
        /// <param name="index">The index of the point to remove.</param>
        /// <returns>True if the point was successfully removed; otherwise, false.</returns>
        public bool RemoveUserDefinedPointAt(int index)
        {
            if (index >= 0 && index < _userDefinedPoints.Count)
            {
                _userDefinedPoints.RemoveAt(index);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets a user-defined point at the specified index.
        /// </summary>
        /// <param name="index">The index of the point to get.</param>
        /// <returns>The point at the specified index, or null if the index is out of range.</returns>
        public PointD GetUserDefinedPointAt(int index)
        {
            if (index >= 0 && index < _userDefinedPoints.Count)
            {
                return _userDefinedPoints[index];
            }
            return null;
        }

        /// <summary>
        /// Gets the number of user-defined points.
        /// </summary>
        public int UserDefinedPointCount => _userDefinedPoints.Count;

        /// <summary>
        /// Clears all user-defined points.
        /// </summary>
        public void ClearUserDefinedPoints()
        {
            _userDefinedPoints.Clear();
        }

        /// <summary>
        /// Calculates the area of the circle.
        /// </summary>
        /// <returns>The area of the circle.</returns>
        public double CalculateArea()
        {
            return Math.PI * Radius * Radius;
        }

        /// <summary>
        /// Calculates the circumference of the circle.
        /// </summary>
        /// <returns>The circumference of the circle.</returns>
        public double CalculateCircumference()
        {
            return 2 * Math.PI * Radius;
        }

        /// <summary>
        /// Checks if a point is inside the circle.
        /// </summary>
        /// <param name="point">The point to check.</param>
        /// <returns>True if the point is inside the circle; otherwise, false.</returns>
        public bool ContainsPoint(PointD point)
        {
            if (point == null)
                return false;

            // Quick check using bounding box first
            if (!BoundingBox.Contains(point))
                return false;

            return Center.DistanceTo(point) <= Radius;
        }

        /// <summary>
        /// Gets a collection of points that approximate the circle for rendering.
        /// </summary>
        /// <param name="segments">The number of line segments to use for the approximation.</param>
        /// <returns>A collection of points that form a polygon approximating the circle.</returns>
        public IEnumerable<PointD> GetApproximationPoints(int segments = 36)
        {
            segments = Math.Max(8, segments); // Ensure minimum number of segments

            List<PointD> points = new List<PointD>(segments);
            double angleIncrement = 2 * Math.PI / segments;

            for (int i = 0; i < segments; i++)
            {
                double angle = i * angleIncrement;
                double x = Center.X + Radius * Math.Cos(angle);
                double y = Center.Y + Radius * Math.Sin(angle);
                points.Add(new PointD(x, y));
            }

            return points;
        }

        /// <summary>
        /// Returns a string representation of this circle.
        /// </summary>
        /// <returns>A string describing this circle.</returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            string selectionStatus = IsSelected ? " (Selected)" : "";
            sb.Append($"Circle{selectionStatus} [ID: {Id}]:");
            sb.AppendLine($" Center: {Center}, Radius: {Radius:F2}, Elevation: {Elevation:F2}");
            sb.AppendLine(BoundingBox.ToString());
            sb.AppendLine($"Area: {CalculateArea():F2}, Circumference: {CalculateCircumference():F2}");

            if (_userDefinedPoints.Count > 0)
            {
                sb.AppendLine($"User-Defined Points ({_userDefinedPoints.Count}):");
                for (int i = 0; i < _userDefinedPoints.Count; i++)
                {
                    sb.AppendLine($"  {i}: {_userDefinedPoints[i]}");
                }
            }

            return sb.ToString();
        }
    }
}