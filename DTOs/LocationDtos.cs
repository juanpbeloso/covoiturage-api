namespace SubiteAPI.DTOs;

public class LocalitySearchDto
{
    /// <summary>Texto de búsqueda (mín. 2 caracteres).</summary>
    public string? Q { get; set; }

    /// <summary>Nombre o ID de provincia en Georef (ej. "Buenos Aires").</summary>
    public string? Provincia { get; set; }

    /// <summary>Si true, filtra localidades dentro del radio del corredor configurado (Junín ↔ CABA por defecto).</summary>
    public bool UseCorridor { get; set; } = true;

    public int Max { get; set; } = 15;
}

public class LocalityDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lng { get; set; }
}

public class AddressSearchDto
{
    /// <summary>Calle y altura o punto de referencia (mín. 3 caracteres).</summary>
    public string? Direccion { get; set; }

    /// <summary>Localidad o barrio (ej. "Vicente López", "Junín").</summary>
    public string? Localidad { get; set; }

    /// <summary>Provincia opcional para acotar la búsqueda en Georef.</summary>
    public string? Provincia { get; set; }

    public bool UseCorridor { get; set; } = true;

    public int Max { get; set; } = 10;
}

public class NormalizedAddressDto
{
    public string Label { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string Locality { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lng { get; set; }
}
