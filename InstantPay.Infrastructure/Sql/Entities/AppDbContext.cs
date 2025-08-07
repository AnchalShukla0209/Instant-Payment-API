using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class AppDbContext : DbContext
{
    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Aepsbanklist> Aepsbanklists { get; set; }

    public virtual DbSet<AepsdailyLogin> AepsdailyLogins { get; set; }

    public virtual DbSet<Apilog> Apilogs { get; set; }

    public virtual DbSet<BbpsOperator> BbpsOperators { get; set; }

    public virtual DbSet<BeneReg> BeneRegs { get; set; }

    public virtual DbSet<Finotoken> Finotokens { get; set; }

    public virtual DbSet<SenderReg> SenderRegs { get; set; }

    public virtual DbSet<TblApiwallet> TblApiwallets { get; set; }

    public virtual DbSet<TblBankDetail> TblBankDetails { get; set; }

    public virtual DbSet<TblPasswordattmt> TblPasswordattmts { get; set; }

    public virtual DbSet<TblService> TblServices { get; set; }

    public virtual DbSet<Tbl_Services> Tbl_Services { get; set; }

    public virtual DbSet<TblSlabName> TblSlabNames { get; set; }

    public virtual DbSet<TblSuperadmin> TblSuperadmins { get; set; }
    public virtual DbSet<TblUser> TblUsers { get; set; }
    public virtual DbSet<TblWlUser> TblWlUsers { get; set; }

    public virtual DbSet<TblWlbalance> TblWlbalances { get; set; }

    public virtual DbSet<Tblapionlinepayment> Tblapionlinepayments { get; set; }

    public virtual DbSet<Tblapiuser> Tblapiusers { get; set; }

    public virtual DbSet<Tblbanklist> Tblbanklists { get; set; }

    public virtual DbSet<Tblcommissionslab> Tblcommissionslabs { get; set; }

    public virtual DbSet<Tblcommplan> Tblcommplans { get; set; }

    public virtual DbSet<Tbliservedyouonboarding> Tbliservedyouonboardings { get; set; }

    public virtual DbSet<TblloginOtp> TblloginOtps { get; set; }

    public virtual DbSet<Tblloginlog> Tblloginlogs { get; set; }

    public virtual DbSet<Tblonlinepayment> Tblonlinepayments { get; set; }

    public virtual DbSet<Tbloperator> Tbloperators { get; set; }

    public virtual DbSet<TblpaymentRequest> TblpaymentRequests { get; set; }

    public virtual DbSet<Tblpaymentcharge> Tblpaymentcharges { get; set; }

    public virtual DbSet<Tblpayouttxn> Tblpayouttxns { get; set; }

    public virtual DbSet<Tblretailer> Tblretailers { get; set; }

    public virtual DbSet<TblretailerWallet> TblretailerWallets { get; set; }

    public virtual DbSet<Tblsystemupdate> Tblsystemupdates { get; set; }

    public virtual DbSet<Tbluserbalance> Tbluserbalances { get; set; }

    public virtual DbSet<TransactionDetail> TransactionDetails { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Data Source=198.38.81.244,1232;Initial Catalog=Sbit_Aanchal;User ID=Sbit_Aanchal;Password=Aanchal@801076;MultipleActiveResultSets=True;TrustServerCertificate=True");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("Sbit_Aanchal");

        modelBuilder.Entity<Aepsbanklist>(entity =>
        {
            entity.ToTable("aepsbanklist", "dbo");

            entity.Property(e => e.BankName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Nbin)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("NBIN");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .IsUnicode(false);
        });

        modelBuilder.Entity<AepsdailyLogin>(entity =>
        {
            entity.ToTable("aepsdailyLogin", "dbo");

            entity.Property(e => e.LoginType)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Logindate)
                .HasColumnType("datetime")
                .HasColumnName("logindate");
            entity.Property(e => e.UserId)
                .HasMaxLength(255)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Apilog>(entity =>
        {
            entity.ToTable("apilogs", "dbo");

            entity.Property(e => e.Apiname)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("APIName");
            entity.Property(e => e.Reqdatae)
                .HasColumnType("datetime")
                .HasColumnName("reqdatae");
        });

        modelBuilder.Entity<BbpsOperator>(entity =>
        {
            entity.ToTable("BbpsOperator", "dbo");

            entity.Property(e => e.BillType)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.BillTypeId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.InputParameter)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.InputParameter2)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.OperatorName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Picture)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.ProviderId)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("provider_id");
            entity.Property(e => e.Status)
                .HasMaxLength(10)
                .IsUnicode(false);
        });

        modelBuilder.Entity<BeneReg>(entity =>
        {
            entity.ToTable("BeneReg", "dbo");

            entity.Property(e => e.AccountNo)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.AccountType)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Avstatus)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("AVStatus");
            entity.Property(e => e.BankName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.BeneName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Ifsccode)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("IFSCCode");
            entity.Property(e => e.ReqDate).HasColumnType("datetime");
            entity.Property(e => e.SenderId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.SenderMobile)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.UserId)
                .HasMaxLength(255)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Finotoken>(entity =>
        {
            entity.ToTable("finotoken", "dbo");

            entity.Property(e => e.Reqdate)
                .HasColumnType("datetime")
                .HasColumnName("reqdate");
            entity.Property(e => e.TokenKey).IsUnicode(false);
        });

        modelBuilder.Entity<SenderReg>(entity =>
        {
            entity.ToTable("SenderReg", "dbo");

            entity.Property(e => e.Address)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Pincode)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.ReqDate).HasColumnType("datetime");
            entity.Property(e => e.SenderMobile)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.SenderName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.UserId)
                .HasMaxLength(255)
                .IsUnicode(false);
        });

        modelBuilder.Entity<TblApiwallet>(entity =>
        {
            entity.ToTable("tblAPIWallet", "dbo");

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CrDrType)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("Cr_Dr_Type");
            entity.Property(e => e.IpAddress)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("Ip_Address");
            entity.Property(e => e.NewBal)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("New_Bal");
            entity.Property(e => e.OldBal)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("Old_Bal");
            entity.Property(e => e.PayRefId)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("Pay_Ref_Id");
            entity.Property(e => e.Remarks).IsUnicode(false);
            entity.Property(e => e.TxnDate)
                .HasColumnType("datetime")
                .HasColumnName("Txn_Date");
            entity.Property(e => e.TxnType)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.UserId)
                .HasMaxLength(255)
                .IsUnicode(false);
        });

        modelBuilder.Entity<TblBankDetail>(entity =>
        {
            entity.ToTable("tblBankDetails", "dbo");

            entity.Property(e => e.AccountNumber)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Accounttype)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Address1).IsUnicode(false);
            entity.Property(e => e.Address2).IsUnicode(false);
            entity.Property(e => e.BankName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Charge)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("charge");
            entity.Property(e => e.City)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("city");
            entity.Property(e => e.Ifsccode)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("IFSCCode");
            entity.Property(e => e.Phone)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.State)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Zipcode)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("zipcode");
        });

        modelBuilder.Entity<TblPasswordattmt>(entity =>
        {
            entity.ToTable("tblPasswordattmt", "dbo");

            entity.Property(e => e.Ipaddress)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Password)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Reqdate).HasColumnType("datetime");
            entity.Property(e => e.UserId)
                .HasMaxLength(255)
                .IsUnicode(false);
        });

        modelBuilder.Entity<TblService>(entity =>
        {
            entity.ToTable("tblService", "dbo");

            entity.Property(e => e.Category)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.ServiceName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .IsUnicode(false);
        });

        

        modelBuilder.Entity<TblSlabName>(entity =>
        {
            entity.ToTable("tblSlabName", "dbo");

            entity.Property(e => e.CommissionType)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.DistributionType)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Ipshare)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("IPShare");
            entity.Property(e => e.ServiceId)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("ServiceID");
            entity.Property(e => e.ServiceName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.SlabName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Wlshare)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("WLShare");
        });

        modelBuilder.Entity<TblSuperadmin>(entity =>
        {
            entity.ToTable("tblSuperadmin", "dbo");

            entity.Property(e => e.Dmtapi)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("DMTAPI");
            entity.Property(e => e.Mobileno)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Password)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Refundpin)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("refundpin");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.TxnPin)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Username)
                .HasMaxLength(255)
                .IsUnicode(false);
        });

        modelBuilder.Entity<TblUser>(entity =>
        {
            entity.ToTable("tblUsers", "dbo");

            entity.Property(e => e.AadharBack)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.AadharCard)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.AadharFront)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.AddressLine1).IsUnicode(false);
            entity.Property(e => e.AddressLine2).IsUnicode(false);
            entity.Property(e => e.Adid)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("ADId");
            entity.Property(e => e.Aeps)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("AEPS");
            entity.Property(e => e.AepsStatus)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.BillPayment)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.City)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.CompanyName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.DeviceId)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("DeviceID");
            entity.Property(e => e.DeviceInfo).IsUnicode(false);
            entity.Property(e => e.EmailId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Lat)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("lat");
            entity.Property(e => e.Latlongstatus)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("latlongstatus");
            entity.Property(e => e.Logo)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("logo");
            entity.Property(e => e.Longitute)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("longitute");
            entity.Property(e => e.Mdid)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("MDId");
            entity.Property(e => e.MerchargeCode)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("merchargeCode");
            entity.Property(e => e.MicroAtm)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("MicroATM");
            entity.Property(e => e.MoneyTransfer)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.PanCard)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Pancopy)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Password)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Phone)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Pincode)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.PlanId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Recharge)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.RegDate).HasColumnType("datetime");
            entity.Property(e => e.SessionKey).IsUnicode(false);
            entity.Property(e => e.ShipZipcode)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.ShopAddress).IsUnicode(false);
            entity.Property(e => e.ShopCity)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.ShopState)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.State)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.TokenKey)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.TxnPin)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Username)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Usertype)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Wlid)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("WLId");
        });

        modelBuilder.Entity<TblWlUser>(entity =>
        {
            entity.ToTable("tblWlUsers", "dbo");

            entity.Property(e => e.AadharBack)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.AadharCard)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.AadharFront)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.AddressLine1).IsUnicode(false);
            entity.Property(e => e.AddressLine2).IsUnicode(false);
            entity.Property(e => e.Aeps)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("AEPS");
            entity.Property(e => e.Apitransfer)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("APITransfer");
            entity.Property(e => e.BillPayment)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.City)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.CompanyName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Debit)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.DomainName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.EmailId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Logo)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Margin)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.MicroAtm)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("MicroATM");
            entity.Property(e => e.MoneyTransfer)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.PanCard)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Pancopy)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Password)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Phone)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Pincode)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.PlanId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Recharge)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.RegDate).HasColumnType("datetime");
            entity.Property(e => e.State)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.TxnPin)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.UserName)
                .HasMaxLength(255)
                .IsUnicode(false);
        });

        modelBuilder.Entity<TblWlbalance>(entity =>
        {
            entity.ToTable("tblWLbalance", "dbo");

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CrdrType)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.NewBal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.OldBal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Remarks).IsUnicode(false);
            entity.Property(e => e.SurComm)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("Sur_Comm");
            entity.Property(e => e.Tds)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("tds");
            entity.Property(e => e.TxnAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TxnType)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("txnType");
            entity.Property(e => e.Txndate)
                .HasColumnType("datetime")
                .HasColumnName("txndate");
            entity.Property(e => e.UserId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.UserName)
                .HasMaxLength(255)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Tblapionlinepayment>(entity =>
        {
            entity.ToTable("tblapionlinepayment", "dbo");

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ApiRequest).IsUnicode(false);
            entity.Property(e => e.ApiResponse).IsUnicode(false);
            entity.Property(e => e.Apikey)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("APIKey");
            entity.Property(e => e.ApprovedDate).HasColumnType("datetime");
            entity.Property(e => e.CallbackUrl)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("CallbackURL");
            entity.Property(e => e.Callbackresponse).HasColumnName("callbackresponse");
            entity.Property(e => e.Charge).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ClientRefId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Cstatus)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("cstatus");
            entity.Property(e => e.CustomerName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Emailid)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Gst).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.MobileNo)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.OrderId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.ReqDate).HasColumnType("datetime");
            entity.Property(e => e.SattelAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.TxnId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.UserId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.UserName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Utrno)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("UTRNO");
        });

        modelBuilder.Entity<Tblapiuser>(entity =>
        {
            entity.ToTable("tblapiusers", "dbo");

            entity.Property(e => e.AadharBack)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.AadharFront)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.AadharNo)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Address)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.ApiKey)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.CallbackUrl)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("CallbackURL");
            entity.Property(e => e.CompanyName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.EmailId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.FullName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Gstper).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Ipaddress)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("IPaddress");
            entity.Property(e => e.MobileNo)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.PanNo)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Pancopy)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Password)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Pay100124999)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("Pay1001_24999");
            entity.Property(e => e.Pay11000)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("Pay1_1000");
            entity.Property(e => e.Pay25000).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.PayType)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Paygst)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("paygst");
            entity.Property(e => e.Payin1000)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("payin1000");
            entity.Property(e => e.Payin1999)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("payin1_999");
            entity.Property(e => e.PayinLimit).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Payinstatus)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("payinstatus");
            entity.Property(e => e.PayoutCapping).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Payoutstatus)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("payoutstatus");
            entity.Property(e => e.Pincode)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Profilepic)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("profilepic");
            entity.Property(e => e.Reqdate).HasColumnType("datetime");
        });

        modelBuilder.Entity<Tblbanklist>(entity =>
        {
            entity.ToTable("tblbanklist", "dbo");

            entity.Property(e => e.BankId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Bankname)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Ifsc)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Picture)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("status");
        });

        modelBuilder.Entity<Tblcommissionslab>(entity =>
        {
            entity.ToTable("tblcommissionslab", "dbo");

            entity.Property(e => e.Adshare)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("ADShare");
            entity.Property(e => e.CommissionType)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.DistributionType)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Ipshare)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("IPShare");
            entity.Property(e => e.Mdshare)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("MDShare");
            entity.Property(e => e.PlanId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Rtshare)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("RTShare");
            entity.Property(e => e.ServiceId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.ServiceName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.SlabId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.SlabName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.WlShare).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Wlid)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("WLID");
        });

        modelBuilder.Entity<Tblcommplan>(entity =>
        {
            entity.ToTable("tblcommplan", "dbo");

            entity.Property(e => e.PlanName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Remarks).IsUnicode(false);
            entity.Property(e => e.Reqdate)
                .HasColumnType("datetime")
                .HasColumnName("reqdate");
            entity.Property(e => e.UserId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.UserType)
                .HasMaxLength(255)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Tbliservedyouonboarding>(entity =>
        {
            entity.ToTable("tbliservedyouonboarding", "dbo");

            entity.Property(e => e.ApprovedDate).HasColumnType("datetime");
            entity.Property(e => e.Bcaddress)
                .IsUnicode(false)
                .HasColumnName("BCAddress");
            entity.Property(e => e.Bcagentid)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("bcagentid");
            entity.Property(e => e.Bcarea)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("BCArea");
            entity.Property(e => e.BccompanyName)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("BCCompanyName");
            entity.Property(e => e.BcfirstName)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("BCFirstName");
            entity.Property(e => e.BclastName)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("BCLastName");
            entity.Property(e => e.Bcmobileno)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("BCMobileno");
            entity.Property(e => e.Bcpancard)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("BCPancard");
            entity.Property(e => e.Bcpincode)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("BCPincode");
            entity.Property(e => e.Bcresponse)
                .IsUnicode(false)
                .HasColumnName("BCResponse");
            entity.Property(e => e.BcshopArea)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("BCShopArea");
            entity.Property(e => e.BcshopCity)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("BCShopCity");
            entity.Property(e => e.BcshopName)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("BCShopName");
            entity.Property(e => e.BcshopPincode)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("BCShopPincode");
            entity.Property(e => e.BcshopState)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("BCShopState");
            entity.Property(e => e.Bcshopaddress)
                .IsUnicode(false)
                .HasColumnName("BCShopaddress");
            entity.Property(e => e.Bcshopdisctrict)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("BCShopdisctrict");
            entity.Property(e => e.Bcstatus)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("BCStatus");
            entity.Property(e => e.ReqDate).HasColumnType("datetime");
            entity.Property(e => e.UserId)
                .HasMaxLength(255)
                .IsUnicode(false);
        });

        modelBuilder.Entity<TblloginOtp>(entity =>
        {
            entity.ToTable("tblloginOTP", "dbo");

            entity.Property(e => e.Ipaddress)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Otptype)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("OTPType");
            entity.Property(e => e.Reqdate).HasColumnType("datetime");
            entity.Property(e => e.UserId)
                .HasMaxLength(255)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Tblloginlog>(entity =>
        {
            entity.ToTable("tblloginlogs", "dbo");

            entity.Property(e => e.Ipaddress)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.LoginTime).HasColumnType("datetime");
            entity.Property(e => e.Macaddress)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("macaddress");
            entity.Property(e => e.UserId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Usertype)
                .HasMaxLength(255)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Tblonlinepayment>(entity =>
        {
            entity.ToTable("tblonlinepayment", "dbo");

            entity.Property(e => e.AadharCard)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.AdId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Apiresponse)
                .IsUnicode(false)
                .HasColumnName("apiresponse");
            entity.Property(e => e.Cardno)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("cardno");
            entity.Property(e => e.Cardtype)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("cardtype");
            entity.Property(e => e.Gatwaytype)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("gatwaytype");
            entity.Property(e => e.Gst)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("GST");
            entity.Property(e => e.Mdid)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("MDId");
            entity.Property(e => e.MobileNo)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.OrderId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.PanName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Pancard)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Paymentid)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("paymentid");
            entity.Property(e => e.ReqBy)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.ReqDate).HasColumnType("datetime");
            entity.Property(e => e.Reqlogs).IsUnicode(false);
            entity.Property(e => e.ResDate).HasColumnType("datetime");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.TransferAmt).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TxnCharge).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TxnId)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("txn_Id");
            entity.Property(e => e.UserKey)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.UserName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.WlId)
                .HasMaxLength(255)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Tbloperator>(entity =>
        {
            entity.ToTable("tbloperator", "dbo");

            entity.Property(e => e.Apiname)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("APIName");
            entity.Property(e => e.CommissionType)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Ipshare)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("IPShare");
            entity.Property(e => e.OperatorName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Picture)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.ServiceId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.ServiceName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Servicetype)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Spkey)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Wlshare)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("WLShare");
        });

        modelBuilder.Entity<TblpaymentRequest>(entity =>
        {
            entity.ToTable("tblpaymentRequest", "dbo");

            entity.Property(e => e.AminRemarks).IsUnicode(false);
            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.BankId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.BankName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Charge).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DepositMode)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Reqdate)
                .HasColumnType("datetime")
                .HasColumnName("reqdate");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.TransferAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TxnId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.TxnSlip)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Updatedate).HasColumnType("datetime");
            entity.Property(e => e.UserId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.UserName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Usertype)
                .HasMaxLength(255)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Tblpaymentcharge>(entity =>
        {
            entity.ToTable("tblpaymentcharge", "dbo");

            entity.Property(e => e.Charge).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ChargeType)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.SlabName)
                .HasMaxLength(255)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Tblpayouttxn>(entity =>
        {
            entity.ToTable("tblpayouttxn", "dbo");

            entity.Property(e => e.AccountNo)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ApiRequest).IsUnicode(false);
            entity.Property(e => e.ApiResponse).IsUnicode(false);
            entity.Property(e => e.BankName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.BeneName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Brid)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("BRID");
            entity.Property(e => e.Charge).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ClientRefid)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Gst)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("GST");
            entity.Property(e => e.Ifsccode)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("IFSCCode");
            entity.Property(e => e.MobileNo)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.NewBal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.OldBal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ReqDate).HasColumnType("datetime");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.TotalCost).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TxnId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.TxnMode)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.UserId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.UserName)
                .HasMaxLength(255)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Tblretailer>(entity =>
        {
            entity.ToTable("tblretailer", "dbo");

            entity.Property(e => e.EmailId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.MobileNo)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Password)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.RegDate).HasColumnType("datetime");
            entity.Property(e => e.RetailerName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .IsUnicode(false);
        });

        modelBuilder.Entity<TblretailerWallet>(entity =>
        {
            entity.ToTable("tblretailerWallet", "dbo");

            entity.Property(e => e.Accountno)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("accountno");
            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CrdrType)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.DrCrBy)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.NewBal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.OldBal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Opname)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("opname");
            entity.Property(e => e.Remarks).IsUnicode(false);
            entity.Property(e => e.SurComm)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("Sur_Comm");
            entity.Property(e => e.TxnAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TxnId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Txndate).HasColumnType("datetime");
            entity.Property(e => e.Txntype)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("txntype");
            entity.Property(e => e.UserId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.UserName)
                .HasMaxLength(255)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Tblsystemupdate>(entity =>
        {
            entity.ToTable("tblsystemupdate", "dbo");

            entity.Property(e => e.Lastupdate)
                .HasColumnType("datetime")
                .HasColumnName("lastupdate");
        });

        modelBuilder.Entity<Tbluserbalance>(entity =>
        {
            entity.ToTable("tbluserbalance", "dbo");

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CrdrType)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.NewBal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.OldBal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Remarks).IsUnicode(false);
            entity.Property(e => e.SurCom)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("Sur_Com");
            entity.Property(e => e.Tds)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("tds");
            entity.Property(e => e.TxnAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TxnType)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("txnType");
            entity.Property(e => e.Txndate)
                .HasColumnType("datetime")
                .HasColumnName("txndate");
            entity.Property(e => e.UserName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.WlId)
                .HasMaxLength(255)
                .IsUnicode(false);
        });

        modelBuilder.Entity<TransactionDetail>(entity =>
        {
            entity.HasKey(e => e.TransId);

            entity.ToTable("TransactionDetails", "dbo");

            entity.Property(e => e.AccountNo)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.AdComm)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("AD_Comm");
            entity.Property(e => e.AdId)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("AD_ID");
            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ApiName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.ApiTxnId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.BankName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Brid)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("BRId");
            entity.Property(e => e.Charge).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ComingFrom)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Comm).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Cost).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CustomerName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.IfscCode)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.MdComm)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("MD_Comm");
            entity.Property(e => e.MdId)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("MD_ID");
            entity.Property(e => e.Mobileno)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.NewBal)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.OldBal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.OpId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.OperatorName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.ReqDate).HasColumnType("datetime");
            entity.Property(e => e.ServiceName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Tds)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("tds");
            entity.Property(e => e.TxnId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.TxnMode)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.TxnType)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.UpdateDate).HasColumnType("datetime");
            entity.Property(e => e.UserId)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.UserName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.WlComm)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("WL_Comm");
            entity.Property(e => e.WlId)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("WL_Id");
            entity.Property(e => e.ServiceId).HasColumnType("int")
               .HasMaxLength(255)
               .IsUnicode(false)
               .HasColumnName("ServiceId");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
