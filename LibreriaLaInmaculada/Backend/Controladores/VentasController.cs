using System.Globalization;
using LibreriaLaInmaculada.Backend.AccesoDatos;
using LibreriaLaInmaculada.Backend.Modelos;
using LibreriaLaInmaculada.Backend.Seguridad;
using LibreriaLaInmaculada.Frontend.ModelosDeVista;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibreriaLaInmaculada.Backend.Controladores;

/// <summary>
/// Ventas: las usan los dos roles.
///   - Vendedor: vende, ve SOLO sus ventas y su comprobante, sin costos ni ganancias.
///   - Administrador: ve todas las ventas con su ganancia, y puede anular o registrar devoluciones.
/// </summary>
public class VentasController : Controller
{
    private readonly VentaRepositorio _ventas;
    private readonly ArticuloRepositorio _articulos;
    private readonly UsuarioRepositorio _usuarios;

    public VentasController(VentaRepositorio ventas, ArticuloRepositorio articulos, UsuarioRepositorio usuarios)
    {
        _ventas = ventas;
        _articulos = articulos;
        _usuarios = usuarios;
    }

    // ---------------------------------------------------------------- Lista
    public async Task<IActionResult> Index(DateTime? desde, DateTime? hasta, int? vendedor, string? estado)
    {
        bool esAdmin = User.EsAdministrador();
        if (estado is not ("Anulada" or "Todas")) estado = "Activa";
        if (desde > hasta) (desde, hasta) = (hasta, desde);       // si las pusieron al revés

        // El vendedor solo ve sus propias ventas, aunque cambie la dirección de la página
        int? idUsuario = esAdmin ? vendedor : User.IdUsuario();

        var filtro = new FiltroVentas { Desde = desde, Hasta = hasta, IdUsuario = idUsuario, Estado = estado };
        var totales = await _ventas.TotalizarAsync(filtro);
        var resumen = await _ventas.ObtenerResumenAsync(esAdmin ? null : User.IdUsuario());

        var vm = new VentasIndexViewModel
        {
            Ventas = await _ventas.ListarAsync(filtro),
            Vendedores = esAdmin ? await _usuarios.ListarAsync() : new(),
            EsAdmin = esAdmin,
            Desde = desde,
            Hasta = hasta,
            IdVendedor = esAdmin ? vendedor : null,
            Estado = estado,
            Limite = filtro.Limite,
            TotalEncontradas = totales.Cantidad,
            TotalVendido = totales.Total,
            TotalGanancia = totales.Ganancia,
            VentasHoy = resumen.VentasHoy,
            VendidoHoy = resumen.VendidoHoy,
            VendidoMes = resumen.VendidoMes,
            GananciaMes = resumen.GananciaMes
        };
        return View(vm);
    }

    // ---------------------------------------------------------------- Vender
    [HttpGet]
    public async Task<IActionResult> Nueva()
    {
        var vm = new VentaNuevaViewModel { Articulos = await ArticulosParaVender() };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Nueva(VentaNuevaViewModel vm)
    {
        vm.Articulos = await ArticulosParaVender();

        // Las líneas se revisan aquí abajo con mensajes propios
        foreach (var clave in ModelState.Keys.Where(k => k.StartsWith("Lineas")).ToList())
            ModelState.Remove(clave);

        vm.Lineas = vm.Lineas.Where(l => l.IdArticulo is not null).ToList();

        if (vm.Lineas.Count == 0)
            ModelState.AddModelError("", "Agregue al menos un artículo a la venta.");

        decimal total = 0;
        foreach (var l in vm.Lineas)
        {
            var a = vm.Articulos.FirstOrDefault(x => x.IdArticulo == l.IdArticulo);
            if (a is null)
            {
                ModelState.AddModelError("", "Uno de los artículos ya no existe o está desactivado.");
                continue;
            }
            if (l.Cantidad is null || l.Cantidad < 1 || l.Cantidad > Validaciones.CantidadMaximaVenta)
                ModelState.AddModelError("", $"«{a.Descripcion}»: la cantidad debe estar entre 1 y {Validaciones.CantidadMaximaVenta:N0}.");
            else if (a.Cantidad == 0)
                ModelState.AddModelError("", $"«{a.Descripcion}» está agotado.");
            else if (l.Cantidad > a.Cantidad)
                ModelState.AddModelError("", $"De «{a.Descripcion}» solo quedan {a.Cantidad}.");
            else
                total += Math.Round(l.Cantidad.Value * a.PrecioVenta, 2, MidpointRounding.AwayFromZero);
        }

        foreach (var repetido in vm.Lineas.GroupBy(l => l.IdArticulo).Where(g => g.Count() > 1))
        {
            var nombre = vm.Articulos.FirstOrDefault(a => a.IdArticulo == repetido.Key)?.Descripcion ?? "un artículo";
            ModelState.AddModelError("", $"«{nombre}» aparece {repetido.Count()} veces: déjelo una sola vez y sume las cantidades.");
        }

        if (ModelState.IsValid && vm.PagaCon is not null && vm.PagaCon < total)
            ModelState.AddModelError(nameof(vm.PagaCon), $"El pago no alcanza: el total es {Cordobas(total)}.");

        if (!ModelState.IsValid)
            return View(vm);

        // La base vuelve a revisar precio y stock con bloqueo, por si otra persona vendió al mismo tiempo
        var resultado = await _ventas.RegistrarAsync(
            vm.Lineas.Select(l => new LineaVenta { IdArticulo = l.IdArticulo!.Value, Cantidad = l.Cantidad!.Value }).ToList(),
            User.IdUsuario());

        if (!resultado.Exito)
        {
            foreach (var error in resultado.Errores)
                ModelState.AddModelError("", error);
            vm.Articulos = await ArticulosParaVender();   // stock actualizado
            return View(vm);
        }

        TempData["Mensaje"] = $"Venta #{resultado.IdVenta} registrada por {Cordobas(resultado.Total)}.";
        if (vm.PagaCon is not null)
            TempData["PagaCon"] = vm.PagaCon.Value.ToString(CultureInfo.InvariantCulture);
        return RedirectToAction(nameof(Detalle), new { id = resultado.IdVenta });
    }

    private static string Cordobas(decimal valor) => "C$ " + valor.ToString("N2", CultureInfo.GetCultureInfo("es-NI"));

    /// <summary>Artículos activos (con y sin stock: los agotados se muestran, pero no se dejan agregar).</summary>
    private Task<List<Articulo>> ArticulosParaVender() => _articulos.ListarAsync(new FiltroInventario());

    // ---------------------------------------------------------------- Comprobante
    [HttpGet]
    public async Task<IActionResult> Detalle(int id)
    {
        var venta = await _ventas.ObtenerAsync(id);
        if (venta is null) return NotFound();

        bool esAdmin = User.EsAdministrador();
        if (!esAdmin && venta.IdUsuario != User.IdUsuario())
            return Forbid();   // un vendedor no puede ver ventas de otro vendedor

        decimal? pagaCon = null;
        if (TempData["PagaCon"] is string texto && decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var p))
            pagaCon = p;

        return View(new VentaDetalleViewModel { Venta = venta, EsAdmin = esAdmin, PagaCon = pagaCon });
    }

    // ---------------------------------------------------------------- Anular (solo Administrador)
    [Authorize(Roles = Roles.Administrador)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Anular(int id)
    {
        string? error = await _ventas.AnularAsync(id);
        if (error is null)
            TempData["Mensaje"] = $"La venta #{id} quedó anulada y sus unidades regresaron al inventario.";
        else
            TempData["Error"] = error;
        return RedirectToAction(nameof(Detalle), new { id });
    }

    // ---------------------------------------------------------------- Devolución parcial (solo Administrador)
    /// <summary>devolver[idArticulo] = unidades que devuelve el cliente.</summary>
    [Authorize(Roles = Roles.Administrador)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Devolver(int id, Dictionary<int, int?> devolver)
    {
        var cantidades = devolver.ToDictionary(d => d.Key, d => d.Value ?? 0);
        var (error, ventaNueva, devueltas) = await _ventas.DevolverAsync(id, cantidades);

        if (error is not null)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Detalle), new { id });
        }

        string unidades = devueltas == 1 ? "1 unidad" : $"{devueltas} unidades";
        if (ventaNueva is null)
        {
            TempData["Mensaje"] = $"El cliente devolvió todo ({unidades}): la venta #{id} quedó anulada y las unidades regresaron al inventario.";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        TempData["Mensaje"] = $"Devolución registrada: {unidades} regresaron al inventario. La venta #{id} quedó anulada " +
                              $"y esta venta #{ventaNueva} tiene lo que el cliente se quedó. Si algo regresó dañado, regístrelo en Pérdidas.";
        return RedirectToAction(nameof(Detalle), new { id = ventaNueva });
    }
}
