using System.Globalization;

namespace LibreriaLaInmaculada.Frontend.Ayudantes;

/// <summary>Formatos de moneda y fecha en español de Nicaragua.</summary>
public static class Formato
{
    private static readonly CultureInfo Ni = CultureInfo.GetCultureInfo("es-NI");

    private static readonly string[] Meses =
        { "enero", "febrero", "marzo", "abril", "mayo", "junio",
          "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre" };

    private static readonly string[] MesesCortos =
        { "Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic" };

    private static readonly string[] Dias =
        { "Domingo", "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado" };

    /// <summary>1234.5 → "C$ 1,234.50"</summary>
    public static string Cordobas(decimal valor) => "C$ " + valor.ToString("N2", Ni);

    /// <summary>1234.5 → "C$ 1,235" (sin centavos, para tarjetas grandes)</summary>
    public static string CordobasRedondo(decimal valor) => "C$ " + valor.ToString("N0", Ni);

    public static string Mes(int mes) => Meses[mes - 1];

    public static string MesCorto(int mes) => MesesCortos[mes - 1];

    /// <summary>"Jueves 12 de noviembre de 2026"</summary>
    public static string FechaLarga(DateTime f) => $"{Dias[(int)f.DayOfWeek]} {f.Day} de {Meses[f.Month - 1]} de {f.Year}";

    /// <summary>"12/11/2026 7:42 a. m."</summary>
    public static string FechaHora(DateTime f) =>
        f.ToString("dd/MM/yyyy ", Ni) + f.ToString("h:mm", Ni) + (f.Hour < 12 ? " a. m." : " p. m.");

    /// <summary>Número para CSS (siempre con punto decimal).</summary>
    public static string Css(double valor) => valor.ToString("0.##", CultureInfo.InvariantCulture);
}
