using InstantPay.Infrastructure.Sql.Entities;

namespace InstantPay.Application.Interfaces
{
    public interface ICommissionService
    {
        Task<decimal> GetCommissionFromPlanAsync(
            int planId, decimal amount, int serviceId, string apiCode, string shareColumn, int? operatorId = null);

        Task DistributeCommissionAsync(
            TransactionDetail tx, TblUser user, decimal amount, int planId,
            int serviceId, string apiCode, string remarksPrefix, int? operatorId = null);
    }
}
