using System.ComponentModel.DataAnnotations;
using LibreriaLaInmaculada.Backend.Modelos;

namespace LibreriaLaInmaculada.Frontend.ModelosDeVista;

/// <summary>Pantalla Pérdidas: lista, filtros, totales por motivo y motivos.</summary>
public class PerdidasIndexViewModel
{
    public List<Perdida> Perdidas { get; set; } = new();
    public List<PerdidaPorMotivo> PorMotivo { get; set; } = new();
    public List<MotivoPerdida> Motivos { get; set; } = new();

    // Filtros aplicados
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }
    public int? IdMotivo { get; set; }
    public string? Buscar { get; set; }

    public int TotalUnidades => Perdidas.Sum(p => p.Cantidad);
    public decimal TotalCosto => Perdidas.Sum(p => p.CostoAproximado);
}

/// <summary>Formulario para registrar una pérdida.</summary>
public class PerdidaNuevaViewModel
{
    [Required(ErrorMessage = "Elija el artículo de la lista.")]
    [Display(Name = "Artículo")]
    public int? IdArticulo { get; set; }

    [Required(ErrorMessage = "Elija el motivo.")]
    [Display(Name = "Motivo")]
    public int? IdMotivo { get; set; }

    [Required(ErrorMessage = "Escriba cuántas unidades se pierden.")]
    [Range(1, Validaciones.CantidadMaxima, ErrorMessage = "La cantidad debe estar entre 1 y 10,000.")]
    [Display(Name = "Cantidad")]
    public int? Cantidad { get; set; }

    [StringLength(255, ErrorMessage = "La observación puede tener hasta 255 caracteres.")]
    [Display(Name = "Observación (opcional)")]
    public string? Observacion { get; set; }

    // Para el buscador y la lista de motivos
    public List<Articulo> Articulos { get; set; } = new();
    public List<MotivoPerdida> Motivos { get; set; } = new();
}
