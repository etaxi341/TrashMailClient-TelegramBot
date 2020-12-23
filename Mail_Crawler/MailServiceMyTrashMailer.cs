using HtmlAgilityPack;
using Mail_Crawler.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Mail_Crawler
{
    public class MailServiceMyTrashMailer : MailServiceBase
    {
        public override List<MailModel> GetMails()
        {
            List<MailModel> mails = new List<MailModel>();

            try
            {
                string mailuser = mailAddress.Split('@')[0];
                string maildomain = mailAddress.Split('@')[1];


                var httpWebRequest = (HttpWebRequest)WebRequest.Create("https://www.mytrashmailer.com/ajax.php?task=receive");
                httpWebRequest.UserAgent = @"Mozilla / 5.0(Windows NT 10.0; Win64; x64) AppleWebKit / 537.36(KHTML, like Gecko) Chrome / 81.0.4044.138 Safari / 537.36";
                httpWebRequest.Accept = "text/html, */*; q=0.01";
                httpWebRequest.Method = "POST";

                string postData = "emaillocal=" + mailuser + "&emailglobal=" + maildomain;
                ASCIIEncoding encoding = new ASCIIEncoding();
                byte[] postDataBytes = encoding.GetBytes(postData);

                httpWebRequest.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
                httpWebRequest.ContentLength = postDataBytes.Length;

                Stream newStream = httpWebRequest.GetRequestStream();

                newStream.Write(postDataBytes, 0, postDataBytes.Length);

                var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                string result = "";
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    result = streamReader.ReadToEnd();
                }

                if (!result.Contains("<table"))
                    return mails;

                bool firstResult = true;
                foreach (string str in result.Split("<td valign=\"top\">"))
                {
                    if (firstResult)
                    {
                        firstResult = false;
                        continue;
                    }
                    MailModel mail = new MailModel();

                    string title = str.Split("<a data-open=\"exampleModal1\"><strong>")[1].Split("</strong>")[0];
                    string sender = str.Split("<br>")[1].Split("</td>")[0].Replace("\r", "").Replace("\n", "").Replace("\t", "");
                    string timeString = str.Split("<i>")[1].Split("</i>")[0];

                    mail.htmlContent = str.Split("<p>")[3].Split("<button class=\"close - button\" data-close aria-label=\"Close reveal\" type=\"button\">")[0];
                    mail.htmlContent = mail.htmlContent.Substring(0, mail.htmlContent.LastIndexOf("</p>"));
                    mail.title = title;
                    mail.receiveDate = DateTime.ParseExact(timeString, "MM/dd/yy - HH:mm", null);
                    mail.sender = sender;
                    mail.content = mail.htmlContent;

                    MatchCollection matches = Regex.Matches(mail.htmlContent, @"href=(""|')(http|ftp|https)://([\w_-]+(?:(?:\.[\w_-]+)+))([\w.,@?^=%&:/~+#-]*[\w@?^=%&/~+#-])?");

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
            return false;
        }
    }
}
