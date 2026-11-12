using Microsoft.AspNetCore.Mvc;

namespace LibreriaLaInmaculada.Backend.Controladores;

/// <summary>Inventario: los dos roles lo consultan; editar precios será solo del Administrador.</summary>
public class InventarioController : Controller
{
    public IActionResult Index() => View("EnConstruccion", (object)"Inventario");
}
