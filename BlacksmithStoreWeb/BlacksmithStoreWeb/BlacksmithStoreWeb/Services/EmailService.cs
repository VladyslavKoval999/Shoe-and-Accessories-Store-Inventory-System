using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace BlacksmithStoreWeb.Services
{
    public class EmailService
    {
        private readonly string _smtpUser = "blacksmithstoreinfo@gmail.com";
        private readonly string _smtpPass = "************"; //

        public async Task SendRestockEmailAsync(string toEmail, string userName, string productName)
        {
            using var client = new SmtpClient("smtp.gmail.com", 587)
            {
                Credentials = new NetworkCredential(_smtpUser, _smtpPass),
                EnableSsl = true
            };

            string subject = "Товар знову в наявності! - Blacksmith Store";
            string body = $@"
                <h3>Вітаємо, {userName}!</h3>
                <p>Товар <b>{productName}</b>, який ви очікували, знову з'явився у наявності в нашому магазині.</p>
                <p>Ви можете перейти на сайт та оформити замовлення, поки він є в наявності!</p>
                <br/>
                <p>З повагою,<br/>Команда Blacksmith Store</p>
            ";

            var mailMessage = new MailMessage
            {
                From = new MailAddress(_smtpUser, "Blacksmith Store"),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            mailMessage.To.Add(toEmail);

            await client.SendMailAsync(mailMessage);
        }
    }
}