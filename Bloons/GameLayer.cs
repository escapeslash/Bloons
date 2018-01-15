using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using CocosSharp;

namespace Bloons
{
    public class GameLayer : CCLayerColor
    {
        int layerWidth = 0;
        int layerHeight = 0;
        string appName = string.Empty;
        string errorLog = string.Empty;
        bool forceTouchAvailable = false;
        string environmentType = string.Empty;
        PhilipsHue philipsHue = new PhilipsHue();

        float frameTime = 0;
        float windSpeed = 0;
        float previousWindSpeed = 0;
        float touchForce = 0;
        bool soundOn = true;
        bool gamePaused = false;
        bool balloonMoved = false;
        bool balloonLaunched = false;
        bool windSpeedChanged = false;
        bool windBlowingRight = true;
        float balloonXVelocity = 0;
        float balloonYVelocity = 0;
        float newBalloonYVelocity = 0;
        string balloonColor = "255, 255, 255";
        CCPoint startSwipeLocation;
        CCPoint lastTouchLocation;
        Random randomNumber = new Random();
        DateTime lastPlayTime = DateTime.Now;
        DateTime startSwipeTime = DateTime.Now;
        DateTime lastGameVariationTime = DateTime.Now;
        TimeSpan previousTimeSpan = new TimeSpan();

        Action<string> ControlMenu;
        Action<bool, string, string, float> ControlSound;
        Func<string, string> GetDefault;
        Action<string, string> SetDefault;
        Action ShowBestTimeAlert;
        CCLabel dataLabel;
        CCSprite sunSprite;
        CCSprite boatSprite;
        CCSprite oceanSprite;
        CCSprite balloonSprite;
        CCSprite explosionSprite;
        CCSprite stopwatchSprite;
        CCSprite previousBestTimeSprite;
        CCParallaxNode cloudsParallax;
        CCRepeatForever boatAnimation;

        const float GAME_GRAVITY = 0.000050f;
        const int REMINDER_ALERT_DURATION = 30;
        const int GAME_VARIATION_FREQUENCY = 30;
        const int INVERSE_FORCETOUCH_LAUNCH_VELOCITY_CONSTANT = 25;
        const float BACKGROUND_MUSIC_VOLUME = 0.5f;
        const float SOUND_EFFECT_VOLUME = 1f;
        const string BALLOON_TAP_SOUND_FILE = "BalloonTap.mp3";
        const string BALLOON_POP_SOUND_FILE = "BalloonPop.mp3";
        const string PREVIOUS_BEST_TIME_KEY = "PreviousBestTime";

        public GameLayer(string appName, List<object> iOSProperties, List<object> androidProperties) : base(new CCColor4B(238, 247, 252, 255))
        {
            this.appName = appName;

            if (iOSProperties.Count > 0)
            {
                environmentType = "iOS";
                layerWidth = int.Parse(iOSProperties[0].ToString());
                layerHeight = int.Parse(iOSProperties[1].ToString());
                forceTouchAvailable = bool.Parse(iOSProperties[2].ToString());
                ControlMenu = (Action<string>)iOSProperties[3];
                ControlSound = (Action<bool, string, string, float>)iOSProperties[4];
                GetDefault = (Func<string, string>)iOSProperties[5];
                SetDefault = (Action<string, string>)iOSProperties[6];
                ShowBestTimeAlert = (Action)iOSProperties[7];
            }
            else if (androidProperties.Count > 0)
            {
                environmentType = "Android";
            }

            Schedule(RunGameLogic);
        }

        public CCSprite SunSprite
        {
            get
            {
                return sunSprite;
            }
        }

        public CCSprite BalloonSprite
        {
            get
            {
                return balloonSprite;
            }
        }

        public bool GamePaused
        {
            get
            {
                return gamePaused;
            }
            set
            {
                gamePaused = value;
            }
        }

        public bool BalloonLaunched
        {
            get
            {
                return balloonLaunched;
            }
        }

        public bool SoundOn
        {
            get
            {
                return soundOn;
            }
            set
            {
                soundOn = value;
            }
        }

        public float TouchForce
        {
            get
            {
                return touchForce;
            }
            set
            {
                if (value >= 1)
                {
                    touchForce = (float)value;
                }
                else
                {
                    touchForce = 0;
                }
            }
        }

        public PhilipsHue PhilipsHue
        {
            set
            {
                philipsHue = value;
            }
        }

        public string ErrorLog
        {
            get
            {
                return errorLog;
            }
        }

        protected override void AddedToScene()
        {
            base.AddedToScene();

            InitializeLayer();
            AddTouchEvents();
        }

        void InitializeLayer()
        {
            ZOrder = 0;

            AddExplosionNode();
            AddStopwatch();
            AddSun();
            AddClouds();
            AddOceanscape();
            AddBalloon();
        }

        void AddBalloon()
        {
            string previousBestTime = GetDefault(PREVIOUS_BEST_TIME_KEY);
            string currentTime = ((CCLabel)stopwatchSprite.GetChildByTag(1)).Text;
            TimeSpan currentTimeSpan = new TimeSpan(int.Parse(currentTime.Split(':')[0]), int.Parse(currentTime.Split(':')[1]), int.Parse(currentTime.Split(':')[2]));

            previousTimeSpan = (String.IsNullOrWhiteSpace(previousBestTime) ? new TimeSpan(0, 0, 0) : new TimeSpan(int.Parse(previousBestTime.Split(':')[0]),
                                                                                                                        int.Parse(previousBestTime.Split(':')[1]),
                                                                                                                        int.Parse(previousBestTime.Split(':')[2])));

            GetRandomBalloonColor();

            balloonSprite = new CCSprite("Balloon:" + balloonColor);
            balloonSprite.Name = "Balloon:" + balloonColor;
            balloonSprite.PositionX = layerWidth / 2 + 3;
            balloonSprite.PositionY = 0;
            balloonSprite.Opacity = 200;

            balloonMoved = false;
            balloonLaunched = false;
            balloonXVelocity = 0;
            balloonYVelocity = 0;
            newBalloonYVelocity = 0;
            lastPlayTime = DateTime.Now;

            if ((currentTimeSpan.TotalSeconds - previousTimeSpan.TotalSeconds) > 0)
            {
                CCEaseIn shakeIn;
                CCEaseOut shakeOut;
                CCEaseIn shakeBackIn;
                CCEaseOut shakeBackOut;
                CCCallFunc resetFunction;
                CCCallFunc bestTimeAlertFunction;
                CCSequence motionSequence;

                shakeIn = new CCEaseIn(new CCMoveBy(0.05f, new CCPoint(15, 0)), 1f);
                shakeOut = new CCEaseOut(new CCMoveBy(0.05f, new CCPoint(-30, 0)), 1f);
                resetFunction = new CCCallFunc(() => ((CCLabel)previousBestTimeSprite.GetChildByTag(1)).Text = currentTime);
                shakeBackIn = new CCEaseIn(new CCMoveBy(0.1f, new CCPoint(30, 0)), 1f);
                shakeBackOut = new CCEaseOut(new CCMoveBy(0.1f, new CCPoint(-15, 0)), 1f);
                bestTimeAlertFunction = new CCCallFunc(() => ShowBestTimeAlert());
                motionSequence = new CCSequence(shakeIn, shakeOut, resetFunction, shakeBackIn, shakeBackOut, bestTimeAlertFunction);
                previousTimeSpan = currentTimeSpan;

                previousBestTimeSprite.RunAction(motionSequence);
                SetDefault(((CCLabel)stopwatchSprite.GetChildByTag(1)).Text, PREVIOUS_BEST_TIME_KEY);
            }
            else
            {
                ControlMenu("Show");
            }

            AddChild(balloonSprite);
            balloonSprite.RunActions(new CCScaleTo(0.10f, 1.5f), new CCScaleTo(0.10f, 1f));
        }

        void RemoveBalloon()
        {
            RemoveChild(balloonSprite);
        }

        void AddSun()
        {
            CCParticleSun sunParticle;

            sunSprite = new CCSprite("Sun");
            sunSprite.PositionX = 0;
            sunSprite.PositionY = layerHeight;

            sunParticle = new CCParticleSun(CCPoint.Zero, CCEmitterMode.Gravity);
            sunParticle.StartColor = new CCColor4F(CCColor4B.Red);
            sunParticle.EndColor = new CCColor4F(CCColor4B.Yellow);
            sunParticle.Position = new CCPoint(5, layerHeight - 5);

            AddChild(sunSprite);
            AddChild(sunParticle);
        }

        void AddClouds()
        {
            float yRatio1 = 1.0f;
            float yRatio2 = 0.15f;
            float yRatio3 = 0.5f;
            var cloudOne = new CCSprite("Cloud");
            var cloudTwo = new CCSprite("Cloud");
            var cloudThree = new CCSprite("Cloud");

            cloudsParallax = new CCParallaxNode
            {
                Position = new CCPoint(0, layerHeight)
            };

            cloudOne.Scale = 0.5f;
            cloudTwo.Scale = 0.5f;
            cloudThree.Scale = 0.5f;

            AddChild(cloudsParallax);
            cloudsParallax.AddChild(cloudOne, 0, new CCPoint(1.0f, yRatio1), new CCPoint(100, -100 + layerHeight - (layerHeight * yRatio1)));
            cloudsParallax.AddChild(cloudTwo, 0, new CCPoint(1.0f, yRatio2), new CCPoint(325, -200 + layerHeight - (layerHeight * yRatio2)));
            cloudsParallax.AddChild(cloudThree, 0, new CCPoint(1.0f, yRatio3), new CCPoint(170, -150 + layerHeight - (layerHeight * yRatio3)));
        }

        void AddOceanscape()
        {
            float oceanScaleFactor = 0;
            float beachScaleFactor = 0;
            CCSprite beachSprite = new CCSprite("Beach");
            CCMoveBy moveUpBy = new CCMoveBy(3, new CCPoint(0, 10));
            CCMoveBy moveDownBy = new CCMoveBy(3, new CCPoint(0, -10));
            CCSequence oceanSequence = new CCSequence(moveUpBy, moveDownBy);
            CCRepeatForever oceanAnimation = new CCRepeatForever(oceanSequence);
            CCCallFunc boatAnimationFunction;

            oceanSprite = new CCSprite("Ocean");
            oceanScaleFactor = layerWidth / oceanSprite.ContentSize.Width;
            beachScaleFactor = layerWidth / beachSprite.ContentSize.Width;

            oceanSprite.ScaleX = oceanScaleFactor + 0.02f;
            oceanSprite.ScaleY = oceanScaleFactor;
            oceanSprite.PositionX = oceanSprite.ContentSize.Center.X * oceanScaleFactor;
            oceanSprite.PositionY = oceanSprite.ContentSize.Center.Y * oceanScaleFactor + beachSprite.ContentSize.Center.Y * oceanScaleFactor - 40;
            oceanSprite.UserData = oceanSprite.PositionY;

            beachSprite.ScaleX = beachScaleFactor + 0.02f;
            beachSprite.ScaleY = beachScaleFactor;
            beachSprite.PositionX = beachSprite.ContentSize.Center.X * beachScaleFactor;
            beachSprite.PositionY = beachSprite.ContentSize.Center.Y * beachScaleFactor;

            boatSprite = new CCSprite("Boat");
            boatSprite.Position = new CCPoint(100, oceanSprite.PositionY + boatSprite.ContentSize.Height / 2);

            oceanAnimation = new CCRepeatForever(oceanSequence);
            boatAnimationFunction = new CCCallFunc(() => boatSprite.PositionY = oceanSprite.PositionY + boatSprite.ContentSize.Height / 2 - 3);
            boatAnimation = new CCRepeatForever(boatAnimationFunction);

            AddChild(boatSprite);
            AddChild(oceanSprite);
            AddChild(beachSprite);
            boatSprite.RunAction(boatAnimation);
            oceanSprite.RunAction(oceanAnimation);
        }

        void AddExplosionNode()
        {
            explosionSprite = new CCSprite("ExplosionSprite");
            explosionSprite.Visible = false;

            AddChild(explosionSprite);
        }

        void AddStopwatch()
        {
            CCLabel stopwatchLabel;
            CCLabel previousBestTimeLabel;

            stopwatchSprite = new CCSprite("StopwatchBackground");
            stopwatchSprite.Color = CCColor3B.Gray;
            stopwatchSprite.PositionX = layerWidth - 44;
            stopwatchSprite.PositionY = layerHeight - 18;
            stopwatchSprite.ContentSize = new CCSize(80, 30);
            stopwatchSprite.UserData = 0;
            previousBestTimeSprite = new CCSprite("StopwatchBackground");
            previousBestTimeSprite.Color = CCColor3B.White;
            previousBestTimeSprite.PositionX = layerWidth - 44;
            previousBestTimeSprite.PositionY = layerHeight - 51;
            previousBestTimeSprite.ContentSize = new CCSize(80, 30);
            stopwatchLabel = new CCLabel(string.Empty, "JosefinSans.ttf", 30, CCLabelFormat.SystemFont);
            stopwatchLabel.Tag = 1;
            stopwatchLabel.Text = "00:00:00";
            stopwatchLabel.Color = CCColor3B.White;
            stopwatchLabel.Position = new CCPoint(stopwatchSprite.ContentSize.Width / 2, stopwatchSprite.ContentSize.Height / 2);
            previousBestTimeLabel = new CCLabel(string.Empty, "JosefinSans.ttf", 30, CCLabelFormat.SystemFont);
            previousBestTimeLabel.Tag = 1;
            previousBestTimeLabel.Text = (String.IsNullOrWhiteSpace(GetDefault(PREVIOUS_BEST_TIME_KEY)) == true ? "00:00:00" : GetDefault(PREVIOUS_BEST_TIME_KEY));
            previousBestTimeLabel.Color = CCColor3B.Gray;
            previousBestTimeLabel.Position = new CCPoint(stopwatchSprite.ContentSize.Width / 2, stopwatchSprite.ContentSize.Height / 2);

            stopwatchSprite.AddChild(stopwatchLabel);
            previousBestTimeSprite.AddChild(previousBestTimeLabel);
            AddChild(stopwatchSprite);
            AddChild(previousBestTimeSprite);
        }

        void PlayBackgroundMusic()
        {
            if (soundOn == true)
            {
                ControlSound(true, string.Empty, "Play", BACKGROUND_MUSIC_VOLUME);
            }
        }

        void ResumeBackgroundMusic()
        {
            if (soundOn == true)
            {
                ControlSound(true, string.Empty, "Resume", BACKGROUND_MUSIC_VOLUME);
            }
        }

        void StopBackgroundMusic()
        {
            if (soundOn == true)
            {
                ControlSound(true, string.Empty, "Stop", BACKGROUND_MUSIC_VOLUME);
            }
        }

        public void ToggleDebugData()
        {
            if (dataLabel == null)
            {
                dataLabel = new CCLabel(string.Empty, "JosefinSans.ttf", 30, CCLabelFormat.SystemFont);
                dataLabel.Color = CCColor3B.Magenta;
                dataLabel.PositionX = layerWidth - 5;
                dataLabel.PositionY = layerHeight - 120;
                dataLabel.AnchorPoint = CCPoint.AnchorMiddleRight;

                AddChild(dataLabel);
            }
            else
            {
                RemoveChild(dataLabel, true);

                dataLabel = null;
            }
        }

        void MoveBoat(bool ignoreWindspeedChange)
        {
            if ((windSpeedChanged == true) ||
                (ignoreWindspeedChange == true))
            {
                CCRepeatForever boatMovingAnimation;
                CCSequence boatMovingSequence;

                if (windSpeed > 0)
                {
                    float targetPositionX = (windBlowingRight == true ? layerWidth + boatSprite.ContentSize.Width : -boatSprite.ContentSize.Width);
                    CCMoveTo moveAcrossTo = new CCMoveTo(windSpeed * 1.4f, new CCPoint(targetPositionX, boatSprite.PositionY));
                    CCCallFunc resetPositionX = new CCCallFunc(() => boatSprite.PositionX = (windBlowingRight == true ? -boatSprite.ContentSize.Width :
                                                                                           layerWidth + boatSprite.ContentSize.Width));
                    CCEaseSineIn easeInAcrossTo = new CCEaseSineIn(moveAcrossTo);

                    previousWindSpeed = windSpeed * 1.4f;
                    boatMovingSequence = new CCSequence(resetPositionX, moveAcrossTo);
                    boatMovingAnimation = new CCRepeatForever(boatMovingSequence);

                    boatSprite.StopAllActions();
                    boatSprite.RunActions(easeInAcrossTo, boatMovingAnimation);
                }
                else if (previousWindSpeed > 0)
                {
                    float targetPositionX = (windBlowingRight == true ? previousWindSpeed * 7 : previousWindSpeed * -7);
                    CCEaseSineOut easeOutBy = new CCEaseSineOut(new CCMoveBy(previousWindSpeed * 1.2f, new CCPoint(targetPositionX, 0)));
                    CCEaseSineOut easeOutSlowerBy = new CCEaseSineOut(new CCMoveBy(previousWindSpeed * 1.4f, new CCPoint(targetPositionX, 0)));
                    CCEaseSineIn easeInTo = new CCEaseSineIn(new CCMoveTo(0.5f, new CCPoint(boatSprite.PositionX, oceanSprite.PositionY +
                                                                                            boatSprite.ContentSize.Height / 2 - 3)));
                    CCCallFunc resetBoatPositionX = new CCCallFunc(() =>
                    {
                        if ((windBlowingRight == true) &&
                            (boatSprite.PositionX >= layerWidth + boatSprite.ContentSize.Width))
                        {
                            boatSprite.PositionX = -boatSprite.ContentSize.Width;
                        }
                        else if ((windBlowingRight == false) &&
                            (boatSprite.PositionX <= -boatSprite.ContentSize.Width))
                        {
                            boatSprite.PositionX = layerWidth + boatSprite.ContentSize.Width;
                        }
                    });

                    boatMovingSequence = new CCSequence(easeOutBy, resetBoatPositionX, easeOutSlowerBy, easeInTo, boatAnimation);

                    boatSprite.StopAllActions();
                    boatSprite.RunAction(boatMovingSequence);
                }
            }
        }

        void MoveClouds()
        {
            if ((windSpeedChanged == true) &&
                (windSpeed > 0))
            {
                CCRepeatForever cloudsAnimation;
                float targetPositionX = (windBlowingRight == true ? layerWidth + 350 : -350f);
                CCSequence cloudsMovingSequence = new CCSequence(new CCMoveBy(0, new CCPoint(0, 0)));
                CCMoveTo moveAcrossTo = new CCMoveTo(windSpeed * 3f, new CCPoint(targetPositionX, cloudsParallax.PositionY));
                CCEaseSineIn easeInAcrossTo = new CCEaseSineIn(moveAcrossTo);

                cloudsMovingSequence = new CCSequence(moveAcrossTo,
                                                      new CCCallFunc(() => cloudsParallax.PositionX = (windBlowingRight == true ? -350f :
                                                                                                       layerWidth + 350)));
                cloudsAnimation = new CCRepeatForever(cloudsMovingSequence);

                cloudsParallax.StopAllActions();
                cloudsParallax.RunActions(easeInAcrossTo, cloudsAnimation);
            }
            else if (windSpeed < float.Epsilon)
            {
                float targetPositionX = (windBlowingRight == true ? previousWindSpeed * 7 : previousWindSpeed * -7);
                CCEaseSineOut easeOutBy = new CCEaseSineOut(new CCMoveBy(previousWindSpeed * 1.2f, new CCPoint(targetPositionX, 0)));

                cloudsParallax.StopAllActions();
                cloudsParallax.RunAction(easeOutBy);
            }
        }

        void ChangeWindSpeed(bool stopWind)
        {
            windSpeedChanged = true;

            if (stopWind == true)
            {
                windSpeed = 0;
            }
            else
            {
                while ((windSpeed > previousWindSpeed) ||
                       (windSpeed < previousWindSpeed))
                {
                    windSpeed = randomNumber.Next(0, 10);
                }

                if (windSpeed < 5)
                {
                    windSpeed = 0;
                }
                else
                {
                    windBlowingRight = (randomNumber.Next(1, 4) > 2);
                }
            }

            MoveBoat(false);
            MoveClouds();

            windSpeedChanged = false;
        }

        void GetRandomBalloonColor()
        {
            List<string> balloonColors = new List<string>();

            foreach (string imageFile in Directory.GetFiles("./Content/" + this.GameView.ContentManager.SearchPaths[1], "Balloon:*"))
            {
                if (imageFile.Split(':')[1].Split('.')[0] != balloonColor)
                {
                    balloonColors.Add(imageFile.Split(':')[1].Split('.')[0]);
                }
            }

            balloonColor = balloonColors[randomNumber.Next(0, balloonColors.Count)];
        }

        public void ScaleBalloon(float touchForce, float maxTouchForce)
        {
            if (gamePaused == false)
            {
                if (touchForce >= 1)
                {
                    CCScaleTo scaleUpTo = new CCScaleTo(0.25f, 1 + float.Parse((touchForce / 3).ToString()));

                    this.touchForce = touchForce;

                    balloonSprite.RunActions(scaleUpTo,
                                             new CCCallFunc(() =>
                                             {
                                                 if (touchForce >= Math.Floor(double.Parse(maxTouchForce.ToString())))
                                                 {
                                                     this.touchForce = -1;

                                                     ExplodeBalloon();
                                                 }
                                             }));
                }
                else
                {
                    CCScaleTo scaleDownTo = new CCScaleTo(0.25f, 1);

                    balloonSprite.RunAction(scaleDownTo);
                }
            }
        }

        public void LaunchBalloon(CCPoint endSwipeLocation)
        {
            if (gamePaused == false)
            {
                if (forceTouchAvailable == false)
                {
                    DateTime endSwipeTime = DateTime.Now;

                    balloonXVelocity = (float)((endSwipeLocation.X - startSwipeLocation.X) / (endSwipeTime.Subtract(startSwipeTime).TotalMilliseconds));
                    balloonYVelocity = (float)((endSwipeLocation.Y - startSwipeLocation.Y) / (endSwipeTime.Subtract(startSwipeTime).TotalMilliseconds));
                    newBalloonYVelocity = balloonYVelocity;
                    startSwipeLocation = CCPoint.Zero;
                }
                else
                {
                    balloonYVelocity = float.Parse((touchForce / INVERSE_FORCETOUCH_LAUNCH_VELOCITY_CONSTANT).ToString()) * 2;
                    newBalloonYVelocity = balloonYVelocity;
                }

                lastPlayTime = DateTime.Now;
                balloonSprite.UserData = true;
                lastTouchLocation = CCPoint.Zero;
                balloonLaunched = true;

                ControlMenu("Hide");
                PlayBackgroundMusic();
            }
        }

        void ExplodeBalloon()
        {
            CCSequence explosionSequence;
            CCColor4B explosionColor = new CCColor4B(byte.Parse(balloonColor.Split(',')[0]), byte.Parse(balloonColor.Split(',')[1]),
                                                     byte.Parse(balloonColor.Split(',')[2]), 255);

            explosionSequence = new CCSequence(new CCScaleTo(0.6f, 100),
            new CCCallFunc(() => Color = new CCColor3B(explosionColor)),
            new CCCallFunc(() => UpdateColor()),
            new CCCallFunc(() =>
            {
                AddBalloon();
                ResetStopwatch();

                explosionSprite.Visible = false;

                explosionSprite.RunAction(new CCScaleTo(0.1f, 1));
            }));

            explosionSprite.Color = new CCColor3B(explosionColor);
            explosionSprite.Position = balloonSprite.Position;
            explosionSprite.Visible = true;

            explosionSprite.UpdateColor();
            philipsHue.SetBulbColor(int.Parse(explosionColor.R.ToString()), int.Parse(explosionColor.G.ToString()),
                                    int.Parse(explosionColor.B.ToString()));
            StopBackgroundMusic();
            ControlSound(false, BALLOON_POP_SOUND_FILE, "Play", SOUND_EFFECT_VOLUME);
            RemoveBalloon();

            explosionSprite.RunAction(explosionSequence);
        }

        void SetStopwatch()
        {
            ((CCLabel)stopwatchSprite.GetChildByTag(1)).Text = DateTime.Now.Subtract(lastPlayTime).Hours.ToString().PadLeft(2, '0') + ":" +
                DateTime.Now.Subtract(lastPlayTime).Minutes.ToString().PadLeft(2, '0') + ":" + DateTime.Now.Subtract(lastPlayTime).Seconds.ToString().PadLeft(2, '0');
        }

        void ResetStopwatch()
        {
            if (((CCLabel)stopwatchSprite.GetChildByTag(1)).Text != "00:00:00")
            {
                CCEaseIn shakeIn;
                CCEaseOut shakeOut;
                CCEaseIn shakeBackIn;
                CCEaseOut shakeBackOut;
                CCCallFunc resetFunction;
                CCSequence motionSequence;

                shakeIn = new CCEaseIn(new CCMoveBy(0.05f, new CCPoint(15, 0)), 1f);
                shakeOut = new CCEaseOut(new CCMoveBy(0.05f, new CCPoint(-30, 0)), 1f);
                resetFunction = new CCCallFunc(() => ((CCLabel)stopwatchSprite.GetChildByTag(1)).Text = "00:00:00");
                shakeBackIn = new CCEaseIn(new CCMoveBy(0.1f, new CCPoint(30, 0)), 1f);
                shakeBackOut = new CCEaseOut(new CCMoveBy(0.1f, new CCPoint(-15, 0)), 1f);
                motionSequence = new CCSequence(shakeIn, shakeOut, resetFunction, shakeBackIn, shakeBackOut);

                stopwatchSprite.RunAction(motionSequence);
            }
        }

        bool CheckForExplosion()
        {
            bool explodeBalloon = false;

            if (explosionSprite.Visible == false)
            {
                if ((balloonSprite.Position.X <= -balloonSprite.ContentSize.Width) ||
                    (balloonSprite.Position.X >= layerWidth + balloonSprite.ContentSize.Width))
                {
                    explodeBalloon = true;
                }
                else if ((balloonSprite.Position.Y <= 0) ||
                    (balloonSprite.Position.Y >= layerHeight + balloonSprite.ContentSize.Height))
                {
                    explodeBalloon = true;
                }
            }

            return explodeBalloon;
        }

        void UpdateDebugData()
        {
            if (dataLabel != null)
            {
                dataLabel.Text = "Wind Speed: " + windSpeed.ToString() +
                    "\r\nWind Direction: " + (windBlowingRight == true ? "Right" : "Left") +
                    "\r\nBalloon X: " + Math.Round(balloonSprite.PositionX, 2) +
                    "\r\nBalloon Y: " + Math.Round(balloonSprite.PositionY, 2) +
                    "\r\nBalloon Vel. X: " + Math.Round(balloonXVelocity, 2) +
                    "\r\nBalloon Vel. Y: " + Math.Round(balloonYVelocity, 2) +
                    "\r\nTouch Loc. X: " + Math.Round(lastTouchLocation.X, 2) +
                    "\r\nTouch Loc. Y: " + Math.Round(lastTouchLocation.Y, 2) +
                    "\r\nTouch Force: " + Math.Round(touchForce, 2);
            }
        }

        void RunGameLogic(float frameTimeInSeconds)
        {
            try
            {
                if (gamePaused == false)
                {
                    frameTime = frameTimeInSeconds;

                    if ((balloonLaunched == true) &&
                        (explosionSprite.Visible == false) &&
                        (balloonSprite.Parent != null) &&
                        (balloonSprite.PositionY >= 0) &&
                        ((balloonSprite.PositionY - balloonSprite.ContentSize.Height) <= layerHeight) &&
                        (balloonSprite.PositionX + balloonSprite.ContentSize.Width >= 0) &&
                        ((balloonSprite.PositionX - balloonSprite.ContentSize.Width) <= layerWidth))
                    {
                        // If balloon was just launched, wobble it and perform other operations.
                        if ((bool)balloonSprite.UserData == true)
                        {
                            if (touchForce >= 4)
                            {
                                CCRotateBy rotateBackBy = new CCRotateBy(0.12f, -20);
                                CCRotateBy rotateForthBy = new CCRotateBy(0.12f, 20);
                                CCRotateBy rotateBackFasterBy = new CCRotateBy(0.06f, -10);
                                CCRotateBy rotateForthFasterBy = new CCRotateBy(0.06f, 10);
                                CCSequence launchSequence = new CCSequence(rotateBackBy, rotateForthBy, rotateForthBy, rotateBackBy, rotateBackFasterBy, rotateForthFasterBy,
                                                                           rotateForthFasterBy, new CCRotateTo(0.25f, 0));

                                balloonSprite.RunAction(launchSequence);

                                balloonSprite.UserData = false;
                            }

                            touchForce = 0;
                        }

                        newBalloonYVelocity -= frameTimeInSeconds * GAME_GRAVITY * 1000;
                        balloonSprite.PositionX += balloonXVelocity * frameTimeInSeconds * 1000 + (windBlowingRight == true ? 1 : -1) * windSpeed * frameTimeInSeconds * 100;
                        balloonSprite.PositionY -= ((newBalloonYVelocity * newBalloonYVelocity) - (balloonYVelocity * balloonYVelocity)) / GAME_GRAVITY;
                        balloonYVelocity = newBalloonYVelocity;

                        if (CheckForExplosion() == true)
                        {
                            ExplodeBalloon();
                        }
                        else
                        {
                            if ((DateTime.Now.Subtract(lastGameVariationTime).TotalSeconds > GAME_VARIATION_FREQUENCY))
                            {
                                ChangeWindSpeed(false);
                            }

                            SetStopwatch();
                        }
                    }
                    else if ((balloonLaunched == false) &&
                             (balloonSprite.PositionY <= 0) &&
                             (balloonSprite.Parent != null) &&
                             ((DateTime.Now.Subtract(lastPlayTime).TotalSeconds > REMINDER_ALERT_DURATION)))
                    {
                        Task balloonReminderTask = new Task(async () =>
                        {
                            await balloonSprite.RunActionsAsync(new CCScaleTo(0.10f, 1.5f), new CCScaleTo(0.10f, 1f));
                            await balloonSprite.RunActionsAsync(new CCScaleTo(0.10f, 1.5f), new CCScaleTo(0.10f, 1f));
                        });

                        lastPlayTime = DateTime.Now;

                        balloonReminderTask.Start();
                    }
                }

                UpdateDebugData();
            }
            catch (Exception runGameLogicException)
            {
                errorLog += "\r\n\r\n" + DateTime.Now.ToString() + ": " + runGameLogicException;
            }
        }

        void AddTouchEvents()
        {
            var ccTouchListener = new CCEventListenerTouchAllAtOnce();

            ccTouchListener.OnTouchesBegan = OnCCTouchesBegan;
            ccTouchListener.OnTouchesEnded = OnCCTouchesEnded;
            ccTouchListener.OnTouchesMoved = OnCCTouchesMoved;

            AddEventListener(ccTouchListener, this);
        }

        void OnCCTouchesBegan(List<CCTouch> touches, CCEvent touchEvent)
        {
            if (gamePaused == false)
            {
                if (balloonLaunched == false)
                {
                    lastTouchLocation = CCPoint.Zero;
                }

                if ((touches.Count > 0) &&
                    (forceTouchAvailable == false) &&
                    (balloonLaunched == false) &&
                    (touches[0].Location.X >= balloonSprite.BoundingBox.MinX) &&
                    (touches[0].Location.X <= balloonSprite.BoundingBox.MaxX) &&
                    (touches[0].Location.Y >= balloonSprite.BoundingBox.MinY) &&
                    (touches[0].Location.Y <= balloonSprite.BoundingBox.MaxY))
                {
                    startSwipeTime = DateTime.Now;
                    startSwipeLocation = touches[0].Location;

                    balloonSprite.RunAction(new CCScaleTo(0.10f, 1.5f));
                }
            }
        }

        void OnCCTouchesMoved(List<CCTouch> touches, CCEvent touchEvent)
        {
            if (gamePaused == false)
            {
                if ((touches.Count > 0) &&
                    (forceTouchAvailable == false) &&
                    (startSwipeLocation.Equals(CCPoint.Zero) == false))
                {
                    balloonMoved = true;
                }
            }
        }

        void OnCCTouchesEnded(List<CCTouch> touches, CCEvent touchEvent)
        {
            if (touches.Count > 0)
            {
                if (gamePaused == false)
                {
                    if (balloonLaunched == true)
                    {
                        lastTouchLocation = touches[0].Location;
                    }
                    else
                    {
                        balloonSprite.RunAction(new CCScaleTo(0.25f, 1f));
                    }

                    if ((forceTouchAvailable == false) &&
                        (balloonLaunched == false) &&
                        (balloonMoved == true) &&
                        (startSwipeLocation.Equals(CCPoint.Zero) == false))
                    {
                        LaunchBalloon(touches[0].Location);
                    }
                    else if ((forceTouchAvailable == true) &&
                             (balloonLaunched == false) &&
                             (touchForce >= 1) &&
                             (touches[0].Location.X >= balloonSprite.BoundingBox.MinX) &&
                             (touches[0].Location.X <= balloonSprite.BoundingBox.MaxX) &&
                             (touches[0].Location.Y >= balloonSprite.BoundingBox.MinY) &&
                             (touches[0].Location.Y <= balloonSprite.BoundingBox.MaxY))
                    {
                        LaunchBalloon(CCPoint.Zero);
                    }
                    else if ((forceTouchAvailable == true) &&
                             (balloonLaunched == false) &&
                             (touchForce >= 4) &&
                             (touches[0].Location.X >= 0) &&
                             (touches[0].Location.X <= 45) &&
                             (touches[0].Location.Y >= layerHeight - 45) &&
                             (touches[0].Location.Y <= layerHeight))
                    {
                        ToggleDebugData();
                    }
                    else if (balloonLaunched == true)
                    {
                        bool balloonTapped = false;

                        // If tapped to the left of the balloon, move balloon to the right and vice versa.
                        if ((lastTouchLocation.X > 0) &&
                            (lastTouchLocation.X < balloonSprite.PositionX) &&
                            (lastTouchLocation.X > balloonSprite.PositionX - balloonSprite.ContentSize.Width / 2 - 30))
                        {
                            balloonXVelocity += 0.05f;
                            balloonTapped = true;
                        }
                        else if ((lastTouchLocation.X > 0) &&
                            (lastTouchLocation.X > balloonSprite.PositionX) &&
                            (lastTouchLocation.X < balloonSprite.PositionX + balloonSprite.ContentSize.Width / 2 + 30))
                        {
                            balloonXVelocity -= 0.05f;
                            balloonTapped = true;
                        }

                        // If tapped to the top of the balloon, move balloon down and vice versa.
                        if ((lastTouchLocation.Y > 0) &&
                            (lastTouchLocation.Y < balloonSprite.PositionY) &&
                            (lastTouchLocation.Y > balloonSprite.PositionY - balloonSprite.ContentSize.Height / 2 - 30))
                        {
                            balloonYVelocity = 0.06f;
                            newBalloonYVelocity = 0.06f; //float.Parse(Math.Sqrt(0.001 + double.Parse((balloonYVelocity * balloonYVelocity).ToString())).ToString());
                            balloonTapped = true;
                        }
                        else if ((lastTouchLocation.Y > 0) &&
                            (lastTouchLocation.Y > balloonSprite.PositionY) &&
                            (lastTouchLocation.Y < balloonSprite.PositionY + balloonSprite.ContentSize.Height / 2 + 30))
                        {
                            balloonYVelocity = -0.06f;
                            newBalloonYVelocity = -0.06f;
                            balloonTapped = true;
                        }

                        if (balloonTapped == true)
                        {
                            philipsHue.FlashLights();
                            ControlSound(false, BALLOON_TAP_SOUND_FILE, "Play", SOUND_EFFECT_VOLUME);
                        }

                        lastTouchLocation = CCPoint.Zero;
                    }
                }
                else
                {
                    ControlMenu("Collapse");
                    ResumeBackgroundMusic();

                    gamePaused = false;
                }
            }
        }
    }
}