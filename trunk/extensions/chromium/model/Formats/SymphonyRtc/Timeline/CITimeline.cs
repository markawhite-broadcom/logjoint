﻿using LogJoint.Analytics;
using LogJoint.Analytics.Timeline;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Text.RegularExpressions;

namespace LogJoint.Symphony.Rtc
{
	public interface ITimelineEvents
	{
		IEnumerableAsync<Event[]> GetEvents(IEnumerableAsync<MessagePrefixesPair[]> input);
	};

	public class TimelineEvents : ITimelineEvents
	{
		public TimelineEvents(
			IPrefixMatcher matcher
		)
		{
		}


		IEnumerableAsync<Event[]> ITimelineEvents.GetEvents(IEnumerableAsync<MessagePrefixesPair[]> input)
		{
			return input.Select<MessagePrefixesPair, Event>(GetEvents, GetFinalEvents);
		}

		void GetEvents(MessagePrefixesPair msgPfx, Queue<Event> buffer)
		{
			string id, type;
			if (logableIdUtils.TryParseLogableId(msgPfx.Message.Logger.Value, out type, out id))
			{
				switch (type)
				{
					case "ui.overlay":
						GetFlowInitiatorEvents(msgPfx, buffer, id);
						break;
					case "ui.localMedia":
						GetLocalMediaUIEvents(msgPfx, buffer, id);
						break;
					case "localMedia":
						GetLocalMediaEvents(msgPfx, buffer, id);
						break;
				}
			}
			GetCIEvents(msgPfx, buffer);
		}

		void GetFinalEvents(Queue<Event> buffer)
		{
		}

		void GetLocalMediaUIEvents(MessagePrefixesPair msgPfx, Queue<Event> buffer, string loggableId)
		{
			Match m;
			var msg = msgPfx.Message;
			if ((m = localMediaUIButtonRegex.Match(msg.Text)).Success)
			{
				buffer.Enqueue(new UserActionEvent(msg, m.Groups["btn"].Value));
			}
		}

		void GetLocalMediaEvents(MessagePrefixesPair msgPfx, Queue<Event> buffer, string loggableId)
		{
			Match m;
			var msg = msgPfx.Message;
			if ((m = localMediaOfferRegex.Match(msg.Text)).Success)
			{
				var action = m.Groups["action"].Value;
				var offer = m.Groups["offerId"].Value;
				buffer.Enqueue(new ProcedureEvent(
						msg, offer, offer, action == "processing" ? ActivityEventType.Begin : ActivityEventType.End));
			}
		}

		void GetFlowInitiatorEvents(MessagePrefixesPair msgPfx, Queue<Event> buffer, string loggableId)
		{
			var msg = msgPfx.Message;
			if (msg.Text == "leave flow")
			{
				buffer.Enqueue(new UserActionEvent(msg, "leave"));
			}
		}

		void GetCIEvents(MessagePrefixesPair msgPfx, Queue<Event> buffer)
		{
			Match m = ciTestRegex.Match(msgPfx.Message.Text);
			if (m.Success)
			{
				buffer.Enqueue(new ProcedureEvent(
					msgPfx.Message, m.Groups["test"].Value, m.Groups["test"].Value, m.Groups["p["].Value == "start" ? ActivityEventType.Begin : ActivityEventType.End));
			}
		}

		readonly LogableIdUtils logableIdUtils = new LogableIdUtils();
		static readonly RegexOptions reopts = RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.Multiline;
		readonly Regex localMediaUIButtonRegex = new Regex(@"^(?<btn>audio|video|screen) button pressed$", reopts);
		readonly Regex localMediaOfferRegex = new Regex(@"^(?<action>processing|processed) (?<offerId>\S+)$", reopts);
		readonly Regex ciTestRegex = new Regex(@"^TEST (?<op>start|done): (?<test>)", reopts);
	}
}
