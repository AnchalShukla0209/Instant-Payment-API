using InstantPay.Application.DTOs;

namespace InstantPay.Application.Interfaces.PPI;

public interface IPPIMoneyTransferService
{
    Task<PPIMoneyTransferResponse> MoneyTransferAsync(PPIMoneyTransferRequest request);
}
