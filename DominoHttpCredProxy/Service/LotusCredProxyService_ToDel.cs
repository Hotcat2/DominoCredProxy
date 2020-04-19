using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LotusHttpCredProxy.Service
{
	public class LotusCredProxyService_ToDel // ^^^ REMOVE
	{
		private readonly ILoggerFactory _loggerFactory;
		private ILogger _logger;

		public LotusCredProxyService_ToDel(ILoggerFactory loggerFactory)
		{
			_loggerFactory = loggerFactory;
			_logger = loggerFactory.CreateLogger(typeof(LotusCredProxyService_ToDel));
		}

		private void Log(LogLevel level, string msg, params object[] args)
			=> _logger.Log(level, $"{DateTime.Now:HH:mm:ss.fff} [T:{Thread.CurrentThread.ManagedThreadId}] {msg}", args);

		public async Task Start(IPAddress addressToBind, int port, CancellationToken ctx)
		{
			/*
			var listener = new HttpListener();
			// установка адресов прослушки
			listener.Prefixes.Add("https://*:8443/"); // "http://*:80/" "http://localhost:8888/connection/"
			listener.AuthenticationSchemeSelectorDelegate = httpRequest => { httpRequest. };
			listener.Start();
			Console.WriteLine("Ожидание подключений...");
			// метод GetContext блокирует текущий поток, ожидая получение запроса 
			var context = await listener.GetContextAsync();
			//context.User.Identity.
			var request = context.Request;
			var inputStream = request.InputStream;
			//inputStream.CopyToAsync()
			// получаем объект ответа
			var response = context.Response;
			*/

			var certificate = X509Certificate.CreateFromCertFile("^^^");

			var tcpListener = new TcpListener(addressToBind, port);
			tcpListener.Start();
			var clientTasks = new List<Task>();

			while (!ctx.IsCancellationRequested)
			{
				var client = await tcpListener.AcceptTcpClientAsync();
				var clientTask = ProcessTcpClient(client, certificate, ctx);
				clientTasks.Add(clientTask);

				clientTasks.RemoveAll(t => !t.IsCompleted);
			}
		}

		private async Task ProcessTcpClient(TcpClient client, X509Certificate certificate, CancellationToken ctx)
		{
			var sw = Stopwatch.StartNew();
			var remoteIp = client.Client.RemoteEndPoint as IPEndPoint;
			Log(LogLevel.Information, $"Start [{remoteIp}]");

			client.ReceiveTimeout = 30000;
			client.SendTimeout = 30000;

			var stream = new SslStream(client.GetStream(), false);
			await stream.AuthenticateAsServerAsync(certificate, clientCertificateRequired: false, checkCertificateRevocation: true);
			var sr = new StreamReader(stream, Encoding.ASCII);

			var inputs = new List<string>();
			while (!ctx.IsCancellationRequested)
			{
				var str = await sr.ReadLineAsync();
				if (string.IsNullOrEmpty(str))
					break;
				inputs.Add(str);
			}

			Log(LogLevel.Information, $"Read [{remoteIp}]: : {sw.Elapsed} {inputs.FirstOrDefault()}");
			await Task.Delay(10000, ctx);

			var res = Encoding.ASCII.GetBytes(@"HTTP/1.1 200 OK
Content-Length: 0
Content-Type: text/plain

");
			await stream.WriteAsync(res, ctx);
			//LogInfo($"PreFinish [{remoteIp}]: {sw.Elapsed}");

			stream.Close();
			client.Close();

			Log(LogLevel.Information, $"Finish [{remoteIp}]: {sw.Elapsed}");
		}
	}
}