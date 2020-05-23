using System;
using System.ComponentModel.DataAnnotations;

namespace DataManager.Models
{
    public class Subscribers
    {
        public enum MailProcess
        {
            autoconfirm,
            read,
            readlinks
        }

        [Key]
        public long chatID { get; set; }
        public MailProcess mailprocess { get; set; }
        public DateTime createDate { get; set; } = DateTime.Now;
    }
}
