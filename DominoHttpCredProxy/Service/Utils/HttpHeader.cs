using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LotusHttpCredProxy.Service.Utils
{
	public class HttpHeader
	{
		private readonly MemoryStream _cache;

		/// <summary> Values positions in the <see cref="_cache"/>: [FirstCharNum, ValueLength] </summary>
		private readonly List<KeyValuePair<int, int>> _values = new List<KeyValuePair<int, int>>();

		public HttpHeader(MemoryStream cache)
		{
			_cache = cache;
		}
		
		private string _nameCached;

		public string Name
		{
			get
			{
				if (_nameCached != null) // Return if cached name exists.
					return _nameCached;
				
				var name = ValueBytesAll().TakeWhile(ch => ch != ':').ToList(); // Find name until column char.
				_nameCached = Encoding.ASCII.GetString(name.ToArray()).Trim();
				return _nameCached;
			}
		}

		public byte[] ValueBytes
		{
			get
			{
				var name = Name;
				var bytes = ValueBytesAll().Skip(name.Length + 1).ToArray();

				if (bytes.FirstOrDefault() == ' ')
					bytes = bytes.Skip(1).ToArray();
				return bytes;
			}
		}

		public string Value => Encoding.ASCII.GetString(ValueBytes);

		/// <summary> All header bytes including name and value. </summary>
		public IEnumerable<byte> ValueBytesAll()
		{
			foreach (var (charPos, valueLen) in _values)
			{
				for (var n = charPos; n < charPos + valueLen; n++)
					yield return _cache.GetBuffer()[n];
			}
		}

		/// <summary> Adding value range. </summary>
		public void AddValuePart(int startValue, int lengthValue)
		{
			_nameCached = null;
			_values.Add(new KeyValuePair<int, int>(startValue, lengthValue));
		}
	}
}