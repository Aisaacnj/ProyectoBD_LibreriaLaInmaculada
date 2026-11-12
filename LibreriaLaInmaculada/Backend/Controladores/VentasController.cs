using LibreriaLaInmaculada.Backend.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibreriaLaInmaculada.Backend.Controladores;

/// <summary>Ventas: lo usan los dos roles. Anular será solo del Administrador.</summary>
public class VentasController : Controller
{
    // Pantalla "Vender" (punto de venta)
    public IActionResult Nueva() => View("EnConstruccion", (object)"Vender");

    // Lista de ventas
    public IActionResult Index() => View("EnConstruccion", (object)"Ventas");

    // Ejemplo de cómo quedará protegida la anulación:
    // [HttpPost, ValidateAntiForgeryToken]
    // [Authorize(Roles = Roles.Administrador)]
    // public async Task<IActionResult> Anular(int id) { ... }
}
