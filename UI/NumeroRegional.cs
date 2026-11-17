using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace UI
{
    // El editor usa la misma convención que los importes mostrados, sin cambiar
    // la cultura del hilo que también ejecuta reglas e integridad de usuarios.
    public sealed class NumeroRegional : NumericUpDown
    {
        private readonly ToolTip ayuda = new ToolTip();
        private decimal valorMostrado;
        private bool formateando;
        public string ErrorEntrada { get; private set; }
        public bool TieneEdicionPendiente { get { return UserEdit || ErrorEntrada != null; } }

        public static void ExigirEntradas(params NumericUpDown[] controles)
        {
            foreach (var control in controles) {
                var regional = control as NumeroRegional;
                if (regional != null && !regional.ValidarEntrada()) {
                    control.Focus();
                    throw new ArgumentException((control.AccessibleName ?? "Valor") + ": " + regional.ErrorEntrada);
                }
            }
        }

        public static void Restablecer(NumericUpDown control, decimal valor)
        {
            var regional = control as NumeroRegional;
            if (regional != null) { regional.UserEdit = false; regional.ErrorEntrada = null; }
            control.Value = valor;
            if (regional != null) regional.UpdateEditText();
        }

        public bool ValidarEntrada() { ValidateEditText(); return ErrorEntrada == null; }

        protected override void UpdateEditText()
        {
            if (Hexadecimal) { base.UpdateEditText(); return; }
            if (formateando) return;
            if (UserEdit) { ValidateEditText(); return; }
            decimal actual = base.Value;
            if (ErrorEntrada != null && actual == valorMostrado) return;
            ErrorEntrada = null; formateando = true;
            try {
                ChangingText = true;
                Text = actual.ToString((ThousandsSeparator ? "N" : "F") + DecimalPlaces, FormatoPresentacion.Cultura);
                UserEdit = false; valorMostrado = actual;
                MostrarAyuda();
            } finally { formateando = false; }
        }

        protected override void ValidateEditText()
        {
            if (Hexadecimal) { base.ValidateEditText(); return; }
            decimal ingresado = 0; string texto = Text.Trim();
            bool formato = Regex.IsMatch(texto, @"^[+-]?(?:\d+|\d{1,3}(?:\.\d{3})+)(?:,\d+)?$");
            bool valido = formato && decimal.TryParse(texto, NumberStyles.Number, FormatoPresentacion.Cultura, out ingresado);
            // No se reinterpreta un punto decimal como miles ni se recorta un valor al rango.
            int coma = texto.IndexOf(',');
            ErrorEntrada = !valido ? "Formato inválido. Use coma para decimales (ej.: 1.234,56)." :
                coma >= 0 && texto.Length - coma - 1 > DecimalPlaces ? "Use hasta " + DecimalPlaces + " decimales." :
                ingresado < Minimum || ingresado > Maximum ? "El valor debe estar entre " + Minimum.ToString("G", FormatoPresentacion.Cultura) + " y " + Maximum.ToString("G", FormatoPresentacion.Cultura) + "." : null;
            UserEdit = false;
            if (ErrorEntrada != null) { MostrarAyuda(); return; }
            base.Value = ingresado;
            UpdateEditText();
        }

        private void MostrarAyuda()
        {
            string texto = ErrorEntrada ?? (DecimalPlaces == 0 ? "Ingrese un número entero." : "Formato: 1.234,56. Use coma para decimales.");
            AccessibleDescription = texto;
            if (ayuda != null) ayuda.SetToolTip(this, texto);
            ForeColor = ErrorEntrada == null ? TemaAplicacion.Texto : TemaAplicacion.ColorMensaje(true);
        }

        public override void UpButton() { if (ValidarEntrada()) base.UpButton(); }
        public override void DownButton() { if (ValidarEntrada()) base.DownButton(); }

        protected override void Dispose(bool disposing) { if (disposing && ayuda != null) ayuda.Dispose(); base.Dispose(disposing); }

        protected override void OnTextBoxKeyPress(object source, KeyPressEventArgs e)
        {
            if (Hexadecimal) { base.OnTextBoxKeyPress(source, e); return; }
            e.Handled = !char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && e.KeyChar != ',' && e.KeyChar != '.' && e.KeyChar != '-';
            base.OnKeyPress(e);
        }
    }
}
