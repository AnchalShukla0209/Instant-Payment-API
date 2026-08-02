-- Adds the ApiRequest and ApiMsg columns required by the RechargeKIT payout integration in SettlementService.
-- Run this against the InstantPayment_Db database before deploying the updated code.

ALTER TABLE [InstantPayment_Db].[SettlementWithdrawals]
ADD ApiRequest VARCHAR(MAX) NULL,
    ApiMsg VARCHAR(MAX) NULL;
GO
