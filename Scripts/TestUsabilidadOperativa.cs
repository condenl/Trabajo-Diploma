// Component/integration checks exclusively against a fresh Perfumeria_Usabilidad_Test_* database.
using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using BE;
using BE.Seguridad;
using BLL;
using Servicios;
using UI;
using UI.UserControls;

internal static class TestUsabilidadOperativa
{
    private static int checks;
    private static SqlConnection cn;
    private static T Find<T>(Control root, string name) where T : Control { return (T)root.Controls.Find(name, true).Single(); }
    private static void Assert(bool ok, string text) { if (!ok) throw new Exception(text); checks++; Console.WriteLine("PASS " + text); }
    private static void Click(Control root, string name) {
        var b = Find<Button>(root, name);
        if (!b.Enabled) throw new Exception("Button disabled: " + name);
        typeof(Button).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(b, new object[] { EventArgs.Empty }); Application.DoEvents();
    }
    private static DataTable Query(string sql) {
        using (var cmd = new SqlCommand(sql, cn)) using (var da = new SqlDataAdapter(cmd)) { var t = new DataTable(); da.Fill(t); return t; }
    }
    private static int Count(string table) { return Convert.ToInt32(Query("SELECT COUNT(*) FROM dbo.[" + table + "]").Rows[0][0]); }
    private static object Field(object value, string name) { return value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value); }
    private static void Host(Form host, Control view) { foreach (Control old in host.Controls.Cast<Control>().ToArray()) old.Dispose(); host.Controls.Add(view); TemaAplicacion.Aplicar(view); host.Show(); Application.DoEvents(); }
    private static void SelectOrder(DataGridView grid, EstadoOrdenCompra state) {
        var row = grid.Rows.Cast<DataGridViewRow>().First(r => (EstadoOrdenCompra)r.Cells["EstadoCodigo"].Value == state);
        grid.CurrentCell = row.Cells["IdOrdenCompra"]; Application.DoEvents();
    }
    private static Usuario Admin(Usuario actor) {
        var admin = (Usuario)actor.Clone(); admin.Permisos = actor.Permisos.ToList();
        foreach (TipoPermiso p in new[] { TipoPermiso.Usuarios_Ver, TipoPermiso.Usuarios_Alta, TipoPermiso.Usuarios_Modificar, TipoPermiso.Usuarios_Baja, TipoPermiso.Roles_Gestion,
            TipoPermiso.Auditoria_Ver, TipoPermiso.Auditoria_Restaurar, TipoPermiso.Soporte_Gestion })
            admin.Permisos.Add(new Patente { Nombre = p.ToString(), PermisoAsociado = p });
        return admin;
    }
    private sealed class MenuPrueba : FormPrincipal {
        public bool Descartar; public int Consultas;
        public MenuPrueba(Usuario actor) : base(actor) { }
        protected override bool ConfirmarDescartePendiente() { Consultas++; return Descartar; }
    }
    private sealed class ComprasPrueba : UC_Aprovisionamiento {
        public bool Descartar; public int Consultas;
        public ComprasPrueba(Usuario actor) : base(actor) { ModoPrueba = true; }
        protected override bool ConfirmarDescarte(string contexto) { Consultas++; return Descartar; }
    }
    private sealed class VentaPrueba : UC_Ventas {
        public bool Descartar; public int Consultas;
        protected override bool ConfirmarDescarteVenta() { Consultas++; return Descartar; }
    }
    private static void ComprobarNuevaVenta(Form host)
    {
        var vista = new VentaPrueba { ModoPrueba=true }; Host(host,vista);
        var producto=Find<ComboBox>(vista,"cmbProducto"); var cantidad=Find<NumericUpDown>(vista,"nudCantidad");
        cantidad.Value=7; producto.SelectedIndex=(producto.SelectedIndex+1)%producto.Items.Count;
        Assert(cantidad.Value==1,"Elegir otro perfume reinicia la cantidad de venta en uno");
        cantidad.Value=3; Click(vista,"btnActualizarVenta"); Assert(cantidad.Value==3,"Actualizar conserva la cantidad si conserva el perfume");
        producto.SelectedValue=producto.Items.Cast<Producto>().Single(p=>p.Codigo=="DEMO-FLORAL").IdProducto;
        Click(vista,"btnAgregarProducto"); var carrito=Find<DataGridView>(vista,"dgvVenta");
        var correo=Find<TextBox>(vista,"txtEmail"); correo.Text="test.venta@example.com"; Click(vista,"btnBuscarCliente");
        cantidad.Text="invalida"; int ventas=Count("Venta"), movimientos=Count("MovimientoStock");
        Click(vista,"btnNuevaVenta");
        Assert(vista.Consultas==1 && carrito.Rows.Count==1 && correo.Text!="" && cantidad.Text=="invalida", "Cancelar Nueva venta conserva carrito, cliente y edicion pendiente");
        vista.Descartar=true; Click(vista,"btnNuevaVenta");
        Assert(carrito.Rows.Count==0 && correo.Text=="" && cantidad.Value==1 && ((NumeroRegional)cantidad).ErrorEntrada==null && !vista.TieneCambiosPendientes && !Find<Button>(vista,"btnConfirmarVenta").Enabled,
            "Nueva venta confirmada limpia carrito, identidad, cantidad, errores e importes");
        Assert(Count("Venta")==ventas && Count("MovimientoStock")==movimientos,"Descartar una venta no escribe ventas ni movimientos");
        int preguntas=vista.Consultas; Click(vista,"btnNuevaVenta"); Assert(vista.Consultas==preguntas,"Una venta vacia no pide confirmar el descarte");
        correo.Text="correo.sin.buscar@example.com"; vista.Descartar=false; Click(vista,"btnNuevaVenta");
        Assert(vista.Consultas==preguntas+1 && correo.Text!="", "El correo preparado sin carrito tambien se protege al iniciar una venta");
        vista.Descartar=true; Click(vista,"btnNuevaVenta");
    }
    private static void ComprobarEspacioOperativo(Form host, Usuario owner)
    {
        Size anterior=host.ClientSize;
        foreach(var size in new[]{new Size(810,430),new Size(1320,650)}) {
            host.ClientSize=size; var vista=new UC_Aprovisionamiento(owner){ModoPrueba=true}; Host(host,vista);
            var tabs=vista.Controls.OfType<TabControl>().Single(); tabs.SelectedTab=tabs.TabPages.Cast<TabPage>().Single(p=>p.Text=="Órdenes de compra");
            Click(vista,"btnNuevoBorrador");
            for(int i=0;i<3;i++){Find<ComboBox>(vista,"cmbOrdenProducto").SelectedIndex=i; Find<NumericUpDown>(vista,"nudOrdenPrecio").Value=100;Click(vista,"btnAgregarRenglonOrden");}
            var ordenes=Find<DataGridView>(vista,"dgvOrdenes"); var scroll=Find<Panel>(vista,"scrollOrdenes");
            Assert(ordenes.Height>=180 && scroll.VerticalScroll.Visible,"Compras conserva una grilla util y desplazamiento con tres perfumes: "+size);
            scroll.ScrollControlIntoView(ordenes); Application.DoEvents();
            Assert(scroll.ClientRectangle.IntersectsWith(scroll.RectangleToClient(ordenes.RectangleToScreen(ordenes.ClientRectangle))),"Se puede alcanzar el listado de compras mediante desplazamiento: "+size);
            tabs.SelectedTab=tabs.TabPages.Cast<TabPage>().Single(p=>p.Text=="Evaluación de proveedores");
            Click(vista,"btnCriteriosEvaluacion");Click(vista,"btnHistorialEvaluacion");
            var resultados=Find<TabControl>(vista,"tabsResultadosEvaluacion");var scrollN03=Find<Panel>(vista,"scrollEvaluacion");
            Assert(resultados.Height>=250,"Los resultados N03 no colapsan al abrir criterios e historial: "+size);
            var confirmar=Find<Button>(vista,"btnConfirmarProveedor");scrollN03.ScrollControlIntoView(confirmar);Application.DoEvents();
            Assert(scrollN03.ClientRectangle.Contains(scrollN03.RectangleToClient(confirmar.RectangleToScreen(confirmar.ClientRectangle))),"Los botones de decision N03 son alcanzables: "+size);
        }
        host.ClientSize=anterior;
    }
    private static void ComprobarPreparacionCompras(Form host, Usuario owner)
    {
        var compras = new ComprasPrueba(owner); Host(host, compras);
        var tabs = compras.Controls.OfType<TabControl>().Single();
        tabs.SelectedTab = tabs.TabPages.Cast<TabPage>().Single(t => t.Text == "Órdenes de compra");
        var perfume = Find<ComboBox>(compras,"cmbOrdenProducto");
        var proveedor = Find<ComboBox>(compras,"cmbOrdenProveedor");
        var cantidad = Find<NumericUpDown>(compras,"nudOrdenCantidad");
        var precio = Find<NumericUpDown>(compras,"nudOrdenPrecio");
        var filas = Find<DataGridView>(compras,"dgvRenglonesOrden");
        var productos = perfume.Items.Cast<Producto>().Take(2).ToArray();
        int antes = Count("OrdenCompra"); Click(compras,"btnNuevoBorrador");
        precio.Text = "12000.50"; Click(compras,"btnCrearOrden");
        Assert(Count("OrdenCompra")==antes && filas.Rows.Count==0 && (compras.UltimoError ?? "").Contains("Formato"), "Un importe ambiguo no crea ni agrega una linea de compra");
        precio.Text="12.000,50"; cantidad.Value=2; perfume.SelectedValue=productos[0].IdProducto;
        Click(compras,"btnAgregarRenglonOrden");
        Assert(filas.Rows.Count==1 && !proveedor.Enabled && compras.TieneCambiosPendientes, "Agregar perfume conserva un borrador pendiente y fija el proveedor");
        Find<FlowLayoutPanel>(compras,"panelNuevaOrden").Visible=false;
        Assert(compras.TieneCambiosPendientes, "Ocultar el editor no oculta sus cambios pendientes");
        Click(compras,"btnNuevoBorrador");
        Assert(compras.Consultas==1 && filas.Rows.Count==1, "Cancelar el descarte al pedir Nuevo conserva el borrador");
        Click(compras,"btnCancelarNuevaOrden");
        Assert(compras.Consultas==2 && filas.Rows.Count==1, "Cancelar el descarte al cerrar conserva las lineas preparadas");
        compras.Descartar=true; Click(compras,"btnCancelarNuevaOrden"); Click(compras,"btnNuevoBorrador");
        Assert(filas.Rows.Count==0 && cantidad.Value==1 && precio.Value==0 && proveedor.Enabled && !compras.TieneCambiosPendientes, "Confirmar descarte deja un nuevo borrador completamente limpio");
        perfume.SelectedValue=productos[0].IdProducto; cantidad.Value=2; precio.Text="100,50"; Click(compras,"btnAgregarRenglonOrden");
        perfume.SelectedValue=productos[1].IdProducto; cantidad.Value=3; precio.Text="200,00"; Click(compras,"btnAgregarRenglonOrden");
        Assert(filas.Rows.Count==2 && filas.Rows.Cast<DataGridViewRow>().Sum(f=>(decimal)f.Cells["Subtotal"].Value)==801m, "Dos perfumes tienen cantidades, precios y total calculados por renglon");
        filas.CurrentCell=filas.Rows[1].Cells["Producto"]; filas.CurrentCell=filas.Rows[0].Cells["Producto"];
        Assert((int)perfume.SelectedValue==productos[0].IdProducto && cantidad.Value==2 && precio.Value==100.50m && Find<Button>(compras,"btnAgregarRenglonOrden").Text=="Actualizar renglón",
            "Seleccionar un renglon recupera perfume, cantidad, precio y modo de actualizacion");
        Click(compras,"btnActualizarOrdenes");
        Assert((int)perfume.SelectedValue==productos[0].IdProducto && cantidad.Value==2 && precio.Value==100.50m,"Actualizar catalogos conserva el renglon que se estaba corrigiendo");
        perfume.SelectedValue=productos[0].IdProducto;
        Assert(filas.CurrentRow!=null && (int)filas.CurrentRow.Cells["IdProducto"].Value==productos[0].IdProducto && Find<Button>(compras,"btnQuitarRenglonOrden").Enabled,
            "Elegir un perfume ya agregado selecciona su renglon y habilita quitarlo");
        cantidad.Value=4; precio.Text="100,50"; Click(compras,"btnAgregarRenglonOrden");
        Assert(filas.Rows.Count==2 && (int)filas.Rows[0].Cells["Cantidad"].Value==4, "Actualizar un perfume reemplaza su renglon sin duplicarlo");
        Assert(perfume.SelectedIndex==-1 && cantidad.Value==1 && precio.Value==0 && Find<Button>(compras,"btnAgregarRenglonOrden").Text=="Agregar producto","Guardar el renglon prepara otro perfume sin arrastrar sus valores");
        Assert(!Find<Button>(compras,"btnQuitarRenglonOrden").Enabled && filas.CurrentRow==null,"Sin fila seleccionada no se permite quitar un renglon anterior");
        filas.CurrentCell=filas.Rows[1].Cells["Producto"]; Click(compras,"btnQuitarRenglonOrden");
        Assert(filas.Rows.Count==1, "Quitar seleccionado retira solo el perfume elegido");
        perfume.SelectedValue=productos[1].IdProducto; cantidad.Value=3; precio.Text="200,00";
        Click(compras,"btnCrearOrden");
        var bll=new AprovisionamientoBLL(); var orden=bll.ConsultarHistorialCompras(null).OrderByDescending(o=>o.IdOrdenCompra).First();
        Assert(Count("OrdenCompra")==antes+1 && orden.Detalles.Count==2 && orden.Total==1002m && !compras.TieneCambiosPendientes,
            "Crear incluye la ultima linea escrita y persiste una sola orden con los dos perfumes");
        foreach(var ofertante in new ProveedorBLL().Listar(true).Take(2)) {
            var oferta=new CotizacionProveedor { IdProveedor=ofertante.IdProveedor, FechaVigencia=DateTime.Today.AddDays(30) };
            foreach(var p in productos) oferta.Detalles.Add(new DetalleCotizacionProveedor { IdProducto=p.IdProducto, PrecioUnitario=100, PlazoEntregaDias=3 });
            bll.RegistrarCotizacion(oferta);
        }
        bll.EnviarOrdenARevisionContable(orden.IdOrdenCompra);
        var contable=new UsuarioBLL().Login("demo.contabilidad@perfumeria.example","DemoContabilidad2026!");
        bll.AprobarOrdenCompra(orden.IdOrdenCompra,contable.Id);
        Session.Instance.Login(owner); bll.EmitirOrdenAprobada(orden.IdOrdenCompra);
        tabs.SelectedTab=tabs.TabPages.Cast<TabPage>().Single(t=>t.Text=="Recepción e inventario"); Click(compras,"btnActualizarRecepcion");
        var recepcion=Find<ComboBox>(compras,"cmbOrdenRecepcion"); recepcion.SelectedValue=orden.IdOrdenCompra;
        var recibido=Find<ComboBox>(compras,"cmbRecepcionProducto"); recibido.SelectedValue=productos[0].IdProducto;
        var lote=Find<TextBox>(compras,"txtLoteCodigo"); var origen=Find<TextBox>(compras,"txtLoteOrigen");
        var unidades=Find<NumericUpDown>(compras,"nudLoteCantidad"); var vence=Find<DateTimePicker>(compras,"dtpLoteVencimiento");
        unidades.Value=2; compras.Descartar=false; int preguntas=compras.Consultas;
        recibido.SelectedValue=productos[1].IdProducto;
        Assert((int)recibido.SelectedValue==productos[0].IdProducto && unidades.Value==2 && compras.Consultas==preguntas+1, "Cambiar solo cantidad ya protege la recepcion al elegir otro perfume");
        lote.Text="TEST-RECEPCION-SEGURA"; origen.Text="Argentina";
        int otra=recepcion.Items.Cast<object>().Select(o=>(int)o.GetType().GetProperty("IdOrdenCompra").GetValue(o,null)).First(id=>id!=orden.IdOrdenCompra);
        recepcion.SelectedValue=otra;
        Assert((int)recepcion.SelectedValue==orden.IdOrdenCompra && lote.Text=="TEST-RECEPCION-SEGURA" && origen.Text=="Argentina", "Rechazar cambio de orden conserva identidad, lote y origen");
        Click(compras,"btnActualizarRecepcion");
        Assert((int)recepcion.SelectedValue==orden.IdOrdenCompra && (int)recibido.SelectedValue==productos[0].IdProducto && lote.Text=="TEST-RECEPCION-SEGURA", "Actualizar conserva la recepcion preparada en su orden y perfume");
        compras.Descartar=true; recibido.SelectedValue=productos[1].IdProducto;
        Assert(lote.Text=="" && origen.Text=="" && unidades.Value==1, "Confirmar cambio de perfume limpia la preparacion anterior");
        vence.Value=vence.Value.AddDays(1); preguntas=compras.Consultas; compras.Descartar=false; recepcion.SelectedValue=otra;
        Assert((int)recepcion.SelectedValue==orden.IdOrdenCompra && compras.Consultas==preguntas+1, "Cambiar solo vencimiento tambien requiere confirmar el descarte");
        compras.Descartar=true; recepcion.SelectedValue=otra; recepcion.SelectedValue=orden.IdOrdenCompra; recibido.SelectedValue=productos[0].IdProducto;
        lote.Text="TEST-RECEPCION-SEGURA"; origen.Text="Argentina";
        int stock=new InventarioBLL().VerificarStock(productos[0].IdProducto);
        int lotes=Query("SELECT * FROM Lote WHERE IdProducto="+productos[0].IdProducto).Rows.Count;
        Click(compras,"btnRegistrarRecepcion");
        Assert(new InventarioBLL().VerificarStock(productos[0].IdProducto)==stock+1 && Find<Label>(compras,"lblStock").Text.EndsWith((stock+1).ToString()) &&
            Find<DataGridView>(compras,"dgvTrazabilidad").Rows.Count==lotes+1 && lote.Text=="" && !compras.TieneCambiosPendientes,
            "Registrar recepcion actualiza stock y trazabilidad visibles y limpia la preparacion");
        unidades.Value=3; lote.Text="TEST-PREPARADO"; origen.Text="Argentina";
        bll.RecepcionarMercaderia(orden.IdOrdenCompra,new System.Collections.Generic.List<Lote> {
            new Lote { IdProducto=productos[0].IdProducto, CodigoLote="TEST-EXTERNO", Origen="Argentina", CantidadIngresada=2, FechaVencimiento=DateTime.Today.AddYears(1) }
        });
        Click(compras,"btnActualizarRecepcion"); int ingresos=Count("Lote");
        Assert(unidades.Text=="3" && ((NumeroRegional)unidades).ErrorEntrada!=null && (int)recibido.SelectedValue==productos[0].IdProducto,
            "Si otra recepcion reduce el saldo, Actualizar conserva el texto preparado y senala el exceso sin recortarlo");
        Click(compras,"btnRegistrarRecepcion");
        Assert(Count("Lote")==ingresos && (compras.UltimoError ?? "").Contains("entre"), "Una cantidad preparada que supera el nuevo saldo no registra otro lote");
    }
    private static void Denegado(Action accion, string texto)
    { bool rechazo = false; try { accion(); } catch (UnauthorizedAccessException) { rechazo = true; } Assert(rechazo, texto); }
    private static void ComprobarRestricciones(FormPrincipal menu, string actor)
    {
        Assert(!Find<Button>(menu, "btnAuditoria").Visible && !Find<Button>(menu, "btnSoporteTecnico").Visible, "Menu administrativo oculto para " + actor);
        Assert(menu.Text.EndsWith(Session.Instance.Usuario.NombreCompleto) && !menu.Text.Contains("Correo electrónico:"), "El titulo identifica al actor sin reutilizar la etiqueta de correo: " + actor);
        Denegado(delegate { new AuditoriaBLL().ListarHistorial(); }, actor + " no consulta auditoria por BLL");
        Denegado(delegate { new AuditoriaBLL().HacerRollback(null); }, actor + " no restaura por BLL");
        Denegado(delegate { new SoporteBLL().ListarSolicitudes(); }, actor + " no lista solicitudes administrativas");
        Denegado(delegate { new SoporteBLL().ModificarSolicitudPorAdmin(null); }, actor + " no modifica solicitudes administrativas");
        Denegado(delegate { using (var vista = new UC_AuditoriaUsuarios()) { } }, actor + " no fuerza la apertura de auditoria");
        Denegado(delegate { using (var vista = new UC_SoporteAdmin()) { } }, actor + " no fuerza la apertura de soporte administrativo");
    }
    private static void ComprobarMigracion()
    {
        Assert(Query("SELECT Id FROM Seguridad_Componente WHERE PermisoAsociado IN ('Auditoria_Ver','Auditoria_Restaurar','Soporte_Gestion')").Rows.Count == 3, "Migracion crea tres permisos administrativos");
        var idiomas = new IdiomaBLL().Listar(); var traducciones = new TraduccionBLL();
        Assert(traducciones.ObtenerTraduccion(idiomas.First(i => i.Nombre == "Español"), "login_title").Texto == "Perfumería" &&
            traducciones.ObtenerTraduccion(idiomas.First(i => i.Nombre == "English"), "login_title").Texto == "Perfumery", "La distribucion identifica Perfumeria en ambos idiomas");
        var antes = Query("SELECT COUNT(*) AS Cantidad FROM Seguridad_Familia_Hijo").Rows[0][0];
        string dvv = Convert.ToString(Query("SELECT Valor FROM DVV WHERE NombreEntidad='Usuario'").Rows[0][0]);
        Exec("UPDATE Traduccion SET Texto=N'Acceso personalizado de prueba' WHERE NombreEtiqueta='login_title' AND IdIdioma IN (SELECT Id FROM Idioma WHERE Nombre='English')");
        string sql = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Iteracion3_Pulido_Demo.sql"));
        for (int repeticion=0; repeticion<2; repeticion++) foreach (string batch in System.Text.RegularExpressions.Regex.Split(sql, @"(?im)^\s*GO\s*(?:--.*)?$")) if (!string.IsNullOrWhiteSpace(batch)) Exec(batch);
        Assert(Convert.ToInt32(antes) == Convert.ToInt32(Query("SELECT COUNT(*) FROM Seguridad_Familia_Hijo").Rows[0][0]) &&
            dvv == Convert.ToString(Query("SELECT Valor FROM DVV WHERE NombreEntidad='Usuario'").Rows[0][0]) && new UsuarioBLL().VerificarIntegridad().Count==0,
            "Repetir la migracion no duplica permisos ni altera usuarios o integridad");
        Assert(traducciones.ObtenerTraduccion(idiomas.First(i => i.Nombre == "English"), "login_title").Texto == "Acceso personalizado de prueba", "La migracion conserva traducciones personalizadas");
        Exec("UPDATE Traduccion SET Texto=N'Perfumery' WHERE NombreEtiqueta='login_title' AND IdIdioma IN (SELECT Id FROM Idioma WHERE Nombre='English')");
        var rol = new SeguridadBLL().ObtenerRolesDisponibles().Single(r => r.Nombre == "Administrador");
        var completo = new SeguridadBLL().ObtenerFamiliaCompleta(rol.Id);
        Assert(completo.TienePermiso(TipoPermiso.Auditoria_Ver) && completo.TienePermiso(TipoPermiso.Auditoria_Restaurar) && completo.TienePermiso(TipoPermiso.Soporte_Gestion),
            "El rol Administrador conserva las funciones administrativas");
    }
    private static void ComprobarSoporte(Usuario owner)
    {
        new UsuarioBLL().Logout(); var soporte = new SoporteBLL();
        Denegado(delegate { new AuditoriaBLL().ListarHistorial(); }, "Sin sesion no se consulta auditoria");
        Denegado(delegate { new AuditoriaBLL().HacerRollback(null); }, "Sin sesion no se restaura auditoria");
        Denegado(delegate { soporte.ListarSolicitudes(); }, "Sin sesion no se listan tickets administrativos");
        Denegado(delegate { soporte.ModificarSolicitudPorAdmin(null); }, "Sin sesion no se administran tickets");
        soporte.CrearSolicitud(new BE.Soporte.SolicitudSoporte { EmailSolicitante="test.soporte@example.com", TipoProblema=soporte.ObtenerTipos().First(), Descripcion="Solicitud de ayuda previa al acceso." });
        Session.Instance.Login(Admin(owner));
        var solicitud = soporte.ListarSolicitudes().Single(s => s.EmailSolicitante == "test.soporte@example.com");
        solicitud.Estado = soporte.ObtenerEstados().Last(); solicitud.ObservacionesAdmin = "Gestion administrativa de prueba.";
        soporte.ModificarSolicitudPorAdmin(solicitud);
        Assert(soporte.ListarSolicitudes().Single(s => s.Id==solicitud.Id).ObservacionesAdmin==solicitud.ObservacionesAdmin, "Administrador puede gestionar el ticket creado sin sesion");
        using (var menu = new FormPrincipal(Session.Instance.Usuario)) {
            menu.Show(); Application.DoEvents();
            Assert(Find<Button>(menu,"btnAuditoria").Visible && Find<Button>(menu,"btnSoporteTecnico").Visible, "Los permisos especificos habilitan ambos menus administrativos");
            menu.Close();
        }
    }
    private static void Exec(string sql) { using (var cmd = new SqlCommand(sql, cn)) cmd.ExecuteNonQuery(); }
    private static void ComprobarFormatoNumerico()
    {
        var anterior = System.Globalization.CultureInfo.CurrentCulture;
        try {
            foreach (string cultura in new[] { "en-US", "es-AR" }) {
                System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(cultura);
                using (var numero = new NumeroRegional { Maximum=100000, DecimalPlaces=2, ThousandsSeparator=true }) {
                    numero.Value=1234.56m;
                    Assert(numero.Text=="1.234,56", "Editor monetario uniforme con cultura " + cultura);
                    numero.Text="1.234,56"; Assert(numero.Value==1234.56m, "El editor interpreta separadores argentinos con cultura " + cultura);
                    numero.Text="9,50"; numero.UpButton(); Assert(numero.Value==10.50m, "Incrementar conserva el decimal con cultura " + cultura);
                    numero.Text="no numerico"; Assert(!numero.ValidarEntrada() && numero.Value==10.50m && numero.Text=="no numerico", "Entrada invalida conserva texto y ultimo valor con cultura " + cultura);
                    numero.Maximum=1000; numero.Text="999999"; Assert(!numero.ValidarEntrada() && numero.Value==10.50m && numero.Text=="999999", "El editor rechaza el exceso sin recortarlo al maximo con cultura " + cultura);
                    numero.Minimum=5; numero.Text="-100"; Assert(!numero.ValidarEntrada() && numero.Value==10.50m && numero.Text=="-100", "El editor rechaza valores menores al minimo sin sustituirlos con cultura " + cultura);
                    numero.Text="12000.50"; Assert(!numero.ValidarEntrada() && numero.Value==10.50m, "Un punto decimal ambiguo no se convierte en miles con cultura " + cultura);
                    numero.Text="10,555"; Assert(!numero.ValidarEntrada(), "No se redondean silenciosamente decimales de mas con cultura " + cultura);
                    NumeroRegional.Restablecer(numero,12.50m); Assert(numero.ErrorEntrada==null && numero.Text=="12,50", "Descartar la edicion restaura el formato y limpia el error con cultura " + cultura);
                }
            }
        } finally { System.Globalization.CultureInfo.CurrentCulture=anterior; }
    }
    private static void ComprobarAcceso()
    {
        using (var login = new Login()) {
            login.Show(); Application.DoEvents();
            var correo = Find<TextBox>(login, "txtEmail"); var clave = Find<TextBox>(login, "txtPassword");
            Assert(login.ClientSize.Width <= 700 && Find<Label>(login, "label3").Text == "Perfumería" && !Find<Label>(login, "label4").Text.Contains("edificios"), "Login compacto con identidad de Perfumeria");
            Assert(login.ActiveControl == correo, "El acceso comienza en el correo");
            Click(login, "btnSoporte"); Assert(login.AcceptButton == null, "Enter en Soporte no inicia una sesion accidentalmente");
            Click(login, "btnCancelar"); Assert(login.AcceptButton == Find<Button>(login, "btnIniciarSesion") && login.ActiveControl == correo, "Volver de Soporte recupera el acceso por teclado");
            Click(login, "btnRegistro"); Assert(login.AcceptButton == null, "Enter en Registro no envia el login");
            Click(login, "btnVolver"); Assert(login.AcceptButton == Find<Button>(login, "btnIniciarSesion") && login.ActiveControl == correo, "Volver de Registro recupera el acceso por teclado");
            typeof(Form).GetMethod("ProcessDialogKey", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(login, new object[] { Keys.Tab });
            Assert(login.ActiveControl == clave, "Tab lleva del correo a la contraseña");
            correo.Text = "demo.dueno@perfumeria.example"; clave.Text = "DemoDueno2026!";
            typeof(Form).GetMethod("ProcessDialogKey", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(login, new object[] { Keys.Enter }); Application.DoEvents();
            var menu = Application.OpenForms.OfType<FormPrincipal>().Single();
            Assert(!login.Visible && menu.Visible, "Enter autentica y abre el menu sin pedir aceptar una bienvenida");
            Assert(Find<Label>(menu, "lblSesionActiva").Text.Contains("Compras") && Find<Label>(menu, "lblSesionActiva").Text.Contains(correo.Text), "La sesion de Compras identifica nombre completo, rol y correo");
            ComprobarRestricciones(menu, "Compras");
            menu.Close(); Application.DoEvents();
            Assert(login.Visible && correo.Text == "" && clave.Text == "" && login.ActiveControl == correo, "Cerrar el menu limpia las credenciales y vuelve al correo");
            login.Close();
        }
        foreach (string[] datos in new[] {
            new[] { "contabilidad", "DemoContabilidad2026!" }, new[] { "deposito", "DemoDeposito2026!" }, new[] { "ventas", "DemoVentas2026!" } }) {
            var actor = new UsuarioBLL().Login("demo." + datos[0] + "@perfumeria.example", datos[1]);
            using (var menu = new FormPrincipal(actor)) {
                menu.Show(); menu.ClientSize = new Size(1000, 570); Application.DoEvents();
                var sesion = Find<Label>(menu, "lblSesionActiva"); var cuerpo = Find<Panel>(menu, "panel1");
                Assert(sesion.Text.Contains(actor.NombreCompleto) && actor.Permisos.OfType<Familia>().All(r => sesion.Text.Contains(r.Nombre)) &&
                    menu.ClientRectangle.Contains(sesion.Bounds) && sesion.Bottom <= cuerpo.Top &&
                    TextRenderer.MeasureText(sesion.Text, sesion.Font).Width <= sesion.ClientSize.Width - sesion.Padding.Horizontal,
                    "Actor visible y legible sin superponer la vista: " + datos[0]);
                ComprobarRestricciones(menu, datos[0]);
                menu.Close();
            }
            new UsuarioBLL().Logout();
        }
    }
    [STAThread]
    private static int Main(string[] args)
    {
        try {
            var cs = new SqlConnectionStringBuilder(ConfigurationManager.ConnectionStrings["MiConexionDB"].ConnectionString);
            if (!cs.InitialCatalog.StartsWith("Perfumeria_Usabilidad_Test_", StringComparison.Ordinal)) throw new Exception("Solo se admite la base aislada del script test-usabilidad-operativa.ps1.");
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            var owner = new UsuarioBLL().Login("demo.dueno@perfumeria.example", "DemoDueno2026!");
            if (owner == null) throw new Exception("No se pudo iniciar sesión demo.");
            if (args.Length == 0) { Session.Instance.Login(Admin(owner)); Application.Run(new FormPrincipal(Session.Instance.Usuario)); return 0; }
            using (cn = new SqlConnection(cs.ConnectionString)) using (var host = new Form { ClientSize = new Size(1080, 680) }) {
                cn.Open(); ComprobarMigracion(); ComprobarAcceso(); ComprobarSoporte(owner); ComprobarFormatoNumerico(); Session.Instance.Login(owner); int clients = Count("Cliente");
                Assert(FormatoPresentacion.Moneda(60000m).Contains("60.000,00"), "Importes mostrados usan el criterio argentino sin cambiar la cultura del hilo");
                using (var alta = new FormAltaCliente("test.venta@example.com")) {
                    Click(alta, "btnRegistrarCliente");
                    Assert(Find<Label>(alta, "lblEstadoAltaCliente").Text.Contains("nombre") && Count("Cliente") == clients, "Alta de cliente invalida queda abierta con aviso y sin escrituras");
                    Find<TextBox>(alta, "txtNombre").Text = "Prueba"; Find<TextBox>(alta, "txtApellido").Text = "Usabilidad";
                    Find<CheckBox>(alta, "chkMembresia").Checked = false;
                    Assert(!Find<NumericUpDown>(alta, "nudDescuento").Enabled, "Descuento solo editable si se crea membresia");
                    Find<CheckBox>(alta, "chkMembresia").Checked = true; Find<NumericUpDown>(alta, "nudDescuento").Value = 10;
                    Click(alta, "btnRegistrarCliente");
                    Assert(alta.DialogResult == DialogResult.OK && alta.ClienteCreado.IdCliente > 0 && Count("Cliente") == clients + 1, "Alta separada guarda cliente y membresia reales");
                }
                using (var alta = new FormAltaCliente()) { Click(alta, "btnCancelarCliente"); Assert(Count("Cliente") == clients + 1, "Cancelar alta de cliente no guarda"); }
                ComprobarNuevaVenta(host); ComprobarEspacioOperativo(host,owner);
                var ventas = new UC_Ventas { ModoPrueba = true }; Host(host, ventas);
                Assert(!ventas.Controls.Find("txtNombre", true).Any() && !ventas.Controls.Find("chkMarketing", true).Any(), "Venta mantiene el alta de clientes fuera de la pantalla operativa");
                var productos = Find<ComboBox>(ventas, "cmbProducto"); var floral = productos.Items.Cast<Producto>().Single(p => p.Codigo == "DEMO-FLORAL"); productos.SelectedValue = floral.IdProducto;
                int sales = Count("Venta"), moves = Count("MovimientoStock");
                Click(ventas, "btnAgregarProducto");
                var grid = Find<DataGridView>(ventas, "dgvVenta");
                Assert(grid.ReadOnly && !grid.AllowUserToDeleteRows && grid.Rows.Count == 1, "Carrito de solo lectura con accion explicita para quitar");
                Assert(Count("Venta") == sales && Count("MovimientoStock") == moves, "Vista previa no registra ventas ni movimientos de stock");
                var email = Find<TextBox>(ventas, "txtEmail"); email.Text = "test.venta@example.com"; Click(ventas, "btnBuscarCliente");
                var cotizacion = (Venta)Field(ventas, "cotizacion");
                Assert(cotizacion.Descuento == Math.Round(floral.PrecioVenta * .1m, 2, MidpointRounding.AwayFromZero) && cotizacion.Total == cotizacion.Subtotal - cotizacion.Descuento,
                    "Vista previa aplica membresia con el mismo redondeo de BLL");
                Assert(Find<Label>(ventas, "lblTotalesVenta").Text.Contains(FormatoPresentacion.Moneda(cotizacion.Total)), "Total visible antes de confirmar en formato argentino");
                email.Text = "otra.persona@example.com";
                Assert(Field(ventas, "clienteActual") == null && ((Venta)Field(ventas, "cotizacion")).Descuento == 0, "Cambiar correo elimina cliente y descuento anteriores");
                email.Text = "test.venta@example.com"; Click(ventas, "btnBuscarCliente"); Click(ventas, "btnQuitarCliente");
                Assert(email.Text == "" && Field(ventas, "clienteActual") == null && grid.Rows.Count == 1, "Venta ocasional conserva carrito y quita identidad del cliente");
                Click(ventas, "btnQuitarProducto"); Assert(grid.Rows.Count == 0 && !Find<Button>(ventas, "btnConfirmarVenta").Enabled, "Quitar ultimo producto pone total en cero y deshabilita confirmacion");
                Find<NumericUpDown>(ventas, "nudCantidad").Value = 999; Click(ventas, "btnAgregarProducto");
                Assert(grid.Rows.Count == 0 && ventas.UltimoError.Contains(floral.Nombre), "Stock insuficiente se avisa con nombre de producto sin agregar una linea invalida");
                Find<NumericUpDown>(ventas, "nudCantidad").Value = 1; Click(ventas, "btnAgregarProducto");
                Query("UPDATE Producto SET PrecioVenta=PrecioVenta+123 WHERE IdProducto=" + floral.IdProducto);
                Click(ventas, "btnConfirmarVenta");
                Assert(Count("Venta") == sales && ventas.UltimoMensaje.Contains("importes cambiaron") && grid.Rows.Count == 1, "Cambio externo de precio requiere revisar el nuevo total antes de guardar");
                email.Text = "test.venta@example.com"; Click(ventas, "btnBuscarCliente"); cotizacion = (Venta)Field(ventas, "cotizacion"); Click(ventas, "btnConfirmarVenta");
                var persisted = Query("SELECT TOP(1) Subtotal,Descuento,Total,IdCliente FROM Venta ORDER BY IdVenta DESC").Rows[0];
                Assert(Count("Venta") == sales + 1 && (decimal)persisted[0] == cotizacion.Subtotal && (decimal)persisted[1] == cotizacion.Descuento && (decimal)persisted[2] == cotizacion.Total, "Importes confirmados coinciden con vista previa y membresia");
                Assert(grid.Rows.Count == 0 && email.Text == "" && Field(ventas, "clienteActual") == null && Find<NumericUpDown>(ventas,"nudCantidad").Value==1, "Venta finalizada prepara carrito, identidad y cantidad limpios");
                ComprobarPreparacionCompras(host, owner);
                var compras = new UC_Aprovisionamiento(owner) { ModoPrueba = true }; Host(host, compras);
                var orders = Find<DataGridView>(compras, "dgvOrdenes");
                Assert(Find<DateTimePicker>(compras,"dtpOrdenDesde").Format==DateTimePickerFormat.Custom && Find<DateTimePicker>(compras,"dtpOrdenDesde").CustomFormat=="dd/MM/yyyy" &&
                    Convert.ToString(orders.Rows[0].Cells["Total"].FormattedValue)==((decimal)orders.Rows[0].Cells["Total"].Value).ToString("N2",FormatoPresentacion.Cultura), "Grilla y fechas de Compras usan la misma convencion regional");
                foreach (var state in new[] { EstadoOrdenCompra.Borrador, EstadoOrdenCompra.PendienteAprobacion, EstadoOrdenCompra.Aprobada, EstadoOrdenCompra.Rechazada, EstadoOrdenCompra.Emitida, EstadoOrdenCompra.RecibidaParcial, EstadoOrdenCompra.Recibida }) {
                    SelectOrder(orders, state);
                    Assert(Find<Button>(compras, "btnEnviarRevision").Enabled == (state == EstadoOrdenCompra.Borrador) && Find<Button>(compras, "btnEmitirOrden").Enabled == (state == EstadoOrdenCompra.Aprobada), "Acciones disponibles segun estado " + state);
                }
                SelectOrder(orders, EstadoOrdenCompra.PendienteAprobacion);
                Assert((string)orders.CurrentRow.Cells["Estado"].Value == "Pendiente de aprobación", "Grilla muestra estados legibles");
                var states = Find<ComboBox>(compras, "cmbFiltroEstado"); states.SelectedIndex = 3; // All, draft, pending, approved.
                Assert(orders.Rows.Count > 0 && orders.Rows.Cast<DataGridViewRow>().All(r => (EstadoOrdenCompra)r.Cells["EstadoCodigo"].Value == EstadoOrdenCompra.Aprobada), "Filtro por estado reduce el listado");
                compras.RutaArchivoPrueba = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ordenes-filtradas.xml"); Click(compras, "btnExportarOrdenesXml");
                Assert(OrdenesXml.Leer(compras.RutaArchivoPrueba).Ordenes.Count == orders.Rows.Count, "Exportacion XML respeta el listado filtrado");
                var providers = Find<ComboBox>(compras, "cmbFiltroProveedor"); providers.SelectedValue = 0;
                Find<CheckBox>(compras, "chkFechasOrdenes").Checked = true; Find<DateTimePicker>(compras, "dtpOrdenDesde").Value = DateTime.Today.AddYears(5);
                Assert(orders.Rows.Count == 0 && !Find<Button>(compras, "btnEmitirOrden").Enabled && !Find<Button>(compras, "btnExportarOrdenesXml").Enabled, "Sin resultados no se habilitan acciones ni exportacion");
                Click(compras, "btnLimpiarFiltrosOrdenes"); Assert(orders.Rows.Count == Count("OrdenCompra"), "Limpiar filtros recupera todas las ordenes");
                SelectOrder(orders, EstadoOrdenCompra.Aprobada); int emitida = (int)orders.CurrentRow.Cells["IdOrdenCompra"].Value;
                compras.ModoPrueba = false; Click(compras, "btnEmitirOrden"); compras.ModoPrueba = true;
                Assert(Find<Label>(compras, "lblActualizacionCompras").Text == compras.UltimoMensaje && compras.UltimoMensaje.Contains("emitida") &&
                    new AprovisionamientoBLL().ConsultarHistorialCompras(null).Single(o => o.IdOrdenCompra == emitida).Estado == EstadoOrdenCompra.Emitida,
                    "Emitir guarda y muestra el resultado en la vista sin modal de exito");
                int providerId = providers.Items.Cast<Proveedor>().First(p => p.IdProveedor != 0).IdProveedor;
                providers.SelectedValue = providerId;
                string providerName = ((Proveedor)providers.SelectedItem).RazonSocial;
                Assert(orders.Rows.Count > 0 && orders.Rows.Cast<DataGridViewRow>().All(r => (string)r.Cells["Proveedor"].Value == providerName), "Filtro de proveedor usa su nombre");
                Click(compras, "btnActualizarOrdenes"); Assert((int)providers.SelectedValue == providerId, "Actualizar mantiene los filtros disponibles");
                var receipt = Find<ComboBox>(compras, "cmbOrdenRecepcion");
                receipt.SelectedItem = receipt.Items.Cast<object>().First(i => ((OrdenCompra)i.GetType().GetProperty("Orden").GetValue(i, null)).CreadaPor.StartsWith("DEMO-OC-06 |"));
                var receiptProduct = Find<ComboBox>(compras, "cmbRecepcionProducto"); receiptProduct.SelectedValue = floral.IdProducto;
                Assert(Find<Label>(compras, "lblSaldoRecepcion").Text.Contains("Pedidas: 10") && Find<Label>(compras, "lblSaldoRecepcion").Text.Contains("Recibidas: 4") &&
                    Find<NumericUpDown>(compras, "nudLoteCantidad").Maximum == 6, "Recepcion muestra proveedor, cantidades y limita el saldo de Floral a seis");
                receiptProduct.SelectedIndex = receiptProduct.Items.Count - 1;
                Assert(Find<NumericUpDown>(compras, "nudLoteCantidad").Maximum == 3, "Cambiar perfume limita al saldo de Madera");
                receipt.DataSource = null; Assert(!Find<Button>(compras, "btnRegistrarRecepcion").Enabled, "Sin orden pendiente no se habilita recepcion");
                var detailOrder = new AprovisionamientoBLL().ConsultarHistorialCompras(null).First(o => o.Estado == EstadoOrdenCompra.Rechazada);
                using (var detail = new FormDetalleOrdenCompra(detailOrder, "Proveedor de prueba", new ProductoBLL().Listar(false), null)) {
                    Assert(Find<DataGridView>(detail, "dgvDetalleOrden").ReadOnly && Find<DataGridView>(detail, "dgvDetalleOrden").Rows.Count == detailOrder.Detalles.Count &&
                        Find<Label>(detail, "lblRevisionOrden").Text.Contains(detailOrder.MotivoRechazo), "Detalle de solo lectura muestra precios, saldos y motivo de rechazo");
                }
                var suggestedProduct = new Producto { Codigo = "TEST-CONTINUIDAD", Nombre = "Producto de continuidad", PrecioVenta = 100, StockMinimo = 7 };
                new ProductoBLL().Guardar(suggestedProduct);
                int suggestedId = new ProductoBLL().Listar().Single(p => p.Codigo == suggestedProduct.Codigo).IdProducto;
                foreach (var supplier in new ProveedorBLL().Listar(true).Take(2)) {
                    var quote = new CotizacionProveedor { IdProveedor = supplier.IdProveedor, FechaVigencia = DateTime.Today.AddMonths(1) };
                    quote.Detalles.Add(new DetalleCotizacionProveedor { IdProducto = suggestedId, PrecioUnitario = 40, PlazoEntregaDias = 3 }); new AprovisionamientoBLL().RegistrarCotizacion(quote);
                }
                var rootTabs = compras.Controls.OfType<TabControl>().Single(); rootTabs.SelectedTab = rootTabs.TabPages.Cast<TabPage>().Single(t => t.Text == "Evaluación de proveedores");
                var evaluation = (UC_EvaluacionProveedores)Field(compras, "evaluacion"); evaluation.ModoPrueba = true;
                Find<ComboBox>(evaluation, "cmbEvaluacionProducto").SelectedValue = suggestedId; Click(evaluation, "btnCompararProveedores"); Click(evaluation, "btnUsarCantidadSugerida");
                Assert(Find<NumericUpDown>(evaluation, "nudEvaluacionCantidad").Value == 7 && !evaluation.TieneCambiosPendientes, "Usar cantidad sugerida vuelve a comparar sin confirmar ni crear compras");
                string fundamento = "Compra de reposicion fundamentada en stock." + Environment.NewLine + "Se elige por conveniencia y plazo.";
                Find<TextBox>(evaluation, "txtMotivoSeleccion").Text = fundamento;
                Assert(compras.TieneCambiosPendientes, "El fundamento de N03 sin confirmar cuenta como trabajo pendiente");
                evaluation.ModoPrueba = false; Click(evaluation, "btnConfirmarProveedor"); evaluation.ModoPrueba = true;
                Assert(Find<Label>(evaluation, "lblActualizacionEvaluacion").Text == evaluation.UltimoMensaje && evaluation.UltimoMensaje.Contains("Proveedor confirmado"),
                    "Confirmar proveedor muestra el siguiente paso sin modal de exito");
                var confirmada = (EvaluacionProveedor)Field(evaluation,"actual");
                Assert(new EvaluacionProveedorBLL().Obtener(confirmada.IdEvaluacion).MotivoSeleccion==fundamento, "El fundamento de varias lineas se conserva completo al confirmar");
                int ordersBefore = Count("OrdenCompra"); Click(evaluation, "btnOrdenEvaluacion");
                Assert(Find<Button>(evaluation, "btnOrdenEvaluacion").Enabled && Find<Button>(evaluation, "btnOrdenEvaluacion").Text.StartsWith("Abrir orden"), "El borrador vinculado ofrece abrir la orden existente");
                int linked = Convert.ToInt32(Query("SELECT MAX(IdOrdenCompra) FROM OrdenCompra").Rows[0][0]); Click(evaluation, "btnOrdenEvaluacion");
                Assert(rootTabs.SelectedTab.Text == "Órdenes de compra" && (int)orders.CurrentRow.Cells["IdOrdenCompra"].Value == linked && Count("OrdenCompra") == ordersBefore + 1,
                    "Abrir orden navega y selecciona el borrador sin duplicarlo ni mantener filtros que lo oculten");
                using (var menu = new MenuPrueba(owner)) {
                    menu.Show(); Application.DoEvents(); Click(menu, "btnVentas");
                    var saleView = Find<ComboBox>(menu, "cmbProducto"); saleView.SelectedValue = floral.IdProducto; Click(menu, "btnAgregarProducto");
                    int recordedSales = Count("Venta"); Click(menu, "btnAprovisionamiento");
                    Assert(menu.Consultas == 1 && Find<DataGridView>(menu, "dgvVenta").Rows.Count == 1, "Cancelar navegacion conserva el carrito y la vista original");
                    menu.Descartar = true; Click(menu, "btnAprovisionamiento");
                    Assert(menu.Consultas == 2 && !menu.Controls.Find("dgvVenta", true).Any() && Count("Venta") == recordedSales, "Descartar permite navegar sin registrar la venta");
                    Click(menu, "btnVentas"); Assert(menu.Consultas == 2, "Navegar sin datos pendientes no muestra avisos");
                    Find<ComboBox>(menu, "cmbProducto").SelectedValue = floral.IdProducto; Click(menu, "btnAgregarProducto"); menu.Descartar = false; menu.Close();
                    Assert(!menu.IsDisposed && menu.Consultas == 3, "Cancelar cierre conserva la venta en preparacion");
                    menu.Descartar = true; menu.Close();
                }
                Session.Instance.Login(Admin(owner)); var usuarios = new UC_Usuarios(); Host(host, usuarios);
                Assert(Find<Button>(usuarios, "btnAgregar").Enabled && !Find<Button>(usuarios, "btnModificar").Enabled && !Find<Button>(usuarios, "btnBaja").Enabled, "Nuevo usuario no permite modificar ni desactivar otro usuario");
                Click(usuarios, "btnAgregar"); Assert(!string.IsNullOrWhiteSpace(Find<Label>(usuarios, "lblEstadoUsuarios").Text), "Campos obligatorios de usuario muestran aviso junto al formulario");
                int userCount = Count("Usuarios");
                Find<TextBox>(usuarios, "txtDni").Text = "98123457"; Find<TextBox>(usuarios, "txtNombre").Text = "Alta UI";
                Find<TextBox>(usuarios, "txtApellido").Text = "Usabilidad"; Find<TextBox>(usuarios, "txtEmail").Text = "test.usuario@example.com";
                Find<TextBox>(usuarios, "txtContrasenia").Text = "Usuario2026!Demo"; Click(usuarios, "btnAgregar");
                Assert(Count("Usuarios") == userCount + 1 && Find<TextBox>(usuarios, "txtEmail").Text == "", "Guardar nuevo usuario persiste una cuenta y limpia el formulario");
                var usersGrid = Find<DataGridView>(usuarios, "dgvUsuarios"); var row = usersGrid.Rows.Cast<DataGridViewRow>().Single(r => ((Usuario)r.DataBoundItem).Email == "test.usuario@example.com");
                Assert(Find<TextBox>(usuarios, "txtEmail").Right <= Find<TextBox>(usuarios, "txtEmail").Parent.ClientSize.Width && usersGrid.Right <= usersGrid.Parent.ClientSize.Width,
                    "Formulario y grilla de usuarios caben en el ancho disponible");
                usersGrid.CurrentCell = row.Cells["Email"]; row.Selected = true;
                typeof(UC_Usuarios).GetMethod("dgvUsuarios_CellClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(usuarios, new object[] { usersGrid, new DataGridViewCellEventArgs(row.Cells["Email"].ColumnIndex, row.Index) });
                Assert(!Find<Button>(usuarios, "btnAgregar").Enabled && Find<Button>(usuarios, "btnModificar").Enabled && Find<Label>(usuarios, "lblModoUsuario").Text.Contains("Editar usuario"), "Seleccionar usuario activa modo edicion sin crear duplicados");
                Assert(Find<TextBox>(usuarios, "txtDni").Text == ((Usuario)row.DataBoundItem).Dni, "Seleccion muestra DNI ya descifrado por el mapper");
                string userPassword = ((Usuario)row.DataBoundItem).Password;
                Find<TextBox>(usuarios, "txtEmail").Text = "invalido"; Click(usuarios, "btnModificar");
                Assert(((Usuario)row.DataBoundItem).Email == "test.usuario@example.com" && new UsuarioBLL().BuscarPorEmail("test.usuario@example.com") != null,
                    "Edicion invalida no altera la fila ni los datos persistidos");
                Find<TextBox>(usuarios, "txtEmail").Text = "test.usuario@example.com"; Find<TextBox>(usuarios, "txtNombre").Text = "Editado UI"; Click(usuarios, "btnModificar");
                var saved = new UsuarioBLL().BuscarPorEmail("test.usuario@example.com");
                Assert(saved.Nombre == "Editado UI" && saved.Password == userPassword, "Guardar cambios modifica datos y conserva contraseña vacia");
                row = usersGrid.Rows.Cast<DataGridViewRow>().Single(r => ((Usuario)r.DataBoundItem).Id == saved.Id); usersGrid.CurrentCell = row.Cells["Email"]; row.Selected = true;
                typeof(UC_Usuarios).GetMethod("dgvUsuarios_CellClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(usuarios, new object[] { usersGrid, new DataGridViewCellEventArgs(row.Cells["Email"].ColumnIndex, row.Index) });
                Click(usuarios, "btnNuevoUsuario"); Assert(Find<Button>(usuarios, "btnAgregar").Enabled && usersGrid.CurrentRow == null && Find<TextBox>(usuarios, "txtEmail").Text == "", "Nuevo usuario limpia seleccion y formulario anteriores");
                var restricted = new UC_Usuarios(); Session.Instance.Login(owner); Host(host, restricted);
                Assert(!Find<Button>(restricted, "btnAgregar").Enabled && !Find<Button>(restricted, "btnModificar").Enabled, "Sin permisos administrativos no se habilita alta ni edicion");
                Session.Instance.Login(Admin(owner));
                var target = new Usuario { Nombre = "Antes", Apellido = "Restauracion", Email = "test.audit@example.com", Dni = "98123456" };
                new UsuarioBLL().Insertar(target, "Audit2026!Clave"); target = new UsuarioBLL().BuscarPorEmail(target.Email);
                DAL.AuditoriaDAL.Instancia.GuardarSnapshot("UPDATE", target, owner.Email);
                var version = new AuditoriaBLL().ListarHistorial().First(a => a.Id == target.Id && a.AuditAction == "UPDATE");
                var changed = (Usuario)target.Clone(); changed.Nombre = "Despues"; changed.Email = "test.audit.cambiado@example.com";
                new UsuarioBLL().Modificar(changed, "");
                var current = new UsuarioBLL().ListarTodos().Single(u => u.Id == target.Id);
                var lector = (Usuario)owner.Clone(); lector.Permisos = new System.Collections.Generic.List<Componente> { new Patente { PermisoAsociado=TipoPermiso.Auditoria_Ver } };
                Session.Instance.Login(lector);
                using (var lectura = new UC_AuditoriaUsuarios()) {
                    Host(host,lectura); var historial = Find<DataGridView>(lectura,"dgvHistorial");
                    var filaHistorial = historial.Rows.Cast<DataGridViewRow>().First(r => ((UsuarioAuditoria)r.DataBoundItem).Id==target.Id);
                    historial.CurrentCell=filaHistorial.Cells["Email"]; Application.DoEvents();
                    Assert(!Find<Button>(lectura,"btnRollback").Enabled, "Permiso de lectura permite consultar sin habilitar restauracion");
                    Denegado(delegate { new AuditoriaBLL().HacerRollback(version); }, "Lectura no habilita restauracion directa por BLL");
                    Assert(new UsuarioBLL().ListarTodos().Single(u=>u.Id==target.Id).Email==current.Email, "El intento denegado no modifica al usuario");
                }
                Session.Instance.Login(Admin(owner));
                using (var preview = new FormRestaurarUsuario(version, current)) {
                    Assert(preview.CantidadCambios == 2 && Find<DataGridView>(preview, "dgvCambiosRestauracion").Rows.Cast<DataGridViewRow>().Any(r => (string)r.Cells["Campo"].Value == "Correo"), "Restauracion muestra valores actuales y futuros de los datos cambiados");
                    Click(preview, "btnCancelarRestauracion");
                    Assert(new UsuarioBLL().ListarTodos().Single(u => u.Id == target.Id).Nombre == "Despues", "Cancelar revision de restauracion no modifica el usuario");
                }
                using (var preview = new FormRestaurarUsuario(version, target))
                    Assert(preview.CantidadCambios == 0 && !Find<Button>(preview, "btnConfirmarRestauracion").Enabled, "Sin diferencias no hay restauracion para confirmar");
                var passwordVersion = new UsuarioAuditoria { Dni = target.Dni, Nombre = target.Nombre, Apellido = target.Apellido, Email = target.Email, Activo = target.Activo, Password = "hash-diferente" };
                using (var preview = new FormRestaurarUsuario(passwordVersion, target))
                    Assert(Find<DataGridView>(preview, "dgvCambiosRestauracion").Rows.Cast<DataGridViewRow>().All(r => !Convert.ToString(r.Cells["Restaurado"].Value).Contains("hash-diferente") && !Convert.ToString(r.Cells["Actual"].Value).Contains(target.Password)), "Revision nunca expone hashes de credenciales");
                new AuditoriaBLL().HacerRollback(version);
                var restored = new UsuarioBLL().ListarTodos().Single(u => u.Id == target.Id);
                Assert(restored.Nombre == target.Nombre && restored.Email == target.Email && restored.Password == target.Password, "Restauracion por ID recupera nombre y correo conservando credencial historica");
                Assert(new UsuarioBLL().VerificarIntegridad().Count == 0, "Restauracion conserva integridad DVH y DVV");
                var audit = new UC_AuditoriaUsuarios(); Host(host, audit);
                Assert(!Find<Button>(audit, "btnRollback").Enabled && Find<Label>(audit, "lblEstadoAuditoria").Text.Contains("revise"), "Auditoria exige seleccionar y revisar una version antes de restaurar");
                Find<TextBox>(audit, "txtBuscar").Text = "test.audit@example.com"; Click(audit, "btnBuscar");
                int matches = Find<DataGridView>(audit, "dgvHistorial").Rows.Count; Click(audit, "btnActualizarAuditoria");
                Assert(matches > 0 && Find<DataGridView>(audit, "dgvHistorial").Rows.Count == matches, "Actualizar auditoria conserva la busqueda aplicada");
                Console.WriteLine("RESULTADO: " + checks + " verificaciones OK; BASE=" + cs.InitialCatalog);
            }
            return 0;
        } catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { new UsuarioBLL().Logout(); }
    }
}
