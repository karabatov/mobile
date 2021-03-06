﻿using System;
using System.Collections.Generic;
using Cirrious.FluentLayouts.Touch;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using Toggl.Phoebe;
using Toggl.Phoebe.Net;
using Toggl.Ross.Theme;

namespace Toggl.Ross.ViewControllers
{
    public class FeedbackViewController : UIViewController
    {
        private readonly List<NSObject> notificationObjects = new List<NSObject> ();
        private float keyboardDuration;
        private float keyboardHeight;
        private UILabel moodLabel;
        private UIView positiveMoodSeparatorView;
        private UIView negativeMoodSeparatorView;
        private UIButton positiveMoodButton;
        private UIButton neutralMoodButton;
        private UIButton negativeMoodButton;
        private UIView messageTopBorderView;
        private UITextView messageTextView;
        private UIView messageBottomBorderView;
        private UIButton sendButton;
        private FeedbackMessage.Mood? userMood;
        private bool isSending;

        public FeedbackViewController ()
        {
            Title = "FeedbackTitle".Tr ();
        }

        public override void LoadView ()
        {
            View = new UIView ().Apply (Style.Screen);

            Add (moodLabel = new UILabel () {
                TranslatesAutoresizingMaskIntoConstraints = false,
                Text = "FeedbackMood".Tr (),
            }.Apply (Style.Feedback.MoodLabel));

            Add (positiveMoodButton = new UIButton () {
                TranslatesAutoresizingMaskIntoConstraints = false,
            }.Apply (Style.Feedback.PositiveMoodButton));
            positiveMoodButton.TouchUpInside += OnMoodButtonTouchUpInside;
            Add (neutralMoodButton = new UIButton () {
                TranslatesAutoresizingMaskIntoConstraints = false,
            }.Apply (Style.Feedback.NeutralMoodButton));
            neutralMoodButton.TouchUpInside += OnMoodButtonTouchUpInside;
            Add (negativeMoodButton = new UIButton () {
                TranslatesAutoresizingMaskIntoConstraints = false,
            }.Apply (Style.Feedback.NegativeMoodButton));
            negativeMoodButton.TouchUpInside += OnMoodButtonTouchUpInside;
            Add (positiveMoodSeparatorView = new UIView () {
                TranslatesAutoresizingMaskIntoConstraints = false,
            }.Apply (Style.Feedback.MoodSeparator));
            Add (negativeMoodSeparatorView = new UIView () {
                TranslatesAutoresizingMaskIntoConstraints = false,
            }.Apply (Style.Feedback.MoodSeparator));
            Add (messageTopBorderView = new UIView () {
                TranslatesAutoresizingMaskIntoConstraints = false,
            }.Apply (Style.Feedback.MessageBorder));
            Add (messageTextView = new UITextView () {
                TranslatesAutoresizingMaskIntoConstraints = false,
            }.Apply (Style.Feedback.MessageField));
            messageTextView.Changed += (s, e) => RebindSendButton ();
            Add (messageBottomBorderView = new UIView () {
                TranslatesAutoresizingMaskIntoConstraints = false,
            }.Apply (Style.Feedback.MessageBorder));
            Add (sendButton = new UIButton () {
                TranslatesAutoresizingMaskIntoConstraints = false,
            }.Apply (Style.Feedback.SendButton));
            sendButton.TouchUpInside += OnSendTouchUpInside;

            RebindSendButton ();
            ResetConstraints ();
        }

        private void ResetConstraints ()
        {
            View.RemoveConstraints (View.Constraints);

            var keyboardVisible = keyboardHeight >= 1f;

            View.AddConstraints (
                moodLabel.AtTopOf (View, keyboardVisible ? 20f : 70f),
                moodLabel.AtLeftOf (View, 5f),
                moodLabel.AtRightOf (View, 5f),
                moodLabel.Height ().EqualTo (40f),

                neutralMoodButton.Below (moodLabel, 5f),
                neutralMoodButton.WithSameCenterX (View),

                positiveMoodSeparatorView.ToLeftOf (neutralMoodButton, 30f),
                positiveMoodSeparatorView.WithSameCenterY (neutralMoodButton),
                positiveMoodSeparatorView.Height ().EqualTo (15f),
                positiveMoodSeparatorView.Width ().EqualTo (1f),

                positiveMoodButton.ToLeftOf (positiveMoodSeparatorView, 30f),
                positiveMoodButton.WithSameCenterY (neutralMoodButton),

                negativeMoodSeparatorView.ToRightOf (neutralMoodButton, 30f),
                negativeMoodSeparatorView.WithSameCenterY (neutralMoodButton),
                negativeMoodSeparatorView.Height ().EqualTo (15f),
                negativeMoodSeparatorView.Width ().EqualTo (1f),

                negativeMoodButton.ToRightOf (negativeMoodSeparatorView, 30f),
                negativeMoodButton.WithSameCenterY (neutralMoodButton),

                messageTopBorderView.Below (neutralMoodButton, 15f),
                messageTopBorderView.AtLeftOf (View),
                messageTopBorderView.AtRightOf (View),
                messageTopBorderView.Height ().EqualTo (1f),

                messageTextView.Below (messageTopBorderView),
                messageTextView.AtLeftOf (View),
                messageTextView.AtRightOf (View),

                messageBottomBorderView.Below (messageTextView),
                messageBottomBorderView.AtLeftOf (View),
                messageBottomBorderView.AtRightOf (View),
                messageBottomBorderView.Height ().EqualTo (1f),

                sendButton.Below (messageBottomBorderView, 5f),
                sendButton.AtLeftOf (View),
                sendButton.AtRightOf (View),
                sendButton.AtBottomOf (View, (keyboardVisible ? keyboardHeight : 0f) + 5f),
                sendButton.Height ().EqualTo (60f),

                null
            );
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);

            ObserveNotification (UIKeyboard.WillHideNotification, (notif) => {
                var duration = notif.UserInfo.ObjectForKey (UIKeyboard.AnimationDurationUserInfoKey) as NSNumber;
                keyboardDuration = duration != null ? duration.FloatValue : 0.3f;

                OnKeyboardHeightChanged (0);
            });
            ObserveNotification (UIKeyboard.WillShowNotification, (notif) => {
                var duration = notif.UserInfo.ObjectForKey (UIKeyboard.AnimationDurationUserInfoKey) as NSNumber;
                keyboardDuration = duration != null ? duration.FloatValue : 0.3f;

                var frame = notif.UserInfo.ObjectForKey (UIKeyboard.FrameEndUserInfoKey) as NSValue;

                if (frame != null) {
                    OnKeyboardHeightChanged ((int)frame.CGRectValue.Height);
                }
            });
            ObserveNotification (UIKeyboard.WillChangeFrameNotification, (notif) => {
                var duration = notif.UserInfo.ObjectForKey (UIKeyboard.AnimationDurationUserInfoKey) as NSNumber;
                keyboardDuration = duration != null ? duration.FloatValue : 0.3f;

                var frame = notif.UserInfo.ObjectForKey (UIKeyboard.FrameEndUserInfoKey) as NSValue;

                if (frame != null) {
                    OnKeyboardHeightChanged ((int)frame.CGRectValue.Height);
                }
            });
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);

            NSNotificationCenter.DefaultCenter.RemoveObservers (notificationObjects);
            notificationObjects.Clear ();
        }

        private void ObserveNotification (string name, Action<NSNotification> callback)
        {
            var obj = NSNotificationCenter.DefaultCenter.AddObserver (name, callback);
            if (obj != null) {
                notificationObjects.Add (obj);
            }
        }

        private void OnKeyboardHeightChanged (float height)
        {
            keyboardHeight = height;

            UIView.Animate (keyboardDuration, () => {
                ResetConstraints ();
                View.LayoutIfNeeded ();
            });
        }

        private void OnSendTouchUpInside (object sender, EventArgs e)
        {
            messageTextView.ResignFirstResponder ();
            SendMessage ();
        }

        private void OnMoodButtonTouchUpInside (object sender, EventArgs e)
        {
            if (isSending)
                return;
            if (sender == positiveMoodButton) {
                userMood = FeedbackMessage.Mood.Positive;
            } else if (sender == neutralMoodButton) {
                userMood = FeedbackMessage.Mood.Neutral;
            } else if (sender == negativeMoodButton) {
                userMood = FeedbackMessage.Mood.Negative;
            } else {
                userMood = null;
            }
            RebindMoodButtons ();
            RebindSendButton ();
        }

        private void RebindMoodButtons ()
        {
            if (userMood == FeedbackMessage.Mood.Positive) {
                positiveMoodButton.Apply (Style.Feedback.PositiveMoodButtonSelected);
            } else {
                positiveMoodButton.Apply (Style.Feedback.PositiveMoodButton);
            }
            if (userMood == FeedbackMessage.Mood.Neutral) {
                neutralMoodButton.Apply (Style.Feedback.NeutralMoodButtonSelected);
            } else {
                neutralMoodButton.Apply (Style.Feedback.NeutralMoodButton);
            }
            if (userMood == FeedbackMessage.Mood.Negative) {
                negativeMoodButton.Apply (Style.Feedback.NegativeMoodButtonSelected);
            } else {
                negativeMoodButton.Apply (Style.Feedback.NegativeMoodButton);
            }
        }

        private void RebindSendButton ()
        {
            sendButton.Enabled = !isSending && userMood.HasValue && !String.IsNullOrWhiteSpace (messageTextView.Text);

            if (isSending) {
                sendButton.SetTitle ("FeedbackSending".Tr (), UIControlState.Normal);
                sendButton.SetTitle ("FeedbackSending".Tr (), UIControlState.Disabled);
            } else {
                sendButton.SetTitle ("FeedbackSend".Tr (), UIControlState.Normal);
                sendButton.SetTitle ("FeedbackSend".Tr (), UIControlState.Disabled);
            }
        }

        private async void SendMessage ()
        {
            isSending = true;
            View.UserInteractionEnabled = false;
            RebindSendButton ();

            try {
                var msg = new FeedbackMessage () {
                    CurrentMood = userMood.Value,
                    Message = messageTextView.Text,
                };

                var sent = await msg.Send ();
                if (sent) {
                    var appStoreUrl = new NSUrl (Build.AppStoreUrl);
                    var askReview = userMood == FeedbackMessage.Mood.Positive && UIApplication.SharedApplication.CanOpenUrl (appStoreUrl);
                    var userMessage = messageTextView.Text;

                    // Reset state before showing alert.
                    userMood = null;
                    messageTextView.Text = String.Empty;
                    RebindMoodButtons ();

                    if (askReview) {
                        var alert = new UIAlertView (
                                        "FeedbackReviewTitle".Tr (),
                                        "FeedbackReviewMessage".Tr (),
                                        null,
                                        "FeedbackReviewCancel".Tr (),
                                        "FeedbackReviewAppStore".Tr ());
                        alert.Clicked += (sender, e) => {
                            if (e.ButtonIndex == 1) {
                                UIPasteboard.General.String = userMessage;
                                UIApplication.SharedApplication.OpenUrl (appStoreUrl);
                            }
                        };
                        alert.Show ();
                    } else {
                        new UIAlertView (
                            "FeedbackSuccessTitle".Tr (),
                            "FeedbackSuccessMessage".Tr (),
                            null,
                            "FeedbackSuccessOk".Tr ()).Show ();
                    }
                } else {
                    new UIAlertView (
                        "FeedbackFailureTitle".Tr (),
                        "FeedbackFailureMessage".Tr (),
                        null,
                        "FeedbackFailureOk".Tr ()).Show ();
                }
            } finally {
                isSending = false;
                View.UserInteractionEnabled = true;
                RebindSendButton ();
            }
        }
    }
}
