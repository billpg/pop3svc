/* billpg industries POP3 Listener. */
/* Copyright, William Godfrey 2021. All Rights Reserved. */
/* https://billpg.com/POP3/ */

using billpg.pop3svc;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Pop3ServiceForm
{
    public partial class ServiceLauncher : Form, IPOP3MailboxProvider, IPOP3Mailbox
    {
        public ServiceLauncher()
        {
            InitializeComponent();
        }

        POP3Listener pop3;
        UserRecord SelectedUser => (UserRecord)usersList.SelectedItem;
        IEnumerable<UserRecord> AllUsers => usersList.Items.Cast<UserRecord>();

        private void ServiceLauncher_Load(object sender, EventArgs e)
        {
            UserRecord meUser = new UserRecord("me", "passw0rd");            
            string uid = $"{Guid.NewGuid()}".ToUpperInvariant();
            meUser.Messages.Add(uid, new MessageRecord(uid, "me@example.com", "you@example.com", "Welcome to billpg industries POP3 Service", "Rutabaga"));

            usersList.Items.Clear();
            usersList.Items.Add(meUser);
            usersList.SelectedIndex = 0;
            RefreshMessagesView();


            var cert = new X509Certificate2("SelfSigned.pfx", "Rutabaga");

            pop3 = new POP3Listener();
            pop3.ListenOnStandard(IPAddress.Loopback);
            pop3.RequireSecureLogin = true;
            pop3.SecureCertificate = cert;
            pop3.Provider = this;
        }

        private void AddLogEntry(string entry)
        {
            this.Invoke(new Action(Internal));
            void Internal()
            {
                listBox1.Items.Add(entry);
                listBox1.TopIndex = listBox1.Items.Count - 1;
            }
        }

        string IPOP3MailboxProvider.Name => "Pop3ServiceForm";
        static void DoNothing()
        { }
        string nextUserID = null;
        RaiseNewMessageEvent onNewMessage;

        void IPOP3MailboxProvider.RegisterNewMessageAction(RaiseNewMessageEvent onNewMessage)
        {
            this.onNewMessage = onNewMessage;
        }

        IPOP3Mailbox IPOP3MailboxProvider.Authenticate(IPOP3ConnectionInfo info, string user, string pass)
        {
            bool isAuth = (bool)this.Invoke(new Func<bool>(Internal));
            if (isAuth)
            {
                this.nextUserID = user;
                return this;
            }
            else
            {
                return null;
            }

            bool Internal()
            {
                foreach (var userAvail in AllUsers)
                {
                    if (userAvail.User == user && userAvail.Pass == pass)
                        return true;
                }

                return false;
            }
        }
    
        string IPOP3Mailbox.UserID(IPOP3ConnectionInfo info)
        {
            string userID = this.nextUserID;
            this.nextUserID = null;
            return userID;
        }

        IList<string> IPOP3Mailbox.ListMessageUniqueIDs(IPOP3ConnectionInfo info)
        {
            IList<string> uniqueIDs = null;
            this.Invoke(new Action(Internal));
            return uniqueIDs;

            void Internal()
            {
                uniqueIDs = UserByName(info.UserID).Messages.Keys.ToList();
            }
        }

        bool IPOP3Mailbox.MessageExists(IPOP3ConnectionInfo info, string uniqueID)
            => false;

        bool ShowMessageDeleteForm(string user, IList<string> messages)
        {
            var formResponse = MessageBox.Show(this, $"Delete {string.Join(",", messages)} ?", "Delete", MessageBoxButtons.YesNo);
            if (formResponse == DialogResult.Yes)
            {
                var allMessages = Program.MessageList(user);
                foreach (var messageToDelete in messages)
                {
                    allMessages.Remove(messageToDelete);
                }
                return true;
            }

            else
                return false;
        }

        long IPOP3Mailbox.MessageSize(IPOP3ConnectionInfo into, string uniqueID)
        {
            return (long)Invoke(new Func<long>(Internal));
            long Internal()
            {
                return UserByName(into.UserID).Messages[uniqueID].AsRfc822.Length;
            }
        }

        private UserRecord UserByName(string user) => AllUsers.Where(u => u.User == user).Single();


        IMessageContent IPOP3Mailbox.MessageContents(IPOP3ConnectionInfo info, string uniqueID)
        {
            return (IMessageContent)Invoke(new Func<IMessageContent>(Internal));
            IMessageContent Internal()
            {
                string rfc = UserByName(info.UserID).Messages[uniqueID].AsRfc822;
                var lines = rfc.Split('\r').Select(line => line.Trim()).ToList();
                return new WrapList(lines);
            }
        }


        private class WrapList : IMessageContent
        {
            private readonly List<string> msg;
            private int nextLineIndex;

            public WrapList(List<string> msg)
            {
                this.msg = msg;
                this.nextLineIndex = 0;
            }

            void IMessageContent.Close() { /* Nothing to do. */ }

            string IMessageContent.NextLine()
            {
                if (nextLineIndex < msg.Count)
                    return msg[nextLineIndex++];
                else
                    return null;
            }
        }


        void IPOP3Mailbox.MessageDelete(IPOP3ConnectionInfo info, IList<string> uniqueIDs)
        {
            Invoke(new Action(Internal));
            void Internal()
            {
                var user = UserByName(info.UserID);
                foreach (var msgToRemove in uniqueIDs)
                    user.Messages.Remove(msgToRemove);

                RefreshMessagesView();
            }
        }

        private void addUserNameTextBox_TextChanged(object sender, EventArgs e)
        {
            bool isOkay = addUserNameTextBox.Text.Length > 0 && addUserPassTextBox.Text.Length > 0;
            addUserCmd.Enabled = isOkay;
        }

        private void usersList_SelectedIndexChanged(object sender, EventArgs e)
        {
            RefreshMessagesView();
        }

        private void RefreshMessagesView()
        {
            messageList.Rows.Clear();
            if (SelectedUser == null)
                return;

            foreach (var message in SelectedUser.Messages.Values)
            {
                messageList.Rows.Add(message.uniqueID, message.fromAddress, message.toAddress, message.subjectText, message.bodyText);
            }
        }

        private void addMessageCmd_Click(object sender, EventArgs e)
        {
            var frm = new MessageContentsForm();
            frm.UniqueID = $"{Guid.NewGuid()}".ToUpperInvariant();
            var result = frm.ShowDialog(this);
            if (result == DialogResult.OK)
            {
                SelectedUser.Messages[frm.UniqueID] = frm.AsMessageRecord;
                RefreshMessagesView();
                onNewMessage(SelectedUser.User);
            }
        }

        private void addUserCmd_Click(object sender, EventArgs e)
        {
            usersList.Items.Add(new UserRecord(addUserNameTextBox.Text, addUserPassTextBox.Text));
            usersList.SelectedIndex = usersList.Items.Count - 1;
            RefreshMessagesView();
            addUserNameTextBox.Text = "";
            addUserPassTextBox.Text = "";
        }
    }
}
