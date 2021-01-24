using System;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitMQExample
{
    class Program
    {
        static void Main(string[] args)
        {
            //NOT: Unutulmamalıdır ki gerçek hayatta publisher ve consumer ayrı birer servis olarak oluşturulur ve işlem bu şekilde gerçekleştirilir!
            
            var email = new EmailTemplate
            {
                Title = "RabbitMQ",
                Message = "RabbitMQ test mesajı",
                Email = "rabbitmqtest6@gmail.com"
            };

            #region Publisher Service

            //RabbitMQ bağlantımızı oluşturuyoruz.
            var factory = new ConnectionFactory {HostName = "localhost",UserName = "admin",Password = "123456"};
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();
            
            channel.QueueDeclare(queue: "mail", durable: false, exclusive: false, autoDelete: false, arguments: null); //mail isminde bir kuyruk oluşturuyoruz.
            
            //Modeli json string formatına dönüştürüyoruz.
            string message = JsonConvert.SerializeObject(email);
            
            //Kuyruğa gönderilecek değeri byte'a çeviriyoruz.
            var body = Encoding.UTF8.GetBytes(message);
            for (int i = 0; i < 10; i++) //10 adet kuyruğa ekliyoruz.
            {
                //Mesajı RabbitMQ'ya ekliyoruz.
                channel.BasicPublish(exchange:"",routingKey:"mail",null,body); //routingKey mail olan kuyruğa verilerimizi ekliyoruz.
            }
            Console.WriteLine("Mesaj başarıyla kuyruğa alındı!");

            #endregion

            #region Consumer Service
            
            using var channelConsumer = connection.CreateModel();
 
            //mail kuyruğuna erişiyoruz.
            channelConsumer.QueueDeclare("mail", false, false, false, null);

            //Kuyruktan mail ile ilgili var olan verileri alıyoruz.
            var consumer = new EventingBasicConsumer(channelConsumer);
            consumer.Received += (model, ea) => //mail kuyruğunda ki verileri dinliyoruz.
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                var response = JsonConvert.DeserializeObject<EmailTemplate>(message);
                Console.WriteLine($"Başlık: {response.Title}, Mail: {response.Email}, Mesaj: {response.Message}"); //Kuyruktan gelen verileri consol ekranına basıyoruz.
                channelConsumer.BasicAck(ea.DeliveryTag,true); //Knowledge bilgisi göndererek kuyruktan aldığımızı okuduğumuzu ve işlediğimiz bildiriyoruz.
                
                #region SMTP Mail

                var fromAddress = new MailAddress("rabbitmqtest6@gmail.com", "Gönderen");
                var toAddress = new MailAddress(response.Email, "İletilen");
                const string fromPassword = "R4bb1tMQ6";
                var subject = response.Title;
                var bodyMail = response.Message;

                var smtp = new SmtpClient
                {
                    Host = "smtp.gmail.com",
                    Port = 587,
                    EnableSsl = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(fromAddress.Address, fromPassword)
                };
                using var messageMail = new MailMessage(fromAddress, toAddress) {Subject = subject, Body = bodyMail};

                smtp.Send(messageMail);
                #endregion
            };
            channelConsumer.BasicConsume("mail", false, consumer);
            Console.WriteLine("Mesaj başarıyla kuyruktan alındı!");

            #endregion

            Console.ReadLine();
        }
    }
}