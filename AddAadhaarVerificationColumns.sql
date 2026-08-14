IF COL_LENGTH('InstantPayment_Db.tblUsers', 'IsAadhaarVerified') IS NULL
    ALTER TABLE [InstantPayment_Db].[tblUsers] ADD [IsAadhaarVerified] BIT NOT NULL
        CONSTRAINT [DF_tblUsers_IsAadhaarVerified] DEFAULT (0);

IF COL_LENGTH('InstantPayment_Db.tblUsers', 'AadharVerifiedAt') IS NULL
    ALTER TABLE [InstantPayment_Db].[tblUsers] ADD [AadharVerifiedAt] DATETIME NULL;
