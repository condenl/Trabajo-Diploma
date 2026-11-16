using System;
using System.Drawing;
using System.Windows.Forms;
using BE;
using BE.Seguridad;
using BLL;
using Servicios;

namespace UI
{
    public abstract class FormAltaCatalogo : Form
    {
        private readonly TableLayoutPanel campos;
        private readonly Label error = new Label { Name = "lblErrorCatalogo", AutoSize = true, ForeColor = Color.Firebrick, Dock = DockStyle.Top, Padding = new Padding(12, 6, 12, 6) };
        protected FormAltaCatalogo(string titulo)
        {
            ExigirPermiso();
            Text = titulo; StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Font; ClientSize = new Size(560, 410);
            MinimumSize = new Size(500, 430); MaximizeBox = false; MinimizeBox = false;
            campos = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(16) };
            campos.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
            campos.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var contenido = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            contenido.Controls.Add(campos);
            var pie = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12) };
            var guardar = new Button { Text = "Guardar", Name = "btnGuardarCatalogo", AutoSize = true, Padding = new Padding(9, 4, 9, 4) };
            var cancelar = new Button { Text = "Cancelar", Name = "btnCancelarCatalogo", AutoSize = true, Padding = new Padding(9, 4, 9, 4), DialogResult = DialogResult.Cancel };
            guardar.Click += delegate { Guardar(); };
            pie.Controls.Add(guardar); pie.Controls.Add(cancelar);
            Controls.Add(contenido); Controls.Add(error); Controls.Add(pie);
            AcceptButton = guardar; CancelButton = cancelar;
            TemaAplicacion.Aplicar(this);
        }
        protected void Campo(string etiqueta, Control control)
        {
            control.AccessibleName = etiqueta;
            int fila = campos.RowCount++;
            campos.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            campos.Controls.Add(new Label { Text = etiqueta, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 10, 8, 10) }, 0, fila);
            control.Dock = DockStyle.Fill; control.Margin = new Padding(0, 7, 0, 7);
            campos.Controls.Add(control, 1, fila);
        }
        protected static void ExigirPermiso()
        {
            var actor = Session.Instance.Usuario;
            if (actor == null || !actor.Activo || !actor.TienePermiso(TipoPermiso.Aprovisionamiento_Gestion))
                throw new UnauthorizedAccessException("La gestión de catálogos requiere una sesión de Compras o Dueño.");
        }
        private void Guardar()
        {
            try { ExigirPermiso(); GuardarRegistro(); DialogResult = DialogResult.OK; Close(); }
            catch (Exception ex) { error.Text = ex.Message; }
        }
        protected abstract void GuardarRegistro();
    }

    public sealed class FormAltaProveedor : FormAltaCatalogo
    {
        private readonly TextBox razon = new TextBox { Name = "txtProveedorRazon", MaxLength = 150 };
        private readonly TextBox cuit = new TextBox { Name = "txtProveedorCuit", MaxLength = 20 };
        private readonly TextBox contacto = new TextBox { Name = "txtProveedorContacto", MaxLength = 200 };
        private readonly NumericUpDown antiguedad = new NumeroRegional { Name = "nudProveedorAntiguedad", Maximum = 200 };
        private readonly NumericUpDown prestigio = new NumeroRegional { Name = "nudProveedorPrestigio", Maximum = 100, DecimalPlaces = 2, Value = 50 };
        public FormAltaProveedor() : base("Nuevo proveedor")
        {
            Campo("Razón social", razon); Campo("CUIT (11 dígitos)", cuit); Campo("Contacto", contacto);
            Campo("Antigüedad (años)", antiguedad); Campo("Prestigio (0 a 100)", prestigio);
        }
        protected override void GuardarRegistro()
        {
            NumeroRegional.ExigirEntradas(antiguedad, prestigio);
            new ProveedorBLL().Guardar(new Proveedor { RazonSocial = razon.Text.Trim(), Cuit = cuit.Text.Trim(), Contacto = contacto.Text.Trim(),
                AntiguedadMercado = (int)antiguedad.Value, IndicePrestigio = prestigio.Value });
        }
    }

    public sealed class FormAltaPerfume : FormAltaCatalogo
    {
        private readonly TextBox codigo = new TextBox { Name = "txtProductoCodigo", MaxLength = 50 };
        private readonly TextBox nombre = new TextBox { Name = "txtProductoNombre", MaxLength = 150 };
        private readonly TextBox marca = new TextBox { Name = "txtProductoMarca", MaxLength = 100 };
        private readonly TextBox familia = new TextBox { Name = "txtProductoFamilia", MaxLength = 100 };
        private readonly NumericUpDown precio = new NumeroRegional { Name = "nudProductoPrecio", Maximum = 100000000, DecimalPlaces = 2, ThousandsSeparator = true };
        private readonly NumericUpDown minimo = new NumeroRegional { Name = "nudProductoStockMinimo", Maximum = 100000 };
        private readonly ComboBox seleccion = new ComboBox { Name = "cmbEditarPerfume", DropDownStyle = ComboBoxStyle.DropDownList, FormattingEnabled = true, DropDownWidth = 500 };
        private readonly bool edicion;
        private Producto productoEditado;
        public FormAltaPerfume() : this(false) { }
        public FormAltaPerfume(bool editar) : base(editar ? "Editar perfume" : "Nuevo perfume")
        {
            edicion = editar;
            if (editar) Campo("Perfume a editar", seleccion);
            Campo("Código", codigo); Campo("Nombre", nombre); Campo("Marca", marca); Campo("Familia olfativa", familia);
            Campo("Precio de venta", precio); Campo("Stock mínimo", minimo);
            if (editar) {
                seleccion.DisplayMember = "Nombre"; seleccion.ValueMember = "IdProducto";
                seleccion.Format += delegate(object sender, ListControlConvertEventArgs e) {
                    var p = (Producto)e.ListItem; e.Value = p.Nombre + " · " + p.Codigo + (p.Activo ? "" : " (inactivo)");
                };
                seleccion.SelectedIndexChanged += delegate {
                    productoEditado = seleccion.SelectedItem as Producto;
                    if (productoEditado == null) return;
                    codigo.Text = productoEditado.Codigo; nombre.Text = productoEditado.Nombre;
                    marca.Text = productoEditado.Marca; familia.Text = productoEditado.FamiliaOlfativa;
                    precio.Maximum = Math.Max(100000000m, productoEditado.PrecioVenta);
                    minimo.Maximum = Math.Max(100000m, productoEditado.StockMinimo);
                    NumeroRegional.Restablecer(precio, productoEditado.PrecioVenta);
                    NumeroRegional.Restablecer(minimo, productoEditado.StockMinimo);
                };
                seleccion.DataSource = new ProductoBLL().Listar(false);
            }
            TemaAplicacion.Aplicar(this);
        }
        protected override void GuardarRegistro()
        {
            NumeroRegional.ExigirEntradas(precio, minimo);
            var bll = new ProductoBLL();
            var existente = edicion && productoEditado != null ? bll.Obtener(productoEditado.IdProducto) : null;
            if (edicion && existente == null) throw new InvalidOperationException("Seleccione un perfume existente para editar.");
            bll.Guardar(new Producto { IdProducto = existente == null ? 0 : existente.IdProducto, Activo = existente == null || existente.Activo,
                Codigo = codigo.Text.Trim(), Nombre = nombre.Text.Trim(), Marca = marca.Text.Trim(),
                FamiliaOlfativa = familia.Text.Trim(), PrecioVenta = precio.Value, StockMinimo = (int)minimo.Value });
        }
    }
}
