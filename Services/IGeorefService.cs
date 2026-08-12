using SubiteAPI.DTOs;

namespace SubiteAPI.Services;

public interface IGeorefService
{
    Task<IReadOnlyList<LocalityDto>> SearchLocalitiesAsync(LocalitySearchDto search);
    Task<IReadOnlyList<NormalizedAddressDto>> SearchAddressesAsync(AddressSearchDto search);
}
