using LibreriaLaInmaculada.Backend.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibreriaLaInmaculada.Backend.Controladores;

// Módulos solo para el Administrador que todavía están en construcción.
// Compras, Proveedores y Usuarios ya tienen su propio archivo
// (ComprasController.cs, ProveedoresController.cs, UsuariosController.cs).

[Authorize(Roles = Roles.Administrador)]
public class PerdidasController : Controller
{
    public IActionResult Index() => View("EnConstruccion", (object)"Pérdidas");
}

[Authorize(Roles = Roles.Administrador)]
public class ReportesController : Controller
{
    public IActionResult Index() => View("EnConstruccion", (object)"Reportes");
}
