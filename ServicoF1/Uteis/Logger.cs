using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServicoF1.Uteis
{
    internal class Logger
    {
        private readonly ILogger<Worker> _logger;

        public Logger(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        public void LogError(string? message, params object?[] args)
        {
            if(string.IsNullOrEmpty(message))
                throw new ArgumentNullException(nameof(message));

            _logger.LogError(message, args);
        }
    }
}
