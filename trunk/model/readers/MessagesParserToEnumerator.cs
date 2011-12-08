﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LogJoint
{
	public static class MessagesParserToEnumerator
	{
		[Flags]
		public enum ParserAsEnumeratorFlag
		{
			Default = 0,
			YieldLastNullMessage = 1
		};

		public static IEnumerable<MessageBase> ParserAsEnumerator(IPositionedMessagesParser parser, ParserAsEnumeratorFlag flags = ParserAsEnumeratorFlag.Default, Action<Exception> readExceptionHandler = null)
		{
			for (; ; )
			{
				MessageBase msg = null;
				try
				{
					msg = parser.ReadNext();
				}
				catch (Exception e)
				{
					if (readExceptionHandler != null)
						readExceptionHandler(e);
					else
						throw;
				}
				if (msg == null)
				{
					if ((flags & ParserAsEnumeratorFlag.YieldLastNullMessage) != 0)
						yield return msg;
					break;
				}
				yield return msg;
			}
		}

		public static IPositionedMessagesParser EnumeratorAsParser(IEnumerable<MessageBase> enumerable)
		{
			return new EnumeratorAsParserImpl(enumerable);
		}

		class EnumeratorAsParserImpl : IPositionedMessagesParser
		{
			public EnumeratorAsParserImpl(IEnumerable<MessageBase> enumerable)
			{
				if (enumerable == null)
					throw new ArgumentNullException("enumerable");
				this.enumerable = enumerable;
			}

			public MessageBase ReadNext()
			{
				if (disposed)
					throw new ObjectDisposedException("EnumeratorAsParser");
				if (enumerator == null)
					enumerator = enumerable.GetEnumerator();
				if (!enumerator.MoveNext())
					return null;
				return enumerator.Current;
			}

			public void Dispose()
			{
				if (disposed)
					return;
				disposed = true;
				if (enumerator != null)
					enumerator.Dispose();
			}

			readonly IEnumerable<MessageBase> enumerable;
			IEnumerator<MessageBase> enumerator;
			bool disposed;
		};
	}
}
