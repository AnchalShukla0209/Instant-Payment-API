IF COL_LENGTH('InstantPayment_Db.tblUsers', 'IsPhoneVerified') IS NULL
    ALTER TABLE [InstantPayment_Db].[tblUsers] ADD [IsPhoneVerified] BIT NOT NULL
        CONSTRAINT [DF_tblUsers_IsPhoneVerified] DEFAULT (0);

IF COL_LENGTH('InstantPayment_Db.tblUsers', 'PhoneVerifiedAt') IS NULL
    ALTER TABLE [InstantPayment_Db].[tblUsers] ADD [PhoneVerifiedAt] DATETIME NULL;

IF COL_LENGTH('InstantPayment_Db.tblUsers', 'IsEmailVerified') IS NULL
    ALTER TABLE [InstantPayment_Db].[tblUsers] ADD [IsEmailVerified] BIT NOT NULL
        CONSTRAINT [DF_tblUsers_IsEmailVerified] DEFAULT (0);

IF COL_LENGTH('InstantPayment_Db.tblUsers', 'EmailVerifiedAt') IS NULL
    ALTER TABLE [InstantPayment_Db].[tblUsers] ADD [EmailVerifiedAt] DATETIME NULL;

IF COL_LENGTH('InstantPayment_Db.tblUsers', 'IsPanVerified') IS NULL
    ALTER TABLE [InstantPayment_Db].[tblUsers] ADD [IsPanVerified] BIT NOT NULL
        CONSTRAINT [DF_tblUsers_IsPanVerified] DEFAULT (0);

IF COL_LENGTH('InstantPayment_Db.tblUsers', 'PanVerifiedAt') IS NULL
    ALTER TABLE [InstantPayment_Db].[tblUsers] ADD [PanVerifiedAt] DATETIME NULL;

IF COL_LENGTH('InstantPayment_Db.tblUsers', 'PanVerifiedName') IS NULL
    ALTER TABLE [InstantPayment_Db].[tblUsers] ADD [PanVerifiedName] NVARCHAR(255) NULL;

IF COL_LENGTH('InstantPayment_Db.tblUsers', 'RazorpayPayment') IS NULL
    ALTER TABLE [InstantPayment_Db].[tblUsers] ADD [RazorpayPayment] VARCHAR(20) NOT NULL
        CONSTRAINT [DF_tblUsers_RazorpayPayment] DEFAULT ('Inactive');

IF COL_LENGTH('InstantPayment_Db.tblUsers', 'Settlement') IS NULL
    ALTER TABLE [InstantPayment_Db].[tblUsers] ADD [Settlement] VARCHAR(20) NOT NULL
        CONSTRAINT [DF_tblUsers_Settlement] DEFAULT ('Inactive');

IF COL_LENGTH('InstantPayment_Db.tblUsers', 'SelfieImage') IS NULL
    ALTER TABLE [InstantPayment_Db].[tblUsers] ADD [SelfieImage] VARCHAR(500) NULL;
