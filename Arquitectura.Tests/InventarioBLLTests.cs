using System;
using System.Collections.Generic;
using BE;
using BE.Seguridad;
using Servicios;
using BE.Interfaces;
using DAL.Interfaces;
using BLL;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arquitectura.Tests
{
    [TestClass]
    [DoNotParallelize]
    public class InventarioBLLTests
    {
        [TestInitialize]
        public void IniciarActor() => SesionPruebas.Iniciar(TipoPermiso.Inventario_Recepcionar);
        [TestCleanup]
        public void CerrarActor() => Session.Instance.Logout();

        [TestMethod]
        [DataRow(0)] [DataRow(1)] [DataRow(2)]
        public void Escrituras_ExigenSesionActivaYPermiso(int caso)
        {
            var servicio = new InventarioBLL(new FakeInventarioRepository(), new FakeRegistroActividad());
            SesionPruebas.Restringir(caso, TipoPermiso.Inventario_Recepcionar);
            Assert.Throws<UnauthorizedAccessException>(() => servicio.RegistrarIngresoLote(null!));
            Assert.Throws<UnauthorizedAccessException>(() => servicio.DescontarStockLote(1, 1));
        }
        [TestMethod]
        public void RegistrarIngresoLote_PermiteOmitirCertificado()
        {
            FakeInventarioRepository repositorio = new FakeInventarioRepository();
            InventarioBLL servicio = new InventarioBLL(repositorio, new FakeRegistroActividad());
            Lote lote = new Lote
            {
                IdProducto = 1,
                IdProveedor = 1,
                CodigoLote = "L-1",
                Origen = "Francia",
                CantidadIngresada = 2
            };

            Assert.IsTrue(servicio.RegistrarIngresoLote(lote));
            Assert.AreEqual(1, lote.IdLote);
        }

        [TestMethod]
        public void RegistrarIngresoLote_SigueExigiendoOrigen()
        {
            InventarioBLL servicio = new InventarioBLL(new FakeInventarioRepository(), new FakeRegistroActividad());
            Assert.Throws<ArgumentException>(() => servicio.RegistrarIngresoLote(new Lote {
                IdProducto = 1, IdProveedor = 1, CodigoLote = "L-1", CantidadIngresada = 2
            }));
        }

        [TestMethod]
        public void RegistrarIngresoLote_ValidaLongitudDelCertificadoOpcional()
        {
            InventarioBLL servicio = new InventarioBLL(new FakeInventarioRepository(), new FakeRegistroActividad());
            Assert.Throws<ArgumentException>(() => servicio.RegistrarIngresoLote(new Lote {
                IdProducto = 1, IdProveedor = 1, CodigoLote = "L-1", Origen = "Francia",
                CantidadIngresada = 2, CertificadoOrigen = new string('A', 251)
            }));
        }

        [TestMethod]
        public void DescontarStockLote_NoPermiteStockNegativo()
        {
            FakeInventarioRepository repositorio = new FakeInventarioRepository { DescuentoExitoso = false };
            InventarioBLL servicio = new InventarioBLL(repositorio, new FakeRegistroActividad());

            Assert.Throws<InvalidOperationException>(() => servicio.DescontarStockLote(1, 5));
        }
    }

    internal sealed class FakeInventarioRepository : IInventarioRepository
    {
        public bool DescuentoExitoso { get; set; }

        public int VerificarStock(int idProducto) { return 0; }

        public int RegistrarIngresoLote(Lote lote, string responsable, string referencia) { return 1; }

        public bool DescontarStockLote(int idLote, int cantidad, string responsable, string referencia)
        {
            return DescuentoExitoso;
        }

        public IList<Lote> ObtenerLotesDisponibles(int idProducto) { return new List<Lote>(); }

        public IList<Lote> ObtenerTrazabilidad(int idProducto) { return new List<Lote>(); }
    }
}
