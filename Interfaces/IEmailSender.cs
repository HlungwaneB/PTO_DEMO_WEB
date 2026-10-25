namespace PTO_Management_System.Interfaces
{

    
        public interface IEmailSender
        {
            bool IsEnabled { get; }
            Task SendAsync(string toEmail, string toName, string subject, string title, string message);
           Task SendCodeAsync(string toEmail, string toName, string code);
        }
   


}
