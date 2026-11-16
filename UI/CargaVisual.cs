using System;
using System.Drawing;
using System.Windows.Forms;

namespace UI
{
    // Compose descendant windows together instead of exposing each child paint.
    // https://learn.microsoft.com/windows/win32/winmsg/extended-window-styles
    internal sealed class PanelContenido : Panel
    {
        protected override CreateParams CreateParams
        {
            get { var parametros = base.CreateParams; parametros.ExStyle |= 0x02000000; return parametros; }
        }
    }

    internal static class CargaVisual
    {
        internal static void Ejecutar(Control contenedor, Action cargar, string mensaje)
        {
            if (!contenedor.IsHandleCreated || !contenedor.Visible) { cargar(); return; }
            using (var cubierta = new Panel { Name = "panelCargaNavegacion", Bounds = contenedor.ClientRectangle,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = SystemInformation.HighContrast ? SystemColors.Window : TemaAplicacion.Superficie,
                AccessibleName = mensaje, TabStop = false })
            {
                cubierta.Controls.Add(new Label { Name = "lblCargaNavegacion", Text = mensaje, Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter, ForeColor = TemaAplicacion.Texto, AutoSize = false });
                bool esperaAnterior = contenedor.UseWaitCursor;
                var cursorAnterior = Cursor.Current;
                ControlEventHandler mantenerCubierta = delegate { cubierta.BringToFront(); };
                try {
                    contenedor.Controls.Add(cubierta);
                    contenedor.ControlAdded += mantenerCubierta;
                    cubierta.BringToFront(); contenedor.UseWaitCursor = true; Cursor.Current = Cursors.WaitCursor;
                    // Paint the indicator before synchronous UI/data initialization. Do not
                    // pump input with DoEvents or construct WinForms controls on a worker.
                    cubierta.Refresh();
                    cargar();
                    contenedor.PerformLayout();
                } finally {
                    contenedor.ControlAdded -= mantenerCubierta;
                    contenedor.Controls.Remove(cubierta);
                    contenedor.UseWaitCursor = esperaAnterior; Cursor.Current = cursorAnterior;
                    contenedor.Invalidate(true);
                }
            }
        }
    }
}
