using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

public class MakerGalaxyForm : Form
{
    public MakerGalaxyForm()
    {
        // Reduce flicker
        this.DoubleBuffered = true;

        // Useful if your form uses custom borders or rounded corners later
        this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                      ControlStyles.UserPaint |
                      ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Maker Galaxy gradient colors
        Color start = ColorTranslator.FromHtml("#0A0F2E"); // dark blue
        Color end = ColorTranslator.FromHtml("#2C0A47"); // deep purple

        using (LinearGradientBrush brush = new LinearGradientBrush(
            this.ClientRectangle,
            start,
            end,
            LinearGradientMode.ForwardDiagonal    // 45-degree cosmic sweep
        ))
        {
            e.Graphics.FillRectangle(brush, this.ClientRectangle);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        using (var brush = new LinearGradientBrush(
            this.ClientRectangle,
            ColorTranslator.FromHtml("#0A0F2E"),
            ColorTranslator.FromHtml("#2C0A47"),
            LinearGradientMode.ForwardDiagonal))
        {
            e.Graphics.FillRectangle(brush, this.ClientRectangle);
        }

        base.OnPaint(e); // draw child controls, borders, etc.
    }

}
