using System;
using System.Net;
using System.Net.Mail;

namespace FirebirdWeb.Helpers
{
    public class EmailHelper
    {
        private readonly string _smtpHost = "smtp.gmail.com"; // change if needed
        private readonly int _smtpPort = 587;
        private readonly string _smtpUser = "jason.choo2004@gmail.com"; // replace
        private readonly string _smtpPass = "ioyn fsxs qzot jowe"; // replace

        public bool SendEmail(string toEmail, string subject, string body)
        {
            try
            {
                var message = new MailMessage();
                message.From = new MailAddress(_smtpUser, "FirebirdWeb OTP");
                message.To.Add(toEmail);
                message.Subject = subject;
                message.Body = body;
                message.IsBodyHtml = true;

                using var client = new SmtpClient(_smtpHost, _smtpPort)
                {
                    Credentials = new NetworkCredential(_smtpUser, _smtpPass),
                    EnableSsl = true
                };
                client.Send(message);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Email error: {ex.Message}");
                return false;
            }
        }
    }
}