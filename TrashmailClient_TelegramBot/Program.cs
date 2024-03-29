using DataManager.Data;
using DataManager.Models;
using Mail_Crawler;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TrashmailClient_TelegramBot
{
    class Program
    {
        static TelegramBotClient bot;
        static User botUser;

        #region COMMANDS
        const string start = "/start";
        const string help = "/help";
        const string generate = "/generate";
        const string options = "/options";
        const string custom = "/custom";
        const string autoconfirm = "Automatically confirm";
        const string show = "Show";
        const string listlinks = "Show only links";
        const string generatemail = "Generate new mail";
        const string refresh = "Refresh mail";
        #endregion

        static readonly ReplyKeyboardMarkup replyMarkup = new ReplyKeyboardMarkup(keyboardRow: new[] { new KeyboardButton(generatemail) })
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = false
        };

        static readonly InlineKeyboardMarkup recieveOptions = new InlineKeyboardMarkup(
        new InlineKeyboardButton[][]
        {
            new [] { InlineKeyboardButton.WithCallbackData(autoconfirm) },
            new [] { InlineKeyboardButton.WithCallbackData(show) },
            new [] { InlineKeyboardButton.WithCallbackData(listlinks) }
        });

        static readonly InlineKeyboardMarkup refreshOptions = new InlineKeyboardMarkup(
        new InlineKeyboardButton[][]
        {
            new [] { InlineKeyboardButton.WithCallbackData(refresh) }
        });

        static async Task Main(string[] args)
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

            using CancellationTokenSource cts = new();
            ReceiverOptions receiverOptions = new() { AllowedUpdates = { } };

            bot = new TelegramBotClient(botToken);
            bot.StartReceiving(Bot_HandleUpdateAsync,
                               Bot_HandleErrorAsync,
                               receiverOptions,
                               cts.Token);

            botUser = await bot.GetMeAsync();

            while (true)
            {
                CheckForMails();
                DeleteOldDbEntries();

                Thread.Sleep(60000); //Sleep one minute
            }
        }

        private static void CheckForMails()
        {
            DatabaseContext db = new DatabaseContext();
            ActiveMails[] activeMails = db.activemails.Include("subscriber").Where(a => a.endDate > DateTime.Now).ToArray();

            foreach (ActiveMails activeMail in activeMails)
            {
                Thread messageThread = new Thread(() => CheckForMail(activeMail));
                messageThread.Start();

                Thread.Sleep(20);
            }
        }

        static void CheckForMail(ActiveMails activeMail)
        {
            DatabaseContext db = new DatabaseContext();
            activeMail = db.activemails.Include("subscriber").Where(a => a.ID == activeMail.ID).FirstOrDefault();

            //Update timer
            try
            {
                int timeLeft = (int)((activeMail.endDate - DateTime.Now).TotalMinutes);
                string suffix = "This mail will be valid for another " + timeLeft + " minutes";

                if (timeLeft <= 1)
                    suffix = "This mail will be valid for another minute";

                bot.EditMessageTextAsync(
                    chatId: activeMail.subscriber.chatID,
                    messageId: activeMail.messageID,
                    text: activeMail.address + Environment.NewLine + suffix
                );
                IMailService mailServer = MailService.Create(activeMail.address);
                var mails = mailServer.GetMails();

                if (mails.Count == 0)
                    return;

                foreach (var mail in mails)
                {
                    if (db.readmails.Any(r => r.mail == activeMail && r.sender == mail.sender && r.receiveDate == mail.receiveDate && r.title == mail.title))
                        continue;

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
                    Thread.Sleep(1000);
                }
            }
            catch (ApiRequestException)
            {
                var readmails = db.readmails.Where(r => r.mail == activeMail).ToList();
                db.readmails.RemoveRange(readmails);
                db.activemails.Remove(activeMail);

                db.SaveChanges();
            }
            catch { }
        }

        static void DeleteOldDbEntries()
        {
            DatabaseContext db = new DatabaseContext();
            ActiveMails[] activeMails = db.activemails.Include("subscriber").Where(a => a.endDate < DateTime.Now).ToArray();
            ReadMails[] readMails = db.readmails.Include("mail").Where(a => a.mail.endDate < DateTime.Now).ToArray();

            foreach (ActiveMails activeMail in activeMails)
            {
                try
                {
                    bot.EditMessageTextAsync(
                        chatId: activeMail.subscriber.chatID,
                        messageId: activeMail.messageID,
                        text: activeMail.address + Environment.NewLine + "This mail has run out of time",
                        replyMarkup: refreshOptions
                    );
                }
                catch
                {

                }

                IMailService mailServer = MailService.Create(activeMail.address);

                if (mailServer == null)
                    continue;

                var mails = mailServer.GetMails();
                foreach (var mail in mails)
                {
                    mailServer.DeleteMail(mail);
                }
            }

            db.readmails.RemoveRange(readMails);
            db.activemails.RemoveRange(activeMails);

            db.SaveChanges();
        }

        public static Task Bot_HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var ErrorMessage = exception switch
            {
                ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(ErrorMessage);
            return Task.CompletedTask;
        }

        public static async Task Bot_HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            var handler = update.Type switch
            {
                // UpdateType.Unknown:
                // UpdateType.ChannelPost:
                // UpdateType.EditedChannelPost:
                // UpdateType.ShippingQuery:
                // UpdateType.PreCheckoutQuery:
                // UpdateType.Poll:
                UpdateType.Message => Bot_OnMessage(botClient, update.Message!),
                //UpdateType.EditedMessage:
                UpdateType.CallbackQuery => Bot_OnCallbackQuery(botClient, update.CallbackQuery!),
                //UpdateType.InlineQuery:
                //UpdateType.ChosenInlineResult:
                _ => UnknownUpdateHandlerAsync(botClient, update)
            };

            try
            {
                await handler;
            }
            catch (Exception exception)
            {
                await Bot_HandleErrorAsync(botClient, exception, cancellationToken);
            }
        }

        private static Task UnknownUpdateHandlerAsync(ITelegramBotClient botClient, Update update)
        {
            Console.WriteLine($"Unknown update type: {update.Type}");
            return Task.CompletedTask;
        }

        private static Task Bot_OnCallbackQuery(object sender, CallbackQuery callback)
        {
            long chatID = callback.Message.Chat.Id;

            DatabaseContext db = new DatabaseContext();
            Subscribers sub = db.subscribers.Where(s => s.chatID == chatID).FirstOrDefault();

            try
            {
                switch (callback.Data)
                {
                    case autoconfirm:
                        sub.mailprocess = Subscribers.MailProcess.autoconfirm;

                        bot.AnswerCallbackQueryAsync(
                            callbackQueryId: callback.Id,
                            text: "Thank you, all future mails will be automatically confirmed!"
                        );
                        break;
                    case show:
                        sub.mailprocess = Subscribers.MailProcess.read;

                        bot.AnswerCallbackQueryAsync(
                            callbackQueryId: callback.Id,
                            text: "Thank you, I will send all future mails to you!"
                        );
                        break;
                    case listlinks:
                        sub.mailprocess = Subscribers.MailProcess.readlinks;

                        bot.AnswerCallbackQueryAsync(
                            callbackQueryId: callback.Id,
                            text: "Thank you, I will filter out all the links in future mails and send them to you!"
                        );
                        break;
                    case refresh:
                        string originalMail = callback.Message.Text.Split("\n")[0];

                        bot.AnswerCallbackQueryAsync(
                            callbackQueryId: callback.Id
                        );
                        AnswerGenerateMail(db, sub, chatID, custom, originalMail, callback.Message.MessageId);
                        break;
                }
            }
            catch
            {

            }

            db.SaveChanges();
            return Task.CompletedTask;
        }

        private static Task Bot_OnMessage(object sender, Message message)
        {
            long chatID = message.Chat.Id;

            DatabaseContext db = new DatabaseContext();
            Subscribers sub = db.subscribers.Where(s => s.chatID == chatID).FirstOrDefault();
            if (sub == null)
            {
                sub = new Subscribers();
                sub.chatID = chatID;
                sub.mailprocess = Subscribers.MailProcess.read;
                db.subscribers.Add(sub);
            }

            string command = message.Text;

            if (string.IsNullOrEmpty(command))
                return Task.CompletedTask;

            command = command.ToLower().Replace("@" + botUser.Username.ToLower(), "");

            List<string> commandParameters = new List<string>();
            if (command.StartsWith("/"))
            {
                commandParameters = command.Split(' ').ToList();
                command = commandParameters[0];
                commandParameters.RemoveAt(0);
            }

            try
            {
                switch (command)
                {
                    case start:
                    case help:
                        bot.SendTextMessageAsync(
                            chatId: chatID,
                            text:
                            @"
Thank you for using the TrashMailClient

Commands:
/start - Shows you this text
/generate - Generates a new mail for you
/custom - Generate a new custom mail
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
                            replyMarkup: recieveOptions
                        );
                        break;
                    case generate:
                    case generatemail:
                    case custom:
                        string customMail = "";
                        if (commandParameters != null && commandParameters.Count > 0)
                            customMail = commandParameters[0];
                        AnswerGenerateMail(db, sub, chatID, command, customMail);
                        break;
                }
            }
            catch
            {

            }

            db.SaveChanges();
            return Task.CompletedTask;
        }

        static void AnswerGenerateMail(DatabaseContext db, Subscribers sender, long chatID, string command, string customMail, int messageID = 0)
        {
            var currentlyActiveMails = db.activemails.Where(a => a.endDate > DateTime.Now && a.subscriber == sender).ToArray();

            try
            {
                string generatedMail = "";
                if (command == custom)
                {
                    if (!string.IsNullOrEmpty(customMail))
                    {
                        generatedMail = customMail;
                    }
                    else
                    {
                        string botText = "This is not how you use this command." + Environment.NewLine + "Try this:" + Environment.NewLine + Environment.NewLine + "/custom <user@domain>" + Environment.NewLine + Environment.NewLine + "Valid domains are:" + Environment.NewLine;

                        string exampleDomain = "";
                        foreach (var service in MailService.mailProviders.Keys)
                        {
                            foreach (string domain in MailService.mailProviders[service])
                            {
                                if (string.IsNullOrEmpty(exampleDomain))
                                    exampleDomain = domain;
                                botText += domain + Environment.NewLine;
                            }
                        }

                        botText += "For example:" + Environment.NewLine + "abc123@" + exampleDomain;


                        bot.SendTextMessageAsync(
                            chatId: chatID,
                            text: botText,
                            replyMarkup: replyMarkup
                        );
                        return;
                    }
                }
                else
                {
                    generatedMail = MailService.GenerateMail();
                }

                if (currentlyActiveMails.Length >= 5)
                {
                    bot.SendTextMessageAsync(
                        chatId: chatID,
                        text: "You have generated too many mails. Try again later.",
                        replyMarkup: replyMarkup
                    );
                    return;
                }

                if (MailService.Create(generatedMail) == null)
                {
                    bot.SendTextMessageAsync(
                        chatId: chatID,
                        text: generatedMail + " is not a valid address!"
                    );
                    return;
                }

                Message result = null;

                if (messageID == 0)
                {
                    result = bot.SendTextMessageAsync(
                        chatId: chatID,
                        text: generatedMail + Environment.NewLine + "This mail will be valid for another 15 minutes"
                    ).Result;
                }
                else
                {
                    result = bot.EditMessageTextAsync(
                        chatId: chatID,
                        messageId: messageID,
                        text: generatedMail + Environment.NewLine + "This mail will be valid for another 15 minutes"
                    ).Result;
                }

                ActiveMails activeMail = new ActiveMails();
                activeMail.address = generatedMail;
                activeMail.subscriber = sender;
                activeMail.messageID = result.MessageId;
                activeMail.endDate = DateTime.Now.AddMinutes(15);

                db.activemails.Add(activeMail);
            }
            catch
            {

            }
        }
    }
}
