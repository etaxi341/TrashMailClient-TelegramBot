using Mail_Crawler.Models;
using System;
using System.Collections.Generic;
using System.Net;

namespace Mail_Crawler
{
    public class MailServiceBase : IMailService
    {
        public string mailAddress { get; set; }

        public void ConfirmLinks(MailModel mail)
        {
            foreach (string link in mail.links)
            {
                try
                {
                    var request = (HttpWebRequest)WebRequest.Create(link);
                    request.UserAgent = @"Mozilla / 5.0(Windows NT 10.0; Win64; x64) AppleWebKit / 537.36(KHTML, like Gecko) Chrome / 81.0.4044.138 Safari / 537.36";
                    request.GetResponse();
                }
                catch { }
            }
        }

        public virtual bool DeleteMail(MailModel mail)
        {
            throw new NotImplementedException();
        }

        public virtual List<MailModel> GetMails()
        {
            throw new NotImplementedException();
        }
    }
}
