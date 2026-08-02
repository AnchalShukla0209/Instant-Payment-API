namespace InstantPay.Application.DTOs;

public class SenderLoginRequestDto
{
    public string UserId { get; set; } = string.Empty;
    public string APIKey { get; set; } = string.Empty;
    public string SenderMobile { get; set; } = string.Empty;
}

public class SenderRegistrationRequestDto
{
    public string UserId { get; set; } = string.Empty;
    public string APIKey { get; set; } = string.Empty;
    public string SenderMobile { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Pincode { get; set; } = string.Empty;
}

public class SenderEkycRequestDto
{
    public string UserId { get; set; } = string.Empty;
    public string APIKey { get; set; } = string.Empty;
    public string SenderMobile { get; set; } = string.Empty;
    public string OTP { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
}

public class SenderResponseDto
{
    public string first_name { get; set; } = string.Empty;
    public string state { get; set; } = string.Empty;
}

public class SenderApiResponseDto
{
    public string Status_Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public object Data { get; set; } = new();
}
