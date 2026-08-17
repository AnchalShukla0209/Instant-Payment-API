SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH('InstantPayment_Db.tblUsers', 'OnboardingStatus') IS NULL ALTER TABLE [InstantPayment_Db].[tblUsers] ADD [OnboardingStatus] VARCHAR(30) NULL;
IF COL_LENGTH('InstantPayment_Db.tblUsers', 'OnboardingVersion') IS NULL ALTER TABLE [InstantPayment_Db].[tblUsers] ADD [OnboardingVersion] INT NOT NULL CONSTRAINT [DF_tblUsers_OnboardingVersion] DEFAULT (0);
IF COL_LENGTH('InstantPayment_Db.tblUsers', 'SubmittedAt') IS NULL ALTER TABLE [InstantPayment_Db].[tblUsers] ADD [SubmittedAt] DATETIME2 NULL;
IF COL_LENGTH('InstantPayment_Db.tblUsers', 'ApprovedAt') IS NULL ALTER TABLE [InstantPayment_Db].[tblUsers] ADD [ApprovedAt] DATETIME2 NULL;
IF COL_LENGTH('InstantPayment_Db.tblUsers', 'ApprovedBy') IS NULL ALTER TABLE [InstantPayment_Db].[tblUsers] ADD [ApprovedBy] INT NULL;
IF COL_LENGTH('InstantPayment_Db.tblUsers', 'RejectedAt') IS NULL ALTER TABLE [InstantPayment_Db].[tblUsers] ADD [RejectedAt] DATETIME2 NULL;
IF COL_LENGTH('InstantPayment_Db.tblUsers', 'RejectedBy') IS NULL ALTER TABLE [InstantPayment_Db].[tblUsers] ADD [RejectedBy] INT NULL;
IF COL_LENGTH('InstantPayment_Db.tblUsers', 'FinalReviewRemarks') IS NULL ALTER TABLE [InstantPayment_Db].[tblUsers] ADD [FinalReviewRemarks] NVARCHAR(2000) NULL;
IF COL_LENGTH('InstantPayment_Db.tblUsers', 'LastDraftSavedAt') IS NULL ALTER TABLE [InstantPayment_Db].[tblUsers] ADD [LastDraftSavedAt] DATETIME2 NULL;
IF COL_LENGTH('InstantPayment_Db.tblUsers', 'CreatedByUserId') IS NULL ALTER TABLE [InstantPayment_Db].[tblUsers] ADD [CreatedByUserId] INT NULL;
IF COL_LENGTH('InstantPayment_Db.tblUsers', 'CreatedByUserType') IS NULL ALTER TABLE [InstantPayment_Db].[tblUsers] ADD [CreatedByUserType] VARCHAR(20) NULL;
IF COL_LENGTH('InstantPayment_Db.tblUsers', 'RowVersion') IS NULL ALTER TABLE [InstantPayment_Db].[tblUsers] ADD [RowVersion] ROWVERSION NOT NULL;

IF OBJECT_ID('[InstantPayment_Db].[tblUserOnboardingDocuments]', 'U') IS NULL
CREATE TABLE [InstantPayment_Db].[tblUserOnboardingDocuments] (
 [Id] BIGINT IDENTITY PRIMARY KEY, [UserId] INT NOT NULL, [DocumentType] VARCHAR(50) NOT NULL, [CurrentFilePath] NVARCHAR(500) NOT NULL,
 [ReviewStatus] VARCHAR(30) NOT NULL CONSTRAINT [DF_OnboardingDocument_Status] DEFAULT('Pending'), [RejectionRemarks] NVARCHAR(1000) NULL,
 [CurrentVersion] INT NOT NULL CONSTRAINT [DF_OnboardingDocument_Version] DEFAULT(1), [CreatedAt] DATETIME2 NOT NULL, [UpdatedAt] DATETIME2 NOT NULL,
 [ReviewedBy] INT NULL, [ReviewedAt] DATETIME2 NULL,
 CONSTRAINT [FK_OnboardingDocument_User] FOREIGN KEY([UserId]) REFERENCES [InstantPayment_Db].[tblUsers]([Id]),
 CONSTRAINT [UQ_OnboardingDocument_UserType] UNIQUE([UserId],[DocumentType]));

IF OBJECT_ID('[InstantPayment_Db].[tblUserOnboardingDocumentVersions]', 'U') IS NULL
CREATE TABLE [InstantPayment_Db].[tblUserOnboardingDocumentVersions] (
 [Id] BIGINT IDENTITY PRIMARY KEY, [DocumentId] BIGINT NOT NULL, [UserId] INT NOT NULL, [VersionNumber] INT NOT NULL,
 [FilePath] NVARCHAR(500) NOT NULL, [OriginalFileName] NVARCHAR(255) NOT NULL, [ContentType] NVARCHAR(100) NOT NULL, [FileSize] BIGINT NOT NULL,
 [FileHash] VARCHAR(64) NOT NULL, [CorrectionRemarks] NVARCHAR(1000) NULL, [UploadedBy] INT NOT NULL, [UploadedAt] DATETIME2 NOT NULL,
 CONSTRAINT [FK_OnboardingDocumentVersion_Document] FOREIGN KEY([DocumentId]) REFERENCES [InstantPayment_Db].[tblUserOnboardingDocuments]([Id]),
 CONSTRAINT [FK_OnboardingDocumentVersion_User] FOREIGN KEY([UserId]) REFERENCES [InstantPayment_Db].[tblUsers]([Id]),
 CONSTRAINT [UQ_OnboardingDocumentVersion] UNIQUE([DocumentId],[VersionNumber]));

IF OBJECT_ID('[InstantPayment_Db].[tblUserOnboardingReviews]', 'U') IS NULL
CREATE TABLE [InstantPayment_Db].[tblUserOnboardingReviews] (
 [Id] BIGINT IDENTITY PRIMARY KEY, [UserId] INT NOT NULL, [SubmissionVersion] INT NOT NULL, [ReviewStatus] VARCHAR(30) NOT NULL,
 [FinalRemarks] NVARCHAR(2000) NULL, [ReviewedBy] INT NULL, [StartedAt] DATETIME2 NOT NULL, [CompletedAt] DATETIME2 NULL,
 CONSTRAINT [FK_OnboardingReview_User] FOREIGN KEY([UserId]) REFERENCES [InstantPayment_Db].[tblUsers]([Id]),
 CONSTRAINT [UQ_OnboardingReview_Submission] UNIQUE([UserId],[SubmissionVersion]));

IF OBJECT_ID('[InstantPayment_Db].[tblUserOnboardingFieldReviews]', 'U') IS NULL
CREATE TABLE [InstantPayment_Db].[tblUserOnboardingFieldReviews] (
 [Id] BIGINT IDENTITY PRIMARY KEY, [ReviewId] BIGINT NOT NULL, [UserId] INT NOT NULL, [FieldName] VARCHAR(100) NOT NULL,
 [ReviewStatus] VARCHAR(30) NOT NULL, [RejectionRemarks] NVARCHAR(1000) NULL, [ReviewedBy] INT NULL, [ReviewedAt] DATETIME2 NULL,
 CONSTRAINT [FK_OnboardingFieldReview_Review] FOREIGN KEY([ReviewId]) REFERENCES [InstantPayment_Db].[tblUserOnboardingReviews]([Id]),
 CONSTRAINT [FK_OnboardingFieldReview_User] FOREIGN KEY([UserId]) REFERENCES [InstantPayment_Db].[tblUsers]([Id]),
 CONSTRAINT [UQ_OnboardingFieldReview] UNIQUE([ReviewId],[FieldName]));

IF OBJECT_ID('[InstantPayment_Db].[tblUserOnboardingHistory]', 'U') IS NULL
CREATE TABLE [InstantPayment_Db].[tblUserOnboardingHistory] (
 [Id] BIGINT IDENTITY PRIMARY KEY, [UserId] INT NOT NULL, [OnboardingVersion] INT NOT NULL, [EventType] VARCHAR(50) NOT NULL,
 [FromStatus] VARCHAR(30) NULL, [ToStatus] VARCHAR(30) NOT NULL, [Remarks] NVARCHAR(2000) NULL, [ActorUserId] INT NOT NULL,
 [ActorUserType] VARCHAR(20) NOT NULL, [ChangedFieldsJson] NVARCHAR(MAX) NULL, [IpAddress] VARCHAR(64) NULL, [UserAgent] NVARCHAR(500) NULL,
 [CreatedAt] DATETIME2 NOT NULL, CONSTRAINT [FK_OnboardingHistory_User] FOREIGN KEY([UserId]) REFERENCES [InstantPayment_Db].[tblUsers]([Id]));

IF OBJECT_ID('[InstantPayment_Db].[tblUserCredentialDeliveryLogs]', 'U') IS NULL
CREATE TABLE [InstantPayment_Db].[tblUserCredentialDeliveryLogs] (
 [Id] BIGINT IDENTITY PRIMARY KEY, [UserId] INT NOT NULL, [Channel] VARCHAR(20) NOT NULL, [DestinationMasked] NVARCHAR(255) NOT NULL,
 [DeliveryStatus] VARCHAR(30) NOT NULL, [IdempotencyKey] VARCHAR(100) NOT NULL UNIQUE, [FailureReason] NVARCHAR(2000) NULL,
 [AttemptCount] INT NOT NULL CONSTRAINT [DF_CredentialDelivery_Attempts] DEFAULT(0), [CreatedAt] DATETIME2 NOT NULL, [SentAt] DATETIME2 NULL,
 CONSTRAINT [FK_CredentialDelivery_User] FOREIGN KEY([UserId]) REFERENCES [InstantPayment_Db].[tblUsers]([Id]));

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_tblUsers_STId_OnboardingStatus_RegDate' AND object_id=OBJECT_ID('[InstantPayment_Db].[tblUsers]'))
 CREATE INDEX [IX_tblUsers_STId_OnboardingStatus_RegDate] ON [InstantPayment_Db].[tblUsers]([STId],[OnboardingStatus],[RegDate]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_OnboardingHistory_User_CreatedAt' AND object_id=OBJECT_ID('[InstantPayment_Db].[tblUserOnboardingHistory]'))
 CREATE INDEX [IX_OnboardingHistory_User_CreatedAt] ON [InstantPayment_Db].[tblUserOnboardingHistory]([UserId],[CreatedAt] DESC);

COMMIT TRANSACTION;
