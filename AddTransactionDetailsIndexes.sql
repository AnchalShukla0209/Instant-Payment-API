-- Fix: SQL execution timeout on INSERT INTO TransactionDetails
-- Root cause: full table scan on duplicate-check queries caused lock escalation,
-- blocking concurrent INSERTs. These indexes convert the scan to an index seek.

USE [InstantPayment_Db];
GO

-- 1. Composite index for the duplicate-transaction check used in every DMT transfer:
--    WHERE UserId = @u AND ServiceName = @s AND ReqDate >= @d AND Amount = @a AND AccountNo = @n
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('InstantPayment_Db.TransactionDetails')
      AND name = 'IX_TransactionDetails_UserId_ServiceName_ReqDate'
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_TransactionDetails_UserId_ServiceName_ReqDate]
        ON [InstantPayment_Db].[TransactionDetails] ([UserId] ASC, [ServiceName] ASC, [ReqDate] DESC)
        INCLUDE ([Amount], [AccountNo])
    WITH (FILLFACTOR = 85);
    PRINT 'Created IX_TransactionDetails_UserId_ServiceName_ReqDate';
END
ELSE
    PRINT 'IX_TransactionDetails_UserId_ServiceName_ReqDate already exists — skipped.';
GO

-- 2. Index for CheckStatus / TxnId lookups:
--    WHERE TxnId = @t OR ApiTxnId = @t
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('InstantPayment_Db.TransactionDetails')
      AND name = 'IX_TransactionDetails_TxnId'
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_TransactionDetails_TxnId]
        ON [InstantPayment_Db].[TransactionDetails] ([TxnId] ASC)
        INCLUDE ([ApiTxnId], [Status], [UserId])
    WITH (FILLFACTOR = 90);
    PRINT 'Created IX_TransactionDetails_TxnId';
END
ELSE
    PRINT 'IX_TransactionDetails_TxnId already exists — skipped.';
GO
