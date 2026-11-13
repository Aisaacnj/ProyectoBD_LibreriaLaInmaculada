using LibreriaLaInmaculada.Backend.AccesoDatos;
using LibreriaLaInmaculada.Backend.Modelos;
using LibreriaLaInmaculada.Backend.Seguridad;
using LibreriaLaInmaculada.Frontend.ModelosDeVista;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace LibreriaLaInmaculada.Backend.Controladores;

/// <summary>
/// Pérdidas: solo el Administrador. Todo lo que sale del inventario sin venderse
/// (daño, producto seco, faltante, defecto de fábrica, uso interno) se registra aquí con su motivo.
/// </summary>
[Authorize(Roles = Roles.Administrador)]
public class PerdidasController : Controller
{
    private readonly PerdidaRepositorio _perdidas;
    private readonly ArticuloRepositorio _articulos;

    public PerdidasController(PerdidaRepositorio perdidas, ArticuloRepositorio articulos)
    {
        _perdidas = perdidas;
        _articulos = articulos;
    }

    // ---------------------------------------------------------------- Lista
    public async Task<IActionResult> Index(DateTime? desde, DateTime? hasta, int? motivo, string? buscar)
    {
        if (desde > hasta) (desde, hasta) = (hasta, desde);
        var filtro = new FiltroPerdidas
        {
            Desde = desde,
            Hasta = hasta,
            IdMotivo = motivo,
            Buscar = Validaciones.LimpiarTexto(buscar)
        };

        var vm = new PerdidasIndexViewModel
        {
            Perdidas = await _perdidas.ListarAsync(filtro),
            PorMotivo = await _perdidas.TotalizarPorMotivoAsync(filtro),
            Motivos = await _perdidas.ListarMotivosAsync(),
            Desde = desde,
            Hasta = hasta,
            IdMotivo = motivo,
            Buscar = filtro.Buscar
        };
        return View(vm);
    }

    // ---------------------------------------------------------------- Registrar
    [HttpGet]
    public async Task<IActionResult> Nueva()
    {
        var vm = new PerdidaNuevaViewModel();
        await LlenarListas(vm);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Nueva(PerdidaNuevaViewModel vm)
    {
        vm.Observacion = Validaciones.LimpiarTexto(vm.Observacion);
        await LlenarListas(vm);

        var articulo = vm.Articulos.FirstOrDefault(a => a.IdArticulo == vm.IdArticulo);
        if (vm.IdArticulo is not null && articulo is null)
            ModelState.AddModelError(nameof(vm.IdArticulo), "Elija un artículo de la lista.");
        if (vm.IdMotivo is not null && !vm.Motivos.Any(m => m.IdMotivo == vm.IdMotivo))
            ModelState.AddModelError(nameof(vm.IdMotivo), "Elija un motivo de la lista.");
        if (articulo is not null && vm.Cantidad > articulo.Cantidad)
            ModelState.AddModelError(nameof(vm.Cantidad), articulo.Cantidad == 0
                ? $"«{articulo.Descripcion}» no tiene unidades en stock."
                : $"De «{articulo.Descripcion}» solo hay {articulo.Cantidad} en stock.");

        if (!ModelState.IsValid)
            return View(vm);

        // La base vuelve a revisar el stock con bloqueo, por si alguien vendió mientras tanto
        string? error = await _perdidas.RegistrarAsync(vm.IdArticulo!.Value, vm.IdMotivo!.Value, vm.Cantidad!.Value,
                                                       vm.Observacion, User.IdUsuario());
        if (error is not null)
        {
            ModelState.AddModelError("", error);
            await LlenarListas(vm);
            return View(vm);
        }

        TempData["Mensaje"] = $"Se registró la pérdida de {vm.Cantidad} {(vm.Cantidad == 1 ? "unidad" : "unidades")} de «{articulo!.Descripcion}» " +
                              $"y se restaron del inventario.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Incluye artículos desactivados: también se pueden dar de baja (por ejemplo, al botar lo que ya no se vende).</summary>
    private async Task LlenarListas(PerdidaNuevaViewModel vm)
    {
        vm.Articulos = await _articulos.ListarAsync(new FiltroInventario { IncluirInactivos = true });
        vm.Motivos = await _perdidas.ListarMotivosAsync();
    }

    // ---------------------------------------------------------------- Corregir un error (solo el mismo día)
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(int id)
    {
        var perdida = await _perdidas.ObtenerAsync(id);
        string? error = await _perdidas.EliminarAsync(id);
        if (error is null)
            TempData["Mensaje"] = $"Se eliminó el registro y {perdida?.Cantidad} {(perdida?.Cantidad == 1 ? "unidad regresó" : "unidades regresaron")} " +
                                  $"al inventario de «{perdida?.Articulo}».";
        else
            TempData["Error"] = error;
        return RedirectToAction(nameof(Index));
    }

    // ---------------------------------------------------------------- Motivos
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> NuevoMotivo(string? descripcion)
    {
        descripcion = Validaciones.LimpiarTexto(descripcion);
        if (string.IsNullOrEmpty(descripcion) || descripcion.Length < 3 || descripcion.Length > 150)
            TempData["Error"] = "El motivo debe tener entre 3 y 150 caracteres.";
        else if (await _perdidas.ExisteMotivoAsync(descripcion))
            TempData["Error"] = $"Ya existe el motivo «{descripcion}».";
        else
        {
            try
            {
                await _perdidas.CrearMotivoAsync(descripcion);
                TempData["Mensaje"] = $"Se agregó el motivo «{descripcion}».";
            }
            catch (SqlException ex) when (SqlErrores.EsDuplicado(ex))
            {
                TempData["Error"] = $"Ya existe el motivo «{descripcion}».";
            }
        }
        return RedirectToAction(nameof(Index));
    }
}
