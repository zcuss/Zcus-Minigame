namespace AutoMinigameWinForms;

public sealed class CaptureDragEventArgs(int deltaX, int deltaY) : EventArgs
{
    public int DeltaX { get; } = deltaX;
    public int DeltaY { get; } = deltaY;
}

public sealed class MiniPreviewForm : Form
{
    private readonly PictureBox _picture;
    private bool _draggingCapture;
    private Point _dragStart;

    public event EventHandler<CaptureDragEventArgs>? CaptureDrag;

    public MiniPreviewForm(int x, int y, int w, int h)
    {
        Text = "Capture Preview";
        StartPosition = FormStartPosition.Manual;
        FormBorderStyle = FormBorderStyle.None;
        TopMost = false;
        ShowInTaskbar = false;
        BackColor = Color.Black;
        SetBounds(x, y, Math.Max(80, w), Math.Max(80, h));

        _picture = new PictureBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            SizeMode = PictureBoxSizeMode.StretchImage,
            BorderStyle = BorderStyle.FixedSingle,
        };
        Controls.Add(_picture);

        _picture.MouseDown += PictureOnMouseDown;
        _picture.MouseMove += PictureOnMouseMove;
        _picture.MouseUp += PictureOnMouseUp;
    }

    public void ApplyGeometry(int x, int y, int w, int h)
    {
        SetBounds(x, y, Math.Max(80, w), Math.Max(80, h));
    }

    public void SetFrame(Bitmap bitmap)
    {
        var old = _picture.Image;
        _picture.Image = bitmap;
        old?.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _picture.MouseDown -= PictureOnMouseDown;
            _picture.MouseMove -= PictureOnMouseMove;
            _picture.MouseUp -= PictureOnMouseUp;
            _picture.Image?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void PictureOnMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _draggingCapture = true;
        _dragStart = e.Location;
        Cursor = Cursors.SizeAll;
    }

    private void PictureOnMouseMove(object? sender, MouseEventArgs e)
    {
        if (!_draggingCapture)
        {
            return;
        }

        var dx = e.X - _dragStart.X;
        var dy = e.Y - _dragStart.Y;
        if (dx == 0 && dy == 0)
        {
            return;
        }

        _dragStart = e.Location;
        CaptureDrag?.Invoke(this, new CaptureDragEventArgs(dx, dy));
    }

    private void PictureOnMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _draggingCapture = false;
        Cursor = Cursors.Default;
    }
}
