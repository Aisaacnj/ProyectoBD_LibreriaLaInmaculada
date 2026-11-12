using System.Data;
using LibreriaLaInmaculada.Frontend.ModelosDeVista;
using Microsoft.Data.SqlClient;

namespace LibreriaLaInmaculada.Backend.AccesoDatos;

/// <summary>Consultas de la pantalla de Inicio. Solo lectura, sobre las vistas de la base.</summary>
public class InicioRepositorio
{
    private readonly Db _db;

    public InicioRepositorio(Db db) => _db = db;

    public async Task<InicioAdminViewModel> ObtenerResumenAdminAsync()
    {
        var vm = new InicioAdminViewModel();
        await using var cn = await _db.AbrirAsync();

        // 1. Mes a resumir: el de la última venta activa (si no hay ventas, el mes actual)
        await using (var cmd = new SqlCommand("SELECT MAX(Fecha_Venta) FROM dbo.Venta WHERE Estado = 'Activa';", cn))
        {
            object? r = await cmd.ExecuteScalarAsync();
            DateTime ultima = r is DateTime d ? d : DateTime.Today;
            vm.MesReferencia = new DateTime(ultima.Year, ultima.Month, 1);
        }
        DateTime inicio = vm.MesReferencia;
        DateTime fin = inicio.AddMonths(1);

        // 2. Ventas, monto vendido y ganancia del mes
        const string sqlMes = @"
            SELECT COUNT(DISTINCT idVenta), ISNULL(SUM(Total), 0), ISNULL(SUM(Ganancia), 0)
            FROM dbo.V_VentaDetalle
            WHERE Estado = 'Activa' AND Fecha_Venta >= @Inicio AND Fecha_Venta < @Fin;";
        await using (var cmd = new SqlCommand(sqlMes, cn))
        {
            cmd.Parameters.Add("@Inicio", SqlDbType.DateTime2).Value = inicio;
            cmd.Parameters.Add("@Fin", SqlDbType.DateTime2).Value = fin;
            await using var rd = await cmd.ExecuteReaderAsync();
            if (await rd.ReadAsync())
            {
                vm.VentasMes = rd.GetInt32(0);
                vm.VendidoMes = rd.GetDecimal(1);
                vm.GananciaMes = rd.GetDecimal(2);
            }
        }

        // 3. Ventas por mes del año
        const string sqlAnio = @"
            SELECT MONTH(Fecha_Venta) AS Mes, SUM(Total) AS Total
            FROM dbo.Venta
            WHERE Estado = 'Activa' AND YEAR(Fecha_Venta) = @Anio
            GROUP BY MONTH(Fecha_Venta);";
        var porMes = new Dictionary<int, decimal>();
        await using (var cmd = new SqlCommand(sqlAnio, cn))
        {
            cmd.Parameters.Add("@Anio", SqlDbType.Int).Value = inicio.Year;
            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
                porMes[rd.GetInt32(0)] = rd.GetDecimal(1);
        }
        for (int m = 1; m <= inicio.Month; m++)
            vm.VentasPorMes.Add(new VentaMes { Mes = m, Total = porMes.GetValueOrDefault(m) });

        // 4. Los 5 artículos más vendidos del año (en unidades)
        const string sqlTop = @"
            SELECT TOP (5) Nombre_Articulo, SUM(Cantidad) AS Unidades
            FROM dbo.V_VentaDetalle
            WHERE Estado = 'Activa' AND YEAR(Fecha_Venta) = @Anio
            GROUP BY Nombre_Articulo
            ORDER BY Unidades DESC;";
        await using (var cmd = new SqlCommand(sqlTop, cn))
        {
            cmd.Parameters.Add("@Anio", SqlDbType.Int).Value = inicio.Year;
            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
                vm.MasVendidos.Add(new ArticuloTop { Nombre = rd.GetString(0), Unidades = rd.GetInt32(1) });
        }

        // 5. Artículos por reabastecer
        vm.PorReabastecer = await ObtenerPorReabastecerAsync(cn);
        return vm;
    }

    public async Task<InicioVendedorViewModel> ObtenerResumenVendedorAsync(int idUsuario)
    {
        var vm = new InicioVendedorViewModel { Hoy = DateTime.Today };
        await using var cn = await _db.AbrirAsync();

        // 1. Ventas de hoy de este vendedor
        const string sqlHoy = @"
            SELECT COUNT(*), ISNULL(SUM(Total), 0)
            FROM dbo.Venta
            WHERE Estado = 'Activa' AND idUsuario = @Usuario
              AND Fecha_Venta >= @Hoy AND Fecha_Venta < @Manana;";
        await using (var cmd = new SqlCommand(sqlHoy, cn))
        {
            cmd.Parameters.Add("@Usuario", SqlDbType.Int).Value = idUsuario;
            cmd.Parameters.Add("@Hoy", SqlDbType.DateTime2).Value = vm.Hoy;
            cmd.Parameters.Add("@Manana", SqlDbType.DateTime2).Value = vm.Hoy.AddDays(1);
            await using var rd = await cmd.ExecuteReaderAsync();
            if (await rd.ReadAsync())
            {
                vm.VentasHoy = rd.GetInt32(0);
                vm.VendidoHoy = rd.GetDecimal(1);
            }
        }

        // 2. Sus últimas 5 ventas
        const string sqlUltimas = @"
            SELECT TOP (5) idVenta, Fecha_Venta, Total, Estado
            FROM dbo.Venta
            WHERE idUsuario = @Usuario
            ORDER BY Fecha_Venta DESC, idVenta DESC;";
        await using (var cmd = new SqlCommand(sqlUltimas, cn))
        {
            cmd.Parameters.Add("@Usuario", SqlDbType.Int).Value = idUsuario;
            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
                vm.UltimasVentas.Add(new VentaResumen
                {
                    IdVenta = rd.GetInt32(0),
                    Fecha = rd.GetDateTime(1),
                    Total = rd.GetDecimal(2),
                    Estado = rd.GetString(3)
                });
        }

        // 3. Artículos por reabastecer (para avisar a los dueños)
        vm.PorReabastecer = await ObtenerPorReabastecerAsync(cn);
        return vm;
    }

    private static async Task<List<ArticuloAlerta>> ObtenerPorReabastecerAsync(SqlConnection cn)
    {
        const string sql = @"
            SELECT Descripcion, Stock_Disponible, Stock_Minimo
            FROM dbo.V_Articulos
            WHERE Bajo_Stock = 1 AND Activo = 1
            ORDER BY Stock_Disponible, Descripcion;";
        var lista = new List<ArticuloAlerta>();
        await using var cmd = new SqlCommand(sql, cn);
        await using var rd = await cmd.ExecuteReaderAsync();
        while (await rd.ReadAsync())
            lista.Add(new ArticuloAlerta
            {
                Descripcion = rd.GetString(0),
                Stock = rd.GetInt32(1),
                StockMinimo = rd.GetInt32(2)
            });
        return lista;
    }
}
