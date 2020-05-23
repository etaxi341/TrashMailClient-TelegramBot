using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataManager.Models
{
    public class ActiveMails
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Key]
        public long ID { get; set; }
        public string address { get; set; }
        public Subscribers subscriber { get; set; }
        public DateTime endDate { get; set; }
    }
}
