using System;
using System.ComponentModel.DataAnnotations;

namespace Octopus.Client.Model
{
    public class SmtpConfigurationResource : Resource
    {
        [Writeable]
        public string SmtpHost { get; set; }

        [Writeable, Required]
        public int? SmtpPort { get; set; }

        [Writeable]
        public string SendEmailFrom { get; set; }

        [Writeable]
        public string SmtpLogin { get; set; }

        [Writeable]
        public bool EnableSsl { get; set; }

        [NotReadable]
        [Writeable]
        public string NewSmtpPassword { get; set; }
    }
}