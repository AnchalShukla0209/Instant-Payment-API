using InstantPay.Application.DTOs;

namespace InstantPay.Application.Interfaces;

public interface ISenderService
{
    Task<SenderApiResponseDto> SenderLoginAsync(SenderLoginRequestDto request);
    Task<SenderApiResponseDto> SenderRegistrationAsync(SenderRegistrationRequestDto request);
    Task<SenderApiResponseDto> SenderEkycAsync(SenderEkycRequestDto request);
}
