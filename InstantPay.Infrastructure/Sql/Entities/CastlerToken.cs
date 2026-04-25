using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Infrastructure.Sql.Entities
{
    public class CastlerToken
    {
        [Key]
        public int Id { get; set; }
        [Column("CastlerToken")]
        public string? CastlerAccessToken { get; set; }
        public DateTime? CreatedOn { get; set; }
        public DateTime? UpdatedOn { get; set; }
    }
}
