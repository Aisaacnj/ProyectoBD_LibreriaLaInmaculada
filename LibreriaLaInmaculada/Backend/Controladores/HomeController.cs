using LibreriaLaInmaculada.Backend.AccesoDatos;
using LibreriaLaInmaculada.Backend.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibreriaLaInmaculada.Backend.Controladores;

public class HomeController : Controller
{
    private readonly InicioRepositorio _inicio;

    public HomeController(InicioRepositorio inicio) => _inicio = inicio;

    /// <summary>Misma dirección para todos; cada rol ve su propio resumen.</summary>
    public async Task<IActionResult> Index()
    {
        if (User.EsAdministrador())
            return View("Index", await _inicio.ObtenerResumenAdminAsync());

        return View("IndexVendedor", await _inicio.ObtenerResumenVendedorAsync(User.IdUsuario()));
    }

    [AllowAnonymous]
    public IActionResult Error() => View();
}
