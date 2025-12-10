using System.Drawing.Drawing2D;

public class MakerGalaxyPanel : Panel
{
    public MakerGalaxyPanel()
    {
        this.DoubleBuffered = true;
        this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                      ControlStyles.UserPaint |
                      ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        Color start = ColorTranslator.FromHtml("#0A0F2E");
        Color end = ColorTranslator.FromHtml("#2C0A47");

        using (var brush = new LinearGradientBrush(
            this.ClientRectangle,
            start,
            end,
            LinearGradientMode.ForwardDiagonal))
        {
            e.Graphics.FillRectangle(brush, this.ClientRectangle);
        }
    }
}
