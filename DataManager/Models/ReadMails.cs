using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataManager.Models
{
    public class ReadMails
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Key]
        public long ID { get; set; }
        public ActiveMails mail { get; set; }
        public string sender { get; set; }
        public string title { get; set; }
        public DateTime receiveDate { get; set; }
    }
}
