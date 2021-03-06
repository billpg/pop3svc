﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Linq;
using billpg.pop3;

namespace BuildYourOwnPop3Service
{
    class Program
    {
        static void Main()
        {
            /* Launch POP3. */
            var pop3 = new POP3Listener();
            /* Add just before the ListenOn call. */
            pop3.Provider = new MyProvider();
            pop3.ListenOn(IPAddress.Loopback, 110, false);

            /* Keep running until the process is killed. */
            while (true) System.Threading.Thread.Sleep(10000);
        }
    }

    /* New class separate from the Program class. */
    class MyProvider : IPOP3MailboxProvider
    {
        /* Inside the MyProvider class. */
        public string Name => "My Provider";

        public IPOP3Mailbox Authenticate(
            IPOP3ConnectionInfo info,
            string username,
            string password)
        {
            if (username == "me" && password == "passw0rd")
                return new MyMailbox();
            else
                return null;
        }

        /* This is necessary, but we can ignore it. */
        public void RegisterNewMessageAction(
            RaiseNewMessageEvent onNewMessage)
        { }
    }

    class MyMailbox : IPOP3Mailbox
    {
        public string UserID(IPOP3ConnectionInfo info)
    => "Mr Rutabaga";

        const string FOLDER = @"C:\MyMailbox\";

        public IList<string> ListMessageUniqueIDs(
            IPOP3ConnectionInfo info)
            => Directory.GetFiles(FOLDER)
                   .Select(Path.GetFileName)
                   .ToList();

        public bool MessageExists(
            IPOP3ConnectionInfo info,
            string uniqueID)
            => ListMessageUniqueIDs(info)
                   .Contains(uniqueID);

        public bool MailboxIsReadOnly(
    IPOP3ConnectionInfo info)
    => false;



        public long MessageSize(
   IPOP3ConnectionInfo info,
   string uniqueID)
   => 58;

        /* Replace the MessageContents function. */
        public IMessageContent MessageContents(
            IPOP3ConnectionInfo info,
            string uniqueID)
        {
            if (MessageExists(info, uniqueID))
                return new MyMessageContents(
                               Path.Combine(FOLDER, uniqueID));
            else
                return null;
        }

        public void MessageDelete(
     IPOP3ConnectionInfo info,
     IList<string> uniqueIDs)
        {
            foreach (var toDelete in uniqueIDs)
                if (MessageExists(info, toDelete))
                    File.Delete(Path.Combine(FOLDER, toDelete));
        }

    }


    /* New class. */
    class MyMessageContents : IMessageContent
    {
        List<string> lines;
        int index;

        public MyMessageContents(string path)
        {
            lines = File.ReadAllLines(path).ToList();
            index = 0;
        }

        public string NextLine()
            => (index < lines.Count) ? lines[index++] : null;

        public void Close()
        {
        }
    }

}