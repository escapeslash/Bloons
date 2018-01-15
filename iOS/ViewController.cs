using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using AVFoundation;
using CocosSharp;
using CoreGraphics;
using Foundation;
using Q42.HueApi;
using SatelliteMenu;
using Social;
using UIKit;

namespace Bloons.iOS
{
    public partial class ViewController : UIViewController
    {
        string currentMod;
        Task activityTask;
        GameLayer gameLayer;
        UITableView selectorTableView;
        AVAudioPlayer soundEffectPlayer;
        AVAudioPlayer backgroundMusicPlayer;
        SatelliteMenuButton satelliteMenu;
        PhilipsHue philipsHue = new PhilipsHue();
        NSUserDefaults userDefaults = NSUserDefaults.StandardUserDefaults;

        bool touchesEnded = false;
        bool forceTouchAvailable = false;
        bool ambientAudioWasPlaying = false;

        const string APP_NAME = "Bloons";
        const string APP_LINK = "http://bloons.com";
        const string TWITTER_HANDLE = "@icreatecode";
        const string SOCIAL_NETWORKS_CONNECTED = "SocialNetworksConnected";
        const string SOCIAL_NETWORK_ALERT_SHOWED = "SocialNetworkAlertShowed";
        const string DEVICE_NAME = "DeviceName";
        const string IMAGES_FOLDER = "./Content/Images/";
        const string SOUNDS_FOLDER = "./Content/Sounds/";
        const string SOUND_STATUS_KEY = "SoundStatus";
        const int HUE_BRIDGE_LOCATOR_TIMEOUT = 30;
        const string HUE_APP_KEY_KEY = "HueAppKey";
        const string HUE_NOTIFICATION_SHOWED_KEY = "HueNotificationShowed";
        const string HUE_CONNECTED_BULBS_KEY = "HueConnectedBulbs";
        const string PREVIOUS_BEST_TIME_KEY = "PreviousBestTime";

        public ViewController(IntPtr handle) : base(handle)
        {
        }

        public bool AmbientAudioWasPlaying
        {
            set
            {
                ambientAudioWasPlaying = value;
            }
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            AVAudioSession.Notifications.ObserveInterruption(InterruptAudio);

            if (GameView != null)
            {
                // Set loading event to be called once game view is fully initialised.
                GameView.ViewCreated += LoadGame;
            }
        }

        public override void ViewWillDisappear(bool animated)
        {
            base.ViewWillDisappear(animated);

            if (GameView != null)
            {
                GameView.Paused = true;
            }
        }

        public override void ViewDidAppear(bool animated)
        {
            base.ViewDidAppear(animated);

            if (GameView != null)
            {
                GameView.Paused = false;
            }
        }

        public override void DidReceiveMemoryWarning()
        {
            base.DidReceiveMemoryWarning();
            // Release any cached data, images, etc that aren't in use.
        }

        public override bool PrefersStatusBarHidden()
        {
            return true;
        }

        public override void TraitCollectionDidChange(UITraitCollection previousTraitCollection)
        {
            base.TraitCollectionDidChange(previousTraitCollection);

            // See if the new TraitCollection value includes force touch.
            if (TraitCollection.ForceTouchCapability == UIForceTouchCapability.Available)
            {
                forceTouchAvailable = true;
            }
        }

        public override void TouchesBegan(NSSet touches, UIEvent evt)
        {
            base.TouchesBegan(touches, evt);

            touchesEnded = false;
        }

        public override void TouchesMoved(NSSet touches, UIEvent evt)
        {
            base.TouchesMoved(touches, evt);

            if ((touches.Count > 0) &&
                (forceTouchAvailable == true))
            {
                UITouch uiTouch = touches.AnyObject as UITouch;

                // If force touched on the balloon, show a force meter.
                if ((touchesEnded == false) &&
                    (forceTouchAvailable == true) &&
                    (gameLayer.BalloonLaunched == false) &&
                    (gameLayer.TouchForce > -1) &&
                    (uiTouch.LocationInView(uiTouch.View).X >= (View.Window.Frame.Width / 2 - gameLayer.BalloonSprite.ContentSize.Width / 2 - 10)) &&
                    (uiTouch.LocationInView(uiTouch.View).X <= (View.Window.Frame.Width / 2 + gameLayer.BalloonSprite.ContentSize.Width / 2 + 10)) &&
                    (uiTouch.LocationInView(uiTouch.View).Y >= (View.Window.Frame.Height - gameLayer.BalloonSprite.ContentSize.Width / 2 + 10)) &&
                    (uiTouch.LocationInView(uiTouch.View).Y <= View.Window.Frame.Height))
                {
                    gameLayer.ScaleBalloon((float)uiTouch.Force, (float)uiTouch.MaximumPossibleForce);
                }
            }
        }

        public override void TouchesEnded(NSSet touches, UIEvent evt)
        {
            base.TouchesEnded(touches, evt);

            if (gameLayer.TouchForce <= -1)
            {
                gameLayer.TouchForce = 0;
            }

            touchesEnded = true;
        }

        void LoadGame(object sender, EventArgs e)
        {
            CCGameView gameView = sender as CCGameView;
            List<object> iOSProperties = new List<object>();

            currentMod = (String.IsNullOrWhiteSpace(userDefaults.StringForKey("CurrentMod")) ? "Original" : userDefaults.StringForKey("CurrentMod"));

            iOSProperties.Add(View.Window.Frame.Width);
            iOSProperties.Add(View.Window.Frame.Height);
            iOSProperties.Add(forceTouchAvailable);
            iOSProperties.Add((Action<string>)ControlMenu);
            iOSProperties.Add((Action<bool, string, string, float>)ControlSound);
            iOSProperties.Add((Func<string, string>)userDefaults.StringForKey);
            iOSProperties.Add((Action<string, string>)userDefaults.SetString);
            iOSProperties.Add((Action)ShowBestTimeAlert);

            if (gameView != null)
            {
                CCScene gameScene;
                CCSizeI viewSize = gameView.ViewSize;
                var contentSearchPaths = new List<string>() { "Fonts", "Images/" + currentMod, "Sounds/" + currentMod };

                // Set world dimensions.
                gameView.DesignResolution = new CCSizeI((int)View.Window.Frame.Width, (int)View.Window.Frame.Height);

                // Determine whether to use the high or low def versions of our images
                // Make sure the default texel to content size ratio is set correctly
                // Of course you're free to have a finer set of image resolutions e.g (ld, hd, super-hd)
                if (viewSize.Width < (int)View.Window.Frame.Width)
                {
                    CCSprite.DefaultTexelToContentSizeRatio = 1.0f;
                }
                else
                {
                    CCSprite.DefaultTexelToContentSizeRatio = 2.0f;
                }

                gameView.ContentManager.SearchPaths = contentSearchPaths;
                gameScene = new CCScene(gameView);
                gameLayer = new GameLayer(APP_NAME, iOSProperties, new List<object>());

                // Compensate for a bug with CocosSharp GameScene scaling that leaves a white line at the top.
                gameScene.ScaleY = 1.0001f;

                AddMenu();
                gameScene.AddLayer(gameLayer);
                gameView.RunWithScene(gameScene);
            }
        }

        void AddMenu()
        {
            SatelliteMenuButtonItem[] menuItems;
            string connectedServices = (String.IsNullOrWhiteSpace(userDefaults.StringForKey(SOCIAL_NETWORKS_CONNECTED)) ? string.Empty : userDefaults.StringForKey(SOCIAL_NETWORKS_CONNECTED));
            var menuImage = UIImage.FromFile(IMAGES_FOLDER + currentMod + "/" + "Menu.png");
            var philipsHueImage = UIImage.FromFile(IMAGES_FOLDER + currentMod + "/" + "HueOff.png");
            var soundImage = UIImage.FromFile(IMAGES_FOLDER + currentMod + "/" + "SoundOn.png");
            var facebookImage = UIImage.FromFile(IMAGES_FOLDER + currentMod + "/" + "Facebook" + ((SLComposeViewController.IsAvailable(SLServiceKind.Twitter) == true) &&
                                                                                                  (connectedServices.Contains("Facebook") == true) ?
                                                                                                  "Connected" : "Disconnected") + ".png");
            var twitterImage = UIImage.FromFile(IMAGES_FOLDER + currentMod + "/" + "Twitter" + ((SLComposeViewController.IsAvailable(SLServiceKind.Twitter) == true) &&
                                                                                                  (connectedServices.Contains("Twitter") == true) ?
                                                                                                  "Connected" : "Disconnected") + ".png");
            var menuFrame = new RectangleF(2, (float)View.Window.Frame.Height - 50, 48, 48);

            if (String.IsNullOrWhiteSpace(userDefaults.StringForKey(SOUND_STATUS_KEY)) == false)
            {
                gameLayer.SoundOn = (userDefaults.StringForKey(SOUND_STATUS_KEY) == "On");

                if ((gameLayer.SoundOn == true) &&
                    (ambientAudioWasPlaying == false))
                {
                    soundImage = UIImage.FromFile(IMAGES_FOLDER + currentMod + "/" + "SoundOn.png");
                    AVAudioSession.SharedInstance().SetActive(true, AVAudioSessionSetActiveOptions.NotifyOthersOnDeactivation);
                    AVAudioSession.SharedInstance().SetCategory(AVAudioSessionCategory.SoloAmbient);
                }
                else
                {
                    gameLayer.SoundOn = false;

                    userDefaults.SetString("Off", SOUND_STATUS_KEY);
                    soundImage = UIImage.FromFile(IMAGES_FOLDER + currentMod + "/" + "SoundOff.png");
                    AVAudioSession.SharedInstance().SetActive(false, AVAudioSessionSetActiveOptions.NotifyOthersOnDeactivation);
                    AVAudioSession.SharedInstance().SetCategory(AVAudioSessionCategory.Ambient, AVAudioSessionCategoryOptions.MixWithOthers);
                }
            }

            if (String.IsNullOrWhiteSpace(userDefaults.StringForKey(SOCIAL_NETWORKS_CONNECTED)) == true)
            {
                userDefaults.SetString(connectedServices, SOCIAL_NETWORKS_CONNECTED);
            }

            menuImage = menuImage.Scale(new CGSize(96, 96));
            philipsHueImage = philipsHueImage.Scale(new CGSize(96, 96));
            soundImage = soundImage.Scale(new CGSize(96, 96));
            facebookImage = facebookImage.Scale(new CGSize(96, 96));
            twitterImage = twitterImage.Scale(new CGSize(96, 96));
            menuItems = new[] {
                new SatelliteMenuButtonItem(philipsHueImage, 1, "Philips Hue Off"),
                new SatelliteMenuButtonItem(soundImage, 2, "Sound " + (String.IsNullOrWhiteSpace(userDefaults.StringForKey(SOUND_STATUS_KEY)) ? "On"
                                                                       : userDefaults.StringForKey(SOUND_STATUS_KEY))),
                new SatelliteMenuButtonItem(facebookImage, 3, "Facebook " + ((SLComposeViewController.IsAvailable(SLServiceKind.Twitter) == true) &&
                                                                             (connectedServices.Contains("Facebook") == true) ? "Connected" : "Disconnected")),
                new SatelliteMenuButtonItem(twitterImage, 4, "Twitter " + ((SLComposeViewController.IsAvailable(SLServiceKind.Facebook) == true) &&
                                                                             (connectedServices.Contains("Twitter") == true) ? "Connected" : "Disconnected"))
            };

            satelliteMenu = new SatelliteMenuButton(View, menuImage, menuItems, menuFrame);
            satelliteMenu.Alpha = 0.85f;
            satelliteMenu.CloseItemsOnClick = true;
            satelliteMenu.TouchUpInside += (sender, e) =>
            {
                if (gameLayer.GamePaused == false)
                {
                    gameLayer.GamePaused = true;
                }
                else
                {
                    gameLayer.GamePaused = false;
                }
            };
            satelliteMenu.MenuItemClick += (sender, args) =>
            {
                if (args.MenuItem.Name == "Philips Hue On")
                {
                    ControlMenu("Hide");

                    selectorTableView.Hidden = false;
                }
                else if (args.MenuItem.Name == "Philips Hue Off")
                {
                    ControlMenu("Hide");
                    ChangePhilipsHueStatus();
                }
                else if (args.MenuItem.Name == "Sound On")
                {
                    gameLayer.SoundOn = false;
                    args.MenuItem.Name = "Sound Off";
                    args.MenuItem.ItemImage = UIImage.FromFile(IMAGES_FOLDER + currentMod + "/" + "SoundOff.png");

                    userDefaults.SetString("Off", SOUND_STATUS_KEY);

                    if (ambientAudioWasPlaying == true)
                    {
                        UIAlertController gameAudioAlert = UIAlertController.Create("Game Audio",
                            "Would you like to resume the audio that was playing before you started the game?",
                            UIAlertControllerStyle.Alert);

                        gameAudioAlert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, (connectionOK) =>
                        {
                            AVAudioSession.SharedInstance().SetActive(false, AVAudioSessionSetActiveOptions.NotifyOthersOnDeactivation);
                            AVAudioSession.SharedInstance().SetCategory(AVAudioSessionCategory.Ambient, AVAudioSessionCategoryOptions.MixWithOthers);
                        }));

                        gameAudioAlert.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Default, (connectionCancel) =>
                        {
                            AVAudioSession.SharedInstance().SetCategory(AVAudioSessionCategory.Ambient, AVAudioSessionCategoryOptions.MixWithOthers);
                        }));

                        PresentViewController(gameAudioAlert, true, null);
                    }
                    else
                    {
                        AVAudioSession.SharedInstance().SetCategory(AVAudioSessionCategory.Ambient, AVAudioSessionCategoryOptions.MixWithOthers);
                    }
                }
                else if (args.MenuItem.Name == "Sound Off")
                {
                    gameLayer.SoundOn = true;
                    args.MenuItem.Name = "Sound On";
                    args.MenuItem.ItemImage = UIImage.FromFile(IMAGES_FOLDER + currentMod + "/" + "SoundOn.png");

                    userDefaults.SetString("On", SOUND_STATUS_KEY);
                    AVAudioSession.SharedInstance().SetActive(true, AVAudioSessionSetActiveOptions.NotifyOthersOnDeactivation);
                    AVAudioSession.SharedInstance().SetCategory(AVAudioSessionCategory.SoloAmbient);
                }
                else if (args.MenuItem.Name == "Twitter Disconnected")
                {
                    if (SLComposeViewController.IsAvailable(SLServiceKind.Twitter) == false)
                    {
                        if (UIApplication.SharedApplication.CanOpenUrl(new NSUrl("prefs:root=General&path=TWITTER")) == true)
                        {
                            UIApplication.SharedApplication.OpenUrl(new NSUrl("prefs:root=General&path=TWITTER"));
                        }
                        else
                        {
                            UIApplication.SharedApplication.OpenUrl(new NSUrl("App-Prefs:root=TWITTER"));
                        }
                    }
                    else
                    {
                        args.MenuItem.Name = "Twitter Connected";
                        args.MenuItem.ItemImage = UIImage.FromFile(IMAGES_FOLDER + currentMod + "/" + "TwitterConnected.png");

                        userDefaults.SetString(userDefaults.StringForKey(SOCIAL_NETWORKS_CONNECTED) + ",Twitter", SOCIAL_NETWORKS_CONNECTED);
                    }
                }
                else if (args.MenuItem.Name == "Twitter Connected")
                {
                    args.MenuItem.Name = "Twitter Disconnected";
                    args.MenuItem.ItemImage = UIImage.FromFile(IMAGES_FOLDER + currentMod + "/" + "TwitterDisconnected.png");

                    userDefaults.SetString(userDefaults.StringForKey(SOCIAL_NETWORKS_CONNECTED).Replace("Twitter", string.Empty).Trim(','), SOCIAL_NETWORKS_CONNECTED);
                }
                else if (args.MenuItem.Name == "Facebook Disconnected")
                {
                    if (SLComposeViewController.IsAvailable(SLServiceKind.Twitter) == false)
                    {
                        if (UIApplication.SharedApplication.CanOpenUrl(new NSUrl("prefs:root=General&path=FACEBOOK")) == true)
                        {
                            UIApplication.SharedApplication.OpenUrl(new NSUrl("prefs:root=General&path=FACEBOOK"));
                        }
                        else
                        {
                            UIApplication.SharedApplication.OpenUrl(new NSUrl("App-Prefs:root=FACEBOOK"));
                        }
                    }
                    else
                    {
                        args.MenuItem.Name = "Facebook Connected";
                        args.MenuItem.ItemImage = UIImage.FromFile(IMAGES_FOLDER + currentMod + "/" + "FacebookConnected.png");

                        userDefaults.SetString(userDefaults.StringForKey(SOCIAL_NETWORKS_CONNECTED) + ",Facebook", SOCIAL_NETWORKS_CONNECTED);
                    }
                }
                else if (args.MenuItem.Name == "Facebook Connected")
                {
                    args.MenuItem.Name = "Facebook Disconnected";
                    args.MenuItem.ItemImage = UIImage.FromFile(IMAGES_FOLDER + currentMod + "/" + "FacebookDisconnected.png");

                    userDefaults.SetString(userDefaults.StringForKey(SOCIAL_NETWORKS_CONNECTED).Replace("Facebook", string.Empty).Trim(','), SOCIAL_NETWORKS_CONNECTED);
                }
            };

            View.AddSubview(satelliteMenu);
        }

        public void ControlMenu(string menuState)
        {
            if (menuState == "Show")
            {
                UIView.Animate(1, () =>
                {
                    satelliteMenu.Alpha = 0.85f;
                });
            }
            else if (menuState == "Collapse")
            {
                satelliteMenu.Collapse();
            }
            else if (menuState == "Hide")
            {
                satelliteMenu.Collapse();

                UIView.Animate(1, () =>
                {
                    satelliteMenu.Alpha = 0;
                });
            }
        }

        void InterruptAudio(object sender, AVAudioSessionInterruptionEventArgs e)
        {
            if (e.InterruptionType == AVAudioSessionInterruptionType.Began)
            {
                AVAudioSession.SharedInstance().SetActive(false, AVAudioSessionSetActiveOptions.NotifyOthersOnDeactivation);
            }
            else
            {
                AVAudioSession.SharedInstance().SetActive(true, AVAudioSessionSetActiveOptions.NotifyOthersOnDeactivation);
            }
        }

        public void ControlSound(bool controlBackgroundMusic = true, string fileName = "", string controlType = "Play",
                                 float volumeLevel = 0.5f)
        {
            if ((controlBackgroundMusic == true) &&
                (gameLayer.SoundOn == true))
            {
                ControlBackgroundMusic(controlType, volumeLevel);
            }
            else if (controlBackgroundMusic == false)
            {
                PlaySoundEffect(fileName, volumeLevel);
            }
        }

        void ControlBackgroundMusic(string controlType, float volumeLevel)
        {
            if (((backgroundMusicPlayer == null) ||
                 (backgroundMusicPlayer.Playing == false)) &&
                (controlType == "Play"))
            {
                backgroundMusicPlayer = AVAudioPlayer.FromUrl(NSUrl.FromFilename(GetRandomBackgroundMusic(backgroundMusicPlayer == null ? string.Empty :
                                                                                                          "./" + backgroundMusicPlayer.Url.RelativePath)));
                backgroundMusicPlayer.NumberOfLoops = -1;
                backgroundMusicPlayer.Volume = volumeLevel;

                backgroundMusicPlayer.Play();
            }
            else if (backgroundMusicPlayer != null)
            {
                if (controlType == "Stop")
                {
                    backgroundMusicPlayer.Stop();
                }
                else if (controlType == "Pause")
                {
                    backgroundMusicPlayer.Pause();
                }
                else if (controlType == "Resume")
                {
                    backgroundMusicPlayer.Play();
                }
            }
        }

        void PlaySoundEffect(string fileName, float volumeLevel)
        {
            soundEffectPlayer = AVAudioPlayer.FromUrl(NSUrl.FromFilename(SOUNDS_FOLDER + currentMod + "/" + fileName));
            soundEffectPlayer.NumberOfLoops = 0;
            soundEffectPlayer.Volume = volumeLevel;

            soundEffectPlayer.Play();
        }

        string GetRandomBackgroundMusic(string currentFile)
        {
            Random randomNumber = new Random();
            List<string> backgroundMusic = new List<string>();

            foreach (string imageFile in Directory.GetFiles(SOUNDS_FOLDER + currentMod, "BackgroundMusic*"))
            {
                if (imageFile != currentFile)
                {
                    backgroundMusic.Add(imageFile);
                }
            }

            return backgroundMusic[randomNumber.Next(0, backgroundMusic.Count)];
        }

        void ChangePhilipsHueButton(string philipsHueStatus)
        {
            satelliteMenu.Items[0].Name = "Philips Hue " + philipsHueStatus;
            satelliteMenu.Items[0].ItemImage = UIImage.FromFile(IMAGES_FOLDER + currentMod + "/" + "Hue" + philipsHueStatus + ".png");
        }

        void ShowActivityIndicator(bool startIndicator)
        {
            CCSprite activityIndicatorSprite;

            if (gameLayer.GetChildByTag(1) == null)
            {
                activityIndicatorSprite = new CCSprite("ActivityIndicator");
                activityIndicatorSprite.Tag = 1;
                activityIndicatorSprite.Opacity = 0;
                activityIndicatorSprite.Name = "ActivityIndicator";
                activityIndicatorSprite.PositionX = 27;
                activityIndicatorSprite.PositionY = 25;
                activityIndicatorSprite.ContentSize = new CCSize(48, 48);

                gameLayer.AddChild(activityIndicatorSprite);
            }
            else
            {
                activityIndicatorSprite = (CCSprite)gameLayer.GetChildByTag(1);
            }

            if (startIndicator == true)
            {
                activityTask = new Task((taskObject) =>
                {
                    CCSprite activityIndicatorTaskSprite = (CCSprite)taskObject;

                    activityIndicatorTaskSprite.RunActions(new CCFadeIn(1),
                                                           new CCRotateBy(60, 360 * 12));
                }, activityIndicatorSprite);

                ControlMenu("Hide");
                activityTask.Start();
            }
            else
            {
                activityTask.Dispose();
                activityIndicatorSprite.RunAction(new CCFadeOut(1));
                gameLayer.RemoveChild(activityIndicatorSprite);
                ControlMenu("Show");
            }
        }

        void ShowSelector(string selectorTitle, string selectorSubtitle, UITableViewSource tableViewSource, string footerButtonTitle = "",
                          Action doneAction = null, Action footerButtonAction = null)
        {
            try
            {
                UILabel selectorTitleLabel = new UILabel();
                UILabel selectorSubtitleLabel = new UILabel();
                UIButton doneButton = new UIButton(UIButtonType.System);
                UIButton footerButton = new UIButton(UIButtonType.System);
                UITableViewHeaderFooterView tableHeader = new UITableViewHeaderFooterView();
                UITableViewHeaderFooterView tableFooter = new UITableViewHeaderFooterView();

                if (selectorTableView != null)
                {
                    selectorTableView.RemoveFromSuperview();
                    selectorTableView.Dispose();
                }

                tableHeader.Frame = new CGRect(20, 0, View.Window.Frame.Width - 20, 50);
                tableFooter.Frame = new CGRect(0, 0, View.Window.Frame.Width - 20, 50);

                selectorTitleLabel.Text = selectorTitle;
                selectorTitleLabel.TextColor = UIColor.Black;
                selectorTitleLabel.AdjustsFontSizeToFitWidth = true;
                selectorTitleLabel.Frame = new CGRect(15, -5, tableHeader.Frame.Width / 2, 50);

                selectorSubtitleLabel.TextColor = UIColor.LightGray;
                selectorSubtitleLabel.Text = selectorSubtitle;
                selectorSubtitleLabel.Frame = new CGRect(15, 10, tableHeader.Frame.Width / 2, 50);
                selectorSubtitleLabel.Font = UIFont.SystemFontOfSize(10);

                doneButton.SetTitle("Done", UIControlState.Normal);
                doneButton.Font.WithSize(12);
                footerButton.SetTitle(footerButtonTitle, UIControlState.Normal);
                footerButton.Font.WithSize(12);

                doneButton.TouchUpInside += (sender, e) =>
                {
                    selectorTableView.Hidden = true;

                    doneAction?.Invoke();
                    ControlMenu("Show");

                    gameLayer.GamePaused = false;
                };
                doneButton.Frame = new CGRect(tableHeader.Frame.Width - doneButton.Frame.Width - 58, 10, 50, 30);
                footerButton.TouchUpInside += (sender, e) =>
                {
                    selectorTableView.Hidden = true;

                    footerButtonAction?.Invoke();
                    ControlMenu("Show");

                    gameLayer.GamePaused = false;
                };
                footerButton.Frame = new CGRect(3, 20, 100, 20);

                tableHeader.Add(selectorTitleLabel);
                tableHeader.Add(selectorSubtitleLabel);
                tableHeader.Add(doneButton);
                tableFooter.Add(footerButton);

                selectorTableView = new UITableView();
                selectorTableView.Layer.BorderColor = new CGColor(0.7f, 0.7f, 0.7f, 0.8f);
                selectorTableView.Layer.BorderWidth = 1;
                selectorTableView.Layer.CornerRadius = 8;
                selectorTableView.BackgroundColor = new UIColor(1, 1, 1, 0.8f);
                selectorTableView.TableHeaderView = tableHeader;
                selectorTableView.TableFooterView = tableFooter;
                selectorTableView.SeparatorStyle = UITableViewCellSeparatorStyle.None;
                selectorTableView.Frame = new CGRect(0, 0, View.Window.Frame.Width - 20, (selectorTableView.ContentSize.Height + 50 < 270 ?
                                                                                             selectorTableView.ContentSize.Height + 50 : 270));
                selectorTableView.Center = new CGPoint(View.Window.Frame.Width / 2, View.Window.Frame.Height / 2);
                selectorTableView.Source = tableViewSource;

                if (philipsHue.ColorBulbs.Count > 5)
                {
                    selectorTableView.ScrollEnabled = true;
                    selectorTableView.ScrollsToTop = true;
                    selectorTableView.ShowsVerticalScrollIndicator = true;
                    selectorTableView.ShowsHorizontalScrollIndicator = false;

                    selectorTableView.FlashScrollIndicators();
                }

                Add(selectorTableView);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        void ChangePhilipsHueStatus()
        {
            if ((philipsHue == null) ||
                ((philipsHue != null) &&
                 (philipsHue.IsConnected == false)))
            {
                string deviceName = string.Empty;
                string hueAppKey = string.Empty;

                gameLayer.GamePaused = true;

                if (String.IsNullOrWhiteSpace(userDefaults.StringForKey(DEVICE_NAME)) == false)
                {
                    deviceName = userDefaults.StringForKey(DEVICE_NAME);
                }

                if (String.IsNullOrWhiteSpace(userDefaults.StringForKey(HUE_APP_KEY_KEY)) == false)
                {
                    hueAppKey = userDefaults.StringForKey(HUE_APP_KEY_KEY);
                }

                if (String.IsNullOrWhiteSpace(userDefaults.StringForKey(HUE_NOTIFICATION_SHOWED_KEY)) == true)
                {
                    UIAlertController philipsHueConnectionAlert = UIAlertController.Create("Philips Hue Connection",
                        "Connecting the app to Philips Hue color bulbs adds an extra dimension to the gameplay. If you own one, " +
                        "press the link button on your Philips Hue bridge within " + HUE_BRIDGE_LOCATOR_TIMEOUT.ToString() + " seconds to check it out!",
                        UIAlertControllerStyle.Alert);

                    philipsHueConnectionAlert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, (connectionOK) =>
                    {
                        philipsHue = new PhilipsHue(APP_NAME, deviceName, hueAppKey, HUE_BRIDGE_LOCATOR_TIMEOUT);

                        ShowActivityIndicator(true);
                        philipsHue.Connect(PhilipsHueConnectionAttemptComplete);
                        philipsHueConnectionAlert.DismissViewController(true, null);
                    }));

                    philipsHueConnectionAlert.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Default, (connectionCancel) =>
                    {
                        philipsHueConnectionAlert.DismissViewController(true, null);
                        ControlMenu("Show");

                        gameLayer.GamePaused = false;
                    }));

                    PresentViewController(philipsHueConnectionAlert, true, null);
                }
                else
                {
                    philipsHue = new PhilipsHue(APP_NAME, deviceName, hueAppKey, HUE_BRIDGE_LOCATOR_TIMEOUT);

                    ShowActivityIndicator(true);
                    philipsHue.Connect(PhilipsHueConnectionAttemptComplete);
                }
            }
            else if ((philipsHue != null) &&
                     (philipsHue.IsConnected == true))
            {
                philipsHue.Disconnect();
                ChangePhilipsHueButton("Off");
                ControlMenu("Show");
            }
        }

        void PhilipsHueConnectionAttemptComplete()
        {
            try
            {
                if (philipsHue.IsConnected == true)
                {
                    InvokeOnMainThread(() =>
                    {
                        userDefaults.SetString(philipsHue.DeviceName, DEVICE_NAME);
                        userDefaults.SetString(philipsHue.HueAppKey, HUE_APP_KEY_KEY);
                        userDefaults.SetString("true", HUE_NOTIFICATION_SHOWED_KEY);

                        gameLayer.PhilipsHue = philipsHue;

                        ShowActivityIndicator(false);

                        if (philipsHue.ColorBulbs.Count > 0)
                        {
                            Action doneAction;
                            Action disconnectAction;
                            List<Light> connectedBulbs = new List<Light>();
                            SelectorDataSource selectorDataSource;

                            if (String.IsNullOrWhiteSpace(userDefaults.StringForKey(HUE_CONNECTED_BULBS_KEY)) == false)
                            {
                                string[] connectedBulbsSetting = userDefaults.StringForKey(HUE_CONNECTED_BULBS_KEY).Split('|');

                                for (int i = 0; i < connectedBulbsSetting.Length; i++)
                                {
                                    Light connectedBulb = new Light();

                                    connectedBulb.Id = connectedBulbsSetting[i].Split('=')[0];
                                    connectedBulb.Name = connectedBulbsSetting[i].Split('=')[1];

                                    connectedBulbs.Add(connectedBulb);
                                }
                            }

                            selectorDataSource = new SelectorDataSource(new List<object>(philipsHue.ColorBulbs), "Id", "Name", new List<object>(connectedBulbs));
                            doneAction = new Action(() =>
                            {
                                if (selectorDataSource.SelectedOptions.Count == 0)
                                {
                                    userDefaults.SetString(string.Empty, HUE_CONNECTED_BULBS_KEY);
                                    ChangePhilipsHueButton("Off");
                                }
                                else
                                {
                                    string connectedBulbsSetting = string.Empty;

                                    for (int i = 0; i < selectorDataSource.SelectedOptions.Count; i++)
                                    {
                                        connectedBulbsSetting += selectorDataSource.SelectedOptions.Cast<Light>().ToList()[i].Id + "=" +
                                                                                          selectorDataSource.SelectedOptions.Cast<Light>().ToList()[i].Name + "|";
                                    }

                                    connectedBulbsSetting = connectedBulbsSetting.Trim('|');

                                    userDefaults.SetString(connectedBulbsSetting, HUE_CONNECTED_BULBS_KEY);
                                    ChangePhilipsHueButton("On");
                                }
                            });
                            disconnectAction = new Action(() =>
                            {
                                philipsHue.Disconnect();
                                ChangePhilipsHueButton("Off");
                            });

                            ShowSelector("Philips Hue Bulbs", "Select bulb(s) the game will control.", selectorDataSource, "Disconnect", doneAction, disconnectAction);
                        }
                        else
                        {
                            UIAlertView colorBulbCountAlert = new UIAlertView()
                            {
                                Title = "Philips Hue Connection",
                                Message = "The app was not able to locate any color bulbs on your network. Please add one or more and try again."
                            };

                            colorBulbCountAlert.Dismissed += (sender, e) =>
                            {
                                ControlMenu("Show");

                                gameLayer.GamePaused = false;
                            };

                            colorBulbCountAlert.AddButton("OK");
                            colorBulbCountAlert.Show();
                            philipsHue.Disconnect();
                        }
                    });
                }
                else
                {
                    InvokeOnMainThread(() =>
                    {
                        UIAlertView connectionFailedAlert = new UIAlertView()
                        {
                            Title = "Philips Hue Connection",
                            Message = "The app was not able to locate a Philips Hue bridge on your network. Please troubleshoot and try again "
                                + "(cycling power to the bridge sometimes helps)."
                        };

                        connectionFailedAlert.Dismissed += (sender, e) =>
                        {
                            ControlMenu("Show");

                            gameLayer.GamePaused = false;
                        };

                        connectionFailedAlert.AddButton("OK");
                        connectionFailedAlert.Show();

                        ShowActivityIndicator(false);
                        ChangePhilipsHueButton("Off");
                    });
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        void ShowBestTimeAlert()
        {
            string currentBestTime = userDefaults.StringForKey(PREVIOUS_BEST_TIME_KEY);
            UIAlertView bestTimeAlert = new UIAlertView()
            {
                Title = "New Achievement!",
                Message = "Congratulations, you've set a new personal best record of " + (currentBestTime.Split(':')[0] == "00" ? string.Empty : int.Parse(currentBestTime.Split(':')[0]) + " hrs ")
                    + (currentBestTime.Split(':')[0] == "00" && currentBestTime.Split(':')[1] == "00" ? string.Empty : int.Parse(currentBestTime.Split(':')[1]) + " mins and ")
                    + int.Parse(currentBestTime.Split(':')[2]) + " secs!" + ((String.IsNullOrWhiteSpace(userDefaults.StringForKey(SOCIAL_NETWORK_ALERT_SHOWED)) == true) &&
                                                                  (String.IsNullOrWhiteSpace(userDefaults.StringForKey(SOCIAL_NETWORKS_CONNECTED)) == true) ?
                                                                  " Connect to a social network from the menu so you can humblebrag about your gaming prowess!" : string.Empty)
            };

            bestTimeAlert.Dismissed += (sender, e) =>
            {
                ComposeSocialMessage();
            };

            bestTimeAlert.AddButton("OK");
            bestTimeAlert.Show();
        }

        void ComposeSocialMessage()
        {
            string socialMessage = string.Empty;
            string currentBestTime = userDefaults.StringForKey(PREVIOUS_BEST_TIME_KEY);
            string connectedServices = (String.IsNullOrWhiteSpace(userDefaults.StringForKey(SOCIAL_NETWORKS_CONNECTED)) == true ?
                                        string.Empty : userDefaults.StringForKey(SOCIAL_NETWORKS_CONNECTED));

            if (String.IsNullOrWhiteSpace(currentBestTime) == true)
            {
                socialMessage = "Got #" + APP_NAME + "?";
            }
            else
            {
                socialMessage = "I survived for " + (currentBestTime.Split(':')[0] == "00" ? string.Empty : int.Parse(currentBestTime.Split(':')[0]) + " hrs ")
                    + (currentBestTime.Split(':')[0] == "00" && currentBestTime.Split(':')[1] == "00" ? string.Empty : int.Parse(currentBestTime.Split(':')[1]) + " mins and ")
                    + int.Parse(currentBestTime.Split(':')[2]) + " secs. Enough said. Booyah. Mic drop. All that. #" + APP_NAME;
            }

            if ((connectedServices.Contains("Twitter") == true) &&
                (SLComposeViewController.IsAvailable(SLServiceKind.Twitter) == true))
            {
                SLComposeViewController twitterViewController = SLComposeViewController.FromService(SLServiceKind.Twitter);

                twitterViewController.SetInitialText(socialMessage);
                twitterViewController.AddUrl(new NSUrl(APP_LINK));

                twitterViewController.CompletionHandler += (twitterobj) =>
                {
                    InvokeOnMainThread(() =>
                    {
                        DismissViewController(true, null);

                        if ((connectedServices.Contains("Facebook") == true) &&
                            (SLComposeViewController.IsAvailable(SLServiceKind.Facebook) == true))
                        {
                            SLComposeViewController facebookViewController = SLComposeViewController.FromService(SLServiceKind.Facebook);

                            facebookViewController.SetInitialText(socialMessage);
                            facebookViewController.AddUrl(new NSUrl(APP_LINK));

                            facebookViewController.CompletionHandler += (facebookobj) =>
                            {
                                InvokeOnMainThread(() =>
                                {
                                    DismissViewController(true, null);
                                    ControlMenu("Show");
                                });
                            };

                            PresentViewController(facebookViewController, true, null);
                        }
                        else
                        {
                            ControlMenu("Show");
                        }
                    });
                };

                PresentViewController(twitterViewController, true, null);
            }
            else if ((connectedServices.Contains("Facebook") == true) &&
                     (SLComposeViewController.IsAvailable(SLServiceKind.Facebook) == true))
            {
                SLComposeViewController facebookViewController = SLComposeViewController.FromService(SLServiceKind.Facebook);

                facebookViewController.SetInitialText(socialMessage);
                facebookViewController.AddUrl(new NSUrl(APP_LINK));

                facebookViewController.CompletionHandler += (facebookobj) =>
                {
                    InvokeOnMainThread(() =>
                    {
                        DismissViewController(true, null);
                        ControlMenu("Show");
                    });
                };

                PresentViewController(facebookViewController, true, null);
            }
            else
            {
                ControlMenu("Show");
            }
        }
    }
}