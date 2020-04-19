using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using LotusHttpCredProxy.Service;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace LotusCredProxy
{
	class Program
	{
		static async Task Main(string[] args)
		{
			Console.WriteLine("Staring Lotus Proxy Server.");

			var loggerFactory = LoggerFactory.Create(builder =>builder.AddConsole(options => options.Format = ConsoleLoggerFormat.Systemd));
			//loggerFactory.AddProvider();

			var ctxSource = new CancellationTokenSource();
			//var proxyServer = new ProxyService(loggerFactory);
			//var proxyServer = new ProxySocketService(loggerFactory);
			var proxyServer = new LotusCredProxyService {LoggerFactory = loggerFactory};
			await proxyServer.Start(IPAddress.Any, 8087, ctxSource.Token);

		}
	}
}
