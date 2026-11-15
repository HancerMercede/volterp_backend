namespace Volterp.ElectronicInvoicing.Printing;

// The units of measure the DGII codes in the "UnidadMedida" field, with the
// name the printed representation writes: the unit of measure of a line is
// indicated in words ("Informe Técnico e-CF v1.0", section 18.2.2). The list
// is the enumeration of "UnidadMedidaType" of the official schemas.
public static class EcfUnitsOfMeasure
{
    private static readonly string[] Names =
    [
        "Barril", "Bolsa", "Bote", "Bultos", "Botella", "Caja", "Cajetilla", "Centímetro", "Cilindro", "Conjunto",
        "Contenedor", "Día", "Docena", "Fardo", "Galones", "Grado", "Gramo", "Granel", "Hora", "Huacal",
        "Kilogramo", "Kilovatio Hora", "Libra", "Litro", "Lote", "Metro", "Metro Cuadrado", "Metro Cúbico",
        "Millones de Unidades Térmicas", "Minuto", "Paquete", "Par", "Pie", "Pieza", "Rollo", "Sobre", "Segundo",
        "Tanque", "Tonelada", "Tubo", "Yarda", "Yarda Cuadrada", "Unidad", "Elemento", "Millar", "Saco", "Lata",
        "Display", "Bidón", "Ración", "Quintal", "Toneladas de Registro Bruto", "Pie Cuadrado", "Pasajero", "Pulgadas",
        "Parqueo Barcos en Muelle", "Bandeja", "Hectárea", "Mililitro", "Miligramo", "Onzas", "Onzas Troy"
    ];

    // The name of a unit, or null when the code is not one of them.
    public static string? Of(int code)
        => code >= 1 && code <= Names.Length ? Names[code - 1] : null;
}
