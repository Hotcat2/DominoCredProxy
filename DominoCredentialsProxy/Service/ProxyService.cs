using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DominoCredentialsProxy.Service.ProxyUtils;
using Microsoft.Extensions.Logging;

namespace DominoCredentialsProxy.Service
{
	public class ProxyService
	{
		private readonly ILoggerFactory _loggerFactory;
		private readonly ILogger _logger;
		private readonly List<Task> _processingTasks = new List<Task>();

		public ProxyService(ILoggerFactory loggerFactory)
		{
			_loggerFactory = loggerFactory;
			_logger = loggerFactory.CreateLogger(typeof(ProxyService));
		}

		public async Task Start(IPAddress address, int port, CancellationToken ctx)
		{
			var listener = new TcpListener(address, port); // ^^^ Навесить SSL.
			ctx.Register(() => listener.Stop());
			listener.Start();
			
			while (!ctx.IsCancellationRequested)
			{
				//var client = listener.AcceptTcpClient();
				//ProcessClient(client, ctx);

				/*
				var socket = await listener.AcceptSocketAsync(); // ^^^ Сделать чтобы зависело от ctx (через TokenSource...).
				socket.NoDelay = true;
				//socket.
				socket.Blocking = false;
				var mem = new Memory<byte>(new byte[512]);
				var receive = await socket.ReceiveAsync(mem, SocketFlags.Peek, ctx);
				*/

				LogInfo("#1");
				var client = await listener.AcceptSocketAsync(); // ^^^ Похоже он блокирующий даже в асинхронном режиме..
				LogInfo("#2");
				//var stream = new NetworkStream(client);
				var task = Task.Run(async () => await ProcessClientAsync(client, ctx), ctx);
				LogInfo("#3");

				// ^^^ _processingTasks.RemoveAll(t => t.IsCompleted);
				_processingTasks.Add(task);
			}

			// Waiting all pending task, maximum 3 sec.
			await Task.WhenAny(Task.WhenAll(_processingTasks.ToArray()), Task.Delay(3000, CancellationToken.None));
		}
		
		/* Синхронная работа через клиента.
		private void ProcessClient(TcpClient client, CancellationToken ctx)
		{
			NetworkStream stream = null;
			try
			{
				_logger.LogInformation("Request from {0}", client.Client.RemoteEndPoint?.ToString());
				//client.Client.Blocking = false;

				HttpHeader headerHost = null; // HTTP header: Host.
				HttpHeader headerLotusUri = null; // HTTP header: X-LotusUri (base64 encoded).
				HttpHeader headerAuth = null; // HTTP header: Authorization.

				// If all required headers are found.
				bool AllHeadersFound() => headerHost != null && headerLotusUri != null && headerAuth != null;
				stream = client.GetStream();
				var headersReader = new HttpHeadersReader(stream);
				HttpHeader firstHeader = null;
				var headers = new List<HttpHeader>();

				// Comparing v1 and v2.
				static bool Equals(string v1, string v2) => v1.Equals(v2, StringComparison.InvariantCultureIgnoreCase);
				var dominoClient = new DominoClient();

				//using var sr = new StreamReader(stream);
				//var reqString = sr.ReadToEnd();

				foreach (var header in headersReader.Headers())
				{
					if (firstHeader == null)
						firstHeader = header;
					else if (Equals(header.Name, "Host"))
						headerHost = header;
					else if (Equals(header.Name, "Authorization"))
					{
						headerAuth = header;
						// ^^^ Тут запустить таску на проверку валидного JWT, вытаскивания IndividualID и получение лотусового пароля.
					}
					else if (Equals(header.Name, "X-LotusUri"))
					{
						headerLotusUri = header;
						// ^^^ Тут можно запускать таску на открытие соединения с Domino, уже можно послать первый хидер и хидер "host":
						dominoClient.CreateDominoConnection(header.Value, ctx);
					}
					else
						headers.Add(header);

					if (AllHeadersFound()) // Then send remaining HTTP request to Domino "as is".
						break;
				}
				
				if (!AllHeadersFound())
				{
					ReturnHttpError400(stream, ctx);

					//client.GetStream().Close();
					//client.Close();
					return;
				}

				//var lotusClient = new WebClient();
				//lotusClient.
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error processing TCP connection.");
			}
			finally
			{
				stream?.Close();
				client.Close();
			}
		}

		/// <summary> Returning error HTTP400 (BadRequest). </summary>
		private static void ReturnHttpError400(Stream stream, CancellationToken ctx)
		{
			var enc = Encoding.ASCII;
			var content = enc.GetBytes("Some error.");

			var res = enc.GetBytes("HTTP/1.1 400 BadRequest\r\n" 
										  + "Content-Length: " + content.Length + "\r\n"
										  + "Content-Type: text/plain\r\n"
										  + "\r\n");
			stream.Write(res);

			stream.Write(content);
			stream.Flush();

			//var sw = new StreamWriter(stream);
			//await sw.WriteLineAsync("HTTP/1.1 400 Bad Request\r\n\r\n".ToCharArray(), ctx);
			//await sw.WriteLineAsync("".ToCharArray(), ctx);
			//await sw.WriteLineAsync("".ToCharArray(), ctx);

			//await sw.FlushAsync();
		}
		*/

		internal void LogInfo(string msg, params object[] args)
			=> _logger.LogInformation($"{DateTime.Now:HH:mm:ss.fff} {msg}", args);

		// /* Асинхронная работа через сокет.
		private async Task ProcessClientAsync(Socket client, CancellationToken ctx)
		{
			var remoteIp = client.RemoteEndPoint as IPEndPoint;
			var remoteAddress = remoteIp?.Address + ":" + remoteIp?.Port;
			var timer = DateTime.Now;

			try
			{
				LogInfo("Request from {0}", remoteAddress);
				client.Blocking = false;

				HttpHeader headerHost = null; // HTTP header: Host.
				HttpHeader headerLotusUri = null; // HTTP header: X-LotusUri (base64 encoded).
				HttpHeader headerAuth = null; // HTTP header: Authorization.

				// If all required headers are found.
				bool AllHeadersFound() => headerHost != null && headerLotusUri != null && headerAuth != null;
				//await using var stream = client.GetStream();
				//var stream = new NetworkStream(client, true); // ^^^ Подумать про это.

				var headersReader = new HttpSocketHeadersReader(_loggerFactory, remoteAddress, client);
				HttpHeader firstHeader = null;
				var headers = new List<HttpHeader>();

				// Comparing v1 and v2.
				static bool Equals(string v1, string v2) => v1.Equals(v2, StringComparison.InvariantCultureIgnoreCase);
				var dominoClient = new DominoClient();

				client.ReceiveTimeout = 30000;
				client.SendTimeout = 30000;

				await foreach (var header in headersReader.HeadersAsync(ctx))
				{
					if (firstHeader == null)
						firstHeader = header;
					else if (Equals(header.Name, "Host"))
						headerHost = header;
					else if (Equals(header.Name, "Authorization"))
					{
						headerAuth = header;
						// ^^^ Тут запустить таску на проверку валидного JWT, вытаскивания IndividualID и получение лотусового пароля.
					}
					else if (Equals(header.Name, "X-LotusUri"))
					{
						headerLotusUri = header;
						// ^^^ Тут можно запускать таску на открытие соединения с Domino, уже можно послать первый хидер и хидер "host":
						dominoClient.CreateDominoConnection(header.Value, ctx);
					}
					else
						headers.Add(header);

					if (AllHeadersFound()) // Then send remaining HTTP request to Domino "as is".
						break;
				}

				
				if (headers.Count <= 0)
				{
					await Task.Delay(1000, ctx);
					throw new Exception($"Empty input headers.");
				}

				await Task.Delay(3000, ctx); // ^^^ REMOVE
				// ^^^ Тут при считывании Body запроса ждать пока не появится Available>0, значит нужно учитывать Content-Length, либо Transfer-Encoding: chunked.

				if (!AllHeadersFound())
				{
					await ReturnHttpError400Async(client, ctx);

					//client.GetStream().Close();
					//client.Close();
					return;
				}
				
				//var lotusClient = new WebClient();
				//lotusClient.
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error processing TCP connection.");
			}
			finally
			{
				client.Close();
				LogInfo("Finished {0} ({1})", remoteAddress, DateTime.Now - timer);
			}
		}

		/// <summary> Returning error HTTP400 (BadRequest). </summary>
		private static async Task ReturnHttpError400Async(Socket client, CancellationToken ctx)
		{
			var enc = Encoding.ASCII;
			var content = enc.GetBytes("Some error (async).");

			var res = enc.GetBytes("HTTP/1.1 400 BadRequest\r\n"
			                       + "Content-Length: " + content.Length + "\r\n"
			                       + "Content-Type: text/plain\r\n"
			                       + "\r\n");

			async Task Send(byte[] bytes) => await client.SendAsync(new ArraySegment<byte>(bytes), SocketFlags.Partial, ctx);

			await Send(res);
			await Send(content);
		}
		// */

		/*
		/// <summary> Асинхронная работа через стрим. </summary>
		private async Task ProcessClientAsync(Socket client, NetworkStream stream, CancellationToken ctx)
		{
			var remoteIp = client.RemoteEndPoint as IPEndPoint;
			var remoteAddress = remoteIp?.Address + ":" + remoteIp?.Port;
			var timer = DateTime.Now;

			try
			{
				LogInfo("Request from {0}", remoteAddress);
				client.Blocking = false;

				HttpHeader headerHost = null; // HTTP header: Host.
				HttpHeader headerLotusUri = null; // HTTP header: X-LotusUri (base64 encoded).
				HttpHeader headerAuth = null; // HTTP header: Authorization.

				// If all required headers are found.
				bool AllHeadersFound() => headerHost != null && headerLotusUri != null && headerAuth != null;
				//await using var stream = client.GetStream();
				//var stream = new NetworkStream(client, true); // ^^^ Подумать про это.

				var headersReader = new HttpHeadersReaderFromStream(_loggerFactory, remoteAddress, client, stream);
				HttpHeader firstHeader = null;
				var headers = new List<HttpHeader>();

				// Comparing v1 and v2.
				static bool Equals(string v1, string v2) => v1.Equals(v2, StringComparison.InvariantCultureIgnoreCase);
				var dominoClient = new DominoClient();

				client.ReceiveTimeout = 30000;
				client.SendTimeout = 30000;

				await foreach (var header in headersReader.HeadersAsync(ctx))
				{
					if (firstHeader == null)
						firstHeader = header;
					else if (Equals(header.Name, "Host"))
						headerHost = header;
					else if (Equals(header.Name, "Authorization"))
					{
						headerAuth = header;
						// ^^^ Тут запустить таску на проверку валидного JWT, вытаскивания IndividualID и получение лотусового пароля.
					}
					else if (Equals(header.Name, "X-LotusUri"))
					{
						headerLotusUri = header;
						// ^^^ Тут можно запускать таску на открытие соединения с Domino, уже можно послать первый хидер и хидер "host":
						dominoClient.CreateDominoConnection(header.Value, ctx);
					}
					else
						headers.Add(header);

					if (AllHeadersFound()) // Then send remaining HTTP request to Domino "as is".
						break;
				}

				if (headers.Count <= 0)
				{
					await Task.Delay(1000, ctx);
					throw new Exception("Empty input headers.");
				}

				await Task.Delay(3000, ctx); // ^^^ REMOVE
				
				if (!AllHeadersFound())
				{
					await ReturnHttpError400Async(client, stream, ctx);

					//client.GetStream().Close();
					//client.Close();
					return;
				}

				//var lotusClient = new WebClient();
				//lotusClient.
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error processing TCP connection.");
			}
			finally
			{
				stream.Close();
				client.Close();
				LogInfo("Finished {0} ({1})", remoteAddress, DateTime.Now - timer);
			}
		}

		/// <summary> Returning error HTTP400 (BadRequest). </summary>
		private static async Task ReturnHttpError400Async(Socket client, NetworkStream stream, CancellationToken ctx)
		{
			var enc = Encoding.ASCII;

			var content = enc.GetBytes("Some error (async).");
			var headers = enc.GetBytes("HTTP/1.1 400 BadRequest\r\n"
										  + "Content-Length: " + content.Length + "\r\n"
										  + "Content-Type: text/plain\r\n"
										  + "\r\n");

			async Task Send(byte[] bytes) 
				=> await stream.WriteAsync(bytes, 0, bytes.Length, ctx); //client.SendAsync(new ArraySegment<byte>(bytes), SocketFlags.Partial, ctx);

			await Send(headers);
			await Send(content);
			await stream.FlushAsync(ctx);
		}
		*/
	}
}
