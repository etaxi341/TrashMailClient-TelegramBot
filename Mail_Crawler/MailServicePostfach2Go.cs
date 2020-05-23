using HtmlAgilityPack;
using Mail_Crawler.Models;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;

namespace Mail_Crawler
{
    public class MailServicePostfach2Go : MailServiceBase
    {
        public override List<MailModel> GetMails()
        {
            List<MailModel> mails = new List<MailModel>();

            WebClient wc = new WebClient();
            wc.Headers["User-Agent"] = @"Mozilla / 5.0(Windows NT 10.0; Win64; x64) AppleWebKit / 537.36(KHTML, like Gecko) Chrome / 81.0.4044.138 Safari / 537.36";
            string htmlCode = wc.DownloadString("https://www.postfach2go.de/?" + base.mailAddress);

            if (!htmlCode.Contains("email-list-item"))
                return mails;

            var doc = new HtmlDocument();
            doc.LoadHtml(htmlCode);

            foreach (HtmlNode node in doc.DocumentNode.SelectNodes("//a[contains(@class, 'email-list-item')]"))
            {
                MailModel mail = new MailModel();

                string mailID = node.GetAttributeValue("aria-controls", "").Replace("mail-", "");
                var mailHead = doc.GetElementbyId("print_mail-header-" + mailID);
                var mailBody = doc.GetElementbyId("print_mail-body-" + mailID);
                string title = mailHead.SelectNodes("p[contains(@class, 'list-group-item-text')]")[0].InnerText.Replace("\n", "").Trim();
                string sender = mailHead.SelectNodes("h6[contains(@class, 'list-group-item-heading')]/span")[0].InnerText.Replace("\n", "").Trim();
                string timeString = mailHead.SelectNodes("h6[contains(@class, 'list-group-item-heading')]/small")[0].GetAttributeValue("title", "");

                mail.htmlContent = mailBody.InnerHtml;
                mail.title = title;
                mail.receiveDate = DateTime.ParseExact(timeString, "yyyy-MM-dd HH:mm:ss", null); ;
                mail.sender = sender;
                mail.content = mailBody.InnerText.Replace("\n", "").Trim();

                MatchCollection matches = Regex.Matches(mailBody.InnerHtml, @"href=(""|')(http|ftp|https)://([\w_-]+(?:(?:\.[\w_-]+)+))([\w.,@?^=%&:/~+#-]*[\w@?^=%&/~+#-])?");

                //CONFIRM THEM ALREADY LOL WAY TOO EARLY DAS MUSS IN NE ANDERE METHODE
                foreach (Match match in matches)
                {
                    string value = match.Value;
                    value = value.Substring(6);
                    if (!mail.links.Contains(value))
                        mail.links.Add(value);
                }

                mails.Add(mail);
            }
            return mails;
        }
    }
}
