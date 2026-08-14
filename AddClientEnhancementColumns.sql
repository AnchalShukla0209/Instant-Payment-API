-- Additive schema upgrade for tblWlUsers (Client / White-Label admin accounts)
-- Mirrors the verification, geolocation, selfie, commission plan, and
-- Razorpay/Settlement service-right columns already added to tblUsers.
-- Safe to re-run: every change is guarded with a column-existence check.

IF COL_LENGTH('InstantPayment_Db.tblWlUsers', 'IsPhoneVerified') IS NULL
    ALTER TABLE [InstantPayment_Db].[tblWlUsers] ADD [IsPhoneVerified] BIT NOT NULL
        CONSTRAINT [DF_tblWlUsers_IsPhoneVerified] DEFAULT (0);

IF COL_LENGTH('InstantPayment_Db.tblWlUsers', 'PhoneVerifiedAt') IS NULL
    ALTER TABLE [InstantPayment_Db].[tblWlUsers] ADD [PhoneVerifiedAt] DATETIME NULL;

IF COL_LENGTH('InstantPayment_Db.tblWlUsers', 'IsEmailVerified') IS NULL
    ALTER TABLE [InstantPayment_Db].[tblWlUsers] ADD [IsEmailVerified] BIT NOT NULL
        CONSTRAINT [DF_tblWlUsers_IsEmailVerified] DEFAULT (0);

IF COL_LENGTH('InstantPayment_Db.tblWlUsers', 'EmailVerifiedAt') IS NULL
    ALTER TABLE [InstantPayment_Db].[tblWlUsers] ADD [EmailVerifiedAt] DATETIME NULL;

IF COL_LENGTH('InstantPayment_Db.tblWlUsers', 'IsPanVerified') IS NULL
    ALTER TABLE [InstantPayment_Db].[tblWlUsers] ADD [IsPanVerified] BIT NOT NULL
        CONSTRAINT [DF_tblWlUsers_IsPanVerified] DEFAULT (0);

IF COL_LENGTH('InstantPayment_Db.tblWlUsers', 'PanVerifiedAt') IS NULL
    ALTER TABLE [InstantPayment_Db].[tblWlUsers] ADD [PanVerifiedAt] DATETIME NULL;

IF COL_LENGTH('InstantPayment_Db.tblWlUsers', 'PanVerifiedName') IS NULL
    ALTER TABLE [InstantPayment_Db].[tblWlUsers] ADD [PanVerifiedName] VARCHAR(255) NULL;

IF COL_LENGTH('InstantPayment_Db.tblWlUsers', 'IsAadhaarVerified') IS NULL
    ALTER TABLE [InstantPayment_Db].[tblWlUsers] ADD [IsAadhaarVerified] BIT NOT NULL
        CONSTRAINT [DF_tblWlUsers_IsAadhaarVerified] DEFAULT (0);

IF COL_LENGTH('InstantPayment_Db.tblWlUsers', 'AadharVerifiedAt') IS NULL
    ALTER TABLE [InstantPayment_Db].[tblWlUsers] ADD [AadharVerifiedAt] DATETIME NULL;

IF COL_LENGTH('InstantPayment_Db.tblWlUsers', 'SelfieImage') IS NULL
    ALTER TABLE [InstantPayment_Db].[tblWlUsers] ADD [SelfieImage] VARCHAR(500) NULL;

IF COL_LENGTH('InstantPayment_Db.tblWlUsers', 'RazorpayPayment') IS NULL
    ALTER TABLE [InstantPayment_Db].[tblWlUsers] ADD [RazorpayPayment] VARCHAR(20) NULL
        CONSTRAINT [DF_tblWlUsers_RazorpayPayment] DEFAULT ('Active');

IF COL_LENGTH('InstantPayment_Db.tblWlUsers', 'Settlement') IS NULL
    ALTER TABLE [InstantPayment_Db].[tblWlUsers] ADD [Settlement] VARCHAR(20) NULL
        CONSTRAINT [DF_tblWlUsers_Settlement] DEFAULT ('Active');

IF COL_LENGTH('InstantPayment_Db.tblWlUsers', 'Lat') IS NULL
    ALTER TABLE [InstantPayment_Db].[tblWlUsers] ADD [Lat] VARCHAR(255) NULL;

IF COL_LENGTH('InstantPayment_Db.tblWlUsers', 'Longitute') IS NULL
    ALTER TABLE [InstantPayment_Db].[tblWlUsers] ADD [Longitute] VARCHAR(255) NULL;

IF COL_LENGTH('InstantPayment_Db.tblWlUsers', 'CommissionPlanId') IS NULL
    ALTER TABLE [InstantPayment_Db].[tblWlUsers] ADD [CommissionPlanId] INT NULL;
