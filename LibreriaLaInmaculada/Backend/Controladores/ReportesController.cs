using LibreriaLaInmaculada.Backend.AccesoDatos;
using LibreriaLaInmaculada.Backend.Modelos;
using LibreriaLaInmaculada.Backend.Seguridad;
using LibreriaLaInmaculada.Frontend.Ayudantes;
using LibreriaLaInmaculada.Frontend.ModelosDeVista;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibreriaLaInmaculada.Backend.Controladores;

/// <summary>Reportes: solo el Administrador. Todo lo que pasó en un período, en una sola pantalla.</summary>
[Authorize(Roles = Roles.Administrador)]
public class ReportesController : Controller
{
    private readonly ReporteRepositorio _reportes;
    private readonly PerdidaRepositorio _perdidas;

    public ReportesController(ReporteRepositorio reportes, PerdidaRepositorio perdidas)
    {
        _reportes = reportes;
        _perdidas = perdidas;
    }

    /// <param name="periodo">"mes", "mesanterior", "anio" (por defecto) o "todo"</param>
    /// <param name="desde">fechas propias: si vienen, mandan sobre el período</param>
    public async Task<IActionResult> Index(string? periodo, DateTime? desde, DateTime? hasta)
    {
        var hoy = DateTime.Today;
        var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
        string nombre;

        if (desde is not null || hasta is not null)
        {
            periodo = "personalizado";
            if (desde > hasta) (desde, hasta) = (hasta, desde);
            nombre = (desde, hasta) switch
            {
                (not null, not null) => $"Del {Formato.Fecha(desde.Value)} al {Formato.Fecha(hasta.Value)}",
                (not null, null) => $"Desde el {Formato.Fecha(desde.Value)}",
                _ => $"Hasta el {Formato.Fecha(hasta!.Value)}"
            };
        }
        else
        {
            switch (periodo)
            {
                case "mes":
                    desde = inicioMes; hasta = hoy;
                    nombre = $"{Mayuscula(Formato.Mes(hoy.Month))} {hoy.Year}";
                    break;
                case "mesanterior":
                    desde = inicioMes.AddMonths(-1); hasta = inicioMes.AddDays(-1);
                    nombre = $"{Mayuscula(Formato.Mes(desde.Value.Month))} {desde.Value.Year}";
                    break;
                case "todo":
                    desde = null; hasta = null;
                    nombre = "Todo el historial";
                    break;
                default:
                    periodo = "anio";
                    desde = new DateTime(hoy.Year, 1, 1); hasta = hoy;
                    nombre = $"Año {hoy.Year}";
                    break;
            }
        }

        // Períodos de hasta 2 meses se grafican por día; los más largos, por mes
        bool porDia = desde is not null && hasta is not null && (hasta.Value - desde.Value).TotalDays <= 62;

        var puntos = await _reportes.VentasPorPeriodoAsync(desde, hasta, porDia);
        var vm = new ReportesViewModel
        {
            Periodo = periodo,
            Desde = desde,
            Hasta = hasta,
            NombrePeriodo = nombre,
            PorDia = porDia,
            Resumen = await _reportes.ObtenerResumenAsync(desde, hasta),
            VentasPorPeriodo = CompletarPeriodos(puntos, desde, hasta, porDia),
            MasVendidos = await _reportes.MasVendidosAsync(desde, hasta),
            PorCategoria = await _reportes.PorCategoriaAsync(desde, hasta),
            PorVendedor = await _reportes.PorVendedorAsync(desde, hasta),
            ComprasPorProveedor = await _reportes.ComprasPorProveedorAsync(desde, hasta),
            PerdidasPorMotivo = await _perdidas.TotalizarPorMotivoAsync(new FiltroPerdidas { Desde = desde, Hasta = hasta }),
            PorReabastecer = await _reportes.PorReabastecerAsync(),
            SinMovimiento = await _reportes.SinMovimientoAsync(desde, hasta)
        };
        return View(vm);
    }

    private static string Mayuscula(string texto) => char.ToUpper(texto[0]) + texto[1..];

    /// <summary>
    /// Agrega en cero los días o meses sin ventas, para que la gráfica no tenga huecos.
    /// Si el período no tiene inicio o fin, se usa la primera y la última venta.
    /// </summary>
    private static List<PuntoVentas> CompletarPeriodos(List<PuntoVentas> puntos, DateTime? desde, DateTime? hasta, bool porDia)
    {
        if (puntos.Count == 0 && (desde is null || hasta is null)) return puntos;

        DateTime inicio = desde ?? puntos.First().Periodo;
        DateTime fin = hasta ?? puntos.Last().Periodo;
        if (!porDia)
        {
            inicio = new DateTime(inicio.Year, inicio.Month, 1);
            fin = new DateTime(fin.Year, fin.Month, 1);
        }

        var porFecha = puntos.ToDictionary(p => p.Periodo.Date);
        var lista = new List<PuntoVentas>();
        for (var f = inicio.Date; f <= fin && lista.Count < 400; f = porDia ? f.AddDays(1) : f.AddMonths(1))
            lista.Add(porFecha.TryGetValue(f, out var p) ? p : new PuntoVentas { Periodo = f });
        return lista;
    }
}
