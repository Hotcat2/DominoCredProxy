using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace LotusHttpCredProxy.Service.Utils
{
	internal class HttpStreamHelper : HttpLogger<LotusCredProxyService>
	{
		private readonly NetworkStream _stream;
		internal MemoryStream Cache { get; } = new MemoryStream();
		public int CurrentCharNum { get; private set; } = -1;

		public HttpStreamHelper(NetworkStream stream)
		{
			_stream = stream;
		}

		public async Task<HttpHeader[]> GetRequestHeaders(CancellationToken ctx)
		{
			//var symbolsNewline = Encoding.ASCII.GetBytes("\r\n");
			//var symbolsSpaceOrTab = Encoding.ASCII.GetBytes("\t" + string.Empty);

			bool IsNewlineChar(byte ch) => ch == '\r' || ch == '\n';
			bool IsSpaceOrTabChar(byte ch) => ch == '\t' || char.IsWhiteSpace((char) ch); // ^^^ Проверить что корректно работает.
			
			var buffer = new byte[128]; // ^^^ Вернуть: [1024];
			var headers = new List<HttpHeader>();
			HttpHeader header = null;

			var startValue = -1; // First char num of the current header's value.
			byte prevNewLineChar = 0; // Previous new line char num.
			var headersComplete = false; // Header bytes completed for current bytes.
			var headersCompleteAll = false; // Are header bytes completed.

			do
			{
				var readingTimer = new Stopwatch();
				readingTimer.Restart();

				while (!_stream.DataAvailable)
				{
					if (readingTimer.ElapsedMilliseconds > _stream.ReadTimeout)
						throw new TimeoutException("Reading headers is too long");
					await Task.Delay(5, ctx);
				}

				var bytesRead = await _stream.ReadAsync(buffer, ctx);
				Cache.Write(buffer, 0, bytesRead);

				for (var n = 0; n < bytesRead; n++)
				{
					CurrentCharNum++;
					var ch = buffer[n];

					if (headersComplete)
					{
						if (!IsNewlineChar(ch)) // Return back to one char if there is not LF char.
							CurrentCharNum--;
						headersCompleteAll = true;
						break;
					}

					if (ch == prevNewLineChar) // NewLined appeared twice - HTTP header completed.
					{
						headers.Add(header);
						header = null;
						headersComplete = true;
					} else if (IsNewlineChar(ch))
					{
						if (startValue >= 0) // If newline char meets first time.
							header?.AddValuePart(startValue, CurrentCharNum - startValue);
						startValue = -1;
						if (prevNewLineChar == 0)
							prevNewLineChar = ch;
					}
					else if (prevNewLineChar > 0 && IsSpaceOrTabChar(ch)) // If prev char is newline and space or tab appeared.
					{
						startValue = CurrentCharNum;
						prevNewLineChar = 0;
					}
					else if (startValue < 0) // If new header appeared.
					{
						if (header != null)
							headers.Add(header);
						header = new HttpHeader(Cache);

						prevNewLineChar = 0;
						startValue = CurrentCharNum;
					}
				}

			} while (!headersCompleteAll && !ctx.IsCancellationRequested);

			return headers.ToArray();
		}
	}
}