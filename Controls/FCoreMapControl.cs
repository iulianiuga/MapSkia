using System;

using System.Drawing;

using System.Windows.Forms;

using System.ComponentModel;

using FCoreMap.Geometries;

using FCoreMap.Interactions;

using SkiaSharp;

using SkiaSharp.Views.Desktop;

using System.Diagnostics;

using FCoreMap.Interactions.FCoreMap.Controls;



namespace FCoreMap.Controls

{

    /// <summary>

    /// A SkiaSharp-based UserControl that renders map layers with various geometries and handles coordinate transformation.

    /// Supports panning, zooming, circle drawing/editing, and coordinate transformation.

    /// </summary>

    public class FCoreMapControlSk : SKGLControl

    {

        private LayerManager layerManager;

        private SKColor backgroundColor;

        private CoordinateTransformerSk transformer;

        private bool showWorldCoordinates = false;

        private MapInteractionManagerSk interactionManager;

        private MapRendererSk mapRenderer;



        /// <summary>

        /// Event raised when a circle is drawn on the map.

        /// </summary>

        public event EventHandler<CircleEventArgs> CircleDrawn;



        /// <summary>

        /// Event raised when a circle is modified on the map.

        /// </summary>

        public event EventHandler<CircleModifiedEventArgs> CircleModified;



        /// <summary>

        /// Event raised when a polygon is drawn on the map.

        /// </summary>

        public event EventHandler<PolygonEventArgs> PolygonDrawn;



        /// <summary>

        /// Event raised when a polygon is modified on the map.

        /// </summary>

        public event EventHandler<PolygonModifiedEventArgs> PolygonModified;



        /// <summary>

        /// Define a custom event for mouse movement with coordinates

        /// </summary>

        public event EventHandler<MapMouseEventArgs> MapMouseMove;



        /// <summary>

        /// Gets or sets the active layer for polygon drawing and editing.

        /// </summary>

        [Browsable(false)]

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]

        public Layer ActivePolygonLayer

        {

            get { return interactionManager.PolygonDrawingLayer; }

            set

            {

                if (value == null || value.Type == LayerType.Polygon)

                {

                    interactionManager.SetPolygonDrawingLayer(value);

                }

            }

        }



        private void InitializePolygonEvents()

        {

            // Wire up the interaction manager events for polygons

            interactionManager.PolygonDrawn += InteractionManager_PolygonDrawn;

            interactionManager.PolygonModified += InteractionManager_PolygonModified;

        }



        /// <summary>

        /// Begins drawing a polygon on the specified polygon layer.

        /// </summary>

        /// <param name="polygonLayer">The layer to add the polygon to.</param>

        /// <returns>True if polygon drawing mode was started successfully.</returns>

        public bool StartPolygonDrawing(Layer polygonLayer)

        {

            if (polygonLayer == null || polygonLayer.Type != LayerType.Polygon)

                return false;



            // Set the active polygon layer

            interactionManager.SetPolygonDrawingLayer(polygonLayer);



            // Switch to polygon drawing behavior

            Behavior = MapBehavior.PolygonDrawing;



            // Start drawing

            interactionManager.StartPolygonDrawing();



            return true;

        }



        /// <summary>

        /// Cancels the current polygon drawing operation.

        /// </summary>

        public void CancelPolygonDrawing()

        {

            if (Behavior == MapBehavior.PolygonDrawing)

            {

                interactionManager.CancelPolygonDrawing();

            }

        }



        /// <summary>

        /// Begins editing polygons in the specified polygon layer.

        /// </summary>

        /// <param name="polygonLayer">The layer containing polygons to edit.</param>

        /// <returns>True if polygon editing mode was started successfully.</returns>

        public bool StartPolygonEditing(Layer polygonLayer)

        {

            if (polygonLayer == null || polygonLayer.Type != LayerType.Polygon)

                return false;



            // Set the active polygon layer

            interactionManager.SetPolygonDrawingLayer(polygonLayer);



            // Start polygon editing mode

            interactionManager.StartPolygonEditing(polygonLayer);



            return true;

        }

        /// <summary>
        /// Deletes the currently selected polygon in polygon editing mode.
        /// </summary>
        /// <returns>True if a polygon was successfully deleted, false otherwise.</returns>
        public bool DeleteSelectedPolygon()
        {
            // Check if we're in polygon editing mode and have a polygon layer
            if (interactionManager.Behavior != MapBehavior.PolygonEditing ||
                interactionManager.PolygonDrawingLayer == null)
            {
                return false;
            }

            // Get the selected polygon
            PolygonD polygon = interactionManager.SelectedPolygon;
            if (polygon == null)
            {
                return false;
            }

            // Get the layer
            Layer layer = interactionManager.PolygonDrawingLayer;

            // Record the ID before removing
            int polygonId = polygon.Id;

            // Remove the polygon from the layer
            bool success = layer.RemovePolygonAt(polygonId);

            if (success)
            {
                // After deletion, restart polygon editing mode to reset selection state
                StartPolygonEditing(layer);

                // Redraw the map
                Invalidate();
            }

            return success;
        }



        private void InteractionManager_PolygonDrawn(object sender, PolygonEventArgs e)

        {

            // Forward the event to any listeners of FCoreMapControlSk.PolygonDrawn

            PolygonDrawn?.Invoke(this, e);

        }



        private void InteractionManager_PolygonModified(object sender, PolygonModifiedEventArgs e)

        {

            // Forward the event to any listeners of FCoreMapControlSk.PolygonModified

            PolygonModified?.Invoke(this, e);

        }



        // Method to add to your FCoreMapControlSk constructor

        private void InitializeMouseEvents()

        {

            // Wire up the MouseMove event

            this.MouseMove += FCoreMapControlSk_MouseMoveInternal;

        }



        private void FCoreMapControlSk_MouseMoveInternal(object sender, System.Windows.Forms.MouseEventArgs e)

        {

            // Get the world coordinates using the transformer

            var worldCoord = transformer.ScreenToWorld(e.X, e.Y);



            // Fire the custom event with both coordinate systems

            OnMapMouseMove(new MapMouseEventArgs(e.X, e.Y, worldCoord.X, worldCoord.Y, e));

        }



        // Method to raise the custom event

        protected virtual void OnMapMouseMove(MapMouseEventArgs e)

        {

            MapMouseMove?.Invoke(this, e);

        }



        /// <summary>

        /// Gets or sets the layer manager.

        /// </summary>

        [Browsable(false)]

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]

        public LayerManager LayerManager

        {

            get { return layerManager; }

            set

            {

                layerManager = value;

                Invalidate();

            }

        }



        /// <summary>

        /// Gets or sets the background color of the map.

        /// </summary>

        [Category("Appearance")]

        [Description("The background color of the map.")]

        public Color BackgroundColor

        {

            get { return Color.FromArgb(backgroundColor.Alpha, backgroundColor.Red, backgroundColor.Green, backgroundColor.Blue); }

            set

            {

                backgroundColor = new SKColor((byte)value.R, (byte)value.G, (byte)value.B, (byte)value.A);

                Invalidate();

            }

        }



        /// <summary>

        /// Gets or sets whether to show world coordinates in the bottom corner.

        /// </summary>

        [Category("Appearance")]

        [Description("Determines whether to show world coordinates in the bottom corner.")]

        [DefaultValue(false)]

        public bool ShowWorldCoordinates

        {

            get { return showWorldCoordinates; }

            set

            {

                showWorldCoordinates = value;

                Invalidate();

            }

        }



        /// <summary>

        /// Gets or sets the current interaction behavior of the map.

        /// </summary>

        [Category("Behavior")]

        [Description("Determines how the map responds to mouse interactions.")]

        [DefaultValue(MapBehavior.Pan)]

        public MapBehavior Behavior

        {

            get { return interactionManager.Behavior; }

            set

            {

                // If changing from CircleDrawing to another behavior, cancel any active drawing

                if (interactionManager.Behavior == MapBehavior.CircleDrawing &&

                    value != MapBehavior.CircleDrawing &&

                    interactionManager.IsDrawingCircle)

                {

                    interactionManager.CancelCircleDrawing();

                }



                interactionManager.Behavior = value;

            }

        }



        /// <summary>

        /// Gets or sets the zoom factor applied with each mouse wheel tick.

        /// </summary>

        [Category("Behavior")]

        [Description("The zoom factor applied with each mouse wheel tick. Values greater than 1 zoom in, values less than 1 zoom out.")]

        [DefaultValue(1.2)]

        public double WheelZoomFactor

        {

            get { return interactionManager.WheelZoomFactor; }

            set { interactionManager.WheelZoomFactor = value; }

        }



        /// <summary>

        /// Gets or sets whether mouse wheel zooming centers on the mouse position.

        /// </summary>

        [Category("Behavior")]

        [Description("When true, mouse wheel zooming centers on the cursor position. When false, it zooms to the center of the map.")]

        [DefaultValue(true)]

        public bool ZoomToMousePosition

        {

            get { return interactionManager.ZoomToMousePosition; }

            set { interactionManager.ZoomToMousePosition = value; }

        }



        /// <summary>

        /// Gets or sets the active layer for circle drawing and editing.

        /// </summary>

        [Browsable(false)]

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]

        public Layer ActiveCircleLayer

        {

            get { return interactionManager.CircleDrawingLayer; }

            set

            {

                if (value == null || value.Type == LayerType.Circle)

                {

                    interactionManager.SetCircleDrawingLayer(value);

                }

            }

        }



        /// <summary>

        /// Gets the coordinate transformer used by this control.

        /// </summary>

        [Browsable(false)]

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]

        public CoordinateTransformerSk Transformer

        {

            get { return transformer; }

        }



        /// <summary>

        /// Gets the interaction manager that handles user input for the map.

        /// </summary>

        [Browsable(false)]

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]

        public MapInteractionManagerSk InteractionManager

        {

            get { return interactionManager; }

        }



        /// <summary>

        /// Gets the renderer that handles drawing operations for the map.

        /// </summary>

        [Browsable(false)]

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]

        public MapRendererSk MapRenderer

        {

            get { return mapRenderer; }

        }



        /// <summary>

        /// Initializes a new instance of the FCoreMapControlSk class.

        /// </summary>

        public FCoreMapControlSk()

        {

            // Initialize default values
            layerManager = new LayerManager();
            backgroundColor = new SKColor(255, 255, 255); // White
            transformer = new CoordinateTransformerSk();

            // Create the map renderer
            mapRenderer = new MapRendererSk(transformer);

            // Handle resize events
            this.Resize += FCoreMapControlSk_Resize;

            // Create the interaction manager
            interactionManager = new MapInteractionManagerSk(this, transformer);

            // Wire up the interaction manager events
            interactionManager.CircleDrawn += InteractionManager_CircleDrawn;
            interactionManager.CircleModified += InteractionManager_CircleModified;

            // Wire up the polygon events
            interactionManager.PolygonDrawn += InteractionManager_PolygonDrawn;
            interactionManager.PolygonModified += InteractionManager_PolygonModified;

            InitializeMouseEvents();

        }



        private void InteractionManager_CircleDrawn(object sender, CircleEventArgs e)

        {

            // Forward the event to any listeners of FCoreMapControlSk.CircleDrawn

            CircleDrawn?.Invoke(this, e);

        }



        private void InteractionManager_CircleModified(object sender, CircleModifiedEventArgs e)

        {

            // Forward the event to any listeners of FCoreMapControlSk.CircleModified

            CircleModified?.Invoke(this, e);

        }



        private void FCoreMapControlSk_Resize(object sender, EventArgs e)

        {

            // Update the transformer with the new screen dimensions

            transformer.SetScreenDimensions(this.Width, this.Height);

            Invalidate();

        }



        /// <summary>

        /// Zooms to fit all visible layer data in the viewport.

        /// </summary>

        public void ZoomAll()

        {

            if (layerManager == null)

                return;



            double minX, minY, maxX, maxY;

            if (layerManager.CalculateVisibleLayersBounds(out minX, out minY, out maxX, out maxY))

            {

                // Set the world extents in the transformer

                transformer.SetWorldExtents(minX, minY, maxX, maxY);

            }

            else

            {

                // If no layers have data, set default extents

                transformer.SetWorldExtents(-100, -100, 100, 100);

            }



            // Update the screen dimensions

            transformer.SetScreenDimensions(this.Width, this.Height);



            // Raise the world bounds changed event

            OnWorldBoundsChanged();



            // Redraw the control

            Invalidate();

        }



        /// <summary>

        /// Zooms to a specific window defined by world coordinates.

        /// </summary>

        /// <param name="minX">Minimum X coordinate in world units</param>

        /// <param name="minY">Minimum Y coordinate in world units</param>

        /// <param name="maxX">Maximum X coordinate in world units</param>

        /// <param name="maxY">Maximum Y coordinate in world units</param>

        public void ZoomToWindow(double minX, double minY, double maxX, double maxY)

        {

            // Validate input coordinates

            if (maxX <= minX || maxY <= minY)

            {

                // Invalid window dimensions, return without changing view

                return;

            }



            // Set the world extents in the transformer

            transformer.SetWorldExtents(minX, minY, maxX, maxY);



            // Update the screen dimensions

            transformer.SetScreenDimensions(this.Width, this.Height);



            // Raise the world bounds changed event

            OnWorldBoundsChanged();



            // Redraw the control

            Invalidate();

        }



        /// <summary>

        /// Begins drawing a circle on the specified circle layer.

        /// </summary>

        /// <param name="circleLayer">The layer to add the circle to.</param>

        /// <returns>True if circle drawing mode was started successfully.</returns>

        public bool StartCircleDrawing(Layer circleLayer)

        {

            if (circleLayer == null || circleLayer.Type != LayerType.Circle)

                return false;



            // Set the active circle layer

            interactionManager.SetCircleDrawingLayer(circleLayer);



            // Switch to circle drawing behavior

            Behavior = MapBehavior.CircleDrawing;



            // Start drawing

            interactionManager.StartCircleDrawing();



            return true;

        }



        /// <summary>

        /// Cancels the current circle drawing operation.

        /// </summary>

        public void CancelCircleDrawing()

        {

            if (Behavior == MapBehavior.CircleDrawing)

            {

                interactionManager.CancelCircleDrawing();

            }

        }



        /// <summary>

        /// Begins editing circles in the specified circle layer.

        /// </summary>

        /// <param name="circleLayer">The layer containing circles to edit.</param>

        /// <returns>True if circle editing mode was started successfully.</returns>

        public bool StartCircleEditing(Layer circleLayer)

        {

            if (circleLayer == null || circleLayer.Type != LayerType.Circle)

                return false;



            // Set the active circle layer

            interactionManager.SetCircleDrawingLayer(circleLayer);



            // Start circle editing mode

            interactionManager.StartCircleEditing(circleLayer);



            return true;

        }

        /// <summary>
        /// Deletes the currently selected circle in circle editing mode.
        /// </summary>
        /// <returns>True if a circle was successfully deleted, false otherwise.</returns>
        public bool DeleteSelectedCircle()
        {
            // Check if we're in circle editing mode and have a circle layer
            if (interactionManager.Behavior != MapBehavior.CircleEditing ||
                interactionManager.CircleDrawingLayer == null)
            {
                return false;
            }

            // Get the selected circle
            CircleD circle = interactionManager.SelectedCircle;
            if (circle == null)
            {
                return false;
            }

            // Get the layer
            Layer layer = interactionManager.CircleDrawingLayer;

            // Record the ID before removing
            int circleId = circle.Id;

            // Remove the circle from the layer
            bool success = layer.RemoveCircleAt(circleId);

            if (success)
            {
                // After deletion, restart circle editing mode to reset selection state
                StartCircleEditing(layer);

                // Redraw the map
                Invalidate();
            }

            return success;
        }

        /// <summary>

        /// Checks if the specified circle can be edited (has enough user-defined points).

        /// </summary>

        /// <param name="circle">The circle to check.</param>

        /// <returns>True if the circle can be edited, false otherwise.</returns>

        public bool CanEditCircle(CircleD circle)

        {

            return circle != null && circle.UserDefinedPointCount >= 3;

        }



        /// <summary>

        /// SkiaSharp paint handler

        /// </summary>

        protected override void OnPaintSurface(SKPaintGLSurfaceEventArgs e)

        {

            base.OnPaintSurface(e);



            SKSurface surface = e.Surface;

            SKCanvas canvas = surface.Canvas;



            // Clear the canvas

            canvas.Clear(backgroundColor);



            // Use the map renderer to render the map

            mapRenderer.RenderMap(

                canvas,

                backgroundColor,

                layerManager,

                showWorldCoordinates,

                interactionManager.LastMouseWorldPosition,

                interactionManager.ZoomRectangle,

                interactionManager.IsInteracting,

                interactionManager.Behavior);



            // If in circle drawing mode, draw the preview

            if (interactionManager.Behavior == MapBehavior.CircleDrawing && interactionManager.IsDrawingCircle)

            {

                interactionManager.DrawCirclePreview(canvas);

            }

            // If in circle editing mode, draw the editing UI

            else if (interactionManager.Behavior == MapBehavior.CircleEditing)

            {

                interactionManager.DrawCircleEditingUI(canvas);

            }

            // If in polygon drawing mode, draw the preview

            else if (interactionManager.Behavior == MapBehavior.PolygonDrawing && interactionManager.IsDrawingPolygon)

            {

                interactionManager.DrawPolygonPreview(canvas);

            }

            // If in polygon editing mode, draw the editing UI

            else if (interactionManager.Behavior == MapBehavior.PolygonEditing)

            {

                interactionManager.DrawPolygonEditingUI(canvas);

            }

        }



        /// <summary>

        /// Gets the coordinates at the specified screen position.

        /// </summary>

        public PointD GetWorldCoordinatesAt(int screenX, int screenY)

        {

            return transformer.ScreenToWorld(screenX, screenY);

        }



        /// <summary>

        /// Event raised when the world coordinate bounds change.

        /// </summary>

        public event EventHandler WorldBoundsChanged;



        /// <summary>

        /// Raises the WorldBoundsChanged event.

        /// </summary>

        public virtual void OnWorldBoundsChanged()

        {

            WorldBoundsChanged?.Invoke(this, EventArgs.Empty);

        }



        //protected override void OnLoad(EventArgs e)

        //{

        //    base.OnLoad(e);

        //    // Initialize or setup additional resources if needed

        //}



        /// <summary>

        /// Clean up any resources being used.

        /// </summary>

        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>

        protected override void Dispose(bool disposing)

        {

            if (disposing)

            {

                // Unwire the events before disposing

                if (interactionManager != null)

                {

                    interactionManager.CircleDrawn -= InteractionManager_CircleDrawn;

                    interactionManager.CircleModified -= InteractionManager_CircleModified;

                    interactionManager.PolygonDrawn -= InteractionManager_PolygonDrawn;

                    interactionManager.PolygonModified -= InteractionManager_PolygonModified;

                    interactionManager.Dispose();

                }

            }



            base.Dispose(disposing);

        }

    }



    /// <summary>

    /// Event arguments for mouse movement with both screen and world coordinates

    /// </summary>

    public class MapMouseEventArgs : EventArgs

    {

        // Pixel coordinates

        public int PixelX { get; private set; }

        public int PixelY { get; private set; }



        // World coordinates

        public double WorldX { get; private set; }

        public double WorldY { get; private set; }



        // Original mouse event args

        public System.Windows.Forms.MouseEventArgs OriginalEventArgs { get; private set; }



        public MapMouseEventArgs(int pixelX, int pixelY, double worldX, double worldY, System.Windows.Forms.MouseEventArgs originalArgs)

        {

            PixelX = pixelX;

            PixelY = pixelY;

            WorldX = worldX;

            WorldY = worldY;

            OriginalEventArgs = originalArgs;

        }

    }

}