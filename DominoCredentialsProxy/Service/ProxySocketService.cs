using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DominoCredentialsProxy.Service
{
	public class ProxySocketService
	{
		private readonly ILoggerFactory _loggerFactory;
		private ILogger _logger;

		public ProxySocketService(ILoggerFactory loggerFactory)
		{
			_loggerFactory = loggerFactory;
			_logger = loggerFactory.CreateLogger(typeof(ProxySocketService));
		}

		internal void LogInfo(string msg, params object[] args)
			=> _logger.LogInformation($"{DateTime.Now:HH:mm:ss.fff} [t:{Thread.CurrentThread.ManagedThreadId}] {msg}", args);

		public async Task Start(IPAddress address, int port, CancellationToken ctx)
		{
			var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);

			//socket.Blocking = false;
			//socket.DontFragment = false;
			
			socket.Bind(new IPEndPoint(IPAddress.Any, 8087));
			socket.Listen(20);
			var tasks = new List<Task>();

			while (!ctx.IsCancellationRequested)
			{
				var clientSocket = await socket.AcceptAsync();

				async Task ClientTask(Socket client)
				{
					var sw = Stopwatch.StartNew();
					var remoteIp = client.RemoteEndPoint as IPEndPoint;
					LogInfo($"Start [{remoteIp}]");

					var buffer = new byte[1024];
					//var memory = new Memory<byte>(buffer);
					var readBuffer = new StringBuilder();
					var enc = Encoding.ASCII;

					do
					{
						while (client.Available <= 0 && !ctx.IsCancellationRequested)
							await Task.Delay(1, ctx);

						var bytes = await client.ReceiveAsync(buffer, SocketFlags.Partial);
						if (bytes <= 0)
							continue;

						readBuffer.Append(enc.GetString(buffer, 0, bytes));
						var readStr = readBuffer.ToString();

						if (readStr.IndexOf("\r\n\r\n", StringComparison.Ordinal) <= 0) 
							continue;

						LogInfo($"Read [{remoteIp}]: {readStr.Substring(0, readStr.IndexOf("\r\n", StringComparison.Ordinal))}");
						break;

					} while (!ctx.IsCancellationRequested);

					/*
					// ^^^ Это заменить на поиск пустой строки:
					var stream = new NetworkStream(client);
					//var sr = new StreamReader(stream, Encoding.ASCII);
					//await sr.ReadToEndAsync();

					var buffer = new byte[512];
					var memory = new Memory<byte>(buffer);
					var bytes = await stream.ReadAsync(memory, ctx);
					var str = Encoding.ASCII.GetString(buffer, 0, bytes);
					*/

					await Task.Delay(5000, ctx);

					var res = Encoding.ASCII.GetBytes(@"HTTP/1.1 200 OK
Content-Length: 0
Content-Type: text/plain

");
					await client.SendAsync(res, SocketFlags.Partial, ctx);

					/*
					await stream.WriteAsync(res, ctx);
					await stream.FlushAsync(ctx);

					stream.Close();
					*/
					client.Close();

					LogInfo($"Finish [{remoteIp}]: {sw.Elapsed}");
				}

				var thread = new Thread(async () => await ClientTask(clientSocket));
				thread.Start();
				//tasks.Add(ClientTask());
			}
		}
	}
}