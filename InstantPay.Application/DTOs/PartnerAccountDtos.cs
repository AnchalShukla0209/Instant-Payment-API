namespace InstantPay.Application.DTOs;

public sealed record PartnerAccountProfileDto(
    string Name,
    string Username,
    string Phone,
    string PanCard,
    string AadharCard);

public sealed record PartnerValidateAccountRequest(
    string? PanNo,
    string? AadharNo);

public sealed record PartnerChangePasswordRequest(
    string? OldPassword,
    string? NewPassword,
    string? ConfirmPassword,
    string? Otp);

public sealed record PartnerChangeMpinRequest(
    string? Mpin,
    string? Otp);

public sealed record PartnerChangeTxnPinRequest(
    string? TxnPin,
    string? Otp);
