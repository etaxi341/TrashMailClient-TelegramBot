using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Mail_Crawler
{
    public class MailService
    {
        #region MailProvider
        static readonly Dictionary<string, string[]> mailProviders = new Dictionary<string, string[]>{
            { "postfach2go",  new string[]{ "postfach2go.de", "mailbox2go.de", "briefkasten2go.de" } },
        };

        #endregion

        public static IMailService Create(string address)
        {
            IMailService mailService = null;
            if (mailProviders["postfach2go"].Any(p => address.EndsWith(p)))
            {
                var serviceTemp = new MailServicePostfach2Go();
                mailService = serviceTemp;
            }

            if (mailService != null)
                mailService.mailAddress = address;
            return mailService;
        }

        public static string GenerateMail()
        {
            Random r = new Random();
            var providers = mailProviders.Keys;
            var provider = providers.ToImmutableArray()[r.Next(0, providers.Count)];
            var domains = mailProviders[provider];
            var domain = domains[r.Next(0, domains.Length)];

            return RandomString(8) + "@" + domain;
        }

        static string RandomString(int length)
        {
            const string valid = "abcdefghijklmnopqrstuvwxyz1234567890";
            StringBuilder res = new StringBuilder();
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                byte[] uintBuffer = new byte[sizeof(uint)];

                while (length-- > 0)
                {
                    rng.GetBytes(uintBuffer);
                    uint num = BitConverter.ToUInt32(uintBuffer, 0);
                    res.Append(valid[(int)(num % (uint)valid.Length)]);
                }
            }

            return res.ToString();
        }
    }
}
