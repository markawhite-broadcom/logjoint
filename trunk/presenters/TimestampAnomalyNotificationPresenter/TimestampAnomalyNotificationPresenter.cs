using System;
using System.Collections.Generic;

namespace LogJoint.UI.Presenters.TimestampAnomalyNotification
{
	public class Presenter : IPresenter
	{
		readonly IPresentersFacade presentersFacade;
		readonly Preprocessing.ILogSourcesPreprocessingManager preprocessingManager;
		readonly StatusReports.IPresenter statusReports;
		readonly AsyncInvokeHelper updateInvokeHelper;
		readonly HashSet<ILogSource> logSourcesRequiringReordering = new HashSet<ILogSource>();
		readonly HashSet<ILogSource> ignoredLogSources = new HashSet<ILogSource>();
		readonly LazyUpdateFlag updateFlag = new LazyUpdateFlag();
		StatusReports.IReport currentReport = null;

		public Presenter(
			ILogSourcesManager sourcesManager,
			Preprocessing.ILogSourcesPreprocessingManager preprocessingManager,
			IInvokeSynchronization invoker,
			IHeartBeatTimer heartbeat,
			IPresentersFacade presentersFacade,
			StatusReports.IPresenter statusReports
		)
		{
			this.preprocessingManager = preprocessingManager;
			this.presentersFacade = presentersFacade;
			this.statusReports = statusReports;

			this.updateInvokeHelper = new AsyncInvokeHelper(invoker, Update)
			{
				ForceAsyncInvocation = true
			};

			sourcesManager.OnLogSourceStatsChanged += (sender, e) =>
			{
				if ((e.Flags & LogProviderStatsFlag.FirstMessageWithTimeConstraintViolation) != 0)
				{
					var msg = ((ILogSource)sender).Provider.Stats.FirstMessageWithTimeConstraintViolation;
					bool updated;
					if (msg != null)
						updated = logSourcesRequiringReordering.Add((ILogSource)sender);
					else
						updated = logSourcesRequiringReordering.Remove((ILogSource)sender);
					if (updated)
						updateFlag.Invalidate();
				}
			};
			sourcesManager.OnLogSourceRemoved += (sender, e) =>
			{
				updateFlag.Invalidate();
			};
			heartbeat.OnTimer += (sender, e) =>
			{
				if (e.IsNormalUpdate && updateFlag.Validate())
					Update();
			};
		}

		void Update()
		{
			updateFlag.Validate();

			ignoredLogSources.RemoveWhere(x => x.IsDisposed);
			logSourcesRequiringReordering.RemoveWhere(x => x.IsDisposed);

			if (currentReport != null)
			{
				currentReport.Dispose();
				currentReport = null;
			}

			var messageParts = new List<StatusReports.MessagePart>();
			messageParts.Add(
				new StatusReports.MessagePart(
					string.Format("{0} {1} problem with timestamps. {2}",
						logSourcesRequiringReordering.Count,
						logSourcesRequiringReordering.Count > 1 ? "logs have" : "log has",
						Environment.NewLine
					)
				)
			);
			int shownLogSourcesCount = 0;
			foreach (var logSource in logSourcesRequiringReordering)
			{
				if (ignoredLogSources.Contains(logSource))
					continue;
				++shownLogSourcesCount;

				var logName = logSource.GetShortDisplayNameWithAnnotation();
				if (string.IsNullOrEmpty(logName))
					logName = "log";

				Func<bool> checkSourceIsOk = () =>
				{
					if (logSource.IsDisposed)
					{
						updateInvokeHelper.Invoke();
						return false;
					}
					return true;
				};

				messageParts.AddRange(new[]
				{
					new StatusReports.MessageLink(logName, () => 
					{
						if (!checkSourceIsOk())
							return;
						presentersFacade.ShowLogSource(logSource);
					}),
					new StatusReports.MessagePart("  "),
					new StatusReports.MessageLink("reorder log", () => 
					{
						if (!checkSourceIsOk())
							return;
						var cp = preprocessingManager.AppendReorderingStep(
							logSource.Provider.ConnectionParams, logSource.Provider.Factory);
						if (cp != null)
						{
							logSource.Dispose();
							preprocessingManager.Preprocess(
								new MRU.RecentLogEntry(logSource.Provider.Factory, cp, "", null), makeHiddenLog: false);
						}
					}),
					new StatusReports.MessagePart("  "),
					new StatusReports.MessageLink("ignore", () =>
					{
						ignoredLogSources.Add(logSource);
						updateInvokeHelper.Invoke();
					})
				});
			}

			if (shownLogSourcesCount != 0)
			{
				currentReport = statusReports.CreateNewStatusReport();
				currentReport.ShowStatusPopup(
					"Log source problem",
					messageParts,
					autoHide: false
				);
			}
		}
	};
};