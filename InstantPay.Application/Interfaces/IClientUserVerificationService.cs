using InstantPay.Application.DTOs;

namespace InstantPay.Application.Interfaces;

public interface IClientUserVerificationService
{
    Task<ClientUserVerificationResponse> SendPhoneOtpAsync(string phone);
    Task<ClientUserVerificationResponse> SendEmailOtpAsync(string email);
    Task<ClientUserVerificationResponse> VerifyOtpAsync(VerifyClientUserOtpRequest request);
    Task<ClientUserVerificationResponse> VerifyPanAsync(string panNumber, int clientId = 0);
    Task<ClientUserVerificationResponse> VerifyAadhaarAsync(string aadhaarNumber, int clientId = 0);
    bool ValidateProof(string? token, string type, string value);
    string? GetVerifiedName(string? token);
    void ConsumeProof(string? token);
}
