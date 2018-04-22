﻿using System.Linq;
using LogJoint.Postprocessing;
using System;
using UDF = LogJoint.RegularGrammar.UserDefinedFormatFactory;

namespace LogJoint.Chromium
{
	public interface IPostprocessorsRegistry
	{
		LogSourceMetadata ChromeDebugLog { get; }
		LogSourceMetadata WebRtcInternalsDump { get; }
	};

	public class PostprocessorsInitializer : IPostprocessorsRegistry
	{
		private readonly UDF chromeDebugLogFormat, webRtcInternalsDumpFormat, chromeDriverLogFormat;
		private readonly LogSourceMetadata chromeDebugLogMeta, webRtcInternalsDumpMeta, chromeDriverLogMeta;


		public PostprocessorsInitializer(
			IPostprocessorsManager postprocessorsManager,
			IUserDefinedFormatsManager userDefinedFormatsManager,
			StateInspector.IPostprocessorsFactory stateInspectorPostprocessorsFactory,
			TimeSeries.IPostprocessorsFactory timeSeriesPostprocessorsFactory,
			Correlator.IPostprocessorsFactory correlatorPostprocessorsFactory,
			Timeline.IPostprocessorsFactory timelinePostprocessorsFactory
		)
		{
			Func<string, UDF> findFormat = formatName =>
			{
				var ret = userDefinedFormatsManager.Items.FirstOrDefault(
					f => f.CompanyName == "Google" && f.FormatName == formatName) as UDF;
				if (ret == null)
					throw new Exception(string.Format("Log format {0} is not registered in LogJoint", formatName));
				return ret;
			};

			this.chromeDebugLogFormat = findFormat("Chrome debug log");
			this.webRtcInternalsDumpFormat = findFormat("Chrome WebRTC internals dump as log");
			this.chromeDriverLogFormat = findFormat("chromedriver");

			var correlatorPostprocessorType = correlatorPostprocessorsFactory.CreatePostprocessor(this);
			postprocessorsManager.RegisterCrossLogSourcePostprocessor(correlatorPostprocessorType);

			this.chromeDebugLogMeta = new LogSourceMetadata(
				chromeDebugLogFormat,
				stateInspectorPostprocessorsFactory.CreateChromeDebugPostprocessor(),
				timeSeriesPostprocessorsFactory.CreateChromeDebugPostprocessor(),
				correlatorPostprocessorType
			);
			postprocessorsManager.RegisterLogType(this.chromeDebugLogMeta);

			this.webRtcInternalsDumpMeta = new LogSourceMetadata(
				webRtcInternalsDumpFormat,
				stateInspectorPostprocessorsFactory.CreateWebRtcInternalsDumpPostprocessor(),
				timeSeriesPostprocessorsFactory.CreateWebRtcInternalsDumpPostprocessor(),
				correlatorPostprocessorType
			);
			postprocessorsManager.RegisterLogType(this.webRtcInternalsDumpMeta);

			this.chromeDebugLogMeta = new LogSourceMetadata(
				chromeDriverLogFormat,
				timelinePostprocessorsFactory.CreateChromeDriverPostprocessor()
			);
			postprocessorsManager.RegisterLogType(this.chromeDebugLogMeta);
		}

		LogSourceMetadata IPostprocessorsRegistry.ChromeDebugLog
		{
			get { return chromeDebugLogMeta; }
		}

		LogSourceMetadata IPostprocessorsRegistry.WebRtcInternalsDump
		{
			get { return webRtcInternalsDumpMeta; }
		}
	};
}
