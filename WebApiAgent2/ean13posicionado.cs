using System.Drawing;

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
            using (var sformat = new StringFormat()

            {

                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Far,

            })    
            {

                g.Clear(Color.Transparent);
                g.DrawImage(codbarean13, 10, 10);
                g.DrawString(digito1, new Font("Arial", 6), Brushes.Black,x-x+6, y-5  ,sformat);
                g.DrawString(digito2, new Font("Arial", 6), Brushes.Black, x - 20, y-5, sformat);
                g.DrawString(digito3, new Font("Arial", 6), Brushes.Black, x+24 , y - 5, sformat);

            }

            BCimage.Image = imgComplete;
        }
    }

}
