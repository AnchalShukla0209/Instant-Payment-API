IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_tblUsers_ADId' AND object_id = OBJECT_ID('[InstantPayment_Db].[tblUsers]')
)
    CREATE NONCLUSTERED INDEX [IX_tblUsers_ADId]
        ON [InstantPayment_Db].[tblUsers] ([ADId])
        INCLUDE ([Id], [RegDate], [Usertype], [Status], [Name], [Username]);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_tblUsers_MDId' AND object_id = OBJECT_ID('[InstantPayment_Db].[tblUsers]')
)
    CREATE NONCLUSTERED INDEX [IX_tblUsers_MDId]
        ON [InstantPayment_Db].[tblUsers] ([MDId])
        INCLUDE ([Id], [RegDate], [Usertype], [Status], [Name], [Username]);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_TransactionDetails_UserId_ReqDate_Status'
      AND object_id = OBJECT_ID('[InstantPayment_Db].[TransactionDetails]')
)
    CREATE NONCLUSTERED INDEX [IX_TransactionDetails_UserId_ReqDate_Status]
        ON [InstantPayment_Db].[TransactionDetails] ([UserId], [ReqDate], [Status])
        INCLUDE ([Amount], [TxnId], [ServiceName]);
GO
