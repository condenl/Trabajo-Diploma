using BE;
using BE.Seguridad;
using Servicios;

namespace Arquitectura.Tests;

internal static class SesionPruebas
{
    // Actor en memoria para pruebas de reglas; no modifica perfiles ni bases demo.
    internal static void Iniciar(params TipoPermiso[] permisos)
    {
        var actor = new Usuario { Id=1, Nombre="Prueba", Email="actor@perfumeria.test", Activo=true };
        foreach (var permiso in permisos) actor.Permisos.Add(new Patente { Nombre=permiso.ToString(), PermisoAsociado=permiso });
        Session.Instance.Login(actor);
    }

    internal static void Restringir(int caso, params TipoPermiso[] permisos)
    {
        Session.Instance.Logout();
        if (caso == 0) return;
        Iniciar(caso == 1 ? permisos : System.Array.Empty<TipoPermiso>());
        if (caso == 1) Session.Instance.Usuario.Activo = false;
    }
}
