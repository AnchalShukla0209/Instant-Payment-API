using System;
using System.Collections.Generic;

namespace InstantPay.Infrastructure.Sql.Entities;

public partial class Tbliservedyouonboarding
{
    public int Id { get; set; }

    public string? UserId { get; set; }

    public string? Bcagentid { get; set; }

    public string? BcfirstName { get; set; }

    public string? BclastName { get; set; }

    public string? BccompanyName { get; set; }

    public string? Bcaddress { get; set; }

    public string? Bcarea { get; set; }

    public string? Bcpincode { get; set; }

    public string? Bcmobileno { get; set; }

    public string? BcshopName { get; set; }

    public string? Bcshopaddress { get; set; }

    public string? BcshopState { get; set; }

    public string? BcshopCity { get; set; }

    public string? Bcshopdisctrict { get; set; }

    public string? BcshopArea { get; set; }

    public string? BcshopPincode { get; set; }

    public string? Bcpancard { get; set; }

    public string? Bcstatus { get; set; }

    public string? Bcresponse { get; set; }

    public DateTime? ReqDate { get; set; }

    public DateTime? ApprovedDate { get; set; }
}
