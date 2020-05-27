using HtmlAgilityPack;
using Mail_Crawler.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;

namespace Mail_Crawler
{
    public class MailServiceTrashMailGenerator : MailServiceBase
    {
        public override List<MailModel> GetMails()
        {
            List<MailModel> mails = new List<MailModel>();

            try
            {
                var httpWebRequest = (HttpWebRequest)WebRequest.Create("https://trashmailgenerator.de/backend.php?username=" + mailAddress.Split('@')[0]);
                httpWebRequest.UserAgent = @"Mozilla / 5.0(Windows NT 10.0; Win64; x64) AppleWebKit / 537.36(KHTML, like Gecko) Chrome / 81.0.4044.138 Safari / 537.36";
                httpWebRequest.Accept = "application/json, text/plain, */*";
                httpWebRequest.Method = "GET";

                var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                string result = "";
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    result = streamReader.ReadToEnd();
                }

                JObject json = JObject.Parse(result);
                foreach(var mailJson in json["mails"])
                {
                    MailModel mail = new MailModel();

                    string id = mailJson["id"].ToString();
                    string title = mailJson["subject"].ToString();
                    string sender = mailJson["fromAddress"].ToString();
                    string content = mailJson["textPlain"].ToString();
                    string htmlContent = mailJson["textHtml"].ToString();
                    string timeString = mailJson["date"].ToString();

                    mail.htmlContent = htmlContent;
                    mail.deleteMailInfo = id;
                    mail.title = title;
                    mail.receiveDate = DateTime.ParseExact(timeString, "yyyy-MM-dd HH:mm:ss", null); ;
                    mail.sender = sender;
                    mail.content = content;

                    MatchCollection matches = Regex.Matches(htmlContent, @"href=(""|')(http|ftp|https)://([\w_-]+(?:(?:\.[\w_-]+)+))([\w.,@?^=%&:/~+#-]*[\w@?^=%&/~+#-])?");

                    foreach (Match match in matches)
                    {
                        string value = match.Value;
                        value = value.Substring(6);
                        if (!mail.links.Contains(value))
                            mail.links.Add(value);
                    }

                    mails.Add(mail);
                }
            }
            catch { }

            return mails;
        }
        
        public override bool DeleteMail(MailModel mail)
        {
            try
            {
                var httpWebRequest = (HttpWebRequest)WebRequest.Create("https://trashmailgenerator.de/backend.php?delete_email_id=" + mail.deleteMailInfo + "&username=" + mailAddress);
                httpWebRequest.UserAgent = @"Mozilla / 5.0(Windows NT 10.0; Win64; x64) AppleWebKit / 537.36(KHTML, like Gecko) Chrome / 81.0.4044.138 Safari / 537.36";
                httpWebRequest.Accept = "application/json, text/plain, */*";
                httpWebRequest.Method = "GET";

                var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                string result = "";
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    result = streamReader.ReadToEnd();
                }
            }
            catch
            {
                return false;
            }
            return true;
        }
    }
}
