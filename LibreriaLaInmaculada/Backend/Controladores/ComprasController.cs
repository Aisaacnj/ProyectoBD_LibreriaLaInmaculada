using System.Globalization;
using LibreriaLaInmaculada.Backend.AccesoDatos;
using LibreriaLaInmaculada.Backend.Modelos;
using LibreriaLaInmaculada.Backend.Seguridad;
using LibreriaLaInmaculada.Frontend.ModelosDeVista;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace LibreriaLaInmaculada.Backend.Controladores;

/// <summary>
/// Compras a proveedores: solo el Administrador.
/// Registrar una compra SUMA al stock; anularla lo RESTA. Las compras nunca se borran.
/// </summary>
[Authorize(Roles = Roles.Administrador)]
public class ComprasController : Controller
{
    private static readonly decimal[] TasasIva = { 15m, 0m };

    private readonly CompraRepositorio _compras;
    private readonly ProveedorRepositorio _proveedores;
    private readonly ArticuloRepositorio _articulos;
    private readonly CategoriaRepositorio _categorias;

    public ComprasController(CompraRepositorio compras, ProveedorRepositorio proveedores,
                             ArticuloRepositorio articulos, CategoriaRepositorio categorias)
    {
        _compras = compras;
        _proveedores = proveedores;
        _articulos = articulos;
        _categorias = categorias;
    }

    // ---------------------------------------------------------------- Lista
    public async Task<IActionResult> Index(DateTime? desde, DateTime? hasta, int? proveedor, string? estado)
    {
        if (estado is not ("Anulada" or "Todas")) estado = "Activa";

        var filtro = new FiltroCompras { Desde = desde, Hasta = hasta, IdProveedor = proveedor, Estado = estado };
        var resumen = await _compras.ObtenerResumenMesAsync();

        var vm = new ComprasIndexViewModel
        {
            Compras = await _compras.ListarAsync(filtro),
            Proveedores = await _proveedores.ListarAsync(null, incluirInactivos: true),
            Desde = desde,
            Hasta = hasta,
            IdProveedor = proveedor,
            Estado = estado,
            ComprasMes = resumen.Cantidad,
            TotalMes = resumen.Total,
            IvaMes = resumen.IVA
        };
        return View(vm);
    }

    // ---------------------------------------------------------------- Registrar
    [HttpGet]
    public async Task<IActionResult> Nueva()
    {
        var vm = new CompraNuevaViewModel();
        await LlenarListas(vm);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Nueva(CompraNuevaViewModel vm)
    {
        vm.NumFactura = vm.NumFactura?.Trim().ToUpperInvariant();
        await LlenarListas(vm);

        // Las líneas se revisan aquí abajo con mensajes propios
        foreach (var clave in ModelState.Keys.Where(k => k.StartsWith("Lineas")).ToList())
            ModelState.Remove(clave);

        vm.Lineas = vm.Lineas.Where(l => !l.EstaVacia).ToList();

        // --- Encabezado
        if (vm.IdProveedor is not null && !vm.Proveedores.Any(p => p.IdProveedor == vm.IdProveedor))
            ModelState.AddModelError(nameof(vm.IdProveedor), "Elija un proveedor activo.");
        if (vm.FechaCompra > DateTime.Today)
            ModelState.AddModelError(nameof(vm.FechaCompra), "La fecha de la factura no puede ser futura.");
        else if (vm.FechaCompra < Validaciones.FechaMinima)
            ModelState.AddModelError(nameof(vm.FechaCompra), $"La fecha no puede ser anterior al {Validaciones.FechaMinima:dd/MM/yyyy}.");
        if (vm.IdProveedor is not null && !string.IsNullOrEmpty(vm.NumFactura)
            && await _compras.ExisteFacturaAsync(vm.IdProveedor.Value, vm.NumFactura))
            ModelState.AddModelError(nameof(vm.NumFactura), "Esa factura de ese proveedor ya está registrada.");

        // --- Líneas
        if (vm.Lineas.Count == 0)
            ModelState.AddModelError("", "Agregue al menos un artículo a la compra.");

        for (int i = 0; i < vm.Lineas.Count; i++)
        {
            var l = vm.Lineas[i];
            string fila = $"Fila {i + 1}";
            var articulo = vm.Articulos.FirstOrDefault(a => a.IdArticulo == l.IdArticulo);
            if (articulo is not null) fila += $" ({articulo.Descripcion})";

            if (l.IdArticulo is null)
                ModelState.AddModelError("", $"{fila}: elija el artículo.");
            else if (articulo is null)
                ModelState.AddModelError("", $"{fila}: el artículo no existe o está desactivado.");

            if (l.Cantidad is null || l.Cantidad <= 0 || l.Cantidad > Validaciones.CantidadMaxima)
                ModelState.AddModelError("", $"{fila}: la cantidad debe estar entre 1 y {Validaciones.CantidadMaxima:N0}.");
            if (l.PrecioUnitario is null || l.PrecioUnitario <= 0 || l.PrecioUnitario > Validaciones.CostoMaximo)
                ModelState.AddModelError("", $"{fila}: el costo por unidad (sin IVA) debe ser mayor que C$ 0 y hasta C$ {Validaciones.CostoMaximo:N0}.");
            if (l.PorcentajeIVA is null || !TasasIva.Contains(l.PorcentajeIVA.Value))
                ModelState.AddModelError("", $"{fila}: el IVA debe ser 15 % o exento.");
        }

        foreach (var repetido in vm.Lineas.Where(l => l.IdArticulo is not null)
                                          .GroupBy(l => l.IdArticulo).Where(g => g.Count() > 1))
        {
            var nombre = vm.Articulos.FirstOrDefault(a => a.IdArticulo == repetido.Key)?.Descripcion ?? "un artículo";
            ModelState.AddModelError("", $"«{nombre}» aparece en {repetido.Count()} filas: déjelo en una sola y sume las cantidades.");
        }

        if (!ModelState.IsValid)
            return View(vm);

        var compra = new Compra
        {
            IdProveedor = vm.IdProveedor!.Value,
            NumFactura = vm.NumFactura!,
            FechaCompra = vm.FechaCompra!.Value,
            Lineas = vm.Lineas.Select(l => new CompraDetalle
            {
                IdArticulo = l.IdArticulo!.Value,
                Cantidad = l.Cantidad!.Value,
                PrecioUnitario = l.PrecioUnitario!.Value,
                PorcentajeIVA = l.PorcentajeIVA!.Value
            }).ToList()
        };

        try
        {
            int idCompra = await _compras.RegistrarAsync(compra, User.IdUsuario());
            TempData["Mensaje"] = $"Se registró la compra #{idCompra}. Entraron {compra.Lineas.Sum(l => l.Cantidad):N0} unidades al inventario.";
            return RedirectToAction(nameof(Detalle), new { id = idCompra });
        }
        catch (SqlException ex) when (SqlErrores.EsDuplicado(ex))
        {
            ModelState.AddModelError(nameof(vm.NumFactura), "Esa factura de ese proveedor ya está registrada.");
            return View(vm);
        }
    }

    private async Task LlenarListas(CompraNuevaViewModel vm)
    {
        vm.Proveedores = await _proveedores.ListarAsync(null, incluirInactivos: false);
        vm.Articulos = await _articulos.ListarAsync(new FiltroInventario());   // solo activos
        vm.Categorias = await _categorias.ListarAsync(incluirInactivas: false);
    }

    // ---------------------------------------------------------------- Artículo nuevo (desde la compra)
    /// <summary>
    /// La llama la ventanita "Artículo nuevo" del formulario de compra, sin recargar la página.
    /// Aplica las mismas reglas que Inventario → Nuevo artículo y responde en JSON:
    ///   { ok: true, id, descripcion, costo }   o   { ok: false, errores: [...] }
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearArticulo(ArticuloFormViewModel vm)
    {
        vm.Descripcion = Validaciones.LimpiarTexto(vm.Descripcion);

        if (vm.PrecioCompra is not null && vm.PrecioVenta is not null && vm.PrecioVenta < vm.PrecioCompra)
            ModelState.AddModelError(nameof(vm.PrecioVenta), "El precio de venta no puede ser menor que el costo: se vendería con pérdida.");
        if (vm.IdCategoria is not null)
        {
            var categoria = await _categorias.ObtenerAsync(vm.IdCategoria.Value);
            if (categoria is null || !categoria.Activo)
                ModelState.AddModelError(nameof(vm.IdCategoria), "Elija una categoría activa.");
        }
        if (ModelState.IsValid && await _articulos.ExisteDescripcionAsync(vm.Descripcion!, null))
            ModelState.AddModelError(nameof(vm.Descripcion), $"Ya existe un artículo llamado «{vm.Descripcion}». Búsquelo en la lista de la fila.");

        if (!ModelState.IsValid)
            return Json(new { ok = false, errores = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Distinct() });

        var articulo = new Articulo
        {
            IdCategoria = vm.IdCategoria!.Value,
            Descripcion = vm.Descripcion!,
            StockMinimo = vm.StockMinimo!.Value,
            PrecioCompra = vm.PrecioCompra!.Value,
            PrecioVenta = vm.PrecioVenta!.Value
        };
        try
        {
            int id = await _articulos.CrearAsync(articulo);
            return Json(new
            {
                ok = true,
                id,
                descripcion = articulo.Descripcion,
                costo = articulo.PrecioCompra.ToString("0.00", CultureInfo.InvariantCulture)
            });
        }
        catch (SqlException ex) when (SqlErrores.EsDuplicado(ex))
        {
            return Json(new { ok = false, errores = new[] { $"Ya existe un artículo llamado «{articulo.Descripcion}»." } });
        }
    }

    // ---------------------------------------------------------------- Ver una compra
    [HttpGet]
    public async Task<IActionResult> Detalle(int id)
    {
        var compra = await _compras.ObtenerAsync(id);
        if (compra is null) return NotFound();
        return View(compra);
    }

    // ---------------------------------------------------------------- Anular
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Anular(int id)
    {
        string? error = await _compras.AnularAsync(id);
        if (error is null)
            TempData["Mensaje"] = $"La compra #{id} quedó anulada y sus unidades se restaron del inventario.";
        else
            TempData["Error"] = error;
        return RedirectToAction(nameof(Detalle), new { id });
    }
}
