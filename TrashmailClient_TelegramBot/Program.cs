﻿using DataManager.Data;
using DataManager.Models;
using Mail_Crawler;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.ReplyMarkups;

namespace TrashmailClient_TelegramBot
{
    class Program
    {
        static TelegramBotClient bot;

        #region COMMANDS
        const string start = "/start";
        const string generate = "/generate";
        const string options = "/options";
        const string autoconfirm = "Automatically confirm";
        const string show = "Show";
        const string listlinks = "Show only links";
        const string generatemail = "Generate new mail";
        #endregion

        static readonly ReplyKeyboardMarkup replyMarkup = new ReplyKeyboardMarkup(
            keyboardRow: new[] { new KeyboardButton(generatemail) },
            resizeKeyboard: true,
            oneTimeKeyboard: true
        );

        static readonly InlineKeyboardMarkup replyMarkupOptions = new InlineKeyboardMarkup(
        new InlineKeyboardButton[][]
        {
            new [] { InlineKeyboardButton.WithCallbackData(autoconfirm) },
            new [] { InlineKeyboardButton.WithCallbackData(show) },
            new [] { InlineKeyboardButton.WithCallbackData(listlinks) }
        });

        static void Main(string[] args)
        {
            string botToken;

            if (args == null || args.Length == 0)
            {
                Console.WriteLine("Please enter your Bot Token. Alternatively you can also use it as startup parameter");
                botToken = Console.ReadLine();
            }
            else
            {
                botToken = args[0];
            }
            
            bot = new TelegramBotClient(botToken);
            bot.OnMessage += Bot_OnMessage;
            bot.OnCallbackQuery += Bot_OnCallbackQuery;
            bot.StartReceiving();

            while (true)
            {
                CheckForMails();

                Thread.Sleep(60000); //Sleep one minute
            }
        }

        private static void CheckForMails()
        {
            DateTime in15Minutes = DateTime.Now.AddMinutes(15);

            DatabaseContext db = new DatabaseContext();
            ActiveMails[] activeMails = db.activemails.Where(a => a.endDate < in15Minutes).ToArray();

            foreach (ActiveMails activeMail in activeMails)
            {
                Thread messageThread = new Thread(() => CheckForMail(activeMail));
                messageThread.Start();

                Thread.Sleep(20);
            }
        }

        static void CheckForMail(ActiveMails activeMail)
        {
            try
            {
                IMailService mailServer = MailService.Create(activeMail.address);
                var mails = mailServer.GetMails();

                if (mails.Count == 0)
                    return;

                DatabaseContext db = new DatabaseContext();
                activeMail = db.activemails.Include("subscriber").Where(a => a.ID == activeMail.ID).FirstOrDefault();

                foreach (var mail in mails)
                {
                    if (db.readmails.Any(r => r.mail == activeMail && r.sender == mail.sender && r.receiveDate == mail.receiveDate && r.title == mail.title))
                        continue;

                    try
                    {
                        switch (activeMail.subscriber.mailprocess)
                        {
                            case Subscribers.MailProcess.autoconfirm:
                                mailServer.ConfirmLinks(mail);

                                bot.SendTextMessageAsync(
                                    chatId: activeMail.subscriber.chatID,
                                    text: "Your mail for \"" + mail.title + "\" was confirmed!",
                                    replyMarkup: replyMarkup
                                );
                                break;
                            case Subscribers.MailProcess.read:
                                bot.SendDocumentAsync(
                                    chatId: activeMail.subscriber.chatID,
                                    document: new Telegram.Bot.Types.InputFiles.InputOnlineFile(new MemoryStream(Encoding.UTF8.GetBytes(mail.htmlContent ?? "")), "Mail.html")
                                );
                                break;
                            case Subscribers.MailProcess.readlinks:
                                string messagelink = mail.sender + Environment.NewLine + mail.title;
                                messagelink += Environment.NewLine + Environment.NewLine;
                                messagelink += "Links:";
                                foreach (string link in mail.links)
                                    messagelink += Environment.NewLine + link;

                                bot.SendTextMessageAsync(
                                    chatId: activeMail.subscriber.chatID,
                                    text: messagelink,
                                    replyMarkup: replyMarkup
                                );
                                break;
                        }

                        ReadMails readMail = new ReadMails();
                        readMail.mail = activeMail;
                        readMail.receiveDate = mail.receiveDate;
                        readMail.sender = mail.sender;
                        readMail.title = mail.title;

                        db.readmails.Add(readMail);
                        db.SaveChanges();
                    }
                    catch { }
                    Thread.Sleep(1000);
                }
            }
            catch { }
        }

        private static void Bot_OnCallbackQuery(object sender, CallbackQueryEventArgs e)
        {
            long chatID = e.CallbackQuery.Message.Chat.Id;

            DatabaseContext db = new DatabaseContext();
            Subscribers sub = db.subscribers.Where(s => s.chatID == chatID).FirstOrDefault();

            switch (e.CallbackQuery.Data)
            {
                case autoconfirm:
                    sub.mailprocess = Subscribers.MailProcess.autoconfirm;

                    bot.SendTextMessageAsync(
                        chatId: chatID,
                        text: "Thank you, all future mails will be automatically confirmed!",
                        replyMarkup: replyMarkup
                    );
                    break;
                case show:
                    sub.mailprocess = Subscribers.MailProcess.read;

                    bot.SendTextMessageAsync(
                        chatId: chatID,
                        text: "Thank you, I will send all future mails to you!",
                        replyMarkup: replyMarkup
                    );
                    break;
                case listlinks:
                    sub.mailprocess = Subscribers.MailProcess.readlinks;

                    bot.SendTextMessageAsync(
                        chatId: chatID,
                        text: "Thank you, I will filter out all the links in future mails and send them to you!",
                        replyMarkup: replyMarkup
                    );
                    break;
            }

            db.SaveChanges();
        }

        private static void Bot_OnMessage(object sender, MessageEventArgs e)
        {
            long chatID = e.Message.Chat.Id;

            DatabaseContext db = new DatabaseContext();
            Subscribers sub = db.subscribers.Where(s => s.chatID == chatID).FirstOrDefault();
            if (sub == null)
            {
                sub = new Subscribers();
                sub.chatID = chatID;
                sub.mailprocess = Subscribers.MailProcess.read;
                db.subscribers.Add(sub);
            }

            switch (e.Message.Text)
            {
                case start:
                    bot.SendTextMessageAsync(
                        chatId: chatID,
                        text:
                        @"
Thank you for using the TrashMailClient

Commands:
/start - Shows you this text
/generate - Generates a new mail for you
/options - Select what I should do with mails
                        
If you enjoy this service, feel free to support my creator on patreon:
https://www.patreon.com/etaxi341"
                    );
                    Thread.Sleep(1000);
                    goto case options;
                case options:
                    bot.SendTextMessageAsync(
                        chatId: chatID,
                        text: "What should I do with mails?",
                        replyMarkup: replyMarkupOptions
                    );
                    break;
                case generate:
                case generatemail:

                    var currentlyActiveMails = db.activemails.Where(a => a.endDate > DateTime.Now && a.subscriber == sub).ToArray();

                    if (currentlyActiveMails.Length >= 5)
                    {
                        bot.SendTextMessageAsync(
                            chatId: chatID,
                            text: "You have generated too many mails. Try again later.",
                            replyMarkup: replyMarkup
                        );
                        break;
                    }

                    string generatedMail = MailService.GenerateMail();
                    bot.SendTextMessageAsync(
                        chatId: chatID,
                        text: "I will listen on the following mail for 15 minutes: " + generatedMail,
                        replyMarkup: replyMarkup
                    );

                    ActiveMails activeMail = new ActiveMails();
                    activeMail.address = generatedMail;
                    activeMail.subscriber = sub;
                    activeMail.endDate = DateTime.Now.AddMinutes(15);

                    db.activemails.Add(activeMail);
                    break;
            }

            db.SaveChanges();
        }
    }
}