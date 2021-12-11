using System;
using System.Collections.Generic;
using System.Text;
using LogJoint.RegularExpressions;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace LogJoint.RegularGrammar
{
	public class FormatInfo : StreamBasedFormatInfo
	{
		[Flags]
		public enum FormatFlags
		{
			None = 0,
			AllowPlainTextSearchOptimization = 1
		};

		public readonly LoadedRegex HeadRe;
		public readonly LoadedRegex BodyRe;
		public readonly string Encoding;
		public readonly FieldsProcessor.IInitializationParams FieldsProcessorParams;
		public readonly DejitteringParams? DejitteringParams;
		public readonly TextStreamPositioningParams TextStreamPositioningParams;
		public readonly static string EmptyBodyReEquivalientTemplate = "^(?<body>.*)$";
		public readonly FormatFlags Flags;
		public readonly RotationParams RotationParams;
		public readonly BoundFinder BeginFinder;
		public readonly BoundFinder EndFinder;

		public FormatInfo(
			LoadedRegex headRe, LoadedRegex bodyRe, 
			string encoding, FieldsProcessor.IInitializationParams fieldsParams, 
			MessagesReaderExtensions.XmlInitializationParams extensionsInitData,
			DejitteringParams? dejitteringParams,
			TextStreamPositioningParams textStreamPositioningParams,
			FormatFlags flags,
			RotationParams rotationParams,
			BoundFinder beginFinder,
			BoundFinder endFinder
		) :
			base(extensionsInitData)
		{
			this.HeadRe = headRe;
			this.BodyRe = bodyRe;
			this.Encoding = encoding;
			this.FieldsProcessorParams = fieldsParams;
			this.DejitteringParams = dejitteringParams;
			this.TextStreamPositioningParams = textStreamPositioningParams;
			this.Flags = flags;
			this.RotationParams = rotationParams;
			this.BeginFinder = beginFinder;
			this.EndFinder = endFinder;
		}

		public IEnumerable<string> InputFieldNames =>
				HeadRe.Regex.GetGroupNames().Skip(1).Concat(
					BodyRe.Regex != null ? BodyRe.Regex.GetGroupNames().Skip(1) :
					HeadRe.Regex.GetGroupNames().Contains("body") ? Enumerable.Empty<string>() :
					Enumerable.Repeat("body", 1)
				);
	};

	public class MessagesReader : MediaBasedPositionedMessagesReader
	{
		readonly ILogSourceThreadsInternal threads;
		readonly FormatInfo fmtInfo;
		readonly FieldsProcessor.IFactory fieldsProcessorFactory;
		readonly ITraceSourceFactory traceSourceFactory;
		readonly IRegexFactory regexFactory;
		readonly Lazy<ValueTask<bool>> isBodySingleFieldExpression;
		readonly LJTraceSource trace;

		public MessagesReader(
			MediaBasedReaderParams readerParams,
			FormatInfo fmt,
			FieldsProcessor.IFactory fieldsProcessorFactory,
			IRegexFactory regexFactory,
			ITraceSourceFactory traceSourceFactory
		) :
			base(readerParams.Media, fmt.BeginFinder, fmt.EndFinder, fmt.ExtensionsInitData, fmt.TextStreamPositioningParams, readerParams.Flags, readerParams.SettingsAccessor)
		{
			if (readerParams.Threads == null)
				throw new ArgumentNullException(nameof (readerParams) + ".Threads");
			this.threads = readerParams.Threads;
			this.traceSourceFactory = traceSourceFactory;
			this.regexFactory = regexFactory;
			this.fmtInfo = fmt;
			this.fieldsProcessorFactory = fieldsProcessorFactory;
			this.trace = traceSourceFactory.CreateTraceSource("LogSource", string.Format("{0}.r{1:x4}", readerParams.ParentLoggingPrefix, Hashing.GetShortHashCode(this.GetHashCode())));

			base.Extensions.AttachExtensions();

			this.isBodySingleFieldExpression = new Lazy<ValueTask<bool>>(async () =>
			{
				return (await CreateNewFieldsProcessor()).IsBodySingleFieldExpression();
			});
		}

		ValueTask<FieldsProcessor.IFieldsProcessor> CreateNewFieldsProcessor()
		{
			return fieldsProcessorFactory.CreateProcessor(
				fmtInfo.FieldsProcessorParams,
				fmtInfo.InputFieldNames,
				Extensions.Items.Select(ext => new FieldsProcessor.ExtensionInfo(ext.Name, ext.AssemblyName, ext.ClassName, ext.Instance)),
				trace
			);
		}

		MessagesBuilderCallback CreateMessageBuilderCallback()
		{
			IThread fakeThread = null;
			//fakeThread = threads.GetThread("");
			return new MessagesBuilderCallback(threads, fakeThread);
		}

		static IMessage MakeMessageInternal(
			TextMessageCapture capture,
			IRegex headRe,
			IRegex bodyRe,
			ref IMatch bodyMatch,
			FieldsProcessor.IFieldsProcessor fieldsProcessor,
			FieldsProcessor.MakeMessageFlags makeMessageFlags,
			DateTime sourceTime,
			ITimeOffsets timeOffsets,
			MessagesBuilderCallback threadLocalCallbackImpl
		)
		{
			if (bodyRe != null)
				if (!bodyRe.Match(capture.BodyBuffer, capture.BodyIndex, capture.BodyLength, ref bodyMatch))
					return null;

			int idx = 0;
			Group[] groups;

			fieldsProcessor.Reset();
			fieldsProcessor.SetSourceTime(sourceTime);
			fieldsProcessor.SetPosition(capture.BeginPosition);
			fieldsProcessor.SetTimeOffsets(timeOffsets);

			groups = capture.HeaderMatch.Groups;
			for (int i = 1; i < groups.Length; ++i)
			{
				var g = groups[i];
				fieldsProcessor.SetInputField(idx++, new StringSlice(capture.HeaderBuffer, g.Index, g.Length));
			}

			if (bodyRe != null)
			{
				groups = bodyMatch.Groups;
				for (int i = 1; i < groups.Length; ++i)
				{
					var g = groups[i];
					fieldsProcessor.SetInputField(idx++, new StringSlice(capture.BodyBuffer, g.Index, g.Length));
				}
			}
			else
			{
				fieldsProcessor.SetInputField(idx++, new StringSlice(capture.BodyBuffer, capture.BodyIndex, capture.BodyLength));
			}

			threadLocalCallbackImpl.SetCurrentPosition(capture.BeginPosition, capture.EndPosition);
			threadLocalCallbackImpl.SetRawText(StringSlice.Concat(capture.MessageHeaderSlice, capture.MessageBodySlice).Trim());

			var ret = fieldsProcessor.MakeMessage(threadLocalCallbackImpl, makeMessageFlags);

			return ret;
		}

		class SingleThreadedStrategyImpl : StreamParsingStrategies.SingleThreadedStrategy
		{
			readonly MessagesReader reader;
			readonly MessagesBuilderCallback callback;
			readonly IRegex headerRegex, bodyRegex;
			FieldsProcessor.IFieldsProcessor fieldsProcessor;
			IMatch bodyMatch;

			FieldsProcessor.MakeMessageFlags currentParserFlags;

			public SingleThreadedStrategyImpl(MessagesReader reader) : base(
				reader.LogMedia,
				reader.StreamEncoding,
				CloneRegex(reader.fmtInfo.HeadRe, reader.IsQuickFormatDetectionMode ? ReOptions.Timeboxed : ReOptions.None).Regex,
				reader.fmtInfo.HeadRe.GetHeaderReSplitterFlags(),
				reader.fmtInfo.TextStreamPositioningParams
			)
			{
				this.reader = reader;
				this.callback = reader.CreateMessageBuilderCallback();
				this.headerRegex = headerRe;
				this.bodyRegex = CloneRegex(reader.fmtInfo.BodyRe).Regex;
			}
			public override async Task ParserCreated(CreateParserParams p)
			{
				this.fieldsProcessor = await reader.CreateNewFieldsProcessor();
				await base.ParserCreated(p);
				currentParserFlags = ParserFlagsToMakeMessageFlags(p.Flags);
			}
			protected override IMessage MakeMessage(TextMessageCapture capture)
			{
				return MakeMessageInternal(capture, headerRegex, bodyRegex, ref bodyMatch, fieldsProcessor, currentParserFlags, 
					media.LastModified, reader.TimeOffsets, callback);
			}
		};

		protected override StreamParsingStrategies.BaseStrategy CreateSingleThreadedStrategy()
		{
			return new SingleThreadedStrategyImpl(this);
		}

#if !SILVERLIGHT

		class ProcessingThreadLocalData
		{
			public LoadedRegex headRe;
			public LoadedRegex bodyRe;
			public IMatch bodyMatch;
			public FieldsProcessor.IFieldsProcessor fieldsProcessor;
			public MessagesBuilderCallback callback;
		}

		class MultiThreadedStrategyImpl : StreamParsingStrategies.MultiThreadedStrategy<ProcessingThreadLocalData>
		{
			MessagesReader reader;
			FieldsProcessor.MakeMessageFlags flags;

			public MultiThreadedStrategyImpl(MessagesReader reader) :
				base(reader.LogMedia, reader.StreamEncoding, reader.fmtInfo.HeadRe.Regex,
			         reader.fmtInfo.HeadRe.GetHeaderReSplitterFlags(), reader.fmtInfo.TextStreamPositioningParams, reader.trace.Prefix, reader.traceSourceFactory)
			{
				this.reader = reader;
			}
			public override async Task ParserCreated(CreateParserParams p)
			{
				await base.ParserCreated(p);
				flags = ParserFlagsToMakeMessageFlags(p.Flags);
			}
			public override IMessage MakeMessage(TextMessageCapture capture, ProcessingThreadLocalData threadLocal)
			{
				return MakeMessageInternal(capture, threadLocal.headRe.Regex, threadLocal.bodyRe.Regex, ref threadLocal.bodyMatch, threadLocal.fieldsProcessor, flags, media.LastModified, 
					reader.TimeOffsets, threadLocal.callback);
			}
			public override ProcessingThreadLocalData InitializeThreadLocalState()
			{
				ProcessingThreadLocalData ret = new ProcessingThreadLocalData();
				ret.headRe = CloneRegex(reader.fmtInfo.HeadRe, reader.IsQuickFormatDetectionMode ? ReOptions.Timeboxed : ReOptions.None);
				ret.bodyRe = CloneRegex(reader.fmtInfo.BodyRe);
				ret.fieldsProcessor = reader.CreateNewFieldsProcessor().Result;
				ret.callback = reader.CreateMessageBuilderCallback();
				ret.bodyMatch = null;
				return ret;
			}
		};

		protected override StreamParsingStrategies.BaseStrategy CreateMultiThreadedStrategy()
		{
			return new MultiThreadedStrategyImpl(this);
		}
#else

		protected override StreamParsingStrategies.BaseStrategy CreateMultiThreadedStrategy()
		{
			return null;
		}

#endif

		protected override Encoding DetectStreamEncoding(Stream stream)
		{
			Encoding ret = EncodingUtils.GetEncodingFromConfigXMLName(fmtInfo.Encoding);
			if (ret == null)
				ret = EncodingUtils.DetectEncodingFromBOM(stream, EncodingUtils.GetDefaultEncoding());
			return ret;
		}

		protected override DejitteringParams? GetDejitteringParams()
		{
			return fmtInfo.DejitteringParams;
		}

		public override async Task<ISearchingParser> CreateSearchingParser(CreateSearchingParserParams p)
		{
			var allowPlainTextSearchOptimization =
				   (fmtInfo.Flags & FormatInfo.FormatFlags.AllowPlainTextSearchOptimization) != 0 
				|| p.SearchParams.SearchInRawText
				|| await isBodySingleFieldExpression.Value;
			return new SearchingParser(
				this, 
				p,
				((ITextStreamPositioningParamsProvider)this).TextStreamPositioningParams,
				GetDejitteringParams(),
				VolatileStream,
				StreamEncoding,
				allowPlainTextSearchOptimization,
				fmtInfo.HeadRe,
				threads,
				traceSourceFactory,
				regexFactory
			);
		}
	};

	public class UserDefinedFormatFactory : 
		UserDefinedFactoryBase,
		IFileBasedLogProviderFactory,
		IPrecompilingLogProviderFactory,
		IMediaBasedReaderFactory
	{
		List<string> patterns = new List<string>();
		Lazy<FormatInfo> fmtInfo;
		readonly string uiKey;
		readonly ITempFilesManager tempFilesManager;
		readonly FieldsProcessor.IFactory fieldsProcessorFactory;
		readonly IRegexFactory regexFactory;
		readonly ITraceSourceFactory traceSourceFactory;
		readonly ISynchronizationContext modelSynchronizationContext;
		readonly Settings.IGlobalSettingsAccessor globalSettings;

		public static string ConfigNodeName => "regular-grammar";

		public UserDefinedFormatFactory(UserDefinedFactoryParams createParams, ITempFilesManager tempFilesManager,
			ITraceSourceFactory traceSourceFactory, ISynchronizationContext modelSynchronizationContext,
			Settings.IGlobalSettingsAccessor globalSettings, RegularExpressions.IRegexFactory regexFactory)
			: base(createParams, regexFactory)
		{
			var formatSpecificNode = createParams.FormatSpecificNode;
			ReadPatterns(formatSpecificNode, patterns);
			var boundsNodes = formatSpecificNode.Elements("bounds").Take(1);
			var beginFinder = BoundFinder.CreateBoundFinder(boundsNodes.Select(n => n.Element("begin")).FirstOrDefault());
			var endFinder = BoundFinder.CreateBoundFinder(boundsNodes.Select(n => n.Element("end")).FirstOrDefault());
			this.tempFilesManager = tempFilesManager;
			fieldsProcessorFactory = createParams.FieldsProcessorFactory;
			this.regexFactory = regexFactory;
			this.traceSourceFactory = traceSourceFactory;
			this.modelSynchronizationContext = modelSynchronizationContext;
			this.globalSettings = globalSettings;
			fmtInfo = new Lazy<FormatInfo>(() =>
			{
				FieldsProcessor.IInitializationParams fieldsInitParams = fieldsProcessorFactory.CreateInitializationParams(
					formatSpecificNode.Element("fields-config"), performChecks: true);
				MessagesReaderExtensions.XmlInitializationParams extensionsInitData = new MessagesReaderExtensions.XmlInitializationParams(
					formatSpecificNode.Element("extensions"));
				DejitteringParams? dejitteringParams = DejitteringParams.FromConfigNode(
					formatSpecificNode.Element("dejitter"));
				TextStreamPositioningParams textStreamPositioningParams = TextStreamPositioningParams.FromConfigNode(
					formatSpecificNode);
				RotationParams rotationParams = RotationParams.FromConfigNode(
					formatSpecificNode.Element("rotation"));
				FormatInfo.FormatFlags flags = FormatInfo.FormatFlags.None;
				if (formatSpecificNode.Element("plain-text-search-optimization").AttributeValue("allowed") == "yes")
					flags |= FormatInfo.FormatFlags.AllowPlainTextSearchOptimization;
				return new FormatInfo(
					ReadRe(formatSpecificNode, "head-re", ReOptions.Multiline),
					ReadRe(formatSpecificNode, "body-re", ReOptions.Singleline),
					ReadParameter(formatSpecificNode, "encoding"),
					fieldsInitParams,
					extensionsInitData,
					dejitteringParams,
					textStreamPositioningParams,
					flags,
					rotationParams,
					beginFinder,
					endFinder
				);
			});
			uiKey = ReadParameter(formatSpecificNode, "ui-key");
		}

		public IPositionedMessagesReader CreateMessagesReader(MediaBasedReaderParams readerParams)
		{
			return new MessagesReader(readerParams, fmtInfo.Value, fieldsProcessorFactory, regexFactory, traceSourceFactory);
		}
		
		#region ILogReaderFactory Members

		public override string UITypeKey { get { return string.IsNullOrEmpty(uiKey) ? StdProviderFactoryUIs.FileBasedProviderUIKey : uiKey; } }

		public override string GetUserFriendlyConnectionName(IConnectionParams connectParams)
		{
			return ConnectionParamsUtils.GetFileOrFolderBasedUserFriendlyConnectionName(connectParams);
		}

		public override IConnectionParams GetConnectionParamsToBeStoredInMRUList(IConnectionParams originalConnectionParams)
		{
			return ConnectionParamsUtils.RemoveNonPersistentParams(originalConnectionParams.Clone(true), tempFilesManager);
		}

		public override ILogProvider CreateFromConnectionParams(ILogProviderHost host, IConnectionParams connectParams)
		{
			return new StreamLogProvider(host, this, connectParams, 
				@params => new MessagesReader(@params, fmtInfo.Value, fieldsProcessorFactory, regexFactory, traceSourceFactory),
				tempFilesManager, traceSourceFactory, modelSynchronizationContext, globalSettings);
		}

		public override LogProviderFactoryFlag Flags
		{
			get
			{
				var ret = LogProviderFactoryFlag.SupportsDejitter | LogProviderFactoryFlag.SupportsReordering;
				if (fmtInfo.Value.DejitteringParams.HasValue)
					ret |= LogProviderFactoryFlag.DejitterEnabled;
				if (fmtInfo.Value.RotationParams.IsSupported)
					ret |= LogProviderFactoryFlag.SupportsRotation;
				return ret;
			}
		}

		#endregion

		IEnumerable<string> IFileBasedLogProviderFactory.SupportedPatterns
		{
			get
			{
				return patterns;
			}
		}

		IConnectionParams IFileBasedLogProviderFactory.CreateParams(string fileName)
		{
			return ConnectionParamsUtils.CreateFileBasedConnectionParamsFromFileName(fileName);
		}

		IConnectionParams IFileBasedLogProviderFactory.CreateRotatedLogParams(string folder, IEnumerable<string> patterns)
		{
			return ConnectionParamsUtils.CreateRotatedLogConnectionParamsFromFolderPath(folder, this, patterns);
		}

		byte[] IPrecompilingLogProviderFactory.Precompile(LJTraceSource trace)
		{
			return fieldsProcessorFactory.CreatePrecompiledAssembly(
				fmtInfo.Value.FieldsProcessorParams,
				fmtInfo.Value.InputFieldNames,
				fmtInfo.Value.ExtensionsInitData.Items.Select(
					i => new FieldsProcessor.ExtensionInfo(
						i.name, i.assemblyName, i.className,
						() => throw new InvalidOperationException(
							$"Attempted to instantiate format extension {i.name} while precompiling")
					)
				),
				trace
			);
		}
	};
}
