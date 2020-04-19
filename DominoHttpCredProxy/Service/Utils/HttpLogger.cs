using System;
using Microsoft.Extensions.Logging;

namespace LotusHttpCredProxy.Service.Utils
{
	public abstract class HttpLogger<T>
	{
		private ILoggerFactory _loggerFactory;
		private ILogger _logger;

		public ILoggerFactory LoggerFactory
		{
			get => _loggerFactory;
			set
			{
				_loggerFactory = value;
				_logger = _loggerFactory?.CreateLogger(typeof(T));
			}
		}

		protected void Log(LogLevel level, string msg, params object[] args)
			=> _logger?.Log(level, $"{DateTime.Now:HH:mm:ss.fff} {msg}", args);
	}
}