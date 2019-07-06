﻿using System;
using System.Linq;
using LogJoint.Preprocessing;
using LogJoint.UI.Presenters.MainForm;
using AppKit;
using Foundation;

namespace LogJoint.UI
{
	public class DragDropHandler: IDragDropHandler
	{
		readonly Preprocessing.IManager preprocessingManager;
		readonly Preprocessing.IStepsFactory preprocessingStepsFactory;
		readonly ILogSourcesManager logSourcesManager;

		public DragDropHandler(
			Preprocessing.IManager preprocessingManager,
			Preprocessing.IStepsFactory preprocessingStepsFactory,
			ILogSourcesManager logSourcesManager)
		{
			this.preprocessingManager = preprocessingManager;
			this.preprocessingStepsFactory = preprocessingStepsFactory;
			this.logSourcesManager = logSourcesManager;
		}

		bool IDragDropHandler.ShouldAcceptDragDrop(object dataObject)
		{
			var info = dataObject as NSDraggingInfo;
			if (info == null)
				return false;
			var types = info.DraggingPasteboard.Types;
			if (types.Contains(NSPasteboard.NSFilenamesType.ToString()))
				return true;
			if (types.Contains(NSPasteboard.NSUrlType.ToString()))
				return true;
			return false;
		}

		void IDragDropHandler.AcceptDragDrop(object dataObject, bool controlKeyHeld)
		{
			var info = dataObject as NSDraggingInfo;
			if (info == null)
				return;
			var pboard = info.DraggingPasteboard;
			var types = pboard.Types;
			if (types.Contains(NSPasteboard.NSFilenamesType.ToString()))
			{
				if (controlKeyHeld)
					DeleteExistingLogs();
				var fnames = GetItemsForType(pboard, NSPasteboard.NSFilenamesType);
				foreach (var file in fnames)
					preprocessingManager.Preprocess(
						Enumerable.Repeat(preprocessingStepsFactory.CreateFormatDetectionStep(new PreprocessingStepParams(file)), 1),
						file
					);
			}
			else if (types.Contains(NSPasteboard.NSUrlType.ToString()))
			{
				if (controlKeyHeld)
					DeleteExistingLogs();
				var urls = GetItemsForType(pboard, NSPasteboard.NSUrlType);
				preprocessingManager.Preprocess(
					urls.Select(url => preprocessingStepsFactory.CreateURLTypeDetectionStep(new PreprocessingStepParams(url))),
					urls.Length == 1 ? urls[0] : "Urls drag&drop"
				);
			}
		}

		static string[] GetItemsForType(NSPasteboard pboard, NSString type)
		{
			var items = NSArray.FromArray<NSString>(((NSArray)pboard.GetPropertyListForType(type.ToString())));
			return items.Select(i => i.ToString()).ToArray();
		}

		void DeleteExistingLogs()
		{
			logSourcesManager.DeleteAllLogs ();
			preprocessingManager.DeleteAllPreprocessings ();
		}
	}
}

