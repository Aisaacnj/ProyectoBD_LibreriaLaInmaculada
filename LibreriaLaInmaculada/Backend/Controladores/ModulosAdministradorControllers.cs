using LibreriaLaInmaculada.Backend.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibreriaLaInmaculada.Backend.Controladores;

// Módulos solo para el Administrador que todavía están en construcción.
// Proveedores y Usuarios ya tienen su propio archivo (ProveedoresController.cs, UsuariosController.cs).

[Authorize(Roles = Roles.Administrador)]
public class ComprasController : Controller
{
    public IActionResult Index() => View("EnConstruccion", (object)"Compras");
}

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
