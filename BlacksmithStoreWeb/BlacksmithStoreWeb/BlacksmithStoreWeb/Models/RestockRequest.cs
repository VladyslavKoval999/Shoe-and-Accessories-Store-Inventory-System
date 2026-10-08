using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BlacksmithStoreWeb.Models
{
    [Table("Restock_Requests")]
    public class RestockRequest
    {
        [Key]
        [Column("request_id")]
        public int RequestId { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("product_id")]
        public int ProductId { get; set; }

        [Column("size")]
        public string? Size { get; set; }

        [Column("request_date")]
        public DateTime RequestDate { get; set; }

        [Column("is_fulfilled")]
        public bool IsFulfilled { get; set; }
    }
}