﻿using LogJoint.Workspaces;
using System;
using System.Collections.Generic;

namespace LogJoint.Preprocessing
{
	class OpenWorkspaceStep : IPreprocessingStep
	{
		readonly IWorkspacesManager workspacesManager;
		readonly PreprocessingStepParams source;
		readonly IInvokeSynchronization invoke;

		public OpenWorkspaceStep(PreprocessingStepParams p, IWorkspacesManager workspacesManager, IInvokeSynchronization invoke)
		{
			this.workspacesManager = workspacesManager;
			this.source = p;
			this.invoke = invoke;
		}

		IEnumerable<IPreprocessingStep> IPreprocessingStep.Execute(IPreprocessingStepCallback callback)
		{
			callback.BecomeLongRunning();
			callback.SetStepDescription("Opening workspace " + source.FullPath);

			foreach (var entry in invoke.Invoke(() => workspacesManager.LoadWorkspace(source.Uri, callback.Cancellation), callback.Cancellation).Result.Result)
				callback.YieldChildPreprocessing(entry.Log, entry.IsHiddenLog);

			yield break;
		}

		PreprocessingStepParams IPreprocessingStep.ExecuteLoadedStep(IPreprocessingStepCallback callback, string param)
		{
			throw new NotImplementedException();
		}
	}
}
