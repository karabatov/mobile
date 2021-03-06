﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using MonoTouch.CoreFoundation;
using MonoTouch.UIKit;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.Utils;
using XPlatUtils;
using Toggl.Ross.Data;
using Toggl.Ross.Theme;

namespace Toggl.Ross.ViewControllers
{
    public class TimerNavigationController
    {
        private const string DefaultDurationText = " 00:00:00 ";
        private readonly bool showRunning;
        private UIViewController parentController;
        private UIButton durationButton;
        private UIButton actionButton;
        private UIBarButtonItem navigationButton;
        private TimeEntryModel currentTimeEntry;
        private ActiveTimeEntryManager timeEntryManager;
        private PropertyChangeTracker propertyTracker;
        private bool isStarted;
        private int rebindCounter;
        private bool isActing;

        public TimerNavigationController (TimeEntryModel model = null)
        {
            showRunning = model == null;
            currentTimeEntry = model;
        }

        public void Attach (UIViewController parentController)
        {
            this.parentController = parentController;

            // Lazyily create views
            if (durationButton == null) {
                durationButton = new UIButton ().Apply (Style.NavTimer.DurationButton);
                durationButton.SetTitle (DefaultDurationText, UIControlState.Normal); // Dummy content to use for sizing of the label
                durationButton.SizeToFit ();
                durationButton.TouchUpInside += OnDurationButtonTouchUpInside;
            }

            if (navigationButton == null) {
                actionButton = new UIButton ().Apply (Style.NavTimer.StartButton);
                actionButton.SizeToFit ();
                actionButton.TouchUpInside += OnActionButtonTouchUpInside;
                navigationButton = new UIBarButtonItem (actionButton);
            }

            // Attach views
            var navigationItem = parentController.NavigationItem;
            navigationItem.TitleView = durationButton;
            navigationItem.RightBarButtonItem = navigationButton;
        }

        private void OnDurationButtonTouchUpInside (object sender, EventArgs e)
        {
            var controller = new DurationChangeViewController (currentTimeEntry);
            parentController.NavigationController.PushViewController (controller, true);
        }

        private async void OnActionButtonTouchUpInside (object sender, EventArgs e)
        {
            if (isActing)
                return;
            isActing = true;

            try {
                if (currentTimeEntry != null && currentTimeEntry.State == TimeEntryState.Running) {
                    await currentTimeEntry.StopAsync ();
                } else if (timeEntryManager != null) {
                    currentTimeEntry = (TimeEntryModel)timeEntryManager.Draft;
                    if (currentTimeEntry == null)
                        return;

                    await currentTimeEntry.StartAsync ();

                    var controllers = new List<UIViewController> (parentController.NavigationController.ViewControllers);
                    controllers.Add (new EditTimeEntryViewController (currentTimeEntry));
                    if (ServiceContainer.Resolve<SettingsStore> ().ChooseProjectForNew) {
                        controllers.Add (new ProjectSelectionViewController (currentTimeEntry));
                    }
                    parentController.NavigationController.SetViewControllers (controllers.ToArray (), true);
                }
            } finally {
                isActing = false;
            }
        }

        private void Rebind ()
        {
            if (!isStarted)
                return;

            ResetTrackedObservables ();

            rebindCounter++;

            if (currentTimeEntry == null) {
                durationButton.SetTitle (DefaultDurationText, UIControlState.Normal);
                actionButton.Apply (Style.NavTimer.StartButton);
                actionButton.Hidden = false;
            } else {
                var duration = currentTimeEntry.GetDuration ();

                durationButton.SetTitle (duration.ToString (@"hh\:mm\:ss"), UIControlState.Normal);
                actionButton.Apply (Style.NavTimer.StopButton);
                actionButton.Hidden = currentTimeEntry.State != TimeEntryState.Running;

                var counter = rebindCounter;
                DispatchQueue.MainQueue.DispatchAfter (
                    TimeSpan.FromMilliseconds (1000 - duration.Milliseconds),
                    delegate {
                        if (counter == rebindCounter) {
                            Rebind ();
                        }
                    });
            }
        }

        private void ResetTrackedObservables ()
        {
            if (propertyTracker == null)
                return;

            propertyTracker.MarkAllStale ();

            if (currentTimeEntry != null) {
                propertyTracker.Add (currentTimeEntry, HandleTimeEntryPropertyChanged);
            }

            propertyTracker.ClearStale ();
        }

        private void HandleTimeEntryPropertyChanged (string prop)
        {
            if (prop == TimeEntryModel.PropertyState
                || prop == TimeEntryModel.PropertyStartTime
                || prop == TimeEntryModel.PropertyStopTime)
                Rebind ();
        }

        private void OnTimeEntryManagerPropertyChanged (object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == ActiveTimeEntryManager.PropertyRunning) {
                ResetModelToRunning ();
                Rebind ();
            }
        }

        private void ResetModelToRunning ()
        {
            if (timeEntryManager == null)
                return;

            if (currentTimeEntry == null) {
                currentTimeEntry = (TimeEntryModel)timeEntryManager.Running;
            } else if (timeEntryManager.Running != null) {
                currentTimeEntry.Data = timeEntryManager.Running;
            } else {
                currentTimeEntry = null;
            }
        }

        public void Start ()
        {
            propertyTracker = new PropertyChangeTracker ();

            // Start listening to timer changes
            if (showRunning) {
                timeEntryManager = ServiceContainer.Resolve<ActiveTimeEntryManager> ();
                timeEntryManager.PropertyChanged += OnTimeEntryManagerPropertyChanged;
                ResetModelToRunning ();
            }

            isStarted = true;
            Rebind ();
        }

        public void Stop ()
        {
            // Stop listening to timer changes
            isStarted = false;

            if (propertyTracker != null) {
                propertyTracker.Dispose ();
                propertyTracker = null;
            }

            if (timeEntryManager != null) {
                timeEntryManager.PropertyChanged -= OnTimeEntryManagerPropertyChanged;
                timeEntryManager = null;
            }

            if (showRunning) {
                currentTimeEntry = null;
            }
        }
    }
}
