using System;
using FCoreMap.Geometries;

namespace FCoreMap.Interactions
{
    /// <summary>
    /// Event arguments for circle-related events.
    /// </summary>
    public class CircleEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the circle associated with the event.
        /// </summary>
        public CircleD Circle { get; private set; }

        /// <summary>
        /// Gets the circle layer associated with the event.
        /// </summary>
        public Layer CircleLayer { get; private set; }

        /// <summary>
        /// Creates a new instance of the CircleEventArgs class.
        /// </summary>
        /// <param name="circle">The circle associated with the event.</param>
        /// <param name="layer">The layer containing the circle.</param>
        public CircleEventArgs(CircleD circle, Layer layer)
        {
            Circle = circle;
            CircleLayer = layer;
        }
    }

    /// <summary>
    /// Event arguments for circle modification events.
    /// </summary>
    public class CircleModifiedEventArgs : CircleEventArgs
    {
        /// <summary>
        /// Gets the index of the modified point, if a specific point was modified.
        /// </summary>
        public int ModifiedPointIndex { get; private set; }

        /// <summary>
        /// Gets the original position of the modified point, if available.
        /// </summary>
        public PointD OriginalPointPosition { get; private set; }

        /// <summary>
        /// Creates a new instance of the CircleModifiedEventArgs class.
        /// </summary>
        /// <param name="circle">The circle that was modified.</param>
        /// <param name="layer">The layer containing the circle.</param>
        /// <param name="modifiedPointIndex">The index of the modified point, or -1 if not applicable.</param>
        /// <param name="originalPointPosition">The original position of the modified point, or null if not available.</param>
        public CircleModifiedEventArgs(CircleD circle, Layer layer, int modifiedPointIndex = -1, PointD originalPointPosition = null)
            : base(circle, layer)
        {
            ModifiedPointIndex = modifiedPointIndex;
            OriginalPointPosition = originalPointPosition;
        }
    }
}