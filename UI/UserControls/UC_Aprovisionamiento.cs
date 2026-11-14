using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using BE;
using BE.Seguridad;
using BLL;
using Servicios;
using UI.Reportes;

namespace UI.UserControls
{
    public class UC_Aprovisionamiento : UserControl, IVistaConCambiosPendientes
    {
        public bool TieneCambiosPendientes { get { return
            TieneBorradorPendiente || TieneRecepcionPendiente ||
            nudCotizacionPrecio.Value > 0 || !string.IsNullOrWhiteSpace(txtCotizacionCondiciones.Text) ||
            EdicionNumerica(nudCotizacionPrecio) || EdicionNumerica(nudCotizacionPlazo) ||
            (evaluacion != null && evaluacion.TieneCambiosPendientes); } }
        private bool TieneBorradorPendiente { get { return renglonesOrden.Rows.Count > 0 || nudOrdenPrecio.Value > 0 || nudOrdenCantidad.Value != 1 || EdicionNumerica(nudOrdenPrecio) || EdicionNumerica(nudOrdenCantidad); } }
        private bool TieneRecepcionPendiente { get { return !string.IsNullOrWhiteSpace(txtLoteCodigo.Text) || !string.IsNullOrWhiteSpace(txtLoteOrigen.Text) ||
            nudLoteCantidad.Value != 1 || EdicionNumerica(nudLoteCantidad) || !dtpLoteVencimiento.Checked || dtpLoteVencimiento.Value.Date != vencimientoInicial; } }
        private static bool EdicionNumerica(NumericUpDown control) { var n = control as NumeroRegional; return n != null && n.TieneEdicionPendiente; }
        public bool ModoPrueba { get; set; }
        public string UltimoMensaje { get; private set; }
        public string UltimoError { get; private set; }
        public string RutaArchivoPrueba { get; set; }
        public bool ConfirmarImportacionPrueba { get; set; }

        private readonly Usuario usuario;
        private readonly ProveedorBLL proveedorBLL = new ProveedorBLL();
        private readonly ProductoBLL productoBLL = new ProductoBLL();
        private readonly AprovisionamientoBLL aprovisionamientoBLL = new AprovisionamientoBLL();
        private readonly InventarioBLL inventarioBLL = new InventarioBLL();
        private readonly IntercambioOrdenesBLL intercambioOrdenesBLL = new IntercambioOrdenesBLL();
        private Button btnReporteOrdenesPdf;
        private Button btnExportarOrdenesXml;
        private UC_EvaluacionProveedores evaluacion;
        private bool recargando;
        private bool enlazandoRecepcion;
        private int ordenRecepcionActual, productoRecepcionActual;
        private readonly DateTime vencimientoInicial = DateTime.Today.AddYears(2);
        private Button registrarRecepcion;
        private readonly Label saldoRecepcion = new Label { Name = "lblSaldoRecepcion", AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(4, 8, 4, 8) };
        private FlowLayoutPanel formularioOrden;
        private bool cargandoRenglon;
        private Button agregarRenglon, quitarRenglon;
        private readonly DataGridView renglonesOrden = Grilla("dgvRenglonesOrden");
        private readonly Label totalBorrador = new Label { Name = "lblTotalBorrador", AutoSize = true, Margin = new Padding(4, 10, 4, 4) };
        private Button enviarRevision, emitirProveedor;
        private readonly Label ayudaOrden = new Label { Name = "lblAccionOrden", AutoSize = true, Margin = new Padding(6, 8, 3, 3), MaximumSize = new Size(460, 0) };
        private readonly ComboBox filtroProveedor = Combo("cmbFiltroProveedor");
        private readonly ComboBox filtroEstado = Combo("cmbFiltroEstado");
        private readonly CheckBox filtrarFechas = new CheckBox { Name = "chkFechasOrdenes", Text = "Por fecha", AutoSize = true };
        private readonly DateTimePicker fechaDesde = new DateTimePicker { Name = "dtpOrdenDesde", Format = DateTimePickerFormat.Short, Value = DateTime.Today.AddMonths(-1), Width = 110, Enabled = false };
        private readonly DateTimePicker fechaHasta = new DateTimePicker { Name = "dtpOrdenHasta", Format = DateTimePickerFormat.Short, Value = DateTime.Today, Width = 110, Enabled = false };
        private List<OrdenCompra> ordenesCargadas = new List<OrdenCompra>();
        private bool cargandoFiltros;
        private readonly Label estadoCarga = new Label { Name = "lblActualizacionCompras", Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(10, 4, 10, 4) };

        private readonly TabControl tabs = new PestanasTematicas { Dock = DockStyle.Fill };
        private readonly TabPage tabCatalogos = new TabPage("Cotizaciones");
        private readonly TabPage tabOrdenes = new TabPage("Órdenes de compra");
        private readonly TabPage tabContabilidad = new TabPage("Aprobación contable");
        private readonly TabPage tabRecepcion = new TabPage("Recepción e inventario");

        private readonly ComboBox cmbCotizacionProveedor = Combo("cmbCotizacionProveedor");
        private readonly ComboBox cmbCotizacionProducto = Combo("cmbCotizacionProducto");
        private readonly NumericUpDown nudCotizacionPrecio = Dinero("nudCotizacionPrecio");
        private readonly NumericUpDown nudCotizacionPlazo = new NumeroRegional { Name = "nudCotizacionPlazo", Maximum = 365 };
        private readonly DateTimePicker dtpCotizacionVigencia = new DateTimePicker { Name = "dtpCotizacionVigencia", Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Value = DateTime.Today.AddMonths(1) };
        private readonly TextBox txtCotizacionCondiciones = new TextBox { Name = "txtCotizacionCondiciones" };

        private readonly ComboBox cmbOrdenProveedor = Combo("cmbOrdenProveedor");
        private readonly ComboBox cmbOrdenProducto = Combo("cmbOrdenProducto");
        private readonly NumericUpDown nudOrdenCantidad = new NumeroRegional { Name = "nudOrdenCantidad", Minimum = 1, Maximum = 100000, Value = 1 };
        private readonly NumericUpDown nudOrdenPrecio = Dinero("nudOrdenPrecio");
        private readonly DataGridView dgvOrdenes = Grilla("dgvOrdenes");

        private readonly DataGridView dgvPendientes = Grilla("dgvPendientes");
        private readonly TextBox txtMotivoRechazo = new TextBox { Name = "txtMotivoRechazo" };

        private readonly ComboBox cmbOrdenRecepcion = Combo("cmbOrdenRecepcion");
        private readonly ComboBox cmbRecepcionProducto = Combo("cmbRecepcionProducto");
        private readonly TextBox txtLoteCodigo = new TextBox { Name = "txtLoteCodigo" };
        private readonly TextBox txtLoteOrigen = new TextBox { Name = "txtLoteOrigen" };
        private readonly NumericUpDown nudLoteCantidad = new NumeroRegional { Name = "nudLoteCantidad", Minimum = 1, Maximum = 100000, Value = 1 };
        private readonly DateTimePicker dtpLoteVencimiento = new DateTimePicker { Name = "dtpLoteVencimiento", Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Value = DateTime.Today.AddYears(2), ShowCheckBox = true, Checked = true };
        private readonly ComboBox cmbInventarioProducto = Combo("cmbInventarioProducto");
        private readonly Label lblStock = new Label { Name = "lblStock", AutoSize = true, Text = "Stock disponible: -" };
        private readonly DataGridView dgvTrazabilidad = Grilla("dgvTrazabilidad");

        public UC_Aprovisionamiento()
            : this(Session.Instance.Usuario)
        {
        }

        public UC_Aprovisionamiento(Usuario usuarioActual)
        {
            usuario = usuarioActual;
            Dock = DockStyle.Fill;
            SizeChanged += delegate { estadoCarga.MaximumSize = new Size(Math.Max(100, ClientSize.Width), 0); };
            BindingContext = new BindingContext();
            ConstruirInterfaz();
            ConfigurarAcceso();
            RecargarTodo();
            if (tabs.TabPages.Contains(tabOrdenes)) tabs.SelectedTab = tabOrdenes;
            tabs.SelectedIndexChanged += delegate { RecargarTodo(); };
        }

        private static ComboBox Combo(string nombre)
        {
            return new ComboBox { Name = nombre, DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
        }

        private static NumericUpDown Dinero(string nombre)
        {
            return new NumeroRegional { Name = nombre, DecimalPlaces = 2, Maximum = 100000000, Minimum = 0, ThousandsSeparator = true };
        }

        private static DataGridView Grilla(string nombre)
        {
            return new DataGridView
            {
                Name = nombre,
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
        }

        private void ConstruirInterfaz()
        {
            tabs.TabPages.Add(tabCatalogos);
            tabs.TabPages.Add(tabOrdenes);
            tabs.TabPages.Add(tabContabilidad);
            tabs.TabPages.Add(tabRecepcion);
            ConstruirCatalogos();
            ConstruirOrdenes();
            ConstruirContabilidad();
            ConstruirRecepcion();
            if (TienePermiso(TipoPermiso.Aprovisionamiento_Gestion))
            {
                var paginaEvaluacion = new TabPage("Evaluación de proveedores");
                evaluacion = new UC_EvaluacionProveedores();
                evaluacion.AbrirOrdenSolicitada += AbrirOrden;
                paginaEvaluacion.Controls.Add(evaluacion);
                tabs.TabPages.Add(paginaEvaluacion);
            }
            Controls.Add(tabs);
            Controls.Add(estadoCarga);
            if (TienePermiso(TipoPermiso.Aprovisionamiento_Gestion))
            {
                var menu = new MenuStrip { Name = "menuCompras", Dock = DockStyle.Top };
                var catalogos = new ToolStripMenuItem("Catálogos") { Name = "menuCatalogos" };
                catalogos.DropDownItems.Add(new ToolStripMenuItem("Nuevo proveedor...", null, delegate { AbrirAlta(true); }) { Name = "menuNuevoProveedor" });
                catalogos.DropDownItems.Add(new ToolStripMenuItem("Nuevo perfume...", null, delegate { AbrirAlta(false); }) { Name = "menuNuevoPerfume" });
                menu.Items.Add(catalogos); Controls.Add(menu);
            }
        }

        private void AbrirAlta(bool proveedor)
        {
            Ejecutar(delegate
            {
                if (!TienePermiso(TipoPermiso.Aprovisionamiento_Gestion)) throw new UnauthorizedAccessException("Se requiere permiso de Compras.");
                using (Form alta = proveedor ? (Form)new FormAltaProveedor() : new FormAltaPerfume())
                    if (alta.ShowDialog(this) == DialogResult.OK)
                    {
                        RecargarTodo();
                        estadoCarga.Text = (proveedor ? "Proveedor" : "Perfume") + " agregado. Listas actualizadas a las " + DateTime.Now.ToString("HH:mm:ss") + ".";
                    }
            }, null);
        }

        private void ConstruirCatalogos()
        {
            var flujo = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 3, AutoScroll = true };
            flujo.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            flujo.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            flujo.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            flujo.Controls.Add(Titulo("Registrar cotización recibida"), 0, 0);
            FlowLayoutPanel fc = FlujoHorizontal();
            AgregarCampo(fc, "Proveedor", cmbCotizacionProveedor, 200);
            AgregarCampo(fc, "Perfume", cmbCotizacionProducto, 200);
            AgregarCampo(fc, "Precio unitario", nudCotizacionPrecio, 100);
            AgregarCampo(fc, "Entrega (días)", nudCotizacionPlazo, 80);
            AgregarCampo(fc, "Vigencia", dtpCotizacionVigencia, 135);
            AgregarCampo(fc, "Condiciones", txtCotizacionCondiciones, 160);
            fc.Controls.Add(Boton("Registrar cotización", "btnRegistrarCotizacion", RegistrarCotizacion));
            fc.Controls.Add(Boton("Actualizar", "btnActualizarCotizaciones", delegate { RecargarTodo(); }));
            flujo.Controls.Add(fc, 0, 1);
            flujo.Controls.Add(new Label { Text = "Las altas se encuentran en Catálogos → Nuevo proveedor / Nuevo perfume.", AutoSize = true, Padding = new Padding(5, 15, 5, 5) }, 0, 2);
            tabCatalogos.Controls.Add(flujo);
        }

        private void ConstruirOrdenes()
        {
            var desplazamiento = new Panel { Name = "scrollOrdenes", Dock = DockStyle.Fill, AutoScroll = true };
            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, RowCount = 6, ColumnCount = 1, Padding = new Padding(12) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            dgvOrdenes.MinimumSize = new Size(0, 180);
            desplazamiento.SizeChanged += delegate { layout.MinimumSize = new Size(0, desplazamiento.ClientSize.Height); };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(Titulo("Orden de compra y envío a revisión contable"), 0, 0);
            FlowLayoutPanel barra = FlujoHorizontal();
            barra.Controls.Add(Boton("Nuevo borrador", "btnNuevoBorrador", delegate { NuevoBorrador(); }));
            enviarRevision = Boton("Enviar a Contabilidad", "btnEnviarRevision", EnviarRevision);
            emitirProveedor = Boton("Emitir al proveedor", "btnEmitirOrden", EmitirOrden);
            barra.Controls.Add(enviarRevision); barra.Controls.Add(emitirProveedor);
            barra.Controls.Add(Boton("Actualizar", "btnActualizarOrdenes", delegate { RecargarTodo(); }));
            AgregarDetalleOrden(barra, dgvOrdenes, "btnDetalleOrden");
            barra.Controls.Add(ayudaOrden);
            dgvOrdenes.SelectionChanged += delegate { ActualizarAccionesOrden(); };
            dgvOrdenes.CurrentCellChanged += delegate { ActualizarAccionesOrden(); };
            foreach (Button boton in barra.Controls.OfType<Button>()) boton.Margin = new Padding(3);
            layout.Controls.Add(barra, 0, 1);
            formularioOrden = new FlowLayoutPanel { Name = "panelNuevaOrden", Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Visible = false };
            var editor = FlujoHorizontal();
            AgregarCampo(editor, "Proveedor", cmbOrdenProveedor, 210);
            AgregarCampo(editor, "Perfume", cmbOrdenProducto, 210);
            AgregarCampo(editor, "Cantidad", nudOrdenCantidad, 85);
            AgregarCampo(editor, "Precio compra", nudOrdenPrecio, 105);
            agregarRenglon = Boton("Agregar producto", "btnAgregarRenglonOrden", delegate { Ejecutar(AgregarRenglonBorrador, null); });
            editor.Controls.Add(agregarRenglon);
            editor.Controls.Add(Boton("Otro perfume", "btnNuevoRenglonOrden", delegate { PrepararRenglon(); }));
            renglonesOrden.Dock = DockStyle.None; renglonesOrden.Visible = false; renglonesOrden.Height = 72;
            renglonesOrden.Columns.Add("IdProducto", "Id"); renglonesOrden.Columns[0].Visible = false;
            renglonesOrden.Columns.Add("Producto", "Perfume"); renglonesOrden.Columns.Add("Cantidad", "Cantidad");
            renglonesOrden.Columns.Add("PrecioUnitario", "Precio unitario"); renglonesOrden.Columns.Add("Subtotal", "Subtotal");
            renglonesOrden.Columns["Producto"].FillWeight = 230;
            foreach (string col in new[] { "PrecioUnitario", "Subtotal" }) renglonesOrden.Columns[col].DefaultCellStyle.Format = "C2";
            renglonesOrden.CellClick += delegate(object s, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0) EditarRenglonBorrador(renglonesOrden.Rows[e.RowIndex]); };
            renglonesOrden.CurrentCellChanged += delegate { if (!cargandoRenglon && renglonesOrden.CurrentRow != null) EditarRenglonBorrador(renglonesOrden.CurrentRow); };
            cmbOrdenProducto.SelectedIndexChanged += delegate {
                if (cargandoRenglon) return;
                var fila = renglonesOrden.Rows.Cast<DataGridViewRow>().FirstOrDefault(f => cmbOrdenProducto.SelectedValue is int && (int)f.Cells["IdProducto"].Value == (int)cmbOrdenProducto.SelectedValue);
                if (fila != null) EditarRenglonBorrador(fila);
                else { NumeroRegional.Restablecer(nudOrdenCantidad, 1); NumeroRegional.Restablecer(nudOrdenPrecio, 0); agregarRenglon.Text = "Agregar producto"; }
            };
            var accionesBorrador = FlujoHorizontal(); accionesBorrador.Controls.Add(totalBorrador);
            quitarRenglon = Boton("Quitar seleccionado", "btnQuitarRenglonOrden", delegate {
                cargandoRenglon = true;
                try { if (renglonesOrden.CurrentRow != null) renglonesOrden.Rows.Remove(renglonesOrden.CurrentRow); }
                finally { cargandoRenglon = false; }
                PrepararRenglon(); ActualizarBorrador();
            });
            quitarRenglon.Enabled = false; accionesBorrador.Controls.Add(quitarRenglon);
            accionesBorrador.Controls.Add(Boton("Crear borrador", "btnCrearOrden", CrearOrden));
            accionesBorrador.Controls.Add(Boton("Cancelar", "btnCancelarNuevaOrden", delegate { CancelarBorrador(); }));
            formularioOrden.Controls.Add(editor); formularioOrden.Controls.Add(renglonesOrden); formularioOrden.Controls.Add(accionesBorrador);
            formularioOrden.SizeChanged += delegate {
                editor.Width = accionesBorrador.Width = renglonesOrden.Width = Math.Max(300, formularioOrden.ClientSize.Width - 8);
            };
            ActualizarBorrador();
            layout.Controls.Add(formularioOrden, 0, 2);
            var archivos = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Padding = new Padding(5) };
            btnReporteOrdenesPdf = Boton("Reporte PDF", "btnReporteOrdenesPdf", GenerarReportePdf);
            btnExportarOrdenesXml = Boton("Exportar XML", "btnExportarOrdenesXml", ExportarOrdenesXml);
            var importar = Boton("Importar XML", "btnImportarOrdenesXml", ImportarOrdenesXml);
            var ejemplo = Boton("Sample XML", "btnSampleOrdenesXml", CopiarXmlEjemplo);
            foreach (Button boton in new[] { btnReporteOrdenesPdf, btnExportarOrdenesXml, importar, ejemplo })
            {
                boton.Margin = new Padding(3);
                archivos.Controls.Add(boton);
            }
            archivos.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(430, 0), Margin = new Padding(10, 9, 3, 3),
                Text = "PDF/XML: órdenes del listado filtrado. Importar crea borradores nuevos." });
            layout.Controls.Add(archivos, 0, 3);
            var filtros = FlujoHorizontal();
            filtroEstado.DisplayMember = "Texto";
            filtroEstado.Items.Add(new FiltroEstado { Texto = "Todos los estados" });
            foreach (EstadoOrdenCompra valor in Enum.GetValues(typeof(EstadoOrdenCompra)))
                filtroEstado.Items.Add(new FiltroEstado { Valor = valor, Texto = NombreEstado(valor) });
            filtroEstado.SelectedIndex = 0;
            AgregarCampo(filtros, "Proveedor", filtroProveedor, 200); AgregarCampo(filtros, "Estado", filtroEstado, 175);
            filtros.Controls.Add(filtrarFechas); AgregarCampo(filtros, "Desde", fechaDesde, 110); AgregarCampo(filtros, "Hasta", fechaHasta, 110);
            filtros.Controls.Add(Boton("Limpiar filtros", "btnLimpiarFiltrosOrdenes", delegate { LimpiarFiltrosOrdenes(); }));
            filtroProveedor.SelectedIndexChanged += delegate { AplicarFiltrosOrdenes(); };
            filtroEstado.SelectedIndexChanged += delegate { AplicarFiltrosOrdenes(); };
            filtrarFechas.CheckedChanged += delegate { fechaDesde.Enabled = fechaHasta.Enabled = filtrarFechas.Checked; AplicarFiltrosOrdenes(); };
            fechaDesde.ValueChanged += delegate { if (fechaDesde.Value.Date > fechaHasta.Value.Date) fechaHasta.Value = fechaDesde.Value; AplicarFiltrosOrdenes(); };
            fechaHasta.ValueChanged += delegate { if (fechaHasta.Value.Date < fechaDesde.Value.Date) fechaDesde.Value = fechaHasta.Value; AplicarFiltrosOrdenes(); };
            layout.Controls.Add(filtros, 0, 4); layout.Controls.Add(dgvOrdenes, 0, 5);
            desplazamiento.Controls.Add(layout); tabOrdenes.Controls.Add(desplazamiento);
        }

        private void ConstruirContabilidad()
        {
            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(12) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(Titulo("Revisión y aprobación presupuestaria por Contabilidad"), 0, 0);
            FlowLayoutPanel barra = FlujoHorizontal();
            barra.Controls.Add(Boton("Aprobar seleccionada", "btnAprobarOrden", AprobarOrden));
            AgregarCampo(barra, "Motivo de rechazo", txtMotivoRechazo, 330);
            barra.Controls.Add(Boton("Rechazar seleccionada", "btnRechazarOrden", RechazarOrden));
            barra.Controls.Add(Boton("Actualizar", "btnActualizarPendientes", delegate { RecargarTodo(); }));
            AgregarDetalleOrden(barra, dgvPendientes, "btnDetallePendiente");
            layout.Controls.Add(barra, 0, 1);
            layout.Controls.Add(dgvPendientes, 0, 2);
            tabContabilidad.Controls.Add(layout);
        }

        private void ConstruirRecepcion()
        {
            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 6, ColumnCount = 1, Padding = new Padding(12) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(Titulo("Recepción con lote, origen y trazabilidad"), 0, 0);
            FlowLayoutPanel recepcion = FlujoHorizontal();
            AgregarCampo(recepcion, "Orden emitida", cmbOrdenRecepcion, 180);
            cmbOrdenRecepcion.DropDownWidth = 520;
            cmbOrdenRecepcion.SelectedIndexChanged += CambiarOrdenRecepcion;
            AgregarCampo(recepcion, "Perfume", cmbRecepcionProducto, 200);
            cmbRecepcionProducto.SelectedIndexChanged += CambiarProductoRecepcion;
            AgregarCampo(recepcion, "Código lote", txtLoteCodigo, 110);
            AgregarCampo(recepcion, "Origen", txtLoteOrigen, 130);
            AgregarCampo(recepcion, "Cantidad", nudLoteCantidad, 80);
            AgregarCampo(recepcion, "Vencimiento", dtpLoteVencimiento, 135);
            registrarRecepcion = Boton("Registrar recepción", "btnRegistrarRecepcion", RegistrarRecepcion);
            recepcion.Controls.Add(registrarRecepcion);
            recepcion.Controls.Add(Boton("Actualizar", "btnActualizarRecepcion", delegate { RecargarTodo(); }));
            layout.Controls.Add(recepcion, 0, 1);
            layout.Controls.Add(saldoRecepcion, 0, 2);
            layout.Controls.Add(Titulo("Consulta de stock y trazabilidad"), 0, 3);
            FlowLayoutPanel consulta = FlujoHorizontal();
            AgregarCampo(consulta, "Perfume", cmbInventarioProducto, 240);
            consulta.Controls.Add(Boton("Consultar", "btnConsultarInventario", ConsultarInventario));
            consulta.Controls.Add(lblStock);
            layout.Controls.Add(consulta, 0, 4);
            layout.Controls.Add(dgvTrazabilidad, 0, 5);
            tabRecepcion.Controls.Add(layout);
        }

        private void ConfigurarAcceso()
        {
            bool gestiona = TienePermiso(TipoPermiso.Aprovisionamiento_Gestion);
            bool aprueba = TienePermiso(TipoPermiso.Contabilidad_AprobarCompras);
            bool recibe = TienePermiso(TipoPermiso.Inventario_Recepcionar);
            if (!gestiona) { tabs.TabPages.Remove(tabCatalogos); tabs.TabPages.Remove(tabOrdenes); }
            if (!aprueba) tabs.TabPages.Remove(tabContabilidad);
            if (!recibe) tabs.TabPages.Remove(tabRecepcion);
        }

        private bool TienePermiso(TipoPermiso permiso)
        {
            return usuario != null && usuario.TienePermiso(permiso);
        }

        private void RecargarTodo()
        {
            if (recargando) return;
            Ejecutar(delegate
            {
                recargando = true;
                try
                {
                    CargarCatalogos(); CargarOrdenes(); CargarPendientes(); CargarOrdenesRecepcion();
                    if (TienePermiso(TipoPermiso.Inventario_Recepcionar)) CargarInventario();
                    if (evaluacion != null) evaluacion.ActualizarCatalogos();
                    estadoCarga.Text = "Datos actualizados a las " + DateTime.Now.ToString("HH:mm:ss") + ".";
                }
                finally { recargando = false; }
            }, null);
        }

        private void CargarCatalogos()
        {
            List<Proveedor> proveedores = proveedorBLL.Listar(true);
            List<Producto> productos = productoBLL.Listar(true);
            Enlazar(cmbCotizacionProveedor, proveedores, "RazonSocial", "IdProveedor");
            Enlazar(cmbOrdenProveedor, proveedores.ToList(), "RazonSocial", "IdProveedor");
            Enlazar(cmbCotizacionProducto, productos, "Nombre", "IdProducto");
            var anterior = cmbOrdenProducto.SelectedItem as Producto;
            bool conservarVacio = cmbOrdenProducto.Items.Count > 0 && anterior == null;
            var opcionesOrden = productos.ToList();
            if (anterior != null && TieneBorradorPendiente && !opcionesOrden.Any(p => p.IdProducto == anterior.IdProducto)) opcionesOrden.Add(anterior);
            cargandoRenglon = true;
            try { Enlazar(cmbOrdenProducto, opcionesOrden, "Nombre", "IdProducto"); if (conservarVacio) cmbOrdenProducto.SelectedIndex = -1; }
            finally { cargandoRenglon = false; }
            Enlazar(cmbInventarioProducto, productos.ToList(), "Nombre", "IdProducto");
        }

        private static void Enlazar(ComboBox combo, object datos, string mostrar, string valor)
        {
            object anterior = combo.SelectedValue;
            combo.DataSource = null;
            combo.DisplayMember = mostrar;
            combo.ValueMember = valor;
            combo.DataSource = datos;
            if (anterior is int && combo.Items.Cast<object>().Any(item => Convert.ToInt32(item.GetType().GetProperty(valor).GetValue(item, null)) == (int)anterior))
                combo.SelectedValue = anterior;
        }

        private void CargarOrdenes()
        {
            if (!TienePermiso(TipoPermiso.Aprovisionamiento_Gestion)) return;
            ordenesCargadas = aprovisionamientoBLL.ConsultarHistorialCompras(null);
            cargandoFiltros = true;
            try {
                var opciones = proveedorBLL.Listar(false); opciones.Insert(0, new Proveedor { IdProveedor = 0, RazonSocial = "Todos los proveedores" });
                Enlazar(filtroProveedor, opciones, "RazonSocial", "IdProveedor");
            } finally { cargandoFiltros = false; }
            AplicarFiltrosOrdenes();
        }

        private void AplicarFiltrosOrdenes()
        {
            if (cargandoFiltros || enviarRevision == null) return;
            Ejecutar(delegate {
                var proveedor = filtroProveedor.SelectedItem as Proveedor; var estado = filtroEstado.SelectedItem as FiltroEstado;
                var filtradas = ordenesCargadas.Where(o => (proveedor == null || proveedor.IdProveedor == 0 || o.IdProveedor == proveedor.IdProveedor)
                    && (estado == null || !estado.Valor.HasValue || o.Estado == estado.Valor.Value)
                    && (!filtrarFechas.Checked || (o.FechaEmision.Date >= fechaDesde.Value.Date && o.FechaEmision.Date <= fechaHasta.Value.Date)));
                MostrarOrdenes(dgvOrdenes, filtradas);
                btnReporteOrdenesPdf.Enabled = btnExportarOrdenesXml.Enabled = dgvOrdenes.Rows.Count > 0;
                ActualizarAccionesOrden();
            }, null);
        }

        private void LimpiarFiltrosOrdenes()
        {
            cargandoFiltros = true;
            try { filtroProveedor.SelectedValue = 0; filtroEstado.SelectedIndex = 0; filtrarFechas.Checked = false; }
            finally { cargandoFiltros = false; }
            AplicarFiltrosOrdenes();
        }

        private void ActualizarAccionesOrden()
        {
            if (enviarRevision == null) return;
            var orden = Seleccion(dgvOrdenes);
            enviarRevision.Enabled = orden != null && orden.EstadoCodigo == EstadoOrdenCompra.Borrador;
            emitirProveedor.Enabled = orden != null && orden.EstadoCodigo == EstadoOrdenCompra.Aprobada;
            ayudaOrden.Text = orden == null ? "Seleccione una orden del listado." :
                orden.EstadoCodigo == EstadoOrdenCompra.Borrador ? "Borrador: puede enviarlo a Contabilidad." :
                orden.EstadoCodigo == EstadoOrdenCompra.Aprobada ? "Aprobada: puede emitirla al proveedor." :
                orden.EstadoCodigo == EstadoOrdenCompra.PendienteAprobacion ? "Pendiente: Contabilidad debe revisarla." :
                orden.EstadoCodigo == EstadoOrdenCompra.Emitida || orden.EstadoCodigo == EstadoOrdenCompra.RecibidaParcial ? "La recepción continúa en Recepción e inventario." : "Esta orden no admite envío ni emisión.";
        }

        private static string NombreEstado(EstadoOrdenCompra estado)
        {
            if (estado == EstadoOrdenCompra.PendienteAprobacion) return "Pendiente de aprobación";
            if (estado == EstadoOrdenCompra.RecibidaParcial) return "Recibida parcialmente";
            return estado.ToString();
        }
        private sealed class FiltroEstado { public EstadoOrdenCompra? Valor { get; set; } public string Texto { get; set; } }

        private void MostrarOrdenes(DataGridView grilla, IEnumerable<OrdenCompra> ordenes)
        {
            var seleccionada = Seleccion(grilla);
            // Include inactive catalog entries so historical orders keep their names.
            var proveedores = proveedorBLL.Listar(false).ToDictionary(p => p.IdProveedor);
            var productos = productoBLL.Listar(false).ToDictionary(p => p.IdProducto);
            grilla.DataSource = ordenes.Select(o => OrdenCompraVista.Crear(o, proveedores, productos)).ToList();
            grilla.Columns["EstadoCodigo"].Visible = false;
            grilla.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            grilla.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            grilla.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            grilla.Columns["IdOrdenCompra"].HeaderText = "Orden";
            grilla.Columns["IdOrdenCompra"].FillWeight = 50;
            grilla.Columns["Proveedor"].FillWeight = 140;
            grilla.Columns["Productos"].HeaderText = "Productos y cantidades";
            grilla.Columns["Productos"].FillWeight = 240;
            grilla.Columns["Fecha"].DefaultCellStyle.Format = "dd/MM/yyyy";
            grilla.Columns["Total"].DefaultCellStyle.Format = "N2";
            grilla.Columns["Total"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            grilla.Columns["CreadaPor"].HeaderText = "Responsable";
            grilla.Columns["Unidades"].HeaderText = "Cantidad total";
            grilla.Columns["Unidades"].FillWeight = 65;
            if (seleccionada != null)
                foreach (DataGridViewRow fila in grilla.Rows)
                    if (((OrdenCompraVista)fila.DataBoundItem).IdOrdenCompra == seleccionada.IdOrdenCompra)
                    { grilla.CurrentCell = fila.Cells[0]; break; }
        }

        private ArchivoOrdenesCompra ObtenerListadoGrilla()
        {
            var ids = dgvOrdenes.Rows.Cast<DataGridViewRow>().Where(f => !f.IsNewRow && f.Visible)
                .Select(f => ((OrdenCompraVista)f.DataBoundItem).IdOrdenCompra).ToList();
            return intercambioOrdenesBLL.ObtenerListado(ids);
        }

        private string ElegirArchivo(bool importar, bool pdf = false)
        {
            if (ModoPrueba)
            {
                if (string.IsNullOrWhiteSpace(RutaArchivoPrueba)) throw new InvalidOperationException("Debe indicar RutaArchivoPrueba.");
                return RutaArchivoPrueba;
            }
            if (importar)
            {
                using (var dialogo = new OpenFileDialog { Title = "Importar órdenes desde XML", Filter = "Órdenes XML (*.xml)|*.xml",
                    CheckFileExists = true, Multiselect = false })
                    return dialogo.ShowDialog(this) == DialogResult.OK ? dialogo.FileName : null;
            }
            using (var dialogo = new SaveFileDialog { Title = pdf ? "Guardar reporte de órdenes" : "Exportar listado de órdenes",
                Filter = pdf ? "Reporte PDF (*.pdf)|*.pdf" : "Órdenes XML (*.xml)|*.xml", DefaultExt = pdf ? "pdf" : "xml",
                AddExtension = true, CheckPathExists = true, OverwritePrompt = true,
                FileName = "ordenes_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + (pdf ? ".pdf" : ".xml") })
                return dialogo.ShowDialog(this) == DialogResult.OK ? dialogo.FileName : null;
        }

        private void GenerarReportePdf(object sender, EventArgs e)
        {
            Ejecutar(delegate
            {
                ArchivoOrdenesCompra archivo = ObtenerListadoGrilla();
                string ruta = ElegirArchivo(false, true);
                if (ruta == null) return;
                ReporteOrdenesPdf.Guardar(ruta, archivo);
                Informar("Reporte PDF guardado. Órdenes incluidas: " + archivo.Ordenes.Count + ".");
            }, null);
        }

        private void ExportarOrdenesXml(object sender, EventArgs e)
        {
            Ejecutar(delegate
            {
                ArchivoOrdenesCompra archivo = ObtenerListadoGrilla();
                string ruta = ElegirArchivo(false);
                if (ruta == null) return;
                OrdenesXml.Guardar(ruta, archivo);
                Informar("XML guardado con " + archivo.Ordenes.Count + " órdenes y sus detalles.");
            }, null);
        }

        private void ImportarOrdenesXml(object sender, EventArgs e)
        {
            Ejecutar(delegate
            {
                string ruta = ElegirArchivo(true);
                if (ruta == null) return;
                ArchivoOrdenesCompra archivo = OrdenesXml.Leer(ruta);
                int cantidad = intercambioOrdenesBLL.ValidarImportacion(archivo);
                bool confirmar = ModoPrueba ? ConfirmarImportacionPrueba : MessageBox.Show(this,
                    "Se crearán " + cantidad + " órdenes NUEVAS en Borrador, con fecha y responsable actuales.\n" +
                    "No se copian aprobaciones, recepciones ni stock y no se reemplazan órdenes existentes.\n" +
                    "Volver a importar el mismo archivo crea otras copias.\n\n¿Desea continuar?",
                    "Confirmar importación XML", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
                if (!confirmar) return;
                List<int> ids = intercambioOrdenesBLL.Importar(archivo);
                CargarOrdenes();
                Informar("Se importaron " + ids.Count + " órdenes como borradores. Nuevos IDs: " +
                    string.Join(", ", ids.Take(10)) + (ids.Count > 10 ? "..." : "") + ".");
            }, null);
        }

        private void CopiarXmlEjemplo(object sender, EventArgs e)
        {
            Ejecutar(delegate
            {
                string xml = OrdenesXml.Serializar(intercambioOrdenesBLL.CrearEjemplo());
                Clipboard.SetDataObject(xml, true, 5, 100);
                Informar("Se copió al portapapeles un XML con 3 órdenes de ejemplo y precios ilustrativos.\n" +
                    "Pegue el contenido en un archivo .xml guardado como UTF-8 y ábralo con Importar XML.\n" +
                    "Todavía no se creó ninguna orden.");
            }, null);
        }

        private void CargarPendientes()
        {
            if (!TienePermiso(TipoPermiso.Contabilidad_AprobarCompras)) return;
            MostrarOrdenes(dgvPendientes, aprovisionamientoBLL.ConsultarHistorialCompras(null)
                .Where(o => o.Estado == EstadoOrdenCompra.PendienteAprobacion));
        }

        private void CargarOrdenesRecepcion()
        {
            if (!TienePermiso(TipoPermiso.Inventario_Recepcionar)) return;
            var anterior = cmbOrdenRecepcion.SelectedItem as OrdenRecepcionVista;
            var todas = aprovisionamientoBLL.ConsultarHistorialCompras(null);
            List<OrdenCompra> ordenes = todas
                .Where(o => o.Estado == EstadoOrdenCompra.Emitida || o.Estado == EstadoOrdenCompra.RecibidaParcial)
                .ToList();
            // Un cambio externo no debe trasladar los datos preparados a la primera orden disponible.
            if (anterior != null && TieneRecepcionPendiente && !ordenes.Any(o => o.IdOrdenCompra == anterior.IdOrdenCompra))
                ordenes.Add(todas.FirstOrDefault(o => o.IdOrdenCompra == anterior.IdOrdenCompra) ?? anterior.Orden);
            var proveedores = proveedorBLL.Listar(false).ToDictionary(p => p.IdProveedor, p => p.RazonSocial);
            enlazandoRecepcion = true;
            try { Enlazar(cmbOrdenRecepcion, ordenes.Select(o => new OrdenRecepcionVista(o, proveedores.ContainsKey(o.IdProveedor) ? proveedores[o.IdProveedor] : "Proveedor no disponible")).ToList(), "Descripcion", "IdOrdenCompra"); }
            finally { enlazandoRecepcion = false; }
            CambiarOrdenRecepcion(null, EventArgs.Empty);
        }

        private void CambiarOrdenRecepcion(object sender, EventArgs e)
        {
            if (enlazandoRecepcion) return;
            OrdenRecepcionVista vista = cmbOrdenRecepcion.SelectedItem as OrdenRecepcionVista;
            int nueva = vista == null ? 0 : vista.IdOrdenCompra;
            if (nueva != ordenRecepcionActual && TieneRecepcionPendiente && !ConfirmarDescarte("la recepción preparada")) {
                enlazandoRecepcion = true;
                try { cmbOrdenRecepcion.SelectedValue = ordenRecepcionActual; } finally { enlazandoRecepcion = false; }
                ActualizarSaldoRecepcion(); return;
            }
            if (nueva != ordenRecepcionActual) LimpiarRecepcion();
            ordenRecepcionActual = nueva;
            List<Producto> productos = new List<Producto>();
            if (vista != null)
            {
                foreach (DetalleOrdenCompra detalle in vista.Orden.Detalles.Where(d => d.CantidadRecibida < d.Cantidad))
                {
                    Producto producto = productoBLL.Obtener(detalle.IdProducto);
                    if (producto != null) productos.Add(producto);
                }
            }
            if (vista != null && TieneRecepcionPendiente && productoRecepcionActual > 0 && !productos.Any(p => p.IdProducto == productoRecepcionActual)) {
                var anterior = productoBLL.Obtener(productoRecepcionActual);
                if (anterior != null && vista.Orden.Detalles.Any(d => d.IdProducto == anterior.IdProducto)) productos.Add(anterior);
            }
            enlazandoRecepcion = true;
            try { Enlazar(cmbRecepcionProducto, productos, "Nombre", "IdProducto"); }
            finally { enlazandoRecepcion = false; }
            productoRecepcionActual = cmbRecepcionProducto.SelectedValue is int ? (int)cmbRecepcionProducto.SelectedValue : 0;
            ActualizarSaldoRecepcion();
        }

        private void CambiarProductoRecepcion(object sender, EventArgs e)
        {
            if (enlazandoRecepcion) return;
            int nuevo = cmbRecepcionProducto.SelectedValue is int ? (int)cmbRecepcionProducto.SelectedValue : 0;
            if (nuevo != productoRecepcionActual && TieneRecepcionPendiente && !ConfirmarDescarte("la recepción preparada")) {
                enlazandoRecepcion = true;
                try { cmbRecepcionProducto.SelectedValue = productoRecepcionActual; } finally { enlazandoRecepcion = false; }
                ActualizarSaldoRecepcion(); return;
            }
            if (nuevo != productoRecepcionActual) LimpiarRecepcion();
            productoRecepcionActual = nuevo; ActualizarSaldoRecepcion();
        }

        private void LimpiarRecepcion()
        {
            txtLoteCodigo.Clear(); txtLoteOrigen.Clear(); NumeroRegional.Restablecer(nudLoteCantidad, 1);
            dtpLoteVencimiento.Value = vencimientoInicial; dtpLoteVencimiento.Checked = true;
        }

        private void ActualizarSaldoRecepcion()
        {
            var vista = cmbOrdenRecepcion.SelectedItem as OrdenRecepcionVista;
            var producto = cmbRecepcionProducto.SelectedItem as Producto;
            var detalle = vista == null || producto == null ? null : vista.Orden.Detalles.FirstOrDefault(d => d.IdProducto == producto.IdProducto);
            int saldo = detalle == null ? 0 : detalle.Cantidad - detalle.CantidadRecibida;
            // Una actualización externa puede reducir el saldo mientras se prepara la entrega.
            // Conservar el texto permite señalar el exceso en lugar de aceptar el recorte de NumericUpDown.
            string cantidadPreparada = TieneRecepcionPendiente ? nudLoteCantidad.Text : null;
            nudLoteCantidad.Maximum = Math.Max(1, saldo); nudLoteCantidad.Enabled = saldo > 0;
            if (cantidadPreparada != null && nudLoteCantidad.Text != cantidadPreparada) {
                nudLoteCantidad.Text = cantidadPreparada;
                ((NumeroRegional)nudLoteCantidad).ValidarEntrada();
            }
            if (registrarRecepcion != null) registrarRecepcion.Enabled = saldo > 0;
            saldoRecepcion.Text = detalle == null ? "No hay productos pendientes de recepción en la orden seleccionada." :
                vista.Proveedor + " · " + producto.Nombre + " · Pedidas: " + detalle.Cantidad + " u. · Recibidas: " + detalle.CantidadRecibida + " u. · Pendientes: " + saldo + " u.";
        }

        private void AgregarDetalleOrden(FlowLayoutPanel barra, DataGridView grilla, string nombre)
        {
            var boton = Boton("Ver detalle...", nombre, delegate { MostrarDetalleOrden(grilla); });
            boton.Enabled = false; barra.Controls.Add(boton);
            grilla.CurrentCellChanged += delegate { boton.Enabled = Seleccion(grilla) != null; };
            grilla.CellDoubleClick += delegate(object s, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0) MostrarDetalleOrden(grilla); };
        }

        private void MostrarDetalleOrden(DataGridView grilla)
        {
            var seleccion = Seleccion(grilla); if (seleccion == null) return;
            Ejecutar(delegate {
                var orden = aprovisionamientoBLL.ConsultarHistorialCompras(null).FirstOrDefault(o => o.IdOrdenCompra == seleccion.IdOrdenCompra);
                if (orden == null) throw new InvalidOperationException("La orden ya no está disponible. Actualice el listado.");
                var proveedor = proveedorBLL.Listar(false).FirstOrDefault(p => p.IdProveedor == orden.IdProveedor);
                var aprobador = orden.IdUsuarioAprobadorContable.HasValue ? new UsuarioBLL().ListarTodos().FirstOrDefault(u => u.Id == orden.IdUsuarioAprobadorContable) : null;
                using (var detalle = new FormDetalleOrdenCompra(orden, proveedor == null ? "Proveedor no disponible" : proveedor.RazonSocial,
                    productoBLL.Listar(false), aprobador == null ? null : aprobador.NombreCompleto + " · " + aprobador.Email)) detalle.ShowDialog(this);
            }, null);
        }

        private void AbrirOrden(int id)
        {
            tabs.SelectedTab = tabOrdenes; RecargarTodo(); LimpiarFiltrosOrdenes();
            foreach (DataGridViewRow fila in dgvOrdenes.Rows)
                if (((OrdenCompraVista)fila.DataBoundItem).IdOrdenCompra == id) {
                    dgvOrdenes.CurrentCell = fila.Cells["IdOrdenCompra"]; dgvOrdenes.FirstDisplayedScrollingRowIndex = fila.Index;
                    estadoCarga.Text = "Orden #" + id + " seleccionada. " + ayudaOrden.Text; return;
                }
            estadoCarga.Text = "La orden #" + id + " ya no está disponible en el listado.";
        }

        private void RegistrarCotizacion(object sender, EventArgs e)
        {
            Ejecutar(delegate
            {
                NumeroRegional.ExigirEntradas(nudCotizacionPrecio, nudCotizacionPlazo);
                CotizacionProveedor cotizacion = new CotizacionProveedor
                {
                    IdProveedor = ValorSeleccionado(cmbCotizacionProveedor),
                    FechaVigencia = dtpCotizacionVigencia.Value.Date,
                    Condiciones = txtCotizacionCondiciones.Text.Trim()
                };
                cotizacion.Detalles.Add(new DetalleCotizacionProveedor
                {
                    IdProducto = ValorSeleccionado(cmbCotizacionProducto),
                    PrecioUnitario = nudCotizacionPrecio.Value,
                    PlazoEntregaDias = Convert.ToInt32(nudCotizacionPlazo.Value)
                });
                aprovisionamientoBLL.RegistrarCotizacion(cotizacion);
                nudCotizacionPrecio.Value = 0; nudCotizacionPlazo.Value = 0; txtCotizacionCondiciones.Clear();
                RecargarTodo();
            }, "Cotización registrada correctamente.");
        }

        private void CrearOrden(object sender, EventArgs e)
        {
            Ejecutar(delegate
            {
                if (renglonesOrden.Rows.Count == 0 || nudOrdenPrecio.Value > 0 || nudOrdenCantidad.Value != 1 || EdicionNumerica(nudOrdenPrecio) || EdicionNumerica(nudOrdenCantidad))
                    AgregarRenglonBorrador();
                OrdenCompra orden = new OrdenCompra { IdProveedor = ValorSeleccionado(cmbOrdenProveedor) };
                var detalles = renglonesOrden.Rows.Cast<DataGridViewRow>().Select(f => new DetalleOrdenCompra {
                    IdProducto = (int)f.Cells["IdProducto"].Value, Cantidad = (int)f.Cells["Cantidad"].Value, PrecioUnitario = (decimal)f.Cells["PrecioUnitario"].Value
                }).ToList();
                aprovisionamientoBLL.CrearOrdenPreliminar(orden, detalles);
                LimpiarBorrador(); formularioOrden.Visible = false;
                try {
                    CargarOrdenes(); LimpiarFiltrosOrdenes();
                    foreach (DataGridViewRow fila in dgvOrdenes.Rows)
                        if (((OrdenCompraVista)fila.DataBoundItem).IdOrdenCompra == orden.IdOrdenCompra) { dgvOrdenes.CurrentCell = fila.Cells[0]; break; }
                    Informar("Borrador #" + orden.IdOrdenCompra + " creado. Puede enviarlo a Contabilidad.");
                } catch (Exception ex) { Informar("Borrador #" + orden.IdOrdenCompra + " creado. Actualice el listado: " + ex.Message); }
            }, null);
        }

        protected virtual bool ConfirmarDescarte(string contexto)
        {
            return MessageBox.Show(this, "Hay datos sin guardar en " + contexto + ". ¿Desea descartarlos y continuar?", "Datos sin guardar",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        private void NuevoBorrador()
        {
            formularioOrden.Visible = true;
            if (TieneBorradorPendiente && !ConfirmarDescarte("el borrador de compra")) return;
            LimpiarBorrador();
        }

        private void CancelarBorrador()
        {
            if (TieneBorradorPendiente && !ConfirmarDescarte("el borrador de compra")) return;
            LimpiarBorrador(); formularioOrden.Visible = false;
        }

        private void LimpiarBorrador()
        {
            cargandoRenglon = true;
            try { renglonesOrden.Rows.Clear(); } finally { cargandoRenglon = false; }
            PrepararRenglon();
            if (cmbOrdenProducto.Items.Count > 0) cmbOrdenProducto.SelectedIndex = 0;
            ActualizarBorrador();
        }

        private void PrepararRenglon()
        {
            cargandoRenglon = true;
            try { renglonesOrden.ClearSelection(); renglonesOrden.CurrentCell = null; cmbOrdenProducto.SelectedIndex = -1;
                NumeroRegional.Restablecer(nudOrdenPrecio, 0); NumeroRegional.Restablecer(nudOrdenCantidad, 1);
                agregarRenglon.Text = "Agregar producto";
                quitarRenglon.Enabled = false;
            } finally { cargandoRenglon = false; }
        }

        private void EditarRenglonBorrador(DataGridViewRow fila)
        {
            cargandoRenglon = true;
            try { renglonesOrden.CurrentCell = fila.Cells["Producto"]; fila.Selected = true;
                cmbOrdenProducto.SelectedValue = (int)fila.Cells["IdProducto"].Value;
                NumeroRegional.Restablecer(nudOrdenCantidad, (int)fila.Cells["Cantidad"].Value);
                NumeroRegional.Restablecer(nudOrdenPrecio, (decimal)fila.Cells["PrecioUnitario"].Value);
                agregarRenglon.Text = "Actualizar renglón";
                quitarRenglon.Enabled = true;
            } finally { cargandoRenglon = false; }
        }

        private void AgregarRenglonBorrador()
        {
            NumeroRegional.ExigirEntradas(nudOrdenCantidad, nudOrdenPrecio);
            ValorSeleccionado(cmbOrdenProveedor);
            var producto = cmbOrdenProducto.SelectedItem as Producto;
            if (producto == null) throw new InvalidOperationException("Seleccione un perfume.");
            if (nudOrdenPrecio.Value <= 0) throw new ArgumentException("El precio de compra debe ser mayor que cero.");
            int cantidad = (int)nudOrdenCantidad.Value; decimal precio = nudOrdenPrecio.Value;
            var fila = renglonesOrden.Rows.Cast<DataGridViewRow>().FirstOrDefault(f => (int)f.Cells["IdProducto"].Value == producto.IdProducto);
            cargandoRenglon = true;
            try {
                if (fila == null) renglonesOrden.Rows.Add(producto.IdProducto, producto.Nombre, cantidad, precio, cantidad * precio);
                else { fila.Cells["Cantidad"].Value = cantidad; fila.Cells["PrecioUnitario"].Value = precio; fila.Cells["Subtotal"].Value = cantidad * precio; }
            } finally { cargandoRenglon = false; }
            PrepararRenglon();
            ActualizarBorrador();
        }

        private void ActualizarBorrador()
        {
            int filas = renglonesOrden.Rows.Count;
            renglonesOrden.Visible = filas > 0;
            renglonesOrden.Height = Math.Min(120, 28 + filas * 24);
            cmbOrdenProveedor.Enabled = filas == 0;
            totalBorrador.Text = filas == 0 ? "Agregue los perfumes de esta compra." : filas + " producto(s) · Total: " +
                FormatoPresentacion.Moneda(renglonesOrden.Rows.Cast<DataGridViewRow>().Sum(f => (decimal)f.Cells["Subtotal"].Value)) + " · Proveedor fijado para este borrador.";
        }

        private void EnviarRevision(object sender, EventArgs e)
        {
            OrdenCompraVista orden = Seleccion(dgvOrdenes);
            if (orden == null) return;
            Ejecutar(delegate { aprovisionamientoBLL.EnviarOrdenARevisionContable(orden.IdOrdenCompra); CargarOrdenes(); }, "Orden enviada a Contabilidad.");
        }

        private void EmitirOrden(object sender, EventArgs e)
        {
            OrdenCompraVista orden = Seleccion(dgvOrdenes);
            if (orden == null) return;
            Ejecutar(delegate { aprovisionamientoBLL.EmitirOrdenAprobada(orden.IdOrdenCompra); CargarOrdenes(); CargarOrdenesRecepcion(); }, "Orden emitida al proveedor.");
        }

        private void AprobarOrden(object sender, EventArgs e)
        {
            OrdenCompraVista orden = Seleccion(dgvPendientes);
            if (orden == null) return;
            Ejecutar(delegate { aprovisionamientoBLL.AprobarOrdenCompra(orden.IdOrdenCompra, usuario.Id); CargarPendientes(); }, "Orden aprobada por Contabilidad.");
        }

        private void RechazarOrden(object sender, EventArgs e)
        {
            OrdenCompraVista orden = Seleccion(dgvPendientes);
            if (orden == null) return;
            Ejecutar(delegate { aprovisionamientoBLL.RechazarOrdenCompra(orden.IdOrdenCompra, txtMotivoRechazo.Text); CargarPendientes(); }, "Orden rechazada por Contabilidad.");
        }

        private void RegistrarRecepcion(object sender, EventArgs e)
        {
            OrdenRecepcionVista orden = cmbOrdenRecepcion.SelectedItem as OrdenRecepcionVista;
            if (orden == null) { MessageBox.Show("Seleccione una orden emitida."); return; }
            Ejecutar(delegate
            {
                NumeroRegional.ExigirEntradas(nudLoteCantidad);
                Lote lote = new Lote
                {
                    IdProducto = ValorSeleccionado(cmbRecepcionProducto),
                    CodigoLote = txtLoteCodigo.Text.Trim(),
                    Origen = txtLoteOrigen.Text.Trim(),
                    CantidadIngresada = Convert.ToInt32(nudLoteCantidad.Value),
                    FechaVencimiento = dtpLoteVencimiento.Checked ? (DateTime?)dtpLoteVencimiento.Value.Date : null
                };
                aprovisionamientoBLL.RecepcionarMercaderia(orden.IdOrdenCompra, new List<Lote> { lote });
                LimpiarRecepcion();
                try {
                    CargarOrdenesRecepcion(); CargarOrdenes(); cmbInventarioProducto.SelectedValue = lote.IdProducto; CargarInventario();
                    Informar("Recepción registrada. Stock y trazabilidad actualizados.");
                } catch (Exception ex) { Informar("Recepción registrada. Actualice la consulta de stock: " + ex.Message); }
            }, null);
        }

        private void ConsultarInventario(object sender, EventArgs e)
        {
            Ejecutar(CargarInventario, null);
        }

        private void CargarInventario()
        {
                if (cmbInventarioProducto.SelectedValue == null) { lblStock.Text = "Stock disponible: -"; dgvTrazabilidad.DataSource = null; return; }
                int idProducto = ValorSeleccionado(cmbInventarioProducto);
                lblStock.Text = "Stock disponible: " + inventarioBLL.VerificarStock(idProducto);
                dgvTrazabilidad.DataSource = inventarioBLL.ObtenerTrazabilidad(idProducto);
                if (dgvTrazabilidad.Columns.Contains("CertificadoOrigen"))
                    dgvTrazabilidad.Columns["CertificadoOrigen"].Visible = false;
        }

        private static int ValorSeleccionado(ComboBox combo)
        {
            if (combo.SelectedValue == null) throw new InvalidOperationException("Debe seleccionar un valor de la lista.");
            return Convert.ToInt32(combo.SelectedValue);
        }

        private static OrdenCompraVista Seleccion(DataGridView grilla)
        {
            return grilla.CurrentRow == null ? null : grilla.CurrentRow.DataBoundItem as OrdenCompraVista;
        }

        private void Ejecutar(Action accion, string mensaje)
        {
            try
            {
                UltimoError = null;
                UltimoMensaje = null;
                accion();
                if (!string.IsNullOrWhiteSpace(mensaje))
                {
                    Informar(mensaje);
                }
            }
            catch (Exception error)
            {
                MostrarError(error);
            }
        }

        private void MostrarError(Exception error)
        {
            UltimoError = error.Message;
            if (!ModoPrueba)
                MessageBox.Show(error.Message, "No se pudo completar la operación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void Informar(string mensaje)
        {
            UltimoMensaje = mensaje;
            estadoCarga.Text = mensaje;
            estadoCarga.ForeColor = TemaAplicacion.Texto;
        }

        private static FlowLayoutPanel FlujoHorizontal()
        {
            return new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Padding = new Padding(8) };
        }

        private static Label Titulo(string texto)
        {
            return new Label { Text = texto, Font = new Font(SystemFonts.DefaultFont.FontFamily, 14, FontStyle.Bold), AutoSize = true, Margin = new Padding(3, 3, 3, 10) };
        }

        private static Button Boton(string texto, string nombre, EventHandler evento)
        {
            Button boton = new Button { Text = texto, Name = nombre, AutoSize = true, Height = 32, Margin = new Padding(8, 24, 3, 3) };
            boton.Click += evento;
            return boton;
        }

        private static void AgregarCampo(FlowLayoutPanel panel, string etiqueta, Control control, int ancho)
        {
            control.AccessibleName = etiqueta;
            FlowLayoutPanel bloque = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Margin = new Padding(3) };
            bloque.Controls.Add(new Label { Text = etiqueta, AutoSize = true });
            control.Width = ancho;
            bloque.Controls.Add(control);
            panel.Controls.Add(bloque);
        }

        private sealed class OrdenCompraVista
        {
            public int IdOrdenCompra { get; set; }
            public string Proveedor { get; set; }
            public string Productos { get; set; }
            public DateTime Fecha { get; set; }
            public decimal Total { get; set; }
            public string Estado { get; set; }
            public EstadoOrdenCompra EstadoCodigo { get; set; }
            public string CreadaPor { get; set; }
            public int Unidades { get; set; }

            public static OrdenCompraVista Crear(OrdenCompra orden, IDictionary<int, Proveedor> proveedores, IDictionary<int, Producto> productos)
            {
                Proveedor proveedor;
                proveedores.TryGetValue(orden.IdProveedor, out proveedor);
                return new OrdenCompraVista
                {
                    IdOrdenCompra = orden.IdOrdenCompra,
                    Proveedor = proveedor != null ? proveedor.RazonSocial : "Proveedor no disponible",
                    Productos = string.Join(Environment.NewLine, orden.Detalles.Select(d =>
                    {
                        Producto producto;
                        productos.TryGetValue(d.IdProducto, out producto);
                        string nombre = producto != null ? producto.Nombre : "Producto no disponible";
                        return nombre + " × " + d.Cantidad;
                    })),
                    Fecha = orden.FechaEmision,
                    Total = orden.Total,
                    Estado = NombreEstado(orden.Estado), EstadoCodigo = orden.Estado,
                    CreadaPor = orden.CreadaPor,
                    Unidades = orden.Detalles.Sum(d => d.Cantidad)
                };
            }
        }

        private sealed class OrdenRecepcionVista
        {
            public OrdenCompra Orden { get; private set; }
            public int IdOrdenCompra { get { return Orden.IdOrdenCompra; } }
            public string Proveedor { get; private set; }
            public string Descripcion { get { return "Orden " + Orden.IdOrdenCompra + " · " + Proveedor + " · " + NombreEstado(Orden.Estado); } }

            public OrdenRecepcionVista(OrdenCompra orden, string proveedor)
            {
                Orden = orden; Proveedor = proveedor;
            }
        }
    }
}
