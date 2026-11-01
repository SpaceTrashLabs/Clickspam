using System.Drawing;
using System.Windows.Forms;

namespace AutoClicker
{
    /// <summary>
    /// Dimmed full-desktop overlay (all monitors). Left-click records the cursor's screen
    /// position; Esc or right-click cancels.
    /// </summary>
    sealed class PointPickerOverlay : Form
    {
        readonly Font hintFont = new Font("Segoe UI", 14f, FontStyle.Bold);

        public Point PickedPoint { get; private set; }

        public PointPickerOverlay()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = SystemInformation.VirtualScreen;
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = Color.Black;
            Opacity = 0.45;
            Cursor = Cursors.Cross;
            KeyPreview = true;
            DoubleBuffered = true;
        }

        protected override void OnShown(System.EventArgs e)
        {
            base.OnShown(e);
            Activate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                PickedPoint = Cursor.Position;
                DialogResult = DialogResult.OK;
            }
            else
            {
                DialogResult = DialogResult.Cancel;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) DialogResult = DialogResult.Cancel;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Point screen = Cursor.Position;
            Point local = PointToClient(screen);
            string text = $"{screen.X}, {screen.Y}\nClick to set the point · Esc to cancel";
            e.Graphics.DrawString(text, hintFont, Brushes.White, local.X + 18, local.Y + 18);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) hintFont.Dispose();
            base.Dispose(disposing);
        }
    }
}
