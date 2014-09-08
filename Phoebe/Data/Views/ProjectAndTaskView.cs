﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Net;
using XPlatUtils;

namespace Toggl.Phoebe.Data.Views
{
    public class ProjectAndTaskView : IDataView<object>, IDisposable
    {
        private readonly List<Workspace> workspaceWrappers = new List<Workspace> ();
        private readonly List<ClientData> clientDataObjects = new List<ClientData> ();
        private UserData userData;
        private Subscription<DataChangeMessage> subscriptionDataChange;
        private string filter;

        public ProjectAndTaskView ()
        {
            var bus = ServiceContainer.Resolve<MessageBus> ();
            subscriptionDataChange = bus.Subscribe<DataChangeMessage> (OnDataChange);

            Reload ();
        }

        public void Dispose ()
        {
            if (subscriptionDataChange != null) {
                var bus = ServiceContainer.Resolve<MessageBus> ();
                bus.Unsubscribe (subscriptionDataChange);
                subscriptionDataChange = null;
            }
        }

        public string Filter {
            get {
                return filter;
            }
            set {
                value = value != null ? value.ToLower () : null;
                if (value != filter) {
                    filter = value;
                    OnUpdated ();
                }
            }
        }

        private void OnDataChange (DataChangeMessage msg)
        {
            if (msg.Data is UserData) {
                OnDataChange ((UserData)msg.Data);
            } else if (msg.Data is WorkspaceData) {
                OnDataChange ((WorkspaceData)msg.Data, msg.Action);
            } else if (msg.Data is ProjectData) {
                OnDataChange ((ProjectData)msg.Data, msg.Action);
            } else if (msg.Data is TaskData) {
                OnDataChange ((TaskData)msg.Data, msg.Action);
            } else if (msg.Data is ClientData) {
                OnDataChange ((ClientData)msg.Data, msg.Action);
            }
        }

        private void OnDataChange (UserData data)
        {
            var existingData = userData;

            userData = data;
            if (existingData == null || existingData.DefaultWorkspaceId != data.DefaultWorkspaceId) {
                SortWorkspaces (workspaceWrappers);
                OnUpdated ();
            }

            userData = data;
        }

        private void OnDataChange (WorkspaceData data, DataAction action)
        {
            var isExcluded = action == DataAction.Delete
                             || data.DeletedAt.HasValue;

            Workspace workspace;

            if (isExcluded) {
                if (FindWorkspace (data.Id, out workspace)) {
                    workspaceWrappers.Remove (workspace);
                    OnUpdated ();
                }
            } else {
                data = new WorkspaceData (data);

                if (FindWorkspace (data.Id, out workspace)) {
                    var existingData = workspace.Data;

                    workspace.Data = data;
                    if (existingData.Name != data.Name) {
                        SortWorkspaces (workspaceWrappers);
                    }
                    OnUpdated ();
                } else {
                    workspace = new Workspace (data);
                    workspaceWrappers.Add (workspace);
                    SortWorkspaces (workspaceWrappers);
                    OnUpdated ();
                }
            }
        }

        private void OnDataChange (ProjectData data, DataAction action)
        {
            var isExcluded = action == DataAction.Delete
                             || data.DeletedAt.HasValue
                             || !data.IsActive;

            Workspace workspace;
            Project project;

            if (isExcluded) {
                if (FindProject (data.Id, out workspace, out project)) {
                    workspace.Projects.Remove (project);
                    OnUpdated ();
                }
            } else {
                data = new ProjectData (data);

                if (FindProject (data.Id, out workspace, out project)) {
                    var existingData = project.Data;

                    var shouldReparent = existingData.WorkspaceId != data.WorkspaceId;
                    var shouldSort = existingData.Name != data.Name
                                     || existingData.ClientId != data.ClientId
                                     || shouldReparent;

                    project.Data = data;

                    if (shouldReparent) {
                        workspace.Projects.Remove (project);
                        if (FindWorkspace (data.WorkspaceId, out workspace)) {
                            workspace.Projects.Add (project);
                        }
                    }

                    if (shouldSort && workspace != null) {
                        SortProjects (workspace.Projects, clientDataObjects);
                    }
                    OnUpdated ();
                } else if (FindWorkspace (data.WorkspaceId, out workspace)) {
                    project = new Project (data);

                    workspace.Projects.Add (project);
                    SortProjects (workspace.Projects, clientDataObjects);
                    OnUpdated ();
                }
            }
        }

        private void OnDataChange (TaskData data, DataAction action)
        {
            var isExcluded = action == DataAction.Delete
                             || data.DeletedAt.HasValue
                             || !data.IsActive;

            Workspace workspace;
            Project project;
            TaskData existingData;

            if (isExcluded) {
                if (FindTask (data.Id, out workspace, out project, out existingData)) {
                    project.Tasks.Remove (existingData);
                    OnUpdated ();
                }
            } else {
                data = new TaskData (data);

                if (FindTask (data.Id, out workspace, out project, out existingData)) {
                    var shouldReparent = existingData.ProjectId != data.ProjectId;
                    var shouldSort = existingData.Name != data.Name
                                     || shouldReparent;

                    if (shouldReparent) {
                        project.Tasks.Remove (existingData);

                        if (FindProject (data.ProjectId, out workspace, out project)) {
                            project.Tasks.Add (data);
                        }
                    } else {
                        project.Tasks.UpdateData (data);
                    }

                    if (shouldSort) {
                        SortTasks (project.Tasks);
                    }

                    OnUpdated ();
                } else if (FindProject (data.ProjectId, out workspace, out project)) {
                    project.Tasks.Add (data);
                    SortTasks (project.Tasks);
                    OnUpdated ();
                }
            }
        }

        private void OnDataChange (ClientData data, DataAction action)
        {
            var isExcluded = action == DataAction.Delete
                             || data.DeletedAt.HasValue;

            var existingData = clientDataObjects.FirstOrDefault (item => data.Matches (item));

            if (isExcluded) {
                if (existingData != null) {
                    clientDataObjects.Remove (existingData);
                }
            } else {
                data = new ClientData (data);

                if (existingData != null) {
                    clientDataObjects.UpdateData (data);

                    var shouldSort = data.Name != existingData.Name;

                    if (shouldSort) {
                        Workspace workspace;
                        if (FindWorkspace (data.WorkspaceId, out workspace)) {
                            SortProjects (workspace.Projects, clientDataObjects);
                        }
                    }
                } else {
                    clientDataObjects.Add (data);
                }
            }
        }

        private bool FindWorkspace (Guid id, out Workspace workspace)
        {
            foreach (var ws in workspaceWrappers) {
                if (ws.Data.Id == id) {
                    workspace = ws;
                    return true;
                }
            }

            workspace = null;
            return false;
        }

        private bool FindProject (Guid id, out Workspace workspace, out Project project)
        {
            foreach (var ws in workspaceWrappers) {
                foreach (var proj in ws.Projects) {
                    if (proj.Data != null && proj.Data.Id == id) {
                        workspace = ws;
                        project = proj;
                        return true;
                    }
                }
            }

            workspace = null;
            project = null;
            return false;
        }

        private bool FindTask (Guid id, out Workspace workspace, out Project project, out TaskData existingData)
        {
            foreach (var ws in workspaceWrappers) {
                foreach (var proj in ws.Projects) {
                    foreach (var task in proj.Tasks) {
                        if (task.Id == id) {
                            workspace = ws;
                            project = proj;
                            existingData = task;
                            return true;
                        }
                    }
                }
            }

            workspace = null;
            project = null;
            existingData = null;
            return false;
        }

        public event EventHandler Updated;

        private void OnUpdated ()
        {
            var handler = Updated;
            if (handler != null) {
                handler (this, EventArgs.Empty);
            }
        }

        public async void Reload ()
        {
            if (IsLoading)
                return;

            var bus = ServiceContainer.Resolve<MessageBus> ();
            var shouldSubscribe = subscriptionDataChange != null;

            if (subscriptionDataChange != null) {
                bus.Unsubscribe (subscriptionDataChange);
                subscriptionDataChange = null;
                shouldSubscribe = true;
            }

            try {
                var store = ServiceContainer.Resolve<IDataStore> ();

                userData = ServiceContainer.Resolve<AuthManager> ().User;
                var userId = userData != null ? userData.Id : (Guid?)null;

                IsLoading = true;
                this.workspaceWrappers.Clear ();
                clientDataObjects.Clear ();
                OnUpdated ();

                var workspacesTask = store.Table<WorkspaceData> ()
                    .QueryAsync (r => r.DeletedAt == null);
                var projectsTask = store.GetUserAccessibleProjects (userId ?? Guid.Empty);
                var tasksTask = store.Table<TaskData> ()
                    .QueryAsync (r => r.DeletedAt == null && r.IsActive == true);
                var clientsTask = store.Table<ClientData> ()
                    .QueryAsync (r => r.DeletedAt == null);

                await Task.WhenAll (workspacesTask, projectsTask, tasksTask, clientsTask);

                var workspaces = workspacesTask.Result;
                foreach (var workspaceData in workspaces) {
                    var workspace = new Workspace (workspaceData);

                    var projects = projectsTask.Result.Where (r => r.WorkspaceId == workspaceData.Id);
                    foreach (var projectData in projects) {
                        var project = new Project (projectData);

                        var tasks = tasksTask.Result.Where (r => r.ProjectId == projectData.Id);
                        project.Tasks.AddRange (tasks);
                        workspace.Projects.Add (project);
                    }

                    workspaceWrappers.Add (workspace);
                }

                clientDataObjects.AddRange (clientsTask.Result);

                // Sort everything:
                SortWorkspaces (workspaceWrappers);
                foreach (var workspace in workspaceWrappers) {
                    SortProjects (workspace.Projects, clientDataObjects);
                    foreach (var project in workspace.Projects) {
                        SortTasks (project.Tasks);
                    }
                }
            } finally {
                IsLoading = false;
                OnUpdated ();

                if (shouldSubscribe) {
                    subscriptionDataChange = bus.Subscribe<DataChangeMessage> (OnDataChange);
                }
            }
        }

        private static void SortWorkspaces (List<Workspace> data)
        {
            var user = ServiceContainer.Resolve<AuthManager> ().User;
            data.Sort ((a, b) => {
                if (user != null) {
                    if (a.Data != null && a.Data.Id == user.DefaultWorkspaceId) {
                        return -1;
                    }
                    if (b.Data != null && b.Data.Id == user.DefaultWorkspaceId) {
                        return 1;
                    }
                }

                var aName = a.Data != null ? (a.Data.Name ?? String.Empty) : String.Empty;
                var bName = b.Data != null ? (b.Data.Name ?? String.Empty) : String.Empty;
                return String.Compare (aName, bName, StringComparison.Ordinal);
            });
        }

        private static void SortProjects (List<Project> data, List<ClientData> clients)
        {
            data.Sort ((a, b) => {
                if (a.IsNoProject != b.IsNoProject) {
                    return a.IsNoProject ? -1 : 1;
                }

                if (a.IsNewProject != b.IsNewProject) {
                    return a.IsNewProject ? 1 : -1;
                }

                var aName = a.Data != null ? (a.Data.Name ?? String.Empty) : String.Empty;
                var bName = b.Data != null ? (b.Data.Name ?? String.Empty) : String.Empty;
                var res = String.Compare (aName, bName, StringComparison.Ordinal);

                // Try to order by client name when same project name
                if (res == 0) {
                    var aClient = a.Data != null ? clients.FirstOrDefault (r => r.Id == a.Data.ClientId) : null;
                    var bClient = b.Data != null ? clients.FirstOrDefault (r => r.Id == b.Data.ClientId) : null;

                    var aClientName = aClient != null ? aClient.Name ?? String.Empty : String.Empty;
                    var bClientName = bClient != null ? bClient.Name ?? String.Empty : String.Empty;

                    res = String.Compare (aClientName, bClientName, StringComparison.Ordinal);
                }

                return res;
            });
        }

        private static void SortTasks (List<TaskData> data)
        {
            data.Sort ((a, b) => String.Compare (
                a.Name ?? String.Empty,
                b.Name ?? String.Empty,
                StringComparison.Ordinal
            ));
        }

        public void LoadMore ()
        {
        }

        public IEnumerable<object> Data {
            get {
                var includeWorkspaces = workspaceWrappers.Count > 1;

                foreach (var ws in workspaceWrappers) {
                    if (includeWorkspaces) {
                        yield return ws;
                    }
                        
                    foreach (var proj in ws.Projects) {
                        bool projectMatches = true;
                        if (proj.IsNewProject) {
                            yield return proj;
                            continue;
                        }
                        if (!String.IsNullOrEmpty (filter)) {
                            if (proj.IsNoProject) {
                                continue;
                            } else if (proj.Data != null) {
                                var projName = proj.Data.Name ?? String.Empty;
                                if (!projName.ToLower ().Contains (filter)) {
                                    projectMatches = false;
                                }
                            }
                        }

                        if (projectMatches) {
                            yield return proj;
                            foreach (var task in proj.Tasks) {
                                yield return task;
                            }
                        } else {
                            var filteredTasks = new List<TaskData> ();
                            foreach (var task in proj.Tasks) {
                                var taskName = task.Name ?? String.Empty;
                                if (taskName.ToLower ().Contains (filter)) {
                                    filteredTasks.Add (task);
                                }
                            }
                            if (filteredTasks.Count > 0) {
                                yield return proj;
                                foreach (var task in filteredTasks) {
                                    yield return task;
                                }
                            }
                        }
                    }
                }
            }
        }

        public IEnumerable<Workspace> Workspaces {
            get { return workspaceWrappers; }
        }

        public long Count {
            get { return Data.LongCount (); }
        }

        public bool HasMore {
            get { return false; }
        }

        public bool IsLoading { get; private set; }

        public class Workspace
        {
            private WorkspaceData dataObject;
            private readonly List<Project> projects = new List<Project> ();

            public Workspace (WorkspaceData dataObject)
            {
                this.dataObject = dataObject;
                projects.Add (new Project (dataObject));
                projects.Add (new Project (new ProjectData () {
                    WorkspaceId = dataObject.Id,
                    Color = new Random ().Next (),
                }));
            }

            public WorkspaceData Data {
                get { return dataObject; }
                set {
                    if (value == null)
                        throw new ArgumentNullException ("value");
                    if (dataObject.Id != value.Id)
                        throw new ArgumentException ("Cannot change Id of the workspace.", "value");
                    dataObject = value;
                }
            }

            public List<Project> Projects {
                get { return projects; }
            }
        }

        public class Project
        {
            private ProjectData dataObject;
            private readonly List<TaskData> tasks = new List<TaskData> ();
            private readonly Guid workspaceId;

            public Project (ProjectData dataObject)
            {
                this.dataObject = dataObject;
                workspaceId = dataObject.WorkspaceId;
            }

            public Project (WorkspaceData workspaceData)
            {
                this.dataObject = null;
                workspaceId = workspaceData.Id;
            }

            public bool IsNoProject {
                get { return dataObject == null; }
            }

            public bool IsNewProject {
                get { return dataObject != null && dataObject.Id == Guid.Empty; }
            }

            public Guid WorkspaceId {
                get { return dataObject != null ? dataObject.WorkspaceId : workspaceId; }
            }

            public ProjectData Data {
                get { return dataObject; }
                set {
                    if (value == null)
                        throw new ArgumentNullException ("value");
                    if (dataObject.Id != value.Id)
                        throw new ArgumentException ("Cannot change Id of the project.", "value");
                    dataObject = value;
                }
            }

            public List<TaskData> Tasks {
                get { return tasks; }
            }
        }
    }
}
