using LibreriaLaInmaculada.Backend.AccesoDatos;
using LibreriaLaInmaculada.Backend.Modelos;
using LibreriaLaInmaculada.Backend.Seguridad;
using LibreriaLaInmaculada.Frontend.ModelosDeVista;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace LibreriaLaInmaculada.Backend.Controladores;

/// <summary>
/// Inventario: los dos roles consultan la lista.
/// Crear, editar, activar/desactivar y las categorías son solo del Administrador.
/// </summary>
public class InventarioController : Controller
{
    private readonly ArticuloRepositorio _articulos;
    private readonly CategoriaRepositorio _categorias;

    public InventarioController(ArticuloRepositorio articulos, CategoriaRepositorio categorias)
    {
        _articulos = articulos;
        _categorias = categorias;
    }

    // ---------------------------------------------------------------- Lista
    public async Task<IActionResult> Index(string? buscar, int? categoria, string? estado)
    {
        bool esAdmin = User.EsAdministrador();
        if (estado is not ("bajo" or "agotado" or "inactivos")) estado = null;
        if (!esAdmin && estado == "inactivos") estado = null;   // el vendedor no ve inactivos

        var filtro = new FiltroInventario
        {
            Buscar = string.IsNullOrWhiteSpace(buscar) ? null : buscar.Trim(),
            IdCategoria = categoria,
            Estado = estado,
            IncluirInactivos = estado == "inactivos"
        };

        var resumen = await _articulos.ObtenerResumenAsync();
        var vm = new InventarioIndexViewModel
        {
            Articulos = await _articulos.ListarAsync(filtro),
            Categorias = await _categorias.ListarAsync(incluirInactivas: false),
            Buscar = filtro.Buscar,
            IdCategoria = categoria,
            Estado = estado,
            TotalActivos = resumen.Activos,
            BajoStock = resumen.BajoStock,
            Agotados = resumen.Agotados,
            ValorInventario = resumen.ValorInventario
        };
        return View(vm);
    }

    // ---------------------------------------------------------------- Crear / editar artículo
    [Authorize(Roles = Roles.Administrador)]
    [HttpGet]
    public async Task<IActionResult> Nuevo()
    {
        var vm = new ArticuloFormViewModel { Categorias = await CategoriasParaFormulario(null) };
        return View("Formulario", vm);
    }

    [Authorize(Roles = Roles.Administrador)]
    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Nuevo(ArticuloFormViewModel vm)
    {
        vm.IdArticulo = 0;
        return Guardar(vm);
    }

    [Authorize(Roles = Roles.Administrador)]
    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var a = await _articulos.ObtenerAsync(id);
        if (a is null) return NotFound();

        var vm = new ArticuloFormViewModel
        {
            IdArticulo = a.IdArticulo,
            Descripcion = a.Descripcion,
            IdCategoria = a.IdCategoria,
            StockMinimo = a.StockMinimo,
            PrecioCompra = a.PrecioCompra,
            PrecioVenta = a.PrecioVenta,
            Cantidad = a.Cantidad,
            Activo = a.Activo,
            Categorias = await CategoriasParaFormulario(a.IdCategoria)
        };
        return View("Formulario", vm);
    }

    [Authorize(Roles = Roles.Administrador)]
    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Editar(ArticuloFormViewModel vm) => Guardar(vm);

    private async Task<IActionResult> Guardar(ArticuloFormViewModel vm)
    {
        vm.Descripcion = Validaciones.LimpiarTexto(vm.Descripcion);

        // El precio de venta no puede ser menor que lo que cuesta el artículo
        if (vm.PrecioCompra is not null && vm.PrecioVenta is not null && vm.PrecioVenta < vm.PrecioCompra)
            ModelState.AddModelError(nameof(vm.PrecioVenta),
                $"El precio de venta no puede ser menor que el costo (C$ {vm.PrecioCompra:N2}): se vendería con pérdida.");

        Articulo? actual = null;
        if (!vm.EsNuevo)
        {
            actual = await _articulos.ObtenerAsync(vm.IdArticulo);
            if (actual is null) return NotFound();
        }

        if (ModelState.IsValid && await _articulos.ExisteDescripcionAsync(vm.Descripcion!, vm.EsNuevo ? null : vm.IdArticulo))
            ModelState.AddModelError(nameof(vm.Descripcion), "Ya existe un artículo con esa descripción.");

        if (ModelState.IsValid)
        {
            var articulo = new Articulo
            {
                IdArticulo = vm.IdArticulo,
                IdCategoria = vm.IdCategoria!.Value,
                Descripcion = vm.Descripcion!,
                StockMinimo = vm.StockMinimo!.Value,
                PrecioCompra = vm.PrecioCompra!.Value,
                PrecioVenta = vm.PrecioVenta!.Value
            };
            try
            {
                if (vm.EsNuevo)
                {
                    await _articulos.CrearAsync(articulo);
                    TempData["Mensaje"] = $"Se agregó «{articulo.Descripcion}» al inventario con stock 0. El stock entra al registrar una compra.";
                }
                else
                {
                    await _articulos.ActualizarAsync(articulo);
                    TempData["Mensaje"] = $"Se guardaron los cambios de «{articulo.Descripcion}».";
                }
                return RedirectToAction(nameof(Index));
            }
            catch (SqlException ex) when (SqlErrores.EsDuplicado(ex))
            {
                ModelState.AddModelError(nameof(vm.Descripcion), "Ya existe un artículo con esa descripción.");
            }
        }

        // Hubo errores: se vuelve a mostrar el formulario con lo que se escribió
        vm.Categorias = await CategoriasParaFormulario(vm.IdCategoria);
        vm.Cantidad = actual?.Cantidad ?? 0;
        vm.Activo = actual?.Activo ?? true;
        return View("Formulario", vm);
    }

    [Authorize(Roles = Roles.Administrador)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(int id)
    {
        var a = await _articulos.ObtenerAsync(id);
        if (a is null) return NotFound();

        await _articulos.CambiarEstadoAsync(id, !a.Activo);
        TempData["Mensaje"] = a.Activo
            ? $"«{a.Descripcion}» se desactivó: ya no aparece para vender. Su historial se conserva."
            : $"«{a.Descripcion}» se activó de nuevo.";
        return RedirectToAction(nameof(Index), new { estado = a.Activo ? null : "inactivos" });
    }

    /// <summary>Categorías activas, más la del artículo aunque esté inactiva.</summary>
    private async Task<List<Categoria>> CategoriasParaFormulario(int? idActual)
    {
        var todas = await _categorias.ListarAsync(incluirInactivas: true);
        return todas.Where(c => c.Activo || c.IdCategoria == idActual).ToList();
    }

    // ---------------------------------------------------------------- Categorías
    [Authorize(Roles = Roles.Administrador)]
    [HttpGet]
    public async Task<IActionResult> Categorias()
    {
        var vm = new CategoriasViewModel { Categorias = await _categorias.ListarAsync(incluirInactivas: true) };
        return View(vm);
    }

    [Authorize(Roles = Roles.Administrador)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> NuevaCategoria(string? nombre)
    {
        nombre = Validaciones.LimpiarTexto(nombre);
        if (string.IsNullOrEmpty(nombre) || nombre.Length < 3 || nombre.Length > 80)
            TempData["Error"] = "El nombre de la categoría debe tener entre 3 y 80 caracteres.";
        else if (await _categorias.ExisteNombreAsync(nombre, null))
            TempData["Error"] = $"Ya existe la categoría «{nombre}».";
        else
        {
            await _categorias.CrearAsync(nombre);
            TempData["Mensaje"] = $"Se agregó la categoría «{nombre}».";
        }
        return RedirectToAction(nameof(Categorias));
    }

    [Authorize(Roles = Roles.Administrador)]
    [HttpGet]
    public async Task<IActionResult> EditarCategoria(int id)
    {
        var c = await _categorias.ObtenerAsync(id);
        if (c is null) return NotFound();
        return View(new CategoriaFormViewModel { IdCategoria = c.IdCategoria, Nombre = c.Nombre });
    }

    [Authorize(Roles = Roles.Administrador)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditarCategoria(CategoriaFormViewModel vm)
    {
        vm.Nombre = Validaciones.LimpiarTexto(vm.Nombre);
        if (await _categorias.ObtenerAsync(vm.IdCategoria) is null) return NotFound();

        if (ModelState.IsValid && await _categorias.ExisteNombreAsync(vm.Nombre!, vm.IdCategoria))
            ModelState.AddModelError(nameof(vm.Nombre), "Ya existe otra categoría con ese nombre.");
        if (!ModelState.IsValid)
            return View(vm);

        await _categorias.ActualizarAsync(vm.IdCategoria, vm.Nombre!);
        TempData["Mensaje"] = $"La categoría ahora se llama «{vm.Nombre}».";
        return RedirectToAction(nameof(Categorias));
    }

    [Authorize(Roles = Roles.Administrador)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstadoCategoria(int id)
    {
        var c = await _categorias.ObtenerAsync(id);
        if (c is null) return NotFound();

        await _categorias.CambiarEstadoAsync(id, !c.Activo);
        TempData["Mensaje"] = c.Activo
            ? $"La categoría «{c.Nombre}» se desactivó: ya no se puede elegir para artículos nuevos."
            : $"La categoría «{c.Nombre}» se activó de nuevo.";
        return RedirectToAction(nameof(Categorias));
    }
}
