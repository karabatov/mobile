﻿using System;
using System.Collections.Generic;
using NUnit.Framework;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.DataObjects;

namespace Toggl.Phoebe.Tests.Data
{
    [TestFixture]
    public class DataStoreTest : Test
    {
        private readonly List<DataChangeMessage> messages = new List<DataChangeMessage> ();
        private Subscription<DataChangeMessage> subscriptionDataChange;

        public override void SetUp ()
        {
            base.SetUp ();

            messages.Clear ();
            subscriptionDataChange = MessageBus.Subscribe<DataChangeMessage> ((msg) => {
                lock (messages) {
                    messages.Add (msg);
                }
            }, threadSafe: true);
        }

        public override void TearDown ()
        {
            MessageBus.Unsubscribe (subscriptionDataChange);
            subscriptionDataChange = null;

            base.TearDown ();
        }

        [Test]
        public void TestObjectCreation ()
        {
            RunAsync (async delegate {
                var obj1 = new WorkspaceData () {
                    Name = "Test",
                };

                var obj2 = await DataStore.PutAsync (obj1);

                Assert.AreNotSame (obj1, obj2, "Put should return a new instance of the object.");
                Assert.AreNotEqual (obj1.Id, obj2.Id, "Primary key was not set!");

                // Verify that single message was sent
                Assert.That (messages, Has.Count.EqualTo (1));
                Assert.That (messages, Has.Exactly (1)
                    .Matches<DataChangeMessage> (msg => msg.Action == DataAction.Put && obj2.Matches (msg.Data)));
            });
        }

        [Test]
        public void TestObjectUpdating ()
        {
            RunAsync (async delegate {
                // Create new object
                var obj1 = await DataStore.PutAsync (new WorkspaceData () {
                    Name = "Test",
                });

                // Update it
                obj1.Name = "Other";
                var obj2 = await DataStore.PutAsync (obj1);

                Assert.AreNotSame (obj1, obj2, "Put should return a new instance of the object.");
                Assert.AreEqual ("Other", obj2.Name, "Property was not updated.");

                // Verify that the message about object update was delivered
                Assert.That (messages, Has.Count.EqualTo (2));
                Assert.That (messages, Has.Exactly (2)
                    .Matches<DataChangeMessage> (msg => msg.Action == DataAction.Put && obj2.Matches (msg.Data)));
            });
        }

        [Test]
        public void TestObjectDeleting ()
        {
            RunAsync (async delegate {
                // Create new object
                var obj1 = await DataStore.PutAsync (new WorkspaceData () {
                    Name = "Test",
                });

                // Delete it
                var success = await DataStore.DeleteAsync (obj1);
                Assert.IsTrue (success, "Object was not deleted.");

                // Verify that the message about object delete was delivered
                Assert.That (messages, Has.Count.EqualTo (2));
                Assert.That (messages, Has.Exactly (1)
                    .Matches<DataChangeMessage> (msg => msg.Action == DataAction.Put && obj1.Matches (msg.Data)));
                Assert.That (messages, Has.Exactly (1)
                    .Matches<DataChangeMessage> (msg => msg.Action == DataAction.Delete && obj1.Matches (msg.Data)));
            });
        }

        [Test]
        public void TestFailedDelete ()
        {
            RunAsync (async delegate {
                // Create non-existing object
                var obj1 = new WorkspaceData () {
                    Id = Guid.NewGuid (),
                    Name = "Test",
                };

                // Delete it
                var success = await DataStore.DeleteAsync (obj1);
                Assert.IsFalse (success, "Delete should've failed.");

                Assert.That (messages, Has.Count.EqualTo (0));
            });
        }

        [Test]
        public void TestQuery ()
        {
            RunAsync (async delegate {
                // Create some data
                await DataStore.PutAsync (new WorkspaceData () {
                    Name = "Test #1",
                });
                await DataStore.PutAsync (new WorkspaceData () {
                    Name = "Test #2",
                });
                await DataStore.PutAsync (new WorkspaceData () {
                    Name = "Foo #1",
                });
                messages.Clear ();

                var count = await DataStore.Table<WorkspaceData> ().CountAsync ();
                Assert.AreEqual (3, count, "Query returned false count for items.");

                count = await DataStore.Table<WorkspaceData> ().Where ((m) => m.Name.StartsWith ("Foo")).CountAsync ();
                Assert.AreEqual (1, count, "Query returned false count for items starting with Foo");

                var data = await DataStore.Table<WorkspaceData> ().QueryAsync ((m) => m.Name.StartsWith ("Test"));
                foreach (var obj in data) {
                    Assert.AreNotEqual ("Foo #1", obj.Name);
                }

                data = await DataStore.Table<WorkspaceData> ()
                    .OrderBy ((m) => m.Name)
                    .Take (1).Skip (1)
                    .QueryAsync ((m) => m.Name.StartsWith ("Test"));
                Assert.AreEqual (1, data.Count, "Should've received only a single result");
                Assert.AreEqual ("Test #2", data [0].Name, "Invalid item returned");

                Assert.That (messages, Has.Count.EqualTo (0));
            });
        }

        [Test]
        public void TestTransaction ()
        {
            RunAsync (async delegate {
                await DataStore.ExecuteInTransactionAsync ((ctx) => {
                    var obj1 = ctx.Put (new WorkspaceData () {
                        Name = "Test",
                    });

                    var obj2 = ctx.Connection.Get<WorkspaceData> (obj1.Id);
                    Assert.IsNotNull (obj2);
                    Assert.AreEqual (obj1.Id, obj2.Id);
                    Assert.AreEqual (obj1.Name, obj2.Name);
                });

                // Verify messages
                Assert.That (messages, Has.Count.EqualTo (1));
                Assert.That (messages, Has.Exactly (1)
                    .Matches<DataChangeMessage> ((msg) => msg.Action == DataAction.Put));
            });
        }

        [Test]
        public void TestTransactionRollback ()
        {
            RunAsync (async delegate {
                var obj1 = await DataStore.PutAsync (new WorkspaceData () {
                    Name = "Test",
                });
                messages.Clear ();

                try {
                    await DataStore.ExecuteInTransactionAsync ((ctx) => {
                        Assert.IsTrue (ctx.Delete (obj1));
                        throw new NotImplementedException ();
                    });
                } catch (NotImplementedException) {
                }

                // Verify no delete messages got through messages
                Assert.That (messages, Has.Count.EqualTo (0));
            });
        }
    }
}
