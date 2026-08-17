-- Additive and repeatable schema upgrade for the Sales Team hierarchy.
IF COL_LENGTH('InstantPayment_Db.tblUsers', 'STId') IS NULL
BEGIN
    ALTER TABLE [InstantPayment_Db].[tblUsers] ADD [STId] VARCHAR(255) NULL;
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_tblUsers_STId'
      AND object_id = OBJECT_ID('[InstantPayment_Db].[tblUsers]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_tblUsers_STId]
        ON [InstantPayment_Db].[tblUsers] ([STId])
        INCLUDE ([Id], [Usertype], [Status], [Name], [Username]);
END;
