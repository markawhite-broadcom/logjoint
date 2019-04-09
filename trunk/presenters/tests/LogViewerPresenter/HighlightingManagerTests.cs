﻿using System;
using System.Linq;
using LogJoint.UI.Presenters.LogViewer;
using System.Text;
using NUnit.Framework;
using NSubstitute;
using System.Collections.Immutable;
using System.Collections.Generic;

namespace LogJoint.UI.Presenters.Tests.HighlightingManagerTests
{
	[TestFixture]
	public class HighlightingManagerTests
	{
		IHighlightingManager highlightingManager;
		ISearchResultModel searchResultModel;
		IFiltersList highlightFilters;
		ISelectionManager selectionManager;
		IWordSelection wordSelection;
		bool isRawMessagesMode;
		int viewSize;
		IMessage msg1, msg2;
		IMessage msgWithMultilineText;
		IMessagesSource messagesSource;

		[SetUp]
		public void BeforeEach()
		{
			searchResultModel = Substitute.For<ISearchResultModel>();
			highlightFilters = Substitute.For<IFiltersList>();
			selectionManager = Substitute.For<ISelectionManager>();
			messagesSource = Substitute.For<IMessagesSource>();
			wordSelection = new WordSelection();
			isRawMessagesMode = false;
			viewSize = 3;
			highlightFilters.FilteringEnabled.Returns(true);
			msg1 = new Message(0, 1, null, new MessageTimestamp(), new StringSlice("test message 1"), SeverityFlag.Info);
			msg2 = new Message(0, 1, null, new MessageTimestamp(), new StringSlice("test message 2"), SeverityFlag.Info);
			msgWithMultilineText = new Message(0, 1, null, new MessageTimestamp(), new StringSlice(
@"2019/03/27 06:27:52.143 T#1 I app: model creation finished
import { imock, instance, when, anything, verify, resetCalls, MockPropertyPolicy, deepEqual } from 'ts-mockito';
import * as expect from 'expect';
import { Map, OrderedSet } from 'immutable';
import { ChangeNotification } from '../../../../client/model/utils';
import { PodStore, Pod } from '../../../../client/model/pod/store';
import { MeetingSession } from '../../../../client/model/meeting/impl/meeting/meetingSession';
import { Meeting, MeetingParticipant, LocalMeetingParticipant } from '../../../../client/model/meeting';
import { Conversation, ConversationStore, StreamId } from '../../../../client/model/conversation/store';
import { MeetingStartupOptions, Meeting as MeetingBase, MediaDevices, ParticipantTracks, ScreenTrack } from '../../../../client/model/meeting';
import { Protocol } from '../../../../client/model/meeting/protocol';
import { MeetingImpl } from '../../../../client/model/meeting/impl/meeting/impl/meetingImpl';
import { RtcManager, RtcManagerConfig } from '../../../../client/model/rtcManager';
import { MeetingAnalytics, LeaveSources } from '../../../../client/model/analytics';
import { MeetingParticipants } from '../../../../client/model/meeting/impl/meeting/meetingParticipants';
import { UncaughtExceptionsTrap } from '../../../utils/uncaughtExceptionsTrap';
import { AutoResetEvent, waiter } from '../../../utils/promise-utils';
import { MeetingLocalMedia } from '../../../../client/model/meeting/impl/media/meetingLocalMedia';
import { MeetingRemoteMedia } from '../../../../client/model/meeting/impl/media/meetingRemoteMedia';
import { LolexClock, install } from 'lolex';
import { RtcTokenProvider } from '../../../../client/model/meeting/impl/protocol/impl/rtcTokenProviderImpl';
import { User, UserStore, CurrentUser, UserId } from '../../../../client/model/users/store';
import { fireAndForget } from '../../../utils/promise-utils';
import { RtcSettingsStore, RtcSettings } from '../../../../client/model/settings/store';
import { GeoRouter } from '../../../../client/model/georouting/geoRouter';
import { UserBackend } from '../../../../client/model/users/backend';

describe('MeetingV2', () => {
    let meeting: Meeting;"
), SeverityFlag.Info);
		}

		void CreateHighlightingManager()
		{
			highlightingManager = new HighlightingManager(searchResultModel,
				() => MessageTextGetters.Get(isRawMessagesMode), () => viewSize, highlightFilters, selectionManager, wordSelection);
		}

		IFilter CreateFilter(FilterAction action, bool expectRawMessagesMode, bool enabed,
			params (IMessage, int?, Search.MatchedTextRange)[] matches)
		{
			var filter = Substitute.For<IFilter>();
			filter.Enabled.Returns(true);
			filter.Action.Returns(action);
			var processing = Substitute.For<IFilterBulkProcessing>();
			filter.StartBulkProcessing(MessageTextGetters.Get(expectRawMessagesMode), false).Returns(processing);
			foreach (var m in matches)
			{
				processing.Match(m.Item1, m.Item2).Returns(m.Item3);
			}
			return filter;
		}

		ViewLine CreateViewLine(IMessage message, int textLineIndex, int lineIndex)
		{
			return new ViewLine()
			{
				Message = msg1,
				Text = new StringUtils.MultilineText(new StringSlice("test message 1")),
				TextLineIndex = 0,
				LineIndex = 10
			};
		}

		void VerifyRanges(IEnumerable<(int, int, FilterAction)> actual, params (int, int, FilterAction)[] expected)
		{
			CollectionAssert.AreEqual(expected.OrderBy(x => x), actual.OrderBy(x => x));
		}

		[TestFixture]
		public class HighlightingFiltersTests : HighlightingManagerTests
		{
			[SetUp]
			public new void BeforeEach()
			{
				var f1 = CreateFilter(FilterAction.Include, false, true, (msg1, null, new Search.MatchedTextRange(StringSlice.Empty, 3, 4, true)));
				highlightFilters.Items.Returns(ImmutableList.Create(f1));
			}

			[Test]
			public void HappyPath()
			{
				CreateHighlightingManager();
				VerifyRanges(
					highlightingManager.HighlightingFiltersHandler.GetHighlightingRanges(CreateViewLine(msg1, 0, 10)),
					(3, 4, FilterAction.Include)
				);
			}
		};

		[TestFixture]
		public class SelectionFiltersTests : HighlightingManagerTests
		{
			[SetUp]
			public new void BeforeEach()
			{
			}

			[Test]
			public void EmptySelection()
			{
				selectionManager.Selection.Returns(new SelectionInfo(
					new CursorPosition(msg1, messagesSource, 0, 1),
					new CursorPosition(msg1, messagesSource, 0, 1),
					MessageTextGetters.Get(false)
				));
				CreateHighlightingManager();
				Assert.IsNull(highlightingManager.SelectionHandler);
			}

			[Test]
			public void SubstringSelection()
			{
				selectionManager.Selection.Returns(new SelectionInfo(
					new CursorPosition(msg1, messagesSource, 0, 1),
					new CursorPosition(msg1, messagesSource, 0, 3),
					MessageTextGetters.Get(false)
				));
				CreateHighlightingManager();
				VerifyRanges(highlightingManager.SelectionHandler.GetHighlightingRanges(CreateViewLine(msg1, 0, 10)),
					(6, 8, FilterAction.Include) // two matches "es" but first original one is not highlighted
				);
			}

			[Test]
			public void SelectionWithMultipleMatches()
			{
				selectionManager.Selection.Returns(new SelectionInfo(
					new CursorPosition(msg1, messagesSource, 0, 1),
					new CursorPosition(msg1, messagesSource, 0, 2),
					MessageTextGetters.Get(false)
				));
				CreateHighlightingManager();
				VerifyRanges(highlightingManager.SelectionHandler.GetHighlightingRanges(CreateViewLine(msg1, 0, 10)),
					(11, 12, FilterAction.Include),
					(6, 7, FilterAction.Include)
				);
			}
		};

		[TestFixture]
		public class SelectionResultsTextTests : HighlightingManagerTests
		{
			IFiltersList searchFilters;

			[SetUp]
			public new void BeforeEach()
			{
				CreateHighlightingManager();
				searchFilters = new FiltersList(FilterAction.Exclude, FiltersListPurpose.Search, Substitute.For<IChangeNotification>());
			}

			[Test]
			public void SingleLineMatch()
			{
				searchFilters.Insert(0, new Filter(FilterAction.Include, "test", true, new Search.Options() { Template = "meeting" }, Substitute.For<IFiltersFactory>()));
				var textInfo = highlightingManager.GetSearchResultMessageText(msgWithMultilineText, MessageTextGetters.SummaryTextGetter, searchFilters);
				Assert.AreEqual(StringUtils.NormalizeLinebreakes(
@"import { MeetingSession } from '../../../../client/model/meeting/impl/meeting/meetingSession';
import { Meeting, MeetingParticipant, LocalMeetingParticipant } from '../../../../client/model/meeting';
import { MeetingStartupOptions, Meeting as MeetingBase, MediaDevices, ParticipantTracks, ScreenTrack } from '../../../../client/model/meeting';
import { Protocol } from '../../../../client/model/meeting/protocol';
import { MeetingImpl } from '../../../../client/model/meeting/impl/meeting/impl/meetingImpl';
import { MeetingAnalytics, LeaveSources } from '../../../../client/model/analytics';
import { MeetingParticipants } from '../../../../client/model/meeting/impl/meeting/meetingParticipants';
import { MeetingLocalMedia } from '../../../../client/model/meeting/impl/media/meetingLocalMedia';
import { MeetingRemoteMedia } from '../../../../client/model/meeting/impl/media/meetingRemoteMedia';
import { RtcTokenProvider } from '../../../../client/model/meeting/impl/protocol/impl/rtcTokenProviderImpl';
describe('MeetingV2', () => {
    let meeting: Meeting;"), StringUtils.NormalizeLinebreakes(textInfo.DisplayText.ToString()));
			}

			[Test]
			public void MultilineLineMatch()
			{
				searchFilters.Insert(0, new Filter(FilterAction.Include, "test", true, new Search.Options() {
					Template = @"Impl\'\;\r\n+import",
					Regexp = true
				}, Substitute.For<IFiltersFactory>()));
				var textInfo = highlightingManager.GetSearchResultMessageText(msgWithMultilineText, MessageTextGetters.SummaryTextGetter, searchFilters);
				Assert.AreEqual(StringUtils.NormalizeLinebreakes(
@"import { MeetingImpl } from '../../../../client/model/meeting/impl/meeting/impl/meetingImpl';
import { RtcManager, RtcManagerConfig } from '../../../../client/model/rtcManager';
import { RtcTokenProvider } from '../../../../client/model/meeting/impl/protocol/impl/rtcTokenProviderImpl';
import { User, UserStore, CurrentUser, UserId } from '../../../../client/model/users/store';"), StringUtils.NormalizeLinebreakes(textInfo.DisplayText.ToString()));
			}

		}
	}
}