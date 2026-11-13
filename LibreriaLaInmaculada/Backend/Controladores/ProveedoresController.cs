using LibreriaLaInmaculada.Backend.AccesoDatos;
using LibreriaLaInmaculada.Backend.Modelos;
using LibreriaLaInmaculada.Backend.Seguridad;
using LibreriaLaInmaculada.Frontend.ModelosDeVista;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace LibreriaLaInmaculada.Backend.Controladores;

/// <summary>Proveedores: todo el módulo es solo del Administrador.</summary>
[Authorize(Roles = Roles.Administrador)]
public class ProveedoresController : Controller
{
    private readonly ProveedorRepositorio _proveedores;

    public ProveedoresController(ProveedorRepositorio proveedores) => _proveedores = proveedores;

    public async Task<IActionResult> Index(string? buscar, bool inactivos = false)
    {
        buscar = string.IsNullOrWhiteSpace(buscar) ? null : buscar.Trim();
        var vm = new ProveedoresIndexViewModel
        {
            Proveedores = await _proveedores.ListarAsync(buscar, inactivos),
            Buscar = buscar,
            MostrarInactivos = inactivos
        };
        return View(vm);
    }

    [HttpGet]
    public IActionResult Nuevo() => View("Formulario", new ProveedorFormViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Nuevo(ProveedorFormViewModel vm)
    {
        vm.IdProveedor = 0;
        return Guardar(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var p = await _proveedores.ObtenerAsync(id);
        if (p is null) return NotFound();

        return View("Formulario", new ProveedorFormViewModel
        {
            IdProveedor = p.IdProveedor,
            Nombre = p.Nombre,
            ContactoNombre = p.ContactoNombre,
            Telefono = p.Telefono,
            Email = p.Email,
            Direccion = p.Direccion,
            Activo = p.Activo
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Editar(ProveedorFormViewModel vm) => Guardar(vm);

    private async Task<IActionResult> Guardar(ProveedorFormViewModel vm)
    {
        vm.Nombre = Validaciones.LimpiarTexto(vm.Nombre);
        vm.ContactoNombre = Validaciones.LimpiarTexto(vm.ContactoNombre);
        vm.Direccion = Validaciones.LimpiarTexto(vm.Direccion);
        vm.Email = vm.Email?.Trim().ToLowerInvariant();
        if (ModelState.IsValid)
            vm.Telefono = Validaciones.FormatearTelefono(vm.Telefono);   // siempre se guarda como 0000-0000

        Proveedor? actual = null;
        if (!vm.EsNuevo)
        {
            actual = await _proveedores.ObtenerAsync(vm.IdProveedor);
            if (actual is null) return NotFound();
            vm.Activo = actual.Activo;
        }

        if (ModelState.IsValid && await _proveedores.ExisteNombreAsync(vm.Nombre!, vm.EsNuevo ? null : vm.IdProveedor))
            ModelState.AddModelError(nameof(vm.Nombre), "Ya existe un proveedor con ese nombre.");

        if (ModelState.IsValid)
        {
            var proveedor = new Proveedor
            {
                IdProveedor = vm.IdProveedor,
                Nombre = vm.Nombre!,
                ContactoNombre = vm.ContactoNombre,
                Telefono = vm.Telefono,
                Email = vm.Email,
                Direccion = vm.Direccion
            };
            try
            {
                if (vm.EsNuevo)
                {
                    await _proveedores.CrearAsync(proveedor);
                    TempData["Mensaje"] = $"Se agregó el proveedor «{proveedor.Nombre}».";
                }
                else
                {
                    await _proveedores.ActualizarAsync(proveedor);
                    TempData["Mensaje"] = $"Se guardaron los cambios de «{proveedor.Nombre}».";
                }
                return RedirectToAction(nameof(Index));
            }
            catch (SqlException ex) when (SqlErrores.EsDuplicado(ex))
            {
                ModelState.AddModelError(nameof(vm.Nombre), "Ya existe un proveedor con ese nombre.");
            }
        }
        return View("Formulario", vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(int id)
    {
        var p = await _proveedores.ObtenerAsync(id);
        if (p is null) return NotFound();

        await _proveedores.CambiarEstadoAsync(id, !p.Activo);
        TempData["Mensaje"] = p.Activo
            ? $"«{p.Nombre}» se desactivó: ya no aparecerá al registrar compras. Sus compras anteriores se conservan."
            : $"«{p.Nombre}» se activó de nuevo.";
        return RedirectToAction(nameof(Index), new { inactivos = p.Activo });
    }
}
