// UI/MapCanvas.cs
using System.Drawing;
using System.Drawing.Drawing2D;
using System.ComponentModel;

namespace EU4FLO;

public sealed class MapCanvas : Control
{
    private Bitmap? _image;
    private float _zoom = 1f;
    private PointF _offset = PointF.Empty;
    private bool _panning;
    private Point _panStart;
    private PointF _panStartOffset;
    private Point _mouseDownPos;
    private bool _dragged;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public MapRenderer? Renderer { get; set; }

    public event Action<int>? ProvinceClicked;

    public MapCanvas()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.Selectable, true);
        BackColor = Color.Black;
        TabStop = true;
    }

    public void SetImage(Bitmap bmp)
    {
        _image?.Dispose();
        _image = bmp;
        if (_image != null && _zoom == 1f && _offset == PointF.Empty)
            CenterImage();
        Invalidate();
    }

    public void ResetView()
    {
        _zoom = 1f;
        CenterImage();
        Invalidate();
    }

    private void CenterImage()
    {
        if (_image == null) return;
        _offset = new PointF(
            (Width - _image.Width) / 2f,
            (Height - _image.Height) / 2f);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        if (_image == null) return;

        e.Graphics.InterpolationMode = _zoom >= 1f
            ? InterpolationMode.NearestNeighbor
            : InterpolationMode.HighQualityBilinear;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;

        var destRect = new RectangleF(_offset.X, _offset.Y,
            _image.Width * _zoom, _image.Height * _zoom);
        e.Graphics.DrawImage(_image, destRect);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (_image == null) return;
        Focus();

        float oldZoom = _zoom;
        float factor = e.Delta > 0 ? 1.25f : 1f / 1.25f;
        float newZoom = Math.Clamp(oldZoom * factor, 0.05f, 40f);
        if (Math.Abs(newZoom - oldZoom) < 1e-4f) return;

        var mouse = new PointF(e.X, e.Y);
        var imagePoint = new PointF(
            (mouse.X - _offset.X) / oldZoom,
            (mouse.Y - _offset.Y) / oldZoom);

        _zoom = newZoom;
        _offset = new PointF(
            mouse.X - imagePoint.X * _zoom,
            mouse.Y - imagePoint.Y * _zoom);
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();

        if (e.Button == MouseButtons.Middle || e.Button == MouseButtons.Right)
        {
            _panning = true;
            _panStart = e.Location;
            _panStartOffset = _offset;
            Cursor = Cursors.SizeAll;
        }
        else if (e.Button == MouseButtons.Left)
        {
            _mouseDownPos = e.Location;
            _dragged = false;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_panning)
        {
            _offset = new PointF(
                _panStartOffset.X + (e.X - _panStart.X),
                _panStartOffset.Y + (e.Y - _panStart.Y));
            Invalidate();
            return;
        }
        if (e.Button == MouseButtons.Left &&
            (Math.Abs(e.X - _mouseDownPos.X) > 3 ||
             Math.Abs(e.Y - _mouseDownPos.Y) > 3))
        {
            _dragged = true;
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        bool wasPanning = _panning;
        _panning = false;
        Cursor = Cursors.Default;

        if (e.Button == MouseButtons.Left && !_dragged && !wasPanning)
        {
            int pid = GetProvinceAt(e.Location);
            ProvinceClicked?.Invoke(pid);
        }
    }

    private int GetProvinceAt(Point canvasPt)
    {
        if (_image == null || Renderer == null) return -1;
        float ix = (canvasPt.X - _offset.X) / _zoom;
        float iy = (canvasPt.Y - _offset.Y) / _zoom;
        int x = (int)ix, y = (int)iy;
        if (x < 0 || y < 0 || x >= _image.Width || y >= _image.Height) return -1;
        return Renderer.GetProvinceIdAt(x, y);
    }

    public Bitmap RenderVisible()
    {
        int w = Math.Max(1, ClientSize.Width);
        int h = Math.Max(1, ClientSize.Height);
        var bmp = new Bitmap(w, h);
        using var g = Graphics.FromImage(bmp);
        g.Clear(BackColor);

        if (_image == null) return bmp;

        g.InterpolationMode = _zoom >= 1f
            ? InterpolationMode.NearestNeighbor
            : InterpolationMode.HighQualityBilinear;
        g.PixelOffsetMode = PixelOffsetMode.Half;

        var destRect = new RectangleF(
            _offset.X, _offset.Y,
            _image.Width * _zoom, _image.Height * _zoom);
        g.DrawImage(_image, destRect);

        return bmp;
    }

    public Bitmap? RenderFull()
    {
        if (_image == null) return null;
        return new Bitmap(_image);
    }
    
    public bool HasImage => _image != null;
}