using System;
using System.Collections.Generic;
using System.Drawing;
using GoogleAnalytics.iOS;
using MonoTouch.CoreAnimation;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.Views;
using XPlatUtils;
using Toggl.Ross.DataSources;
using Toggl.Ross.Theme;
using Toggl.Ross.Views;

namespace Toggl.Ross.ViewControllers
{
    public class ProjectSelectionViewController : UITableViewController
    {
        private const float CellSpacing = 4f;
        private readonly TimeEntryModel model;
        private Source source;
        private UISearchBar searchBar;

        public ProjectSelectionViewController (TimeEntryModel model) : base (UITableViewStyle.Plain)
        {
            this.model = model;

            Title = "ProjectTitle".Tr ();
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            View.Apply (Style.Screen);
            EdgesForExtendedLayout = UIRectEdge.None;
            source = new Source (this);
            source.Attach ();

            searchBar = new UISearchBar ();
            var newFrame = new RectangleF (new PointF (), searchBar.SystemLayoutSizeFittingSize (UIView.UILayoutFittingCompressedSize));
            searchBar.Frame = newFrame;
            searchBar.Placeholder = "ProjectSearch".Tr ();
            TableView.TableHeaderView = searchBar;
            searchBar.CancelButtonClicked += HandleSearchCancelButtonClicked;
            searchBar.TextChanged += HandleSearchTextChanged;
            searchBar.OnEditingStarted += HandleSearchEditingStarted;
            searchBar.SearchButtonClicked += HandleSearchButtonClicked;
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);

            var tracker = ServiceContainer.Resolve<IGAITracker> ();
            tracker.Set (GAIConstants.ScreenName, "Project Selection View");
            tracker.Send (GAIDictionaryBuilder.CreateAppView ().Build ());
        }

        public Action ProjectSelected { get; set; }

        private async void Finish (TaskModel task = null, ProjectModel project = null, WorkspaceModel workspace = null)
        {
            project = task != null ? task.Project : project;
            if (project != null) {
                await project.LoadAsync ();
                workspace = project.Workspace;
            }

            if (project != null || task != null || workspace != null) {
                model.Workspace = workspace;
                model.Project = project;
                model.Task = task;
                await model.SaveAsync ();
            }

            var cb = ProjectSelected;
            if (cb != null) {
                cb ();
            } else {
                // Pop to previous view controller
                var vc = NavigationController.ViewControllers;
                var i = Array.IndexOf (vc, this) - 1;
                if (i >= 0) {
                    NavigationController.PopToViewController (vc [i], true);
                }
            }
        }

        private void HandleSearchCancelButtonClicked (object sender, EventArgs e)
        {
            source.Filter = null;
            searchBar.Text = String.Empty;
            searchBar.ResignFirstResponder ();
            searchBar.SetShowsCancelButton (false, true);
        }

        private void HandleSearchTextChanged (object sender, UISearchBarTextChangedEventArgs e)
        {
            source.Filter = (object obj1) => {
                string itemName = String.Empty;
                if (obj1 is ProjectAndTaskView.Project) {
                    var proj = (ProjectAndTaskView.Project)obj1;
                    if (proj.IsNoProject || proj.IsNewProject) {
                        return false;
                    }
                    if (proj.Data != null) {
                        itemName = proj.Data.Name;
                    }
                }
                if (obj1 is TaskData) {
                    var task = (TaskData)obj1;
                    itemName = task.Name;
                }
                if (!String.IsNullOrEmpty (itemName) && !itemName.ToLower ().Contains (e.SearchText.ToLower ())) {
                    return false;
                } else {
                    return true;
                }
            };
        }

        private void HandleSearchEditingStarted (object sender, EventArgs e)
        {
            if (!searchBar.ShowsCancelButton) {
                searchBar.SetShowsCancelButton (true, true);
            }
        }

        private void HandleSearchButtonClicked (object sender, EventArgs e)
        {
            if (searchBar.ShowsCancelButton) {
                searchBar.SetShowsCancelButton (false, true);
            }
            searchBar.ResignFirstResponder ();
        }

        class Source : PlainDataViewSource<object>
        {
            private readonly static NSString WorkspaceHeaderId = new NSString ("SectionHeaderId");
            private readonly static NSString ProjectCellId = new NSString ("ProjectCellId");
            private readonly static NSString TaskCellId = new NSString ("TaskCellId");
            private readonly ProjectSelectionViewController controller;
            private readonly HashSet<Guid> expandedProjects = new HashSet<Guid> ();
            private Func<object, bool> filter;

            public Source (ProjectSelectionViewController controller)
                : base (controller.TableView, new ProjectAndTaskView ())
            {
                this.controller = controller;
            }

            public override void Attach ()
            {
                base.Attach ();

                controller.TableView.RegisterClassForCellReuse (typeof(WorkspaceHeaderCell), WorkspaceHeaderId);
                controller.TableView.RegisterClassForCellReuse (typeof(ProjectCell), ProjectCellId);
                controller.TableView.RegisterClassForCellReuse (typeof(TaskCell), TaskCellId);
                controller.TableView.SeparatorStyle = UITableViewCellSeparatorStyle.None;
            }

            public Func<object, bool> Filter { 
                get { 
                    return filter;
                } 
                set {
                    filter = value;
                    Update ();
                }
            }

            private void ToggleTasksExpanded (Guid projectId)
            {
                SetTasksExpanded (projectId, !expandedProjects.Contains (projectId));
            }

            private void SetTasksExpanded (Guid projectId, bool expand)
            {
                if (expand && expandedProjects.Add (projectId)) {
                    Update ();
                } else if (!expand && expandedProjects.Remove (projectId)) {
                    Update ();
                }
            }

            public override float EstimatedHeight (UITableView tableView, NSIndexPath indexPath)
            {
                return 60f;
            }

            public override float GetHeightForRow (UITableView tableView, NSIndexPath indexPath)
            {
                var row = GetRow (indexPath);
                if (row is ProjectAndTaskView.Workspace)
                    return 42f;
                if (row is TaskModel)
                    return 49f;
                return EstimatedHeight (tableView, indexPath);
            }

            public override float EstimatedHeightForHeader (UITableView tableView, int section)
            {
                return -1f;
            }

            public override float GetHeightForHeader (UITableView tableView, int section)
            {
                return EstimatedHeightForHeader (tableView, section);
            }

            public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
            {
                var row = GetRow (indexPath);

                var project = row as ProjectAndTaskView.Project;
                if (project != null) {
                    var cell = (ProjectCell)tableView.DequeueReusableCell (ProjectCellId, indexPath);
                    cell.Bind (project);
                    if (project.Data != null && project.Data.Id != Guid.Empty) {
                        var projectId = project.Data.Id;
                        cell.ToggleTasks = () => ToggleTasksExpanded (projectId);
                    } else {
                        cell.ToggleTasks = null;
                    }
                    return cell;
                }

                var taskData = row as TaskData;
                if (taskData != null) {
                    var cell = (TaskCell)tableView.DequeueReusableCell (TaskCellId, indexPath);
                    cell.Bind ((TaskModel)taskData);

                    var rows = GetCachedRows (GetSection (indexPath.Section));
                    cell.IsFirst = indexPath.Row < 1 || !(rows [indexPath.Row - 1] is TaskModel);
                    cell.IsLast = indexPath.Row >= rows.Count || !(rows [indexPath.Row + 1] is TaskModel);
                    return cell;
                }

                var workspace = row as ProjectAndTaskView.Workspace;
                if (workspace != null) {
                    var cell = (WorkspaceHeaderCell)tableView.DequeueReusableCell (WorkspaceHeaderId, indexPath);
                    cell.Bind (workspace);
                    return cell;
                }

                throw new InvalidOperationException (String.Format ("Unknown row type {0}", row.GetType ()));
            }

            public override UIView GetViewForHeader (UITableView tableView, int section)
            {
                return new UIView ().Apply (Style.ProjectList.HeaderBackgroundView);
            }

            public override bool CanEditRow (UITableView tableView, NSIndexPath indexPath)
            {
                return false;
            }

            public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
            {
                var m = GetRow (indexPath);

                if (m is TaskData) {
                    var data = (TaskData)m;
                    controller.Finish ((TaskModel)data);
                } else if (m is ProjectAndTaskView.Project) {
                    var wrap = (ProjectAndTaskView.Project)m;
                    if (wrap.IsNoProject) {
                        controller.Finish (workspace: new WorkspaceModel (wrap.WorkspaceId));
                    } else if (wrap.IsNewProject) {
                        var proj = (ProjectModel)wrap.Data;
                        // Show create project dialog instead
                        var next = new NewProjectViewController (proj.Workspace, proj.Color) {
                            ProjectCreated = (p) => controller.Finish (project: p),
                        };
                        controller.NavigationController.PushViewController (next, true);
                    } else {
                        controller.Finish (project: (ProjectModel)wrap.Data);
                    }
                } else if (m is ProjectAndTaskView.Workspace) {
                    var wrap = (ProjectAndTaskView.Workspace)m;
                    controller.Finish (workspace: (WorkspaceModel)wrap.Data);
                }

                tableView.DeselectRow (indexPath, true);
            }

            protected override IEnumerable<object> GetRows (string section)
            {
                foreach (var row in DataView.Filter(Filter)) {
                    var task = row as TaskData;
                    if (task != null && !expandedProjects.Contains (task.ProjectId))
                        continue;

                    yield return row;
                }
            }
        }

        private class ProjectCell : ModelTableViewCell<ProjectAndTaskView.Project>
        {
            private UIView textContentView;
            private UILabel projectLabel;
            private UILabel clientLabel;
            private UIButton tasksButton;
            private ProjectModel model;

            public ProjectCell (IntPtr handle) : base (handle)
            {
                this.Apply (Style.Screen);
                BackgroundView = new UIView ();

                ContentView.Add (textContentView = new UIView ());
                ContentView.Add (tasksButton = new UIButton ().Apply (Style.ProjectList.TasksButtons));
                textContentView.Add (projectLabel = new UILabel ().Apply (Style.ProjectList.ProjectLabel));
                textContentView.Add (clientLabel = new UILabel ().Apply (Style.ProjectList.ClientLabel));

                var maskLayer = new CAGradientLayer () {
                    AnchorPoint = PointF.Empty,
                    StartPoint = new PointF (0.0f, 0.0f),
                    EndPoint = new PointF (1.0f, 0.0f),
                    Colors = new [] {
                        UIColor.FromWhiteAlpha (1, 1).CGColor,
                        UIColor.FromWhiteAlpha (1, 1).CGColor,
                        UIColor.FromWhiteAlpha (1, 0).CGColor,
                    },
                    Locations = new [] {
                        NSNumber.FromFloat (0f),
                        NSNumber.FromFloat (0.9f),
                        NSNumber.FromFloat (1f),
                    },
                };
                textContentView.Layer.Mask = maskLayer;

                tasksButton.TouchUpInside += OnTasksButtonTouchUpInside;
            }

            protected override void OnDataSourceChanged ()
            {
                model = null;
                if (DataSource != null) {
                    model = (ProjectModel)DataSource.Data;
                }

                base.OnDataSourceChanged ();
            }

            private void OnTasksButtonTouchUpInside (object sender, EventArgs e)
            {
                var cb = ToggleTasks;
                if (cb != null) {
                    cb ();
                }
            }

            public Action ToggleTasks { get; set; }

            public override void LayoutSubviews ()
            {
                base.LayoutSubviews ();

                var contentFrame = new RectangleF (0, CellSpacing / 2, Frame.Width, Frame.Height - CellSpacing);
                SelectedBackgroundView.Frame = BackgroundView.Frame = ContentView.Frame = contentFrame;

                if (!tasksButton.Hidden) {
                    var virtualWidth = contentFrame.Height;
                    var buttonWidth = tasksButton.CurrentBackgroundImage.Size.Width;
                    var extraPadding = (virtualWidth - buttonWidth) / 2f;
                    tasksButton.Frame = new RectangleF (
                        contentFrame.Width - virtualWidth + extraPadding, extraPadding,
                        buttonWidth, buttonWidth);
                    contentFrame.Width -= virtualWidth;
                }

                contentFrame.X += 13f;
                contentFrame.Width -= 13f;
                textContentView.Frame = contentFrame;
                textContentView.Layer.Mask.Bounds = contentFrame;

                contentFrame = new RectangleF (PointF.Empty, contentFrame.Size);

                if (clientLabel.Hidden) {
                    // Only display single item, so make it fill the whole text frame
                    var bounds = GetBoundingRect (projectLabel);
                    projectLabel.Frame = new RectangleF (
                        x: 0,
                        y: (contentFrame.Height - bounds.Height + projectLabel.Font.Descender) / 2f,
                        width: contentFrame.Width,
                        height: bounds.Height
                    );
                } else {
                    // Carefully craft the layout
                    var bounds = GetBoundingRect (projectLabel);
                    projectLabel.Frame = new RectangleF (
                        x: 0,
                        y: (contentFrame.Height - bounds.Height + projectLabel.Font.Descender) / 2f,
                        width: bounds.Width,
                        height: bounds.Height
                    );

                    const float clientLeftMargin = 7.5f;
                    bounds = GetBoundingRect (clientLabel);
                    clientLabel.Frame = new RectangleF (
                        x: projectLabel.Frame.X + projectLabel.Frame.Width + clientLeftMargin,
                        y: (float)Math.Floor (projectLabel.Frame.Y + projectLabel.Font.Ascender - clientLabel.Font.Ascender),
                        width: bounds.Width,
                        height: bounds.Height
                    );
                }
            }

            private static RectangleF GetBoundingRect (UILabel view)
            {
                var attrs = new UIStringAttributes () {
                    Font = view.Font,
                };
                var rect = ((NSString)(view.Text ?? String.Empty)).GetBoundingRect (
                               new SizeF (Single.MaxValue, Single.MaxValue),
                               NSStringDrawingOptions.UsesLineFragmentOrigin,
                               attrs, null);
                rect.Height = (float)Math.Ceiling (rect.Height);
                return rect;
            }

            protected override void ResetTrackedObservables ()
            {
                Tracker.MarkAllStale ();

                if (model != null) {
                    Tracker.Add (model, HandleProjectPropertyChanged);

                    if (model.Client != null) {
                        Tracker.Add (model.Client, HandleClientPropertyChanged);
                    }
                }

                Tracker.ClearStale ();
            }

            private void HandleProjectPropertyChanged (string prop)
            {
                if (prop == ProjectModel.PropertyClient
                    || prop == ProjectModel.PropertyName
                    || prop == ProjectModel.PropertyColor)
                    Rebind ();
            }

            private void HandleClientPropertyChanged (string prop)
            {
                if (prop == ClientModel.PropertyName)
                    Rebind ();
            }

            protected override void Rebind ()
            {
                ResetTrackedObservables ();

                UIColor projectColor;
                string projectName;
                string clientName = String.Empty;
                int taskCount = 0;

                if (DataSource.IsNoProject) {
                    projectColor = Color.Gray;
                    projectName = "ProjectNoProject".Tr ();
                    projectLabel.Apply (Style.ProjectList.NoProjectLabel);
                } else if (DataSource.IsNewProject) {
                    projectColor = Color.LightestGray;
                    projectName = "ProjectNewProject".Tr ();
                    projectLabel.Apply (Style.ProjectList.NewProjectLabel);
                } else if (model != null) {
                    projectColor = UIColor.Clear.FromHex (model.GetHexColor ());

                    projectName = model.Name;
                    clientName = model.Client != null ? model.Client.Name : String.Empty;
                    taskCount = DataSource.Tasks.Count;
                    projectLabel.Apply (Style.ProjectList.ProjectLabel);
                } else {
                    return;
                }

                if (String.IsNullOrWhiteSpace (projectName)) {
                    projectName = "ProjectNoNameProject".Tr ();
                    clientName = String.Empty;
                }

                if (!String.IsNullOrWhiteSpace (projectName)) {
                    projectLabel.Text = projectName;
                    projectLabel.Hidden = false;

                    if (!String.IsNullOrEmpty (clientName)) {
                        clientLabel.Text = clientName;
                        clientLabel.Hidden = false;
                    } else {
                        clientLabel.Hidden = true;
                    }
                } else {
                    projectLabel.Hidden = true;
                    clientLabel.Hidden = true;
                }

                tasksButton.Hidden = taskCount < 1;
                if (!tasksButton.Hidden) {
                    tasksButton.SetTitle (taskCount.ToString (), UIControlState.Normal);
                    tasksButton.SetTitleColor (projectColor, UIControlState.Normal);
                }

                BackgroundView.BackgroundColor = projectColor;
            }
        }

        private class TaskCell : ModelTableViewCell<TaskModel>
        {
            private readonly UILabel nameLabel;
            private readonly UIView separatorView;
            private bool isFirst;
            private bool isLast;

            public TaskCell (IntPtr handle) : base (handle)
            {
                this.Apply (Style.Screen);
                ContentView.Add (nameLabel = new UILabel ().Apply (Style.ProjectList.TaskLabel));
                ContentView.Add (separatorView = new UIView ().Apply (Style.ProjectList.TaskSeparator));
                BackgroundView = new UIView ().Apply (Style.ProjectList.TaskBackground);
            }

            public override void LayoutSubviews ()
            {
                base.LayoutSubviews ();

                var contentFrame = new RectangleF (0, 0, Frame.Width, Frame.Height);

                if (isFirst) {
                    contentFrame.Y += CellSpacing / 2;
                    contentFrame.Height -= CellSpacing / 2;
                }

                if (isLast) {
                    contentFrame.Height -= CellSpacing / 2;
                }

                SelectedBackgroundView.Frame = BackgroundView.Frame = ContentView.Frame = contentFrame;

                // Add padding
                contentFrame.X = 15f;
                contentFrame.Y = 0;
                contentFrame.Width -= 15f;

                nameLabel.Frame = contentFrame;
                separatorView.Frame = new RectangleF (
                    contentFrame.X, contentFrame.Y + contentFrame.Height - 1f,
                    contentFrame.Width, 1f);
            }

            protected override void Rebind ()
            {
                ResetTrackedObservables ();

                var taskName = DataSource.Name;
                if (String.IsNullOrWhiteSpace (taskName))
                    taskName = "ProjectNoNameTask".Tr ();
                nameLabel.Text = taskName;
            }

            protected override void ResetTrackedObservables ()
            {
                Tracker.MarkAllStale ();

                if (DataSource != null) {
                    Tracker.Add (DataSource, HandleTaskPropertyChanged);
                }

                Tracker.ClearStale ();
            }

            private void HandleTaskPropertyChanged (string prop)
            {
                if (prop == TaskModel.PropertyName)
                    Rebind ();
            }

            public bool IsFirst {
                get { return isFirst; }
                set {
                    if (isFirst == value)
                        return;
                    isFirst = value;
                    SetNeedsLayout ();
                }
            }

            public bool IsLast {
                get { return isLast; }
                set {
                    if (isLast == value)
                        return;
                    isLast = value;
                    SetNeedsLayout ();

                    separatorView.Hidden = isLast;
                }
            }
        }

        private class WorkspaceHeaderCell : ModelTableViewCell<ProjectAndTaskView.Workspace>
        {
            private const float HorizSpacing = 15f;
            private readonly UILabel nameLabel;
            private WorkspaceModel model;

            public WorkspaceHeaderCell (IntPtr handle) : base (handle)
            {
                this.Apply (Style.Screen);
                nameLabel = new UILabel ().Apply (Style.ProjectList.HeaderLabel);
                ContentView.AddSubview (nameLabel);

                BackgroundView = new UIView ().Apply (Style.ProjectList.HeaderBackgroundView);
                UserInteractionEnabled = false;
            }

            protected override void OnDataSourceChanged ()
            {
                model = null;
                if (DataSource != null) {
                    model = (WorkspaceModel)DataSource.Data;
                }

                base.OnDataSourceChanged ();
            }

            public override void LayoutSubviews ()
            {
                base.LayoutSubviews ();
                var contentFrame = ContentView.Frame;

                nameLabel.Frame = new RectangleF (
                    x: HorizSpacing,
                    y: 0,
                    width: contentFrame.Width - 2 * HorizSpacing,
                    height: contentFrame.Height
                );
            }

            protected override void ResetTrackedObservables ()
            {
                Tracker.MarkAllStale ();

                if (model != null) {
                    Tracker.Add (model, HandleClientPropertyChanged);
                }

                Tracker.ClearStale ();
            }

            private void HandleClientPropertyChanged (string prop)
            {
                if (prop == WorkspaceModel.PropertyName)
                    Rebind ();
            }

            protected override void Rebind ()
            {
                ResetTrackedObservables ();

                if (model != null) {
                    nameLabel.Text = model.Name;
                }
            }
        }
    }
}
