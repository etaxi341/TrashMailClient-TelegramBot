using Mail_Crawler.Models;
using System.Collections.Generic;

namespace Mail_Crawler
{
    public interface IMailService
    {
        string mailAddress { get; set; }
        List<MailModel> GetMails();
        void ConfirmLinks(MailModel mail);
    }
}
