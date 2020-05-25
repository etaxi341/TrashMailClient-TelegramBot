using System;
using System.Collections.Generic;

namespace Mail_Crawler.Models
{
    public class MailModel
    {
        public string title { get; set; }
        public string sender { get; set; }
        public string content { get; set; }
        public string htmlContent { get; set; }
        public string deleteMailInfo { get; set; }
        public DateTime receiveDate { get; set; }
        public List<string> links { get; set; } = new List<string>();
    }
}
