using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebSphere.Domain.Entities
{
    public class SignalsDiscrete
    {
        [Key]
        [Required]
        public int Id { get; set; }

        public int TagId { get; set; }

        public bool Value { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime Datetime { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime RegTime { get; set; }
    }
}
