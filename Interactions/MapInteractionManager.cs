using FCoreMap.Controls;

using FCoreMap.Geometries;

using SkiaSharp;



namespace FCoreMap.Interactions

{

    /// <summary>

    /// Defines the interactive behavior modes for the map control.

    /// </summary>

    public enum MapBehavior

    {

        /// <summary>

        /// Pan mode allows dragging the map to navigate.

        /// </summary>

        Pan,



        /// <summary>

        /// Zoom in mode allows drawing a rectangle to zoom in to that area.

        /// </summary>

        ZoomIn,



        /// <summary>

        /// Zoom out mode allows drawing a rectangle to zoom out.

        /// </summary>

        ZoomOut,



        /// <summary>

        /// Circle drawing mode allows adding 3 points to define a circle.

        /// </summary>

        CircleDrawing,



        /// <summary>

        /// Circle editing mode allows selecting and dragging user-defined points of existing circles.

        /// </summary>

        CircleEditing,



        /// <summary>

        /// Polygon drawing mode allows adding vertices to define a polygon.

        /// </summary>

        PolygonDrawing,



        /// <summary>

        /// Polygon editing mode allows selecting and modifying existing polygons.

        /// </summary>

        PolygonEditing

    }



    /// <summary>

    /// Manages user interaction with the map control, including panning, zooming, and coordinate tracking.

    /// </summary>





namespace FCoreMap.Controls

    {

        /// <summary>

        /// Manages user interaction with the map control, including panning, zooming,

        /// circle and polygon drawing/editing, using SkiaSharp for rendering.

        /// </summary>

        public class MapInteractionManagerSk : IDisposable

        {

            private readonly FCoreMapControlSk mapControl;

            private readonly CoordinateTransformerSk transformer;



            // Interaction state

            private bool isInteracting = false;

            private Point interactionStartPoint;

            private double panStartMinX, panStartMinY, panStartMaxX, panStartMaxY;

            private SKRect zoomRect = SKRect.Empty;



            // Settings

            private MapBehavior behavior = MapBehavior.Pan;

            private double wheelZoomFactor = 1.2;

            private bool zoomToMousePosition = true;



            // Last known mouse position in world coordinates

            private PointD lastMouseWorldPosition;



            // Circle drawing properties

            private readonly List<PointD> circlePoints = new List<PointD>(3);

            private bool isDrawingCircle = false;

            private CircleD previewCircle = null;

            private Layer circleLayer = null;



            // Circle editing properties

            private CircleD selectedCircle = null;

            private int selectedPointIndex = -1;

            private bool isDraggingPoint = false;

            private PointD originalPointPosition = null;



            // Polygon drawing/editing properties

            private readonly List<PointD> polygonVertices = new List<PointD>();

            private bool isDrawingPolygon = false;

            private PolygonD previewPolygon = null;

            private Layer polygonLayer = null;



            // Polygon editing properties

            private PolygonD selectedPolygon = null;

            private int selectedVertexIndex = -1;

            private bool isDraggingVertex = false;

            private PointD originalVertexPosition = null;

            private bool isMovingPolygon = false;

            private readonly Dictionary<int, PointD> originalVertexPositions = new Dictionary<int, PointD>();

            private int insertVertexStartIndex = -1;

            private int insertVertexEndIndex = -1;

            private bool isHoveringEdge = false;



            // Events

            public event EventHandler<CircleEventArgs> CircleDrawn;

            public event EventHandler<CircleModifiedEventArgs> CircleModified;

            public event EventHandler<PolygonEventArgs> PolygonDrawn;

            public event EventHandler<PolygonModifiedEventArgs> PolygonModified;



            // Public properties

            public bool IsDrawingPolygon => isDrawingPolygon;

            public int PolygonVertexCount => polygonVertices.Count;

            public PolygonD PreviewPolygon => previewPolygon;

            public IReadOnlyList<PointD> PolygonVertices => polygonVertices.AsReadOnly();

            public Layer PolygonDrawingLayer => polygonLayer;

            public PolygonD SelectedPolygon => selectedPolygon;

            public int SelectedVertexIndex => selectedVertexIndex;

            public bool IsDraggingVertex => isDraggingVertex;

            public MapBehavior Behavior

            {

                get => behavior;

                set

                {

                    if (behavior == MapBehavior.CircleDrawing && value != MapBehavior.CircleDrawing)

                        CancelCircleDrawing();



                    behavior = value;

                    UpdateMapCursor();

                }

            }

            public double WheelZoomFactor

            {

                get => wheelZoomFactor;

                set => wheelZoomFactor = Math.Max(1.01, Math.Min(2.0, value));

            }

            public bool ZoomToMousePosition { get => zoomToMousePosition; set => zoomToMousePosition = value; }

            public PointD LastMouseWorldPosition => lastMouseWorldPosition;

            public SKRect ZoomRectangle => zoomRect;

            public bool IsInteracting => isInteracting;

            public bool IsDrawingCircle => isDrawingCircle;

            public int CirclePointCount => circlePoints.Count;

            public CircleD PreviewCircle => previewCircle;

            public IReadOnlyList<PointD> CirclePoints => circlePoints.AsReadOnly();

            public Layer CircleDrawingLayer => circleLayer;

            public CircleD SelectedCircle => selectedCircle;

            public int SelectedPointIndex => selectedPointIndex;

            public bool IsDraggingPoint => isDraggingPoint;



            /// <summary>

            /// Initializes a new instance and hooks up event handlers.

            /// </summary>

            public MapInteractionManagerSk(FCoreMapControlSk mapControl, CoordinateTransformerSk transformer)

            {

                this.mapControl = mapControl;

                this.transformer = transformer;



                mapControl.MouseMove += MapControl_MouseMove;

                mapControl.MouseDown += MapControl_MouseDown;

                mapControl.MouseUp += MapControl_MouseUp;

                mapControl.MouseLeave += MapControl_MouseLeave;

                mapControl.MouseWheel += MapControl_MouseWheel;

                mapControl.Click += MapControl_Click;

              //  mapControl.KeyDown += MapControl_KeyDown;



                UpdateMapCursor();

            }



            /// <summary>

            /// Cleans up resources and detaches event handlers.

            /// </summary>

            public void Dispose()

            {

                mapControl.MouseMove -= MapControl_MouseMove;

                mapControl.MouseDown -= MapControl_MouseDown;

                mapControl.MouseUp -= MapControl_MouseUp;

                mapControl.MouseLeave -= MapControl_MouseLeave;

                mapControl.MouseWheel -= MapControl_MouseWheel;

                mapControl.Click -= MapControl_Click;

               // mapControl.KeyDown -= MapControl_KeyDown;

            }



            private void MapControl_MouseLeave(object sender, EventArgs e)

            {

                if (isDraggingPoint || isDraggingVertex)

                {

                    isDraggingPoint = false;

                    isDraggingVertex = false;

                    isInteracting = false;

                    mapControl.Invalidate();

                }

                if (isInteracting)

                {

                    isInteracting = false;

                    zoomRect = SKRect.Empty;

                    mapControl.Invalidate();

                    if (behavior == MapBehavior.Pan)

                        mapControl.OnWorldBoundsChanged();

                }

            }



            private void MapControl_MouseWheel(object sender, MouseEventArgs e)

            {

                bool zoomingIn = e.Delta < 0;

                transformer.GetWorldExtents(out double minX, out double minY, out double maxX, out double maxY);

                double worldWidth = maxX - minX;

                double worldHeight = maxY - minY;

                double centerX, centerY;



                if (zoomToMousePosition)

                {

                    var mouseWorld = transformer.ScreenToWorld(e.X, e.Y);

                    centerX = mouseWorld.X;

                    centerY = mouseWorld.Y;

                }

                else

                {

                    centerX = (minX + maxX) / 2;

                    centerY = (minY + maxY) / 2;

                }



                double factor = zoomingIn ? wheelZoomFactor : 1.0 / wheelZoomFactor;

                double newW = worldWidth / factor;

                double newH = worldHeight / factor;

                double newMinX, newMinY, newMaxX, newMaxY;



                if (zoomToMousePosition)

                {

                    double relX = (centerX - minX) / worldWidth;

                    double relY = (centerY - minY) / worldHeight;

                    newMinX = centerX - newW * relX;

                    newMinY = centerY - newH * relY;

                    newMaxX = newMinX + newW;

                    newMaxY = newMinY + newH;

                }

                else

                {

                    newMinX = centerX - newW / 2;

                    newMinY = centerY - newH / 2;

                    newMaxX = centerX + newW / 2;

                    newMaxY = centerY + newH / 2;

                }



                mapControl.ZoomToWindow(newMinX, newMinY, newMaxX, newMaxY);

            }



            private void HandlePanMove(MouseEventArgs e)

            {

                int dx = interactionStartPoint.X - e.X;

                int dy = interactionStartPoint.Y - e.Y;

                if (dx == 0 && dy == 0) return;

                double scale = transformer.GetWorldToScreenScale();

                double wx = panStartMinX + dx / scale;

                double wy = panStartMinY - dy / scale;

                transformer.SetWorldExtents(wx, wy, panStartMaxX + dx / scale, panStartMaxY - dy / scale);

                mapControl.Invalidate();

            }



            private void HandleZoomMove(MouseEventArgs e)

            {

                float x = Math.Min(interactionStartPoint.X, e.X);

                float y = Math.Min(interactionStartPoint.Y, e.Y);

                float w = Math.Abs(e.X - interactionStartPoint.X);

                float h = Math.Abs(e.Y - interactionStartPoint.Y);

                zoomRect = new SKRect(x, y, x + w, y + h);

                mapControl.Invalidate();

            }



            private void ApplyZoom(SKRect rect, bool zoomIn)

            {

                if (rect.IsEmpty || rect.Width <= 0 || rect.Height <= 0) return;

                var tl = transformer.ScreenToWorld(rect.Left, rect.Top);

                var br = transformer.ScreenToWorld(rect.Right, rect.Bottom);

                double minX = Math.Min(tl.X, br.X), minY = Math.Min(tl.Y, br.Y);

                double maxX = Math.Max(tl.X, br.X), maxY = Math.Max(tl.Y, br.Y);



                if (zoomIn)

                {

                    mapControl.ZoomToWindow(minX, minY, maxX, maxY);

                }

                else

                {

                    transformer.GetWorldExtents(out double curMinX, out double curMinY, out double curMaxX, out double curMaxY);

                    double w = curMaxX - curMinX;

                    double h = curMaxY - curMinY;

                    double rw = maxX - minX;

                    double rh = maxY - minY;

                    if (rw <= 0 || rh <= 0) return;

                    double sx = w / rw;

                    double sy = h / rh;

                    double s = Math.Max(sx, sy);

                    double cx = (minX + maxX) / 2;

                    double cy = (minY + maxY) / 2;

                    double nw = w * s;

                    double nh = h * s;

                    double nmX = cx - nw / 2;

                    double nmY = cy - nh / 2;

                    mapControl.ZoomToWindow(nmX, nmY, cx + nw / 2, cy + nh / 2);

                }

            }



            private void UpdateMapCursor()

            {

                switch (behavior)

                {

                    case MapBehavior.Pan:

                        mapControl.Cursor = Cursors.Hand;

                        break;

                    case MapBehavior.ZoomIn:

                    case MapBehavior.ZoomOut:

                        mapControl.Cursor = Cursors.Cross;

                        break;

                    case MapBehavior.CircleDrawing:

                        mapControl.Cursor = Cursors.Cross;

                        break;

                    case MapBehavior.CircleEditing:

                        mapControl.Cursor = (selectedCircle != null && selectedPointIndex >= 0)

                            ? Cursors.SizeAll

                            : Cursors.Default;

                        break;

                    case MapBehavior.PolygonDrawing:

                        mapControl.Cursor = Cursors.Cross;

                        break;

                    case MapBehavior.PolygonEditing:

                        if (isDraggingVertex || isMovingPolygon) mapControl.Cursor = Cursors.SizeAll;

                        else if (selectedPolygon != null && selectedVertexIndex >= 0) mapControl.Cursor = Cursors.Hand;

                        else mapControl.Cursor = Cursors.Default;

                        break;

                    default:

                        mapControl.Cursor = Cursors.Default;

                        break;

                }

            }



            private void MapControl_MouseDown(object sender, MouseEventArgs e)

            {

                // Circle editing

                if (behavior == MapBehavior.CircleEditing && e.Button == MouseButtons.Left && circleLayer != null)

                {

                    var wp = transformer.ScreenToWorld(e.X, e.Y);

                    double thr = 5.0 / transformer.GetWorldToScreenScale();

                    if (selectedCircle != null && selectedPointIndex >= 0)

                    {

                        for (int i = 0; i < selectedCircle.UserDefinedPointCount; i++)

                        {

                            if (i == selectedPointIndex) continue;

                            var pt = selectedCircle.GetUserDefinedPointAt(i);

                            if (wp.DistanceTo(pt) < thr)

                            {

                                selectedPointIndex = i;

                                isDraggingPoint = true;

                                originalPointPosition = pt;

                                isInteracting = true;

                                interactionStartPoint = e.Location;

                                mapControl.Invalidate();

                                return;

                            }

                        }

                        var selPt = selectedCircle.GetUserDefinedPointAt(selectedPointIndex);

                        if (wp.DistanceTo(selPt) < thr)

                        {

                            isDraggingPoint = true;

                            originalPointPosition = selPt;

                            isInteracting = true;

                            interactionStartPoint = e.Location;

                            mapControl.Invalidate();

                            return;

                        }

                    }

                    FindAndSelectPoint(wp);

                    mapControl.Invalidate();

                    return;

                }

                // Polygon editing

                if (behavior == MapBehavior.PolygonEditing && e.Button == MouseButtons.Left && polygonLayer != null)

                {

                    var wp = transformer.ScreenToWorld(e.X, e.Y);

                    double thr = 5.0 / transformer.GetWorldToScreenScale();

                    if (selectedPolygon != null && selectedVertexIndex >= 0)

                    {

                        for (int i = 0; i < selectedPolygon.VertexCount; i++)

                        {

                            if (i == selectedVertexIndex) continue;

                            var v = selectedPolygon.GetVertexAt(i);

                            if (wp.DistanceTo(v) < thr)

                            {

                                selectedVertexIndex = i;

                                isDraggingVertex = true;

                                originalVertexPosition = v;

                                isInteracting = true;

                                interactionStartPoint = e.Location;

                                mapControl.Invalidate();

                                return;

                            }

                        }

                        var selV = selectedPolygon.GetVertexAt(selectedVertexIndex);

                        if (wp.DistanceTo(selV) < thr)

                        {

                            isDraggingVertex = true;

                            originalVertexPosition = selV;

                            isInteracting = true;

                            interactionStartPoint = e.Location;

                            mapControl.Invalidate();

                            return;

                        }

                        if (selectedPolygon.ContainsPoint(wp))

                        {

                            isMovingPolygon = true;

                            isInteracting = true;

                            interactionStartPoint = e.Location;

                            originalVertexPositions.Clear();

                            for (int i = 0; i < selectedPolygon.VertexCount; i++)

                                originalVertexPositions[i] = selectedPolygon.GetVertexAt(i);

                            mapControl.Invalidate();

                            return;

                        }

                    }

                    FindAndSelectVertex(wp);

                    mapControl.Invalidate();

                    return;

                }

                // Pan / Zoom / Start Draw

                if (e.Button == MouseButtons.Left && !isDrawingCircle && !isDrawingPolygon)

                {

                    isInteracting = true;

                    interactionStartPoint = e.Location;

                    if (behavior == MapBehavior.Pan)

                        transformer.GetWorldExtents(out panStartMinX, out panStartMinY, out panStartMaxX, out panStartMaxY);

                    else if (behavior == MapBehavior.ZoomIn || behavior == MapBehavior.ZoomOut)

                        zoomRect = new SKRect(e.X, e.Y, e.X, e.Y);

                }

            }



            private void MapControl_MouseUp(object sender, MouseEventArgs e)

            {

                // Circle editing end

                if (behavior == MapBehavior.CircleEditing && isDraggingPoint && selectedCircle != null)

                {

                    isDraggingPoint = false;

                    isInteracting = false;

                    OnCircleModified(selectedCircle, circleLayer, selectedPointIndex, originalPointPosition);

                    mapControl.Invalidate();

                    return;

                }

                // Polygon editing end

                if (behavior == MapBehavior.PolygonEditing && (isDraggingVertex || isMovingPolygon) && selectedPolygon != null)

                {

                    if (isDraggingVertex)

                    {

                        isDraggingVertex = false;

                        isInteracting = false;

                        OnPolygonModified(selectedPolygon, polygonLayer, PolygonModificationType.VertexMoved, selectedVertexIndex, originalVertexPosition);

                    }

                    else if (isMovingPolygon)

                    {

                        isMovingPolygon = false;

                        isInteracting = false;

                        OnPolygonModified(selectedPolygon, polygonLayer, PolygonModificationType.PolygonMoved);

                        originalVertexPositions.Clear();

                    }

                    mapControl.Invalidate();

                    return;

                }

                // Zoom completion

                if (e.Button == MouseButtons.Left && isInteracting)

                {

                    isInteracting = false;

                    if ((behavior == MapBehavior.ZoomIn || behavior == MapBehavior.ZoomOut)

                        && zoomRect.Width > 5 && zoomRect.Height > 5)

                    {

                        ApplyZoom(zoomRect, behavior == MapBehavior.ZoomIn);

                    }

                    mapControl.OnWorldBoundsChanged();

                    zoomRect = SKRect.Empty;

                    mapControl.Invalidate();

                }

            }



            private void MapControl_Click(object sender, EventArgs e)

            {

                if (!(e is MouseEventArgs me)) return;

                // Circle drawing

                if (behavior == MapBehavior.CircleDrawing && circleLayer != null && me.Button == MouseButtons.Left)

                {

                    if (!isDrawingCircle) StartCircleDrawing();

                    var wp = transformer.ScreenToWorld(me.X, me.Y);

                    circlePoints.Add(wp);

                    UpdateCirclePreview(me);

                    if (circlePoints.Count == 3) CompleteCircle();

                    mapControl.Invalidate();

                    return;

                }

                // Polygon drawing

                if (behavior == MapBehavior.PolygonDrawing && polygonLayer != null && me.Button == MouseButtons.Left)

                {

                    if (me.Clicks == 2 && polygonVertices.Count >= 2)

                    {

                        var wp = transformer.ScreenToWorld(me.X, me.Y);

                        var first = polygonVertices[0];

                        double thr = 5.0 / transformer.GetWorldToScreenScale();

                        if (wp.DistanceTo(first) > thr)

                            polygonVertices.Add(wp);

                        if (polygonVertices.Count >= 3) CompletePolygon();

                    }

                    else if (me.Clicks == 1)

                    {

                        if (!isDrawingPolygon) StartPolygonDrawing();

                        var wp = transformer.ScreenToWorld(me.X, me.Y);

                        if (polygonVertices.Count > 0)

                        {

                            var first = polygonVertices[0];

                            double thr = 5.0 / transformer.GetWorldToScreenScale();

                            if (polygonVertices.Count >= 3 && wp.DistanceTo(first) <= thr)

                            {

                                CompletePolygon();

                                return;

                            }

                        }

                        polygonVertices.Add(wp);

                        UpdatePolygonPreview();

                        mapControl.Invalidate();

                    }

                    return;

                }

                // Polygon editing click

                if (behavior == MapBehavior.PolygonEditing && polygonLayer != null)

                {

                    if (me.Button == MouseButtons.Left)

                    {

                        var wp = transformer.ScreenToWorld(me.X, me.Y);

                        if (isHoveringEdge && selectedPolygon != null && insertVertexStartIndex >= 0)

                        {

                            InsertVertexOnEdge(wp);

                            mapControl.Invalidate();

                        }

                        else

                        {

                            FindAndSelectVertex(wp);

                            mapControl.Invalidate();

                        }

                    }

                    else if (me.Button == MouseButtons.Right && selectedPolygon != null && selectedVertexIndex >= 0 && selectedPolygon.VertexCount > 3)

                    {

                        DeleteSelectedVertex();

                        mapControl.Invalidate();

                    }

                    return;

                }

                // Circle editing click

                if (behavior == MapBehavior.CircleEditing && circleLayer != null && me.Button == MouseButtons.Left)

                {

                    var wp = transformer.ScreenToWorld(me.X, me.Y);

                    FindAndSelectPoint(wp);

                    mapControl.Invalidate();

                }

            }



            private void MapControl_MouseMove(object sender, MouseEventArgs e)

            {

                lastMouseWorldPosition = transformer.ScreenToWorld(e.X, e.Y);

                if (behavior == MapBehavior.CircleDrawing && isDrawingCircle && circlePoints.Count > 0)

                {

                    UpdateCirclePreview(e);

                    mapControl.Invalidate();

                }

                else if (behavior == MapBehavior.CircleEditing && isDraggingPoint && selectedCircle != null && selectedPointIndex >= 0)

                {

                    var pts = new List<PointD>();

                    for (int i = 0; i < selectedCircle.UserDefinedPointCount; i++)

                    {

                        pts.Add(i == selectedPointIndex ? lastMouseWorldPosition : selectedCircle.GetUserDefinedPointAt(i));

                    }

                    try

                    {

                        if (pts.Count >= 3)

                        {

                            var c = new CircleD(pts[0], pts[1], pts[2]);

                            selectedCircle.Center = c.Center;

                            selectedCircle.Radius = c.Radius;

                            selectedCircle.UpdateBoundingBox();

                            selectedCircle.ClearUserDefinedPoints();

                            foreach (var p in pts) selectedCircle.AddUserDefinedPoint(p);

                        }

                    }

                    catch { }

                    mapControl.Invalidate();

                }

                else if (behavior == MapBehavior.PolygonDrawing && isDrawingPolygon && polygonVertices.Count > 0)

                {

                    UpdatePolygonPreview();

                    mapControl.Invalidate();

                }

                else if (behavior == MapBehavior.PolygonEditing && isDraggingVertex && selectedPolygon != null && selectedVertexIndex >= 0)

                {

                    var vs = new List<PointD>();

                    for (int i = 0; i < selectedPolygon.VertexCount; i++)

                        vs.Add(i == selectedVertexIndex ? lastMouseWorldPosition : selectedPolygon.GetVertexAt(i));

                    var up = new PolygonD(vs) { Id = selectedPolygon.Id };

                    polygonLayer.RemovePolygonAt(selectedPolygon.Id);

                    polygonLayer.AddPolygon(up);

                    selectedPolygon = up;

                    mapControl.Invalidate();

                }

                else if (behavior == MapBehavior.PolygonEditing && isMovingPolygon && selectedPolygon != null)

                {

                    var oldP = transformer.ScreenToWorld(interactionStartPoint.X, interactionStartPoint.Y);

                    var deltaX = lastMouseWorldPosition.X - oldP.X;

                    var deltaY = lastMouseWorldPosition.Y - oldP.Y;

                    var vs = new List<PointD>();

                    for (int i = 0; i < selectedPolygon.VertexCount; i++)

                    {

                        var orig = originalVertexPositions[i];

                        vs.Add(new PointD(orig.X + deltaX, orig.Y + deltaY));

                    }

                    var mp = new PolygonD(vs) { Id = selectedPolygon.Id };

                    polygonLayer.RemovePolygonAt(selectedPolygon.Id);

                    polygonLayer.AddPolygon(mp);

                    selectedPolygon = mp;

                    mapControl.Invalidate();

                }

                else if (isInteracting && e.Button == MouseButtons.Left)

                {

                    switch (behavior)

                    {

                        case MapBehavior.Pan: HandlePanMove(e); break;

                        case MapBehavior.ZoomIn:

                        case MapBehavior.ZoomOut: HandleZoomMove(e); break;

                    }

                }

                else if (behavior == MapBehavior.PolygonEditing)

                {

                    UpdatePolygonHoverState(e);

                }

                else if (behavior == MapBehavior.CircleEditing)

                {

                    UpdateHoverPoint(e);

                }

                if (mapControl.ShowWorldCoordinates && !isInteracting)

                    mapControl.Invalidate();

            }



            private void UpdateHoverPoint(MouseEventArgs e)

            {

                if (isDraggingPoint || circleLayer == null) return;

                var wp = transformer.ScreenToWorld(e.X, e.Y);

                double thr = 5.0 / transformer.GetWorldToScreenScale();

                bool hover = false;

                foreach (var c in circleLayer.GetCircles() ?? new List<CircleD>())

                {

                    for (int i = 0; i < c.UserDefinedPointCount; i++)

                    {

                        if (wp.DistanceTo(c.GetUserDefinedPointAt(i)) < thr) { hover = true; break; }

                    }

                    if (hover) break;

                }

                mapControl.Cursor = hover ? Cursors.Hand : Cursors.Default;

            }



            private void UpdatePolygonHoverState(MouseEventArgs e)

            {

                if (isDraggingVertex || isMovingPolygon || polygonLayer == null) return;

                var wp = transformer.ScreenToWorld(e.X, e.Y);

                double vthr = 5.0 / transformer.GetWorldToScreenScale(), ethr = 3.0 / transformer.GetWorldToScreenScale();

                bool hv = false, he = false;

                insertVertexStartIndex = insertVertexEndIndex = -1;

                foreach (var poly in polygonLayer.GetPolygons() ?? new List<PolygonD>())

                {

                    for (int i = 0; i < poly.VertexCount; i++)

                    {

                        if (wp.DistanceTo(poly.GetVertexAt(i)) < vthr) { hv = true; break; }

                    }

                    if (hv) { selectedPolygon = poly; break; }

                    var vs = poly.Vertices;

                    for (int i = 0; i < vs.Count; i++)

                    {

                        var v1 = vs[i];

                        var v2 = vs[(i + 1) % vs.Count];

                        double d = DistanceToLineSegment(wp, v1, v2);

                        if (d < ethr)

                        {

                            he = true;

                            insertVertexStartIndex = i;

                            insertVertexEndIndex = (i + 1) % vs.Count;

                            selectedPolygon = poly;

                            break;

                        }

                    }

                    if (he) break;

                }

                if (hv) mapControl.Cursor = Cursors.Hand;

                else if (he) { mapControl.Cursor = Cursors.Cross; isHoveringEdge = true; }

                else { mapControl.Cursor = Cursors.Default; isHoveringEdge = false; }

            }



            private double DistanceToLineSegment(PointD p, PointD v1, PointD v2)

            {

                double l2 = Math.Pow(v2.X - v1.X, 2) + Math.Pow(v2.Y - v1.Y, 2);

                if (l2 == 0) return p.DistanceTo(v1);

                double t = ((p.X - v1.X) * (v2.X - v1.X) + (p.Y - v1.Y) * (v2.Y - v1.Y)) / l2;

                t = Math.Max(0, Math.Min(1, t));

                double cx = v1.X + t * (v2.X - v1.X), cy = v1.Y + t * (v2.Y - v1.Y);

                return Math.Sqrt(Math.Pow(p.X - cx, 2) + Math.Pow(p.Y - cy, 2));

            }



            private void FindAndSelectVertex(PointD wp)

            {

                selectedPolygon = null; selectedVertexIndex = -1;

                double thr = 5.0 / transformer.GetWorldToScreenScale();

                double closest = double.MaxValue;

                foreach (var poly in polygonLayer.GetPolygons() ?? new List<PolygonD>())

                {

                    for (int i = 0; i < poly.VertexCount; i++)

                    {

                        double d = wp.DistanceTo(poly.GetVertexAt(i));

                        if (d < thr && d < closest) { closest = d; selectedPolygon = poly; selectedVertexIndex = i; }

                    }

                }

                if (selectedPolygon == null)

                {

                    foreach (var poly in polygonLayer.GetPolygons() ?? new List<PolygonD>())

                    {

                        if (poly.ContainsPoint(wp)) { selectedPolygon = poly; break; }

                    }

                }

                UpdateMapCursor();

            }



            private bool InsertVertexOnEdge(PointD wp)

            {

                if (selectedPolygon == null || insertVertexStartIndex < 0) return false;

                var v1 = selectedPolygon.GetVertexAt(insertVertexStartIndex);

                var v2 = selectedPolygon.GetVertexAt(insertVertexEndIndex);

                double l2 = Math.Pow(v2.X - v1.X, 2) + Math.Pow(v2.Y - v1.Y, 2);

                if (l2 < 1e-5) return false;

                double t = ((wp.X - v1.X) * (v2.X - v1.X) + (wp.Y - v1.Y) * (v2.Y - v1.Y)) / l2;

                t = Math.Max(0, Math.Min(1, t));

                var nv = new PointD(v1.X + t * (v2.X - v1.X), v1.Y + t * (v2.Y - v1.Y));

                var vs = new List<PointD>();

                for (int i = 0; i <= insertVertexStartIndex; i++) vs.Add(selectedPolygon.GetVertexAt(i));

                vs.Add(nv);

                for (int i = insertVertexEndIndex; i < selectedPolygon.VertexCount; i++) vs.Add(selectedPolygon.GetVertexAt(i));

                var up = new PolygonD(vs) { Id = selectedPolygon.Id };

                polygonLayer.RemovePolygonAt(selectedPolygon.Id);

                polygonLayer.AddPolygon(up);

                selectedPolygon = up; selectedVertexIndex = insertVertexStartIndex + 1;

                OnPolygonModified(selectedPolygon, polygonLayer, PolygonModificationType.VertexAdded, selectedVertexIndex, null);

                return true;

            }



            private bool DeleteSelectedVertex()

            {

                if (selectedPolygon == null || selectedVertexIndex < 0 || selectedPolygon.VertexCount <= 3) return false;

                var orig = selectedPolygon.GetVertexAt(selectedVertexIndex);

                var vs = new List<PointD>();

                for (int i = 0; i < selectedPolygon.VertexCount; i++) if (i != selectedVertexIndex) vs.Add(selectedPolygon.GetVertexAt(i));

                var up = new PolygonD(vs) { Id = selectedPolygon.Id };

                polygonLayer.RemovePolygonAt(selectedPolygon.Id);

                polygonLayer.AddPolygon(up);

                selectedPolygon = up; selectedVertexIndex = -1;

                OnPolygonModified(selectedPolygon, polygonLayer, PolygonModificationType.VertexDeleted, -1, orig);

                return true;

            }



            public void DrawCirclePreview(SKCanvas canvas)

            {

                if (!isDrawingCircle) return;

                canvas.Save(); transformer.ResetTransformation(canvas);

                using (var ptPaint = new SKPaint { Color = SKColors.Red, Style = SKPaintStyle.Fill, IsAntialias = true })

                using (var ptOut = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true })

                {

                    float size = 10;

                    for (int i = 0; i < circlePoints.Count; i++)

                    {

                        var sp = transformer.WorldToScreen((float)circlePoints[i].X, (float)circlePoints[i].Y);

                        canvas.DrawCircle(sp.X, sp.Y, size / 2, ptPaint);

                        canvas.DrawCircle(sp.X, sp.Y, size / 2, ptOut);

                        using (var tx = new SKPaint { Color = SKColors.White, TextSize = 12, IsAntialias = true, TextAlign = SKTextAlign.Center })

                        {

                            canvas.DrawText((i + 1).ToString(), sp.X, sp.Y + 4, tx);

                        }

                    }

                }

                if (previewCircle != null)

                {

                    using (var fill = new SKPaint { Color = new SKColor((byte)(circleLayer?.Style?.Color.R ?? 0), (byte)(circleLayer?.Style?.Color.G ?? 0), (byte)(circleLayer?.Style?.Color.B ?? 255), 100), Style = SKPaintStyle.Fill, IsAntialias = true })

                    using (var outl = new SKPaint { Color = new SKColor((byte)(circleLayer?.Style?.OutlineColor.R ?? 0), (byte)(circleLayer?.Style?.OutlineColor.G ?? 0), (byte)(circleLayer?.Style?.OutlineColor.B ?? 0)), Style = SKPaintStyle.Stroke, StrokeWidth = circleLayer?.Style?.OutlineWidth ?? 1.5f, PathEffect = SKPathEffect.CreateDash(new float[] { 4, 4 }, 0), IsAntialias = true })

                    {

                        var sc = transformer.WorldToScreen((float)previewCircle.Center.X, (float)previewCircle.Center.Y);

                        float sr = (float)(previewCircle.Radius * transformer.GetWorldToScreenScale());

                        canvas.DrawCircle(sc.X, sc.Y, sr, fill);

                        canvas.DrawCircle(sc.X, sc.Y, sr, outl);

                    }

                }

                canvas.Restore();

            }



            public void DrawCircleEditingUI(SKCanvas canvas)

            {

                if (behavior != MapBehavior.CircleEditing || circleLayer == null) return;

                canvas.Save(); transformer.ResetTransformation(canvas);

                foreach (var circle in circleLayer.GetCircles() ?? new List<CircleD>())

                {

                    bool sel = circle == selectedCircle;

                    if (sel)

                    {

                        var sc = transformer.WorldToScreen((float)circle.Center.X, (float)circle.Center.Y);

                        float sr = (float)(circle.Radius * transformer.GetWorldToScreenScale());

                        using (var hl = new SKPaint { Color = SKColors.Yellow, Style = SKPaintStyle.Stroke, StrokeWidth = 3, PathEffect = SKPathEffect.CreateDash(new float[] { 5, 5 }, 0), IsAntialias = true })

                            canvas.DrawCircle(sc.X, sc.Y, sr, hl);

                    }

                    for (int i = 0; i < circle.UserDefinedPointCount; i++)

                    {

                        var pt = circle.GetUserDefinedPointAt(i);

                        var sp = transformer.WorldToScreen((float)pt.X, (float)pt.Y);

                        float psize = sel ? (i == selectedPointIndex ? 12 : 10) : 8;

                        SKColor fill = sel ? (i == selectedPointIndex ? SKColors.Yellow : SKColors.Orange) : SKColors.Red;

                        using (var ptp = new SKPaint { Color = fill, Style = SKPaintStyle.Fill, IsAntialias = true })

                        using (var po = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true })

                        {

                            canvas.DrawCircle(sp.X, sp.Y, psize / 2, ptp);

                            canvas.DrawCircle(sp.X, sp.Y, psize / 2, po);

                            using (var tx = new SKPaint { Color = SKColors.White, TextSize = 12, IsAntialias = true, TextAlign = SKTextAlign.Center })

                                canvas.DrawText((i + 1).ToString(), sp.X, sp.Y + 4, tx);

                        }

                    }

                }

                canvas.Restore();

            }



            public void DrawPolygonPreview(SKCanvas canvas)

            {

                if (!isDrawingPolygon) return;

                canvas.Save(); transformer.ResetTransformation(canvas);

                using (var vp = new SKPaint { Color = SKColors.Red, Style = SKPaintStyle.Fill, IsAntialias = true })

                using (var vo = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true })

                using (var lp = new SKPaint { Color = SKColors.Blue, Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true })

                {

                    float vsz = 8;

                    for (int i = 0; i < polygonVertices.Count; i++)

                    {

                        var sp = transformer.WorldToScreen((float)polygonVertices[i].X, (float)polygonVertices[i].Y);

                        canvas.DrawCircle(sp.X, sp.Y, vsz / 2, vp);

                        canvas.DrawCircle(sp.X, sp.Y, vsz / 2, vo);

                        if (i < polygonVertices.Count - 1)

                        {

                            var np = transformer.WorldToScreen((float)polygonVertices[i + 1].X, (float)polygonVertices[i + 1].Y);

                            canvas.DrawLine(sp.X, sp.Y, np.X, np.Y, lp);

                        }

                    }

                    if (polygonVertices.Count >= 3)

                    {

                        var f = transformer.WorldToScreen((float)polygonVertices[0].X, (float)polygonVertices[0].Y);

                        var l = transformer.WorldToScreen((float)polygonVertices[polygonVertices.Count - 1].X, (float)polygonVertices[polygonVertices.Count - 1].Y);

                        using (var cp = new SKPaint { Color = SKColors.Blue, Style = SKPaintStyle.Stroke, StrokeWidth = 2, PathEffect = SKPathEffect.CreateDash(new float[] { 4, 4 }, 0), IsAntialias = true })

                            canvas.DrawLine(l.X, l.Y, f.X, f.Y, cp);

                    }

                }

                if (previewPolygon != null)

                {

                    using (var fill = new SKPaint { Color = new SKColor((byte)(polygonLayer?.Style?.Color.R ?? 0), (byte)(polygonLayer?.Style?.Color.G ?? 255), (byte)(polygonLayer?.Style?.Color.B ?? 0), 100), Style = SKPaintStyle.Fill, IsAntialias = true })

                    using (var outl = new SKPaint { Color = new SKColor((byte)(polygonLayer?.Style?.OutlineColor.R ?? 0), (byte)(polygonLayer?.Style?.OutlineColor.G ?? 0), (byte)(polygonLayer?.Style?.OutlineColor.B)), Style = SKPaintStyle.Stroke, StrokeWidth = polygonLayer?.Style?.OutlineWidth ?? 1.5f, PathEffect = SKPathEffect.CreateDash(new float[] { 4, 4 }, 0), IsAntialias = true })

                    {

                        using (var path = new SKPath())

                        {

                            var v0 = previewPolygon.GetVertexAt(0);

                            var s0 = transformer.WorldToScreen((float)v0.X, (float)v0.Y);

                            path.MoveTo(s0);

                            for (int i = 1; i < previewPolygon.VertexCount; i++)

                            {

                                var v = previewPolygon.GetVertexAt(i);

                                var sp = transformer.WorldToScreen((float)v.X, (float)v.Y);

                                path.LineTo(sp);

                            }

                            path.Close();

                            canvas.DrawPath(path, fill);

                            canvas.DrawPath(path, outl);

                        }

                    }

                }

                canvas.Restore();

            }



            public void DrawPolygonEditingUI(SKCanvas canvas)

            {

                if (behavior != MapBehavior.PolygonEditing || polygonLayer == null) return;

                canvas.Save(); transformer.ResetTransformation(canvas);

                if (isHoveringEdge && selectedPolygon != null && insertVertexStartIndex >= 0)

                {

                    var v1 = selectedPolygon.GetVertexAt(insertVertexStartIndex);

                    var v2 = selectedPolygon.GetVertexAt(insertVertexEndIndex);

                    var s1 = transformer.WorldToScreen((float)v1.X, (float)v1.Y);

                    var s2 = transformer.WorldToScreen((float)v2.X, (float)v2.Y);

                    using (var hl = new SKPaint { Color = SKColors.Red, StrokeWidth = 2, Style = SKPaintStyle.Stroke, IsAntialias = true })

                        canvas.DrawLine(s1.X, s1.Y, s2.X, s2.Y, hl);

                }

                if (selectedPolygon != null)

                {

                    using (var path = new SKPath())

                    {

                        var v0 = selectedPolygon.GetVertexAt(0);

                        var s0 = transformer.WorldToScreen((float)v0.X, (float)v0.Y);

                        path.MoveTo(s0);

                        for (int i = 1; i < selectedPolygon.VertexCount; i++)

                        {

                            var v = selectedPolygon.GetVertexAt(i);

                            var sp = transformer.WorldToScreen((float)v.X, (float)v.Y);

                            path.LineTo(sp);

                        }

                        path.Close();

                        using (var hl = new SKPaint { Color = SKColors.Yellow, StrokeWidth = 2, Style = SKPaintStyle.Stroke, PathEffect = SKPathEffect.CreateDash(new float[] { 5, 5 }, 0), IsAntialias = true })

                            canvas.DrawPath(path, hl);

                    }

                    for (int i = 0; i < selectedPolygon.VertexCount; i++)

                    {

                        var v = selectedPolygon.GetVertexAt(i);

                        var sp = transformer.WorldToScreen((float)v.X, (float)v.Y);

                        float vsz = i == selectedVertexIndex ? 12 : 10;

                        SKColor fill = i == selectedVertexIndex ? SKColors.Yellow : SKColors.Orange;

                        using (var vp = new SKPaint { Color = fill, Style = SKPaintStyle.Fill, IsAntialias = true })

                        using (var vo = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true })

                        {

                            canvas.DrawCircle(sp.X, sp.Y, vsz / 2, vp);

                            canvas.DrawCircle(sp.X, sp.Y, vsz / 2, vo);

                            using (var tx = new SKPaint { Color = SKColors.White, TextSize = 12, IsAntialias = true, TextAlign = SKTextAlign.Center })

                                canvas.DrawText(i.ToString(), sp.X, sp.Y + 4, tx);

                        }

                    }

                }

                canvas.Restore();

            }



            private void UpdatePolygonPreview()

            {

                if (!isDrawingPolygon || polygonVertices.Count < 2) { previewPolygon = null; return; }

                try { previewPolygon = new PolygonD(new List<PointD>(polygonVertices)); }

                catch { }

            }



            private void UpdateCirclePreview(MouseEventArgs e)

            {

                if (!isDrawingCircle || circlePoints.Count == 0) return;

                var cp = transformer.ScreenToWorld(e.X, e.Y);

                try

                {

                    if (circlePoints.Count == 1)

                    {

                        var p1 = circlePoints[0];

                        double cx = (p1.X + cp.X) / 2, cy = (p1.Y + cp.Y) / 2;

                        double r = p1.DistanceTo(cp) / 2;

                        previewCircle = new CircleD(new PointD(cx, cy), r);

                        previewCircle.ClearUserDefinedPoints();

                        previewCircle.AddUserDefinedPoint(p1);

                        previewCircle.AddUserDefinedPoint(cp);

                    }

                    else if (circlePoints.Count == 2)

                    {

                        try { previewCircle = new CircleD(circlePoints[0], circlePoints[1], cp); }

                        catch { var p1 = circlePoints[0]; var p2 = circlePoints[1]; double cx = (p1.X + p2.X) / 2, cy = (p1.Y + p2.Y) / 2; double r = p1.DistanceTo(p2) / 2; previewCircle = new CircleD(new PointD(cx, cy), r); }

                        previewCircle.ClearUserDefinedPoints();

                        previewCircle.AddUserDefinedPoint(circlePoints[0]);

                        previewCircle.AddUserDefinedPoint(circlePoints[1]);

                        previewCircle.AddUserDefinedPoint(cp);

                    }

                }

                catch { }

            }



            private void CompleteCircle()

            {

                if (!isDrawingCircle || circleLayer == null || circlePoints.Count != 3) return;

                try

                {

                    var finalCircle = new CircleD(circlePoints[0], circlePoints[1], circlePoints[2]);

                    finalCircle.ClearUserDefinedPoints();

                    circlePoints.ForEach(pt => finalCircle.AddUserDefinedPoint(pt));

                    circleLayer.AddCircle(finalCircle);

                    OnCircleDrawn(finalCircle, circleLayer);

                    isDrawingCircle = false;

                    circlePoints.Clear();

                    previewCircle = null;

                    mapControl.Invalidate();

                }

                catch (ArgumentException) { }

            }



            protected virtual void OnCircleDrawn(CircleD circle, Layer layer)

                => CircleDrawn?.Invoke(this, new CircleEventArgs(circle, layer));



            protected virtual void OnCircleModified(CircleD circle, Layer layer, int idx = -1, PointD orig = null)

                => CircleModified?.Invoke(this, new CircleModifiedEventArgs(circle, layer, idx, orig));



            protected virtual void OnPolygonDrawn(PolygonD poly, Layer layer)

                => PolygonDrawn?.Invoke(this, new PolygonEventArgs(poly, layer));



            protected virtual void OnPolygonModified(PolygonD poly, Layer layer, PolygonModificationType type, int idx = -1, PointD orig = null)

                => PolygonModified?.Invoke(this, new PolygonModifiedEventArgs(poly, layer, type, idx, orig));



            public void StartPolygonEditing(Layer layer)

            {

                if (layer == null || layer.Type != LayerType.Polygon) return;

                polygonLayer = layer;

                selectedPolygon = null;

                selectedVertexIndex = -1;

                isDraggingVertex = false;

                isMovingPolygon = false;

                isHoveringEdge = false;

                behavior = MapBehavior.PolygonEditing;

                UpdateMapCursor();

            }



            public void StartCircleEditing(Layer layer)

            {

                if (layer == null || layer.Type != LayerType.Circle) return;

                circleLayer = layer;

                selectedCircle = null;

                selectedPointIndex = -1;

                isDraggingPoint = false;

                behavior = MapBehavior.CircleEditing;

                UpdateMapCursor();

            }



            public void SetPolygonDrawingLayer(Layer layer)

                => polygonLayer = layer != null && layer.Type == LayerType.Polygon ? layer : polygonLayer;



            public void SetCircleDrawingLayer(Layer layer)

                => circleLayer = layer != null && layer.Type == LayerType.Circle ? layer : circleLayer;



            public void StartPolygonDrawing()

            {

                if (polygonLayer == null) return;

                isDrawingPolygon = true;

                polygonVertices.Clear();

                previewPolygon = null;

            }



            public void CancelPolygonDrawing()

            {

                isDrawingPolygon = false;

                polygonVertices.Clear();

                previewPolygon = null;

                mapControl.Invalidate();

            }



            public bool CompletePolygon()

            {

                if (!isDrawingPolygon || polygonLayer == null || polygonVertices.Count < 3) return false;

                try

                {

                    var final = new PolygonD(polygonVertices);

                    polygonLayer.AddPolygon(final);

                    OnPolygonDrawn(final, polygonLayer);

                    isDrawingPolygon = false;

                    polygonVertices.Clear();

                    previewPolygon = null;

                    mapControl.Invalidate();

                    return true;

                }

                catch

                {

                    return false;

                }

            }



            public void StartCircleDrawing()

            {

                if (circleLayer == null) return;

                isDrawingCircle = true;

                circlePoints.Clear();

                previewCircle = null;

            }



            public void CancelCircleDrawing()

            {

                isDrawingCircle = false;

                circlePoints.Clear();

                previewCircle = null;

                mapControl.Invalidate();

            }



            private void FindAndSelectPoint(PointD wp)

            {

                selectedCircle = null; selectedPointIndex = -1;

                double thr = 5.0 / transformer.GetWorldToScreenScale();

                double best = double.MaxValue;

                foreach (var c in circleLayer.GetCircles() ?? new List<CircleD>())

                {

                    for (int i = 0; i < c.UserDefinedPointCount; i++)

                    {

                        var p = c.GetUserDefinedPointAt(i);

                        double d = wp.DistanceTo(p);

                        if (d < thr && d < best) { best = d; selectedCircle = c; selectedPointIndex = i; }

                    }

                }

                UpdateMapCursor();

            }

        }

    }

}