using System;
using System.Collections;
using System.Drawing;
using static CortizoERP.Etiquetas.Funciones;

namespace CortizoERP.Etiquetas
{
    public class CodigoBarrasean13 : IElemento
    {
        public Posicion Posicion { get; set; }
        public Tamaño Tamaño { get; set; }
        public int Alto { get; set; }
        public Hashtable Datos { get; set; }
        public string Origen { get; set; }
        public TipoOrigen Tipo { get; set; }
        public bool ShowLabel { get; set; } = true;

        public CodigoBarrasean13()
        {
            Posicion = new Posicion();
            Tamaño = new Tamaño();
            Tipo = TipoOrigen.Dato;
        }

        private const int TOTAL_MODULES = 95;

        private string _ean13Ini;
        private string _ean13Fin;
        private string[] _ean13Dat_r;
        private string[] _ean13Dat_l;
        private string _ean13_sep6;
        private string[] _ean13Dat_g;
        private string[] _ean13Dat_p;

        public object Clone()
        {
            return this.MemberwiseClone();
        }

        public void Imprimir(Graphics graphics)
        {
            var codigo = ObtenerOrigen(Tipo, Origen, Datos);
            if (string.IsNullOrEmpty(codigo)) return;

            // Determinar dimensiones: preferir Tamaño, fallback a Alto
            int ancho = (Tamaño != null && Tamaño.Ancho > 0) ? Tamaño.Ancho : 0;
            int alto = (Tamaño != null && Tamaño.Alto > 0) ? Tamaño.Alto : Alto;
            if (alto <= 0) alto = 50;
            if (ancho <= 0) ancho = (int)(alto * 1.5);

            // Calcular espacio para texto
            float fontSize = ShowLabel ? Math.Max(6f, alto * 0.14f) : 0;
            int altoTexto = 0;
            int margenIzq = 0;

            if (ShowLabel)
            {
                using (var tmpFont = new Font("Arial", fontSize, FontStyle.Regular))
                {
                    var textSize = graphics.MeasureString("0", tmpFont, 0, StringFormat.GenericTypographic);
                    altoTexto = (int)Math.Ceiling(textSize.Height);
                    margenIzq = (int)Math.Ceiling(textSize.Width * 1.3f);
                }
            }

            int anchoBarras = Math.Max(TOTAL_MODULES, ancho - margenIzq);
            float moduleW = (float)anchoBarras / TOTAL_MODULES;
            int altoBarras = alto; // Barras a altura completa
            int altoBarrasDatos = alto - altoTexto; // Barras de datos más cortas

            // Calcular codificación
            string encoded = Calcular(codigo);
            if (encoded.StartsWith("Error")) return;

            float x0 = Posicion.X + margenIzq;
            float y0 = Posicion.Y;

            using (var pen = new Pen(Color.Black, moduleW))
            {
                pen.Alignment = System.Drawing.Drawing2D.PenAlignment.Left;

                float xPos = x0;
                for (int i = 0; i < encoded.Length; i++)
                {
                    bool isGuard = (i < 3) || (i >= 45 && i < 50) || (i >= 92);
                    float barBottom = isGuard ? y0 + altoBarras : y0 + altoBarrasDatos;

                    if (encoded[i] == 'N')
                    {
                        graphics.DrawLine(pen, xPos, y0, xPos, barBottom);
                    }
                    xPos += moduleW;
                }
            }

            // Pintar números
            if (ShowLabel && codigo.Length >= 13)
            {
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                var sfTypo = StringFormat.GenericTypographic;

                using (var font = new Font("Arial", fontSize, FontStyle.Regular))
                using (var brush = new SolidBrush(Color.Black))
                {
                    float textY = Posicion.Y + alto - altoTexto;

                    // Primer dígito: a la izquierda de las barras
                    graphics.DrawString(codigo.Substring(0, 1), font, brush, Posicion.X, textY);

                    // Dígitos 2-7: centrados bajo grupo izquierdo (módulos 3-44)
                    string leftDigits = codigo.Substring(1, 6);
                    float leftWidth = graphics.MeasureString(leftDigits, font, 0, sfTypo).Width;
                    float centerLeft = x0 + 24f * moduleW;
                    graphics.DrawString(leftDigits, font, brush, centerLeft - leftWidth / 2, textY);

                    // Dígitos 8-13: centrados bajo grupo derecho (módulos 50-91)
                    string rightDigits = codigo.Substring(7, 6);
                    float rightWidth = graphics.MeasureString(rightDigits, font, 0, sfTypo).Width;
                    float centerRight = x0 + 71f * moduleW;
                    graphics.DrawString(rightDigits, font, brush, centerRight - rightWidth / 2, textY);
                }
            }
        }

        private int obtener(string parTexto)
        {
            if (parTexto.Length > 0 && parTexto[0] >= '0' && parTexto[0] <= '9')
                return parTexto[0] - '0';
            return -1;
        }

        private string Calcular(string parTexto)
        {
            Cargar();

            if (parTexto.Length != 13)
                return "Error! La longitud debe ser de 13 dígitos";

            // Último grupo de 6 dígitos (derecha)
            string ean13_s_u6 = "";
            for (int i = 7; i < 13; i++)
            {
                int d = obtener(parTexto.Substring(i, 1));
                if (d >= 0) ean13_s_u6 += _ean13Dat_r[d];
            }

            // Codec del primer dígito
            int d1 = obtener(parTexto.Substring(0, 1));
            string codec = (d1 >= 0) ? _ean13Dat_p[d1] : "";

            // Grupo medio de 6 dígitos (izquierda)
            string ean13_s_m6 = "";
            for (int i = 1; i <= 6; i++)
            {
                int d = obtener(parTexto.Substring(i, 1));
                if (d >= 0)
                {
                    string enc = codec.Substring(i - 1, 1);
                    ean13_s_m6 += (enc == "L") ? _ean13Dat_l[d] : _ean13Dat_g[d];
                }
            }

            return _ean13Ini + ean13_s_m6 + _ean13_sep6 + ean13_s_u6 + _ean13Fin;
        }

        private void Cargar()
        {
            _ean13Ini = "N_N";
            _ean13_sep6 = "_N_N_";
            _ean13Fin = "N_N";
            _ean13Dat_r = new string[10];

            _ean13Dat_r[0] = "NNN__N_"; // 0
            _ean13Dat_r[1] = "NN__NN_"; // 1
            _ean13Dat_r[2] = "NN_NN__"; // 2
            _ean13Dat_r[3] = "N____N_"; // 3
            _ean13Dat_r[4] = "N_NNN__"; // 4
            _ean13Dat_r[5] = "N__NNN_"; // 5
            _ean13Dat_r[6] = "N_N____"; // 6
            _ean13Dat_r[7] = "N___N__"; // 7
            _ean13Dat_r[8] = "N__N___"; // 8
            _ean13Dat_r[9] = "NNN_N__"; // 9

            _ean13Dat_g = new string[10];
            _ean13Dat_g[0] = "_N__NNN"; // 0
            _ean13Dat_g[1] = "_NN__NN"; // 1
            _ean13Dat_g[2] = "__NN_NN"; // 2
            _ean13Dat_g[3] = "_N____N"; // 3
            _ean13Dat_g[4] = "__NNN_N"; // 4
            _ean13Dat_g[5] = "_NNN__N"; // 5
            _ean13Dat_g[6] = "____N_N"; // 6
            _ean13Dat_g[7] = "__N___N"; // 7
            _ean13Dat_g[8] = "___N__N"; // 8
            _ean13Dat_g[9] = "__N_NNN"; // 9

            _ean13Dat_l = new string[10];
            _ean13Dat_l[0] = "___NN_N"; // 0
            _ean13Dat_l[1] = "__NN__N"; // 1
            _ean13Dat_l[2] = "__N__NN"; // 2
            _ean13Dat_l[3] = "_NNNN_N"; // 3
            _ean13Dat_l[4] = "_N___NN"; // 4
            _ean13Dat_l[5] = "_NN___N"; // 5
            _ean13Dat_l[6] = "_N_NNNN"; // 6
            _ean13Dat_l[7] = "_NNN_NN"; // 7
            _ean13Dat_l[8] = "_NN_NNN"; // 8
            _ean13Dat_l[9] = "___N_NN"; // 9

            _ean13Dat_p = new string[10];
            _ean13Dat_p[0] = "LLLLLL"; // 0
            _ean13Dat_p[1] = "LLGLGG"; // 1
            _ean13Dat_p[2] = "LLGGLG"; // 2
            _ean13Dat_p[3] = "LLGGGL"; // 3
            _ean13Dat_p[4] = "LGLLGG"; // 4
            _ean13Dat_p[5] = "LGGLLG"; // 5
            _ean13Dat_p[6] = "LGGGLL"; // 6
            _ean13Dat_p[7] = "LGLGLG"; // 7
            _ean13Dat_p[8] = "LGLGGL"; // 8
            _ean13Dat_p[9] = "LGGLGL"; // 9
        }
    }
}
