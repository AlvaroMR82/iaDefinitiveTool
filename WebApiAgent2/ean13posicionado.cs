using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace cbprueba1
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button2_Click(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            Zen.Barcode.CodeEan13BarcodeDraw codeean13  = Zen.Barcode.BarcodeDrawFactory.CodeEan13WithChecksum;

          BCimage.Image = codeean13.Draw(txtcb.Text, 60);
            var codbarean13 = BCimage.Image;

            var imgComplete = new Bitmap(codbarean13.Width+20, codbarean13.Height + 20);


            var x = imgComplete.Width  / 2;
            var y = imgComplete.Height;
            string digito1 = txtcb.Text.Length > 0 ? txtcb.Text.Substring(0, 1) : "";
            string digito2 = txtcb.Text.Length > 0 ? txtcb.Text.Substring(1, 6) : "";
            string digito3 = txtcb.Text.Length > 0 ? txtcb.Text.Substring(1, 6) : "";


            //var imagen = b.Encode(BarcodeLib.TYPE.EAN13, codigo, ancho + margenLabel, alto);

            using (var g = Graphics.FromImage(imgComplete))
            using (var sformat = (StringFormat)StringFormat.GenericTypographic.Clone())
            {
                // Improve rendering quality and consistency
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.CompositingQuality = CompositingQuality.HighQuality;

                sformat.Alignment = StringAlignment.Center;
                sformat.LineAlignment = StringAlignment.Far;

                g.Clear(Color.Transparent);
                g.DrawImage(codbarean13, 10, 10);

                using (var font = new Font("Arial", 6, FontStyle.Regular))
                {
                    g.DrawString(digito1, font, Brushes.Black, x - x + 6, y - 5, sformat);
                    g.DrawString(digito2, font, Brushes.Black, x - 20, y - 5, sformat);
                    g.DrawString(digito3, font, Brushes.Black, x + 24, y - 5, sformat);
                }
            }

            BCimage.Image = imgComplete;
        }
    }

}
