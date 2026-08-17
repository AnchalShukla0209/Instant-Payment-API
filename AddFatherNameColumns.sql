-- Additive and repeatable schema upgrade for user father-name details.
IF COL_LENGTH('InstantPayment_Db.tblUsers', 'FatherName') IS NULL
BEGIN
    ALTER TABLE [InstantPayment_Db].[tblUsers] ADD [FatherName] VARCHAR(255) NULL;
END;

IF COL_LENGTH('InstantPayment_Db.tblWlUsers', 'FatherName') IS NULL
BEGIN
    ALTER TABLE [InstantPayment_Db].[tblWlUsers] ADD [FatherName] VARCHAR(255) NULL;
END;
