using System;
using FCoreMap.Geometries;

namespace FCoreMap.Interactions
{
    /// <summary>
    /// Event arguments for polygon-related events.
    /// </summary>
    public class PolygonEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the polygon associated with the event.
        /// </summary>
        public PolygonD Polygon { get; private set; }

        /// <summary>
        /// Gets the polygon layer associated with the event.
        /// </summary>
        public Layer PolygonLayer { get; private set; }

        /// <summary>
        /// Creates a new instance of the PolygonEventArgs class.
        /// </summary>
        /// <param name="polygon">The polygon associated with the event.</param>
        /// <param name="layer">The layer containing the polygon.</param>
        public PolygonEventArgs(PolygonD polygon, Layer layer)
        {
            Polygon = polygon;
            PolygonLayer = layer;
        }
    }

    /// <summary>
    /// Event arguments for polygon modification events.
    /// </summary>
    public class PolygonModifiedEventArgs : PolygonEventArgs
    {
        /// <summary>
        /// Gets the index of the modified vertex, if a specific vertex was modified.
        /// </summary>
        public int ModifiedVertexIndex { get; private set; }

        /// <summary>
        /// Gets the original position of the modified vertex, if available.
        /// </summary>
        public PointD OriginalVertexPosition { get; private set; }

        /// <summary>
        /// Gets the type of modification that was performed.
        /// </summary>
        public PolygonModificationType ModificationType { get; private set; }

        /// <summary>
        /// Creates a new instance of the PolygonModifiedEventArgs class.
        /// </summary>
        /// <param name="polygon">The polygon that was modified.</param>
        /// <param name="layer">The layer containing the polygon.</param>
        /// <param name="modificationType">The type of modification performed.</param>
        /// <param name="modifiedVertexIndex">The index of the modified vertex, or -1 if not applicable.</param>
        /// <param name="originalVertexPosition">The original position of the modified vertex, or null if not available.</param>
        public PolygonModifiedEventArgs(PolygonD polygon, Layer layer, PolygonModificationType modificationType,
                                      int modifiedVertexIndex = -1, PointD originalVertexPosition = null)
            : base(polygon, layer)
        {
            ModifiedVertexIndex = modifiedVertexIndex;
            OriginalVertexPosition = originalVertexPosition;
            ModificationType = modificationType;
        }
    }

    /// <summary>
    /// Defines the types of modifications that can be performed on a polygon.
    /// </summary>
    public enum PolygonModificationType
    {
        /// <summary>
        /// A vertex was added to the polygon.
        /// </summary>
        VertexAdded,

        /// <summary>
        /// A vertex was moved within the polygon.
        /// </summary>
        VertexMoved,

        /// <summary>
        /// A vertex was deleted from the polygon.
        /// </summary>
        VertexDeleted,

        /// <summary>
        /// The entire polygon was moved.
        /// </summary>
        PolygonMoved,

        /// <summary>
        /// The polygon was completed (all vertices were added during drawing).
        /// </summary>
        PolygonCompleted
    }
}