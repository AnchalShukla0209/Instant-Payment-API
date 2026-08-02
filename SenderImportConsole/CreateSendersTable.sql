-- Create Senders table in IpSenderList_DB
USE IpSenderList_DB;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Senders')
BEGIN
    CREATE TABLE Senders (
        id INT IDENTITY(1,1) PRIMARY KEY,
        sender_mobile NVARCHAR(20) NOT NULL UNIQUE,
        first_name NVARCHAR(100) NOT NULL,
        last_name NVARCHAR(100),
        address NVARCHAR(500),
        pincode NVARCHAR(10),
        state NVARCHAR(100),
        is_kyc_verified BIT DEFAULT 0,
        otp NVARCHAR(10),
        otp_expiry DATETIME,
        created_on DATETIME NOT NULL DEFAULT GETUTCDATE(),
        updated_on DATETIME NOT NULL DEFAULT GETUTCDATE()
    );
    
    CREATE INDEX IX_Senders_SenderMobile ON Senders(sender_mobile);
    CREATE INDEX IX_Senders_IsKycVerified ON Senders(is_kyc_verified);
    
    PRINT 'Senders table created successfully.';
END
ELSE
BEGIN
    PRINT 'Senders table already exists.';
END
GO
