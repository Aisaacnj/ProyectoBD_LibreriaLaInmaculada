using LibreriaLaInmaculada.Backend.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibreriaLaInmaculada.Backend.Controladores;

// Módulos solo para el Administrador. El atributo [Authorize] bloquea la entrada aunque
// alguien escriba la dirección a mano (por ejemplo /Compras): lo manda a Acceso denegado.
// Cada uno se moverá a su propio archivo cuando se programe.

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
public class ProveedoresController : Controller
{
    public IActionResult Index() => View("EnConstruccion", (object)"Proveedores");
}

[Authorize(Roles = Roles.Administrador)]
public class ReportesController : Controller
{
    public IActionResult Index() => View("EnConstruccion", (object)"Reportes");
}

[Authorize(Roles = Roles.Administrador)]
public class UsuariosController : Controller
{
    public IActionResult Index() => View("EnConstruccion", (object)"Usuarios");
}
